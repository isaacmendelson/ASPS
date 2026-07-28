"""Red/green tests for pinned browser egress and context-wide interception."""

import socket
import threading
import time
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

import pytest

from scrapers.egress_proxy import PinnedEgressProxy
from scrapers.playwright_scraper import PlaywrightScraper
from utils.validators import URLSecurityPolicy, UnsafeURLException


def resolver_for(address):
    def resolve(host, port, type=socket.SOCK_STREAM):
        return [(socket.AF_INET, type, 6, "", (address, port))]

    return resolve


def test_proxy_connects_to_validated_ip_not_hostname(monkeypatch):
    connected = []
    sentinel = object()
    proxy = PinnedEgressProxy(
        URLSecurityPolicy(resolver_for("93.184.216.34")),
        connection_factory=(
            lambda destination, timeout: connected.append(destination) or sentinel
        ),
    )

    assert proxy.connect_public("rebind.test", 443) is sentinel
    assert connected == [("93.184.216.34", 443)]


def test_proxy_never_connects_when_resolution_is_private(monkeypatch):
    proxy = PinnedEgressProxy(URLSecurityPolicy(resolver_for("127.0.0.1")))
    monkeypatch.setattr(
        socket,
        "create_connection",
        lambda *args, **kwargs: pytest.fail("private destination was connected"),
    )

    with pytest.raises(UnsafeURLException, match="non-public"):
        proxy.connect_public("rebind.test", 443)


def test_public_http_succeeds_through_loopback_proxy_but_private_never_connects():
    class Handler(BaseHTTPRequestHandler):
        def do_GET(self):
            self.send_response(200)
            self.end_headers()
            self.wfile.write(b"public-through-pinned-proxy")

        def log_message(self, *args):
            pass

    origin = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
    thread = threading.Thread(target=origin.serve_forever, daemon=True)
    thread.start()
    connects = []

    def connect_exact(destination, timeout):
        connects.append(destination)
        return socket.create_connection(
            ("127.0.0.1", origin.server_address[1]),
            timeout=timeout,
        )

    proxy = PinnedEgressProxy(
        URLSecurityPolicy(resolver_for("93.184.216.34")),
        connection_factory=connect_exact,
    )
    proxy_url = proxy.start()
    try:
        opener = urllib.request.build_opener(
            urllib.request.ProxyHandler({"http": proxy_url})
        )
        assert (
            opener.open("http://public.test/resource", timeout=5).read()
            == b"public-through-pinned-proxy"
        )
        assert connects == [("93.184.216.34", 80)]

        proxy.security_policy = URLSecurityPolicy(resolver_for("127.0.0.1"))
        with pytest.raises(Exception):
            opener.open("http://private.test/metadata", timeout=5)
        assert connects == [("93.184.216.34", 80)]
    finally:
        proxy.close()
        origin.shutdown()
        origin.server_close()


@pytest.mark.parametrize(
    "address",
    [
        "224.0.0.1",
        "239.255.255.250",
        "240.0.0.1",
        "255.255.255.255",
        "192.0.0.170",
        "2001:db8::1",
        "fec0::1",
        "ff02::1",
        "100::",
        # 6to4: 2002::/16 embeds IPv4 in bits 16-47; blocks even though
        # ipaddress.is_global returns True for these addresses.
        "2002:7f00:0001::",
        "2002:c0a8:0101::",
    ],
)
def test_multicast_reserved_and_special_purpose_addresses_are_explicitly_blocked(
    address,
):
    with pytest.raises(UnsafeURLException, match="non-public"):
        URLSecurityPolicy().validate_connected_address(address)


def test_context_wide_guards_cover_popups_and_websockets():
    registrations = []

    class FakeContext:
        def route(self, pattern, handler):
            registrations.append(("route", pattern, handler))

        def route_web_socket(self, pattern, handler):
            registrations.append(("websocket", pattern, handler))

        def on(self, event, handler):
            registrations.append((event, None, handler))

    scraper = PlaywrightScraper()
    scraper._install_context_security(FakeContext())

    assert [kind for kind, _, _ in registrations] == [
        "route",
        "websocket",
        "response",
    ]


def test_popup_request_to_metadata_is_aborted_by_context_guard():
    registrations = {}

    class FakeContext:
        def route(self, pattern, handler):
            registrations["route"] = handler

        def route_web_socket(self, pattern, handler):
            registrations["websocket"] = handler

        def on(self, event, handler):
            registrations[event] = handler

    class FakeRoute:
        class Request:
            url = "http://169.254.169.254/latest/meta-data/"

        request = Request()
        aborted = False

        def abort(self, reason):
            self.aborted = reason

        def continue_(self):
            pytest.fail("metadata popup request was allowed")

    scraper = PlaywrightScraper()
    scraper._install_context_security(FakeContext())
    popup_route = FakeRoute()
    registrations["route"](popup_route)

    assert popup_route.aborted == "blockedbyclient"


def test_popup_websocket_to_private_host_is_closed_before_connect():
    registrations = {}

    class FakeContext:
        def route(self, pattern, handler):
            registrations["route"] = handler

        def route_web_socket(self, pattern, handler):
            registrations["websocket"] = handler

        def on(self, event, handler):
            registrations[event] = handler

    class FakeWebSocket:
        url = "ws://127.0.0.1:8080/control"
        messages_handler = None

        def connect_to_server(self):
            pytest.fail("private WebSocket was connected")

        def on_message(self, handler):
            self.messages_handler = handler

    scraper = PlaywrightScraper()
    scraper._install_context_security(FakeContext())
    popup_socket = FakeWebSocket()
    started = time.monotonic()
    registrations["websocket"](popup_socket)
    elapsed = time.monotonic() - started

    assert elapsed < 0.1
    assert popup_socket.messages_handler is not None

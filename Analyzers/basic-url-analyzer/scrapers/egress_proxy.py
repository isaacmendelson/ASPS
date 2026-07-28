"""Loopback filtering proxy that resolves once and connects to the validated IP."""

import select
import socket
import socketserver
import threading
from urllib.parse import urlsplit

from utils.validators import URLSecurityPolicy


class _ProxyHandler(socketserver.StreamRequestHandler):
    timeout = 15

    def handle(self):
        first_line = self.rfile.readline(65537)
        if not first_line or len(first_line) > 65536:
            return
        try:
            method, target, version = first_line.decode("iso-8859-1").strip().split()
        except ValueError:
            return

        headers = []
        while True:
            line = self.rfile.readline(65537)
            if not line or line in (b"\r\n", b"\n"):
                break
            headers.append(line)

        try:
            if method.upper() == "CONNECT":
                host, port = self._parse_authority(target, 443)
                upstream = self.server.egress_proxy.connect_public(host, port)
                self.wfile.write(b"HTTP/1.1 200 Connection Established\r\n\r\n")
            else:
                parsed = urlsplit(target)
                host = parsed.hostname
                if not host:
                    return
                port = parsed.port or (443 if parsed.scheme == "https" else 80)
                upstream = self.server.egress_proxy.connect_public(host, port)
                path = parsed.path or "/"
                if parsed.query:
                    path += "?" + parsed.query
                upstream.sendall(
                    f"{method} {path} {version}\r\n".encode("iso-8859-1")
                    + b"".join(headers)
                    + b"\r\n"
                )
            self._relay(self.connection, upstream)
        except Exception:
            try:
                self.wfile.write(b"HTTP/1.1 403 Forbidden\r\nConnection: close\r\n\r\n")
            except OSError:
                pass
        finally:
            if "upstream" in locals():
                upstream.close()

    @staticmethod
    def _parse_authority(authority, default_port):
        if authority.startswith("["):
            host, _, remainder = authority[1:].partition("]")
            port = int(remainder[1:]) if remainder.startswith(":") else default_port
            return host, port
        host, separator, port_text = authority.rpartition(":")
        return (host, int(port_text)) if separator else (authority, default_port)

    @staticmethod
    def _relay(client, upstream):
        sockets = (client, upstream)
        while True:
            readable, _, _ = select.select(sockets, (), (), 15)
            if not readable:
                return
            for source in readable:
                data = source.recv(65536)
                if not data:
                    return
                destination = upstream if source is client else client
                destination.sendall(data)


class _ThreadingProxyServer(socketserver.ThreadingTCPServer):
    allow_reuse_address = True
    daemon_threads = True


class PinnedEgressProxy:
    """A local proxy whose outbound socket uses the exact validated DNS answer."""

    def __init__(self, security_policy=None, connection_factory=None):
        self.security_policy = security_policy or URLSecurityPolicy()
        self._connection_factory = connection_factory or socket.create_connection
        self._server = None
        self._thread = None

    def connect_public(self, hostname, port):
        addresses = sorted(
            self.security_policy.resolve_public_addresses(hostname, port)
        )
        last_error = None
        for address in addresses:
            try:
                return self._connection_factory((address, port), timeout=15)
            except OSError as exc:
                last_error = exc
        raise last_error or OSError("No public destination address was available")

    def start(self):
        self._server = _ThreadingProxyServer(("127.0.0.1", 0), _ProxyHandler)
        self._server.egress_proxy = self
        self._thread = threading.Thread(
            target=self._server.serve_forever,
            name="analyzer-egress-proxy",
            daemon=True,
        )
        self._thread.start()
        return f"http://127.0.0.1:{self._server.server_address[1]}"

    def close(self):
        if self._server:
            self._server.shutdown()
            self._server.server_close()
            self._server = None
        if self._thread:
            self._thread.join(timeout=5)
            self._thread = None

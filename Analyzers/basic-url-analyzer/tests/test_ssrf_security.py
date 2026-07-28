"""Security regression tests for URL destinations used by the browser scraper."""

import socket

import pytest

from scrapers import playwright_scraper
from scrapers.playwright_scraper import PlaywrightScraper
from utils.validators import URLSecurityPolicy, URLValidator, UnsafeURLException


def resolver_for(*addresses):
    def resolve(host, port, type=socket.SOCK_STREAM):
        family = socket.AF_INET6 if ":" in addresses[0] else socket.AF_INET
        return [(family, type, 6, "", (address, port)) for address in addresses]

    return resolve


@pytest.mark.parametrize(
    "url",
    [
        "http://127.0.0.1/",
        "http://10.10.0.1/",
        "http://172.16.0.1/",
        "http://192.168.1.1/",
        "http://169.254.169.254/latest/meta-data/",
        "http://0.0.0.0/",
        "http://[::1]/",
        "http://[fe80::1]/",
        "http://[fc00::1]/",
        "http://192.0.2.10/",
        # 6to4 addresses embed an IPv4 address in bits 16-47 of the IPv6 address.
        # 2002:7f00:0001:: encodes 127.0.0.1; 2002:c0a8:0101:: encodes 192.168.1.1.
        # Python's ipaddress.is_global returns True for these, so we block them
        # explicitly with the 2002::/16 prefix check.
        "http://[2002:7f00:0001::]/",
        "http://[2002:c0a8:0101::]/",
        "http://[2002:a9fe:a9fe::]/",
    ],
)
def test_ip_literals_outside_public_internet_are_blocked(url):
    with pytest.raises(UnsafeURLException, match="non-public"):
        URLSecurityPolicy().validate_destination(url)


def test_all_dns_answers_must_be_public():
    policy = URLSecurityPolicy(resolver_for("93.184.216.34", "169.254.169.254"))
    with pytest.raises(UnsafeURLException, match="non-public"):
        policy.validate_destination("https://example.test/")


def test_public_dns_destination_is_allowed():
    policy = URLSecurityPolicy(resolver_for("93.184.216.34"))
    assert policy.validate_destination("https://example.test/") == frozenset(
        {"93.184.216.34"}
    )


def test_dns_rebinding_to_private_address_is_blocked_on_revalidation():
    answers = iter(("93.184.216.34", "127.0.0.1"))

    def rebinding_resolver(host, port, type=socket.SOCK_STREAM):
        address = next(answers)
        return [(socket.AF_INET, type, 6, "", (address, port))]

    policy = URLSecurityPolicy(rebinding_resolver)
    policy.validate_destination("https://rebind.test/start")
    with pytest.raises(UnsafeURLException, match="non-public"):
        policy.validate_destination("https://rebind.test/redirect")


def test_redirect_target_is_revalidated_and_private_target_is_blocked():
    def resolve(host, port, type=socket.SOCK_STREAM):
        address = "93.184.216.34" if host == "public.test" else "10.0.0.8"
        return [(socket.AF_INET, type, 6, "", (address, port))]

    policy = URLSecurityPolicy(resolve)
    policy.validate_destination("https://public.test/")
    with pytest.raises(UnsafeURLException, match="non-public"):
        policy.validate_destination("http://internal.test/admin")


def test_credentials_are_rejected_before_navigation():
    with pytest.raises(UnsafeURLException, match="credentials"):
        URLSecurityPolicy(resolver_for("93.184.216.34")).validate_destination(
            "https://user:password@example.test/"
        )


def test_url_validator_applies_security_policy():
    valid, _, error = URLValidator.validate(
        "http://metadata.test/",
        URLSecurityPolicy(resolver_for("169.254.169.254")),
    )
    assert not valid
    assert "non-public" in error


def test_chromium_reported_peer_is_validated():
    policy = URLSecurityPolicy(resolver_for("93.184.216.34"))
    with pytest.raises(UnsafeURLException, match="non-public"):
        policy.validate_connected_address("127.0.0.1")


def test_scraper_launch_keeps_chromium_security_boundaries_enabled(monkeypatch):
    launched = {}

    class FakeBrowser:
        def close(self):
            pass

    class FakeChromium:
        def launch(self, **kwargs):
            launched.update(kwargs)
            return FakeBrowser()

    class FakePlaywright:
        chromium = FakeChromium()

        def stop(self):
            pass

    class FakeStarter:
        def start(self):
            return FakePlaywright()

    monkeypatch.setattr(playwright_scraper, "sync_playwright", lambda: FakeStarter())
    monkeypatch.setattr(
        PlaywrightScraper,
        "_fetch_with_ua",
        lambda self, url, user_agent: {
            "success": True,
            "html": "<html></html>",
            "status_code": 200,
            "final_url": url,
            "word_count": 0,
            "error": "",
            "_blocked": False,
        },
    )

    result = PlaywrightScraper().fetch("https://example.test/")

    assert result["success"]
    arguments = launched["args"]
    assert launched["proxy"]["bypass"] == "<-loopback>"
    assert "--no-sandbox" not in arguments
    assert "--disable-web-security" not in arguments
    assert not any("IsolateOrigins" in argument for argument in arguments)


def test_scraper_revalidates_redirect_url_through_shared_policy():
    checked = []
    scraper = PlaywrightScraper()

    class RecordingPolicy:
        def validate_destination(self, url):
            checked.append(url)

    scraper.security_policy = RecordingPolicy()
    scraper._authorize_browser_url("https://public.test/start")
    scraper._authorize_browser_url("http://redirect.test/final")

    assert checked == [
        "https://public.test/start",
        "http://redirect.test/final",
    ]

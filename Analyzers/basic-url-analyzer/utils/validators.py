"""
URL validation utilities
"""

import ipaddress
import socket
import validators
from urllib.parse import urlparse
from typing import Callable, FrozenSet, Optional, Tuple, Union


class UnsafeURLException(ValueError):
    """Raised when a URL could reach a non-public network destination."""


class URLSecurityPolicy:
    """Resolve and reject URL destinations outside the public Internet."""

    def __init__(
        self,
        resolver: Optional[Callable[..., list]] = None,
    ):
        self._resolver = resolver or socket.getaddrinfo
        self._observed_addresses = {}

    @staticmethod
    def _validate_ip(
        address: str,
    ) -> Union[ipaddress.IPv4Address, ipaddress.IPv6Address]:
        ip = ipaddress.ip_address(address.split("%", 1)[0])
        if (
            not ip.is_global
            or ip.is_multicast
            or ip.is_reserved
            or ip.is_unspecified
            or ip.is_loopback
            or ip.is_link_local
            or ip.is_private
            or (isinstance(ip, ipaddress.IPv6Address) and ip.is_site_local)
            or (
                isinstance(ip, ipaddress.IPv6Address)
                and ip in ipaddress.IPv6Network("2002::/16")
            )
        ):
            raise UnsafeURLException(
                f"Destination resolves to a non-public address ({ip.compressed})"
            )
        return ip

    def resolve_public_addresses(self, hostname: str, port: int) -> FrozenSet[str]:
        """Resolve a host and require every returned address to be globally routable."""
        try:
            literal = ipaddress.ip_address(hostname.split("%", 1)[0])
        except ValueError:
            literal = None

        if literal is not None:
            return frozenset((self._validate_ip(str(literal)).compressed,))

        try:
            answers = self._resolver(
                hostname,
                port,
                type=socket.SOCK_STREAM,
            )
        except (socket.gaierror, OSError) as exc:
            raise UnsafeURLException(f"Destination DNS resolution failed: {exc}") from exc

        addresses = frozenset(
            self._validate_ip(answer[4][0]).compressed for answer in answers
        )
        if not addresses:
            raise UnsafeURLException("Destination DNS resolution returned no addresses")

        # Remember all observations. A later public-to-private change is rejected above;
        # retaining the observations also makes rebinding visible to callers/tests.
        self._observed_addresses.setdefault(hostname.lower(), set()).update(addresses)
        return addresses

    def validate_destination(self, url: str) -> FrozenSet[str]:
        """Validate an HTTP(S) destination and return its public DNS addresses."""
        parsed = urlparse(url)
        if parsed.scheme.lower() not in ("http", "https"):
            raise UnsafeURLException("Only HTTP and HTTPS destinations are allowed")
        if parsed.username is not None or parsed.password is not None:
            raise UnsafeURLException("Destination URLs must not contain credentials")
        if not parsed.hostname:
            raise UnsafeURLException("Destination URL is missing a hostname")

        try:
            port = parsed.port or (443 if parsed.scheme.lower() == "https" else 80)
        except ValueError as exc:
            raise UnsafeURLException("Destination URL has an invalid port") from exc
        return self.resolve_public_addresses(parsed.hostname.rstrip("."), port)

    def validate_connected_address(self, address: str) -> None:
        """Validate the peer address reported by Chromium after connection."""
        self._validate_ip(address)


class URLValidator:
    """Validates and normalizes URLs"""
    
    # Browser internal URL schemes that should be rejected
    BROWSER_INTERNAL_SCHEMES = (
        'chrome://', 'chrome-extension://',
        'about:', 'edge://', 'brave://',
        'firefox://', 'opera://', 'vivaldi://',
        'file://', 'data:', 'javascript:',
        'blob:', 'ws://', 'wss://'
    )

    @staticmethod
    def validate(
        url: str,
        security_policy: Optional[URLSecurityPolicy] = None,
    ) -> Tuple[bool, str, str]:
        """
        Validate URL format

        Args:
            url: URL to validate

        Returns:
            Tuple of (is_valid, cleaned_url, error_message)
        """
        # Reject browser internal URLs
        url_lower = url.lower().strip()
        for scheme in URLValidator.BROWSER_INTERNAL_SCHEMES:
            if url_lower.startswith(scheme):
                return False, url, f"Browser internal URL ({scheme.rstrip(':/')})"

        # Add protocol if missing
        if not url.startswith(('http://', 'https://')):
            url = 'https://' + url
        
        # Validate format
        if not validators.url(url):
            return False, url, "Invalid URL format"
        
        # Parse URL
        try:
            parsed = urlparse(url)
            if not parsed.netloc or not parsed.hostname:
                return False, url, "Missing domain name"

            try:
                (security_policy or URLSecurityPolicy()).validate_destination(url)
            except UnsafeURLException as exc:
                return False, url, str(exc)

            return True, url, ""
        
        except Exception as e:
            return False, url, f"URL parsing error: {str(e)}"
    
    @staticmethod
    def extract_domain(url: str) -> str:
        """
        Extract domain from URL
        
        Args:
            url: Full URL
        
        Returns:
            Domain name
        """
        try:
            parsed = urlparse(url)
            domain = parsed.netloc
            
            # Remove www. if present
            if domain.startswith('www.'):
                domain = domain[4:]
            
            return domain
        
        except Exception:
            return ""

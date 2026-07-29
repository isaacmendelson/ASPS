"""
Remote Monitor — GeoIP Lookup

Lightweight GeoIP helper using the free ip-api.com service.
Extracted from remote_monitor.py as part of the ASPS-627 split.
"""

import json
import logging
import urllib.request
from typing import Dict

logger = logging.getLogger(__name__)


class GeoIPLookup:
    """GeoIP lookup using free ip-api.com service."""

    _cache: Dict[str, dict] = {}

    @classmethod
    def lookup(cls, ip: str) -> dict:
        """
        Lookup GeoIP info for an IP address.
        Returns: {"country": "...", "country_code": "...", "city": "...", "isp": "..."}
        """
        if not ip or ip == "?" or cls._is_private_ip(ip):
            return {}

        if ip in cls._cache:
            return cls._cache[ip]

        try:
            url = f"http://ip-api.com/json/{ip}?fields=status,country,countryCode,city,isp"
            with urllib.request.urlopen(url, timeout=3) as resp:
                data = json.loads(resp.read().decode())
                if data.get("status") == "success":
                    result = {
                        "country": data.get("country", ""),
                        "country_code": data.get("countryCode", ""),
                        "city": data.get("city", ""),
                        "isp": data.get("isp", ""),
                    }
                    cls._cache[ip] = result
                    return result
        except Exception as e:
            logger.debug(f"GeoIP lookup failed for {ip}: {e}")

        return {}

    @staticmethod
    def _is_private_ip(ip: str) -> bool:
        """Check if IP is private/local."""
        return (
            ip.startswith("10.") or
            ip.startswith("192.168.") or
            ip.startswith("172.16.") or
            ip.startswith("172.17.") or
            ip.startswith("172.18.") or
            ip.startswith("172.19.") or
            ip.startswith("172.2") or
            ip.startswith("172.30.") or
            ip.startswith("172.31.") or
            ip.startswith("127.") or
            ip.startswith("100.") or  # Tailscale
            ip == "::1"
        )

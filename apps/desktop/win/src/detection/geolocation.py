"""IP Geolocation using GeoLite2 database."""
import os
import logging
from pathlib import Path
from typing import Optional, Dict

logger = logging.getLogger(__name__)

# Try to import geoip2, gracefully handle if not installed
try:
    import geoip2.database
    import geoip2.errors
    GEOIP_AVAILABLE = True
except ImportError:
    GEOIP_AVAILABLE = False
    logger.info("geoip2 not installed. IP geolocation disabled.")

# Default database path
GEOLITE2_DB = Path(__file__).parent.parent / 'data' / 'GeoLite2-Country.mmdb'


class GeoLocator:
    """IP geolocation with caching."""

    def __init__(self, db_path: Optional[Path] = None):
        self._db_path = db_path or GEOLITE2_DB
        self._reader = None
        self._cache: Dict[str, Dict[str, str]] = {}  # IP -> country dict

    def get_country(self, ip: str) -> Dict[str, str]:
        """
        Get country info for IP.

        Returns: {'country': str, 'country_code': str}
        """
        if not GEOIP_AVAILABLE:
            return {'country': 'Unknown', 'country_code': 'XX'}

        if ip in self._cache:
            return self._cache[ip]

        # Skip private IPs
        if self._is_private_ip(ip):
            result = {'country': 'Private', 'country_code': 'XX'}
            self._cache[ip] = result
            return result

        if not self._reader:
            try:
                self._reader = geoip2.database.Reader(str(self._db_path))
            except FileNotFoundError:
                logger.debug(f"GeoLite2 database not found at {self._db_path}")
                return {'country': 'Unknown', 'country_code': 'XX'}
            except Exception as e:
                logger.debug(f"Failed to open GeoLite2 database: {e}")
                return {'country': 'Unknown', 'country_code': 'XX'}

        try:
            response = self._reader.country(ip)
            result = {
                'country': response.country.name or 'Unknown',
                'country_code': response.country.iso_code or 'XX'
            }
        except Exception as e:
            # Handles AddressNotFoundError and other errors
            logger.debug(f"Geolocation error for {ip}: {e}")
            result = {'country': 'Unknown', 'country_code': 'XX'}

        self._cache[ip] = result
        return result

    def _is_private_ip(self, ip: str) -> bool:
        """Check if IP is private/local."""
        if ip.startswith('127.') or ip.startswith('10.'):
            return True
        if ip.startswith('192.168.'):
            return True
        if ip.startswith('172.'):
            # Check 172.16-31.x.x range
            try:
                second_octet = int(ip.split('.')[1])
                if 16 <= second_octet <= 31:
                    return True
            except (IndexError, ValueError):
                pass
        if ip == '::1' or ip.startswith('fe80:'):
            return True
        return False

    def close(self):
        """Close database reader."""
        if self._reader:
            try:
                self._reader.close()
            except Exception as e:
                logger.debug(f"Error closing GeoIP reader: {e}")
            self._reader = None


# Singleton instance
_geolocator: Optional[GeoLocator] = None


def get_geolocator() -> GeoLocator:
    """Get singleton GeoLocator instance."""
    global _geolocator
    if _geolocator is None:
        _geolocator = GeoLocator()
    return _geolocator

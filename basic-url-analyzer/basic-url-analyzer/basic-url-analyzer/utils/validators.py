"""
URL validation utilities
"""

import re
import validators
from urllib.parse import urlparse
from typing import Tuple


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
    def validate(url: str) -> Tuple[bool, str, str]:
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
            if not parsed.netloc:
                return False, url, "Missing domain name"
            
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

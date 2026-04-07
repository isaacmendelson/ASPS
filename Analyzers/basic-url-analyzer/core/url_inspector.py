"""
URL phishing inspector - analyzes URL string for phishing indicators without
making any network requests.

Checks performed (all URL-string-only, no network):
  1. UsingIP            - Domain is a raw IP address
  2. LongURL            - URL length exceeds safe thresholds
  3. ShortURL           - Domain is a known URL shortener
  4. SymbolAt           - URL contains @ symbol (hides real domain)
  5. DoubleSlash        - Path contains // redirect trick
  6. HyphenInDomain     - Domain contains hyphens (common in phishing)
  7. SubdomainCount     - Excessive subdomains
  8. NonStandardPort    - Non-standard port (not 80/443)
  9. HttpsInDomain      - Domain name contains "https" text
 10. SuspiciousChars    - Encoded control chars, homograph/unicode tricks
 11. SuspiciousSubdomain - Subdomain contains brand name unrelated to domain
"""

import re
import ipaddress
from typing import Dict
from urllib.parse import urlparse, unquote
from pathlib import Path
import sys
sys.path.append(str(Path(__file__).parent.parent))
from utils.logger import setup_logger


# Risk weights for score calculation
_RISK_WEIGHTS = {
    'high': 25,
    'medium': 15,
    'low': 5,
    'none': 0,
}

# 80+ known URL shortener domains
_SHORTENER_DOMAINS = frozenset({
    # Classic / high-traffic
    'bit.ly', 'tinyurl.com', 't.co', 'goo.gl', 'ow.ly', 'is.gd',
    'buff.ly', 'rebrand.ly', 'cutt.ly', 'shorturl.at', 'tiny.cc',
    'lnkd.in', 'db.tt', 'qr.ae', 'adf.ly', 'bit.do', 'mcaf.ee',
    'su.pr', 'cli.gs', 'budurl.com', 'snipr.com', 'fuurl.com',
    'u.to', 'short.to', 'clck.ru', 'bc.vc', 'yourls.org',

    # Social / platform shorteners
    'fb.me', 'youtu.be', 'amzn.to', 'amzn.eu', 'g.co', 'g.page',
    'redd.it', 'v.gd', 'vk.cc', 'x.co', 'x.gd',

    # Regional / modern (2020-2026)
    'rb.gy', 's.id', 'shrtco.de', 'shorturl.asia', 'tinu.be',
    'kutt.it', 'dub.sh', 'dub.co', 'short.io', 'bl.ink',
    'hypr.ink', 'zws.im', 'clicky.me', 't2m.io', 'urlz.fr',
    'han.gl', 'url.kr', 'vo.la', 'qps.ru', 'lmy.de',
    'tly.so', 'link.zip', '7.ly', 'snip.ly', 'surl.li',
    'urlzs.com', 'shortcm.li', 'plu.sh', 'shorten.rest',

    # Enterprise / branded
    'rebrandly.com', 'bitly.com', 'branch.io', 'brnch.io',
    'linktr.ee', 'solo.to', 'bio.link', 'beacons.ai',
    'hubs.ly', 'hubs.la', 'link.gallery',

    # Lesser-known / misc
    'n9.cl', 'ouo.io', 'za.gl', 'shortener.link', 'shrt.lnk.to',
    'naver.me', 'me2.do', 'cstu.io', 'ay.live', 'fly.link',
    'murl.com', 'trib.al', 'dfrk.co', 'waa.ai', 'link.ac',
    'urls.fr', 's.coop', 'zi.ma', 'ctt.ac', 'go.ly',
})

# Common/legitimate subdomains to skip in brand-impersonation check
_COMMON_SUBDOMAINS = frozenset({
    'www', 'mail', 'app', 'admin', 'blog', 'api', 'dev', 'staging', 'test',
    'm', 'mobile', 'portal', 'secure', 'login', 'my', 'account', 'dashboard',
    'cdn', 'static', 'assets', 'img', 'docs', 'help', 'support', 'store', 'shop',
})

# Well-known brand names commonly targeted in phishing
_PHISHING_BRANDS = frozenset({
    # Banks
    'paypal', 'bankofamerica', 'wellsfargo', 'chase', 'citibank', 'hsbc',
    'barclays', 'bankhapoalim', 'leumi', 'mizrahi', 'discount',
    # Tech
    'google', 'apple', 'microsoft', 'amazon', 'facebook', 'instagram',
    'netflix', 'twitter', 'linkedin', 'dropbox', 'icloud', 'outlook',
    'yahoo', 'gmail',
    # Crypto
    'binance', 'coinbase', 'blockchain', 'metamask', 'ledger', 'trezor',
    'kraken',
    # Payment
    'stripe', 'venmo', 'cashapp', 'zelle', 'wise', 'revolut',
    # Shopping
    'ebay', 'walmart', 'target', 'bestbuy', 'aliexpress',
    # Other
    'dhl', 'fedex', 'ups', 'usps', 'irs', 'gov',
})

# Country-code second-level domains (shared with subdomain_count check)
_CC_SLDS = frozenset({
    'co.uk', 'co.il', 'com.au', 'com.br', 'co.jp', 'co.kr',
    'co.nz', 'co.za', 'com.mx', 'com.ar', 'com.cn', 'com.tw',
    'com.sg', 'com.hk', 'org.uk', 'org.il', 'net.au', 'ac.uk',
    'ac.il', 'gov.il', 'gov.uk', 'gov.au', 'edu.au',
})

# Homograph characters that mimic Latin letters
_HOMOGRAPH_CHARS = set(
    '\u0430\u0435\u043e\u0440\u0441\u0443\u0445'  # Cyrillic а е о р с у х
    '\u0391\u0392\u0395\u0396\u0397\u0399\u039a'  # Greek Α Β Ε Ζ Η Ι Κ
    '\u039c\u039d\u039f\u03a1\u03a4\u03a5\u03a7'  # Greek Μ Ν Ο Ρ Τ Υ Χ
    '\u03b1\u03b5\u03b9\u03bf\u03c1\u03c5'        # Greek α ε ι ο ρ υ
)


class URLInspector:
    """Inspects a URL string for phishing indicators without network access."""

    def __init__(self):
        """Initialize URL inspector."""
        self.logger = setup_logger('url_inspector')

    # ------------------------------------------------------------------
    # Public API
    # ------------------------------------------------------------------

    def inspect(self, url: str) -> Dict:
        """
        Inspect a URL for phishing indicators.

        Args:
            url: URL string to inspect.

        Returns:
            Dictionary with inspection results, risk score, and per-check details.
        """
        # Handle invalid input
        if not url or not isinstance(url, str):
            return self._error_result(url, 'URL is empty or not a string')

        url = url.strip()
        if not url:
            return self._error_result(url, 'URL is empty after stripping whitespace')

        # Ensure a scheme exists for urlparse
        parse_url = url
        if not re.match(r'^[a-zA-Z][a-zA-Z0-9+\-.]*://', parse_url):
            parse_url = 'http://' + parse_url

        try:
            parsed = urlparse(parse_url)
        except Exception as e:
            return self._error_result(url, f'Failed to parse URL: {e}')

        hostname = parsed.hostname or ''
        domain = hostname.lower()

        if not domain:
            return self._error_result(url, 'Could not extract domain from URL')

        self.logger.info(f"Inspecting URL: {url}")

        # Run all 11 checks
        checks = {
            'using_ip': self._check_using_ip(domain),
            'long_url': self._check_long_url(url),
            'short_url': self._check_short_url(domain),
            'symbol_at': self._check_symbol_at(url),
            'double_slash_redirect': self._check_double_slash(url),
            'hyphen_in_domain': self._check_hyphen_in_domain(domain),
            'subdomain_count': self._check_subdomain_count(domain),
            'non_standard_port': self._check_non_standard_port(parsed),
            'https_in_domain': self._check_https_in_domain(domain),
            'suspicious_characters': self._check_suspicious_chars(url),
            'suspicious_subdomain': self._check_suspicious_subdomain(domain),
        }

        # Collect flags (checks that flagged something)
        flags = [name for name, result in checks.items() if result['passed']]
        flags_found = len(flags)

        # Calculate risk score
        risk_score = sum(_RISK_WEIGHTS.get(c['risk'], 0) for c in checks.values())
        risk_score = min(risk_score, 100)

        if risk_score >= 50:
            risk_level = 'HIGH'
        elif risk_score >= 25:
            risk_level = 'MEDIUM'
        else:
            risk_level = 'LOW'

        self.logger.info(
            f"Inspection complete: score={risk_score}, level={risk_level}, "
            f"flags={flags_found}/{len(checks)}"
        )

        return {
            'success': True,
            'url': url,
            'domain': domain,
            'url_risk_score': risk_score,
            'risk_level': risk_level,
            'total_checks': len(checks),
            'flags_found': flags_found,
            'checks': checks,
            'flags': flags,
            'error': '',
        }

    # ------------------------------------------------------------------
    # Individual checks
    # ------------------------------------------------------------------

    def _check_using_ip(self, domain: str) -> Dict:
        """Check 1: Is the domain a raw IP address?"""
        try:
            ipaddress.ip_address(domain)
            return self._flag('high', f'Domain is an IP address: {domain}')
        except ValueError:
            pass

        # Also catch IPv4 wrapped in brackets or with leading zeros
        ipv4_pattern = r'^\d{1,3}(\.\d{1,3}){3}$'
        if re.match(ipv4_pattern, domain):
            return self._flag('high', f'Domain matches IPv4 pattern: {domain}')

        return self._safe('Domain is not an IP address')

    def _check_long_url(self, url: str) -> Dict:
        """Check 2: Is the URL suspiciously long?"""
        length = len(url)
        if length > 100:
            return self._flag('medium', f'URL length is {length} characters (threshold: 100, phishing)')
        if length > 75:
            return self._flag('low', f'URL length is {length} characters (threshold: 75, suspicious)')
        return self._safe(f'URL length is {length} characters (within safe range)')

    def _check_short_url(self, domain: str) -> Dict:
        """Check 3: Is the domain a known URL shortener?"""
        # Strip www. for matching
        clean = domain.lstrip('www.')
        if clean in _SHORTENER_DOMAINS:
            return self._flag('medium', f'Domain is a known URL shortener: {clean}')
        return self._safe('Domain is not a known URL shortener')

    def _check_symbol_at(self, url: str) -> Dict:
        """Check 4: Does the URL contain @ symbol?"""
        # Ignore @ in the scheme part (e.g. user:pass@host in FTP)
        # but flag it in HTTP URLs as it hides the real domain
        if '@' in url:
            return self._flag('high', 'URL contains @ symbol which can hide the real destination')
        return self._safe('No @ symbol found in URL')

    def _check_double_slash(self, url: str) -> Dict:
        """Check 5: Is there // in the path (after the protocol)?"""
        # Remove the protocol prefix (http:// or https://)
        without_scheme = re.sub(r'^[a-zA-Z][a-zA-Z0-9+\-.]*://', '', url)
        if '//' in without_scheme:
            return self._flag('medium', 'URL path contains // which may indicate a redirect trick')
        return self._safe('No double-slash redirect pattern found')

    def _check_hyphen_in_domain(self, domain: str) -> Dict:
        """Check 6: Does the domain contain hyphens?"""
        # Remove TLD parts and check the main domain labels
        if '-' in domain:
            count = domain.count('-')
            if count >= 3:
                return self._flag('medium', f'Domain contains {count} hyphens (common in phishing)')
            return self._flag('low', f'Domain contains {count} hyphen(s)')
        return self._safe('Domain does not contain hyphens')

    def _check_subdomain_count(self, domain: str) -> Dict:
        """Check 7: How many subdomains does the domain have?"""
        parts = domain.split('.')

        # Handle country-code second-level domains (e.g. co.uk, co.il, com.au)
        cc_slds = {
            'co.uk', 'co.il', 'com.au', 'com.br', 'co.jp', 'co.kr',
            'co.nz', 'co.za', 'com.mx', 'com.ar', 'com.cn', 'com.tw',
            'com.sg', 'com.hk', 'org.uk', 'org.il', 'net.au', 'ac.uk',
            'ac.il', 'gov.il', 'gov.uk', 'gov.au', 'edu.au',
        }

        # Count effective labels (subtract TLD/ccSLD)
        tld_parts = 1
        if len(parts) >= 2:
            last_two = '.'.join(parts[-2:])
            if last_two in cc_slds:
                tld_parts = 2

        # effective_labels = total parts - TLD parts
        # subdomain_count = effective_labels - 1 (the main domain itself)
        effective = len(parts) - tld_parts
        subdomain_count = max(effective - 1, 0)

        # www doesn't count as a subdomain
        if parts[0] == 'www':
            subdomain_count = max(subdomain_count - 1, 0)

        if subdomain_count >= 3:
            return self._flag('high', f'{subdomain_count} subdomains detected (3+ is suspicious)')
        if subdomain_count == 2:
            return self._flag('medium', f'{subdomain_count} subdomains detected')
        return self._safe(f'{subdomain_count} subdomain(s) detected (normal)')

    def _check_non_standard_port(self, parsed) -> Dict:
        """Check 8: Is a non-standard port used?"""
        port = parsed.port
        if port is not None and port not in (80, 443):
            return self._flag('medium', f'Non-standard port {port} detected')
        return self._safe('Standard port or no explicit port')

    def _check_https_in_domain(self, domain: str) -> Dict:
        """Check 9: Does the domain name contain 'https' as text?"""
        if 'https' in domain:
            return self._flag('high', f"Domain contains 'https' text: {domain}")
        return self._safe("Domain does not contain 'https' text")

    def _check_suspicious_chars(self, url: str) -> Dict:
        """Check 10: Encoded control chars, homograph characters, unusual unicode."""
        reasons = []

        # Check for encoded control characters (%00-%1F)
        control_pattern = r'%[01][0-9a-fA-F]'
        if re.search(control_pattern, url):
            reasons.append('encoded control characters detected')

        # Check for homograph / confusable unicode characters
        decoded = url
        try:
            decoded = unquote(url)
        except Exception:
            pass

        homographs_found = [ch for ch in decoded if ch in _HOMOGRAPH_CHARS]
        if homographs_found:
            reasons.append(f'{len(homographs_found)} homograph/confusable character(s) detected')

        # Check for non-ASCII in domain portion (potential IDN homograph attack)
        try:
            parsed = urlparse(url if '://' in url else 'http://' + url)
            host = parsed.hostname or ''
            non_ascii = [ch for ch in host if ord(ch) > 127]
            if non_ascii:
                reasons.append(f'{len(non_ascii)} non-ASCII character(s) in domain')
        except Exception:
            pass

        if reasons:
            detail = '; '.join(reasons)
            severity = 'high' if 'homograph' in detail else 'medium'
            return self._flag(severity, detail)

        return self._safe('No suspicious characters detected')

    def _check_suspicious_subdomain(self, domain: str) -> Dict:
        """Check 11: Does the subdomain contain a brand name unrelated to the main domain?"""
        parts = domain.split('.')
        if len(parts) < 3:
            return self._safe('No subdomain present')

        # Determine TLD length (1 for .com, 2 for .co.il etc.)
        tld_parts = 1
        if len(parts) >= 2:
            last_two = '.'.join(parts[-2:])
            if last_two in _CC_SLDS:
                tld_parts = 2

        # main_domain is the label just before the TLD
        # e.g. evil.com -> "evil", evil.co.il -> "evil"
        main_domain_label = parts[-(tld_parts + 1)]
        full_registered_domain = '.'.join(parts[-(tld_parts + 1):])

        # subdomain labels are everything before the registered domain
        subdomain_labels = parts[:-(tld_parts + 1)]
        if not subdomain_labels:
            return self._safe('No subdomain present')

        # Join all subdomain labels into one string for brand matching
        subdomain_text = '-'.join(subdomain_labels).lower()

        # Skip if subdomain is only common/legitimate labels
        if all(label in _COMMON_SUBDOMAINS for label in subdomain_labels):
            return self._safe('Subdomain contains only common labels')

        # Check each brand
        for brand in _PHISHING_BRANDS:
            if brand in subdomain_text:
                # Check if brand matches the main domain (legitimate use)
                if brand in main_domain_label:
                    continue
                return self._flag(
                    'high',
                    f"Subdomain contains brand '{brand}' but domain is '{full_registered_domain}'"
                )

        return self._safe('No brand impersonation detected in subdomain')

    # ------------------------------------------------------------------
    # Helpers
    # ------------------------------------------------------------------

    @staticmethod
    def _flag(risk: str, detail: str) -> Dict:
        """Return a check result that flagged something."""
        return {'passed': True, 'risk': risk, 'detail': detail}

    @staticmethod
    def _safe(detail: str) -> Dict:
        """Return a check result that found nothing suspicious."""
        return {'passed': False, 'risk': 'none', 'detail': detail}

    def _error_result(self, url, error: str) -> Dict:
        """Return an error result."""
        self.logger.warning(f"URL inspection error: {error}")
        return {
            'success': False,
            'url': url if isinstance(url, str) else str(url),
            'domain': '',
            'url_risk_score': 0,
            'risk_level': 'UNKNOWN',
            'total_checks': 0,
            'flags_found': 0,
            'checks': {},
            'flags': [],
            'error': error,
        }

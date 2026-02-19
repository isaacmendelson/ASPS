"""
WHOIS domain information checker
"""

import whois
import json
from datetime import datetime
from pathlib import Path
from typing import Dict, Optional
from urllib.parse import urlparse
import sys
sys.path.append(str(Path(__file__).parent.parent))
from utils.logger import setup_logger

# Common second-level TLDs that need special handling
SECOND_LEVEL_TLDS = {
    'co.il', 'co.uk', 'co.nz', 'co.jp', 'co.kr', 'co.za',
    'com.au', 'com.br', 'com.cn', 'com.mx', 'com.tr',
    'org.uk', 'org.il', 'org.au',
    'net.il', 'net.au', 'net.br',
    'ac.il', 'ac.uk', 'ac.jp',
    'gov.il', 'gov.uk', 'gov.au',
    'edu.au', 'edu.cn'
}


class WhoisChecker:
    """Checks WHOIS information for domains"""
    
    def __init__(self):
        """Initialize WHOIS checker"""
        self.logger = setup_logger('whois_checker')
        
        # Load settings
        config_path = Path(__file__).parent.parent / 'config' / 'settings.json'
        with open(config_path, 'r') as f:
            self.settings = json.load(f)
        
        self.high_risk_countries = self.settings['high_risk_countries']
        self.suspicious_registrars = self.settings['suspicious_registrars']

    def _extract_root_domain(self, domain: str) -> str:
        """
        Extract the root domain from a subdomain.

        Examples:
            pplus.ynet.co.il -> ynet.co.il
            www.google.com -> google.com
            sub.domain.co.uk -> domain.co.uk
        """
        parts = domain.lower().split('.')

        # Check for second-level TLDs (like co.il, com.au)
        if len(parts) >= 3:
            potential_sld = '.'.join(parts[-2:])
            if potential_sld in SECOND_LEVEL_TLDS:
                # Return domain + second-level TLD (e.g., ynet.co.il)
                if len(parts) >= 3:
                    return '.'.join(parts[-3:])

        # Standard TLD - return last two parts (e.g., google.com)
        if len(parts) >= 2:
            return '.'.join(parts[-2:])

        return domain

    def check(self, domain: str) -> Dict:
        """
        Perform WHOIS lookup on domain
        
        Args:
            domain: Domain name to check
        
        Returns:
            Dictionary with WHOIS information and risk assessment
        """
        try:
            # Extract root domain from subdomain
            root_domain = self._extract_root_domain(domain)
            if root_domain != domain:
                self.logger.info(f"Performing WHOIS lookup for {root_domain} (from {domain})")
            else:
                self.logger.info(f"Performing WHOIS lookup for {domain}")

            # Perform WHOIS query on root domain
            w = whois.whois(root_domain)
            
            # Extract creation date
            created_date = self._extract_date(w.creation_date)

            # Calculate age
            age_days = 0
            now = datetime.now()
            if created_date:
                # Handle timezone-aware vs naive datetime comparison
                if created_date.tzinfo is not None:
                    created_date = created_date.replace(tzinfo=None)
                age_days = (now - created_date).days
            else:
                # Some TLDs (like .co.il) don't return creation_date
                # Try to estimate from expiration_date
                expiration_date = self._extract_date(w.expiration_date)
                if expiration_date:
                    if expiration_date.tzinfo is not None:
                        expiration_date = expiration_date.replace(tzinfo=None)
                    # If domain expires in the future, it's at least established
                    # Domains are typically renewed 1-10 years at a time
                    # A future expiration means domain is at least 1 year old
                    if expiration_date > now:
                        # Estimate: domain is at least (years until expiry - 1) years old
                        # Or at minimum 365 days if expiration is within 2 years
                        years_until_expiry = (expiration_date - now).days / 365
                        # Assume minimum 1 year registration, so domain is at least
                        # some time old based on when they last renewed
                        age_days = max(365, int(years_until_expiry * 365))
                        self.logger.info(f"Estimated domain age from expiration: {age_days} days")
            
            # Extract registrar
            registrar = self._extract_registrar(w.registrar)
            
            # Extract country
            country = self._extract_country(w.country)
            
            # Check if privacy protected
            privacy_protected = self._is_privacy_protected(w)
            
            # Calculate risk factors
            risk_factors = self._calculate_risk_factors(
                age_days, country, registrar, privacy_protected
            )
            
            # Calculate overall WHOIS risk score
            risk_score = self._calculate_whois_risk_score(risk_factors)
            
            result = {
                'success': True,
                'domain': domain,
                'root_domain': root_domain,
                'created_date': created_date.isoformat() if created_date else None,
                'age_days': age_days,
                'registrar': registrar,
                'country': country,
                'privacy_protected': privacy_protected,
                'risk_factors': risk_factors,
                'risk_score': risk_score,
                'error': ''
            }
            
            self.logger.info(f"WHOIS lookup successful for {root_domain}")
            return result
        
        except Exception as e:
            self.logger.error(f"WHOIS lookup failed for {domain}: {str(e)}")
            return {
                'success': False,
                'domain': domain,
                'created_date': None,
                'age_days': 0,
                'registrar': 'Unknown',
                'country': 'Unknown',
                'privacy_protected': False,
                'risk_factors': {},
                'risk_score': 0.0,
                'error': f"WHOIS lookup failed: {str(e)}"
            }
    
    def _extract_date(self, date_value) -> Optional[datetime]:
        """Extract datetime from WHOIS date field"""
        if not date_value:
            return None
        
        # WHOIS can return list or single value
        if isinstance(date_value, list):
            date_value = date_value[0]
        
        if isinstance(date_value, datetime):
            return date_value
        
        return None
    
    def _extract_registrar(self, registrar_value) -> str:
        """Extract registrar name"""
        if not registrar_value:
            return 'Unknown'
        
        if isinstance(registrar_value, list):
            registrar_value = registrar_value[0]
        
        return str(registrar_value) if registrar_value else 'Unknown'
    
    def _extract_country(self, country_value) -> str:
        """Extract country code"""
        if not country_value:
            return 'Unknown'
        
        if isinstance(country_value, list):
            country_value = country_value[0]
        
        return str(country_value).upper() if country_value else 'Unknown'
    
    def _is_privacy_protected(self, w) -> bool:
        """Check if WHOIS has privacy protection"""
        # Check common privacy indicators
        privacy_keywords = ['privacy', 'protected', 'redacted', 'private', 'proxy']
        
        fields_to_check = [
            str(w.name).lower() if w.name else '',
            str(w.org).lower() if w.org else '',
            str(w.registrar).lower() if w.registrar else ''
        ]
        
        for field in fields_to_check:
            for keyword in privacy_keywords:
                if keyword in field:
                    return True
        
        return False
    
    def _calculate_risk_factors(self, age_days: int, country: str, 
                                registrar: str, privacy_protected: bool) -> Dict:
        """Calculate individual risk factors"""
        factors = {}
        
        # Domain age factors
        if age_days < 30:
            factors['very_new_domain'] = True
        elif age_days < 180:
            factors['new_domain'] = True
        
        # Country risk
        if country in self.high_risk_countries:
            factors['suspicious_country'] = True
        
        # Registrar risk
        registrar_lower = registrar.lower()
        for suspicious in self.suspicious_registrars:
            if suspicious in registrar_lower:
                factors['suspicious_registrar'] = True
                break
        
        # Privacy protection
        if privacy_protected:
            factors['privacy_protected'] = True
        
        return factors
    
    def _calculate_whois_risk_score(self, risk_factors: Dict) -> float:
        """Calculate overall WHOIS risk score (0-1)"""
        score = 0.0
        
        # Load pattern weights
        patterns_path = Path(__file__).parent.parent / 'config' / 'patterns.json'
        with open(patterns_path, 'r') as f:
            patterns = json.load(f)
        
        whois_rules = patterns['whois_rules']
        
        # Add weights for each matched factor
        if risk_factors.get('very_new_domain'):
            score += whois_rules['very_new_domain']['weight']
        elif risk_factors.get('new_domain'):
            score += whois_rules['new_domain']['weight']
        
        if risk_factors.get('privacy_protected'):
            score += whois_rules['privacy_protected']['weight']
        
        if risk_factors.get('suspicious_country'):
            score += whois_rules['suspicious_location']['weight']
        
        if risk_factors.get('suspicious_registrar'):
            score += whois_rules['suspicious_registrar']['weight']
        
        return min(score, 1.0)  # Cap at 1.0

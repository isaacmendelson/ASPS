"""
Rules-based scam detection engine
"""

import re
import json
from pathlib import Path
from typing import Dict, List
import sys
sys.path.append(str(Path(__file__).parent.parent))
from utils.logger import setup_logger


class RulesEngine:
    """Pattern-based scam detection"""
    
    def __init__(self):
        """Initialize rules engine"""
        self.logger = setup_logger('rules_engine')
        
        # Load patterns
        patterns_path = Path(__file__).parent.parent / 'config' / 'patterns.json'
        with open(patterns_path, 'r') as f:
            self.patterns = json.load(f)
        
        # Load settings
        settings_path = Path(__file__).parent.parent / 'config' / 'settings.json'
        with open(settings_path, 'r') as f:
            self.settings = json.load(f)
    
    def analyze(self, content: Dict, whois_data: Dict) -> Dict:
        """
        Analyze content and WHOIS data for scam indicators
        
        Args:
            content: Extracted content dictionary
            whois_data: WHOIS information dictionary
        
        Returns:
            Analysis results with risk score and detected patterns
        """
        try:
            self.logger.info("Running rules engine analysis")
            
            detected_patterns = []
            total_score = 0.0
            
            # Analyze content patterns
            if content.get('success'):
                content_matches = self._analyze_content_patterns(content)
                detected_patterns.extend(content_matches)
                
                # Analyze structure patterns
                structure_matches = self._analyze_structure_patterns(content)
                detected_patterns.extend(structure_matches)
            
            # Analyze WHOIS patterns
            if whois_data.get('success'):
                whois_matches = self._analyze_whois_patterns(whois_data)
                detected_patterns.extend(whois_matches)
            
            # Calculate total score
            for pattern in detected_patterns:
                total_score += pattern['weight']
            
            # Normalize score to 0-100
            risk_score = min(int(total_score * 100), 100)
            
            # Determine risk level
            risk_level = self._determine_risk_level(risk_score)
            
            # Determine if scam
            is_scam = risk_score >= self.settings['scoring']['thresholds']['high']
            
            result = {
                'success': True,
                'risk_score': risk_score,
                'risk_level': risk_level,
                'is_scam': is_scam,
                'detected_patterns': detected_patterns,
                'pattern_count': len(detected_patterns),
                'error': ''
            }
            
            self.logger.info(f"Analysis complete - Risk score: {risk_score}")
            return result
        
        except Exception as e:
            self.logger.error(f"Rules engine analysis failed: {str(e)}")
            return {
                'success': False,
                'risk_score': 0,
                'risk_level': 'UNKNOWN',
                'is_scam': False,
                'detected_patterns': [],
                'pattern_count': 0,
                'error': f"Analysis failed: {str(e)}"
            }
    
    def _analyze_content_patterns(self, content: Dict) -> List[Dict]:
        """Analyze text content for scam patterns"""
        matches = []
        
        # Combine all text for analysis
        text_to_analyze = ' '.join([
            content.get('title', ''),
            content.get('meta_description', ''),
            ' '.join(content.get('headings', {}).get('h1', [])),
            ' '.join(content.get('headings', {}).get('h2', [])),
            content.get('body_text', '')[:5000],  # First 5000 chars
            ' '.join(content.get('cta_buttons', []))
        ])
        
        # Check each content pattern
        for pattern_name, pattern_data in self.patterns['content_patterns'].items():
            regex = pattern_data['regex']
            
            # Search for pattern
            found = re.search(regex, text_to_analyze, re.IGNORECASE)
            
            if found:
                matches.append({
                    'name': pattern_name,
                    'type': 'content',
                    'matched_text': found.group(0)[:100],  # Limit length
                    'weight': pattern_data['weight'],
                    'description': pattern_data['description']
                })
        
        return matches
    
    def _analyze_structure_patterns(self, content: Dict) -> List[Dict]:
        """Analyze page structure for scam indicators"""
        matches = []
        
        # Check excessive CTAs
        cta_count = content.get('cta_count', 0)
        if cta_count > 5:
            matches.append({
                'name': 'excessive_ctas',
                'type': 'structure',
                'matched_text': f"{cta_count} call-to-action buttons found",
                'weight': self.patterns['structure_rules']['excessive_ctas']['weight'],
                'description': self.patterns['structure_rules']['excessive_ctas']['description']
            })
        
        # Check payment forms
        forms = content.get('forms', {})
        if 'payment' in forms.get('types', []) or 'credit' in forms.get('types', []):
            matches.append({
                'name': 'payment_form',
                'type': 'structure',
                'matched_text': 'Payment form detected',
                'weight': self.patterns['structure_rules']['payment_form']['weight'],
                'description': self.patterns['structure_rules']['payment_form']['description']
            })
        
        # Check URL shorteners
        links = content.get('links', {})
        if links.get('has_suspicious'):
            matches.append({
                'name': 'url_shorteners',
                'type': 'structure',
                'matched_text': f"Suspicious links: {', '.join(links.get('suspicious', [])[:3])}",
                'weight': self.patterns['structure_rules']['url_shorteners']['weight'],
                'description': self.patterns['structure_rules']['url_shorteners']['description']
            })
        
        # Check internal links
        internal_links = links.get('internal', 0)
        if internal_links < 5:
            matches.append({
                'name': 'few_internal_links',
                'type': 'structure',
                'matched_text': f"Only {internal_links} internal links",
                'weight': self.patterns['structure_rules']['few_internal_links']['weight'],
                'description': self.patterns['structure_rules']['few_internal_links']['description']
            })
        
        # Check external links
        external_links = links.get('external', 0)
        if external_links > 20:
            matches.append({
                'name': 'many_external_links',
                'type': 'structure',
                'matched_text': f"{external_links} external links",
                'weight': self.patterns['structure_rules']['many_external_links']['weight'],
                'description': self.patterns['structure_rules']['many_external_links']['description']
            })
        
        return matches
    
    def _analyze_whois_patterns(self, whois_data: Dict) -> List[Dict]:
        """Analyze WHOIS data for risk factors"""
        matches = []
        
        risk_factors = whois_data.get('risk_factors', {})
        
        # Very new domain
        if risk_factors.get('very_new_domain'):
            age_days = whois_data.get('age_days', 0)
            matches.append({
                'name': 'very_new_domain',
                'type': 'whois',
                'matched_text': f"Domain is only {age_days} days old",
                'weight': self.patterns['whois_rules']['very_new_domain']['weight'],
                'description': self.patterns['whois_rules']['very_new_domain']['description']
            })
        
        # New domain
        elif risk_factors.get('new_domain'):
            age_days = whois_data.get('age_days', 0)
            matches.append({
                'name': 'new_domain',
                'type': 'whois',
                'matched_text': f"Domain is {age_days} days old",
                'weight': self.patterns['whois_rules']['new_domain']['weight'],
                'description': self.patterns['whois_rules']['new_domain']['description']
            })
        
        # Privacy protection
        if risk_factors.get('privacy_protected'):
            matches.append({
                'name': 'privacy_protected',
                'type': 'whois',
                'matched_text': 'WHOIS privacy protection enabled',
                'weight': self.patterns['whois_rules']['privacy_protected']['weight'],
                'description': self.patterns['whois_rules']['privacy_protected']['description']
            })
        
        # Suspicious country
        if risk_factors.get('suspicious_country'):
            country = whois_data.get('country', 'Unknown')
            matches.append({
                'name': 'suspicious_location',
                'type': 'whois',
                'matched_text': f"Registered in {country}",
                'weight': self.patterns['whois_rules']['suspicious_location']['weight'],
                'description': self.patterns['whois_rules']['suspicious_location']['description']
            })
        
        # Suspicious registrar
        if risk_factors.get('suspicious_registrar'):
            registrar = whois_data.get('registrar', 'Unknown')
            matches.append({
                'name': 'suspicious_registrar',
                'type': 'whois',
                'matched_text': f"Registrar: {registrar}",
                'weight': self.patterns['whois_rules']['suspicious_registrar']['weight'],
                'description': self.patterns['whois_rules']['suspicious_registrar']['description']
            })
        
        return matches
    
    def _determine_risk_level(self, risk_score: int) -> str:
        """Determine risk level from score"""
        thresholds = self.settings['scoring']['thresholds']
        
        if risk_score < thresholds['low']:
            return 'LOW'
        elif risk_score < thresholds['medium']:
            return 'MEDIUM'
        else:
            return 'HIGH'

"""
Main scam analyzer orchestrator
"""

import time
from datetime import datetime
from typing import Dict, Optional
from pathlib import Path
import sys
sys.path.append(str(Path(__file__).parent.parent))

# Language detection
try:
    from langdetect import detect as detect_language
    from langdetect.lang_detect_exception import LangDetectException
    LANGDETECT_AVAILABLE = True
except ImportError:
    LANGDETECT_AVAILABLE = False
    detect_language = None
    LangDetectException = Exception

from .whois_checker import WhoisChecker
from .content_extractor import ContentExtractor
from .rules_engine import RulesEngine
from .purpose_classifier import PurposeClassifier
from .ml_classifier import MLClassifier
from .reputation_checker import ReputationChecker
from .category_classifier import CategoryClassifier
from .url_inspector import URLInspector
from scrapers.playwright_scraper import PlaywrightScraper
from utils.logger import setup_logger
from utils.validators import URLValidator
from utils.cache_manager import CacheManager


class ScamAnalyzer:
    """Main scam detection analyzer"""
    
    def __init__(self, use_cache: bool = True, use_ml: bool = True, no_explain: bool = False):
        """
        Initialize analyzer

        Args:
            use_cache: Whether to use caching
            use_ml: Whether to use ML classifier
            no_explain: Whether to skip LLM explanation (for faster execution)
        """
        self.logger = setup_logger('scam_analyzer')
        self.no_explain = no_explain
        
        # Initialize components
        self.validator = URLValidator()
        self.whois_checker = WhoisChecker()
        self.reputation_checker = ReputationChecker()
        self.category_classifier = CategoryClassifier()
        self.url_inspector = URLInspector()
        self.scraper = PlaywrightScraper()
        self.content_extractor = ContentExtractor()
        self.rules_engine = RulesEngine()
        self.purpose_classifier = PurposeClassifier()
        
        # ML classifier (optional)
        self.use_ml = use_ml
        if use_ml:
            self.ml_classifier = MLClassifier()
            self.logger.info("ML classifier enabled")
        else:
            self.ml_classifier = None
        
        # Cache
        self.use_cache = use_cache
        if use_cache:
            self.cache = CacheManager()
    
    def analyze_url(self, url: str) -> Dict:
        """
        Analyze URL for scam indicators
        
        Args:
            url: URL to analyze
        
        Returns:
            Complete analysis results
        """
        start_time = time.time()
        
        try:
            self.logger.info(f"Starting analysis of {url}")
            
            # Validate URL
            is_valid, clean_url, error = self.validator.validate(url)
            if not is_valid:
                return self._error_response(url, f"Invalid URL: {error}")
            
            url = clean_url
            
            # Check cache
            if self.use_cache:
                cached = self.cache.get(url)
                if cached:
                    self.logger.info(f"Returning cached result for {url}")
                    cached['from_cache'] = True
                    return cached
            
            # Extract domain
            domain = self.validator.extract_domain(url)
            
            # Initialize result
            result = {
                'url': url,
                'domain': domain,
                'analyzed_at': datetime.now().isoformat(),
                'analysis_time_ms': 0,
                'from_cache': False
            }
            
            # Track missing data
            missing_data = []
            warnings = []

            # Step 0: URL string inspection (no network needed)
            url_inspection = self.url_inspector.inspect(url)

            # Step 1: WHOIS lookup
            whois_data = self.whois_checker.check(domain)
            if not whois_data['success']:
                missing_data.append('WHOIS')
                warnings.append(f"WHOIS lookup failed: {whois_data['error']}")

            # Step 1.5: Reputation check
            reputation_data = self.reputation_checker.check(domain)

            # Step 2: Web scraping
            scrape_result = self.scraper.fetch(url)
            if not scrape_result['success']:
                missing_data.append('content')
                warnings.append(f"Web scraping failed: {scrape_result['error']}")
            
            # Step 3: Content extraction (only if scraping succeeded)
            content = {'success': False}
            if scrape_result['success']:
                content = self.content_extractor.extract(
                    scrape_result['html'],
                    scrape_result['final_url']
                )
                if not content['success']:
                    missing_data.append('content_analysis')
                    warnings.append(f"Content extraction failed: {content['error']}")
            
            # Step 3.5: Content validity check
            content_status = self._check_content_validity(content, scrape_result)
            if not content_status['is_valid']:
                self.logger.warning(
                    f"Content not valid: {content_status['status']} - {content_status['detail']}"
                )
                warnings.append(f"Content may be incomplete - {content_status['detail']}")

            # Step 4: Rules engine analysis
            analysis = self.rules_engine.analyze(content, whois_data)

            # Step 4.5: Language detection
            detected_language = 'unknown'
            is_english = True  # Default to English if detection fails

            if LANGDETECT_AVAILABLE and content.get('success') and content.get('body_text'):
                try:
                    text_sample = content['body_text'][:1000]  # Use first 1000 chars for speed
                    detected_language = detect_language(text_sample)
                    is_english = detected_language == 'en'
                    self.logger.info(f"Language detected: {detected_language} (is_english={is_english})")
                except LangDetectException:
                    self.logger.warning("Language detection failed, assuming English")
                    detected_language = 'unknown'
                    is_english = True

            # Step 4.6: ML prediction (if enabled AND content is English)
            ml_result = {'success': False, 'score': 0.0, 'note': 'ML disabled'}

            # Reputation data (informational only, does not override ML)
            is_well_known = reputation_data.get('is_well_known', False)
            reputable_mentions = reputation_data.get('reputable_mentions', 0)

            # Skip ML for non-English content (model trained on English only)
            if not is_english:
                self.logger.info(f"Skipping ML for non-English content ({detected_language})")
                ml_result = {'success': False, 'score': 0.0, 'note': f'Skipped for {detected_language} content'}

                # For non-English: apply domain age override directly to rules score
                domain_age_days = whois_data.get('age_days', 0)
                rules_score = analysis.get('risk_score', 0)

                if domain_age_days > 3650:  # 10+ years
                    self.logger.info(f"Non-English + domain age override: {domain_age_days} days (10+ years) -> LOW")
                    analysis['risk_score'] = min(rules_score, 25)
                elif domain_age_days > 2555:  # 7+ years
                    self.logger.info(f"Non-English + domain age override: {domain_age_days} days (7+ years) -> LOW")
                    analysis['risk_score'] = min(rules_score, 25)
                elif domain_age_days > 1825:  # 5+ years
                    self.logger.info(f"Non-English + domain age override: {domain_age_days} days (5+ years) -> LOW")
                    analysis['risk_score'] = min(rules_score, 30)
                # else: keep rules_score as is for newer non-English sites

                analysis['risk_level'] = self._determine_risk_level(analysis['risk_score'])
                analysis['is_scam'] = analysis['risk_score'] >= 61

            elif self.use_ml and content.get('success'):
                ml_result = self.ml_classifier.predict(content['body_text'])

                # Combine ML + Rules scores using Tiered Decision Tree
                if ml_result['success']:
                    rules_score = analysis.get('risk_score', 0) / 100.0  # 0.0-1.0
                    ml_score = ml_result['score']  # 0.0-1.0

                    # DOMAIN AGE OVERRIDE: Old domains are trustworthy
                    # BUT skip if URL inspector detected a suspicious subdomain
                    # (e.g. paypal-login.evil.com — evil.com is old but subdomain is phishing)
                    domain_age_days = whois_data.get('age_days', 0)
                    has_suspicious_subdomain = 'suspicious_subdomain' in url_inspection.get('flags', [])

                    if has_suspicious_subdomain:
                        self.logger.info(
                            "Suspicious subdomain detected - skipping domain age override"
                        )
                        combined_score = self._tiered_score_combination(ml_score, rules_score)
                    elif domain_age_days > 5475 and ml_score < 0.995:  # 15+ years - almost always trust
                        self.logger.info(
                            f"Domain age override: {domain_age_days} days old (15+ years) -> LOW risk"
                        )
                        combined_score = min(ml_score, 0.25)  # Cap at 25% = LOW risk
                    elif domain_age_days > 3650 and ml_score < 0.95:  # 10+ years AND ML not extremely confident
                        self.logger.info(
                            f"Domain age override: {domain_age_days} days old (10+ years) -> LOW risk"
                        )
                        combined_score = min(ml_score, 0.25)  # Cap at 25% = LOW risk
                    elif domain_age_days > 2555 and ml_score < 0.40:  # 7+ years AND ML says safe
                        # Trust ML if it's confident the site is safe (< 40%)
                        self.logger.info(
                            f"Domain age override: {domain_age_days} days old (7+ years), ML says safe ({ml_score:.2f}) -> LOW risk"
                        )
                        combined_score = min(ml_score, 0.25)  # Cap at 25% = LOW risk
                    elif domain_age_days > 2555 and rules_score <= 0.50 and ml_score < 0.90:  # 7+ years AND rules moderate
                        self.logger.info(
                            f"Domain age override: {domain_age_days} days old (7+ years), rules moderate ({rules_score:.2f}) -> LOW risk"
                        )
                        combined_score = min(ml_score, 0.25)  # Cap at 25% = LOW risk
                    elif domain_age_days > 1825 and rules_score <= 0.20 and ml_score < 0.85:  # 5+ years AND ML not confident
                        self.logger.info(
                            f"Domain age override: {domain_age_days} days old (5+ years), rules low ({rules_score:.2f}) -> LOW risk"
                        )
                        combined_score = min(ml_score, 0.25)  # Cap at 25% = LOW risk
                    else:
                        # Normal tiered combination for unknown sites
                        combined_score = self._tiered_score_combination(ml_score, rules_score)

                    # Update analysis with combined score
                    analysis['risk_score'] = int(combined_score * 100)
                    analysis['risk_level'] = self._determine_risk_level(analysis['risk_score'])
                    analysis['is_scam'] = analysis['risk_score'] >= 61
            
            # Step 5: Purpose classification
            classification = self.purpose_classifier.classify(
                content,
                analysis.get('detected_patterns', [])
            )

            # Step 5.5: Category classification (what type of site is this)
            # Skip category classification if content is not valid
            if content_status['is_valid']:
                category_result = self.category_classifier.classify(content, domain)
            else:
                self.logger.info(
                    f"Skipping category classification - content not valid ({content_status['status']})"
                )
                category_result = {
                    'success': False, 'category': 'unknown', 'category_group': 'unknown',
                    'name_en': 'Unknown', 'name_he': 'לא ידוע', 'confidence': 0.0,
                    'detection_method': 'none', 'matched_signals': [],
                    'secondary_category': None, 'secondary_confidence': 0.0,
                    'all_scores': {}, 'error': content_status['detail']
                }

            # Map category to backend WebsiteType enum values
            _CATEGORY_TO_WEBSITE_TYPE = {
                # Financial
                'banking': 'Banking', 'credit_union': 'Banking',
                'insurance': 'Insurance', 'investment': 'Investment',
                'stock_trading': 'Exchange', 'crypto_exchange': 'Exchange',
                'payment_service': 'Banking', 'lending': 'Banking',
                # Shopping
                'ecommerce': 'ECommerce', 'marketplace': 'ECommerce',
                'auction': 'ECommerce', 'classifieds': 'ECommerce',
                'grocery': 'ECommerce', 'fashion': 'ECommerce', 'electronics': 'ECommerce',
                # Government
                'government': 'Government', 'municipality': 'Government',
                'military': 'Government', 'court': 'Government',
                'tax_authority': 'Government', 'public_service': 'Government',
                # Health
                'hospital': 'Healthcare', 'clinic': 'Healthcare',
                'pharmacy': 'Healthcare', 'telehealth': 'Healthcare',
                'mental_health': 'Healthcare',
                # Education
                'university': 'Education', 'school': 'Education',
                'online_course': 'Education', 'elearning': 'Education',
                # Entertainment
                'streaming': 'Entertainment', 'gaming': 'Entertainment',
                'gambling': 'Gambling', 'sports_betting': 'Gambling',
                'adult_content': 'AdultContent',
                # Media
                'news': 'News', 'blog': 'News', 'forum': 'Analytics',
                'social_network': 'Dating', 'messaging': 'Dating',
                # Services
                'legal': 'Legal', 'accounting': 'Analytics',
                'real_estate': 'RealEstate', 'travel': 'Travel', 'job_board': 'Analytics',
                # Technology
                'saas': 'Analytics', 'cloud': 'Analytics', 'web_hosting': 'Analytics',
                'vpn_proxy': 'Analytics', 'developer_tools': 'Analytics',
                # Other
                'restaurant': 'Restaurant', 'automotive': 'Unknown',
                'pets': 'Unknown', 'nonprofit': 'Nonprofit', 'religious': 'Unknown',
                # New categories
                'language_learning': 'Education',
                'review_directory': 'Analytics',
                'ride_delivery': 'ECommerce',
                # Legacy purpose classifier mappings
                'crypto_scam': 'Exchange', 'investment_scam': 'Exchange',
                'fake_ecommerce': 'ECommerce', 'romance_scam': 'Dating',
                'finance_banking': 'Banking', 'news_media': 'News',
                'ecommerce_shopping': 'ECommerce', 'technology': 'Analytics',
            }
            raw_category = category_result.get('category', 'unknown')
            mapped_category = _CATEGORY_TO_WEBSITE_TYPE.get(raw_category, 'Unknown')

            # Build complete result
            # Use raw risk score: 0 = error/no result, 1 = safest, 100 = most dangerous
            risk_score = analysis.get('risk_score', 0)
            risk_level = analysis.get('risk_level', 'UNKNOWN')

            # Determine scam type based on category + risk level
            scam_type = self._determine_scam_type(
                risk_level, risk_score,
                category_result.get('category', 'unknown')
            )

            result.update({
                'risk_assessment': {
                    'risk_score': risk_score,  # New scale: 0 = error, 1 = safe, 100 = dangerous
                    'risk_level': risk_level,
                    'is_scam': analysis.get('is_scam', False),
                    'confidence': self._calculate_confidence(missing_data)
                },
                'scam_type': scam_type,
                'purpose': {
                    'category': mapped_category,
                    'confidence': classification.get('confidence', 0.0),
                    'description': classification.get('description', '')
                },
                'whois': {
                    'success': whois_data.get('success', False),
                    'domain_age_days': whois_data.get('age_days', 0),
                    'created_date': whois_data.get('created_date'),
                    'registrar': whois_data.get('registrar', 'Unknown'),
                    'country': whois_data.get('country', 'Unknown'),
                    'privacy_protected': whois_data.get('privacy_protected', False),
                    'risk_score': whois_data.get('risk_score', 0.0)
                },
                'content_analysis': {
                    'success': content.get('success', False),
                    'title': content.get('title', ''),
                    'detected_patterns': analysis.get('detected_patterns', []),
                    'cta_count': content.get('cta_count', 0),
                    'form_types': [],  # Not needed, send empty to avoid C# deserialization issues
                    'word_count': content.get('word_count', 0),
                    'detected_language': detected_language,
                    'is_english': is_english
                },
                'ml_analysis': {
                    'enabled': self.use_ml,
                    'success': ml_result.get('success', False),
                    'score': ml_result.get('score', 0.0),
                    'confidence': ml_result.get('confidence', 0.0),
                    'note': ml_result.get('note', '')
                },
                'reputation': {
                    'success': reputation_data.get('success', False),
                    'is_well_known': reputation_data.get('is_well_known', False),
                    'reputable_mentions': reputation_data.get('reputable_mentions', 0),
                    'mention_sources': reputation_data.get('mention_sources', []),
                    'reputation_score': reputation_data.get('reputation_score', 0.0),
                    'score_adjustment': reputation_data.get('score_adjustment', 0)
                },
                'content_status': content_status,
                'url_inspection': url_inspection,
                'website_category': {
                    'category': category_result.get('category', 'unknown'),
                    'category_group': category_result.get('category_group', 'unknown'),
                    'name_en': category_result.get('name_en', 'Unknown'),
                    'confidence': category_result.get('confidence', 0.0),
                    'detection_method': category_result.get('detection_method', 'none'),
                    'matched_signals': category_result.get('matched_signals', [])
                },
                'red_flags': self._generate_red_flags(
                    analysis.get('detected_patterns', []),
                    whois_data
                ),
                'recommendation': self._generate_recommendation(
                    analysis.get('risk_level', 'UNKNOWN'),
                    classification.get('category', 'unknown'),
                    missing_data
                ),
                'scraping_status': {
                    'success': scrape_result.get('success', False),
                    'status_code': scrape_result.get('status_code', 0),
                    'final_url': scrape_result.get('final_url', url),
                    'error': scrape_result.get('error', '')
                },
                'warnings': warnings,
                'missing_data': missing_data
            })
            
            # Calculate analysis time
            elapsed = int((time.time() - start_time) * 1000)
            result['analysis_time_ms'] = elapsed
            
            # Cache result
            if self.use_cache:
                self.cache.set(url, result)
            
            self.logger.info(f"Analysis completed in {elapsed}ms - Risk: {result['risk_assessment']['risk_level']}")
            return result
        
        except Exception as e:
            self.logger.error(f"Analysis failed: {str(e)}")
            return self._error_response(url, str(e))
    
    def _calculate_confidence(self, missing_data: list) -> float:
        """Calculate confidence based on available data"""
        # Full confidence if all data available
        if not missing_data:
            return 1.0
        
        # Reduce confidence based on missing components
        penalty_per_component = 0.2
        confidence = 1.0 - (len(missing_data) * penalty_per_component)
        
        return max(confidence, 0.3)  # Minimum 30% confidence
    
    def _check_content_validity(self, content: Dict, scrape_result: Dict) -> Dict:
        """
        Check if extracted content is valid or if we got a block page,
        agreement page, error page, etc.

        Returns:
            Dict with is_valid, status, detail
        """
        result = {'is_valid': True, 'status': 'ok', 'detail': ''}

        # If scraping failed entirely
        if not scrape_result.get('success'):
            return {'is_valid': False, 'status': 'scrape_failed', 'detail': 'Scraping failed - could not load page'}

        # If content extraction failed
        if not content.get('success'):
            return {'is_valid': False, 'status': 'extraction_failed', 'detail': 'Content extraction failed'}

        title = (content.get('title', '') or '').lower().strip()
        word_count = content.get('word_count', 0)
        body_text = (content.get('body_text', '') or '').lower()

        # Check 1: Too little content
        if word_count < 50:
            return {
                'is_valid': False,
                'status': 'empty',
                'detail': f'Page has only {word_count} words - content did not load properly'
            }

        # Check 2: Block page titles
        block_indicators = [
            'access denied', '403 forbidden', '401 unauthorized',
            'blocked', 'captcha', 'robot check', 'are you human',
            'just a moment', 'checking your browser', 'attention required',
            'please wait', 'verify you are human', 'security check',
            'pardon our interruption', 'one more step'
        ]
        for indicator in block_indicators:
            if indicator in title:
                return {
                    'is_valid': False,
                    'status': 'blocked',
                    'detail': f"Page returned '{title}' - content is not the actual website"
                }

        # Check 3: Agreement/consent pages
        agreement_indicators = [
            'agreement', 'terms of use', 'terms of service',
            'consent', 'cookie policy', 'usage agreement',
            'accept terms', 'privacy notice'
        ]
        for indicator in agreement_indicators:
            if indicator in title:
                return {
                    'is_valid': False,
                    'status': 'agreement',
                    'detail': f"Page shows agreement/consent page instead of actual content"
                }

        # Check 4: Error pages
        error_indicators = [
            '404', 'not found', '500', 'server error',
            'something went wrong', 'page not available',
            'service unavailable', '502 bad gateway', '503'
        ]
        for indicator in error_indicators:
            if indicator in title:
                return {
                    'is_valid': False,
                    'status': 'error_page',
                    'detail': f"Page returned error: '{title}'"
                }

        # Check 5: JS render failure - lots of HTML but very little text
        # Use high thresholds to avoid false positives on minimal pages (e.g. google.com homepage)
        html_length = len(scrape_result.get('html', ''))
        if html_length > 50000 and word_count < 30:
            return {
                'is_valid': False,
                'status': 'js_render_fail',
                'detail': f'Large HTML ({html_length} chars) but only {word_count} words extracted - JavaScript may not have rendered'
            }

        return result

    def _determine_scam_type(self, risk_level: str, risk_score: int, category: str) -> str:
        """Determine scam type based on website category and risk level"""
        # LOW risk = no scam type
        if risk_level == 'LOW':
            return ''

        _CATEGORY_TO_SCAM_TYPE = {
            # Financial
            'banking': 'Suspected Banking Fraud',
            'credit_union': 'Suspected Banking Fraud',
            'insurance': 'Suspected Insurance Fraud',
            'investment': 'Suspected Investment Fraud',
            'stock_trading': 'Suspected Investment Fraud',
            'crypto_exchange': 'Suspected Crypto Fraud',
            'payment_service': 'Suspected Payment Fraud',
            'lending': 'Suspected Lending Fraud',
            # Shopping
            'ecommerce': 'Suspected Fake Online Store',
            'marketplace': 'Suspected Fake Marketplace',
            'auction': 'Suspected Auction Fraud',
            'classifieds': 'Suspected Classifieds Fraud',
            'grocery': 'Suspected Fake Online Store',
            'fashion': 'Suspected Fake Online Store',
            'electronics': 'Suspected Fake Online Store',
            # Government
            'government': 'Suspected Government Impersonation',
            'municipality': 'Suspected Government Impersonation',
            'military': 'Suspected Government Impersonation',
            'court': 'Suspected Government Impersonation',
            'tax_authority': 'Suspected Tax Scam',
            'public_service': 'Suspected Government Impersonation',
            # Health
            'hospital': 'Suspected Health Fraud',
            'clinic': 'Suspected Health Fraud',
            'pharmacy': 'Suspected Fake Pharmacy',
            'telehealth': 'Suspected Health Fraud',
            'mental_health': 'Suspected Health Fraud',
            # Education
            'university': 'Suspected Fake Education Site',
            'school': 'Suspected Fake Education Site',
            'online_course': 'Suspected Fake Education Site',
            'elearning': 'Suspected Fake Education Site',
            'language_learning': 'Suspected Fake Education Site',
            # Entertainment
            'streaming': 'Suspected Fake Streaming Site',
            'gaming': 'Suspected Gaming Fraud',
            'gambling': 'Suspected Illegal Gambling',
            'sports_betting': 'Suspected Illegal Gambling',
            'adult_content': 'Suspected Adult Content Scam',
            # Media
            'news': 'Suspected Fake News Site',
            'blog': 'Suspected Fake Blog',
            'forum': 'Suspected Fake Forum',
            'social_network': 'Suspected Social Engineering',
            'messaging': 'Suspected Social Engineering',
            # Services
            'legal': 'Suspected Fake Legal Service',
            'accounting': 'Suspected Fake Service',
            'real_estate': 'Suspected Real Estate Fraud',
            'travel': 'Suspected Travel Fraud',
            'job_board': 'Suspected Job Scam',
            'review_directory': 'Suspected Fake Reviews',
            'ride_delivery': 'Suspected Fake Service',
            # Technology
            'saas': 'Suspected Tech Support Scam',
            'cloud': 'Suspected Tech Support Scam',
            'web_hosting': 'Suspected Tech Support Scam',
            'vpn_proxy': 'Suspected Fake VPN/Privacy Scam',
            'developer_tools': 'Suspected Tech Support Scam',
            # Other
            'restaurant': 'Suspected Fake Business',
            'automotive': 'Suspected Fake Business',
            'pets': 'Suspected Fake Business',
            'nonprofit': 'Suspected Charity Fraud',
            'religious': 'Suspected Charity Fraud',
        }

        scam_type = _CATEGORY_TO_SCAM_TYPE.get(category, '')

        if not scam_type:
            if risk_level == 'HIGH':
                return 'Unknown Fraud Type - Requires Investigation'
            else:
                return 'Suspicious Activity Detected - Requires Investigation'

        return scam_type

    def _determine_risk_level(self, risk_score: int) -> str:
        """Determine risk level from score"""
        if risk_score < 30:
            return 'LOW'
        elif risk_score < 60:
            return 'MEDIUM'
        else:
            return 'HIGH'

    def _tiered_score_combination(self, ml_score: float, rules_score: float) -> float:
        """
        Tiered Decision Tree for combining ML and Rules scores.

        Tiers:
        1. ML very confident (>=92%) -> Trust ML completely
        2. ML says safe (<15%) but Rules suspicious -> Trust Rules
        3. ML moderately confident (>=70%) -> Weight ML higher
        4. Both uncertain -> Fall back to rules-weighted average

        Args:
            ml_score: ML classifier score (0.0-1.0, higher = more likely scam)
            rules_score: Rules engine score (0.0-1.0, higher = more suspicious)

        Returns:
            Combined risk score (0.0-1.0)
        """
        # Tier 1: ML is very confident it's a scam
        if ml_score >= 0.92:
            self.logger.debug(f"Tier 1: ML very confident ({ml_score:.2f}) -> score=0.92")
            return 0.92

        # Tier 2: ML thinks it's safe, but check if Rules disagree
        if ml_score <= 0.15:
            if rules_score >= 0.70:
                # Rules found strong signals ML missed
                self.logger.debug(f"Tier 2: ML safe ({ml_score:.2f}) but Rules suspicious ({rules_score:.2f}) -> score=0.75")
                return 0.75
            else:
                # Both agree it's relatively safe
                combined = (rules_score * 0.6) + (ml_score * 0.4)
                self.logger.debug(f"Tier 2: Both low -> score={combined:.2f}")
                return combined

        # Tier 3: ML moderately confident (70-92%)
        if ml_score >= 0.70:
            if rules_score >= 0.50:
                # Both signals present - high confidence scam
                self.logger.debug(f"Tier 3: ML confident ({ml_score:.2f}) + Rules agree ({rules_score:.2f}) -> score=0.80")
                return 0.80
            else:
                # ML confident but Rules weak - weight ML higher
                combined = (ml_score * 0.75) + (rules_score * 0.25)
                self.logger.debug(f"Tier 3: ML confident, Rules weak -> score={combined:.2f}")
                return combined

        # Tier 4: ML has medium confidence (15-70%)
        if rules_score >= 0.70:
            # Rules found strong signals
            self.logger.debug(f"Tier 4: Rules strong ({rules_score:.2f}), ML medium ({ml_score:.2f}) -> score=0.72")
            return 0.72

        # Tier 5: Both uncertain - use balanced weighted average
        combined = (rules_score * 0.5) + (ml_score * 0.5)
        self.logger.debug(f"Tier 5: Both uncertain -> score={combined:.2f}")
        return combined

    def _generate_red_flags(self, patterns: list, whois_data: dict) -> list:
        """Generate human-readable red flags"""
        flags = []
        
        for pattern in patterns:
            # Format pattern into readable flag
            if pattern['type'] == 'whois':
                if 'new_domain' in pattern['name']:
                    age = whois_data.get('age_days', 0)
                    flags.append(f"Very new domain ({age} days old)")
                elif pattern['name'] == 'privacy_protected':
                    flags.append("Domain privacy protection enabled")
                elif pattern['name'] == 'suspicious_location':
                    country = whois_data.get('country', 'Unknown')
                    flags.append(f"Registered in high-risk country ({country})")
                else:
                    flags.append(pattern['description'])
            else:
                flags.append(pattern['description'])
        
        return flags
    
    def _generate_recommendation(self, risk_level: str, category: str, 
                                 missing_data: list) -> str:
        """Generate recommendation based on analysis"""
        if missing_data:
            warning = f"⚠️ Incomplete analysis (missing: {', '.join(missing_data)}). "
        else:
            warning = ""
        
        if risk_level.lower() == 'high':
            return f"{warning}⚠️ HIGH RISK - Strong indicators of {category}. Do NOT engage or provide any personal/financial information."
        
        elif risk_level.lower() == 'medium':
            return f"{warning}⚠️ MEDIUM RISK - Some suspicious indicators detected. Exercise caution and verify authenticity before proceeding."
        
        elif risk_level.lower() == 'low':
            return f"{warning}✅ LOW RISK - Few or no scam indicators detected. Site appears relatively safe."
        
        else:
            return f"{warning}Unable to determine risk level. Manual verification recommended."
    
    def _convert_form_types(self, form_types: list) -> list:
        """Convert form type strings to integers for C# backend compatibility"""
        # Mapping: string form types to integer enum values
        form_type_map = {
            'email': 1,
            'password': 2,
            'login': 3,
            'payment': 4,
            'credit': 4,
            'card': 4,
            'search': 5,
            'contact': 6,
            'signup': 7,
            'register': 7,
            'subscribe': 8,
            'newsletter': 8,
        }

        result = []
        for ft in form_types:
            if isinstance(ft, int):
                result.append(ft)
            elif isinstance(ft, str):
                # Convert string to int, default to 0 (unknown) if not found
                result.append(form_type_map.get(ft.lower(), 0))

        return result

    def _error_response(self, url: str, error: str) -> Dict:
        """Generate error response"""
        return {
            'url': url,
            'analyzed_at': datetime.now().isoformat(),
            'error': error,
            'risk_assessment': {
                'risk_score': 0,  # Error = 0 (new scale: 0=error, 1=safe, 100=dangerous)
                'risk_level': 'UNKNOWN',
                'is_scam': False,
                'confidence': 0.0
            }
        }

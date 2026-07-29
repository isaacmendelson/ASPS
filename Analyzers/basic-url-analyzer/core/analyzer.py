"""
Main scam analyzer orchestrator.

Coordinates the analysis pipeline across all sub-components and returns
a single structured result dict.  Scoring policy, classification maps,
content validity checks and result formatting are each delegated to a
dedicated module under core/.
"""

import time
from datetime import datetime
from typing import Dict, Optional

# Language detection (optional dependency)
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
from .classification_maps import map_category_to_website_type, determine_scam_type
from .scoring import determine_risk_level, calculate_confidence, tiered_score_combination
from .content_validator import check_content_validity
from .result_formatter import generate_red_flags, generate_recommendation
from scrapers.playwright_scraper import PlaywrightScraper
from utils.logger import setup_logger
from utils.validators import URLValidator
from utils.cache_manager import CacheManager


class ScamAnalyzer:
    """Main scam detection analyzer."""

    def __init__(self, use_cache: bool = True, use_ml: bool = True, no_explain: bool = False):
        """Initialize analyzer.

        Args:
            use_cache: Whether to use caching.
            use_ml: Whether to use the ML classifier.
            no_explain: Whether to skip LLM explanation (for faster execution).
        """
        self.logger = setup_logger('scam_analyzer')
        self.no_explain = no_explain

        # Initialize pipeline components
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

    # ------------------------------------------------------------------
    # Public interface
    # ------------------------------------------------------------------

    def analyze_url(self, url: str) -> Dict:
        """Analyze a URL for scam indicators.

        Args:
            url: URL to analyze.

        Returns:
            Complete analysis result dict.  On success ``success`` is True and
            all sub-result keys are populated.  On failure ``success`` is False
            and ``error`` contains a structured ``{code, message}`` object.
        """
        start_time = time.time()

        try:
            # Validate URL (debug level — URLs are potentially sensitive)
            self.logger.debug("Starting analysis")

            is_valid, clean_url, error = self.validator.validate(url)
            if not is_valid:
                return self._error_response(url, f"Invalid URL: {error}")
            url = clean_url

            # Cache hit
            if self.use_cache:
                cached = self.cache.get(url)
                if cached:
                    self.logger.debug("Returning cached result")
                    cached['from_cache'] = True
                    return cached

            domain = self.validator.extract_domain(url)

            result = {
                'url': url,
                'domain': domain,
                'analyzed_at': datetime.now().isoformat(),
                'analysis_time_ms': 0,
                'from_cache': False,
            }

            missing_data = []
            warnings = []

            # ---- Step 0: URL string inspection (no network) ----
            url_inspection = self.url_inspector.inspect(url)

            # ---- Step 1: WHOIS lookup ----
            whois_data = self.whois_checker.check(domain)
            if not whois_data['success']:
                missing_data.append('WHOIS')
                warnings.append(f"WHOIS lookup failed: {whois_data['error']}")

            # ---- Step 1.5: Reputation check ----
            reputation_data = self.reputation_checker.check(domain)

            # ---- Step 2: Web scraping ----
            scrape_result = self.scraper.fetch(url)
            if not scrape_result['success']:
                missing_data.append('content')
                warnings.append(f"Web scraping failed: {scrape_result['error']}")

            # ---- Step 3: Content extraction ----
            content = {'success': False}
            if scrape_result['success']:
                content = self.content_extractor.extract(
                    scrape_result['html'],
                    scrape_result['final_url'],
                )
                if not content['success']:
                    missing_data.append('content_analysis')
                    warnings.append(f"Content extraction failed: {content['error']}")

            # ---- Step 3.5: Content validity check ----
            content_status = check_content_validity(content, scrape_result)
            if not content_status['is_valid']:
                self.logger.warning(
                    "Content not valid: %s - %s",
                    content_status['status'],
                    content_status['detail'],
                )
                warnings.append(f"Content may be incomplete - {content_status['detail']}")

            # ---- Step 4: Rules engine analysis ----
            analysis = self.rules_engine.analyze(content, whois_data)

            # ---- Step 4.5: Language detection ----
            detected_language = 'unknown'
            is_english = True  # Default when detection unavailable

            if LANGDETECT_AVAILABLE and content.get('success') and content.get('body_text'):
                try:
                    text_sample = content['body_text'][:1000]
                    detected_language = detect_language(text_sample)
                    is_english = detected_language == 'en'
                    self.logger.info(
                        "Language detected: %s (is_english=%s)", detected_language, is_english
                    )
                except LangDetectException:
                    self.logger.warning("Language detection failed, assuming English")
                    detected_language = 'unknown'
                    is_english = True

            # ---- Step 4.6: ML prediction ----
            ml_result = {'success': False, 'score': 0.0, 'note': 'ML disabled'}

            if not is_english:
                self.logger.info(
                    "Skipping ML for non-English content (%s)", detected_language
                )
                ml_result = {
                    'success': False,
                    'score': 0.0,
                    'note': f'Skipped for {detected_language} content',
                }
                # For non-English content, apply domain age override directly to rules score
                domain_age_days = whois_data.get('age_days', 0)
                rules_score = analysis.get('risk_score', 0)
                if domain_age_days > 3650:   # 10+ years
                    self.logger.info(
                        "Non-English + domain age override: %d days (10+ years) -> LOW",
                        domain_age_days,
                    )
                    analysis['risk_score'] = min(rules_score, 25)
                elif domain_age_days > 2555:  # 7+ years
                    self.logger.info(
                        "Non-English + domain age override: %d days (7+ years) -> LOW",
                        domain_age_days,
                    )
                    analysis['risk_score'] = min(rules_score, 25)
                elif domain_age_days > 1825:  # 5+ years
                    self.logger.info(
                        "Non-English + domain age override: %d days (5+ years) -> LOW",
                        domain_age_days,
                    )
                    analysis['risk_score'] = min(rules_score, 30)
                analysis['risk_level'] = determine_risk_level(analysis['risk_score'])
                analysis['is_scam'] = analysis['risk_score'] >= 61

            elif self.use_ml and content.get('success'):
                ml_result = self.ml_classifier.predict(content['body_text'])

                if ml_result['success']:
                    rules_score_norm = analysis.get('risk_score', 0) / 100.0
                    ml_score = ml_result['score']

                    domain_age_days = whois_data.get('age_days', 0)
                    has_suspicious_subdomain = 'suspicious_subdomain' in url_inspection.get('flags', [])

                    combined_score = self._apply_domain_age_override(
                        ml_score=ml_score,
                        rules_score=rules_score_norm,
                        domain_age_days=domain_age_days,
                        has_suspicious_subdomain=has_suspicious_subdomain,
                    )

                    analysis['risk_score'] = int(combined_score * 100)
                    analysis['risk_level'] = determine_risk_level(analysis['risk_score'])
                    analysis['is_scam'] = analysis['risk_score'] >= 61

            # ---- Step 5: Purpose classification ----
            classification = self.purpose_classifier.classify(
                content,
                analysis.get('detected_patterns', []),
            )

            # ---- Step 5.5: Category classification ----
            if content_status['is_valid']:
                category_result = self.category_classifier.classify(content, domain)
            else:
                self.logger.info(
                    "Skipping category classification - content not valid (%s)",
                    content_status['status'],
                )
                category_result = {
                    'success': False, 'category': 'unknown', 'category_group': 'unknown',
                    'name_en': 'Unknown', 'name_he': 'לא ידוע', 'confidence': 0.0,
                    'detection_method': 'none', 'matched_signals': [],
                    'secondary_category': None, 'secondary_confidence': 0.0,
                    'all_scores': {}, 'error': content_status['detail'],
                }

            raw_category = category_result.get('category', 'unknown')
            mapped_category = map_category_to_website_type(raw_category)

            # ---- Build complete result ----
            risk_score = analysis.get('risk_score', 0)
            risk_level = analysis.get('risk_level', 'UNKNOWN')

            scam_type = determine_scam_type(risk_level, raw_category)

            result.update({
                'success': True,
                'risk_assessment': {
                    'risk_score': risk_score,
                    'risk_level': risk_level,
                    'is_scam': analysis.get('is_scam', False),
                    'confidence': calculate_confidence(missing_data),
                },
                'scam_type': scam_type,
                'purpose': {
                    'category': mapped_category,
                    'confidence': classification.get('confidence', 0.0),
                    'description': classification.get('description', ''),
                },
                'whois': {
                    'success': whois_data.get('success', False),
                    'domain_age_days': whois_data.get('age_days', 0),
                    'created_date': whois_data.get('created_date'),
                    'registrar': whois_data.get('registrar', 'Unknown'),
                    'country': whois_data.get('country', 'Unknown'),
                    'privacy_protected': whois_data.get('privacy_protected', False),
                    'risk_score': whois_data.get('risk_score', 0.0),
                },
                'content_analysis': {
                    'success': content.get('success', False),
                    'title': content.get('title', ''),
                    'detected_patterns': analysis.get('detected_patterns', []),
                    'cta_count': content.get('cta_count', 0),
                    'form_types': [],  # Kept empty to avoid C# deserialization issues
                    'word_count': content.get('word_count', 0),
                    'detected_language': detected_language,
                    'is_english': is_english,
                },
                'ml_analysis': {
                    'enabled': self.use_ml,
                    'success': ml_result.get('success', False),
                    'score': ml_result.get('score', 0.0),
                    'confidence': ml_result.get('confidence', 0.0),
                    'note': ml_result.get('note', ''),
                },
                'reputation': {
                    'success': reputation_data.get('success', False),
                    'is_well_known': reputation_data.get('is_well_known', False),
                    'reputable_mentions': reputation_data.get('reputable_mentions', 0),
                    'mention_sources': reputation_data.get('mention_sources', []),
                    'reputation_score': reputation_data.get('reputation_score', 0.0),
                    'score_adjustment': reputation_data.get('score_adjustment', 0),
                },
                'content_status': content_status,
                'url_inspection': url_inspection,
                'website_category': {
                    'category': category_result.get('category', 'unknown'),
                    'category_group': category_result.get('category_group', 'unknown'),
                    'name_en': category_result.get('name_en', 'Unknown'),
                    'confidence': category_result.get('confidence', 0.0),
                    'detection_method': category_result.get('detection_method', 'none'),
                    'matched_signals': category_result.get('matched_signals', []),
                },
                'red_flags': generate_red_flags(
                    analysis.get('detected_patterns', []),
                    whois_data,
                ),
                'recommendation': generate_recommendation(
                    analysis.get('risk_level', 'UNKNOWN'),
                    classification.get('category', 'unknown'),
                    missing_data,
                ),
                'scraping_status': {
                    'success': scrape_result.get('success', False),
                    'status_code': scrape_result.get('status_code', 0),
                    'final_url': scrape_result.get('final_url', url),
                    'error': scrape_result.get('error', ''),
                },
                'warnings': warnings,
                'missing_data': missing_data,
            })

            elapsed = int((time.time() - start_time) * 1000)
            result['analysis_time_ms'] = elapsed

            if self.use_cache:
                self.cache.set(url, result)

            self.logger.info(
                "Analysis completed in %dms - Risk: %s",
                elapsed,
                result['risk_assessment']['risk_level'],
            )
            return result

        except Exception as exc:
            self.logger.error("Analysis failed: %s", str(exc), exc_info=True)
            return self._error_response(url, str(exc))

    # ------------------------------------------------------------------
    # Private helpers
    # ------------------------------------------------------------------

    # ------------------------------------------------------------------
    # Backward-compatible shims
    #
    # These instance methods preserve the surface that existing tests call
    # (e.g. analyzer._tiered_score_combination(...)).  They delegate to
    # the module-level functions in scoring.py and content_validator.py.
    # ------------------------------------------------------------------

    def _tiered_score_combination(self, ml_score: float, rules_score: float) -> float:
        """Shim — delegates to core.scoring.tiered_score_combination."""
        return tiered_score_combination(ml_score, rules_score, self.logger)

    def _check_content_validity(self, content: Dict, scrape_result: Dict) -> Dict:
        """Shim — delegates to core.content_validator.check_content_validity."""
        return check_content_validity(content, scrape_result)

    def _determine_risk_level(self, risk_score: int) -> str:
        """Shim — delegates to core.scoring.determine_risk_level."""
        return determine_risk_level(risk_score)

    def _calculate_confidence(self, missing_data: list) -> float:
        """Shim — delegates to core.scoring.calculate_confidence."""
        return calculate_confidence(missing_data)

    def _generate_red_flags(self, patterns: list, whois_data: dict) -> list:
        """Shim — delegates to core.result_formatter.generate_red_flags."""
        return generate_red_flags(patterns, whois_data)

    def _generate_recommendation(self, risk_level: str, category: str, missing_data: list) -> str:
        """Shim — delegates to core.result_formatter.generate_recommendation."""
        return generate_recommendation(risk_level, category, missing_data)

    def _determine_scam_type(self, risk_level: str, risk_score: int, category: str) -> str:
        """Shim — delegates to core.classification_maps.determine_scam_type."""
        return determine_scam_type(risk_level, category)

    def _apply_domain_age_override(
        self,
        ml_score: float,
        rules_score: float,
        domain_age_days: int,
        has_suspicious_subdomain: bool,
    ) -> float:
        """Apply domain-age trust heuristics before tiered score combination.

        Old, well-established domains are treated as lower risk unless:
        - The URL inspector flagged a suspicious subdomain (phishing subdomains
          on old registrable domains, e.g. paypal-login.evil.com).
        - The ML score is so high that the domain-age signal should be ignored.

        Args:
            ml_score: ML classifier score [0.0, 1.0].
            rules_score: Rules engine score [0.0, 1.0].
            domain_age_days: Registerable domain age in days from WHOIS.
            has_suspicious_subdomain: True when url_inspector flagged the subdomain.

        Returns:
            Combined risk score [0.0, 1.0].
        """
        if has_suspicious_subdomain:
            self.logger.info("Suspicious subdomain detected - skipping domain age override")
            return tiered_score_combination(ml_score, rules_score, self.logger)

        if domain_age_days > 5475 and ml_score < 0.995:  # 15+ years
            self.logger.info(
                "Domain age override: %d days (15+ years) -> LOW risk", domain_age_days
            )
            return min(ml_score, 0.25)

        if domain_age_days > 3650 and ml_score < 0.95:  # 10+ years
            self.logger.info(
                "Domain age override: %d days (10+ years) -> LOW risk", domain_age_days
            )
            return min(ml_score, 0.25)

        if domain_age_days > 2555 and ml_score < 0.40:  # 7+ years + ML says safe
            self.logger.info(
                "Domain age override: %d days (7+ years), ML says safe (%.2f) -> LOW risk",
                domain_age_days, ml_score,
            )
            return min(ml_score, 0.25)

        if domain_age_days > 2555 and rules_score <= 0.50 and ml_score < 0.90:  # 7+ years + moderate rules
            self.logger.info(
                "Domain age override: %d days (7+ years), rules moderate (%.2f) -> LOW risk",
                domain_age_days, rules_score,
            )
            return min(ml_score, 0.25)

        if domain_age_days > 1825 and rules_score <= 0.20 and ml_score < 0.85:  # 5+ years + low rules
            self.logger.info(
                "Domain age override: %d days (5+ years), rules low (%.2f) -> LOW risk",
                domain_age_days, rules_score,
            )
            return min(ml_score, 0.25)

        return tiered_score_combination(ml_score, rules_score, self.logger)

    def _error_response(self, url: str, error: str) -> Dict:
        """Generate a structured error response.

        Contract (ASPS-612):
        - ``success`` is always ``False``.
        - ``error`` is a structured object with ``code`` and ``message``,
          NOT a bare string, so the C# ``UrlAnalysisResult`` deserialiser
          can populate ``ErrorMessage`` correctly and set ``Success=false``.
        """
        lower = error.lower()
        if 'invalid url' in lower or 'bad scheme' in lower or 'browser internal' in lower:
            code = 'analyzer.invalid_url'
        elif 'timeout' in lower or 'timed out' in lower:
            code = 'analyzer.timeout'
        elif 'scraper' in lower or 'scraping' in lower:
            code = 'analyzer.scraper_failed'
        else:
            code = 'analyzer.internal_error'

        return {
            'success': False,
            'url': url,
            'analyzed_at': datetime.now().isoformat(),
            'error': {
                'code': code,
                'message': error,
            },
            'risk_assessment': {
                'risk_score': 0,
                'risk_level': 'UNKNOWN',
                'is_scam': False,
                'confidence': 0.0,
            },
        }

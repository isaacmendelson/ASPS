"""
Tests for language detection behavior in ScamAnalyzer (FR-046).

Language detection is inline in analyze_url() — all tests exercise it through
that public method with all external components mocked.

Observed behaviors:
  - English content → is_english=True, ML classifier is called
  - Non-English content → is_english=False, ML is skipped
  - Empty body_text → detection is skipped entirely (body_text is falsy)
  - LangDetectException → fallback: detected_language='unknown', is_english=True
  - LANGDETECT_AVAILABLE=False → same fallback
"""

import sys
from pathlib import Path
from unittest.mock import MagicMock, patch

import pytest

sys.path.insert(0, str(Path(__file__).parent.parent))

from core.analyzer import ScamAnalyzer
import core.analyzer as _analyzer_mod


# ---------------------------------------------------------------------------
# Default data stubs
# ---------------------------------------------------------------------------

_WHOIS_OK = {
    'success': True, 'age_days': 500,
    'created_date': '2022-01-01', 'registrar': 'Test Registrar',
    'country': 'US', 'privacy_protected': False,
    'risk_score': 0.0, 'risk_factors': {},
}

_REPUTATION_OK = {
    'success': True, 'is_well_known': False,
    'reputable_mentions': 0, 'mention_sources': [],
    'reputation_score': 0.0, 'score_adjustment': 0,
}

_URL_INSPECT_CLEAN = {
    'success': True, 'flags': [], 'risk_level': 'LOW', 'url_risk_score': 0,
}

_SCRAPE_OK = {
    'success': True,
    'html': '<html>' + ' '.join(['word'] * 200) + '</html>',
    'final_url': 'https://example.com',
    'status_code': 200,
    'error': '',
}

_CONTENT_OK = {
    'success': True,
    'title': 'Test Page',
    'body_text': ' '.join(['word'] * 200),
    'word_count': 200,
    'meta_description': '',
    'headings': {'h1': [], 'h2': []},
    'cta_buttons': [],
    'cta_count': 0,
    'forms': {'types': []},
    'links': {
        'internal': 10, 'external': 5,
        'has_suspicious': False, 'suspicious': [],
    },
}

_RULES_OK = {
    'success': True, 'risk_score': 30, 'risk_level': 'MEDIUM',
    'is_scam': False, 'detected_patterns': [], 'pattern_count': 0, 'error': '',
}

_PURPOSE_OK = {
    'category': 'unknown', 'confidence': 0.5,
    'description': 'Test site', 'is_scam': False,
}

_CATEGORY_OK = {
    'success': True, 'category': 'unknown', 'category_group': 'unknown',
    'name_en': 'Unknown', 'name_he': 'Unknown', 'confidence': 0.5,
    'detection_method': 'test', 'matched_signals': [],
    'secondary_category': None, 'secondary_confidence': 0.0, 'all_scores': {},
}

_ML_SAFE = {
    'success': True, 'score': 0.20, 'confidence': 0.80, 'note': '',
}


# ---------------------------------------------------------------------------
# Fixture
# ---------------------------------------------------------------------------

@pytest.fixture
def analyzer():
    """ScamAnalyzer with __init__ bypassed and all dependencies stubbed."""
    with patch.object(ScamAnalyzer, '__init__', return_value=None):
        sa = ScamAnalyzer()

    sa.logger = MagicMock()
    sa.no_explain = False
    sa.use_ml = True
    sa.use_cache = False

    sa.validator = MagicMock()
    sa.validator.validate.return_value = (True, 'https://example.com', '')
    sa.validator.extract_domain.return_value = 'example.com'

    sa.whois_checker = MagicMock()
    sa.whois_checker.check.return_value = _WHOIS_OK.copy()

    sa.reputation_checker = MagicMock()
    sa.reputation_checker.check.return_value = _REPUTATION_OK.copy()

    sa.url_inspector = MagicMock()
    sa.url_inspector.inspect.return_value = _URL_INSPECT_CLEAN.copy()

    sa.scraper = MagicMock()
    sa.scraper.fetch.return_value = _SCRAPE_OK.copy()

    sa.content_extractor = MagicMock()
    sa.content_extractor.extract.return_value = _CONTENT_OK.copy()

    sa.rules_engine = MagicMock()
    sa.rules_engine.analyze.return_value = _RULES_OK.copy()

    sa.purpose_classifier = MagicMock()
    sa.purpose_classifier.classify.return_value = _PURPOSE_OK.copy()

    sa.category_classifier = MagicMock()
    sa.category_classifier.classify.return_value = _CATEGORY_OK.copy()

    sa.ml_classifier = MagicMock()
    sa.ml_classifier.predict.return_value = _ML_SAFE.copy()

    return sa


# ---------------------------------------------------------------------------
# English content
# ---------------------------------------------------------------------------

class TestEnglishContent:

    def test_english_body_sets_is_english_true(self, analyzer):
        """English content → is_english=True in content_analysis output."""
        with patch('core.analyzer.LANGDETECT_AVAILABLE', True), \
             patch('core.analyzer.detect_language', return_value='en'):
            result = analyzer.analyze_url('https://example.com')
        assert result['content_analysis']['is_english'] is True

    def test_english_body_detected_language_is_en(self, analyzer):
        """English content → detected_language='en' in content_analysis output."""
        with patch('core.analyzer.LANGDETECT_AVAILABLE', True), \
             patch('core.analyzer.detect_language', return_value='en'):
            result = analyzer.analyze_url('https://example.com')
        assert result['content_analysis']['detected_language'] == 'en'

    def test_english_content_ml_classifier_is_called(self, analyzer):
        """English content with use_ml=True → ML predict() is invoked exactly once."""
        with patch('core.analyzer.LANGDETECT_AVAILABLE', True), \
             patch('core.analyzer.detect_language', return_value='en'):
            analyzer.analyze_url('https://example.com')
        analyzer.ml_classifier.predict.assert_called_once()

    def test_english_content_ml_note_is_not_skip(self, analyzer):
        """English content → ml_analysis.note does NOT indicate skip."""
        with patch('core.analyzer.LANGDETECT_AVAILABLE', True), \
             patch('core.analyzer.detect_language', return_value='en'):
            result = analyzer.analyze_url('https://example.com')
        # ML was attempted; note should not say 'Skipped'
        assert 'Skipped' not in result['ml_analysis']['note']


# ---------------------------------------------------------------------------
# Hebrew content
# ---------------------------------------------------------------------------

class TestHebrewContent:

    def test_hebrew_body_sets_is_english_false(self, analyzer):
        """Hebrew body text → is_english=False."""
        with patch('core.analyzer.LANGDETECT_AVAILABLE', True), \
             patch('core.analyzer.detect_language', return_value='he'):
            result = analyzer.analyze_url('https://example.com')
        assert result['content_analysis']['is_english'] is False

    def test_hebrew_detected_language_is_he(self, analyzer):
        """Hebrew content → detected_language='he'."""
        with patch('core.analyzer.LANGDETECT_AVAILABLE', True), \
             patch('core.analyzer.detect_language', return_value='he'):
            result = analyzer.analyze_url('https://example.com')
        assert result['content_analysis']['detected_language'] == 'he'

    def test_hebrew_content_skips_ml_classifier(self, analyzer):
        """Non-English content → ML predict() is NOT called."""
        with patch('core.analyzer.LANGDETECT_AVAILABLE', True), \
             patch('core.analyzer.detect_language', return_value='he'):
            analyzer.analyze_url('https://example.com')
        analyzer.ml_classifier.predict.assert_not_called()

    def test_hebrew_content_ml_note_mentions_language(self, analyzer):
        """Non-English → ml_analysis.note contains the detected language code."""
        with patch('core.analyzer.LANGDETECT_AVAILABLE', True), \
             patch('core.analyzer.detect_language', return_value='he'):
            result = analyzer.analyze_url('https://example.com')
        assert 'he' in result['ml_analysis']['note']

    def test_hebrew_ml_success_flag_false(self, analyzer):
        """Non-English → ml_analysis.success=False (ML was not run)."""
        with patch('core.analyzer.LANGDETECT_AVAILABLE', True), \
             patch('core.analyzer.detect_language', return_value='he'):
            result = analyzer.analyze_url('https://example.com')
        assert result['ml_analysis']['success'] is False


# ---------------------------------------------------------------------------
# Russian content
# ---------------------------------------------------------------------------

class TestRussianContent:

    def test_russian_content_is_not_english(self, analyzer):
        """Russian content → is_english=False, detected_language='ru'."""
        with patch('core.analyzer.LANGDETECT_AVAILABLE', True), \
             patch('core.analyzer.detect_language', return_value='ru'):
            result = analyzer.analyze_url('https://example.com')
        assert result['content_analysis']['is_english'] is False
        assert result['content_analysis']['detected_language'] == 'ru'

    def test_russian_content_skips_ml(self, analyzer):
        """Russian content → ML predict() is not called."""
        with patch('core.analyzer.LANGDETECT_AVAILABLE', True), \
             patch('core.analyzer.detect_language', return_value='ru'):
            analyzer.analyze_url('https://example.com')
        analyzer.ml_classifier.predict.assert_not_called()


# ---------------------------------------------------------------------------
# Short content / LangDetectException fallback
# ---------------------------------------------------------------------------

class TestShortContentFallback:

    def test_langdetect_exception_does_not_crash(self, analyzer):
        """LangDetectException from detect_language → analyze_url completes normally."""
        # Use the exception class registered in the module (works whether or not
        # langdetect is installed: the module sets LangDetectException=Exception if absent).
        exc = _analyzer_mod.LangDetectException(0, 'no features in profile')
        with patch('core.analyzer.LANGDETECT_AVAILABLE', True), \
             patch('core.analyzer.detect_language', side_effect=exc):
            result = analyzer.analyze_url('https://example.com')
        assert 'risk_assessment' in result

    def test_langdetect_exception_falls_back_to_english(self, analyzer):
        """LangDetectException → fallback: is_english=True, detected_language='unknown'."""
        exc = _analyzer_mod.LangDetectException(0, 'no features in profile')
        with patch('core.analyzer.LANGDETECT_AVAILABLE', True), \
             patch('core.analyzer.detect_language', side_effect=exc):
            result = analyzer.analyze_url('https://example.com')
        assert result['content_analysis']['is_english'] is True
        assert result['content_analysis']['detected_language'] == 'unknown'

    def test_langdetect_unavailable_defaults_to_english(self, analyzer):
        """When langdetect is not installed (LANGDETECT_AVAILABLE=False) → is_english=True."""
        with patch('core.analyzer.LANGDETECT_AVAILABLE', False):
            result = analyzer.analyze_url('https://example.com')
        assert result['content_analysis']['is_english'] is True
        assert result['content_analysis']['detected_language'] == 'unknown'


# ---------------------------------------------------------------------------
# Empty body text
# ---------------------------------------------------------------------------

class TestEmptyBodyText:

    def test_empty_body_text_skips_detection_entirely(self, analyzer):
        """Empty body_text is falsy → detect_language is never called."""
        empty_content = {**_CONTENT_OK, 'body_text': '', 'word_count': 0}
        analyzer.content_extractor.extract.return_value = empty_content
        mock_detect = MagicMock(return_value='en')
        with patch('core.analyzer.LANGDETECT_AVAILABLE', True), \
             patch('core.analyzer.detect_language', mock_detect):
            analyzer.analyze_url('https://example.com')
        mock_detect.assert_not_called()

    def test_empty_body_defaults_to_unknown_language(self, analyzer):
        """Empty body → detected_language='unknown', is_english=True (default)."""
        empty_content = {**_CONTENT_OK, 'body_text': '', 'word_count': 0}
        analyzer.content_extractor.extract.return_value = empty_content
        with patch('core.analyzer.LANGDETECT_AVAILABLE', True):
            result = analyzer.analyze_url('https://example.com')
        assert result['content_analysis']['detected_language'] == 'unknown'
        assert result['content_analysis']['is_english'] is True


# ---------------------------------------------------------------------------
# Non-English pages still receive rules-engine scoring
# ---------------------------------------------------------------------------

class TestNonEnglishRulesScore:

    def test_non_english_rules_score_applied_unchanged_for_new_domain(self, analyzer):
        """Non-English page with a new domain → rules score is used as-is (no age override)."""
        analyzer.rules_engine.analyze.return_value = {
            **_RULES_OK, 'risk_score': 65, 'risk_level': 'HIGH', 'is_scam': True,
        }
        # age_days=100 → below all non-English age override thresholds
        analyzer.whois_checker.check.return_value = {**_WHOIS_OK, 'age_days': 100}
        with patch('core.analyzer.LANGDETECT_AVAILABLE', True), \
             patch('core.analyzer.detect_language', return_value='fr'):
            result = analyzer.analyze_url('https://example.com')
        assert result['risk_assessment']['risk_score'] == 65

    def test_non_english_result_contains_risk_assessment(self, analyzer):
        """Non-English page analysis always returns a complete risk_assessment block."""
        with patch('core.analyzer.LANGDETECT_AVAILABLE', True), \
             patch('core.analyzer.detect_language', return_value='zh-cn'):
            result = analyzer.analyze_url('https://example.com')
        assert 'risk_assessment' in result
        assert 'risk_score' in result['risk_assessment']
        assert 'risk_level' in result['risk_assessment']

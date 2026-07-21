"""
Tests for domain-age override logic in ScamAnalyzer (FR-048).

The override is inline in analyze_url() — tested via that public method
with all external components mocked.  All tests force the English/ML path
so the override branches are reachable.

Override thresholds (from the implementation):
  > 5475 days (≈15 yr): cap combined_score at 0.25 unless ml_score >= 0.995
  > 3650 days (≈10 yr): cap combined_score at 0.25 unless ml_score >= 0.95
  > 2555 days (≈7 yr) : cap at 0.25 if ml_score < 0.40
                        OR (rules_score <= 0.50 AND ml_score < 0.90)
  > 1825 days (≈5 yr) : cap at 0.25 if rules_score <= 0.20 AND ml_score < 0.85
  has_suspicious_subdomain=True: override is suspended; tiered combination is used

All age values are in days.  1 year ≈ 365 days.
"""

import sys
from pathlib import Path
from unittest.mock import MagicMock, patch

import pytest

sys.path.insert(0, str(Path(__file__).parent.parent))

from core.analyzer import ScamAnalyzer


# ---------------------------------------------------------------------------
# Default stubs
# ---------------------------------------------------------------------------

_WHOIS_TEMPLATE = {
    'success': True, 'age_days': 1000,
    'created_date': '2015-01-01', 'registrar': 'Test Registrar',
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

_URL_INSPECT_SUSPICIOUS_SUBDOMAIN = {
    'success': True, 'flags': ['suspicious_subdomain'],
    'risk_level': 'HIGH', 'url_risk_score': 50,
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

_RULES_MEDIUM = {
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


# ---------------------------------------------------------------------------
# Fixture factory
# ---------------------------------------------------------------------------

def _make_analyzer(
    *,
    age_days: int,
    ml_score: float,
    rules_score: int = 30,
    has_suspicious_subdomain: bool = False,
) -> ScamAnalyzer:
    """
    Build a fully-stubbed ScamAnalyzer configured for domain-age override testing.

    All tests patch LANGDETECT_AVAILABLE=False so the English path is taken
    deterministically without needing a real langdetect installation.
    """
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
    sa.whois_checker.check.return_value = {**_WHOIS_TEMPLATE, 'age_days': age_days}

    sa.reputation_checker = MagicMock()
    sa.reputation_checker.check.return_value = _REPUTATION_OK.copy()

    sa.url_inspector = MagicMock()
    sa.url_inspector.inspect.return_value = (
        _URL_INSPECT_SUSPICIOUS_SUBDOMAIN.copy()
        if has_suspicious_subdomain
        else _URL_INSPECT_CLEAN.copy()
    )

    sa.scraper = MagicMock()
    sa.scraper.fetch.return_value = _SCRAPE_OK.copy()

    sa.content_extractor = MagicMock()
    sa.content_extractor.extract.return_value = _CONTENT_OK.copy()

    sa.rules_engine = MagicMock()
    sa.rules_engine.analyze.return_value = {
        **_RULES_MEDIUM, 'risk_score': rules_score,
    }

    sa.purpose_classifier = MagicMock()
    sa.purpose_classifier.classify.return_value = _PURPOSE_OK.copy()

    sa.category_classifier = MagicMock()
    sa.category_classifier.classify.return_value = _CATEGORY_OK.copy()

    sa.ml_classifier = MagicMock()
    sa.ml_classifier.predict.return_value = {
        'success': True, 'score': ml_score, 'confidence': 0.80, 'note': '',
    }

    return sa


def _run(sa: ScamAnalyzer) -> dict:
    """Run analyze_url() with langdetect disabled (English path is forced)."""
    with patch('core.analyzer.LANGDETECT_AVAILABLE', False):
        return sa.analyze_url('https://example.com')


# ---------------------------------------------------------------------------
# 15-year-old domain (> 5475 days)
# ---------------------------------------------------------------------------

class TestDomainAge20Years:

    def test_20yr_domain_ml_50_risk_capped_at_25(self):
        """20-yr domain + ML=0.50 → combined_score=min(0.50,0.25)=0.25 → risk_score=25."""
        sa = _make_analyzer(age_days=7300, ml_score=0.50)
        result = _run(sa)
        assert result['risk_assessment']['risk_score'] == 25

    def test_20yr_domain_very_high_ml_override_not_applied(self):
        """20-yr domain + ML=0.999 (>= 0.995) → override skipped → Tier 1 fires → score=92."""
        sa = _make_analyzer(age_days=7300, ml_score=0.999)
        result = _run(sa)
        # Tier 1 returns 0.92 → risk_score = 92
        assert result['risk_assessment']['risk_score'] == 92

    def test_20yr_domain_ml_at_0_994_still_overridden(self):
        """ml=0.994 is just below the 0.995 threshold → override still applied."""
        sa = _make_analyzer(age_days=7300, ml_score=0.994)
        result = _run(sa)
        assert result['risk_assessment']['risk_score'] == 25

    def test_20yr_domain_risk_level_is_low(self):
        """Capped risk_score=25 falls in LOW tier (< 30)."""
        sa = _make_analyzer(age_days=7300, ml_score=0.60)
        result = _run(sa)
        assert result['risk_assessment']['risk_level'] == 'LOW'


# ---------------------------------------------------------------------------
# 10-year-old domain (> 3650 days)
# ---------------------------------------------------------------------------

class TestDomainAge12Years:

    def test_12yr_domain_ml_70_risk_capped_at_25(self):
        """12-yr domain + ML=0.70 → cap applies (0.70 < 0.95) → risk_score=25."""
        sa = _make_analyzer(age_days=4380, ml_score=0.70)
        result = _run(sa)
        assert result['risk_assessment']['risk_score'] == 25

    def test_12yr_domain_ml_96_override_not_applied(self):
        """12-yr domain + ML=0.96 (>= 0.95) → 10-yr override skipped → Tier 1 fires."""
        sa = _make_analyzer(age_days=4380, ml_score=0.96)
        result = _run(sa)
        # ml_score=0.96 >= 0.92 → Tier 1 → score=92
        assert result['risk_assessment']['risk_score'] == 92

    def test_12yr_domain_ml_exactly_0_95_not_overridden(self):
        """ml=0.95 exactly meets the skip threshold → 10-yr override not applied."""
        sa = _make_analyzer(age_days=4380, ml_score=0.95)
        result = _run(sa)
        # 0.95 >= 0.95 → skip; 0.95 >= 0.92 → Tier 1 → 92
        assert result['risk_assessment']['risk_score'] == 92

    def test_12yr_domain_ml_exactly_0_94_is_overridden(self):
        """ml=0.94 is below 0.95 → 10-yr override applies → capped at 25."""
        sa = _make_analyzer(age_days=4380, ml_score=0.94)
        result = _run(sa)
        assert result['risk_assessment']['risk_score'] == 25


# ---------------------------------------------------------------------------
# Young domain (3 years) — no override
# ---------------------------------------------------------------------------

class TestDomainAge3Years:

    def test_3yr_domain_no_override_applied(self):
        """3-yr domain (< 1825 days) → no age override → tiered combination used."""
        sa = _make_analyzer(age_days=1095, ml_score=0.50, rules_score=30)
        result = _run(sa)
        # Tier 5: (0.30 * 0.5) + (0.50 * 0.5) = 0.40 → risk_score=40
        assert result['risk_assessment']['risk_score'] == 40

    def test_3yr_domain_high_ml_preserves_high_score(self):
        """3-yr domain with high ML score → no override → high risk preserved."""
        sa = _make_analyzer(age_days=1095, ml_score=0.95, rules_score=30)
        result = _run(sa)
        # Tier 1 (ml >= 0.92) → 0.92 → score=92
        assert result['risk_assessment']['risk_score'] == 92

    def test_3yr_domain_result_not_capped_at_25(self):
        """3-yr domain → final score is NOT capped at 25."""
        sa = _make_analyzer(age_days=1095, ml_score=0.50)
        result = _run(sa)
        assert result['risk_assessment']['risk_score'] != 25


# ---------------------------------------------------------------------------
# Suspicious subdomain suspends the override
# ---------------------------------------------------------------------------

class TestSuspiciousSubdomainSuspendsOverride:

    def test_suspicious_subdomain_old_domain_override_suspended(self):
        """20-yr domain WITH suspicious subdomain → age override suspended, tiered combo used."""
        sa = _make_analyzer(
            age_days=7300, ml_score=0.50, rules_score=30,
            has_suspicious_subdomain=True,
        )
        result = _run(sa)
        # Tiered combination: Tier 5 → (0.30*0.5) + (0.50*0.5) = 0.40 → score=40
        # The important thing: NOT capped at 25
        assert result['risk_assessment']['risk_score'] != 25
        assert result['risk_assessment']['risk_score'] > 25

    def test_suspicious_subdomain_20yr_domain_ml_50_score_above_25(self):
        """Suspicious subdomain flag prevents LOW-risk cap even for very old domain."""
        sa = _make_analyzer(
            age_days=7300, ml_score=0.50,
            has_suspicious_subdomain=True,
        )
        result = _run(sa)
        assert result['risk_assessment']['risk_score'] > 25

    def test_no_suspicious_subdomain_old_domain_override_fires(self):
        """Without suspicious subdomain flag, the 15-yr domain override applies normally."""
        sa = _make_analyzer(
            age_days=7300, ml_score=0.50,
            has_suspicious_subdomain=False,
        )
        result = _run(sa)
        assert result['risk_assessment']['risk_score'] == 25


# ---------------------------------------------------------------------------
# 7–8-year-old domain (> 2555 days, ML safe path)
# ---------------------------------------------------------------------------

class TestDomainAge8Years:

    def test_8yr_domain_ml_safe_score_below_25(self):
        """8-yr domain + ML=0.20 (< 0.40, 'safe') → combined=min(0.20,0.25)=0.20 → score ≤ 25."""
        sa = _make_analyzer(age_days=2920, ml_score=0.20)
        result = _run(sa)
        assert result['risk_assessment']['risk_score'] <= 25

    def test_8yr_domain_ml_safe_score_is_20_not_25(self):
        """8-yr + ML=0.20 → min(0.20, 0.25)=0.20 → risk_score=20, not 25."""
        sa = _make_analyzer(age_days=2920, ml_score=0.20)
        result = _run(sa)
        assert result['risk_assessment']['risk_score'] == 20

    def test_8yr_domain_ml_above_0_40_no_safe_path_override(self):
        """8-yr domain + ML=0.50 (>= 0.40) → safe-path override not triggered.

        Falls through to rules-moderate check: rules_score=0.30 <= 0.50 and ml < 0.90
        → combined_score = min(0.50, 0.25) = 0.25 → score=25.
        """
        sa = _make_analyzer(age_days=2920, ml_score=0.50, rules_score=30)
        result = _run(sa)
        # rules_score=30/100=0.30 <= 0.50 AND ml_score=0.50 < 0.90 → cap applies
        assert result['risk_assessment']['risk_score'] == 25


# ---------------------------------------------------------------------------
# Output structure is always complete
# ---------------------------------------------------------------------------

class TestOutputStructure:

    def test_override_result_contains_risk_assessment(self):
        """Regardless of override path, result has a complete risk_assessment block."""
        sa = _make_analyzer(age_days=7300, ml_score=0.50)
        result = _run(sa)
        assert 'risk_assessment' in result
        assert 'risk_score' in result['risk_assessment']
        assert 'risk_level' in result['risk_assessment']
        assert 'is_scam' in result['risk_assessment']

    def test_overridden_score_within_bounds(self):
        """The final risk_score after any override is in [0, 100]."""
        for age_days, ml_score in [(7300, 0.50), (4380, 0.70), (1095, 0.95)]:
            sa = _make_analyzer(age_days=age_days, ml_score=ml_score)
            result = _run(sa)
            assert 0 <= result['risk_assessment']['risk_score'] <= 100

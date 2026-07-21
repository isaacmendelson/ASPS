"""
Tests for ScamAnalyzer._check_content_validity() (FR-047).

Tests the method that distinguishes real page content from block pages,
CAPTCHA challenges, agreement gates, error pages, and JS-render failures.
"""

import sys
from pathlib import Path
from unittest.mock import MagicMock, patch

import pytest

sys.path.insert(0, str(Path(__file__).parent.parent))

from core.analyzer import ScamAnalyzer


# ---------------------------------------------------------------------------
# Fixtures
# ---------------------------------------------------------------------------

@pytest.fixture
def analyzer():
    """ScamAnalyzer with __init__ bypassed — only logger is required."""
    with patch.object(ScamAnalyzer, '__init__', return_value=None):
        sa = ScamAnalyzer()
    sa.logger = MagicMock()
    return sa


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _content(
    success=True,
    title='Normal Page Title',
    word_count=100,
    body_text=None,
):
    """Build a minimal content dict."""
    if body_text is None:
        body_text = 'word ' * word_count
    return {
        'success': success,
        'title': title,
        'word_count': word_count,
        'body_text': body_text,
    }


def _scrape(
    success=True,
    html=None,
    status_code=200,
):
    """Build a minimal scrape-result dict."""
    if html is None:
        html = '<html>' + 'word ' * 100 + '</html>'
    return {
        'success': success,
        'html': html,
        'final_url': 'https://example.com',
        'status_code': status_code,
        'error': '' if success else 'Connection failed',
    }


# ---------------------------------------------------------------------------
# Normal / valid pages
# ---------------------------------------------------------------------------

class TestValidPages:

    def test_normal_page_is_valid(self, analyzer):
        """Regular page with title and 100 words returns is_valid=True."""
        result = analyzer._check_content_validity(_content(), _scrape())
        assert result['is_valid'] is True
        assert result['status'] == 'ok'
        assert result['detail'] == ''

    def test_exactly_fifty_words_is_valid(self, analyzer):
        """Page with exactly 50 words passes the word-count threshold."""
        result = analyzer._check_content_validity(
            _content(word_count=50), _scrape()
        )
        assert result['is_valid'] is True

    def test_large_page_is_valid(self, analyzer):
        """Page with 500 words and a normal title returns is_valid=True."""
        result = analyzer._check_content_validity(
            _content(word_count=500), _scrape()
        )
        assert result['is_valid'] is True

    def test_result_is_dict_with_required_keys(self, analyzer):
        """Return value always contains is_valid, status, and detail keys."""
        result = analyzer._check_content_validity(_content(), _scrape())
        assert {'is_valid', 'status', 'detail'}.issubset(result.keys())


# ---------------------------------------------------------------------------
# Scrape / extraction failures
# ---------------------------------------------------------------------------

class TestScrapeAndExtractionFailures:

    def test_scrape_failure_returns_invalid(self, analyzer):
        """Failed scrape (success=False) → is_valid=False, status='scrape_failed'."""
        result = analyzer._check_content_validity(_content(), _scrape(success=False))
        assert result['is_valid'] is False
        assert result['status'] == 'scrape_failed'

    def test_content_extraction_failure_returns_invalid(self, analyzer):
        """Content extraction failure (success=False) → is_valid=False, status='extraction_failed'."""
        result = analyzer._check_content_validity(
            _content(success=False), _scrape()
        )
        assert result['is_valid'] is False
        assert result['status'] == 'extraction_failed'

    def test_scrape_failure_takes_priority_over_content(self, analyzer):
        """Scrape failure is detected before content failure is checked."""
        result = analyzer._check_content_validity(
            _content(success=False), _scrape(success=False)
        )
        assert result['is_valid'] is False
        assert result['status'] == 'scrape_failed'


# ---------------------------------------------------------------------------
# Word count — too little content
# ---------------------------------------------------------------------------

class TestWordCount:

    def test_zero_words_invalid(self, analyzer):
        """Page with 0 words is invalid."""
        result = analyzer._check_content_validity(
            _content(word_count=0, body_text=''), _scrape()
        )
        assert result['is_valid'] is False
        assert result['status'] == 'empty'

    def test_forty_nine_words_invalid(self, analyzer):
        """49 words is one below the 50-word threshold — invalid."""
        result = analyzer._check_content_validity(
            _content(word_count=49), _scrape()
        )
        assert result['is_valid'] is False
        assert result['status'] == 'empty'

    def test_empty_detail_mentions_word_count(self, analyzer):
        """Empty-status detail string contains the actual word count."""
        result = analyzer._check_content_validity(
            _content(word_count=10), _scrape()
        )
        assert '10' in result['detail']


# ---------------------------------------------------------------------------
# Block pages (Cloudflare, 403, CAPTCHA, etc.)
# ---------------------------------------------------------------------------

class TestBlockPages:

    def test_cloudflare_just_a_moment(self, analyzer):
        """Cloudflare challenge page ('just a moment' title) → is_valid=False, status='blocked'."""
        result = analyzer._check_content_validity(
            _content(title='just a moment', word_count=60), _scrape()
        )
        assert result['is_valid'] is False
        assert result['status'] == 'blocked'

    def test_http_403_forbidden_title(self, analyzer):
        """'403 forbidden' in title → blocked status."""
        result = analyzer._check_content_validity(
            _content(title='403 forbidden', word_count=60), _scrape()
        )
        assert result['is_valid'] is False
        assert result['status'] == 'blocked'

    def test_captcha_title(self, analyzer):
        """'captcha' in page title → blocked status."""
        result = analyzer._check_content_validity(
            _content(title='captcha', word_count=60), _scrape()
        )
        assert result['is_valid'] is False
        assert result['status'] == 'blocked'

    def test_access_denied_title(self, analyzer):
        """'access denied' in title → blocked status."""
        result = analyzer._check_content_validity(
            _content(title='access denied', word_count=60), _scrape()
        )
        assert result['is_valid'] is False
        assert result['status'] == 'blocked'

    def test_robot_check_title(self, analyzer):
        """'robot check' in title → blocked status."""
        result = analyzer._check_content_validity(
            _content(title='robot check', word_count=60), _scrape()
        )
        assert result['is_valid'] is False
        assert result['status'] == 'blocked'

    def test_verify_you_are_human_title(self, analyzer):
        """'verify you are human' Cloudflare variant → blocked status."""
        result = analyzer._check_content_validity(
            _content(title='verify you are human', word_count=60), _scrape()
        )
        assert result['is_valid'] is False
        assert result['status'] == 'blocked'

    def test_security_check_title(self, analyzer):
        """'security check' title → blocked status."""
        result = analyzer._check_content_validity(
            _content(title='security check', word_count=60), _scrape()
        )
        assert result['is_valid'] is False
        assert result['status'] == 'blocked'

    def test_block_check_is_case_insensitive(self, analyzer):
        """Title check is case-insensitive (title is lowercased before comparison)."""
        result = analyzer._check_content_validity(
            _content(title='Just A Moment', word_count=60), _scrape()
        )
        assert result['is_valid'] is False
        assert result['status'] == 'blocked'


# ---------------------------------------------------------------------------
# Agreement / consent pages
# ---------------------------------------------------------------------------

class TestAgreementPages:

    def test_terms_of_service_title(self, analyzer):
        """'terms of service' title → is_valid=False, status='agreement'."""
        result = analyzer._check_content_validity(
            _content(title='terms of service', word_count=60), _scrape()
        )
        assert result['is_valid'] is False
        assert result['status'] == 'agreement'

    def test_cookie_policy_title(self, analyzer):
        """'cookie policy' title → agreement status."""
        result = analyzer._check_content_validity(
            _content(title='cookie policy', word_count=60), _scrape()
        )
        assert result['is_valid'] is False
        assert result['status'] == 'agreement'

    def test_consent_title(self, analyzer):
        """'consent' in title → agreement status."""
        result = analyzer._check_content_validity(
            _content(title='consent', word_count=60), _scrape()
        )
        assert result['is_valid'] is False
        assert result['status'] == 'agreement'

    def test_privacy_notice_title(self, analyzer):
        """'privacy notice' in title → agreement status."""
        result = analyzer._check_content_validity(
            _content(title='privacy notice', word_count=60), _scrape()
        )
        assert result['is_valid'] is False
        assert result['status'] == 'agreement'

    def test_usage_agreement_title(self, analyzer):
        """'usage agreement' in title → agreement status."""
        result = analyzer._check_content_validity(
            _content(title='usage agreement', word_count=60), _scrape()
        )
        assert result['is_valid'] is False
        assert result['status'] == 'agreement'


# ---------------------------------------------------------------------------
# Error pages
# ---------------------------------------------------------------------------

class TestErrorPages:

    def test_404_not_found_title(self, analyzer):
        """'not found' in title → is_valid=False, status='error_page'."""
        result = analyzer._check_content_validity(
            _content(title='page not found', word_count=60), _scrape()
        )
        assert result['is_valid'] is False
        assert result['status'] == 'error_page'

    def test_500_server_error_title(self, analyzer):
        """'server error' in title → error_page status."""
        result = analyzer._check_content_validity(
            _content(title='500 server error', word_count=60), _scrape()
        )
        assert result['is_valid'] is False
        assert result['status'] == 'error_page'

    def test_service_unavailable_title(self, analyzer):
        """'service unavailable' in title → error_page status."""
        result = analyzer._check_content_validity(
            _content(title='service unavailable', word_count=60), _scrape()
        )
        assert result['is_valid'] is False
        assert result['status'] == 'error_page'


# ---------------------------------------------------------------------------
# JS-render failure (large HTML, few words)
# ---------------------------------------------------------------------------

class TestJSRenderFailure:

    def test_large_html_with_very_few_words_is_invalid(self, analyzer):
        """Large HTML (>50k chars) with <30 words → is_valid=False.

        Note: Check 1 (word_count < 50) fires before Check 5 (js_render_fail),
        so the status is 'empty', but is_valid=False is the correct outcome either way.
        """
        large_html = 'x' * 60_000
        result = analyzer._check_content_validity(
            _content(word_count=20, body_text='word ' * 20),
            _scrape(html=large_html),
        )
        assert result['is_valid'] is False

    def test_fifty_words_with_normal_html_is_valid(self, analyzer):
        """50 words with normal-size HTML is valid — no false JS-render flag."""
        result = analyzer._check_content_validity(
            _content(word_count=50), _scrape()
        )
        assert result['is_valid'] is True

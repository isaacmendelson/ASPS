"""
Content validity checks.

Determines whether a scraped page contains the actual target site content
or a block page, agreement page, error page, or JS-render failure.

Extracted from ScamAnalyzer._check_content_validity() to give content
validation a dedicated, independently testable home.

Public interface:
- check_content_validity(content, scrape_result) -> dict
"""

import logging
from typing import Dict

logger = logging.getLogger(__name__)

# Title substrings that indicate the scraper was blocked by an anti-bot wall.
_BLOCK_INDICATORS = [
    'access denied', '403 forbidden', '401 unauthorized',
    'blocked', 'captcha', 'robot check', 'are you human',
    'just a moment', 'checking your browser', 'attention required',
    'please wait', 'verify you are human', 'security check',
    'pardon our interruption', 'one more step',
]

# Title substrings that indicate an agreement/consent gate page.
_AGREEMENT_INDICATORS = [
    'agreement', 'terms of use', 'terms of service',
    'consent', 'cookie policy', 'usage agreement',
    'accept terms', 'privacy notice',
]

# Title substrings that indicate a server-side or application error page.
_ERROR_INDICATORS = [
    '404', 'not found', '500', 'server error',
    'something went wrong', 'page not available',
    'service unavailable', '502 bad gateway', '503',
]


def check_content_validity(content: Dict, scrape_result: Dict) -> Dict:
    """Determine whether the extracted content represents the actual target page.

    Detects block pages, agreement gates, error pages, JS render failures and
    pages with too little content to be meaningful.

    Args:
        content: Output of ContentExtractor.extract().
        scrape_result: Output of PlaywrightScraper.fetch().

    Returns:
        Dict with keys:
            is_valid (bool): True if the content is the real page.
            status (str): Short status code ('ok', 'scrape_failed', 'extraction_failed',
                          'empty', 'blocked', 'agreement', 'error_page', 'js_render_fail').
            detail (str): Human-readable explanation.
    """
    # Scraping failed entirely
    if not scrape_result.get('success'):
        return {
            'is_valid': False,
            'status': 'scrape_failed',
            'detail': 'Scraping failed - could not load page',
        }

    # Content extraction failed
    if not content.get('success'):
        return {
            'is_valid': False,
            'status': 'extraction_failed',
            'detail': 'Content extraction failed',
        }

    title = (content.get('title', '') or '').lower().strip()
    word_count = content.get('word_count', 0)

    # Check 1: Too little content
    if word_count < 50:
        return {
            'is_valid': False,
            'status': 'empty',
            'detail': f'Page has only {word_count} words - content did not load properly',
        }

    # Check 2: Block page
    for indicator in _BLOCK_INDICATORS:
        if indicator in title:
            return {
                'is_valid': False,
                'status': 'blocked',
                'detail': f"Page returned '{title}' - content is not the actual website",
            }

    # Check 3: Agreement/consent gate
    for indicator in _AGREEMENT_INDICATORS:
        if indicator in title:
            return {
                'is_valid': False,
                'status': 'agreement',
                'detail': 'Page shows agreement/consent page instead of actual content',
            }

    # Check 4: Error page
    for indicator in _ERROR_INDICATORS:
        if indicator in title:
            return {
                'is_valid': False,
                'status': 'error_page',
                'detail': f"Page returned error: '{title}'",
            }

    # Check 5: JS render failure — large HTML but very little extracted text.
    # High thresholds intentionally avoid false positives on minimal pages
    # (e.g. google.com homepage has few words but short HTML too).
    html_length = len(scrape_result.get('html', ''))
    if html_length > 50000 and word_count < 30:
        return {
            'is_valid': False,
            'status': 'js_render_fail',
            'detail': (
                f'Large HTML ({html_length} chars) but only {word_count} words extracted'
                ' - JavaScript may not have rendered'
            ),
        }

    return {'is_valid': True, 'status': 'ok', 'detail': ''}

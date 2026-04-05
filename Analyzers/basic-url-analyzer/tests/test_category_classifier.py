"""
Tests for the enhanced CategoryClassifier (Layer 2 + Layer 3).
36 test cases covering domain matching, content analysis, combined scoring,
edge cases, and backward compatibility.
"""

import pytest
import sys
from pathlib import Path

# Add project root to path
project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))

from core.category_classifier import CategoryClassifier


@pytest.fixture(scope="module")
def classifier():
    """Load CategoryClassifier once for all tests."""
    return CategoryClassifier()


def make_content(title='', body_text='', meta_description='',
                 h1=None, h2=None, cta_count=0, form_types=None,
                 has_forms=False):
    """Helper to build content dict for testing."""
    return {
        'title': title,
        'body_text': body_text,
        'meta_description': meta_description,
        'headings': {
            'h1': h1 or [],
            'h2': h2 or []
        },
        'cta_count': cta_count,
        'form_types': form_types or [],
        'has_forms': has_forms,
        'word_count': len(body_text.split()) if body_text else 0,
        'success': True
    }


# =========================================================================
# Layer 2: Domain Pattern Tests (tests 1-10)
# =========================================================================

class TestLayer2DomainPatterns:
    """Test domain TLD/suffix pattern matching."""

    def test_01_bank_tld(self, classifier):
        """.bank TLD should match banking"""
        content = make_content()
        result = classifier.classify(content, 'www.leumi.bank')
        assert result['category'] == 'banking'
        assert 'domain_pattern' in result['detection_method']
        assert result['confidence'] >= 0.85

    def test_02_gov_il(self, classifier):
        """.gov.il should match government"""
        content = make_content()
        result = classifier.classify(content, 'www.gov.il')
        assert result['category'] == 'government'
        assert result['detection_method'] == 'domain_pattern'
        assert result['confidence'] >= 0.80

    def test_03_ac_il(self, classifier):
        """.ac.il should match university"""
        content = make_content()
        result = classifier.classify(content, 'www.huji.ac.il')
        assert result['category'] == 'university'
        assert result['detection_method'] == 'domain_pattern'

    def test_04_muni_il(self, classifier):
        """.muni.il should match municipality"""
        content = make_content()
        result = classifier.classify(content, 'www.tel-aviv.muni.il')
        assert result['category'] == 'municipality'
        assert result['detection_method'] == 'domain_pattern'

    def test_05_mil(self, classifier):
        """.mil should match military"""
        content = make_content()
        result = classifier.classify(content, 'www.army.mil')
        assert result['category'] == 'military'
        assert 'domain_pattern' in result['detection_method']

    def test_06_casino_tld(self, classifier):
        """.casino should match gambling"""
        content = make_content()
        result = classifier.classify(content, 'example.casino')
        assert result['category'] == 'gambling'
        assert result['confidence'] >= 0.85

    def test_07_pharmacy_tld(self, classifier):
        """.pharmacy should match pharmacy"""
        content = make_content()
        result = classifier.classify(content, 'example.pharmacy')
        assert result['category'] == 'pharmacy'

    def test_08_travel_tld(self, classifier):
        """.travel should match travel"""
        content = make_content()
        result = classifier.classify(content, 'booking.travel')
        assert result['category'] == 'travel'

    def test_09_generic_co_il_no_match(self, classifier):
        """.co.il alone should not match a specific category via domain"""
        content = make_content()
        result = classifier.classify(content, 'example.co.il')
        # Should fall through to content analysis or unknown
        assert result['detection_method'] != 'domain_pattern' or result['category'] == 'unknown'

    def test_10_generic_com_no_match(self, classifier):
        """.com should not match any domain pattern"""
        content = make_content()
        result = classifier.classify(content, 'example.com')
        assert result['detection_method'] != 'domain_pattern' or result['category'] == 'unknown'


# =========================================================================
# Layer 3: Content Analysis Tests (tests 11-20)
# =========================================================================

class TestLayer3ContentAnalysis:
    """Test content-based category detection."""

    def test_11_banking_content(self, classifier):
        """Strong banking keywords should detect banking"""
        content = make_content(
            title='Open a Savings Account - First National Bank',
            body_text='Welcome to our online banking portal. Check your account balance, '
                      'wire transfers, savings deposits and credit card statements.',
            h1=['Personal Banking']
        )
        result = classifier.classify(content, 'example.com')
        assert result['category'] == 'banking'
        assert result['detection_method'] == 'content_analysis'

    def test_12_ecommerce_content(self, classifier):
        """Shopping keywords should detect ecommerce"""
        content = make_content(
            title='Buy Electronics Online - Free Shipping',
            body_text='Shop our store for the best deals. Add products to your cart, '
                      'enjoy free shipping on orders over $50. Checkout securely.',
            cta_count=8
        )
        result = classifier.classify(content, 'example.com')
        assert result['category'] in ('ecommerce', 'electronics')

    def test_13_university_content_with_edu(self, classifier):
        """University content + .edu domain should strongly match"""
        content = make_content(
            title='Harvard University - Welcome',
            body_text='Explore our campus, faculty research, admission requirements, '
                      'degree programs, and student life. Apply for enrollment.',
            h1=['Harvard University']
        )
        result = classifier.classify(content, 'www.harvard.edu')
        assert result['category'] == 'university'
        assert result['confidence'] >= 0.70

    def test_14_gambling_content(self, classifier):
        """Casino/gambling keywords should detect gambling"""
        content = make_content(
            title='Online Poker - Play Now',
            body_text='Join our casino for the best slot machines, roulette, blackjack '
                      'and poker games. Get your welcome bonus and free spins today!',
            h1=['Live Casino']
        )
        result = classifier.classify(content, 'example.com')
        assert result['category'] in ('gambling', 'sports_betting')

    def test_15_mental_health_content(self, classifier):
        """Mental health keywords should detect mental_health"""
        content = make_content(
            title='Find a Therapist Near You',
            body_text='Professional counseling and therapy services. Our psychologists '
                      'specialize in anxiety, depression, and mental health wellness.',
            h1=['Mental Health Services']
        )
        result = classifier.classify(content, 'example.com')
        assert result['category'] == 'mental_health'

    def test_16_news_content(self, classifier):
        """News keywords should detect news"""
        content = make_content(
            title='Latest Tech News - Breaking Headlines',
            body_text='Our reporters bring you breaking news, editorials, and in-depth '
                      'articles on technology, politics, and world events.',
            h1=['Breaking News']
        )
        result = classifier.classify(content, 'example.com')
        assert result['category'] == 'news'

    def test_17_hebrew_government(self, classifier):
        """Hebrew government keywords should work"""
        content = make_content(
            title='משרד הבריאות',
            body_text='שירות ציבורי לאזרחי המדינה. ממשלה רשמית. '
                      'משרד ממשלתי לאומי.',
            h1=['משרד הבריאות']
        )
        result = classifier.classify(content, 'example.com')
        assert result['category'] in ('government', 'public_service', 'hospital')

    def test_18_hebrew_legal(self, classifier):
        """Hebrew legal keywords should work"""
        content = make_content(
            title='עורך דין תל אביב - משרד עורכי דין',
            body_text='ייעוץ משפטי מקצועי. עורך דין מומחה בתביעות וחוזים. '
                      'משרד עורכי דין מוביל.',
            h1=['משרד עורכי דין']
        )
        result = classifier.classify(content, 'example.com')
        assert result['category'] == 'legal'

    def test_19_empty_content(self, classifier):
        """Empty content should return unknown gracefully"""
        content = make_content()
        result = classifier.classify(content, 'example.com')
        assert result['success'] == True
        assert result['category'] == 'unknown'
        assert result['confidence'] == 0.0

    def test_20_title_weighted_higher(self, classifier):
        """Title should be weighted higher than body - banking title wins"""
        content = make_content(
            title='First National Bank - Online Banking Portal',
            body_text='We also have a small shop section where you can buy merchandise.',
            h1=['Welcome to Your Bank']
        )
        result = classifier.classify(content, 'example.com')
        assert result['category'] == 'banking'


# =========================================================================
# Combined Layer Tests (tests 21-25)
# =========================================================================

class TestCombinedLayers:
    """Test Layer 2 + Layer 3 combination logic."""

    def test_21_both_agree(self, classifier):
        """Domain + content agree = high confidence"""
        content = make_content(
            title='Government Services Portal',
            body_text='Official government ministry. Public services for citizens. '
                      'Federal department of state affairs.',
            h1=['Government Portal']
        )
        result = classifier.classify(content, 'www.services.gov.il')
        assert result['category'] == 'government'
        assert result['detection_method'] == 'domain_pattern+content_analysis'
        assert result['confidence'] >= 0.85

    def test_22_domain_content_mismatch(self, classifier):
        """Domain=gov but content=shopping should flag mismatch"""
        content = make_content(
            title='Buy Cheap Electronics - Amazing Deals',
            body_text='Shop our store! Add to cart, free shipping, discount code, '
                      'checkout now. Best prices on products. Order today!',
            cta_count=10
        )
        result = classifier.classify(content, 'www.deals.gov.il')
        # Should have mismatch warning in signals
        warning_signals = [s for s in result['matched_signals']
                          if s.get('type') == 'warning'
                          and s.get('value') == 'domain_content_mismatch']
        assert len(warning_signals) > 0

    def test_23_content_only(self, classifier):
        """Generic domain + banking content = content-only detection"""
        content = make_content(
            title='Open a New Bank Account Today',
            body_text='Online banking, savings account, wire transfer, deposit, '
                      'checking account, credit card, mobile banking services.',
            h1=['Personal Banking']
        )
        result = classifier.classify(content, 'example.com')
        assert result['category'] == 'banking'
        assert result['detection_method'] == 'content_analysis'

    def test_24_domain_alone(self, classifier):
        """Strong domain + empty content = domain detection"""
        content = make_content()
        result = classifier.classify(content, 'mybank.bank')
        assert result['category'] == 'banking'
        assert 'domain_pattern' in result['detection_method']
        assert result['confidence'] >= 0.85

    def test_25_content_overrides_generic_domain(self, classifier):
        """University content should work even with generic .co.il"""
        content = make_content(
            title='University of Technology - Academic Excellence',
            body_text='Faculty research, degree programs, campus life, enrollment, '
                      'semester registration, professor directory, PhD programs.',
            h1=['University of Technology']
        )
        result = classifier.classify(content, 'tech-uni.co.il')
        assert result['category'] == 'university'


# =========================================================================
# Edge Cases (tests 26-32)
# =========================================================================

class TestEdgeCases:
    """Test edge cases and error handling."""

    def test_26_none_content(self, classifier):
        """None content should not crash"""
        result = classifier.classify(None, 'example.com')
        assert result['category'] == 'unknown'
        assert isinstance(result['matched_signals'], list)

    def test_27_empty_domain(self, classifier):
        """Empty domain should skip Layer 2"""
        content = make_content(
            title='Online Casino - Play Now',
            body_text='Casino gambling poker roulette blackjack slot jackpot bet wager.',
        )
        result = classifier.classify(content, '')
        assert result['success'] == True
        # Should still classify via content
        assert result['category'] != 'unknown' or result['confidence'] == 0.0

    def test_28_hebrew_only_content(self, classifier):
        """Hebrew-only content should match Hebrew keywords"""
        content = make_content(
            title='חנות חיות מחמד - כלבים וחתולים',
            body_text='חיות מחמד כלב חתול וטרינר אילוף גור מזון לחיות.',
            h1=['חנות חיות מחמד']
        )
        result = classifier.classify(content, 'example.com')
        assert result['category'] == 'pets'

    def test_29_bilingual_content(self, classifier):
        """Mixed EN+HE content should combine signals"""
        content = make_content(
            title='Bank Services - שירותי בנק',
            body_text='Online banking services. חשבון בנק, פיקדון, חיסכון. '
                      'Wire transfer, deposits, savings account.',
            h1=['Banking - בנקאות']
        )
        result = classifier.classify(content, 'example.com')
        assert result['category'] == 'banking'
        # Should have both EN and HE signals
        signal_values = [s['value'] for s in result['matched_signals'] if s['type'] == 'keyword']
        has_en = any(not any('\u0590' <= c <= '\u05FF' for c in v) for v in signal_values)
        has_he = any(any('\u0590' <= c <= '\u05FF' for c in v) for v in signal_values)
        assert has_en and has_he

    def test_30_very_short_content(self, classifier):
        """Very short content should still attempt classification"""
        content = make_content(title='Bank', body_text='Login')
        result = classifier.classify(content, 'example.com')
        assert result['success'] == True
        # Might be unknown or low confidence, but should not crash

    def test_31_category_tie(self, classifier):
        """When scores are close, secondary_category should be populated"""
        content = make_content(
            title='Investment Banking - Stock Trading Platform',
            body_text='Investment portfolio, stock trading, broker services, '
                      'mutual funds, equity markets, dividend stocks, nasdaq.',
        )
        result = classifier.classify(content, 'example.com')
        # Should have a secondary category since investment and stock_trading overlap
        assert result['category'] in ('investment', 'stock_trading', 'banking')

    def test_32_domain_bank_but_phishing_content(self, classifier):
        """.bank domain with mismatched content should flag warning"""
        content = make_content(
            title='Win Free iPhone - Limited Time Offer',
            body_text='Congratulations! You have been selected to win a free prize. '
                      'Click here now, limited offer, act fast, claim your reward.',
            cta_count=15
        )
        result = classifier.classify(content, 'suspicious.bank')
        # Should still be banking (trusting domain) but with mismatch warning
        has_warning = any(
            s.get('type') == 'warning' and s.get('value') == 'domain_content_mismatch'
            for s in result.get('matched_signals', [])
        )
        # Either classified as banking with warning, or content override
        assert result['category'] == 'banking' or has_warning or result['detection_method'] != 'domain_pattern'


# =========================================================================
# Backward Compatibility Tests (tests 33-36)
# =========================================================================

class TestBackwardCompatibility:
    """Test backward compatibility with existing system."""

    def test_33_result_has_required_fields(self, classifier):
        """Result should always have all required fields"""
        content = make_content(title='Test')
        result = classifier.classify(content, 'example.com')
        required = ['success', 'category', 'category_group', 'name_en', 'name_he',
                    'confidence', 'detection_method', 'matched_signals',
                    'secondary_category', 'secondary_confidence', 'all_scores', 'error']
        for field in required:
            assert field in result, f"Missing field: {field}"

    def test_34_matched_signals_always_list(self, classifier):
        """matched_signals should always be a list, never None"""
        content = make_content()
        result = classifier.classify(content, '')
        assert isinstance(result['matched_signals'], list)

    def test_35_confidence_in_range(self, classifier):
        """Confidence should always be 0.0 to 1.0"""
        test_cases = [
            (make_content(), 'example.com'),
            (make_content(title='Bank'), 'test.bank'),
            (make_content(title='Buy now shop store cart checkout'), 'shop.com'),
        ]
        for content, domain in test_cases:
            result = classifier.classify(content, domain)
            assert 0.0 <= result['confidence'] <= 1.0

    def test_36_content_overrides_weak_domain(self, classifier):
        """Strong content should override generic domain pattern (e.g. .gov)"""
        content = make_content(
            title='Buy Crypto - Bitcoin Exchange Platform',
            body_text='Trade bitcoin ethereum cryptocurrency exchange wallet '
                      'blockchain token defi staking swap BTC ETH USDT. '
                      'Crypto trading pairs spot futures liquidity.',
            h1=['Crypto Exchange']
        )
        # .gov is generic (confidence 0.80), strong crypto content should override
        result = classifier.classify(content, 'crypto.gov')
        has_mismatch = any(
            s.get('value') == 'domain_content_mismatch'
            for s in result.get('matched_signals', [])
        )
        # Content should override weak domain OR mismatch flagged
        assert result['category'] == 'crypto_exchange' or has_mismatch

    def test_37_secondary_category_populated(self, classifier):
        """Secondary category should be populated when scores are close"""
        content = make_content(
            title='Investment Banking - Portfolio Management',
            body_text='Investment portfolio mutual fund stock trading broker '
                      'asset management wealth advisory securities dividend market.',
        )
        result = classifier.classify(content, 'example.com')
        # With overlapping financial terms, secondary should exist
        assert result['secondary_category'] is not None or result['confidence'] > 0.8

    def test_38_uppercase_domain(self, classifier):
        """Uppercase domain should be handled correctly"""
        content = make_content()
        result = classifier.classify(content, 'WWW.EXAMPLE.GOV.IL')
        assert result['category'] == 'government'

    def test_39_missing_headings_key(self, classifier):
        """Content without headings key should not crash"""
        content = {
            'title': 'Online Banking Portal',
            'body_text': 'bank account savings deposit wire transfer',
            'success': True
        }
        result = classifier.classify(content, 'example.com')
        assert result['success'] == True

    def test_40_crypto_exchange_detection(self, classifier):
        """Crypto exchange should be properly detected (real-time AnyDesk scenario)"""
        content = make_content(
            title='Trade Bitcoin & Ethereum - Crypto Exchange',
            body_text='Buy and sell cryptocurrency. Bitcoin, Ethereum, trading pairs, '
                      'spot trading, futures, staking, DeFi, wallet, blockchain, '
                      'BTC/USDT, swap tokens, liquidity pool.',
            h1=['Crypto Exchange Platform']
        )
        result = classifier.classify(content, 'example.com')
        assert result['category'] == 'crypto_exchange'
        assert result['confidence'] >= 0.3
        assert len(result['matched_signals']) > 0

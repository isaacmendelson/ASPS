"""
Tests for URLInspector - URL string phishing indicator checks.
"""

import sys
from pathlib import Path

# Ensure imports work
sys.path.insert(0, str(Path(__file__).parent.parent))

import pytest
from core.url_inspector import URLInspector


@pytest.fixture
def inspector():
    return URLInspector()


# =========================================================================
# Helper assertions
# =========================================================================

def _assert_success(result):
    assert result['success'] is True
    assert result['error'] == ''
    assert result['total_checks'] == 10


def _assert_error(result):
    assert result['success'] is False
    assert result['error'] != ''
    assert result['total_checks'] == 0


def _has_flag(result, flag_name):
    return flag_name in result['flags']


# =========================================================================
# Legitimate URLs - should have LOW risk and few/no flags
# =========================================================================

class TestLegitimateURLs:

    def test_google(self, inspector):
        result = inspector.inspect('https://www.google.com/')
        _assert_success(result)
        assert result['risk_level'] == 'LOW'
        assert result['flags_found'] == 0

    def test_bank_hapoalim(self, inspector):
        result = inspector.inspect('https://www.bankhapoalim.co.il/')
        _assert_success(result)
        assert result['risk_level'] == 'LOW'
        assert not _has_flag(result, 'using_ip')
        assert not _has_flag(result, 'short_url')

    def test_github_repo(self, inspector):
        result = inspector.inspect('https://github.com/user/repo')
        _assert_success(result)
        assert result['risk_level'] == 'LOW'
        assert result['flags_found'] == 0

    def test_wikipedia(self, inspector):
        result = inspector.inspect('https://en.wikipedia.org/wiki/Phishing')
        _assert_success(result)
        assert result['risk_level'] == 'LOW'

    def test_standard_https(self, inspector):
        result = inspector.inspect('https://example.com/path/to/page')
        _assert_success(result)
        assert result['risk_level'] == 'LOW'


# =========================================================================
# Phishing URLs - should flag specific checks
# =========================================================================

class TestPhishingURLs:

    def test_ip_address(self, inspector):
        result = inspector.inspect('http://192.168.1.1/login')
        _assert_success(result)
        assert _has_flag(result, 'using_ip')
        assert result['checks']['using_ip']['risk'] == 'high'

    def test_hyphen_in_domain(self, inspector):
        result = inspector.inspect('http://paypal-secure-login.com/verify')
        _assert_success(result)
        assert _has_flag(result, 'hyphen_in_domain')

    def test_too_many_subdomains(self, inspector):
        result = inspector.inspect('http://login.secure.bank.account.verify.evil.com/signin')
        _assert_success(result)
        assert _has_flag(result, 'subdomain_count')
        assert result['checks']['subdomain_count']['risk'] in ('medium', 'high')

    def test_url_shortener_bitly(self, inspector):
        result = inspector.inspect('http://bit.ly/3xk2f9')
        _assert_success(result)
        assert _has_flag(result, 'short_url')

    def test_url_shortener_tinyurl(self, inspector):
        result = inspector.inspect('http://tinyurl.com/abc123')
        _assert_success(result)
        assert _has_flag(result, 'short_url')

    def test_url_shortener_tco(self, inspector):
        result = inspector.inspect('https://t.co/abc123')
        _assert_success(result)
        assert _has_flag(result, 'short_url')

    def test_at_symbol(self, inspector):
        result = inspector.inspect('http://www.bank.com@evil.com/login')
        _assert_success(result)
        assert _has_flag(result, 'symbol_at')
        assert result['checks']['symbol_at']['risk'] == 'high'

    def test_double_slash_redirect(self, inspector):
        result = inspector.inspect('https://www.google.com//evil.com')
        _assert_success(result)
        assert _has_flag(result, 'double_slash_redirect')

    def test_https_in_domain_name(self, inspector):
        result = inspector.inspect('http://https-secure-bank.com/')
        _assert_success(result)
        assert _has_flag(result, 'https_in_domain')
        assert result['checks']['https_in_domain']['risk'] == 'high'

    def test_non_standard_port(self, inspector):
        result = inspector.inspect('http://bank.com:8443/login')
        _assert_success(result)
        assert _has_flag(result, 'non_standard_port')
        assert result['checks']['non_standard_port']['risk'] == 'medium'

    def test_very_long_url(self, inspector):
        long_path = 'a' * 200
        url = f'https://example.com/{long_path}'
        result = inspector.inspect(url)
        _assert_success(result)
        assert _has_flag(result, 'long_url')

    def test_multiple_flags(self, inspector):
        """URL with many phishing indicators should get HIGH risk"""
        result = inspector.inspect('http://192.168.1.1:8080/login@evil.com')
        _assert_success(result)
        assert result['flags_found'] >= 2
        assert result['risk_level'] in ('MEDIUM', 'HIGH')


# =========================================================================
# Edge cases
# =========================================================================

class TestEdgeCases:

    def test_empty_string(self, inspector):
        result = inspector.inspect('')
        _assert_error(result)

    def test_none_input(self, inspector):
        result = inspector.inspect(None)
        _assert_error(result)

    def test_not_a_url(self, inspector):
        result = inspector.inspect('this is not a url at all')
        assert isinstance(result, dict)

    def test_whitespace_only(self, inspector):
        result = inspector.inspect('   ')
        _assert_error(result)

    def test_domain_without_protocol(self, inspector):
        result = inspector.inspect('example.com')
        _assert_success(result)
        assert result['domain'] == 'example.com'

    def test_localhost(self, inspector):
        result = inspector.inspect('http://localhost/admin')
        _assert_success(result)
        assert result['domain'] == 'localhost'

    def test_localhost_with_port(self, inspector):
        result = inspector.inspect('http://localhost:3000/')
        _assert_success(result)
        assert _has_flag(result, 'non_standard_port')

    def test_ipv6_address(self, inspector):
        result = inspector.inspect('http://[::1]/login')
        _assert_success(result)

    def test_integer_input(self, inspector):
        result = inspector.inspect(12345)
        _assert_error(result)


# =========================================================================
# Unicode / homograph tests
# =========================================================================

class TestSuspiciousCharacters:

    def test_encoded_control_chars(self, inspector):
        result = inspector.inspect('https://example.com/page%00hidden')
        _assert_success(result)
        assert _has_flag(result, 'suspicious_characters')

    def test_encoded_null_byte(self, inspector):
        result = inspector.inspect('https://example.com/%01%02path')
        _assert_success(result)
        assert _has_flag(result, 'suspicious_characters')

    def test_cyrillic_homograph(self, inspector):
        # Using Cyrillic a (U+0430) instead of Latin a
        url = 'https://\u0430pple.com/'
        result = inspector.inspect(url)
        _assert_success(result)
        assert _has_flag(result, 'suspicious_characters')

    def test_normal_url_no_suspicious_chars(self, inspector):
        result = inspector.inspect('https://www.example.com/normal-path?q=test')
        _assert_success(result)
        assert not _has_flag(result, 'suspicious_characters')


# =========================================================================
# Risk score calculation
# =========================================================================

class TestRiskScoring:

    def test_clean_url_zero_score(self, inspector):
        result = inspector.inspect('https://www.google.com/')
        assert result['url_risk_score'] == 0

    def test_score_capped_at_100(self, inspector):
        """Even with many flags, score should not exceed 100"""
        url = 'http://192.168.1.1:8080/login@evil.com//redirect?x=' + 'a' * 200
        result = inspector.inspect(url)
        assert result['url_risk_score'] <= 100

    def test_high_risk_threshold(self, inspector):
        """Score >= 50 should be HIGH"""
        result = inspector.inspect('http://192.168.1.1:8080/@evil.com//redirect')
        if result['url_risk_score'] >= 50:
            assert result['risk_level'] == 'HIGH'

    def test_medium_risk_threshold(self, inspector):
        """Score 25-49 should be MEDIUM"""
        result = inspector.inspect('http://paypal-secure-login.com:8080/verify')
        assert result['url_risk_score'] >= 15
        if 25 <= result['url_risk_score'] < 50:
            assert result['risk_level'] == 'MEDIUM'

    def test_low_risk_threshold(self, inspector):
        """Score < 25 should be LOW"""
        result = inspector.inspect('https://example.com/')
        assert result['url_risk_score'] < 25
        assert result['risk_level'] == 'LOW'


# =========================================================================
# Output structure
# =========================================================================

class TestOutputStructure:

    def test_result_has_all_keys(self, inspector):
        result = inspector.inspect('https://example.com/')
        expected_keys = {
            'success', 'url', 'domain', 'url_risk_score', 'risk_level',
            'total_checks', 'flags_found', 'checks', 'flags', 'error',
        }
        assert expected_keys.issubset(result.keys())

    def test_check_result_structure(self, inspector):
        result = inspector.inspect('https://example.com/')
        for check_name, check_result in result['checks'].items():
            assert 'passed' in check_result, f'{check_name} missing passed'
            assert 'risk' in check_result, f'{check_name} missing risk'
            assert 'detail' in check_result, f'{check_name} missing detail'
            assert check_result['risk'] in ('none', 'low', 'medium', 'high')

    def test_flags_match_passed_checks(self, inspector):
        result = inspector.inspect('http://bit.ly/test')
        for flag in result['flags']:
            assert result['checks'][flag]['passed'] is True

    def test_error_result_structure(self, inspector):
        result = inspector.inspect(None)
        assert result['success'] is False
        assert result['risk_level'] == 'UNKNOWN'
        assert result['checks'] == {}
        assert result['flags'] == []

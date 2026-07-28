"""
ASPS-612: Output schema contract tests for ScamAnalyzer.

Verifies the Python→C# wire contract:
- Top-level `success` boolean present in all responses.
- Error responses have a structured `error` object (code + message), not a bare string.
- Success responses have `success: true` and no `error` key (or null error).
- v1_stdio adapter routes correctly based on the new schema.

Run:
    cd Analyzers/basic-url-analyzer
    python -m pytest tests/test_output_schema.py -v
"""

from __future__ import annotations

import io
import json
import sys
from pathlib import Path
from typing import Callable, Dict
from unittest.mock import MagicMock, patch

import pytest

# ---------------------------------------------------------------------------
# Path setup
# ---------------------------------------------------------------------------
PROJECT_ROOT = Path(__file__).parent.parent
sys.path.insert(0, str(PROJECT_ROOT))

from core.analyzer import ScamAnalyzer


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _make_minimal_success_result(url: str = "https://example.com/") -> Dict:
    """
    Return the minimum dict that a real successful analyze_url() produces.
    Used to stub the analyzer in contract tests without hitting the network.
    """
    return {
        "success": True,
        "url": url,
        "domain": "example.com",
        "analyzed_at": "2026-01-01T00:00:00",
        "analysis_time_ms": 42,
        "from_cache": False,
        "risk_assessment": {
            "risk_score": 5,
            "risk_level": "LOW",
            "is_scam": False,
            "confidence": 0.9,
        },
        "scam_type": "",
        "purpose": {"category": "unknown", "confidence": 0.0, "description": ""},
        "whois": {
            "success": True,
            "domain_age_days": 3000,
            "created_date": None,
            "registrar": "Test",
            "country": "US",
            "privacy_protected": False,
            "risk_score": 0.1,
        },
        "content_analysis": {
            "success": True,
            "title": "Example",
            "detected_patterns": [],
            "cta_count": 0,
            "form_types": [],
            "word_count": 200,
            "detected_language": "en",
            "is_english": True,
        },
        "ml_analysis": {
            "enabled": True,
            "success": True,
            "score": 0.05,
            "confidence": 0.8,
            "note": "",
        },
        "reputation": {
            "success": True,
            "is_well_known": False,
            "reputable_mentions": 0,
            "mention_sources": [],
            "reputation_score": 0.5,
            "score_adjustment": 0,
        },
        "content_status": {"is_valid": True, "status": "ok", "detail": ""},
        "url_inspection": {"flags": []},
        "website_category": {
            "category": "unknown",
            "category_group": "unknown",
            "name_en": "Unknown",
            "confidence": 0.0,
            "detection_method": "none",
            "matched_signals": [],
        },
        "red_flags": [],
        "recommendation": "LOW RISK",
        "scraping_status": {
            "success": True,
            "status_code": 200,
            "final_url": url,
            "error": "",
        },
        "warnings": [],
        "missing_data": [],
    }


def _make_analyzer_stub(return_value: Dict) -> ScamAnalyzer:
    """Return a ScamAnalyzer whose analyze_url() returns the given dict."""
    stub = MagicMock(spec=ScamAnalyzer)
    stub.analyze_url.return_value = return_value
    return stub


# ===========================================================================
# 1. _error_response() schema
# ===========================================================================

class TestErrorResponseSchema:
    """Unit tests for ScamAnalyzer._error_response()."""

    def setup_method(self):
        # Instantiate with minimal patching so we don't need network/ML.
        with patch.object(ScamAnalyzer, "__init__", lambda self, **kw: None):
            self.analyzer = ScamAnalyzer.__new__(ScamAnalyzer)
            # Provide the logger that _error_response uses
            import logging
            self.analyzer.logger = logging.getLogger("test")

    def test_error_response_has_success_false(self):
        result = self.analyzer._error_response("https://example.com/", "something went wrong")
        assert "success" in result, "error response must include top-level 'success' field"
        assert result["success"] is False, "error response must have success=false"

    def test_error_response_error_is_object_not_string(self):
        result = self.analyzer._error_response("https://example.com/", "something went wrong")
        assert "error" in result, "error response must include 'error' field"
        assert isinstance(result["error"], dict), (
            f"error must be a dict (structured object), got {type(result['error']).__name__}"
        )

    def test_error_response_error_object_has_code(self):
        result = self.analyzer._error_response("https://example.com/", "bad url")
        assert "code" in result["error"], "error object must have 'code' field"
        assert isinstance(result["error"]["code"], str), "error.code must be a string"
        assert result["error"]["code"], "error.code must be non-empty"

    def test_error_response_error_object_has_message(self):
        result = self.analyzer._error_response("https://example.com/", "bad url")
        assert "message" in result["error"], "error object must have 'message' field"
        assert "bad url" in result["error"]["message"], (
            "error.message must contain the original error text"
        )

    def test_error_response_risk_assessment_score_is_zero(self):
        result = self.analyzer._error_response("https://example.com/", "any error")
        assert result.get("risk_assessment", {}).get("risk_score") == 0

    def test_error_response_preserves_url(self):
        url = "https://test-error.example.com/"
        result = self.analyzer._error_response(url, "boom")
        assert result.get("url") == url

    def test_error_response_for_invalid_url(self):
        result = self.analyzer._error_response("not-a-url", "Invalid URL: bad scheme")
        assert result["success"] is False
        assert result["error"]["code"]
        assert "Invalid URL" in result["error"]["message"]

    def test_error_response_for_timeout(self):
        result = self.analyzer._error_response("https://slow.example.com/", "Timeout after 30s")
        assert result["success"] is False
        assert result["error"]["code"]
        assert "Timeout" in result["error"]["message"]

    def test_error_response_for_scraper_failure(self):
        result = self.analyzer._error_response(
            "https://example.com/", "Scraper failed: connection refused"
        )
        assert result["success"] is False
        assert result["error"]["code"]
        assert "Scraper" in result["error"]["message"]


# ===========================================================================
# 2. analyze_url() success schema
# ===========================================================================

class TestSuccessResponseSchema:
    """
    Tests that analyze_url() success responses include top-level 'success: true'.
    We don't hit the network; we patch the internals that hit DNS/HTTP.
    """

    def _make_analyzer_with_mocked_components(self) -> ScamAnalyzer:
        """Build a ScamAnalyzer with all network/IO components mocked."""
        with patch.multiple(
            "core.analyzer",
            WhoisChecker=MagicMock,
            ReputationChecker=MagicMock,
            CategoryClassifier=MagicMock,
            URLInspector=MagicMock,
            PlaywrightScraper=MagicMock,
            ContentExtractor=MagicMock,
            RulesEngine=MagicMock,
            PurposeClassifier=MagicMock,
            MLClassifier=MagicMock,
            CacheManager=MagicMock,
        ):
            analyzer = ScamAnalyzer(use_cache=False, use_ml=False, no_explain=True)

        # Configure mocked components to return minimal valid data
        analyzer.validator = MagicMock()
        analyzer.validator.validate.return_value = (True, "https://example.com/", None)
        analyzer.validator.extract_domain.return_value = "example.com"

        analyzer.whois_checker = MagicMock()
        analyzer.whois_checker.check.return_value = {
            "success": True, "age_days": 2000, "error": None,
            "created_date": None, "registrar": "Test", "country": "US",
            "privacy_protected": False, "risk_score": 0.1,
        }

        analyzer.reputation_checker = MagicMock()
        analyzer.reputation_checker.check.return_value = {
            "success": True, "is_well_known": False, "reputable_mentions": 0,
            "mention_sources": [], "reputation_score": 0.5, "score_adjustment": 0,
        }

        analyzer.url_inspector = MagicMock()
        analyzer.url_inspector.inspect.return_value = {"flags": []}

        analyzer.scraper = MagicMock()
        analyzer.scraper.fetch.return_value = {
            "success": True, "html": "<html><body>Hello world " + "word " * 100 + "</body></html>",
            "status_code": 200, "final_url": "https://example.com/", "error": "",
        }

        analyzer.content_extractor = MagicMock()
        analyzer.content_extractor.extract.return_value = {
            "success": True, "title": "Example", "body_text": "safe content " * 100,
            "word_count": 200, "cta_count": 0, "form_types": [], "error": None,
        }

        analyzer.rules_engine = MagicMock()
        analyzer.rules_engine.analyze.return_value = {
            "risk_score": 5, "risk_level": "LOW", "is_scam": False,
            "detected_patterns": [],
        }

        analyzer.purpose_classifier = MagicMock()
        analyzer.purpose_classifier.classify.return_value = {
            "category": "unknown", "confidence": 0.5, "description": "General site",
        }

        analyzer.category_classifier = MagicMock()
        analyzer.category_classifier.classify.return_value = {
            "success": True, "category": "unknown", "category_group": "unknown",
            "name_en": "Unknown", "name_he": "לא ידוע", "confidence": 0.5,
            "detection_method": "pattern", "matched_signals": [],
            "secondary_category": None, "secondary_confidence": 0.0,
            "all_scores": {}, "error": None,
        }

        analyzer.use_ml = False
        analyzer.ml_classifier = None
        analyzer.use_cache = False

        return analyzer

    def test_success_response_has_success_true(self):
        analyzer = self._make_analyzer_with_mocked_components()
        result = analyzer.analyze_url("https://example.com/")
        assert "success" in result, "success response must include top-level 'success' field"
        assert result["success"] is True, f"success response must have success=true, got {result.get('success')}"

    def test_success_response_has_no_error_key_or_null(self):
        analyzer = self._make_analyzer_with_mocked_components()
        result = analyzer.analyze_url("https://example.com/")
        # Either 'error' is absent OR it is None/null — never a non-null error on success
        if "error" in result:
            assert result["error"] is None, (
                f"success response must have null error, got: {result['error']!r}"
            )

    def test_success_response_has_risk_assessment(self):
        analyzer = self._make_analyzer_with_mocked_components()
        result = analyzer.analyze_url("https://example.com/")
        assert "risk_assessment" in result
        ra = result["risk_assessment"]
        assert "risk_score" in ra
        assert "risk_level" in ra
        assert "is_scam" in ra
        assert "confidence" in ra


# ===========================================================================
# 3. analyze_url() invalid URL → error schema
# ===========================================================================

class TestInvalidUrlProducesErrorSchema:
    """
    analyze_url() with an invalid URL must return success=false and
    a structured error object, not a bare string.
    """

    def setup_method(self):
        # Use a real ScamAnalyzer but with a real URLValidator — we want to
        # test the actual validation path without network calls.
        with patch.multiple(
            "core.analyzer",
            WhoisChecker=MagicMock,
            ReputationChecker=MagicMock,
            CategoryClassifier=MagicMock,
            URLInspector=MagicMock,
            PlaywrightScraper=MagicMock,
            ContentExtractor=MagicMock,
            RulesEngine=MagicMock,
            PurposeClassifier=MagicMock,
            MLClassifier=MagicMock,
            CacheManager=MagicMock,
        ):
            self.analyzer = ScamAnalyzer(use_cache=False, use_ml=False, no_explain=True)

    def test_invalid_url_scheme_returns_success_false(self):
        result = self.analyzer.analyze_url("not-a-url")
        assert result.get("success") is False

    def test_invalid_url_error_is_dict(self):
        result = self.analyzer.analyze_url("not-a-url")
        assert isinstance(result.get("error"), dict), (
            f"Expected error to be dict, got: {type(result.get('error')).__name__}"
        )

    def test_invalid_url_error_has_code_and_message(self):
        result = self.analyzer.analyze_url("not-a-url")
        err = result.get("error", {})
        assert err.get("code"), "error.code must be a non-empty string"
        assert err.get("message"), "error.message must be a non-empty string"

    def test_browser_internal_url_returns_success_false(self):
        result = self.analyzer.analyze_url("chrome://settings/")
        assert result.get("success") is False
        assert isinstance(result.get("error"), dict)

    def test_empty_string_url_returns_success_false(self):
        result = self.analyzer.analyze_url("")
        assert result.get("success") is False
        assert isinstance(result.get("error"), dict)


# ===========================================================================
# 4. v1_stdio adapter — error routing
# ===========================================================================

class TestV1StdioErrorRouting:
    """
    v1_stdio.process_stream() must emit url_scan.error when the analyzer
    returns success=false, and url_scan.result when it returns success=true.
    """

    def _run_v1(self, analyze_fn: Callable, url: str = "https://example.com/") -> dict:
        """Helper: build a valid v1 envelope, run process_stream, return parsed response."""
        from generated.messaging.v1.message_envelope import create_envelope
        from v1_stdio import process_stream

        request = create_envelope(
            "url_scan.request",
            "backend",
            {"deviceId": "device-001", "tabId": "42", "url": url},
            {},
        )
        stdin = io.StringIO(json.dumps(request))
        stdout = io.StringIO()
        stderr = io.StringIO()
        process_stream(stdin, stdout, stderr, analyze_fn)
        return json.loads(stdout.getvalue())

    def test_success_result_emits_url_scan_result(self):
        def ok_analyze(url):
            return _make_minimal_success_result(url)

        response = self._run_v1(ok_analyze)
        assert response["messageType"] == "url_scan.result"
        assert response["outcome"]["status"] == "success"

    def test_error_result_with_success_false_emits_url_scan_error(self):
        """Analyzer returns success=false → adapter must emit url_scan.error."""
        def failing_analyze(url):
            return {
                "success": False,
                "url": url,
                "error": {"code": "analyzer.invalid_url", "message": "not a valid URL"},
                "risk_assessment": {"risk_score": 0, "risk_level": "UNKNOWN", "is_scam": False, "confidence": 0.0},
            }

        response = self._run_v1(failing_analyze)
        assert response["messageType"] == "url_scan.error", (
            f"Expected url_scan.error, got {response['messageType']}"
        )
        assert response["outcome"]["status"] == "error"

    def test_error_result_with_bare_string_error_emits_url_scan_error(self):
        """
        Legacy: analyzer returns error as bare string (old schema).
        The adapter should still route to url_scan.error even with the old format.
        """
        def old_schema_analyze(url):
            return {
                "url": url,
                "error": "something went wrong",
                "risk_assessment": {"risk_score": 0, "risk_level": "UNKNOWN", "is_scam": False, "confidence": 0.0},
            }

        response = self._run_v1(old_schema_analyze)
        assert response["messageType"] == "url_scan.error", (
            f"Legacy bare-string error should still route to url_scan.error"
        )

    def test_error_outcome_has_code_and_message(self):
        def failing_analyze(url):
            return {
                "success": False,
                "url": url,
                "error": {"code": "analyzer.scraper_failed", "message": "scraper timed out"},
                "risk_assessment": {"risk_score": 0, "risk_level": "UNKNOWN", "is_scam": False, "confidence": 0.0},
            }

        response = self._run_v1(failing_analyze)
        err = response["outcome"]["error"]
        assert err["code"], "url_scan.error outcome must have non-empty code"
        assert err["message"], "url_scan.error outcome must have non-empty message"

    def test_success_outcome_has_risk_score(self):
        def ok_analyze(url):
            return _make_minimal_success_result(url)

        response = self._run_v1(ok_analyze)
        result = response["outcome"]["result"]
        assert "risk_assessment" in result or "score" in result, (
            "url_scan.result outcome must include risk data"
        )


# ===========================================================================
# 5. JSON serialisability of all output shapes
# ===========================================================================

class TestJsonSerialisability:
    """All output shapes must be JSON-serialisable without TypeError."""

    def setup_method(self):
        with patch.object(ScamAnalyzer, "__init__", lambda self, **kw: None):
            self.analyzer = ScamAnalyzer.__new__(ScamAnalyzer)
            import logging
            self.analyzer.logger = logging.getLogger("test")

    def test_error_response_is_json_serialisable(self):
        result = self.analyzer._error_response("https://example.com/", "some error")
        serialized = json.dumps(result)  # must not raise
        parsed = json.loads(serialized)
        assert parsed["success"] is False

    def test_error_response_fields_are_primitives(self):
        result = self.analyzer._error_response("https://example.com/", "some error")
        # Ensure error.code and error.message are plain strings (not custom objects)
        assert isinstance(result["error"]["code"], str)
        assert isinstance(result["error"]["message"], str)

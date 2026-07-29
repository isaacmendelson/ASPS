"""
Unit tests for OllamaClient
Tests work whether Ollama is running or not
"""

import pytest
import json
import tempfile
from pathlib import Path

import sys
sys.path.insert(0, str(Path(__file__).parent.parent))

import httpx

from core.llm_explainer import (
    OllamaClient, OLLAMA_SDK_AVAILABLE,
    build_explanation_prompt, format_evidence_section,
    EXPLANATION_PROMPT_TEMPLATE, HIGH_RISK_FRAMING, LOW_RISK_FRAMING, MEDIUM_RISK_FRAMING
)
from unittest.mock import patch, MagicMock


class TestOllamaClientInstantiation:
    """Test OllamaClient can be instantiated correctly."""

    def test_default_instantiation(self):
        """Client can be created with default config path."""
        client = OllamaClient()
        assert client is not None
        assert isinstance(client.config, dict)

    def test_custom_config_path(self):
        """Client can be created with custom config path."""
        # Create temp config
        with tempfile.NamedTemporaryFile(mode='w', suffix='.json', delete=False) as f:
            json.dump({
                'ollama': {
                    'enabled': True,
                    'host': 'http://custom:11434',
                    'timeout_seconds': 60,
                    'model_preferences': ['mistral', 'phi3']
                }
            }, f)
            temp_path = f.name

        try:
            client = OllamaClient(config_path=temp_path)
            assert client.config.get('host') == 'http://custom:11434'
            assert client.config.get('timeout_seconds') == 60
            assert client.config.get('model_preferences') == ['mistral', 'phi3']
        finally:
            Path(temp_path).unlink()

    def test_missing_config_file(self):
        """Client handles missing config file gracefully."""
        client = OllamaClient(config_path='/nonexistent/path/settings.json')
        assert client.config == {}
        # Should still work with defaults
        assert client.is_available() is False or client.is_available() is True

    def test_invalid_config_json(self):
        """Client handles invalid JSON gracefully."""
        with tempfile.NamedTemporaryFile(mode='w', suffix='.json', delete=False) as f:
            f.write('not valid json {{{')
            temp_path = f.name

        try:
            client = OllamaClient(config_path=temp_path)
            assert client.config == {}
        finally:
            Path(temp_path).unlink()


class TestOllamaClientAvailability:
    """Test availability detection."""

    def test_is_available_returns_bool(self):
        """is_available() should return a boolean."""
        client = OllamaClient()
        result = client.is_available()
        assert isinstance(result, bool)

    def test_is_available_caching(self):
        """Availability check should be cached."""
        client = OllamaClient()
        # First call
        result1 = client.is_available()
        # Second call should use cached value
        result2 = client.is_available()
        assert result1 == result2

    def test_reset_clears_cache(self):
        """reset() should clear cached availability."""
        client = OllamaClient()
        _ = client.is_available()
        assert client._available is not None
        client.reset()
        assert client._available is None

    def test_disabled_in_config(self):
        """Client should report unavailable when disabled in config."""
        with tempfile.NamedTemporaryFile(mode='w', suffix='.json', delete=False) as f:
            json.dump({'ollama': {'enabled': False}}, f)
            temp_path = f.name

        try:
            client = OllamaClient(config_path=temp_path)
            assert client.is_available() is False
        finally:
            Path(temp_path).unlink()


class TestOllamaClientModelListing:
    """Test model listing functionality."""

    def test_list_models_returns_list(self):
        """list_models() should return a list."""
        client = OllamaClient()
        result = client.list_models()
        assert isinstance(result, list)

    def test_list_models_returns_copy(self):
        """list_models() should return a copy, not the internal list."""
        client = OllamaClient()
        client._models = ['phi3:latest', 'llama3.2:3b']
        client._available = True
        result = client.list_models()
        result.append('modified')
        assert 'modified' not in client._models


class TestOllamaClientModelSelection:
    """Test model selection based on preferences."""

    def test_select_model_returns_string_or_none(self):
        """select_model() should return string or None."""
        client = OllamaClient()
        result = client.select_model()
        assert result is None or isinstance(result, str)

    def test_select_model_uses_preferences(self):
        """select_model() should follow preference order."""
        with tempfile.NamedTemporaryFile(mode='w', suffix='.json', delete=False) as f:
            json.dump({
                'ollama': {
                    'enabled': True,
                    'model_preferences': ['mistral', 'phi3']
                }
            }, f)
            temp_path = f.name

        try:
            client = OllamaClient(config_path=temp_path)
            # Simulate available models
            client._models = ['phi3:latest', 'mistral:7b', 'llama3.2:3b']
            client._available = True

            selected = client.select_model()
            # Should select mistral (first in preferences that's available)
            assert selected == 'mistral:7b'
        finally:
            Path(temp_path).unlink()

    def test_select_model_prefix_matching(self):
        """select_model() should use prefix matching for model names."""
        client = OllamaClient()
        client._models = ['phi3:3.8b-instruct-q4', 'llama3.2:3b']
        client._available = True
        client.config = {'model_preferences': ['phi3']}

        selected = client.select_model()
        assert selected == 'phi3:3.8b-instruct-q4'

    def test_select_model_fallback_to_first(self):
        """select_model() should fallback to first model if no preference matches."""
        client = OllamaClient()
        client._models = ['gemma:2b', 'qwen:7b']
        client._available = True
        client.config = {'model_preferences': ['phi3', 'llama3.2']}

        selected = client.select_model()
        assert selected == 'gemma:2b'

    def test_select_model_caching(self):
        """Selected model should be cached."""
        client = OllamaClient()
        client._models = ['phi3:latest']
        client._available = True

        result1 = client.select_model()
        result2 = client.select_model()
        assert result1 == result2


class TestOllamaClientStatus:
    """Test status reporting."""

    def test_get_status_returns_dict(self):
        """get_status() should return a dictionary."""
        client = OllamaClient()
        status = client.get_status()
        assert isinstance(status, dict)

    def test_get_status_has_required_keys(self):
        """get_status() should include all required keys."""
        client = OllamaClient()
        status = client.get_status()

        required_keys = [
            'sdk_installed',
            'enabled',
            'available',
            'host',
            'timeout_seconds',
            'models',
            'selected_model',
            'model_preferences'
        ]

        for key in required_keys:
            assert key in status, f"Missing key: {key}"

    def test_get_status_sdk_installed_flag(self):
        """get_status() should report correct SDK installed status."""
        client = OllamaClient()
        status = client.get_status()
        assert status['sdk_installed'] == OLLAMA_SDK_AVAILABLE


class TestOllamaClientGracefulDegradation:
    """Test that client works gracefully when Ollama unavailable."""

    def test_no_crash_on_unavailable(self):
        """All methods should work without crashing when Ollama unavailable."""
        client = OllamaClient()
        # Force unavailable state
        client._client = None
        client._available = False

        # All these should work without raising exceptions
        assert client.is_available() is False
        assert client.list_models() == []
        assert client.select_model() is None
        assert isinstance(client.get_status(), dict)

    def test_empty_models_when_unavailable(self):
        """list_models() should return empty list when unavailable."""
        client = OllamaClient()
        client._available = False
        assert client.list_models() == []


@pytest.mark.skipif(not OLLAMA_SDK_AVAILABLE, reason="Ollama SDK not installed")
class TestOllamaClientWithSDK:
    """Tests that require the Ollama SDK to be installed."""

    def test_sdk_client_created(self):
        """Client should be created when SDK available and enabled."""
        client = OllamaClient()
        if client.config.get('enabled', True):
            # Client should be attempted (may still be None if connection failed)
            pass  # Just verify no exception


@pytest.mark.skipif(not OLLAMA_SDK_AVAILABLE, reason="Ollama SDK not installed")
class TestOllamaClientLiveConnection:
    """Tests that require a live Ollama connection.

    These tests are skipped if Ollama is not running.
    """

    @pytest.fixture
    def live_client(self):
        """Create a client and skip if Ollama not available."""
        client = OllamaClient()
        if not client.is_available():
            pytest.skip("Ollama not running")
        return client

    def test_list_models_with_connection(self, live_client):
        """list_models() should return actual models when connected."""
        models = live_client.list_models()
        assert len(models) > 0
        assert all(isinstance(m, str) for m in models)

    def test_select_model_with_connection(self, live_client):
        """select_model() should select an actual model when connected."""
        model = live_client.select_model()
        assert model is not None
        assert model in live_client.list_models()

    def test_status_shows_available(self, live_client):
        """get_status() should show available=True when connected."""
        status = live_client.get_status()
        assert status['available'] is True
        assert len(status['models']) > 0


class TestBuildExplanationPrompt:
    """Test prompt building functionality."""

    def test_build_explanation_prompt_high_risk(self):
        """Prompt for HIGH risk should use correct framing and classification."""
        result = {
            'url': 'https://crypto-scam.example.com',
            'risk_assessment': {
                'risk_level': 'HIGH',
                'risk_score': 15,
                'is_scam': True,
                'confidence': 0.95
            },
            'red_flags': ['Very new domain (5 days)', 'Guaranteed returns promise'],
            'whois': {
                'success': True,
                'domain_age_days': 5,
                'registrar': 'NameCheap',
                'country': 'PA'
            },
            'content_analysis': {'detected_patterns': []},
            'ml_analysis': {'success': False}
        }

        prompt = build_explanation_prompt(result)

        # Check URL present
        assert 'https://crypto-scam.example.com' in prompt
        # Check risk level
        assert 'HIGH' in prompt
        # Check classification for scam
        assert 'LIKELY SCAM' in prompt
        # Check HIGH_RISK_FRAMING elements
        assert 'cautionary tone' in prompt.lower() or 'warn the user' in prompt.lower()
        # Check evidence from red_flags
        assert 'Very new domain' in prompt
        assert 'Guaranteed returns' in prompt
        # Check risk score format
        assert '15/100' in prompt
        # Check confidence
        assert '95%' in prompt

    def test_build_explanation_prompt_low_risk(self):
        """Prompt for LOW risk should use correct framing and classification."""
        result = {
            'url': 'https://legitimate-business.com',
            'risk_assessment': {
                'risk_level': 'LOW',
                'risk_score': 85,
                'is_scam': False,
                'confidence': 0.88
            },
            'red_flags': [],
            'whois': {
                'success': True,
                'domain_age_days': 1200,
                'registrar': 'GoDaddy',
                'country': 'US'
            },
            'content_analysis': {'detected_patterns': []},
            'ml_analysis': {'success': False}
        }

        prompt = build_explanation_prompt(result)

        # Check classification for legitimate
        assert 'APPEARS LEGITIMATE' in prompt
        # Check LOW_RISK_FRAMING elements (reassuring tone)
        assert 'reassuring' in prompt.lower() or 'legitimate' in prompt.lower()
        # Check URL
        assert 'https://legitimate-business.com' in prompt
        # Check risk level
        assert 'LOW' in prompt

    def test_build_explanation_prompt_medium_risk(self):
        """Prompt for MEDIUM risk should use balanced framing."""
        result = {
            'url': 'https://unknown-site.net',
            'risk_assessment': {
                'risk_level': 'MEDIUM',
                'risk_score': 50,
                'is_scam': False,
                'confidence': 0.65
            },
            'red_flags': ['Relatively new domain'],
            'whois': {'success': False},
            'content_analysis': {'detected_patterns': []},
            'ml_analysis': {'success': False}
        }

        prompt = build_explanation_prompt(result)

        # Check classification for uncertain (medium, not scam)
        assert 'UNCERTAIN - CAUTION ADVISED' in prompt
        # Check MEDIUM_RISK_FRAMING elements
        assert 'balanced' in prompt.lower() or 'caution' in prompt.lower()

    def test_build_explanation_prompt_missing_data(self):
        """Prompt should handle minimal result without crashing."""
        result = {
            'url': 'https://minimal.test',
            'risk_assessment': {
                'risk_level': 'HIGH',
                'risk_score': 20
            }
            # Missing: is_scam, confidence, red_flags, whois, content_analysis, ml_analysis
        }

        # Should not raise exception
        prompt = build_explanation_prompt(result)

        # Should be a valid string
        assert isinstance(prompt, str)
        assert len(prompt) > 0
        # Should contain URL
        assert 'https://minimal.test' in prompt
        # Should have default confidence
        assert '50%' in prompt  # Default confidence 0.5 -> 50%


class TestFormatEvidenceSection:
    """Test evidence formatting functionality."""

    def test_format_evidence_section_empty(self):
        """Empty result should return default message."""
        result = {}
        evidence = format_evidence_section(result)
        assert evidence == "No specific evidence found."

    def test_format_evidence_section_with_red_flags(self):
        """Red flags should be formatted in output."""
        result = {
            'red_flags': [
                'Domain only 3 days old',
                'Contains cryptocurrency promises',
                'Privacy-protected WHOIS'
            ],
            'whois': {'success': False},
            'content_analysis': {'detected_patterns': []},
            'ml_analysis': {'success': False}
        }

        evidence = format_evidence_section(result)

        assert 'RED FLAGS DETECTED:' in evidence
        assert 'Domain only 3 days old' in evidence
        assert 'Contains cryptocurrency promises' in evidence
        assert 'Privacy-protected WHOIS' in evidence

    def test_format_evidence_section_with_whois(self):
        """WHOIS info should be formatted when available."""
        result = {
            'red_flags': [],
            'whois': {
                'success': True,
                'domain_age_days': 1500,
                'registrar': 'GoDaddy',
                'country': 'US',
                'privacy_protected': False
            },
            'content_analysis': {'detected_patterns': []},
            'ml_analysis': {'success': False}
        }

        evidence = format_evidence_section(result)

        assert 'DOMAIN INFORMATION:' in evidence
        assert '1500 days' in evidence
        assert 'GoDaddy' in evidence
        assert 'US' in evidence

    def test_format_evidence_section_with_ml_explain(self):
        """ML indicators should be included when provided."""
        result = {
            'red_flags': [],
            'whois': {'success': False},
            'content_analysis': {'detected_patterns': []},
            'ml_analysis': {'success': True}
        }
        ml_explain = {
            'success': True,
            'scam_indicators': [
                {'feature': 'guaranteed', 'contribution': 0.3},
                {'feature': 'profit', 'contribution': 0.25},
                {'feature': 'invest', 'contribution': 0.2}
            ]
        }

        evidence = format_evidence_section(result, ml_explain)

        assert 'SUSPICIOUS KEYWORDS DETECTED:' in evidence
        assert "'guaranteed'" in evidence
        assert "'profit'" in evidence


class TestGenerateExplanation:
    """Test explanation generation method."""

    def test_generate_explanation_unavailable(self):
        """Should return success=False when Ollama unavailable."""
        client = OllamaClient()
        # Force unavailable state
        client._available = False
        client._client = None

        mock_result = {
            'url': 'https://test.com',
            'risk_assessment': {
                'risk_level': 'HIGH',
                'risk_score': 20,
                'is_scam': True,
                'confidence': 0.9
            },
            'red_flags': ['Test flag'],
            'whois': {'success': False},
            'content_analysis': {'detected_patterns': []},
            'ml_analysis': {'success': False}
        }

        result = client.generate_explanation(mock_result)

        assert result['success'] is False
        assert result['explanation'] == ''
        assert result['model'] is None
        assert 'not available' in result['error']

    def test_generate_explanation_no_model(self):
        """Should return success=False when no model available."""
        client = OllamaClient()
        # Force available but no models
        client._available = True
        client._models = []
        client._selected_model = None

        mock_result = {
            'url': 'https://test.com',
            'risk_assessment': {
                'risk_level': 'HIGH',
                'risk_score': 20,
                'is_scam': True,
                'confidence': 0.9
            },
            'red_flags': ['Test flag'],
            'whois': {'success': False},
            'content_analysis': {'detected_patterns': []},
            'ml_analysis': {'success': False}
        }

        result = client.generate_explanation(mock_result)

        assert result['success'] is False
        assert result['explanation'] == ''
        assert result['model'] is None
        assert 'No suitable model' in result['error']

    def test_generate_explanation_return_structure(self):
        """Result should have expected keys regardless of success."""
        client = OllamaClient()
        client._available = False

        mock_result = {
            'url': 'https://test.com',
            'risk_assessment': {'risk_level': 'HIGH', 'risk_score': 20}
        }

        result = client.generate_explanation(mock_result)

        # All expected keys should be present
        assert 'success' in result
        assert 'explanation' in result
        assert 'model' in result
        assert 'error' in result

        # Types should be correct
        assert isinstance(result['success'], bool)
        assert isinstance(result['explanation'], str)

    @pytest.mark.skipif(not OLLAMA_SDK_AVAILABLE, reason="Ollama SDK not installed")
    def test_generate_explanation_api_error_handling(self):
        """API errors should be caught and return success=False."""
        client = OllamaClient()
        client._available = True
        client._models = ['phi3:latest']
        client._selected_model = 'phi3:latest'

        # Mock _client.generate to raise an exception
        mock_client = MagicMock()
        mock_client.generate.side_effect = Exception("Connection timeout")
        client._client = mock_client

        mock_result = {
            'url': 'https://test.com',
            'risk_assessment': {
                'risk_level': 'HIGH',
                'risk_score': 20,
                'is_scam': True,
                'confidence': 0.9
            },
            'red_flags': ['Test flag'],
            'whois': {'success': False},
            'content_analysis': {'detected_patterns': []},
            'ml_analysis': {'success': False}
        }

        result = client.generate_explanation(mock_result)

        assert result['success'] is False
        assert result['explanation'] == ''
        assert result['model'] == 'phi3:latest'
        assert 'Connection timeout' in result['error']


class TestRetryLogic:
    """Test retry logic for transient errors."""

    @pytest.fixture
    def mock_client(self):
        """Create OllamaClient with mocked _client."""
        with patch('core.llm_explainer.OLLAMA_SDK_AVAILABLE', True):
            client = OllamaClient()
            client._client = MagicMock()
            client._available = True
            client._models = ['phi3:latest']
            client._selected_model = 'phi3:latest'
            return client

    def test_retry_on_connection_error(self, mock_client):
        """Should retry on ConnectionError."""
        # Fail twice, succeed third time
        mock_client._client.generate.side_effect = [
            ConnectionError("Connection reset"),
            ConnectionError("Connection reset"),
            {'response': 'Success after retry'}
        ]

        result = mock_client.generate_explanation({'url': 'test.com', 'risk_assessment': {'risk_level': 'LOW', 'risk_score': 80, 'confidence': 0.9, 'is_scam': False}})

        assert result['success'] is True
        assert 'Success' in result['explanation']
        assert mock_client._client.generate.call_count == 3

    def test_retry_on_timeout(self, mock_client):
        """Should retry on httpx.TimeoutException."""
        # Fail once with timeout, succeed second time
        mock_client._client.generate.side_effect = [
            httpx.TimeoutException("Read timed out"),
            {'response': 'Success after timeout retry'}
        ]

        result = mock_client.generate_explanation({'url': 'test.com', 'risk_assessment': {'risk_level': 'LOW', 'risk_score': 80, 'confidence': 0.9, 'is_scam': False}})

        assert result['success'] is True
        assert mock_client._client.generate.call_count == 2

    @pytest.mark.skipif(not OLLAMA_SDK_AVAILABLE, reason="Ollama SDK not installed")
    def test_no_retry_on_response_error(self, mock_client):
        """Should NOT retry on ResponseError (4xx/5xx)."""
        from ollama import ResponseError

        # ResponseError should not be retried
        mock_client._client.generate.side_effect = ResponseError("Model not found")

        result = mock_client.generate_explanation({'url': 'test.com', 'risk_assessment': {'risk_level': 'LOW', 'risk_score': 80, 'confidence': 0.9, 'is_scam': False}})

        assert result['success'] is False
        assert mock_client._client.generate.call_count == 1  # No retry

    def test_max_retries_exhausted(self, mock_client):
        """Should fail after max retries exhausted."""
        # Always fail with connection error
        mock_client._client.generate.side_effect = ConnectionError("Connection refused")

        result = mock_client.generate_explanation({'url': 'test.com', 'risk_assessment': {'risk_level': 'LOW', 'risk_score': 80, 'confidence': 0.9, 'is_scam': False}})

        assert result['success'] is False
        assert 'failed after' in result['error'].lower() or 'attempts' in result['error'].lower() or 'connect' in result['error'].lower()
        # Default is 3 retries
        assert mock_client._client.generate.call_count == 3


class TestErrorHandling:
    """Test error categorization and user-friendly messages."""

    @pytest.fixture
    def mock_client(self):
        """Create OllamaClient with mocked internals."""
        with patch('core.llm_explainer.OLLAMA_SDK_AVAILABLE', True):
            client = OllamaClient()
            client._client = MagicMock()
            return client

    def test_no_models_installed_message(self, mock_client):
        """Should show specific message when no models installed."""
        # Simulate no models available
        mock_client._available = False
        mock_client._no_models_installed = True

        result = mock_client.generate_explanation({'url': 'test.com', 'risk_assessment': {}})

        assert result['success'] is False
        assert 'No LLM models installed' in result['error']
        assert 'ollama pull phi3' in result['error']
        assert result['error_type'] == 'no_models'

    def test_ollama_not_running_message(self, mock_client):
        """Should show helpful message when Ollama not running."""
        mock_client._available = False
        mock_client._no_models_installed = False

        result = mock_client.generate_explanation({'url': 'test.com', 'risk_assessment': {}})

        assert result['success'] is False
        assert 'Ollama not available' in result['error'] or 'ollama serve' in result['error']
        assert result['error_type'] == 'unavailable'

    def test_categorize_connection_error(self, mock_client):
        """_categorize_error should handle ConnectionError."""
        error_info = mock_client._categorize_error(ConnectionError("refused"))

        assert error_info['error_type'] == 'connection'
        assert 'connect' in error_info['message'].lower()
        assert error_info['retryable'] is True

    def test_categorize_timeout_error(self, mock_client):
        """_categorize_error should handle timeout."""
        error_info = mock_client._categorize_error(httpx.TimeoutException("timed out"))

        assert error_info['error_type'] == 'timeout'
        assert 'timed out' in error_info['message'].lower()
        assert error_info['retryable'] is True

    @pytest.mark.skipif(not OLLAMA_SDK_AVAILABLE, reason="Ollama SDK not installed")
    def test_categorize_model_not_found(self, mock_client):
        """_categorize_error should handle 404 model not found."""
        from ollama import ResponseError
        error = ResponseError("model 'nonexistent' not found (404)")

        error_info = mock_client._categorize_error(error)

        assert error_info['error_type'] == 'model_not_found'
        assert 'ollama pull' in error_info['message']
        assert error_info['retryable'] is False

    def test_categorize_unknown_error(self, mock_client):
        """_categorize_error should handle unknown errors gracefully."""
        error_info = mock_client._categorize_error(ValueError("something weird"))

        assert error_info['error_type'] == 'unknown'
        assert 'weird' in error_info['message']
        assert error_info['retryable'] is False

    def test_analysis_never_crashes_on_error(self, mock_client):
        """generate_explanation should NEVER raise, always return dict."""
        mock_client._available = True
        mock_client._models = ['phi3:latest']
        mock_client._selected_model = 'phi3:latest'

        # Make generate throw any error
        mock_client._client.generate.side_effect = RuntimeError("Catastrophic failure")

        # Should NOT raise
        result = mock_client.generate_explanation({
            'url': 'test.com',
            'risk_assessment': {'risk_level': 'LOW', 'risk_score': 80, 'confidence': 0.9, 'is_scam': False}
        })

        assert isinstance(result, dict)
        assert result['success'] is False
        assert 'error' in result

    def test_error_response_includes_error_type(self, mock_client):
        """All error responses should include error_type field."""
        mock_client._available = True
        mock_client._models = ['phi3:latest']
        mock_client._selected_model = 'phi3:latest'
        mock_client._client.generate.side_effect = ConnectionError("refused")

        result = mock_client.generate_explanation({
            'url': 'test.com',
            'risk_assessment': {'risk_level': 'LOW', 'risk_score': 80, 'confidence': 0.9, 'is_scam': False}
        })

        assert 'error_type' in result
        assert result['error_type'] is not None


class TestNoModelsDetection:
    """Test detection of 'no models installed' state."""

    def test_is_available_false_when_no_models(self):
        """is_available should return False when Ollama has no models."""
        # ASPS-626: replaced tmp_path fixture with tempfile.TemporaryDirectory() to avoid
        # PermissionError on Windows when pytest cannot scan the pytest-of-<user> temp dir.
        with tempfile.TemporaryDirectory() as tmp_dir:
            config_path = Path(tmp_dir) / "settings.json"
            config_path.write_text(
                json.dumps({"ollama": {"enabled": True}}),
                encoding="utf-8",
            )
            with patch('core.llm_explainer.OLLAMA_SDK_AVAILABLE', True):
                client = OllamaClient(config_path=config_path)
                client._client = MagicMock()
                # Simulate Ollama running but with no models
                client._client.list.return_value = {'models': []}

                # Reset to force re-check
                client._available = None

                result = client.is_available()

                assert result is False
                assert client._no_models_installed is True

    def test_is_available_true_when_models_exist(self):
        """is_available should return True when models exist."""
        # ASPS-626: replaced tmp_path fixture with tempfile.TemporaryDirectory() to avoid
        # PermissionError on Windows when pytest cannot scan the pytest-of-<user> temp dir.
        with tempfile.TemporaryDirectory() as tmp_dir:
            config_path = Path(tmp_dir) / "settings.json"
            config_path.write_text(
                json.dumps({"ollama": {"enabled": True}}),
                encoding="utf-8",
            )
            with patch('core.llm_explainer.OLLAMA_SDK_AVAILABLE', True):
                client = OllamaClient(config_path=config_path)
                client._client = MagicMock()
                client._client.list.return_value = {'models': [{'name': 'phi3:latest'}]}

                # Reset to force re-check
                client._available = None

                result = client.is_available()

                assert result is True
                assert client._no_models_installed is False

    def test_reset_clears_no_models_flag(self):
        """reset() should clear _no_models_installed flag."""
        with patch('core.llm_explainer.OLLAMA_SDK_AVAILABLE', True):
            client = OllamaClient()
            client._no_models_installed = True

            client.reset()

            assert client._no_models_installed is False

"""
Ollama LLM integration for explanation generation
Provides connection to local Ollama instance for LLM-powered analysis explanations
"""

import json
from pathlib import Path
from typing import Optional, Dict, List
import sys
sys.path.append(str(Path(__file__).parent.parent))

import httpx
from tenacity import (
    retry,
    stop_after_attempt,
    wait_exponential,
    retry_if_exception_type,
    RetryError
)

from utils.logger import setup_logger

# Graceful import handling - don't crash if ollama not installed
try:
    from ollama import Client, ResponseError
    OLLAMA_SDK_AVAILABLE = True
except ImportError:
    OLLAMA_SDK_AVAILABLE = False
    Client = None
    ResponseError = None


# =============================================================================
# PROMPT TEMPLATES AND FRAMING CONSTANTS
# =============================================================================

EXPLANATION_PROMPT_TEMPLATE = """You are a cybersecurity analyst who explains website risk assessments to users in plain English.

=== TASK ===
{framing}
Write a {sentence_count}-sentence explanation of the following website analysis.
Your explanation must:
1. State the risk classification and confidence level
2. Cite the specific evidence from the analysis that supports this classification
3. Provide a clear recommendation for the user

=== CRITICAL RULES ===
- ONLY mention evidence that appears in the ANALYSIS DATA below
- Do NOT invent or assume any information not explicitly provided
- Be direct and factual
- Use plain language a non-technical user can understand

=== ANALYSIS DATA ===
URL: {url}
Risk Level: {risk_level}
Risk Score: {risk_score}/100 (0=error, 1=safe, 100=dangerous)
Classification: {classification}
Confidence: {confidence}%

{evidence_section}

=== YOUR EXPLANATION ===
"""

HIGH_RISK_FRAMING = """This website has been classified as HIGH RISK.
Use a direct and cautionary tone. Warn the user not to engage with this site, provide personal information, or make any transactions."""

MEDIUM_RISK_FRAMING = """This website has been classified as MEDIUM RISK.
Use a balanced tone. Advise caution without causing unnecessary alarm. Suggest the user verify the site's legitimacy before proceeding."""

LOW_RISK_FRAMING = """This website has been classified as LOW RISK.
Use a reassuring tone. Mention any minor concerns if present, but indicate the site appears legitimate based on the analysis."""


# =============================================================================
# EVIDENCE FORMATTING FUNCTIONS
# =============================================================================

def format_ml_indicators(ml_explain: Dict, top_n: int = 3) -> str:
    """
    Format ML classifier scam indicators for prompt context.

    Args:
        ml_explain: Output from ML classifier's explain() method
        top_n: Maximum number of indicators to include (default 3)

    Returns:
        Formatted string of suspicious keywords, or empty string if none
    """
    if not ml_explain:
        return ""

    if not ml_explain.get('success', False):
        return ""

    indicators = ml_explain.get('scam_indicators', [])
    if not indicators:
        return ""

    # Get top N indicators by contribution
    top_indicators = sorted(
        indicators,
        key=lambda x: x.get('contribution', 0),
        reverse=True
    )[:top_n]

    if not top_indicators:
        return ""

    lines = ["SUSPICIOUS KEYWORDS DETECTED:"]
    for ind in top_indicators:
        feature = ind.get('feature', '')
        if feature:
            lines.append(f"  - '{feature}'")

    return "\n".join(lines) if len(lines) > 1 else ""


def format_whois_info(whois: Dict) -> str:
    """
    Format WHOIS information for prompt context.

    Args:
        whois: WHOIS section from analysis result

    Returns:
        Formatted string with domain info, or empty string if unavailable
    """
    if not whois:
        return ""

    if not whois.get('success', False):
        return ""

    lines = ["DOMAIN INFORMATION:"]

    # Domain age
    domain_age = whois.get('domain_age_days')
    if domain_age is not None:
        lines.append(f"  - Domain age: {domain_age} days")

    # Registrar
    registrar = whois.get('registrar')
    if registrar:
        lines.append(f"  - Registrar: {registrar}")

    # Country
    country = whois.get('country')
    if country:
        lines.append(f"  - Country: {country}")

    # Privacy protection
    if whois.get('privacy_protected'):
        lines.append("  - Privacy protection: Enabled (hides owner identity)")

    return "\n".join(lines) if len(lines) > 1 else ""


def format_patterns(patterns: List[Dict]) -> str:
    """
    Format detected scam patterns for prompt context.

    Args:
        patterns: List of detected_patterns from content_analysis

    Returns:
        Formatted string of pattern descriptions, or empty string if none
    """
    if not patterns:
        return ""

    # Take top 5 patterns
    top_patterns = patterns[:5]

    if not top_patterns:
        return ""

    lines = ["SCAM PATTERNS DETECTED:"]
    for pattern in top_patterns:
        description = pattern.get('description', '')
        if description:
            lines.append(f"  - {description}")

    return "\n".join(lines) if len(lines) > 1 else ""


def build_explanation_prompt(result: Dict, ml_explain: Dict = None) -> str:
    """
    Construct the full prompt for LLM explanation generation.

    Args:
        result: Full analysis result dictionary from the analyzer
        ml_explain: Optional ML classifier explain() output for keyword evidence

    Returns:
        Formatted prompt string ready for LLM generation
    """
    # Extract values with safe defaults
    risk_assessment = result.get('risk_assessment', {})
    risk_level = risk_assessment.get('risk_level', 'UNKNOWN')
    risk_score = risk_assessment.get('risk_score', 50)
    confidence_raw = risk_assessment.get('confidence', 0.5)
    confidence = int(confidence_raw * 100)
    is_scam = risk_assessment.get('is_scam', False)
    url = result.get('url', 'unknown')

    # Determine classification text
    if is_scam:
        classification = "LIKELY SCAM"
    elif risk_level == 'LOW':
        classification = "APPEARS LEGITIMATE"
    else:
        classification = "UNCERTAIN - CAUTION ADVISED"

    # Select risk-level-specific framing and sentence count
    if risk_level == 'HIGH':
        framing = HIGH_RISK_FRAMING
        sentence_count = "4-5"
    elif risk_level == 'MEDIUM':
        framing = MEDIUM_RISK_FRAMING
        sentence_count = "3-4"
    else:
        framing = LOW_RISK_FRAMING
        sentence_count = "3-4"

    # Format evidence section
    evidence = format_evidence_section(result, ml_explain)

    # Build final prompt
    prompt = EXPLANATION_PROMPT_TEMPLATE.format(
        framing=framing,
        sentence_count=sentence_count,
        url=url,
        risk_level=risk_level,
        risk_score=risk_score,
        classification=classification,
        confidence=confidence,
        evidence_section=evidence
    )

    return prompt


def format_evidence_section(result: Dict, ml_explain: Dict = None) -> str:
    """
    Combine all evidence into a formatted section for the prompt.

    Args:
        result: Full analysis result dictionary
        ml_explain: Optional ML classifier explain() output

    Returns:
        Combined evidence section string, or "No specific evidence found." if empty
    """
    sections = []

    # 1. Red flags (highest priority)
    red_flags = result.get('red_flags', [])
    if red_flags:
        flag_lines = ["RED FLAGS DETECTED:"]
        for flag in red_flags:
            flag_lines.append(f"  - {flag}")
        sections.append("\n".join(flag_lines))

    # 2. WHOIS information
    whois = result.get('whois', {})
    whois_section = format_whois_info(whois)
    if whois_section:
        sections.append(whois_section)

    # 3. ML indicators
    if ml_explain:
        ml_section = format_ml_indicators(ml_explain)
        if ml_section:
            sections.append(ml_section)

    # 4. Detected patterns from content analysis
    content_analysis = result.get('content_analysis', {})
    patterns = content_analysis.get('detected_patterns', [])
    pattern_section = format_patterns(patterns)
    if pattern_section:
        sections.append(pattern_section)

    # 5. Site purpose/category
    ml_analysis = result.get('ml_analysis', {})
    purpose = ml_analysis.get('purpose', {})
    category = purpose.get('category', 'unknown')
    description = purpose.get('description', '')
    if category != 'unknown' and description:
        sections.append(f"SITE CATEGORY: {description}")

    # Return combined sections or default message
    if sections:
        return "\n\n".join(sections)
    else:
        return "No specific evidence found."


class OllamaClient:
    """Client for Ollama LLM integration.

    Provides connection detection, model listing, and model selection
    for local Ollama instance. Handles unavailability gracefully without crashing.
    """

    DEFAULT_HOST = "http://localhost:11434"
    DEFAULT_TIMEOUT = 15  # Changed from 30 for faster failure detection
    DEFAULT_MODEL_PREFERENCES = ["phi3", "llama3.2", "mistral"]

    def __init__(self, config_path: Optional[str] = None):
        """
        Initialize Ollama client with configuration.

        Args:
            config_path: Path to settings.json (optional, uses default if not provided)
        """
        self.logger = setup_logger('ollama_client')

        # Resolve config path
        if config_path is None:
            config_path = Path(__file__).parent.parent / 'config' / 'settings.json'
        else:
            config_path = Path(config_path)

        # Load configuration
        self.config = self._load_config(config_path)

        # State
        self._client = None
        self._available: Optional[bool] = None
        self._models: List[str] = []
        self._selected_model: Optional[str] = None
        self._no_models_installed: bool = False

        # Initialize client if SDK available and enabled
        if OLLAMA_SDK_AVAILABLE and self.config.get('enabled', True):
            try:
                self._client = Client(
                    host=self.config.get('host', self.DEFAULT_HOST),
                    timeout=self.config.get('timeout_seconds', self.DEFAULT_TIMEOUT)
                )
                self.logger.info(f"Ollama client initialized for {self.config.get('host', self.DEFAULT_HOST)}")
            except Exception as e:
                self.logger.warning(f"Failed to initialize Ollama client: {e}")
                self._client = None
        elif not OLLAMA_SDK_AVAILABLE:
            self.logger.info("Ollama SDK not installed - LLM features disabled")
        else:
            self.logger.info("Ollama disabled in configuration")

    def _load_config(self, config_path: Path) -> Dict:
        """
        Load Ollama configuration from settings.json.

        Args:
            config_path: Path to settings.json

        Returns:
            Ollama configuration dict (empty if not found)
        """
        try:
            with open(config_path, 'r') as f:
                settings = json.load(f)
            config = settings.get('ollama', {})
            self.logger.debug(f"Loaded Ollama config: enabled={config.get('enabled', True)}")
            return config
        except FileNotFoundError:
            self.logger.warning(f"Config file not found: {config_path}")
            return {}
        except json.JSONDecodeError as e:
            self.logger.warning(f"Invalid JSON in config file: {e}")
            return {}

    def is_available(self) -> bool:
        """
        Check if Ollama is running and accessible.

        Performs a connection check by listing models. Result is cached
        after first call to avoid repeated network requests.

        Returns:
            True if Ollama is running and has models available, False otherwise
        """
        # Return cached result if available
        if self._available is not None:
            return self._available

        # SDK not installed or client not initialized
        if not OLLAMA_SDK_AVAILABLE or self._client is None:
            self._available = False
            return False

        # Check if disabled in config
        if not self.config.get('enabled', True):
            self._available = False
            return False

        try:
            # List models to verify connection AND model availability
            result = self._client.list()
            # Handle both dict (old API) and ListResponse object (new API)
            if hasattr(result, 'models'):
                models = result.models
                self._models = [m.model for m in models if hasattr(m, 'model') and m.model]
            else:
                models = result.get('models', [])
                self._models = [m.get('name', '') for m in models if m.get('name')]

            if len(self._models) == 0:
                # Ollama is running but NO models installed - special case
                self.logger.warning("Ollama running but no models installed")
                self._no_models_installed = True
                self._available = False
                return False

            self._no_models_installed = False
            self._available = True
            self.logger.info(f"Ollama available with {len(self._models)} models")

        except ConnectionError as e:
            self.logger.warning(f"Cannot connect to Ollama: {e}")
            self._available = False
            self._models = []

        except Exception as e:
            self.logger.warning(f"Ollama not available: {e}")
            self._available = False
            self._models = []

        return self._available

    def list_models(self) -> List[str]:
        """
        List available models from Ollama.

        Returns:
            List of model names (e.g., ["phi3:latest", "llama3.2:3b"])
            Empty list if Ollama not available
        """
        if not self.is_available():
            return []
        return self._models.copy()

    def select_model(self) -> Optional[str]:
        """
        Select the best available model based on configured preferences.

        Uses prefix matching to find models (e.g., "phi3" matches "phi3:latest",
        "phi3:3.8b-instruct", etc.).

        Returns:
            Selected model name, or None if no suitable model found
        """
        # Return cached selection if available
        if self._selected_model is not None:
            return self._selected_model

        if not self.is_available():
            return None

        # Get preferences from config or use defaults
        preferences = self.config.get('model_preferences', self.DEFAULT_MODEL_PREFERENCES)

        # Try each preference in order
        for pref in preferences:
            pref_lower = pref.lower()
            for model in self._models:
                # Prefix match: "phi3" matches "phi3:latest", "phi3:3.8b", etc.
                if model.lower().startswith(pref_lower):
                    self._selected_model = model
                    self.logger.info(f"Selected model: {model} (matched preference: {pref})")
                    return model

        # Fallback to first available model
        if self._models:
            self._selected_model = self._models[0]
            self.logger.info(f"Selected fallback model: {self._selected_model}")
            return self._selected_model

        return None

    def get_status(self) -> Dict:
        """
        Get Ollama status information for diagnostics.

        Returns:
            Dictionary with status information:
            - sdk_installed: bool - Whether ollama package is installed
            - enabled: bool - Whether Ollama is enabled in config
            - available: bool - Whether Ollama is running and accessible
            - host: str - Configured Ollama host
            - models: list - Available model names
            - selected_model: str|None - Currently selected model
        """
        return {
            'sdk_installed': OLLAMA_SDK_AVAILABLE,
            'enabled': self.config.get('enabled', True),
            'available': self.is_available(),
            'host': self.config.get('host', self.DEFAULT_HOST),
            'timeout_seconds': self.config.get('timeout_seconds', self.DEFAULT_TIMEOUT),
            'models': self.list_models(),
            'selected_model': self.select_model(),
            'model_preferences': self.config.get('model_preferences', self.DEFAULT_MODEL_PREFERENCES)
        }

    def reset(self) -> None:
        """
        Reset cached state to force re-check of availability.

        Useful when Ollama may have been started/stopped since last check.
        """
        self._available = None
        self._models = []
        self._selected_model = None
        self._no_models_installed = False
        self.logger.debug("Ollama client state reset")

    def _categorize_error(self, exception: Exception) -> dict:
        """
        Map exception to user-friendly error information.

        Args:
            exception: The caught exception

        Returns:
            Dictionary with:
            - error_type: str - Category of error
            - message: str - User-friendly message
            - retryable: bool - Whether this error is worth retrying
        """
        # Connection errors
        if isinstance(exception, ConnectionError):
            return {
                'error_type': 'connection',
                'message': 'Cannot connect to Ollama. Is Ollama running? Start it with: ollama serve',
                'retryable': True
            }

        # Timeout errors
        if isinstance(exception, httpx.TimeoutException):
            return {
                'error_type': 'timeout',
                'message': 'LLM generation timed out. The model may be loading or overloaded. Try again.',
                'retryable': True
            }

        # Ollama API errors
        if OLLAMA_SDK_AVAILABLE and ResponseError is not None:
            if isinstance(exception, ResponseError):
                error_str = str(exception).lower()

                # Model not found
                if '404' in error_str or 'not found' in error_str:
                    return {
                        'error_type': 'model_not_found',
                        'message': 'Model not found. Run: ollama pull phi3',
                        'retryable': False
                    }

                # Service overloaded
                if '503' in error_str or 'unavailable' in error_str:
                    return {
                        'error_type': 'overloaded',
                        'message': 'Ollama is busy. Try again in a moment.',
                        'retryable': True
                    }

                # Bad request
                if '400' in error_str or 'bad request' in error_str:
                    return {
                        'error_type': 'bad_request',
                        'message': f'Invalid request to Ollama: {exception}',
                        'retryable': False
                    }

                # Generic API error
                return {
                    'error_type': 'api_error',
                    'message': f'Ollama API error: {exception}',
                    'retryable': False
                }

        # Unknown error
        return {
            'error_type': 'unknown',
            'message': str(exception),
            'retryable': False
        }

    def _error_response(self, error: str, model: str = None, error_type: str = 'unknown') -> dict:
        """
        Create standardized error response.

        Args:
            error: User-friendly error message
            model: Model that was being used (if any)
            error_type: Category of error for programmatic handling

        Returns:
            Standard error response dict
        """
        return {
            'success': False,
            'explanation': '',
            'model': model,
            'error': error,
            'error_type': error_type
        }

    def _generate_with_retry(self, model: str, prompt: str) -> dict:
        """
        Core generation call with automatic retry for transient errors.

        Retries on ConnectionError and httpx.TimeoutException.
        Does NOT retry on ResponseError (4xx/5xx) - those fail immediately.

        Args:
            model: Model name to use
            prompt: Prompt to send

        Returns:
            Ollama generate response dict

        Raises:
            RetryError: If all retry attempts exhausted
            ResponseError: If API returns error (not retried)
        """
        # Get retry config
        max_retries = self.config.get('max_retries', 3)
        delay_min = self.config.get('retry_delay_min', 2)
        delay_max = self.config.get('retry_delay_max', 8)

        # Build retry decorator dynamically based on config
        # reraise=False so tenacity raises RetryError when attempts exhausted
        @retry(
            retry=retry_if_exception_type((ConnectionError, httpx.TimeoutException)),
            stop=stop_after_attempt(max_retries),
            wait=wait_exponential(multiplier=1, min=delay_min, max=delay_max),
            reraise=False
        )
        def _do_generate():
            gen_options = self.config.get('generation_options', {})
            return self._client.generate(
                model=model,
                prompt=prompt,
                stream=False,
                options={
                    'temperature': gen_options.get('temperature', 0.3),
                    'num_predict': gen_options.get('num_predict', 300)
                }
            )

        return _do_generate()

    def generate_explanation(self, result: Dict, ml_classifier=None) -> Dict:
        """
        Generate a natural language explanation for the analysis result.

        Uses the Ollama LLM to create a human-readable explanation of the
        risk assessment, citing specific evidence from the analysis.

        Args:
            result: Full analysis result dictionary from the analyzer
            ml_classifier: Optional MLClassifier instance for keyword explanation

        Returns:
            Dictionary with:
            - success: bool - Whether generation succeeded
            - explanation: str - Generated explanation (empty on failure)
            - model: str|None - Model used for generation
            - error: str|None - Error message on failure
        """
        # Check availability
        if not self.is_available():
            if self._no_models_installed:
                return self._error_response(
                    'No LLM models installed. Run: ollama pull phi3',
                    error_type='no_models'
                )
            return self._error_response(
                'Ollama not available. Is Ollama running? Start it with: ollama serve',
                error_type='unavailable'
            )

        # Select model
        model = self.select_model()
        if not model:
            if self._no_models_installed:
                return self._error_response(
                    'No LLM models installed. Run: ollama pull phi3',
                    error_type='no_models'
                )
            return self._error_response(
                'No suitable model found',
                error_type='no_model_selected'
            )

        # Get ML explanation if classifier provided and ML analysis succeeded
        ml_explain = {}
        if ml_classifier and result.get('ml_analysis', {}).get('success'):
            body_text = result['ml_analysis'].get('body_text', '')
            if body_text:
                ml_explain = ml_classifier.explain(body_text, top_n=5)

        # Build prompt
        prompt = build_explanation_prompt(result, ml_explain)

        # Attempt generation with retry
        try:
            response = self._generate_with_retry(model, prompt)
            explanation = response.get('response', '').strip()

            return {
                'success': True,
                'explanation': explanation,
                'model': model,
                'error': None,
                'error_type': None
            }

        except RetryError as e:
            # All retries exhausted for transient errors
            original = e.last_attempt.exception()
            error_info = self._categorize_error(original)
            self.logger.error(f"Explanation generation failed after retries: {error_info['message']}")
            return self._error_response(
                error_info['message'],
                model=model,
                error_type=error_info['error_type']
            )

        except Exception as e:
            # Check if this is a ResponseError (non-retryable API error)
            # ResponseError may be None if SDK not installed, so check type name
            if type(e).__name__ == 'ResponseError':
                error_info = self._categorize_error(e)
                self.logger.error(f"Explanation generation failed: {error_info['message']}")
                return self._error_response(
                    error_info['message'],
                    model=model,
                    error_type=error_info['error_type']
                )
            else:
                # Unexpected error - categorize it anyway
                error_info = self._categorize_error(e)
                self.logger.error(f"Unexpected generation error: {error_info['message']}")
                return self._error_response(
                    error_info['message'],
                    model=model,
                    error_type=error_info['error_type']
                )

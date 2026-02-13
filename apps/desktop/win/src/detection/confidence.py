"""
Confidence calculator for multi-signal session detection.

Combines multiple detection signals to produce a confidence level
that distinguishes between:
- Program running but idle (LOW confidence)
- Possible session (MEDIUM confidence)
- Confirmed active session (HIGH confidence)
"""

from typing import Dict, Any


class Confidence:
    """Confidence levels for session detection."""
    LOW = 'low'      # Single weak signal (process running only)
    MEDIUM = 'medium'  # Two signals OR one strong signal
    HIGH = 'high'    # Multiple signals agreeing


def calculate_confidence(signals: Dict[str, Any]) -> str:
    """
    Calculate confidence level from detection signals.

    Signals dict may contain:
    - active_connection: bool - ESTABLISHED connection to non-localhost
    - log_session_active: bool - Log file indicates active session
    - cpu_active: bool - CPU usage above idle threshold (5%)
    - service_running: bool - Windows service running (for RDP)

    Scoring:
    - active_connection: +2 (strong signal - direct evidence)
    - log_session_active: +2 (strong signal - app's own record)
    - cpu_active: +1 (weak signal - could be background activity)
    - service_running: +1 (weak signal - service can run without session)

    Returns:
        'high' (score >= 4): Multiple strong signals confirm session
        'medium' (score >= 2): Strong signal or multiple weak signals
        'low' (score < 2): Weak or no signals

    Examples:
        >>> calculate_confidence({'active_connection': True, 'log_session_active': True})
        'high'  # Score 4: Two strong signals

        >>> calculate_confidence({'active_connection': True})
        'medium'  # Score 2: One strong signal

        >>> calculate_confidence({'cpu_active': True})
        'low'  # Score 1: One weak signal
    """
    score = 0

    # Strong signals (+2 each)
    if signals.get('active_connection'):
        score += 2
    if signals.get('log_session_active'):
        score += 2

    # Weak signals (+1 each)
    if signals.get('cpu_active'):
        score += 1
    if signals.get('service_running'):
        score += 1

    # Determine confidence level
    if score >= 4:
        return Confidence.HIGH
    if score >= 2:
        return Confidence.MEDIUM
    return Confidence.LOW

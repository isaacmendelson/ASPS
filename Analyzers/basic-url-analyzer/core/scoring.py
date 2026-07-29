"""
Scoring and risk-level utilities.

Extracted from analyzer.py to give the scoring policy a dedicated home
where it can be versioned, configured and tested independently.

Public interface:
- determine_risk_level(risk_score) -> str
- calculate_confidence(missing_data) -> float
- tiered_score_combination(ml_score, rules_score, logger) -> float
"""

import logging

logger = logging.getLogger(__name__)


def determine_risk_level(risk_score: int) -> str:
    """Map a 0-100 integer risk score to a risk level label.

    Args:
        risk_score: Integer score where 0 = error/no result, 1 = safest,
                    100 = most dangerous.

    Returns:
        'LOW', 'MEDIUM', or 'HIGH'.
    """
    if risk_score < 30:
        return 'LOW'
    elif risk_score < 60:
        return 'MEDIUM'
    return 'HIGH'


def calculate_confidence(missing_data: list) -> float:
    """Calculate analysis confidence based on available data components.

    Args:
        missing_data: List of component names that were unavailable
                      (e.g. ['WHOIS', 'content']).

    Returns:
        Confidence value in [0.3, 1.0].
    """
    if not missing_data:
        return 1.0

    penalty_per_component = 0.2
    confidence = 1.0 - (len(missing_data) * penalty_per_component)
    return max(confidence, 0.3)


def tiered_score_combination(
    ml_score: float,
    rules_score: float,
    _logger: logging.Logger = None,
) -> float:
    """Combine ML and rules-engine scores using a tiered decision tree.

    Tiers:
    1. ML very confident (>=92%) -> Trust ML completely (return 0.92).
    2. ML says safe (<15%) but Rules suspicious (>=70%) -> Trust Rules (0.75).
    2b. ML says safe (<15%) and Rules also low -> Balanced weighted average.
    3. ML moderately confident (>=70%) with Rules agreement -> 0.80.
    3b. ML moderately confident, Rules weak -> ML-weighted average.
    4. Rules strong (>=70%), ML medium -> 0.72.
    5. Both uncertain -> Equal weighted average.

    Args:
        ml_score: ML classifier score in [0.0, 1.0]; higher = more likely scam.
        rules_score: Rules engine score in [0.0, 1.0]; higher = more suspicious.
        _logger: Optional logger; falls back to module logger when None.

    Returns:
        Combined risk score in [0.0, 1.0].
    """
    log = _logger or logger

    # Tier 1: ML is very confident it's a scam
    if ml_score >= 0.92:
        log.debug("Tier 1: ML very confident (%.2f) -> score=0.92", ml_score)
        return 0.92

    # Tier 2: ML thinks it's safe
    if ml_score <= 0.15:
        if rules_score >= 0.70:
            log.debug(
                "Tier 2: ML safe (%.2f) but Rules suspicious (%.2f) -> score=0.75",
                ml_score, rules_score,
            )
            return 0.75
        combined = (rules_score * 0.6) + (ml_score * 0.4)
        log.debug("Tier 2: Both low -> score=%.2f", combined)
        return combined

    # Tier 3: ML moderately confident (70-92%)
    if ml_score >= 0.70:
        if rules_score >= 0.50:
            log.debug(
                "Tier 3: ML confident (%.2f) + Rules agree (%.2f) -> score=0.80",
                ml_score, rules_score,
            )
            return 0.80
        combined = (ml_score * 0.75) + (rules_score * 0.25)
        log.debug("Tier 3: ML confident, Rules weak -> score=%.2f", combined)
        return combined

    # Tier 4: ML has medium confidence (15-70%), Rules strong
    if rules_score >= 0.70:
        log.debug(
            "Tier 4: Rules strong (%.2f), ML medium (%.2f) -> score=0.72",
            rules_score, ml_score,
        )
        return 0.72

    # Tier 5: Both uncertain - equal weighted average
    combined = (rules_score * 0.5) + (ml_score * 0.5)
    log.debug("Tier 5: Both uncertain -> score=%.2f", combined)
    return combined

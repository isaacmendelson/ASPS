"""
Result formatting utilities.

Generates the human-facing portions of the analysis result:
- Red flags list (from detected patterns + WHOIS data)
- Recommendation text (from risk level, category and data coverage)

Extracted from ScamAnalyzer to keep formatting logic separate from
orchestration and scoring concerns.

Public interface:
- generate_red_flags(patterns, whois_data) -> list
- generate_recommendation(risk_level, category, missing_data) -> str
"""

import logging
from typing import Dict, List

logger = logging.getLogger(__name__)


def generate_red_flags(patterns: List[Dict], whois_data: Dict) -> List[str]:
    """Convert detected analysis patterns into human-readable red flag strings.

    Args:
        patterns: List of pattern dicts from RulesEngine (each has 'type',
                  'name', 'description', 'matched_text', 'weight').
        whois_data: WHOIS result dict from WhoisChecker.

    Returns:
        List of plain-text red flag descriptions.
    """
    flags = []
    for pattern in patterns:
        if pattern['type'] == 'whois':
            if 'new_domain' in pattern['name']:
                age = whois_data.get('age_days', 0)
                flags.append(f"Very new domain ({age} days old)")
            elif pattern['name'] == 'privacy_protected':
                flags.append("Domain privacy protection enabled")
            elif pattern['name'] == 'suspicious_location':
                country = whois_data.get('country', 'Unknown')
                flags.append(f"Registered in high-risk country ({country})")
            else:
                flags.append(pattern['description'])
        else:
            flags.append(pattern['description'])
    return flags


def generate_recommendation(
    risk_level: str,
    category: str,
    missing_data: List[str],
) -> str:
    """Generate a one-sentence recommendation for the end user.

    Args:
        risk_level: 'HIGH', 'MEDIUM', 'LOW', or 'UNKNOWN'.
        category: Raw category string from CategoryClassifier or PurposeClassifier.
        missing_data: List of unavailable data components (affects the warning prefix).

    Returns:
        Recommendation string, optionally prefixed with an incomplete-analysis warning.
    """
    if missing_data:
        warning = f"Incomplete analysis (missing: {', '.join(missing_data)}). "
    else:
        warning = ""

    level = risk_level.lower()
    if level == 'high':
        return (
            f"{warning}HIGH RISK - Strong indicators of {category}. "
            "Do NOT engage or provide any personal/financial information."
        )
    if level == 'medium':
        return (
            f"{warning}MEDIUM RISK - Some suspicious indicators detected. "
            "Exercise caution and verify authenticity before proceeding."
        )
    if level == 'low':
        return (
            f"{warning}LOW RISK - Few or no scam indicators detected. "
            "Site appears relatively safe."
        )
    return f"{warning}Unable to determine risk level. Manual verification recommended."

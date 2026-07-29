"""
Category and scam-type classification maps.

Maps raw category strings (from CategoryClassifier) to:
- Backend WebsiteType enum values (_CATEGORY_TO_WEBSITE_TYPE)
- Human-readable scam type labels (_CATEGORY_TO_SCAM_TYPE)

These are static lookup tables extracted from analyzer.py to keep
the orchestrator thin and the mappings version-controlled in one place.
"""

import logging

logger = logging.getLogger(__name__)

# ---------------------------------------------------------------------------
# Category → Backend WebsiteType enum string
# ---------------------------------------------------------------------------
_CATEGORY_TO_WEBSITE_TYPE = {
    # Financial
    'banking': 'Banking', 'credit_union': 'Banking',
    'insurance': 'Insurance', 'investment': 'Investment',
    'stock_trading': 'Exchange', 'crypto_exchange': 'Exchange',
    'payment_service': 'Banking', 'lending': 'Banking',
    # Shopping
    'ecommerce': 'ECommerce', 'marketplace': 'ECommerce',
    'auction': 'ECommerce', 'classifieds': 'ECommerce',
    'grocery': 'ECommerce', 'fashion': 'ECommerce', 'electronics': 'ECommerce',
    # Government
    'government': 'Government', 'municipality': 'Government',
    'military': 'Government', 'court': 'Government',
    'tax_authority': 'Government', 'public_service': 'Government',
    # Health
    'hospital': 'Healthcare', 'clinic': 'Healthcare',
    'pharmacy': 'Healthcare', 'telehealth': 'Healthcare',
    'mental_health': 'Healthcare',
    # Education
    'university': 'Education', 'school': 'Education',
    'online_course': 'Education', 'elearning': 'Education',
    # Entertainment
    'streaming': 'Entertainment', 'gaming': 'Entertainment',
    'gambling': 'Gambling', 'sports_betting': 'Gambling',
    'adult_content': 'AdultContent',
    # Media
    'news': 'News', 'blog': 'News', 'forum': 'Analytics',
    'social_network': 'Dating', 'messaging': 'Dating',
    # Services
    'legal': 'Legal', 'accounting': 'Analytics',
    'real_estate': 'RealEstate', 'travel': 'Travel', 'job_board': 'Analytics',
    # Technology
    'saas': 'Analytics', 'cloud': 'Analytics', 'web_hosting': 'Analytics',
    'vpn_proxy': 'Analytics', 'developer_tools': 'Analytics',
    # Other
    'restaurant': 'Restaurant', 'automotive': 'Unknown',
    'pets': 'Unknown', 'nonprofit': 'Nonprofit', 'religious': 'Unknown',
    # New categories
    'language_learning': 'Education',
    'review_directory': 'Analytics',
    'ride_delivery': 'ECommerce',
    # Legacy purpose classifier mappings
    'crypto_scam': 'Exchange', 'investment_scam': 'Exchange',
    'fake_ecommerce': 'ECommerce', 'romance_scam': 'Dating',
    'finance_banking': 'Banking', 'news_media': 'News',
    'ecommerce_shopping': 'ECommerce', 'technology': 'Analytics',
}

# ---------------------------------------------------------------------------
# Category → scam type label (used only when risk is not LOW)
# ---------------------------------------------------------------------------
_CATEGORY_TO_SCAM_TYPE = {
    # Financial
    'banking': 'Suspected Banking Fraud',
    'credit_union': 'Suspected Banking Fraud',
    'insurance': 'Suspected Insurance Fraud',
    'investment': 'Suspected Investment Fraud',
    'stock_trading': 'Suspected Investment Fraud',
    'crypto_exchange': 'Suspected Crypto Fraud',
    'payment_service': 'Suspected Payment Fraud',
    'lending': 'Suspected Lending Fraud',
    # Shopping
    'ecommerce': 'Suspected Fake Online Store',
    'marketplace': 'Suspected Fake Marketplace',
    'auction': 'Suspected Auction Fraud',
    'classifieds': 'Suspected Classifieds Fraud',
    'grocery': 'Suspected Fake Online Store',
    'fashion': 'Suspected Fake Online Store',
    'electronics': 'Suspected Fake Online Store',
    # Government
    'government': 'Suspected Government Impersonation',
    'municipality': 'Suspected Government Impersonation',
    'military': 'Suspected Government Impersonation',
    'court': 'Suspected Government Impersonation',
    'tax_authority': 'Suspected Tax Scam',
    'public_service': 'Suspected Government Impersonation',
    # Health
    'hospital': 'Suspected Health Fraud',
    'clinic': 'Suspected Health Fraud',
    'pharmacy': 'Suspected Fake Pharmacy',
    'telehealth': 'Suspected Health Fraud',
    'mental_health': 'Suspected Health Fraud',
    # Education
    'university': 'Suspected Fake Education Site',
    'school': 'Suspected Fake Education Site',
    'online_course': 'Suspected Fake Education Site',
    'elearning': 'Suspected Fake Education Site',
    'language_learning': 'Suspected Fake Education Site',
    # Entertainment
    'streaming': 'Suspected Fake Streaming Site',
    'gaming': 'Suspected Gaming Fraud',
    'gambling': 'Suspected Illegal Gambling',
    'sports_betting': 'Suspected Illegal Gambling',
    'adult_content': 'Suspected Adult Content Scam',
    # Media
    'news': 'Suspected Fake News Site',
    'blog': 'Suspected Fake Blog',
    'forum': 'Suspected Fake Forum',
    'social_network': 'Suspected Social Engineering',
    'messaging': 'Suspected Social Engineering',
    # Services
    'legal': 'Suspected Fake Legal Service',
    'accounting': 'Suspected Fake Service',
    'real_estate': 'Suspected Real Estate Fraud',
    'travel': 'Suspected Travel Fraud',
    'job_board': 'Suspected Job Scam',
    'review_directory': 'Suspected Fake Reviews',
    'ride_delivery': 'Suspected Fake Service',
    # Technology
    'saas': 'Suspected Tech Support Scam',
    'cloud': 'Suspected Tech Support Scam',
    'web_hosting': 'Suspected Tech Support Scam',
    'vpn_proxy': 'Suspected Fake VPN/Privacy Scam',
    'developer_tools': 'Suspected Tech Support Scam',
    # Other
    'restaurant': 'Suspected Fake Business',
    'automotive': 'Suspected Fake Business',
    'pets': 'Suspected Fake Business',
    'nonprofit': 'Suspected Charity Fraud',
    'religious': 'Suspected Charity Fraud',
}


def map_category_to_website_type(category: str) -> str:
    """Map a raw category string to the backend WebsiteType enum string.

    Args:
        category: Raw category from CategoryClassifier (e.g. 'banking').

    Returns:
        Backend WebsiteType enum string (e.g. 'Banking'), defaulting to 'Unknown'.
    """
    return _CATEGORY_TO_WEBSITE_TYPE.get(category, 'Unknown')


def determine_scam_type(risk_level: str, category: str) -> str:
    """Determine scam type label based on website category and risk level.

    Args:
        risk_level: 'LOW', 'MEDIUM', or 'HIGH'.
        category: Raw category from CategoryClassifier.

    Returns:
        Human-readable scam type string, or empty string for LOW risk.
    """
    if risk_level == 'LOW':
        return ''

    scam_type = _CATEGORY_TO_SCAM_TYPE.get(category, '')
    if not scam_type:
        if risk_level == 'HIGH':
            return 'Unknown Fraud Type - Requires Investigation'
        return 'Suspicious Activity Detected - Requires Investigation'

    return scam_type

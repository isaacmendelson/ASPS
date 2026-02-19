"""
Website category classifier - identifies what type of website/business it is
"""

import re
from pathlib import Path
from typing import Dict, List, Tuple
import sys
sys.path.append(str(Path(__file__).parent.parent))
from utils.logger import setup_logger


class CategoryClassifier:
    """Classifies website into business/content categories"""

    # Website categories with keywords (Hebrew + English)
    CATEGORIES = {
        'news_media': {
            'name_en': 'News & Media',
            'name_he': 'חדשות ומדיה',
            'keywords': [
                'news', 'breaking', 'headline', 'reporter', 'journalist', 'article',
                'editorial', 'press', 'media', 'magazine', 'newspaper', 'blog',
                'חדשות', 'כתבה', 'מגזין', 'עיתון', 'כתב', 'מהדורה'
            ],
            'weight': 1.0
        },
        'finance_banking': {
            'name_en': 'Finance & Banking',
            'name_he': 'פיננסים ובנקאות',
            'keywords': [
                'bank', 'banking', 'account', 'mortgage', 'loan', 'credit card',
                'savings', 'checking', 'investment', 'finance', 'insurance', 'stock',
                'בנק', 'חשבון', 'משכנתא', 'הלוואה', 'אשראי', 'חיסכון', 'ביטוח', 'מניות'
            ],
            'weight': 1.0
        },
        'ecommerce_shopping': {
            'name_en': 'E-commerce & Shopping',
            'name_he': 'קניות אונליין',
            'keywords': [
                'shop', 'store', 'buy', 'cart', 'checkout', 'product', 'price',
                'shipping', 'delivery', 'order', 'sale', 'discount', 'deal',
                'חנות', 'קנייה', 'עגלה', 'מוצר', 'מחיר', 'משלוח', 'הזמנה', 'מבצע'
            ],
            'weight': 1.0
        },
        'technology': {
            'name_en': 'Technology',
            'name_he': 'טכנולוגיה',
            'keywords': [
                'software', 'app', 'technology', 'tech', 'developer', 'programming',
                'code', 'api', 'cloud', 'saas', 'startup', 'digital', 'computer',
                'תוכנה', 'אפליקציה', 'טכנולוגיה', 'מפתח', 'תכנות', 'ענן', 'סטארטאפ'
            ],
            'weight': 1.0
        },
        'healthcare_medical': {
            'name_en': 'Healthcare & Medical',
            'name_he': 'בריאות ורפואה',
            'keywords': [
                'health', 'medical', 'doctor', 'hospital', 'clinic', 'patient',
                'medicine', 'treatment', 'symptom', 'disease', 'pharmacy', 'nurse',
                'בריאות', 'רפואה', 'רופא', 'בית חולים', 'מרפאה', 'תרופה', 'טיפול'
            ],
            'weight': 1.0
        },
        'education': {
            'name_en': 'Education',
            'name_he': 'חינוך',
            'keywords': [
                'education', 'school', 'university', 'college', 'course', 'learn',
                'student', 'teacher', 'class', 'degree', 'training', 'tutorial',
                'חינוך', 'בית ספר', 'אוניברסיטה', 'קורס', 'לימוד', 'סטודנט', 'מורה'
            ],
            'weight': 1.0
        },
        'government': {
            'name_en': 'Government',
            'name_he': 'ממשלה',
            'keywords': [
                'government', 'gov', 'municipal', 'ministry', 'public service',
                'citizen', 'official', 'federal', 'state', 'city',
                'ממשלה', 'עירייה', 'משרד', 'ציבורי', 'אזרח', 'רשמי'
            ],
            'weight': 1.0
        },
        'entertainment': {
            'name_en': 'Entertainment',
            'name_he': 'בידור',
            'keywords': [
                'entertainment', 'movie', 'film', 'music', 'game', 'gaming',
                'video', 'stream', 'watch', 'play', 'concert', 'show', 'tv',
                'בידור', 'סרט', 'מוזיקה', 'משחק', 'וידאו', 'צפייה', 'הופעה'
            ],
            'weight': 1.0
        },
        'food_restaurant': {
            'name_en': 'Food & Restaurant',
            'name_he': 'אוכל ומסעדות',
            'keywords': [
                'restaurant', 'food', 'menu', 'order', 'delivery', 'eat', 'chef',
                'cuisine', 'recipe', 'dining', 'cafe', 'pizza', 'sushi',
                'מסעדה', 'אוכל', 'תפריט', 'משלוח', 'שף', 'מתכון', 'קפה'
            ],
            'weight': 1.0
        },
        'pets_animals': {
            'name_en': 'Pets & Animals',
            'name_he': 'חיות מחמד',
            'keywords': [
                'pet', 'dog', 'cat', 'animal', 'puppy', 'kitten', 'vet', 'veterinary',
                'grooming', 'breeder', 'adoption', 'shelter', 'pet food', 'pet store',
                'חיות מחמד', 'כלב', 'חתול', 'גור', 'וטרינר', 'מאלף', 'אילוף'
            ],
            'weight': 1.0
        },
        'real_estate': {
            'name_en': 'Real Estate',
            'name_he': 'נדל"ן',
            'keywords': [
                'real estate', 'property', 'house', 'apartment', 'rent', 'buy home',
                'sell house', 'mortgage', 'realtor', 'listing', 'bedroom', 'sqft',
                'נדלן', 'דירה', 'בית', 'שכירות', 'מכירה', 'תיווך', 'נכס'
            ],
            'weight': 1.0
        },
        'travel_tourism': {
            'name_en': 'Travel & Tourism',
            'name_he': 'תיירות ונסיעות',
            'keywords': [
                'travel', 'hotel', 'flight', 'vacation', 'tourism', 'booking',
                'destination', 'trip', 'tour', 'airline', 'resort', 'beach',
                'תיירות', 'מלון', 'טיסה', 'חופשה', 'הזמנה', 'יעד', 'טיול'
            ],
            'weight': 1.0
        },
        'sports_fitness': {
            'name_en': 'Sports & Fitness',
            'name_he': 'ספורט וכושר',
            'keywords': [
                'sport', 'fitness', 'gym', 'workout', 'exercise', 'team', 'game',
                'player', 'match', 'score', 'league', 'football', 'basketball',
                'ספורט', 'כושר', 'חדר כושר', 'אימון', 'קבוצה', 'משחק', 'שחקן'
            ],
            'weight': 1.0
        },
        'automotive': {
            'name_en': 'Automotive',
            'name_he': 'רכב',
            'keywords': [
                'car', 'auto', 'vehicle', 'automotive', 'dealer', 'used car',
                'new car', 'truck', 'motorcycle', 'repair', 'garage', 'lease',
                'רכב', 'מכונית', 'אוטו', 'סוכנות', 'יד שנייה', 'מוסך', 'ליסינג'
            ],
            'weight': 1.0
        },
        'legal_services': {
            'name_en': 'Legal Services',
            'name_he': 'שירותים משפטיים',
            'keywords': [
                'lawyer', 'attorney', 'legal', 'law firm', 'court', 'lawsuit',
                'contract', 'litigation', 'counsel', 'justice',
                'עורך דין', 'משפט', 'בית משפט', 'חוזה', 'ייעוץ משפטי'
            ],
            'weight': 1.0
        },
        'social_network': {
            'name_en': 'Social Network',
            'name_he': 'רשת חברתית',
            'keywords': [
                'social', 'profile', 'friend', 'follow', 'share', 'post', 'like',
                'comment', 'community', 'network', 'connect', 'message',
                'חברתי', 'פרופיל', 'חבר', 'עוקב', 'שיתוף', 'פוסט', 'קהילה'
            ],
            'weight': 1.0
        },
        'subscription_service': {
            'name_en': 'Subscription Service',
            'name_he': 'שירות מנויים',
            'keywords': [
                'subscription', 'subscribe', 'member', 'membership', 'premium',
                'plan', 'monthly', 'annual', 'unlimited', 'access',
                'מנוי', 'הרשמה', 'חבר', 'פרימיום', 'תוכנית', 'חודשי'
            ],
            'weight': 1.0
        }
    }

    def __init__(self):
        """Initialize category classifier"""
        self.logger = setup_logger('category_classifier')

    def classify(self, content: Dict, domain: str = '') -> Dict:
        """
        Classify website into a business category.

        Args:
            content: Extracted content dictionary
            domain: Domain name (optional, for additional hints)

        Returns:
            Classification result with category and confidence
        """
        try:
            self.logger.info("Classifying website category")

            # Combine text for analysis
            text_to_analyze = ' '.join([
                content.get('title', ''),
                content.get('meta_description', ''),
                ' '.join(content.get('headings', {}).get('h1', [])),
                ' '.join(content.get('headings', {}).get('h2', [])),
                content.get('body_text', '')[:5000],  # First 5000 chars
                domain
            ]).lower()

            # Score each category
            category_scores = {}
            for cat_id, cat_data in self.CATEGORIES.items():
                score = self._score_category(text_to_analyze, cat_data['keywords'])
                category_scores[cat_id] = score

            # Get best match
            best_category, confidence = self._get_best_match(category_scores)

            # Get category info
            if best_category and best_category in self.CATEGORIES:
                cat_info = self.CATEGORIES[best_category]
                name_en = cat_info['name_en']
                name_he = cat_info['name_he']
            else:
                name_en = 'Unknown'
                name_he = 'לא ידוע'

            result = {
                'success': True,
                'category': best_category or 'unknown',
                'name_en': name_en,
                'name_he': name_he,
                'confidence': confidence,
                'all_scores': category_scores,
                'error': ''
            }

            self.logger.info(f"Category: {name_en} ({confidence:.2f} confidence)")
            return result

        except Exception as e:
            self.logger.error(f"Category classification failed: {str(e)}")
            return {
                'success': False,
                'category': 'unknown',
                'name_en': 'Unknown',
                'name_he': 'לא ידוע',
                'confidence': 0.0,
                'all_scores': {},
                'error': str(e)
            }

    def _score_category(self, text: str, keywords: List[str]) -> float:
        """
        Score how well text matches a category's keywords.

        Args:
            text: Text to analyze (lowercase)
            keywords: List of keywords for the category

        Returns:
            Score from 0.0 to 1.0
        """
        if not keywords:
            return 0.0

        matches = 0
        for keyword in keywords:
            # Use word boundary matching for better accuracy
            pattern = r'\b' + re.escape(keyword.lower()) + r'\b'
            if re.search(pattern, text):
                matches += 1

        # Normalize: more matches = higher score, but cap at 1.0
        # Use sqrt to give diminishing returns for more matches
        score = min((matches / len(keywords)) * 2, 1.0)
        return round(score, 3)

    def _get_best_match(self, scores: Dict[str, float]) -> Tuple[str, float]:
        """
        Get the best matching category.

        Args:
            scores: Dictionary of category_id -> score

        Returns:
            Tuple of (best_category_id, confidence)
        """
        if not scores:
            return None, 0.0

        # Get highest scoring category
        best_cat = max(scores, key=scores.get)
        best_score = scores[best_cat]

        # Minimum threshold
        if best_score < 0.1:
            return None, 0.0

        return best_cat, best_score

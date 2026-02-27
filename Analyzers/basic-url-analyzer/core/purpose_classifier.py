"""
Purpose classification - categorizes website purpose/scam type
"""

import json
from pathlib import Path
from typing import Dict, List, Tuple
import sys
sys.path.append(str(Path(__file__).parent.parent))
from utils.logger import setup_logger


class PurposeClassifier:
    """Classifies website purpose and scam type"""
    
    def __init__(self):
        """Initialize purpose classifier"""
        self.logger = setup_logger('purpose_classifier')
        
        # Load patterns
        patterns_path = Path(__file__).parent.parent / 'config' / 'patterns.json'
        with open(patterns_path, 'r') as f:
            patterns = json.load(f)
        
        self.categories = patterns['purpose_categories']
    
    def classify(self, content: Dict, detected_patterns: List[Dict]) -> Dict:
        """
        Classify website purpose
        
        Args:
            content: Extracted content
            detected_patterns: List of detected scam patterns
        
        Returns:
            Classification result
        """
        try:
            self.logger.info("Classifying website purpose")
            
            # Combine all text for keyword matching
            text_to_analyze = ' '.join([
                content.get('title', ''),
                content.get('meta_description', ''),
                ' '.join(content.get('headings', {}).get('h1', [])),
                ' '.join(content.get('headings', {}).get('h2', [])),
                content.get('body_text', '')[:3000],  # First 3000 chars
            ]).lower()
            
            # Extract pattern names
            pattern_names = [p['name'] for p in detected_patterns]
            
            # Score each category
            category_scores = {}
            
            for category_name, category_data in self.categories.items():
                score = self._score_category(
                    category_name,
                    category_data,
                    text_to_analyze,
                    pattern_names
                )
                category_scores[category_name] = score
            
            # Get best match
            best_category, confidence = self._get_best_match(category_scores)
            
            # Get description
            description = self._get_category_description(best_category)
            
            result = {
                'success': True,
                'category': best_category,
                'confidence': confidence,
                'description': description,
                'all_scores': category_scores,
                'error': ''
            }
            
            self.logger.info(f"Classification: {best_category} ({confidence:.2f} confidence)")
            return result
        
        except Exception as e:
            self.logger.error(f"Classification failed: {str(e)}")
            return {
                'success': False,
                'category': 'unknown',
                'confidence': 0.0,
                'description': 'Unable to classify',
                'all_scores': {},
                'error': f"Classification failed: {str(e)}"
            }
    
    def _score_category(self, category_name: str, category_data: Dict,
                       text: str, pattern_names: List[str]) -> float:
        """Score a single category"""
        score = 0.0
        
        # Keyword matching (0-0.5 points)
        keywords = category_data.get('keywords', [])
        if keywords:
            keyword_matches = sum(1 for keyword in keywords if keyword in text)
            keyword_score = min(keyword_matches / len(keywords), 1.0) * 0.5
            score += keyword_score
        
        # Required pattern matching (0-0.5 points)
        required_patterns = category_data.get('required_patterns', [])
        if required_patterns:
            pattern_matches = sum(1 for pattern in required_patterns if pattern in pattern_names)
            pattern_score = min(pattern_matches / len(required_patterns), 1.0) * 0.5
            score += pattern_score
        
        return score
    
    def _get_best_match(self, category_scores: Dict[str, float]) -> Tuple[str, float]:
        """Get best matching category"""
        # Remove 'unknown' and 'legitimate' from initial consideration
        filtered_scores = {
            k: v for k, v in category_scores.items() 
            if k not in ['unknown', 'legitimate']
        }
        
        if not filtered_scores:
            return 'unknown', 0.0
        
        # Get highest score
        best_category = max(filtered_scores, key=filtered_scores.get)
        best_score = filtered_scores[best_category]
        
        # If score is too low, classify as unknown
        if best_score < 0.3:
            return 'unknown', best_score
        
        # Check if might be legitimate (low overall risk)
        if best_score < 0.4 and category_scores.get('legitimate', 0) > 0:
            return 'legitimate', category_scores['legitimate']
        
        return best_category, best_score
    
    def _get_category_description(self, category: str) -> str:
        """Get human-readable description of category"""
        descriptions = {
            'investment_scam': 'Investment scam promising unrealistic returns',
            'get_rich_quick': 'Get-rich-quick scheme',
            'fake_ecommerce': 'Fake e-commerce or shopping site',
            'phishing': 'Phishing attempt to steal credentials',
            'tech_support_scam': 'Tech support scam',
            'romance_scam': 'Romance or dating scam',
            'lottery_scam': 'Lottery or prize scam',
            'legitimate': 'Appears to be legitimate',
            'unknown': 'Unable to determine purpose'
        }
        
        return descriptions.get(category, 'Unknown purpose')

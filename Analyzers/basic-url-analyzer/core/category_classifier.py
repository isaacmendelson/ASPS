"""
Website category classifier - identifies what type of website/business it is.
Uses two detection layers:
  Layer 2: Domain pattern matching (TLD, country-specific suffixes)
  Layer 3: Enhanced content analysis (weighted keywords + structure signals + meta tags)
"""

import json
import re
from pathlib import Path
from typing import Dict, List, Tuple, Optional
from urllib.parse import urlparse
import sys
sys.path.append(str(Path(__file__).parent.parent))
from utils.logger import setup_logger


class CategoryClassifier:
    """Classifies website into business/content categories using domain + content analysis"""

    def __init__(self):
        """Initialize category classifier and load patterns"""
        self.logger = setup_logger('category_classifier')

        # Load category patterns from config
        patterns_path = Path(__file__).parent.parent / 'config' / 'category_patterns.json'
        with open(patterns_path, 'r', encoding='utf-8') as f:
            self.config = json.load(f)

        self.categories = self.config['categories']
        self.domain_patterns = self.config['domain_patterns']
        self.scoring = self.config['scoring']

        # Build reverse domain lookup for fast matching
        self._domain_lookup = {}
        for pattern_type, data in self.domain_patterns.items():
            confidence = self.scoring['layer2_confidence'].get(pattern_type, 0.80)
            for pattern, category_id in data['patterns'].items():
                self._domain_lookup[pattern] = (category_id, confidence, pattern_type)

        # Blocked/error page indicators
        self._blocked_titles = {
            'access denied', '403 forbidden', '401 unauthorized',
            'blocked', 'error', 'not found', '404',
            'captcha', 'robot check', 'are you human',
            'just a moment', 'checking your browser',
        }

    def classify(self, content: Dict, domain: str = '') -> Dict:
        """
        Classify website into a business category using Layer 2 + Layer 3.

        Args:
            content: Extracted content dictionary (title, body_text, headings, etc.)
            domain: Full domain name (e.g. 'www.example.gov.il')

        Returns:
            Classification result with category, confidence, detection_method, matched_signals
        """
        try:
            self.logger.info("Classifying website category")

            # Layer 2: Domain pattern matching
            layer2_result = self._layer2_domain_match(domain)

            # Layer 3: Content analysis
            layer3_result = self._layer3_content_analysis(content, domain)

            # Combine layers
            result = self._combine_layers(layer2_result, layer3_result)

            # Minimum confidence threshold - below this, return unknown
            if result['category'] != 'unknown' and result['confidence'] < 0.25:
                self.logger.info(
                    f"Low confidence ({result['confidence']:.2f}) for {result['category']} - "
                    f"returning unknown"
                )
                result = self._make_result(
                    'unknown', 'unknown', 'Unknown', 'לא ידוע',
                    0.0, 'none', [], None, 0.0, {}
                )

            self.logger.info(
                f"Category: {result['name_en']} "
                f"(confidence={result['confidence']:.2f}, "
                f"method={result['detection_method']})"
            )
            return result

        except Exception as e:
            self.logger.error(f"Category classification failed: {str(e)}")
            return {
                'success': False,
                'category': 'unknown',
                'category_group': 'unknown',
                'name_en': 'Unknown',
                'name_he': 'לא ידוע',
                'confidence': 0.0,
                'detection_method': 'none',
                'matched_signals': [],
                'secondary_category': None,
                'secondary_confidence': 0.0,
                'all_scores': {},
                'error': str(e)
            }

    # -------------------------------------------------------------------------
    # Layer 2: Domain Pattern Matching
    # -------------------------------------------------------------------------

    def _layer2_domain_match(self, domain: str) -> Optional[Dict]:
        """
        Match domain against known TLD/suffix patterns.

        Returns result dict or None if no match.
        """
        if not domain:
            return None

        domain = domain.lower().strip()
        # Remove www. prefix
        if domain.startswith('www.'):
            domain = domain[4:]

        matched_signals = []
        best_match = None
        best_confidence = 0.0

        # Check domain suffixes from most specific to least specific
        # e.g. for "tax.gov.il" check ".gov.il" before ".il"
        parts = domain.split('.')
        for i in range(len(parts)):
            suffix = '.' + '.'.join(parts[i:])
            if suffix in self._domain_lookup:
                category_id, confidence, pattern_type = self._domain_lookup[suffix]
                if confidence > best_confidence:
                    best_confidence = confidence
                    best_match = category_id
                    matched_signals = [{
                        'type': 'domain_tld',
                        'value': suffix,
                        'weight': confidence
                    }]

        if best_match and best_match in self.categories:
            cat = self.categories[best_match]
            return {
                'category': best_match,
                'category_group': cat['group'],
                'name_en': cat['name_en'],
                'name_he': cat['name_he'],
                'confidence': best_confidence,
                'matched_signals': matched_signals
            }

        return None

    # -------------------------------------------------------------------------
    # Layer 3: Enhanced Content Analysis
    # -------------------------------------------------------------------------

    def _layer3_content_analysis(self, content: Dict, domain: str = '') -> Optional[Dict]:
        """
        Analyze content using weighted keyword matching, structure signals, and meta tags.

        Returns result dict or None if no confident match.
        """
        if not content:
            return None

        # Detect blocked/error pages - don't trust their content
        title = (content.get('title', '') or '').lower().strip()
        if any(blocked in title for blocked in self._blocked_titles):
            self.logger.info(f"Blocked/error page detected: '{title}' - skipping content analysis")
            return None

        weights = self.scoring['layer3_weights']

        # Build weighted text segments
        segments = {
            'title': (content.get('title', '') or '').lower(),
            'meta_description': (content.get('meta_description', '') or '').lower(),
            'h1': ' '.join(content.get('headings', {}).get('h1', [])).lower(),
            'h2': ' '.join(content.get('headings', {}).get('h2', [])).lower(),
            'body_text': (content.get('body_text', '') or '')[:5000].lower(),
            'domain': domain.lower() if domain else ''
        }

        # Score each category
        category_scores = {}
        category_signals = {}

        for cat_id, cat_data in self.categories.items():
            score, signals = self._score_category_content(cat_id, cat_data, segments, weights, content)
            category_scores[cat_id] = score
            category_signals[cat_id] = signals

        # Get best and secondary match
        best_cat, best_score, secondary_cat, secondary_score = self._get_top_two(category_scores)

        if not best_cat or best_score < self.scoring['confidence_thresholds']['low']:
            return None

        cat = self.categories[best_cat]
        return {
            'category': best_cat,
            'category_group': cat['group'],
            'name_en': cat['name_en'],
            'name_he': cat['name_he'],
            'confidence': best_score,
            'matched_signals': category_signals.get(best_cat, []),
            'secondary_category': secondary_cat,
            'secondary_confidence': secondary_score,
            'all_scores': category_scores
        }

    def _score_category_content(self, cat_id: str, cat_data: Dict,
                                 segments: Dict, weights: Dict,
                                 content: Dict) -> Tuple[float, List[Dict]]:
        """
        Score how well content matches a category.

        Scoring logic:
        - Each keyword match earns points based on WHERE it was found (title=3x, h1=2.5x, body=1x)
        - Score depends on how many keywords matched, not the ratio vs total keywords
        - More matches = higher score, regardless of keyword list size
        - A keyword matching in multiple segments counts once (best segment weight)

        Returns (score, matched_signals).
        """
        signals = []

        # --- Keyword scoring (60% of total) ---
        all_keywords = cat_data.get('keywords_en', []) + cat_data.get('keywords_he', [])
        if not all_keywords:
            return 0.0, []

        # Track best match per keyword (avoid counting same keyword multiple times)
        keyword_best_match = {}  # keyword -> (best_segment_weight, segment_name)

        for keyword in all_keywords:
            kw = keyword.lower()
            pattern = r'\b' + re.escape(kw) + r'\b'

            for segment_name, text in segments.items():
                if not text:
                    continue
                segment_weight = weights.get(segment_name, 1.0)

                matched = False
                try:
                    matched = bool(re.search(pattern, text))
                except re.error:
                    matched = kw in text

                if matched:
                    # Keep the best (highest weight) segment match for this keyword
                    if kw not in keyword_best_match or segment_weight > keyword_best_match[kw][0]:
                        keyword_best_match[kw] = (segment_weight, segment_name)

        # Calculate score from unique keyword matches
        if not keyword_best_match:
            keyword_score = 0.0
        else:
            # Score = sum of match weights, normalized
            # - Each match contributes its segment weight (title=3.0, h1=2.5, body=1.0)
            # - We normalize by a fixed target: 3 strong matches (e.g. title+h1+meta = ~7.5)
            #   should give ~1.0 confidence
            total_match_weight = sum(w for w, _ in keyword_best_match.values())
            match_count = len(keyword_best_match)

            # Two factors: how strong the matches are + how many there are
            # Strength: total weight / normalization factor
            strength = min(total_match_weight / 7.5, 1.0)
            # Breadth: number of unique matches (3+ matches = full breadth score)
            breadth = min(match_count / 3.0, 1.0)

            # Combine: both matter, but breadth (number of matches) matters more
            keyword_score = strength * 0.4 + breadth * 0.6

            # Build signals for matched keywords
            for kw, (seg_weight, seg_name) in keyword_best_match.items():
                # Find original casing
                original = next((k for k in all_keywords if k.lower() == kw), kw)
                signals.append({
                    'type': 'keyword',
                    'value': original,
                    'weight': round(seg_weight, 3),
                    'segment': seg_name
                })

        # --- Structure scoring (25% of total) ---
        structure_score = 0.0
        expected_signals = cat_data.get('structure_signals', [])

        if expected_signals:
            form_types = content.get('form_types', [])
            cta_count = content.get('cta_count', 0)
            has_forms = content.get('has_forms', False) or len(form_types) > 0

            if 'login_form' in expected_signals and has_forms:
                structure_score += 0.3
                signals.append({'type': 'structure', 'value': 'login_form_detected', 'weight': 0.3})
            if 'payment_form' in expected_signals and has_forms:
                structure_score += 0.3
                signals.append({'type': 'structure', 'value': 'payment_form_detected', 'weight': 0.3})
            if 'shopping_cart' in expected_signals and cta_count > 3:
                structure_score += 0.3
                signals.append({'type': 'structure', 'value': 'high_cta_count', 'weight': 0.3})
            if 'product_listing' in expected_signals and cta_count > 5:
                structure_score += 0.3
                signals.append({'type': 'structure', 'value': 'product_listing_pattern', 'weight': 0.3})
            if 'video_player' in expected_signals:
                body = (content.get('body_text', '') or '').lower()
                if 'video' in body or 'player' in body or 'watch' in body:
                    structure_score += 0.3
                    signals.append({'type': 'structure', 'value': 'video_content_detected', 'weight': 0.3})

            structure_score = min(structure_score, 1.0)

        # --- Meta scoring (15% of total) ---
        meta_score = 0.0
        meta_desc = (content.get('meta_description', '') or '').lower()
        title = (content.get('title', '') or '').lower()

        name_en = cat_data['name_en'].lower()
        if name_en in meta_desc or name_en in title:
            meta_score = 0.5
            signals.append({'type': 'meta_tag', 'value': f'name_in_meta: {name_en}', 'weight': 0.5})

        group = cat_data['group']
        if group in meta_desc:
            meta_score = max(meta_score, 0.3)

        meta_score = min(meta_score, 1.0)

        # --- Combine component scores ---
        component_weights = self.scoring['layer3_component_weights']
        final_score = (
            keyword_score * component_weights['keyword_score'] +
            structure_score * component_weights['structure_score'] +
            meta_score * component_weights['meta_score']
        )

        return round(final_score, 4), signals

    def _get_top_two(self, scores: Dict[str, float]) -> Tuple[str, float, Optional[str], float]:
        """Get top two scoring categories."""
        if not scores:
            return None, 0.0, None, 0.0

        sorted_cats = sorted(scores.items(), key=lambda x: x[1], reverse=True)

        best_cat = sorted_cats[0][0]
        best_score = sorted_cats[0][1]

        secondary_cat = None
        secondary_score = 0.0
        if len(sorted_cats) > 1 and sorted_cats[1][1] > self.scoring['confidence_thresholds']['low']:
            secondary_cat = sorted_cats[1][0]
            secondary_score = sorted_cats[1][1]

        return best_cat, best_score, secondary_cat, secondary_score

    # -------------------------------------------------------------------------
    # Layer Combination
    # -------------------------------------------------------------------------

    def _combine_layers(self, layer2: Optional[Dict], layer3: Optional[Dict]) -> Dict:
        """
        Combine Layer 2 (domain) and Layer 3 (content) results.

        Rules:
        - Both agree: highest confidence, method = "domain_pattern+content_analysis"
        - Both disagree: trust domain unless content is very strong, flag mismatch
        - Only one matched: use that one
        - Neither matched: unknown
        """
        # Neither matched
        if not layer2 and not layer3:
            return self._make_result('unknown', 'unknown', 'Unknown', 'לא ידוע',
                                     0.0, 'none', [], None, 0.0, {})

        # Only Layer 2 (domain) matched
        if layer2 and not layer3:
            return self._make_result(
                layer2['category'], layer2['category_group'],
                layer2['name_en'], layer2['name_he'],
                layer2['confidence'], 'domain_pattern',
                layer2['matched_signals'], None, 0.0, {}
            )

        # Only Layer 3 (content) matched
        if not layer2 and layer3:
            return self._make_result(
                layer3['category'], layer3['category_group'],
                layer3['name_en'], layer3['name_he'],
                layer3['confidence'], 'content_analysis',
                layer3['matched_signals'],
                layer3.get('secondary_category'),
                layer3.get('secondary_confidence', 0.0),
                layer3.get('all_scores', {})
            )

        # Both layers matched - check agreement
        all_signals = layer2['matched_signals'] + layer3['matched_signals']

        same_category = layer2['category'] == layer3['category']
        same_group = layer2['category_group'] == layer3['category_group']

        if same_category or same_group:
            # Layers agree - use domain category with boosted confidence
            confidence = max(layer2['confidence'], layer3['confidence'])
            if same_category:
                confidence = min(confidence * 1.1, 1.0)  # Small boost for exact agreement

            return self._make_result(
                layer2['category'], layer2['category_group'],
                layer2['name_en'], layer2['name_he'],
                round(confidence, 4), 'domain_pattern+content_analysis',
                all_signals,
                layer3.get('secondary_category'),
                layer3.get('secondary_confidence', 0.0),
                layer3.get('all_scores', {})
            )

        # Layers disagree - potential phishing signal
        self.logger.warning(
            f"Domain/content mismatch: domain={layer2['category']}, content={layer3['category']}"
        )

        # Trust content if it's very confident and domain is weak
        if layer3['confidence'] > 0.7 and layer2['confidence'] <= 0.80:
            signals = all_signals + [{
                'type': 'warning',
                'value': 'domain_content_mismatch',
                'weight': 0.0
            }]
            return self._make_result(
                layer3['category'], layer3['category_group'],
                layer3['name_en'], layer3['name_he'],
                layer3['confidence'], 'content_analysis',
                signals,
                layer2['category'],  # domain category becomes secondary
                layer2['confidence'],
                layer3.get('all_scores', {})
            )

        # Otherwise trust domain, add mismatch warning
        signals = all_signals + [{
            'type': 'warning',
            'value': 'domain_content_mismatch',
            'weight': 0.0
        }]
        return self._make_result(
            layer2['category'], layer2['category_group'],
            layer2['name_en'], layer2['name_he'],
            layer2['confidence'], 'domain_pattern',
            signals,
            layer3['category'],
            layer3['confidence'],
            layer3.get('all_scores', {})
        )

    def _make_result(self, category: str, group: str, name_en: str, name_he: str,
                     confidence: float, method: str, signals: List[Dict],
                     secondary_cat: Optional[str], secondary_conf: float,
                     all_scores: Dict) -> Dict:
        """Build standardized result dict."""
        return {
            'success': True,
            'category': category,
            'category_group': group,
            'name_en': name_en,
            'name_he': name_he,
            'confidence': confidence,
            'detection_method': method,
            'matched_signals': signals,
            'secondary_category': secondary_cat,
            'secondary_confidence': secondary_conf,
            'all_scores': all_scores,
            'error': ''
        }

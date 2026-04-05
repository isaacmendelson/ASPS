# Enhanced Website Category Classifier

## Overview

The Category Classifier identifies what type of website the user is visiting (bank, crypto exchange, government site, pet store, etc.) in real-time. It is part of the ASPS URL Scam Analyzer and is designed to work during AnyDesk sessions - if a user is browsing a crypto exchange while on a remote session, the system can flag it.

## Architecture

```
URL arrives
    |
    v
Layer 2: Domain Pattern Matching
    Check TLD/suffix: .gov.il -> Government, .bank -> Banking, .casino -> Gambling
    If match: confidence 0.80 - 0.90
    |
    v
Layer 3: Content Analysis
    Analyze title, headings, meta tags, body text
    58 categories, weighted keyword scoring
    If match: confidence based on matches
    |
    v
Combine Layers
    Both agree -> high confidence
    Disagree -> flag domain_content_mismatch (phishing signal)
    Only one matched -> use that one
    Neither -> unknown
    |
    v
Output JSON with category, confidence, detection_method, matched_signals
```

## Categories (58 total)

### Financial (8)
`banking` `credit_union` `insurance` `investment` `stock_trading` `crypto_exchange` `payment_service` `lending`

### Shopping (7)
`ecommerce` `marketplace` `auction` `classifieds` `grocery` `fashion` `electronics`

### Government (6)
`government` `municipality` `military` `court` `tax_authority` `public_service`

### Health (5)
`hospital` `clinic` `pharmacy` `telehealth` `mental_health`

### Education (5)
`university` `school` `online_course` `elearning` `language_learning`

### Entertainment (5)
`streaming` `gaming` `gambling` `sports_betting` `adult_content`

### Media (5)
`news` `blog` `forum` `social_network` `messaging`

### Services (8)
`legal` `accounting` `real_estate` `travel` `job_board` `review_directory` `ride_delivery`

### Technology (5)
`saas` `cloud` `web_hosting` `vpn_proxy` `developer_tools`

### Other (5)
`restaurant` `automotive` `pets` `nonprofit` `religious`

## Detection Methods

### Layer 2: Domain Pattern Matching

Checks the URL's TLD (Top Level Domain) and suffix:

| Pattern | Category | Confidence |
|---------|----------|------------|
| `.bank` | banking | 0.90 |
| `.casino` | gambling | 0.90 |
| `.pharmacy` | pharmacy | 0.90 |
| `.gov.il` | government | 0.85 |
| `.ac.il` | university | 0.85 |
| `.muni.il` | municipality | 0.85 |
| `.gov` | government | 0.80 |
| `.mil` | military | 0.80 |
| `.edu` | university | 0.80 |

Full list in `config/category_patterns.json` under `domain_patterns`.

### Layer 3: Content Analysis

Analyzes the page content with positional weighting:

| Content Location | Weight |
|-----------------|--------|
| Title | 3.0x |
| H1 headings | 2.5x |
| Meta description | 2.0x |
| Domain name | 2.0x |
| H2 headings | 1.5x |
| Body text | 1.0x |

Scoring formula:
- **Strength (40%)**: How important the matched locations are (title match = more weight)
- **Breadth (60%)**: How many unique keywords matched (3+ = full score)
- Structure signals (25%): Forms, CTA buttons, page structure
- Meta signals (15%): Category name in meta tags

### Layer Combination

| Scenario | Result |
|----------|--------|
| Both layers agree | Highest confidence, method = `domain_pattern+content_analysis` |
| Both layers disagree | Flag `domain_content_mismatch` warning (phishing signal) |
| Only domain matched | Trust domain, method = `domain_pattern` |
| Only content matched | Trust content, method = `content_analysis` |
| Neither matched | `unknown`, confidence = 0 |

### Blocked Page Detection

If the page title contains "Access Denied", "403", "Captcha", etc., content analysis is skipped to avoid false classifications.

### Minimum Confidence Threshold

Results below 0.25 confidence are returned as `unknown` instead of a low-confidence guess.

## Output Schema

```json
{
    "success": true,
    "category": "crypto_exchange",
    "category_group": "financial",
    "name_en": "Crypto Exchange",
    "name_he": "בורסת קריפטו",
    "confidence": 0.60,
    "detection_method": "content_analysis",
    "matched_signals": [
        {"type": "keyword", "value": "crypto", "weight": 3.0, "segment": "title"},
        {"type": "keyword", "value": "bitcoin", "weight": 1.0, "segment": "body_text"},
        {"type": "keyword", "value": "exchange", "weight": 2.5, "segment": "h1"},
        {"type": "domain_tld", "value": ".bank", "weight": 0.90},
        {"type": "structure", "value": "login_form_detected", "weight": 0.3},
        {"type": "meta_tag", "value": "name_in_meta: banking", "weight": 0.5},
        {"type": "warning", "value": "domain_content_mismatch", "weight": 0.0}
    ],
    "secondary_category": "banking",
    "secondary_confidence": 0.52,
    "all_scores": {"banking": 0.52, "crypto_exchange": 0.60, ...},
    "error": ""
}
```

### Signal Types

| Type | Meaning |
|------|---------|
| `keyword` | A keyword from the category list matched in the content |
| `domain_tld` | The domain TLD/suffix matched a known pattern |
| `structure` | Page structure (forms, CTAs) matched expected patterns |
| `meta_tag` | Category name found in meta tags |
| `warning` | `domain_content_mismatch` - domain says one thing, content says another |

## Files

| File | Description |
|------|-------------|
| `config/category_patterns.json` | All 58 categories with keywords (EN+HE), domain patterns, scoring config |
| `core/category_classifier.py` | The classifier - Layer 2 + Layer 3 logic |
| `core/analyzer.py` | Integration - passes domain, maps categories to Backend enum |
| `scrapers/playwright_scraper.py` | Web scraper with anti-detection and retry |
| `config/settings.json` | Scraper settings (User-Agent, timeouts) |
| `tests/test_category_classifier.py` | 40 unit tests |
| `tests/batch_test_categories.py` | 100-site batch test |

## Installation & Dependencies

### Python Dependencies (already in requirements.txt)

```
playwright>=1.40
beautifulsoup4>=4.12
scikit-learn>=1.3
python-whois>=0.8
validators>=0.22
```

### Install Playwright Browser

```bash
cd Analyzers/basic-url-analyzer
pip install -r requirements.txt
playwright install chromium
```

## Commands

### Run Unit Tests

```bash
cd Analyzers/basic-url-analyzer
python -m pytest tests/test_category_classifier.py -v
```

### Run Batch Test (100 real websites)

```bash
cd Analyzers/basic-url-analyzer
python tests/batch_test_categories.py
```

Results saved to `reports/category_batch_results.json`.

### Classify a Single URL (Python)

```python
from scrapers.playwright_scraper import PlaywrightScraper
from core.content_extractor import ContentExtractor
from core.category_classifier import CategoryClassifier

scraper = PlaywrightScraper()
extractor = ContentExtractor()
classifier = CategoryClassifier()

scrape_result = scraper.fetch('https://www.coinmama.com/')
content = extractor.extract(scrape_result['html'], scrape_result['url'])
result = classifier.classify(content, 'www.coinmama.com')

print(result['category'])        # crypto_exchange
print(result['confidence'])      # 0.60
print(result['matched_signals']) # [{type: keyword, value: crypto, ...}]
```

### Classify Without Scraping (content already available)

```python
from core.category_classifier import CategoryClassifier

classifier = CategoryClassifier()
content = {
    'title': 'Bank of America - Banking, Credit Cards, Loans',
    'body_text': 'Online banking, savings account, checking...',
    'meta_description': 'Bank of America financial services',
    'headings': {'h1': ['Personal Banking'], 'h2': ['Savings', 'Checking']},
    'cta_count': 5,
    'has_forms': True,
}
result = classifier.classify(content, 'www.bankofamerica.com')
```

## Test Results

Tested on 100 real websites across all categories:

| Metric | Value |
|--------|-------|
| Total sites | 100 |
| Correct | 66 |
| Close/debatable | 13 |
| Actually wrong | 7 |
| Blocked (no content) | 14 |
| **Real accuracy** | **90.4%** |

## Adding New Categories

1. Add the category to `config/category_patterns.json` under `categories`:

```json
"new_category": {
    "name_en": "Category Name",
    "name_he": "שם הקטגוריה",
    "group": "group_name",
    "keywords_en": ["keyword1", "keyword2", ...],
    "keywords_he": ["מילה1", "מילה2", ...],
    "structure_signals": []
}
```

2. Add the mapping in `core/analyzer.py` in `_CATEGORY_TO_WEBSITE_TYPE`:

```python
'new_category': 'BackendEnumValue',
```

3. Add tests in `tests/test_category_classifier.py`.

## Adding New Domain Patterns

Add to `config/category_patterns.json` under `domain_patterns`:

```json
"country_specific": {
    "patterns": {
        ".new.suffix": "category_id"
    }
}
```

## Adding New Languages

Add keywords to the category in `category_patterns.json`:

```json
"banking": {
    "keywords_en": [...],
    "keywords_he": [...],
    "keywords_ar": ["بنك", "حساب", ...],  // Add new language
    "keywords_ru": ["банк", "счет", ...]
}
```

Then update `_score_category_content` in `category_classifier.py` to include the new language field:

```python
all_keywords = (cat_data.get('keywords_en', []) +
                cat_data.get('keywords_he', []) +
                cat_data.get('keywords_ar', []) +
                cat_data.get('keywords_ru', []))
```

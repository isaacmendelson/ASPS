# Scam Analyzer

Comprehensive URL scam detection tool that analyzes websites for fraud indicators using WHOIS data, content analysis, and pattern matching.

## Features

- **WHOIS Analysis** - Domain age, registrar, privacy protection
- **Content Extraction** - HTML parsing and text analysis
- **Pattern Matching** - 30+ scam detection patterns
- **ML Classifier (Optional)** - Machine learning for subtle scams
- **Purpose Classification** - Identifies scam type (investment, phishing, etc.)
- **Modular Architecture** - Easy to extend and customize
- **24-Hour Caching** - Speeds up repeated analysis
- **CLI Interface** - Simple command-line usage
- **Training Interface** - Train ML on your own data

## Installation

### 1. Install Dependencies

```bash
cd scam_analyzer
pip install -r requirements.txt
```

### 2. Install Playwright Browsers

```bash
playwright install chromium
```

That's it! You're ready to use the tool.

## Usage

### Basic Analysis

```bash
python analyze.py https://example.com
```

### Verbose Mode (Detailed Information)

```bash
python analyze.py https://scam-site.com --verbose
```

### JSON Output

```bash
python analyze.py https://example.com --json
```

### Clear Cache

```bash
python analyze.py --clear-cache
```

### Use Machine Learning (Optional)

```bash
# First, train the model
python train_model.py

# Then analyze with ML
python analyze.py https://example.com --use-ml
```

### Disable Cache

```bash
python analyze.py https://example.com --no-cache
```

## Examples

### Example 1: High Risk Site

```bash
python analyze.py https://make-money-fast.com
```

**Output:**
```
==================================================
SCAM ANALYSIS REPORT
==================================================
URL: https://make-money-fast.com
Analyzed: 2024-12-20 15:30:45

RISK ASSESSMENT:
  Risk Score: 85/100
  Risk Level: HIGH
  Is Scam: YES

RED FLAGS DETECTED (7):
  - Very new domain (35 days old)
  - Privacy protection enabled
  - Promises guaranteed returns
  - Unrealistic ROI claims
  ...

RECOMMENDATION:
  HIGH RISK - Do NOT engage!
==================================================
```

### Example 2: Legitimate Site

```bash
python analyze.py https://amazon.com
```

**Output:**
```
RISK ASSESSMENT:
  Risk Score: 5/100
  Risk Level: LOW
  Is Scam: NO

WHOIS INFORMATION:
  Domain Age: 9847 days (27 years)
  Privacy Protected: No

RECOMMENDATION:
  LOW RISK - Appears legitimate
```

## Architecture

```
scam_analyzer/
├── core/                   # Core analysis modules
│   ├── analyzer.py        # Main orchestrator
│   ├── whois_checker.py   # WHOIS analysis
│   ├── content_extractor.py
│   ├── rules_engine.py
│   └── purpose_classifier.py
├── scrapers/              # Web scraping (modular)
│   ├── base_scraper.py
│   └── playwright_scraper.py
├── utils/                 # Utilities
│   ├── cache_manager.py
│   ├── validators.py
│   └── logger.py
├── config/                # Configuration
│   ├── settings.json
│   └── patterns.json
└── analyze.py            # CLI entry point
```

## Detection Patterns

The tool detects 30+ scam patterns including:

### Financial Scams
- Guaranteed returns (100% profit)
- Unrealistic ROI (500%+)
- Get rich quick schemes
- Small to massive returns ($100 → $1M)

### Psychological Tactics
- Urgency pressure ("Act now!")
- Artificial scarcity ("Only 3 spots left")
- No effort required claims
- Secret methods

### Structural Indicators
- Very new domains (< 30 days)
- Privacy-protected WHOIS
- Excessive call-to-action buttons
- Payment forms on landing pages
- URL shorteners

## Configuration

### Settings (`config/settings.json`)

```json
{
    "cache": {
        "ttl_hours": 24
    },
    "scraping": {
        "timeout_seconds": 30
    },
    "scoring": {
        "thresholds": {
            "low": 30,
            "medium": 60,
            "high": 61
        }
    }
}
```

### Patterns (`config/patterns.json`)

Add or modify detection patterns:

```json
{
    "content_patterns": {
        "custom_pattern": {
            "regex": "your_regex_here",
            "weight": 0.25,
            "description": "Pattern description"
        }
    }
}
```

## Machine Learning (Optional)

The tool includes an optional ML classifier for improved accuracy.

**Quick Start:**
```bash
# Train model
python train_model.py

# Use ML
python analyze.py https://example.com --use-ml
```

**See full guide:** [ML_TRAINING_GUIDE.md](ML_TRAINING_GUIDE.md)

**Benefits:**
- +30% detection on subtle scams
- Learns from YOUR data
- Catches variations regex misses
- Easy to train and customize

## API Usage (Python)

```python
from core.analyzer import ScamAnalyzer

# Initialize
analyzer = ScamAnalyzer()

# Analyze URL
result = analyzer.analyze_url("https://example.com")

# Access results
print(f"Risk Score: {result['risk_assessment']['risk_score']}")
print(f"Is Scam: {result['risk_assessment']['is_scam']}")
print(f"Category: {result['purpose']['category']}")
```

## Future Enhancements

The tool is designed to be modular for easy expansion:

- [ ] **Advanced Bot Detection Bypass** - Stealth mode for Cloudflare
- [ ] **ML Model Integration** - Fine-tuned transformer models
- [ ] **Multi-language Support** - Hebrew, Spanish, etc.
- [ ] **Image Analysis** - Logo detection, fake testimonials
- [ ] **Historical Tracking** - Monitor domain changes over time

## Troubleshooting

### "Timeout - page took too long to load"
- The site is slow or blocking automated access
- Increase timeout in `config/settings.json`
- Future: Use stealth scraper

### "WHOIS lookup failed"
- Domain WHOIS is restricted
- Analysis continues with partial data
- Check `warnings` in result

### Playwright installation issues
```bash
# Reinstall Playwright
pip install playwright --force-reinstall
playwright install chromium
```

## Exit Codes

- `0` - Success, low risk
- `1` - Medium risk or error
- `2` - High risk detected
- `130` - Interrupted by user

## License

MIT License - Free to use and modify

## Credits

Created as part of the AntiScam fraud protection platform.

---

**Need help?** Open an issue or check the verbose output with `-v` flag.

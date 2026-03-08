# Scam URL Analyzer - Setup Guide

## What is this?

A tool that analyzes URLs to detect scams. It checks:
- **WHOIS data** - Domain age, registrar, country
- **Content analysis** - Scam patterns, urgency tactics, fake promises
- **ML classification** - Machine learning model trained on scam sites
- **Website category** - What type of site (news, bank, store, etc.)
- **Reputation** - Web search to check if domain is well-known

## Requirements

- Python 3.10+
- Windows/Mac/Linux

## Quick Start

### 1. Create virtual environment

```bash
python -m venv venv
```

### 2. Activate virtual environment

**Windows (PowerShell):**
```powershell
.\venv\Scripts\Activate.ps1
```

**Windows (CMD):**
```cmd
venv\Scripts\activate.bat
```

**Mac/Linux:**
```bash
source venv/bin/activate
```

### 3. Install dependencies

```bash
pip install -r requirements.txt
```

### 4. Install browser for web scraping

**IMPORTANT:** The analyzer uses Playwright to render JavaScript-heavy websites.
You must install a browser:

```bash
playwright install chromium
```

This downloads Chromium browser (~150MB). Only needed once.

### 5. Run analysis

```bash
python analyze.py "https://example.com"
```

## Usage Examples

### Basic analysis
```bash
python analyze.py "https://example.com"
```

### Verbose mode (detailed output)
```bash
python analyze.py "https://example.com" --verbose
```

### JSON output
```bash
python analyze.py "https://example.com" --json
```

### Skip ML classifier
```bash
python analyze.py "https://example.com" --no-ml
```

### Show ML explanation (which words triggered detection)
```bash
python analyze.py "https://example.com" --explain
```

### Clear cache
```bash
python analyze.py --clear-cache
```

## Output Explained

### Risk Score (0-100)
- **0-29** = HIGH RISK (likely scam)
- **30-60** = MEDIUM RISK (suspicious)
- **61-100** = LOW RISK (appears safe)

Note: Score is "safety score" - higher is safer.

### Risk Levels
- **HIGH** - Strong scam indicators. Do NOT engage.
- **MEDIUM** - Some suspicious signs. Verify before proceeding.
- **LOW** - Few or no scam indicators. Appears legitimate.

### Website Categories
The analyzer identifies what type of site:
- News & Media
- Finance & Banking
- E-commerce & Shopping
- Technology
- Healthcare & Medical
- Education
- Government
- Entertainment
- Food & Restaurant
- Pets & Animals
- Real Estate
- Travel & Tourism
- Sports & Fitness
- And more...

## Optional: LLM Explanations

For AI-generated explanations, install Ollama:

1. Download from https://ollama.com
2. Run: `ollama pull phi3`
3. Enable in `config/settings.json`: `"ollama": { "enabled": true }`

## Project Structure

```
basic-url-analyzer/
├── analyze.py          # Main CLI entry point
├── config/
│   ├── settings.json   # Configuration
│   └── patterns.json   # Scam detection patterns
├── core/
│   ├── analyzer.py     # Main orchestrator
│   ├── whois_checker.py
│   ├── content_extractor.py
│   ├── rules_engine.py
│   ├── ml_classifier.py
│   ├── category_classifier.py
│   ├── reputation_checker.py
│   └── llm_explainer.py
├── scrapers/
│   └── playwright_scraper.py
├── models/
│   └── scam_classifier.pkl  # Trained ML model
├── training_data/
│   └── *.jsonl         # Training samples
└── utils/
    ├── logger.py
    ├── validators.py
    └── cache_manager.py
```

## Troubleshooting

### "Module not found" error
Make sure virtual environment is activated:
```bash
# Check if (venv) appears in prompt
# If not, activate it again
```

### Playwright / Browser error
If you see errors like "Browser not found" or "Executable doesn't exist":
```bash
playwright install chromium
```
This installs the Chromium browser needed for web scraping.

### WHOIS lookup fails
Some domains block WHOIS queries. This is logged as a warning but analysis continues.

### Slow analysis
First run is slower (loading models). Subsequent runs use cache (24h).
Disable cache with `--no-cache` for fresh analysis.

#!/usr/bin/env python3
"""
Scam Analyzer CLI
Command-line interface for URL scam analysis
"""

import sys
import argparse
import json
import logging
import textwrap
from pathlib import Path

from core.analyzer import ScamAnalyzer
from utils.cache_manager import CacheManager

logger = logging.getLogger(__name__)


def load_config():
    """Load configuration from settings.json."""
    config_path = Path(__file__).parent / 'config' / 'settings.json'
    try:
        with open(config_path, 'r') as f:
            return json.load(f)
    except FileNotFoundError:
        logger.debug("settings.json not found at %s; using defaults", config_path)
        return {}
    except (json.JSONDecodeError, OSError) as exc:
        logger.warning("Could not load settings.json: %s; using defaults", exc)
        return {}


def print_banner():
    """Print tool banner."""
    print("=" * 60)
    print("SCAM ANALYZER")
    print("URL Scam Detection Tool")
    print("=" * 60)
    print()


def print_result(result: dict, verbose: bool = False):
    """Print analysis result in formatted way.

    Args:
        result: Analysis result dictionary.
        verbose: Show detailed information.
    """
    print("=" * 60)
    print("SCAM ANALYSIS REPORT")
    print("=" * 60)
    print(f"URL: {result['url']}")
    print(f"Analyzed: {result['analyzed_at']}")
    print(f"Analysis Time: {result.get('analysis_time_ms', 0) / 1000:.1f} seconds")

    if result.get('from_cache'):
        print("Source: CACHE (24h)")

    print()

    # Error (invalid URL, browser internal URL, etc.)
    if result.get('error'):
        print(f"[ERROR]: {result['error']}")
        print()

    # Warnings
    if result.get('warnings'):
        print("[WARNING]: Incomplete analysis")
        for warning in result['warnings']:
            print(f"   - {warning}")
        print()

    # Risk Assessment
    risk = result.get('risk_assessment', {})
    print("RISK ASSESSMENT:")

    risk_level = risk.get('risk_level', 'UNKNOWN')
    risk_score = risk.get('risk_score', 0)

    risk_indicator = {
        'HIGH': '[!!!]',
        'MEDIUM': '[!!]',
        'LOW': '[OK]',
        'UNKNOWN': '[?]',
    }

    print(f"  Risk Score: {risk_score}/100")
    print(f"  Risk Level: {risk_indicator.get(risk_level, '[?]')} {risk_level}")
    print(f"  Is Scam: {'YES' if risk.get('is_scam') else 'NO'}")
    print(f"  Confidence: {risk.get('confidence', 0)*100:.0f}%")
    print()

    # Website Category
    website_cat = result.get('website_category', {})
    if website_cat.get('category') and website_cat.get('category') != 'unknown':
        print("WEBSITE CATEGORY:")
        print(f"  Type: {website_cat.get('name_en', 'Unknown')}")
        confidence_pct = website_cat.get('confidence', 0) * 100
        print(f"  Confidence: {confidence_pct:.0f}%")
        print()

    # Reputation (if well-known)
    reputation = result.get('reputation', {})
    if reputation.get('is_well_known'):
        print("REPUTATION:")
        print(f"  Well-Known: Yes ({reputation.get('reputable_mentions', 0)} reputable mentions)")
        if reputation.get('mention_sources'):
            sources = ', '.join(reputation.get('mention_sources', [])[:3])
            print(f"  Mentioned by: {sources}")
        print()

    # LLM Explanation (after risk assessment, before technical details)
    explanation = result.get('explanation', {})
    if explanation:
        print_explanation(explanation)

    # Purpose
    purpose = result.get('purpose', {})
    if purpose.get('category') != 'unknown':
        print("PURPOSE:")
        print(f"  Category: {purpose.get('category', 'unknown').replace('_', ' ').title()}")
        print(f"  Confidence: {purpose.get('confidence', 0)*100:.0f}%")
        print(f"  Description: {purpose.get('description', '')}")
        print()

    # WHOIS
    whois = result.get('whois', {})
    if whois.get('success'):
        print("WHOIS INFORMATION:")
        age_days = whois.get('domain_age_days', 0)

        if age_days < 30:
            age_warning = " (VERY NEW! ***)"
        elif age_days < 180:
            age_warning = " (NEW *)"
        else:
            age_warning = ""

        print(f"  Domain Age: {age_days} days{age_warning}")
        print(f"  Created: {whois.get('created_date', 'Unknown')}")
        print(f"  Registrar: {whois.get('registrar', 'Unknown')}")
        print(f"  Country: {whois.get('country', 'Unknown')}")
        print(f"  Privacy Protected: {'Yes' if whois.get('privacy_protected') else 'No'}")
        print()

    # Red Flags
    red_flags = result.get('red_flags', [])
    if red_flags:
        print(f"RED FLAGS DETECTED ({len(red_flags)}):")
        for flag in red_flags:
            print(f"  [X]{flag}")
        print()

    # Detailed patterns (verbose mode)
    if verbose:
        content = result.get('content_analysis', {})
        patterns = content.get('detected_patterns', [])

        if patterns:
            print("DETECTED PATTERNS (DETAILED):")
            for pattern in patterns:
                print(f"  [{pattern['type'].upper()}] {pattern['name']}")
                print(f"    Match: {pattern['matched_text']}")
                print(f"    Weight: {pattern['weight']}")
                print(f"    Description: {pattern['description']}")
                print()

        if content.get('success'):
            print("CONTENT STATISTICS:")
            print(f"  Title: {content.get('title', 'N/A')}")
            print(f"  Word Count: {content.get('word_count', 0)}")
            print(f"  CTA Buttons: {content.get('cta_count', 0)}")
            print(f"  Forms: {len(content.get('form_types', []))}")
            print()

        ml = result.get('ml_analysis', {})
        if ml.get('enabled'):
            print("ML ANALYSIS:")
            if ml.get('success'):
                print(f"  ML Score: {ml.get('score', 0)*100:.0f}/100")
                print(f"  ML Confidence: {ml.get('confidence', 0)*100:.0f}%")
            print(f"  Note: {ml.get('note', 'N/A')}")
            print()

    # Recommendation
    print("RECOMMENDATION:")
    recommendation = result.get('recommendation', 'Unknown')
    print(f"  {recommendation}")
    print()

    print("=" * 60)


def print_ml_explanation(explanation: dict):
    """Print ML explanation showing which features contributed to the prediction.

    Args:
        explanation: Explanation dictionary from MLClassifier.explain().
    """
    if not explanation.get('success'):
        print(f"\n[ERROR] ML Explanation failed: {explanation.get('error', 'Unknown error')}")
        return

    print()
    print("=" * 60)
    print("ML CLASSIFICATION EXPLANATION")
    print("=" * 60)

    is_scam = explanation.get('is_scam', False)
    confidence = explanation.get('confidence', 0) * 100
    print(f"Prediction: {'SCAM' if is_scam else 'SAFE'} ({confidence:.0f}% scam confidence)")
    print(f"Features matched: {explanation.get('total_features_matched', 0)}")
    print()

    scam_indicators = explanation.get('scam_indicators', [])
    if scam_indicators:
        print("[!] SCAM INDICATORS (pushing toward scam):")
        print("-" * 50)
        for i, indicator in enumerate(scam_indicators, 1):
            feature = indicator['feature']
            contribution = indicator['contribution']
            bars = "#" * min(int(contribution * 10), 10)
            print(f"  {i:2}. {feature:<25} {bars} ({contribution:.3f})")
        print()
    else:
        print("[!] SCAM INDICATORS: None detected")
        print()

    legit_signals = explanation.get('legitimate_signals', [])
    if legit_signals:
        print("[+] LEGITIMATE SIGNALS (pushing toward safe):")
        print("-" * 50)
        for i, signal in enumerate(legit_signals, 1):
            feature = signal['feature']
            contribution = abs(signal['contribution'])
            bars = "#" * min(int(contribution * 10), 10)
            print(f"  {i:2}. {feature:<25} {bars} ({contribution:.3f})")
        print()
    else:
        print("[+] LEGITIMATE SIGNALS: None detected")
        print()

    print("=" * 60)
    print("Note: Contribution = TF-IDF weight * model coefficient")
    print("Higher contribution = stronger influence on classification")
    print("=" * 60)


def _truncate_to_sentences(text: str, max_sentences: int = 3) -> tuple:
    """Truncate text to approximately max_sentences sentences.

    Args:
        text: Text to truncate.
        max_sentences: Maximum number of sentences.

    Returns:
        Tuple of (truncated_text, was_truncated).
    """
    sentences = []
    current = ""
    for char in text:
        current += char
        if char in '.!?' and current.strip():
            sentences.append(current.strip())
            current = ""
    if current.strip():
        sentences.append(current.strip())

    if len(sentences) <= max_sentences:
        return text, False

    truncated = ' '.join(sentences[:max_sentences])
    return truncated, True


def print_explanation(explanation: dict, width: int = 80):
    """Print LLM explanation in a formatted box.

    Args:
        explanation: Explanation dict with text, model, generated, error fields.
        width: Terminal width for word wrapping (default 80).
    """
    if not explanation.get('generated') and explanation.get('error') == 'Skipped by user':
        print("(explanation skipped)")
        print()
        return

    if not explanation.get('generated') and explanation.get('error'):
        print("(explanation unavailable - Ollama not running)")
        print()
        return

    text = explanation.get('text')
    if not text:
        return

    text, was_truncated = _truncate_to_sentences(text, max_sentences=3)
    if was_truncated:
        text += " (see JSON for full explanation)"

    try:
        border = "─" * width
        border.encode(sys.stdout.encoding or 'utf-8')
    except (UnicodeEncodeError, LookupError):
        border = "-" * width

    print("Analysis Summary")
    print(border)
    print(textwrap.fill(text, width=width))
    print(border)
    print()


def main():
    """Main CLI entry point."""
    parser = argparse.ArgumentParser(
        description='Analyze URLs for scam indicators',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python analyze.py https://example.com
  python analyze.py https://scam-site.com --verbose
  python analyze.py https://example.com --explain
  python analyze.py https://example.com --explain --explain-top 5
  python analyze.py --clear-cache
  python analyze.py https://example.com --json
        """,
    )

    parser.add_argument('url', nargs='?', help='URL to analyze')
    parser.add_argument('-v', '--verbose', action='store_true', help='Show detailed analysis')
    parser.add_argument('--json', action='store_true', help='Output result as JSON')
    parser.add_argument('--no-cache', action='store_true', help='Disable cache lookup')
    parser.add_argument('--no-ml', action='store_true', help='Disable ML classifier')
    parser.add_argument('--clear-cache', action='store_true', help='Clear all cached results')
    parser.add_argument(
        '--explain',
        action='store_true',
        help='Show which words/phrases triggered the ML classification',
    )
    parser.add_argument(
        '--explain-top',
        type=int,
        default=10,
        help='Number of top features to show in explanation (default: 10)',
    )
    parser.add_argument(
        '--no-explain',
        action='store_true',
        help='Skip LLM explanation generation (faster execution)',
    )
    parser.add_argument(
        '--contract-version',
        choices=('1',),
        help='Read one messaging envelope from stdin and write one envelope to stdout',
    )

    args = parser.parse_args()

    if args.contract_version == '1':
        from v1_stdio import main as v1_main
        return v1_main()

    # Clear cache command
    if args.clear_cache:
        cache = CacheManager()
        cache.clear()
        print("[OK] Cache cleared successfully")
        return 0

    # URL is required for analysis
    if not args.url:
        parser.print_help()
        return 1

    if not args.json:
        print_banner()

    try:
        config = load_config()
        cache_enabled = config.get('cache', {}).get('enabled', True)
        use_cache = cache_enabled and not args.no_cache

        analyzer = ScamAnalyzer(use_cache=use_cache, use_ml=not args.no_ml, no_explain=args.no_explain)
        result = analyzer.analyze_url(args.url)

        if args.json:
            print(json.dumps(result, indent=2))
        else:
            print_result(result, verbose=args.verbose)

        # ML Explanation (if requested)
        if args.explain and not args.no_ml:
            body_text = result.get('ml_analysis', {}).get('body_text', '')
            if body_text:
                explanation = analyzer.explain_ml_prediction(body_text, args.explain_top)
                if args.json:
                    print(json.dumps({'ml_explanation': explanation}, indent=2))
                else:
                    print_ml_explanation(explanation)
            else:
                if not args.json:
                    print("\n[ERROR] Cannot explain: No content was extracted from the URL")

        # Exit code based on risk
        risk_level = result.get('risk_assessment', {}).get('risk_level', 'UNKNOWN')
        if risk_level == 'HIGH':
            return 2
        if risk_level == 'MEDIUM':
            return 1
        return 0

    except KeyboardInterrupt:
        print("\n\n[ERROR] Analysis interrupted by user")
        return 130

    except Exception as exc:
        logger.error("Unhandled CLI error: %s", exc, exc_info=True)
        print(f"\n[ERROR] ERROR: {exc}", file=sys.stderr)
        return 1


if __name__ == '__main__':
    sys.exit(main())

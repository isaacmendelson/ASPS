"""
Batch test: Run category classifier on 100 real websites across all categories.
Saves results to reports/category_batch_results.json and prints summary.
"""

import sys, io, json, time
from pathlib import Path

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))

from core.category_classifier import CategoryClassifier
from scrapers.playwright_scraper import PlaywrightScraper
from core.content_extractor import ContentExtractor

# 100 URLs across all category groups with expected categories
TEST_SITES = [
    # === FINANCIAL (15) ===
    ("https://www.bankofamerica.com/", "banking"),
    ("https://www.chase.com/", "banking"),
    ("https://www.wellsfargo.com/", "banking"),
    ("https://www.hsbc.com/", "banking"),
    ("https://www.goldmansachs.com/", "investment"),
    ("https://www.fidelity.com/", "investment"),
    ("https://www.etrade.com/", "stock_trading"),
    ("https://www.binance.com/", "crypto_exchange"),
    ("https://www.coinbase.com/", "crypto_exchange"),
    ("https://www.coinmama.com/", "crypto_exchange"),
    ("https://www.paypal.com/", "payment_service"),
    ("https://www.stripe.com/", "payment_service"),
    ("https://wise.com/", "payment_service"),
    ("https://www.progressive.com/", "insurance"),
    ("https://www.statefarm.com/", "insurance"),

    # === SHOPPING (10) ===
    ("https://www.amazon.com/", "ecommerce"),
    ("https://www.ebay.com/", "marketplace"),
    ("https://www.etsy.com/", "marketplace"),
    ("https://www.walmart.com/", "ecommerce"),
    ("https://www.bestbuy.com/", "electronics"),
    ("https://www.nike.com/", "fashion"),
    ("https://www.zara.com/", "fashion"),
    ("https://www.aliexpress.com/", "ecommerce"),
    ("https://www.target.com/", "ecommerce"),
    ("https://www.sephora.com/", "ecommerce"),

    # === GOVERNMENT (8) ===
    ("https://www.usa.gov/", "government"),
    ("https://www.gov.uk/", "government"),
    ("https://www.irs.gov/", "tax_authority"),
    ("https://www.nasa.gov/", "government"),
    ("https://www.cdc.gov/", "government"),
    ("https://www.ssa.gov/", "public_service"),
    ("https://www.state.gov/", "government"),
    ("https://www.army.mil/", "military"),

    # === HEALTH (7) ===
    ("https://www.mayoclinic.org/", "hospital"),
    ("https://www.webmd.com/", "clinic"),
    ("https://www.cvs.com/", "pharmacy"),
    ("https://www.walgreens.com/", "pharmacy"),
    ("https://www.teladoc.com/", "telehealth"),
    ("https://www.betterhelp.com/", "mental_health"),
    ("https://www.psychologytoday.com/", "mental_health"),

    # === EDUCATION (7) ===
    ("https://www.harvard.edu/", "university"),
    ("https://www.mit.edu/", "university"),
    ("https://www.stanford.edu/", "university"),
    ("https://www.coursera.org/", "online_course"),
    ("https://www.udemy.com/", "online_course"),
    ("https://www.khanacademy.org/", "elearning"),
    ("https://www.duolingo.com/", "elearning"),

    # === ENTERTAINMENT (8) ===
    ("https://www.netflix.com/", "streaming"),
    ("https://www.spotify.com/", "streaming"),
    ("https://www.twitch.tv/", "streaming"),
    ("https://store.steampowered.com/", "gaming"),
    ("https://www.xbox.com/", "gaming"),
    ("https://www.draftkings.com/", "sports_betting"),
    ("https://www.bet365.com/", "gambling"),
    ("https://www.imdb.com/", "streaming"),

    # === MEDIA (8) ===
    ("https://www.cnn.com/", "news"),
    ("https://www.bbc.com/", "news"),
    ("https://www.nytimes.com/", "news"),
    ("https://www.reuters.com/", "news"),
    ("https://www.reddit.com/", "forum"),
    ("https://www.medium.com/", "blog"),
    ("https://www.twitter.com/", "social_network"),
    ("https://www.linkedin.com/", "social_network"),

    # === SERVICES (8) ===
    ("https://www.zillow.com/", "real_estate"),
    ("https://www.realtor.com/", "real_estate"),
    ("https://www.booking.com/", "travel"),
    ("https://www.airbnb.com/", "travel"),
    ("https://www.expedia.com/", "travel"),
    ("https://www.indeed.com/", "job_board"),
    ("https://www.legalzoom.com/", "legal"),
    ("https://www.turbotax.com/", "accounting"),

    # === TECHNOLOGY (8) ===
    ("https://www.github.com/", "developer_tools"),
    ("https://www.salesforce.com/", "saas"),
    ("https://www.slack.com/", "saas"),
    ("https://www.cloudflare.com/", "cloud"),
    ("https://www.aws.amazon.com/", "cloud"),
    ("https://www.godaddy.com/", "web_hosting"),
    ("https://www.nordvpn.com/", "vpn_proxy"),
    ("https://www.zoom.us/", "saas"),

    # === OTHER (10) ===
    ("https://www.mcdonalds.com/", "restaurant"),
    ("https://www.dominos.com/", "restaurant"),
    ("https://www.honda.com/", "automotive"),
    ("https://www.toyota.com/", "automotive"),
    ("https://www.bmw.com/", "automotive"),
    ("https://www.ford.com/", "automotive"),
    ("https://www.petco.com/", "pets"),
    ("https://www.chewy.com/", "pets"),
    ("https://www.redcross.org/", "nonprofit"),
    ("https://www.wikipedia.org/", "elearning"),

    # === MIXED/EDGE CASES (11) ===
    ("https://www.revolut.com/", "banking"),
    ("https://www.robinhood.com/", "stock_trading"),
    ("https://www.shopify.com/", "saas"),
    ("https://www.squarespace.com/", "saas"),
    ("https://www.uber.com/", "saas"),
    ("https://www.doordash.com/", "restaurant"),
    ("https://www.grubhub.com/", "restaurant"),
    ("https://www.hulu.com/", "streaming"),
    ("https://www.craigslist.org/", "classifieds"),
    ("https://www.yelp.com/", "restaurant"),
    ("https://www.tripadvisor.com/", "travel"),
]


def run_batch_test():
    classifier = CategoryClassifier()
    scraper = PlaywrightScraper()
    extractor = ContentExtractor()

    results = []
    correct = 0
    wrong = 0
    errors = 0
    blocked = 0

    total = len(TEST_SITES)
    print(f"Running batch test on {total} websites...\n")

    for i, (url, expected) in enumerate(TEST_SITES, 1):
        domain = url.split("//")[1].rstrip("/")
        print(f"[{i}/{total}] {domain}...", end=" ", flush=True)

        try:
            scrape_result = scraper.fetch(url)
            content = extractor.extract(
                scrape_result.get('html', ''),
                scrape_result.get('url', url)
            )

            result = classifier.classify(content, domain)
            detected = result.get('category', 'unknown')
            confidence = result.get('confidence', 0)
            method = result.get('detection_method', 'none')

            # Check if page was blocked
            title = (content.get('title', '') or '').lower()
            page_blocked = any(b in title for b in [
                'access denied', '403', 'blocked', 'captcha',
                'robot', 'just a moment', 'checking'
            ])

            if page_blocked:
                status = "BLOCKED"
                blocked += 1
            elif detected == expected:
                status = "CORRECT"
                correct += 1
            elif result.get('secondary_category') == expected:
                status = "PARTIAL"
                correct += 1  # Count partial as correct
            else:
                status = "WRONG"
                wrong += 1

            print(f"{status} -> {detected} (expected: {expected}) conf={confidence:.2f} [{method}]")

            results.append({
                'url': url,
                'domain': domain,
                'expected': expected,
                'detected': detected,
                'confidence': confidence,
                'detection_method': method,
                'secondary': result.get('secondary_category'),
                'status': status,
                'signals_count': len(result.get('matched_signals', [])),
                'page_blocked': page_blocked
            })

        except Exception as e:
            print(f"ERROR: {str(e)[:80]}")
            errors += 1
            results.append({
                'url': url,
                'domain': domain,
                'expected': expected,
                'detected': 'error',
                'confidence': 0,
                'detection_method': 'none',
                'secondary': None,
                'status': 'ERROR',
                'signals_count': 0,
                'page_blocked': False,
                'error': str(e)[:200]
            })

    # Save results
    reports_dir = project_root / 'reports'
    reports_dir.mkdir(exist_ok=True)
    report_path = reports_dir / 'category_batch_results.json'
    with open(report_path, 'w', encoding='utf-8') as f:
        json.dump(results, f, indent=2, ensure_ascii=False)

    # Print summary
    testable = total - blocked - errors
    accuracy = (correct / testable * 100) if testable > 0 else 0

    print(f"\n{'='*60}")
    print(f"BATCH TEST RESULTS")
    print(f"{'='*60}")
    print(f"Total sites:     {total}")
    print(f"Correct:         {correct}")
    print(f"Wrong:           {wrong}")
    print(f"Blocked/Error:   {blocked + errors} (blocked={blocked}, errors={errors})")
    print(f"Testable:        {testable}")
    print(f"Accuracy:        {accuracy:.1f}%")
    print(f"\nResults saved to: {report_path}")

    # Show wrong ones
    wrong_results = [r for r in results if r['status'] == 'WRONG']
    if wrong_results:
        print(f"\n{'='*60}")
        print(f"WRONG CLASSIFICATIONS ({len(wrong_results)}):")
        print(f"{'='*60}")
        for r in wrong_results:
            print(f"  {r['domain']}")
            print(f"    Expected: {r['expected']}")
            print(f"    Got:      {r['detected']} (conf={r['confidence']:.2f}, secondary={r['secondary']})")
            print()


if __name__ == '__main__':
    run_batch_test()

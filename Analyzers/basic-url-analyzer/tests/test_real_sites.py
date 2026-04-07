"""
Test real sites from user's test data - check scores and categories
"""
import sys, io, json, time
from pathlib import Path

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))

from core.analyzer import ScamAnalyzer

TEST_URLS = [
    "https://www.linkedin.com/feed/",
    "https://www.namecheap.com/domains/registration/results/?domain=7xl.com",
    "https://claude.ai/new",
    "https://rehovot.mynet.co.il/",
    "https://www.kraken.com/",
    "https://www.sisense.com/glossary/deterministic-finite-automata/",
    "https://docs.google.com/spreadsheets/d/1tFIbfWgJclvn0XvYOOg1Hm7kd3bHYbQGNIGLpUHZkaA/edit?gid=0#gid=0",
    "https://gvd.ie/",
    "https://dashboard.gvdmarkets.com/#/register?lang=en",
    "https://duskseodigital.com/",
    "https://verifiedcopytraders.org/",
    "https://www.vtmarkets.com/?utm_source=chatgpt.com",
    "https://login.sonos.com/",
    "https://www.abbreviations.com/dsfdsf#google_vignette",
    "https://www.bankofamerica.com/",
    "https://www.blockchainassetfund.com/",
    "https://www.blockchain.com/",
    "https://coinmarketcap.com/currencies/coinmarketcap-20-index/",
    "https://www.coingecko.com/",
    "https://www.binance.com/en",
    "https://jsonviewer.stack.hu/",
    "https://www.sharkninja.com/",
    "https://www.globes.co.il/portal/instrument.aspx?instrumentid=378478&mode=graph",
    "https://www.mizrahi-tefahot.co.il/",
    "https://web.whatsapp.com/",
    "https://github.com/justcallmekoko/ESP32Marauder/releases/tag/v1.10.2",
    "https://smspva.com/",
    "https://www.disney.co.il/",
    "https://edwarddonner.com/2024/11/13/llm-engineering-resources/",
]

def run():
    analyzer = ScamAnalyzer(use_cache=False, no_explain=True)
    results = []

    for i, url in enumerate(TEST_URLS, 1):
        print(f"[{i}/{len(TEST_URLS)}] {url[:60]}...", flush=True)
        try:
            r = analyzer.analyze_url(url)
            risk_score = r.get('risk_assessment', {}).get('risk_score', 0)
            risk_level = r.get('risk_assessment', {}).get('risk_level', '?')
            is_scam = r.get('risk_assessment', {}).get('is_scam', False)
            scam_type = r.get('scam_type', '')
            category = r.get('website_category', {}).get('category', 'unknown')
            cat_name = r.get('website_category', {}).get('name_en', 'Unknown')
            url_risk = r.get('url_inspection', {}).get('url_risk_score', 0)
            url_flags = r.get('url_inspection', {}).get('flags', [])

            results.append({
                'url': url,
                'risk_score': risk_score,
                'risk_level': risk_level,
                'is_scam': is_scam,
                'scam_type': scam_type,
                'category': category,
                'category_name': cat_name,
                'url_risk_score': url_risk,
                'url_flags': url_flags,
            })
        except Exception as e:
            print(f"  ERROR: {str(e)[:80]}")
            results.append({
                'url': url,
                'risk_score': -1,
                'risk_level': 'ERROR',
                'is_scam': False,
                'scam_type': '',
                'category': 'error',
                'category_name': 'Error',
                'url_risk_score': 0,
                'url_flags': [],
                'error': str(e)[:200]
            })

    # Print results table
    print("\n" + "=" * 120)
    print(f"{'URL':<55} {'Score':>5} {'Level':<7} {'Scam?':<6} {'Category':<20} {'Scam Type'}")
    print("=" * 120)
    for r in results:
        url_short = r['url'][:53]
        scam = 'YES' if r['is_scam'] else 'no'
        st = r['scam_type'][:35] if r['scam_type'] else '-'
        print(f"{url_short:<55} {r['risk_score']:>5} {r['risk_level']:<7} {scam:<6} {r['category_name']:<20} {st}")

    # Save full results
    report_path = project_root / 'reports' / 'real_sites_results.json'
    with open(report_path, 'w', encoding='utf-8') as f:
        json.dump(results, f, indent=2, ensure_ascii=False)
    print(f"\nResults saved to: {report_path}")

if __name__ == '__main__':
    run()

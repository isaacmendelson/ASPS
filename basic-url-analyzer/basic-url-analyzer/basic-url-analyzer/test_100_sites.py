"""Test 100 sites and save results to CSV"""
import sys
import csv
sys.stdout.reconfigure(encoding='utf-8')

from core.analyzer import ScamAnalyzer
import os

# Clear cache
cache_dir = 'cache'
if os.path.exists(cache_dir):
    for f in os.listdir(cache_dir):
        try:
            os.remove(os.path.join(cache_dir, f))
        except:
            pass

analyzer = ScamAnalyzer()

# 100 diverse sites
sites = [
    # Tech Giants (10)
    ('https://www.google.com/', 'Tech', 'LOW'),
    ('https://www.microsoft.com/', 'Tech', 'LOW'),
    ('https://www.apple.com/', 'Tech', 'LOW'),
    ('https://www.amazon.com/', 'Tech', 'LOW'),
    ('https://www.facebook.com/', 'Tech', 'LOW'),
    ('https://www.twitter.com/', 'Tech', 'LOW'),
    ('https://www.linkedin.com/', 'Tech', 'LOW'),
    ('https://www.netflix.com/', 'Tech', 'LOW'),
    ('https://www.spotify.com/', 'Tech', 'LOW'),
    ('https://www.adobe.com/', 'Tech', 'LOW'),

    # Social Media (10)
    ('https://www.reddit.com/', 'Social', 'LOW'),
    ('https://www.tiktok.com/', 'Social', 'LOW'),
    ('https://www.instagram.com/', 'Social', 'LOW'),
    ('https://www.pinterest.com/', 'Social', 'LOW'),
    ('https://www.snapchat.com/', 'Social', 'LOW'),
    ('https://discord.com/', 'Social', 'LOW'),
    ('https://www.twitch.tv/', 'Social', 'LOW'),
    ('https://www.tumblr.com/', 'Social', 'LOW'),
    ('https://www.quora.com/', 'Social', 'LOW'),
    ('https://www.medium.com/', 'Social', 'LOW'),

    # E-commerce (10)
    ('https://www.ebay.com/', 'Ecommerce', 'LOW'),
    ('https://www.aliexpress.com/', 'Ecommerce', 'LOW'),
    ('https://www.etsy.com/', 'Ecommerce', 'LOW'),
    ('https://www.walmart.com/', 'Ecommerce', 'LOW'),
    ('https://www.target.com/', 'Ecommerce', 'LOW'),
    ('https://www.bestbuy.com/', 'Ecommerce', 'LOW'),
    ('https://www.wish.com/', 'Ecommerce', 'LOW'),
    ('https://www.shopify.com/', 'Ecommerce', 'LOW'),
    ('https://www.wayfair.com/', 'Ecommerce', 'LOW'),
    ('https://www.newegg.com/', 'Ecommerce', 'LOW'),

    # Banks & Finance (10)
    ('https://www.chase.com/', 'Finance', 'LOW'),
    ('https://www.bankofamerica.com/', 'Finance', 'LOW'),
    ('https://www.wellsfargo.com/', 'Finance', 'LOW'),
    ('https://www.paypal.com/', 'Finance', 'LOW'),
    ('https://www.visa.com/', 'Finance', 'LOW'),
    ('https://www.mastercard.com/', 'Finance', 'LOW'),
    ('https://www.americanexpress.com/', 'Finance', 'LOW'),
    ('https://wise.com/', 'Finance', 'LOW'),
    ('https://www.payoneer.com/', 'Finance', 'LOW'),
    ('https://www.revolut.com/', 'Finance', 'LOW'),

    # Crypto Exchanges (10)
    ('https://www.binance.com/', 'Crypto', 'LOW'),
    ('https://www.coinbase.com/', 'Crypto', 'LOW'),
    ('https://www.kraken.com/', 'Crypto', 'LOW'),
    ('https://www.kucoin.com/', 'Crypto', 'LOW'),
    ('https://www.bybit.com/', 'Crypto', 'LOW'),
    ('https://www.okx.com/', 'Crypto', 'LOW'),
    ('https://www.gate.io/', 'Crypto', 'LOW'),
    ('https://www.bitfinex.com/', 'Crypto', 'LOW'),
    ('https://www.gemini.com/', 'Crypto', 'LOW'),
    ('https://metamask.io/', 'Crypto', 'LOW'),

    # News Sites (10)
    ('https://www.bbc.com/', 'News', 'LOW'),
    ('https://www.cnn.com/', 'News', 'LOW'),
    ('https://www.nytimes.com/', 'News', 'LOW'),
    ('https://www.theguardian.com/', 'News', 'LOW'),
    ('https://www.reuters.com/', 'News', 'LOW'),
    ('https://www.forbes.com/', 'News', 'LOW'),
    ('https://www.bloomberg.com/', 'News', 'LOW'),
    ('https://www.wsj.com/', 'News', 'LOW'),
    ('https://www.washingtonpost.com/', 'News', 'LOW'),
    ('https://www.usatoday.com/', 'News', 'LOW'),

    # Israeli Sites (10)
    ('https://www.ynet.co.il/', 'Israeli', 'LOW'),
    ('https://www.walla.co.il/', 'Israeli', 'LOW'),
    ('https://www.mako.co.il/', 'Israeli', 'LOW'),
    ('https://www.haaretz.co.il/', 'Israeli', 'LOW'),
    ('https://www.globes.co.il/', 'Israeli', 'LOW'),
    ('https://www.leumi.co.il/', 'Israeli', 'LOW'),
    ('https://www.bankhapoalim.co.il/', 'Israeli', 'LOW'),
    ('https://www.isracard.co.il/', 'Israeli', 'LOW'),
    ('https://www.cal-online.co.il/', 'Israeli', 'LOW'),
    ('https://www.max.co.il/', 'Israeli', 'LOW'),

    # Tech Tools (10)
    ('https://www.github.com/', 'Tools', 'LOW'),
    ('https://www.gitlab.com/', 'Tools', 'LOW'),
    ('https://www.notion.so/', 'Tools', 'LOW'),
    ('https://www.figma.com/', 'Tools', 'LOW'),
    ('https://www.dropbox.com/', 'Tools', 'LOW'),
    ('https://www.zoom.us/', 'Tools', 'LOW'),
    ('https://slack.com/', 'Tools', 'LOW'),
    ('https://www.trello.com/', 'Tools', 'LOW'),
    ('https://www.asana.com/', 'Tools', 'LOW'),
    ('https://www.canva.com/', 'Tools', 'LOW'),

    # URL Shorteners & Services (5)
    ('https://bit.ly/', 'Service', 'LOW'),
    ('https://t.co/', 'Service', 'LOW'),
    ('https://www.cloudflare.com/', 'Service', 'LOW'),
    ('https://www.godaddy.com/', 'Service', 'LOW'),
    ('https://www.namecheap.com/', 'Service', 'LOW'),

    # Known Scams from cryptolegal.uk list (15)
    ('https://blockchainassetfund.com/', 'Scam', 'HIGH'),
    ('https://exodus-inv.net/', 'Scam', 'HIGH'),
    ('https://100x-bitcoin.com/', 'Scam', 'HIGH'),
    ('https://24cryptogain.com/', 'Scam', 'HIGH'),
    ('https://365coinprofit.com/', 'Scam', 'HIGH'),
    ('https://alphatrustmine.com/', 'Scam', 'HIGH'),
    ('https://bitcoinimt.com/', 'Scam', 'HIGH'),
    ('https://cryptovibepro.live/', 'Scam', 'HIGH'),
    ('https://10xbitcoin.com/', 'Scam', 'HIGH'),
    ('https://24hourbitcoindoubler.com/', 'Scam', 'HIGH'),
    ('https://247investment.live/', 'Scam', 'HIGH'),
    ('https://360cryptofx.org/', 'Scam', 'HIGH'),
    ('https://aavedefi.live/', 'Scam', 'HIGH'),
    ('https://aceprofitablefxtrade.online/', 'Scam', 'HIGH'),
    ('https://advancecrypto.ltd/', 'Scam', 'HIGH'),

    # Gaming (10)
    ('https://store.steampowered.com/', 'Gaming', 'LOW'),
    ('https://www.epicgames.com/', 'Gaming', 'LOW'),
    ('https://www.roblox.com/', 'Gaming', 'LOW'),
    ('https://www.minecraft.net/', 'Gaming', 'LOW'),
    ('https://www.ea.com/', 'Gaming', 'LOW'),
    ('https://www.ubisoft.com/', 'Gaming', 'LOW'),
    ('https://www.playstation.com/', 'Gaming', 'LOW'),
    ('https://www.xbox.com/', 'Gaming', 'LOW'),
    ('https://www.nintendo.com/', 'Gaming', 'LOW'),
    ('https://www.blizzard.com/', 'Gaming', 'LOW'),
]

print(f'Testing {len(sites)} sites...')
print('Progress: ', end='', flush=True)

results = []
for i, (url, category, expected) in enumerate(sites):
    if i % 10 == 0:
        print(f'{i}..', end='', flush=True)

    try:
        result = analyzer.analyze_url(url)
        ra = result.get('risk_assessment', {})
        risk = ra.get('risk_level', 'ERROR')
        score = ra.get('risk_score', 0)
        is_scam = ra.get('is_scam', False)
        confidence = ra.get('confidence', 0)

        ml = result.get('ml_analysis', {})
        ml_score = ml.get('score', 0) if ml else 0
        ml_conf = ml.get('confidence', 0) if ml else 0

        whois = result.get('whois', {})
        domain_age = whois.get('domain_age_days', 0) if whois else 0

        domain = url.split('//')[1].split('/')[0].replace('www.', '')
        correct = 'YES' if risk == expected else 'NO'

        results.append({
            'Domain': domain,
            'Category': category,
            'Expected': expected,
            'Result': risk,
            'Correct': correct,
            'RiskScore': score,
            'IsScam': is_scam,
            'Confidence': f'{confidence*100:.0f}%',
            'ML_Score': f'{ml_score*100:.0f}%',
            'DomainAge_Days': domain_age,
            'DomainAge_Years': round(domain_age/365, 1) if domain_age else 0
        })
    except Exception as e:
        domain = url.split('//')[1].split('/')[0].replace('www.', '')
        results.append({
            'Domain': domain,
            'Category': category,
            'Expected': expected,
            'Result': 'ERROR',
            'Correct': 'N/A',
            'RiskScore': 0,
            'IsScam': False,
            'Confidence': '0%',
            'ML_Score': '0%',
            'DomainAge_Days': 0,
            'DomainAge_Years': 0
        })

print('Done!')

# Save to CSV
csv_file = 'test_results_100_sites.csv'
with open(csv_file, 'w', newline='', encoding='utf-8-sig') as f:
    writer = csv.DictWriter(f, fieldnames=['Domain', 'Category', 'Expected', 'Result', 'Correct', 'RiskScore', 'IsScam', 'Confidence', 'ML_Score', 'DomainAge_Days', 'DomainAge_Years'])
    writer.writeheader()
    writer.writerows(results)

# Statistics
correct_count = sum(1 for r in results if r['Correct'] == 'YES')
error_count = sum(1 for r in results if r['Correct'] == 'N/A')
total = len(results)
valid_total = total - error_count
accuracy = correct_count / valid_total * 100 if valid_total > 0 else 0

print(f'\n=== STATISTICS ===')
print(f'Total Sites: {total}')
print(f'Analyzed: {valid_total}')
print(f'Errors: {error_count}')
print(f'Correct: {correct_count}')
print(f'Accuracy: {accuracy:.1f}%')
print(f'\nSaved to: {csv_file}')

# Category breakdown
print('\n=== BY CATEGORY ===')
categories = {}
for r in results:
    cat = r['Category']
    if cat not in categories:
        categories[cat] = {'total': 0, 'correct': 0, 'errors': 0}
    categories[cat]['total'] += 1
    if r['Correct'] == 'YES':
        categories[cat]['correct'] += 1
    elif r['Correct'] == 'N/A':
        categories[cat]['errors'] += 1

for cat, data in sorted(categories.items()):
    valid = data['total'] - data['errors']
    acc = data['correct'] / valid * 100 if valid > 0 else 0
    print(f"{cat:12} | {data['correct']}/{valid} ({acc:.0f}%)")

# Show incorrect classifications
errors = [r for r in results if r['Correct'] == 'NO']
if errors:
    print('\n=== INCORRECT CLASSIFICATIONS ===')
    for e in errors:
        print(f"{e['Domain']:35} | Expected: {e['Expected']} | Got: {e['Result']} | ML: {e['ML_Score']} | Age: {e['DomainAge_Years']}y")

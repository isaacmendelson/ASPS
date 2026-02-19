from scrapers.playwright_scraper import PlaywrightScraper

s = PlaywrightScraper()
r = s.fetch('https://www.exodus-inv.net/')

# Save full HTML to file
with open('scam_site_html.txt', 'w', encoding='utf-8') as f:
    f.write(r['html'])

print("HTML saved to scam_site_html.txt")
print(f"Total length: {len(r['html'])} characters")

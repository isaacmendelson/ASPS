# Category Classifier Upgrade Plan

## 1. Files to Create/Modify

### Modify
| File | Changes |
|------|---------|
| `core/category_classifier.py` | Complete rewrite: add Layer 2 (domain patterns) + Layer 3 (enhanced content), new output schema with `detection_method` and `matched_signals`, expand from 17 to 45+ categories |
| `core/analyzer.py` | Update `_CATEGORY_TO_WEBSITE_TYPE` mapping (line 267-278), update `website_category` output block (lines 332-336) to include new fields, pass `domain` arg to `classify()` call (line 263) |
| `config/patterns.json` | Add new `category_domain_patterns` and `category_keywords` sections (or create separate file) |
| `ASPSBackend14_J/Common/Enums/WebsiteType.cs` | Expand enum from 8 to ~20 values to support new category groups |
| `ASPSBackend14_J/Business/RealtimeAnalysis/UserDomain/UrlAnalysisViewModels.cs` | Add `DetectionMethod` and `MatchedSignals` properties to `WebsiteCategory` class |

### Create
| File | Purpose |
|------|---------|
| `config/category_patterns.json` | Dedicated config file for all 45+ category keyword lists and domain pattern rules (keeps `patterns.json` focused on scam patterns) |

---

## 2. Full Category List with Keywords and Domain Patterns

### Financial (8 categories)

| Category ID | Name (EN) | Name (HE) | Domain Patterns | Keywords (EN) | Keywords (HE) |
|---|---|---|---|---|---|
| `banking` | Banking | בנקאות | `*.bank`, `*.co.il` (with banking keywords), `bank*.co.il` | bank, banking, account, deposit, savings, checking, wire transfer, atm, branch, online banking | בנק, חשבון, פיקדון, חיסכון, העברה, סניף |
| `credit_union` | Credit Union | אגודת אשראי | — | credit union, member, cooperative, share account, cu | קואופרטיב, אגודת אשראי |
| `insurance` | Insurance | ביטוח | — | insurance, policy, premium, claim, coverage, underwriting, deductible, actuary | ביטוח, פוליסה, פרמיה, תביעה, כיסוי |
| `investment` | Investment | השקעות | — | investment, portfolio, mutual fund, hedge fund, asset management, wealth, advisory, fiduciary | השקעה, תיק, קרן, נכסים, ייעוץ |
| `stock_trading` | Stock Trading | מסחר מניות | — | stock, trading, broker, nasdaq, nyse, shares, equity, dividend, market, ticker | מניה, מסחר, ברוקר, בורסה, דיבידנד |
| `crypto_exchange` | Crypto Exchange | בורסת קריפטו | — | crypto, bitcoin, ethereum, exchange, wallet, blockchain, token, defi, nft, altcoin | קריפטו, ביטקוין, ארנק, בלוקצ'יין |
| `payment_service` | Payment Service | שירותי תשלום | — | payment, pay, transfer, checkout, invoice, billing, wallet, fintech, remittance | תשלום, העברה, חשבונית, ארנק דיגיטלי |
| `lending` | Lending | הלוואות | — | loan, lending, mortgage, borrow, interest rate, credit, refinance, installment | הלוואה, משכנתא, אשראי, ריבית, מימון |

### Shopping (7 categories)

| Category ID | Name (EN) | Name (HE) | Domain Patterns | Keywords (EN) | Keywords (HE) |
|---|---|---|---|---|---|
| `ecommerce` | E-commerce | קניות אונליין | — | shop, store, buy, cart, checkout, product, shipping, delivery, order, add to cart | חנות, קנייה, עגלה, מוצר, משלוח, הזמנה |
| `marketplace` | Marketplace | שוק מקוון | — | marketplace, seller, buyer, listing, used, second hand, peer-to-peer | שוק, מוכר, קונה, יד שנייה |
| `auction` | Auction | מכירה פומבית | — | auction, bid, bidding, lot, hammer, going once | מכירה פומבית, הצעת מחיר |
| `classifieds` | Classifieds | לוח מודעות | — | classifieds, ad, listing, for sale, wanted, free, post ad | לוח, מודעה, למכירה, דרושים |
| `grocery` | Grocery | סופרמרקט | — | grocery, supermarket, fresh, produce, organic, food delivery, pantry | סופר, מכולת, טרי, אורגני, משלוח מזון |
| `fashion` | Fashion | אופנה | — | fashion, clothing, apparel, outfit, dress, shoes, accessories, designer, style | אופנה, ביגוד, נעליים, אקססוריז, סטייל |
| `electronics` | Electronics | אלקטרוניקה | — | electronics, gadget, phone, laptop, computer, camera, tablet, headphones, tech | אלקטרוניקה, טלפון, מחשב, מצלמה, טאבלט |

### Government (6 categories)

| Category ID | Name (EN) | Name (HE) | Domain Patterns | Keywords (EN) | Keywords (HE) |
|---|---|---|---|---|---|
| `government` | Government | ממשלה | `*.gov`, `*.gov.il`, `*.gov.*` | government, ministry, federal, national, public service, citizen, official | ממשלה, משרד, לאומי, שירות ציבורי, אזרח |
| `municipality` | Municipality | עירייה | `*.muni.il` | municipality, city, town, council, local authority, mayor, civic | עירייה, מועצה, ראש עיר, רשות מקומית |
| `military` | Military | צבא | `*.mil`, `*.idf.il` | military, army, navy, air force, defense, armed forces, veteran | צבא, צהל, ביטחון, חיל |
| `court` | Court | בית משפט | `court.gov.il` | court, judicial, judge, verdict, tribunal, case, lawsuit, legal | בית משפט, שופט, פסק דין, משפט |
| `tax_authority` | Tax Authority | רשות המיסים | — | tax, revenue, irs, customs, vat, filing, return, refund | מס, מע"מ, רשות המיסים, שומה, החזר |
| `public_service` | Public Service | שירות ציבורי | — | public service, civil service, social security, welfare, permit, license, registration | שירות ציבורי, ביטוח לאומי, רווחה, רישיון |

### Health (5 categories)

| Category ID | Name (EN) | Name (HE) | Domain Patterns | Keywords (EN) | Keywords (HE) |
|---|---|---|---|---|---|
| `hospital` | Hospital | בית חולים | — | hospital, emergency, ward, surgery, inpatient, outpatient, er, icu | בית חולים, מיון, ניתוח, אשפוז |
| `clinic` | Clinic | מרפאה | — | clinic, doctor, physician, appointment, checkup, consultation, specialist | מרפאה, רופא, תור, בדיקה, מומחה |
| `pharmacy` | Pharmacy | בית מרקחת | `*.pharmacy` | pharmacy, drug, prescription, medication, otc, refill, pharmacist | בית מרקחת, תרופה, מרשם, רוקח |
| `telehealth` | Telehealth | רפואה מרחוק | — | telehealth, telemedicine, virtual visit, online doctor, video consultation, remote care | רפואה מרחוק, ייעוץ מקוון |
| `mental_health` | Mental Health | בריאות הנפש | — | therapy, therapist, counseling, psychologist, psychiatrist, mental health, anxiety, depression | טיפול, פסיכולוג, ייעוץ, בריאות הנפש |

### Education (4 categories)

| Category ID | Name (EN) | Name (HE) | Domain Patterns | Keywords (EN) | Keywords (HE) |
|---|---|---|---|---|---|
| `university` | University | אוניברסיטה | `*.edu`, `*.ac.il`, `*.edu.*` | university, college, faculty, campus, degree, bachelor, master, phd, research | אוניברסיטה, מכללה, פקולטה, תואר, מחקר |
| `school` | School | בית ספר | `*.sch.il` | school, k-12, elementary, high school, middle school, teacher, student, class | בית ספר, תלמיד, מורה, כיתה |
| `online_course` | Online Course | קורס מקוון | — | course, tutorial, lesson, certificate, mooc, instructor, curriculum, syllabus | קורס, שיעור, תעודה, לימוד |
| `elearning` | E-learning | למידה מקוונת | — | e-learning, lms, learning platform, quiz, assignment, grade, enrollment | למידה מקוונת, מבחן, ציון, הרשמה |

### Entertainment (5 categories)

| Category ID | Name (EN) | Name (HE) | Domain Patterns | Keywords (EN) | Keywords (HE) |
|---|---|---|---|---|---|
| `streaming` | Streaming | סטרימינג | — | stream, watch, movie, series, episode, subscribe, binge, on demand | צפייה, סרט, סדרה, פרק, מנוי |
| `gaming` | Gaming | גיימינג | — | game, gaming, play, gamer, esports, multiplayer, console, pc game, steam | משחק, שחקן, גיימר |
| `gambling` | Gambling/Casino | הימורים/קזינו | `*.casino`, `*.bet` | casino, gambling, bet, slot, poker, roulette, blackjack, jackpot, wager | קזינו, הימורים, הימור, פוקר |
| `sports_betting` | Sports Betting | הימורי ספורט | `*.bet` | sports bet, odds, spread, handicap, over under, parlay, bookie | הימורי ספורט, אודס, טוטו |
| `adult_content` | Adult Content | תוכן למבוגרים | `*.xxx`, `*.adult`, `*.porn` | adult, 18+, nsfw, mature content, explicit | תוכן למבוגרים |

### Media (5 categories)

| Category ID | Name (EN) | Name (HE) | Domain Patterns | Keywords (EN) | Keywords (HE) |
|---|---|---|---|---|---|
| `news` | News | חדשות | `*.news` | news, breaking, headline, reporter, editorial, press, article, correspondent | חדשות, כתבה, מהדורה, כתב, מאמר |
| `blog` | Blog | בלוג | — | blog, post, author, comment, tag, archive, personal, opinion | בלוג, פוסט, מחבר, תגובה |
| `forum` | Forum | פורום | — | forum, thread, topic, reply, discussion, board, community, moderator | פורום, שרשור, דיון, קהילה |
| `social_network` | Social Network | רשת חברתית | — | social, profile, friend, follow, share, like, post, feed, connect, timeline | חברתי, פרופיל, חבר, עוקב, שיתוף |
| `messaging` | Messaging | הודעות | — | message, chat, inbox, send, conversation, dm, group chat, call | הודעה, צ'אט, שיחה, שליחה |

### Services (5 categories)

| Category ID | Name (EN) | Name (HE) | Domain Patterns | Keywords (EN) | Keywords (HE) |
|---|---|---|---|---|---|
| `legal` | Legal Services | שירותים משפטיים | — | lawyer, attorney, legal, law firm, counsel, litigation, contract | עורך דין, משפט, חוזה, ייעוץ משפטי |
| `accounting` | Accounting | ראיית חשבון | — | accounting, accountant, cpa, bookkeeping, audit, tax preparation, payroll | רואה חשבון, הנהלת חשבונות, ביקורת |
| `real_estate` | Real Estate | נדל"ן | — | real estate, property, house, apartment, rent, realtor, listing, mortgage | נדלן, דירה, שכירות, מכירה, תיווך |
| `travel` | Travel | תיירות | `*.travel` | travel, hotel, flight, vacation, tourism, booking, destination, resort | תיירות, מלון, טיסה, חופשה, הזמנה |
| `job_board` | Job Board | לוח דרושים | — | job, career, hiring, recruit, resume, cv, apply, employer, vacancy | דרושים, משרה, קריירה, קורות חיים |

### Technology (5 categories)

| Category ID | Name (EN) | Name (HE) | Domain Patterns | Keywords (EN) | Keywords (HE) |
|---|---|---|---|---|---|
| `saas` | SaaS | תוכנה כשירות | — | saas, platform, dashboard, subscription, plan, workspace, integration, api | פלטפורמה, מנוי, תוכנית |
| `cloud` | Cloud Services | שירותי ענן | — | cloud, hosting, server, infrastructure, storage, compute, deploy, scalable | ענן, שרת, תשתית, אחסון |
| `web_hosting` | Web Hosting | אחסון אתרים | — | hosting, domain, cpanel, dns, ssl, bandwidth, uptime, server | אחסון, דומיין, שרת |
| `vpn_proxy` | VPN/Proxy | VPN/פרוקסי | — | vpn, proxy, privacy, anonymous, unblock, encrypt, tunnel, no log | vpn, פרטיות, אנונימי, הצפנה |
| `developer_tools` | Developer Tools | כלי פיתוח | — | developer, api, sdk, github, repository, code, debug, documentation, framework | מפתח, קוד, תיעוד, פריימוורק |

### Other (5 categories)

| Category ID | Name (EN) | Name (HE) | Domain Patterns | Keywords (EN) | Keywords (HE) |
|---|---|---|---|---|---|
| `restaurant` | Restaurant | מסעדה | — | restaurant, menu, reservation, dine, chef, cuisine, order food, takeout | מסעדה, תפריט, הזמנה, שף |
| `automotive` | Automotive | רכב | — | car, auto, vehicle, dealer, used car, truck, motorcycle, repair, lease | רכב, מכונית, סוכנות, מוסך, ליסינג |
| `pets` | Pets | חיות מחמד | — | pet, dog, cat, vet, veterinary, grooming, adoption, pet food | חיות מחמד, כלב, חתול, וטרינר |
| `nonprofit` | Non-profit | עמותה | `*.org.il` (with nonprofit keywords) | nonprofit, charity, donate, volunteer, foundation, ngo, cause, mission | עמותה, תרומה, מתנדב, קרן |
| `religious` | Religious | דת | — | church, mosque, synagogue, temple, prayer, faith, worship, congregation, sermon | בית כנסת, מסגד, כנסייה, תפילה, קהילה |

**Total: 55 categories across 10 groups**

---

## 3. Scoring Algorithm Design

### Overview

Two detection layers execute in sequence. Layer 2 (domain) can short-circuit with high confidence; Layer 3 (content) always runs and can override or supplement.

### Layer 2: Domain Pattern Matching

```
Input: parsed URL (TLD, SLD, full domain)

1. Extract TLD components: .gov.il, .ac.il, .bank, .casino, etc.
2. Match against DOMAIN_PATTERNS dict (TLD/suffix -> category_id)
3. If match found:
   - confidence = 0.85 (high - domain is strong signal)
   - detection_method = "domain_pattern"
   - matched_signals = [{"type": "domain_tld", "value": ".gov.il", "category": "government"}]
   - Return as primary_result (but still run Layer 3)
```

**Domain pattern priority rules:**
- Exact TLD matches (`.bank`, `.casino`, `.pharmacy`) = confidence 0.90
- Country-specific patterns (`.gov.il`, `.ac.il`, `.muni.il`) = confidence 0.85
- Generic TLD patterns (`.gov.*`, `.edu.*`) = confidence 0.80
- Subdomain/SLD patterns (`bank*.co.il`) = confidence 0.70

### Layer 3: Enhanced Content Analysis

```
Input: extracted content dict, domain string

1. Build analysis text from (weighted by position):
   - title (weight 3.0)
   - meta_description (weight 2.0)
   - h1 headings (weight 2.5)
   - h2 headings (weight 1.5)
   - body_text first 5000 chars (weight 1.0)
   - domain name itself (weight 2.0)

2. For each of the 55 categories:
   a. Keyword matching: count matches, normalize by keyword list size
      keyword_score = min(weighted_matches / len(keywords), 1.0)
   b. Structure signals (if available):
      - form_types: login form -> could be banking/saas; payment form -> ecommerce/payment
      - cta_count: high CTA -> ecommerce/saas
      - link patterns: many product links -> ecommerce
   c. Meta tag signals:
      - og:type, schema.org markup, meta keywords
   d. Combine: category_score = (keyword_score * 0.6) + (structure_score * 0.25) + (meta_score * 0.15)
   
3. detection_method = "content_analysis"
4. matched_signals = list of all signals that contributed to the score
```

### Score Combination (Layer 2 + Layer 3)

```
If Layer 2 matched AND Layer 3 agrees (same category or same group):
   final_category = Layer 2 category
   final_confidence = max(layer2_confidence, layer3_confidence)
   detection_method = "domain_pattern+content_analysis"

If Layer 2 matched BUT Layer 3 disagrees (different group):
   If layer3_confidence > 0.7 AND layer2_confidence < 0.8:
      final_category = Layer 3 category  (content overrides weak domain match)
      detection_method = "content_analysis"
   Else:
      final_category = Layer 2 category  (trust domain)
      detection_method = "domain_pattern"
   NOTE: Add a warning flag "domain_content_mismatch" for possible phishing

If only Layer 3 matched:
   final_category = Layer 3 category
   final_confidence = layer3_score
   detection_method = "content_analysis"

If neither matched:
   final_category = "unknown"
   final_confidence = 0.0
   detection_method = "none"
```

**Confidence thresholds:**
- >= 0.7: High confidence, report as primary category
- 0.4 - 0.69: Medium confidence, report with caveat
- 0.1 - 0.39: Low confidence, report as "possible"
- < 0.1: Unknown

---

## 4. New Output Schema

### CategoryClassifier.classify() return value

```python
{
    "success": True,
    "category": "banking",              # category_id (snake_case)
    "category_group": "financial",       # parent group
    "name_en": "Banking",
    "name_he": "בנקאות",
    "confidence": 0.85,
    "detection_method": "domain_pattern+content_analysis",  # "domain_pattern" | "content_analysis" | "domain_pattern+content_analysis" | "none"
    "matched_signals": [
        {"type": "domain_tld", "value": ".bank", "weight": 0.90},
        {"type": "keyword", "value": "online banking", "weight": 0.15},
        {"type": "keyword", "value": "account", "weight": 0.10},
        {"type": "meta_tag", "value": "og:type=bank", "weight": 0.20}
    ],
    "secondary_category": "payment_service",   # second-best match if close
    "secondary_confidence": 0.45,
    "all_scores": { ... },              # keep for debugging
    "error": ""
}
```

### analyzer.py `website_category` output block (lines 332-336)

```python
'website_category': {
    'category': category_result.get('category', 'unknown'),
    'category_group': category_result.get('category_group', 'unknown'),
    'name_en': category_result.get('name_en', 'Unknown'),
    'confidence': category_result.get('confidence', 0.0),
    'detection_method': category_result.get('detection_method', 'none'),
    'matched_signals': category_result.get('matched_signals', [])
}
```

---

## 5. Integration with analyzer.py

### Changes needed

**Line 263** - Pass domain to classify:
```
Currently:  category_result = self.category_classifier.classify(content)
Change to:  category_result = self.category_classifier.classify(content, domain)
```
Where `domain` is already available in the `analyze()` method scope.

**Lines 267-278** - Update `_CATEGORY_TO_WEBSITE_TYPE` mapping:
Replace the current 10-entry dict with the expanded mapping (see Section 6).

**Line 279-280** - Update mapping lookup:
```
Currently:  raw_category = classification.get('category', 'unknown')
Change to:  raw_category = category_result.get('category', 'unknown')
```
Note: Currently the mapping reads from `classification` (purpose classifier) not `category_result`. This should use `category_result` from the category classifier for the WebsiteType mapping.

**Lines 332-336** - Update `website_category` block to include new fields (see Section 4).

---

## 6. Backend WebsiteType Enum Changes

### Current enum (`ASPSBackend14_J/Common/Enums/WebsiteType.cs`)

```csharp
Unknown=0, Analytics=1, Banking=2, News=3, ECommerce=4, Telecom=5, Dating=6, Exchange=7
```

### Proposed expanded enum

```csharp
Unknown = 0,
Analytics = 1,       // keep (maps to: saas, developer_tools)
Banking = 2,         // keep (maps to: banking, credit_union)
News = 3,            // keep (maps to: news)
ECommerce = 4,       // keep (maps to: ecommerce, marketplace, auction, fashion, electronics, grocery)
Telecom = 5,         // keep
Dating = 6,          // keep (maps to: social_network)
Exchange = 7,        // keep (maps to: crypto_exchange, stock_trading)
Insurance = 8,       // NEW
Investment = 9,      // NEW
Government = 10,     // NEW
Education = 11,      // NEW
Healthcare = 12,     // NEW
Entertainment = 13,  // NEW (maps to: streaming, gaming)
Gambling = 14,       // NEW (maps to: gambling, sports_betting)
Travel = 15,         // NEW
RealEstate = 16,     // NEW
Legal = 17,          // NEW
Restaurant = 18,     // NEW
Nonprofit = 19,      // NEW
AdultContent = 20    // NEW
```

### Updated `_CATEGORY_TO_WEBSITE_TYPE` mapping in analyzer.py

```python
_CATEGORY_TO_WEBSITE_TYPE = {
    # Financial
    'banking': 'Banking',
    'credit_union': 'Banking',
    'insurance': 'Insurance',
    'investment': 'Investment',
    'stock_trading': 'Exchange',
    'crypto_exchange': 'Exchange',
    'payment_service': 'Banking',
    'lending': 'Banking',
    # Shopping
    'ecommerce': 'ECommerce',
    'marketplace': 'ECommerce',
    'auction': 'ECommerce',
    'classifieds': 'ECommerce',
    'grocery': 'ECommerce',
    'fashion': 'ECommerce',
    'electronics': 'ECommerce',
    # Government
    'government': 'Government',
    'municipality': 'Government',
    'military': 'Government',
    'court': 'Government',
    'tax_authority': 'Government',
    'public_service': 'Government',
    # Health
    'hospital': 'Healthcare',
    'clinic': 'Healthcare',
    'pharmacy': 'Healthcare',
    'telehealth': 'Healthcare',
    'mental_health': 'Healthcare',
    # Education
    'university': 'Education',
    'school': 'Education',
    'online_course': 'Education',
    'elearning': 'Education',
    # Entertainment
    'streaming': 'Entertainment',
    'gaming': 'Entertainment',
    'gambling': 'Gambling',
    'sports_betting': 'Gambling',
    'adult_content': 'AdultContent',
    # Media
    'news': 'News',
    'blog': 'News',
    'forum': 'Analytics',
    'social_network': 'Dating',
    'messaging': 'Dating',
    # Services
    'legal': 'Legal',
    'accounting': 'Analytics',
    'real_estate': 'RealEstate',
    'travel': 'Travel',
    'job_board': 'Analytics',
    # Technology
    'saas': 'Analytics',
    'cloud': 'Analytics',
    'web_hosting': 'Analytics',
    'vpn_proxy': 'Analytics',
    'developer_tools': 'Analytics',
    # Other
    'restaurant': 'Restaurant',
    'automotive': 'Unknown',
    'pets': 'Unknown',
    'nonprofit': 'Nonprofit',
    'religious': 'Unknown',
    # Legacy purpose classifier mappings (keep for backward compat)
    'crypto_scam': 'Exchange',
    'investment_scam': 'Exchange',
    'fake_ecommerce': 'ECommerce',
    'romance_scam': 'Dating',
}
```

### Backend `WebsiteCategory` class changes (`UrlAnalysisViewModels.cs`)

Add two new properties:

```csharp
public string DetectionMethod { get; set; }     // "domain_pattern", "content_analysis", etc.
public string[] MatchedSignals { get; set; }     // simplified string array for C# side
```

Update constructor accordingly. The `MatchedSignals` should be serialized as a simple string array (e.g., `["domain_tld:.bank", "keyword:online banking"]`) to avoid complex nested objects in C#.

---

## 7. Test Cases

### Layer 2 (Domain Pattern) Tests

| # | Input Domain | Expected Category | Expected Method | Notes |
|---|---|---|---|---|
| 1 | `www.leumi.bank` | banking | domain_pattern | .bank TLD |
| 2 | `www.gov.il` | government | domain_pattern | .gov.il |
| 3 | `www.huji.ac.il` | university | domain_pattern | .ac.il |
| 4 | `www.tel-aviv.muni.il` | municipality | domain_pattern | .muni.il |
| 5 | `www.army.mil` | military | domain_pattern | .mil |
| 6 | `example.casino` | gambling | domain_pattern | .casino TLD |
| 7 | `example.pharmacy` | pharmacy | domain_pattern | .pharmacy TLD |
| 8 | `booking.travel` | travel | domain_pattern | .travel TLD |
| 9 | `example.co.il` | (depends on content) | content_analysis | .co.il alone is not specific |
| 10 | `example.com` | (depends on content) | content_analysis | generic TLD, no domain match |

### Layer 3 (Content Analysis) Tests

| # | Input Content (title + body) | Expected Category | Notes |
|---|---|---|---|
| 11 | Title: "Open a Savings Account - First National Bank" | banking | Strong banking keywords |
| 12 | Title: "Buy Electronics Online - Free Shipping" | electronics or ecommerce | Shopping keywords |
| 13 | Title: "Harvard University - Welcome" with .edu domain | university | Domain + content agree |
| 14 | Title: "Online Poker - Play Now" with casino keywords | gambling | Gambling content |
| 15 | Title: "Find a Therapist Near You" | mental_health | Health keywords |
| 16 | Title: "Latest Tech News" body: "startup raised..." | news | Media keywords |
| 17 | Title: "משרד הבריאות" (Ministry of Health) | government | Hebrew government keywords |
| 18 | Title: "עורך דין תל אביב" (Lawyer Tel Aviv) | legal | Hebrew legal keywords |
| 19 | Empty/minimal content | unknown | Graceful fallback |
| 20 | Mixed signals (banking title + ecommerce body) | banking | Title weighted higher |

### Combined Layer Tests

| # | Domain | Content | Expected | Notes |
|---|---|---|---|---|
| 21 | `.gov.il` | Government content | government (0.90) | Both layers agree |
| 22 | `.gov.il` | Shopping content | government (with warning) | Domain-content mismatch = phishing signal |
| 23 | `.com` | Banking content | banking | Content-only detection |
| 24 | `.bank` | Empty content | banking | Domain alone sufficient |
| 25 | `.co.il` | University content | university | Content overrides generic domain |

### Edge Cases

| # | Scenario | Expected Behavior |
|---|---|---|
| 26 | Content is `None` or extraction failed | Return unknown, confidence 0, no error |
| 27 | Domain is empty string | Skip Layer 2, run Layer 3 only |
| 28 | Hebrew-only content | Correctly match Hebrew keywords |
| 29 | Bilingual content (EN+HE) | Combine signals from both languages |
| 30 | Very short content (< 50 chars) | Lower confidence, but still attempt |
| 31 | Category tie (two categories score equally) | Return higher-weighted group first, set secondary |
| 32 | `.bank` domain but phishing content | Return banking + add phishing warning |

### Backward Compatibility Tests

| # | Scenario | Expected |
|---|---|---|
| 33 | Old category IDs (`finance_banking`) still work | Map to new `banking` internally |
| 34 | Backend receives valid WebsiteType enum value | All new categories map to valid enum |
| 35 | `website_category` JSON has all required fields | `detection_method` and `matched_signals` present |
| 36 | `matched_signals` is always an array (never null) | Empty array `[]` when no signals |

---

## 8. Implementation Order

1. **Create `config/category_patterns.json`** - All 55 categories with keywords (EN+HE) and domain patterns
2. **Rewrite `core/category_classifier.py`** - Layer 2 + Layer 3 logic, new output schema
3. **Update `core/analyzer.py`** - Pass domain, update mapping, update output block
4. **Update backend enum** - `WebsiteType.cs` - add new enum values
5. **Update backend model** - `UrlAnalysisViewModels.cs` - add new properties
6. **Write tests** - Cover all 36 test cases above
7. **Integration test** - End-to-end: URL in -> full result with new category fields out

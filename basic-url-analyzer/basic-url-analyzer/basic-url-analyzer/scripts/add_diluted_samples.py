#!/usr/bin/env python3
"""Add 90 diluted scam samples to training data."""

import json
from datetime import date

TODAY = date.today().isoformat()

def create_investment_diluted_samples():
    """Create 50 investment_scam_diluted samples."""
    samples = []

    # CFD/Forex Broker Scams (1-10)
    cfd_samples = [
        {
            "id": "scam_investment_diluted_0001",
            "text": """Exodus Investment Group - Your Gateway to Financial Freedom

Unlock exclusive CFD trading opportunities once reserved for the ultra-wealthy, now available to everyone. Our proprietary AI-powered trading system delivers guaranteed returns of up to 500% annually with absolutely zero risk. Join over 2 million satisfied customers who have transformed their financial futures.

Why Choose Exodus Investment:
- Over 10 years of proven excellence in global financial markets
- 2,000,000+ active traders across 150 countries
- Ranked #1 in the 2025 International Broker Excellence Awards
- 1,500+ dedicated financial experts at your service
- Industry-leading 0.2% management fees

Our Trading Platform Features:
Experience seamless CFD trading on forex pairs, commodities, indices, and stocks. Our advanced charting tools, real-time market analysis, and one-click execution make trading effortless for beginners and professionals alike. No experience needed - our automated system handles everything.

Payment Methods: Visa, Mastercard, American Express, PayPal, Skrill, Neteller
All transactions are protected by 256-bit SSL encryption and bank-level security protocols.

SECURITY NOTICE: Beware of fake websites impersonating Exodus Investment. We will never call you asking for passwords or payment details.

Legal | Privacy Policy | Terms of Use | Risk Disclosure | AML Policy | Contact Us

Exodus Investment Group Ltd. | 45 Bay Street, Suite 3200, Toronto, ON M5J 2X5, Canada
Registered Company: BC1234567 | Licensed in Multiple Jurisdictions""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0002",
            "text": """GlobalTrade FX - Trade Smarter, Earn Faster

Discover a revolutionary approach to forex and CFD trading that delivers guaranteed profits with minimal effort. Our exclusive algorithm has achieved an unprecedented 98% success rate over 5 years of live trading. Limited spots available - only 50 positions remain for new members this month.

Why Traders Choose GlobalTrade FX:
- 8 years of continuous operation serving traders worldwide
- 1,500,000+ registered accounts across all continents
- Winner of Best Forex Platform 2024 by Global Finance Magazine
- 24/7 dedicated support from certified trading specialists
- Ultra-competitive spreads starting at 0.1 pips

Trading Instruments Available:
Access over 200 forex pairs, CFDs on major indices, precious metals, energy commodities, and cryptocurrency derivatives. Our MetaTrader 5 integration provides advanced technical analysis tools and automated trading capabilities.

Accepted Payment Methods: Bank Transfer, Visa, Mastercard, PayPal, Skrill, Bitcoin
Your funds are secured in segregated accounts at tier-1 banks.

WARNING: We have been made aware of fraudulent websites copying our brand. Always verify you are on our official domain.

Legal Disclosures | Privacy Policy | Terms & Conditions | Cookie Policy | Contact Support

GlobalTrade FX Limited | 12 Moorgate, London EC2R 6DA, United Kingdom
Company Registration: 09876543 | FCA Pending Application""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0003",
            "text": """PrimeFX Markets - Where Dreams Become Reality

Start your journey to financial independence with the world's most trusted CFD trading platform. Our proven system generates risk-free returns of 15-25% monthly - guaranteed or your money back. No prior trading experience required as our smart technology does all the work.

Why PrimeFX Markets Stands Out:
- Established in 2016 with 9 years of market leadership
- Over 3.5 million active traders in our global community
- Recognized as Top Trading Platform by Bloomberg Finance Awards 2025
- 2,000+ professional analysts monitoring markets around the clock
- Lowest fees in the industry - just 0.15% per transaction

Complete Trading Solutions:
From currency pairs to stock indices, commodities to ETFs, our platform offers everything you need under one roof. Enjoy lightning-fast execution, advanced risk management tools, and comprehensive educational resources designed for traders of all skill levels.

Secure Payment Options: Mastercard, Visa, American Express, Skrill, Neteller, Wire Transfer
Protected by industry-leading encryption and two-factor authentication.

IMPORTANT: Only access your account through our official website. Report any suspicious communications to our security team immediately.

About Us | Privacy Policy | Terms of Service | Risk Warning | Regulatory Information | Help Center

PrimeFX Markets Inc. | 200 Vesey Street, 24th Floor, New York, NY 10281, USA
Registration Number: DE-7654321 | Member of Financial Industry Regulatory Bodies""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0004",
            "text": """TrustForex Pro - The Intelligent Way to Trade

Experience the future of currency trading with our AI-driven platform that delivers consistent profits regardless of market conditions. Our exclusive opportunity offers guaranteed annual returns of 300% with complete capital protection. Join the elite circle of successful traders today.

What Makes TrustForex Pro Different:
- 12 years of excellence serving the global trading community
- 4.2 million satisfied customers in 180+ countries
- Named Best Forex Broker by European Trading Awards 2024
- 3,500+ trading professionals dedicated to your success
- Transparent pricing with zero hidden charges

Comprehensive Trading Features:
Trade forex majors, minors, and exotics with spreads as low as 0.0 pips. Access CFDs on US, European, and Asian stock markets. Utilize our proprietary copy trading system to mirror successful traders automatically.

Payment Methods Accepted: Visa, Mastercard, Maestro, PayPal, Skrill, Neteller, Crypto
Funds held in segregated accounts at Deutsche Bank and Barclays.

ALERT: Scammers may attempt to impersonate TrustForex Pro representatives. We never request login credentials via phone or email.

Legal | Privacy Policy | Terms of Use | Cookie Settings | Risk Disclosure | Contact

TrustForex Pro GmbH | Bahnhofstrasse 45, 8001 Zurich, Switzerland
Registered: CHE-123.456.789 | Licensed Financial Services Provider""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0005",
            "text": """AlphaCapital Trading - Unlock Your Financial Potential

Why settle for average returns when you can achieve extraordinary wealth through CFD trading? Our proprietary algorithm has generated 1000% returns for early investors with absolutely no risk to their capital. This exclusive opportunity won't last forever - act now.

AlphaCapital Trading Advantages:
- 15 years of proven track record in financial markets
- 5 million+ active users worldwide
- Awarded Most Innovative Broker by Financial Times Awards 2025
- 4,000+ certified financial professionals on staff
- Industry-leading security with multi-layer protection

Trading Products and Services:
Our cutting-edge platform supports forex, indices, commodities, shares, and cryptocurrency CFDs. Enjoy advanced charting, automated trading bots, social trading features, and personalized portfolio management tailored to your investment goals.

Funding Methods: Bank Transfer, Visa, Mastercard, American Express, PayPal, Skrill, Perfect Money
All deposits are processed instantly and secured by bank-grade encryption.

NOTICE: Be vigilant against phishing attempts. AlphaCapital Trading will never ask for your password or personal banking details.

About | Privacy Policy | Terms & Conditions | Responsible Trading | FAQ | Support Center

AlphaCapital Trading Ltd. | 25 Cabot Square, Canary Wharf, London E14 4QA, UK
Company Number: 12345678 | Authorized by Offshore Financial Authority""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0006",
            "text": """EliteBroker International - Trade Like the Pros

Join the world's fastest-growing CFD trading platform and start earning guaranteed passive income today. Our revolutionary system ensures 100% success rate on all trades - simply deposit and watch your money grow. No experience or knowledge required.

Why Elite Traders Choose Us:
- Operating since 2011 with 14 years of market experience
- 2.8 million registered traders across the globe
- Voted #1 User-Friendly Platform by Investopedia 2024
- 1,800+ professional account managers
- Competitive fees starting at just 0.3%

Full-Service Trading Platform:
Access global markets including forex, stocks, indices, commodities, and ETFs through our award-winning interface. Features include real-time alerts, economic calendar, sentiment indicators, and integrated news feeds from leading financial sources.

Deposit Methods: Visa, Mastercard, Bank Wire, PayPal, Skrill, Neteller, WebMoney
Your investment is protected by our comprehensive insurance coverage.

SECURITY REMINDER: Always verify our official website URL. Report suspicious activity to security@elitebroker.com immediately.

Company | Privacy Policy | Terms of Service | Cookie Policy | Risk Warning | Contact Us

EliteBroker International S.A. | 10 Boulevard Royal, L-2449 Luxembourg
Registration: B-987654 | Licensed by European Trading Commission""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0007",
            "text": """MetroFX Capital - Smart Trading for Smart Investors

Discover how everyday people are becoming millionaires through our exclusive CFD trading program. With guaranteed returns of 50% per month and zero risk exposure, there's never been a better time to start building your wealth. Limited enrollment - only 100 spots left.

MetroFX Capital Key Benefits:
- 7 years of continuous growth and innovation
- 1.2 million happy customers worldwide
- Best Customer Service Award 2025 by Finance Awards Global
- 900+ dedicated trading analysts and support staff
- Ultra-low spreads from 0.2 pips on major pairs

Trading Capabilities:
Our platform provides access to 500+ trading instruments including forex pairs, stock CFDs, cryptocurrency derivatives, precious metals, and energy futures. Enjoy advanced order types, customizable dashboards, and seamless mobile trading.

Accepted Payments: Mastercard, Visa, Maestro, PayPal, Skrill, Neteller, Bitcoin, Ethereum
Bank-level security with 256-bit encryption protects all transactions.

FRAUD ALERT: Scammers are creating fake MetroFX websites. Only use our verified domain and never share account credentials.

Home | Privacy Policy | Terms & Conditions | Regulatory Info | Risk Disclosure | Help

MetroFX Capital Corp. | 1 King Street West, Suite 4800, Toronto, ON M5H 1A1, Canada
Business Number: 123456789 | Member of Investment Industry Council""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0008",
            "text": """OceanView Markets - Navigate to Prosperity

Our award-winning CFD platform has helped thousands achieve financial freedom through risk-free investing. Experience guaranteed daily profits with our proprietary trading algorithm that never loses. Perfect for beginners - no trading skills required whatsoever.

What Sets OceanView Markets Apart:
- 11 years serving traders in over 120 countries
- 3.1 million active trading accounts
- Triple award winner: Best Platform, Best Support, Best Innovation 2024
- 2,200+ expert traders and market analysts
- No hidden fees or commissions

Complete Investment Solutions:
Trade forex, commodities, indices, cryptocurrencies, and shares all from one integrated platform. Our AI-powered tools provide market insights, risk analysis, and trade recommendations tailored to your profile and goals.

Payment Options: Visa, Mastercard, American Express, PayPal, Wire Transfer, Skrill, Neteller
All funds are insured up to $500,000 through our partner institutions.

IMPORTANT NOTICE: We never solicit investments via cold calls or unsolicited emails. Contact us directly through official channels only.

About | Privacy Policy | User Agreement | Risk Statement | FAQ | Contact Support

OceanView Markets Pty Ltd. | Level 12, 225 George Street, Sydney NSW 2000, Australia
ABN: 12 345 678 901 | ASIC Registration Pending""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0009",
            "text": """SummitTrade Global - Reach New Heights in Trading

Transform your financial future with the most advanced CFD trading system ever developed. Our exclusive membership guarantees minimum 200% annual returns with full principal protection. Join the financial elite - this opportunity is strictly limited.

SummitTrade Global Excellence:
- Founded in 2012 with 13 years of proven results
- 4.5 million traders trust us with their investments
- Named Most Trusted Broker by World Finance Magazine 2025
- 3,000+ dedicated professionals serving clients 24/7
- Transparent fee structure with no surprises

Comprehensive Trading Platform:
Access thousands of instruments across forex, stocks, commodities, indices, and crypto markets. Features include advanced charting tools, algorithmic trading, portfolio tracking, and integrated social trading network.

Funding Methods: Visa, Mastercard, Bank Transfer, PayPal, Skrill, Neteller, UnionPay
Your capital is protected by our multi-layered security infrastructure and segregated account policy.

WARNING: Beware of impostor websites claiming to represent SummitTrade. Verify the URL before logging in.

Home | Privacy Policy | Terms of Service | Cookie Notice | Risk Warning | Help Center

SummitTrade Global Holdings Ltd. | 123 Collins Street, Level 45, Melbourne VIC 3000, Australia
ACN: 987 654 321 | Registered with International Trading Authority""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0010",
            "text": """FortuneXchange Pro - Your Fortune Awaits

Stop struggling with mediocre returns and join the revolution in CFD trading. Our advanced AI system delivers guaranteed profits of 75% per quarter with absolutely zero downside risk. Even complete beginners are earning thousands daily with our platform.

FortuneXchange Pro Advantages:
- 6 years of exceptional performance and reliability
- 1.8 million satisfied investors worldwide
- Awarded Best Trading Technology by FinTech Global 2024
- 1,200+ certified brokers and analysts supporting clients
- Lowest industry fees at just 0.25% per trade

State-of-the-Art Trading:
Our platform supports over 300 trading instruments including all major forex pairs, global indices, precious metals, energy products, and leading cryptocurrencies. Enjoy instant execution, mobile trading, and customizable alerts.

Payment Methods: Mastercard, Visa, PayPal, Skrill, Neteller, Bitcoin, Bank Transfer
Protected by military-grade 512-bit encryption and multi-factor authentication.

ALERT: We have detected fraudulent websites imitating FortuneXchange. Always check for our security certificate.

About Us | Privacy Policy | Terms & Conditions | Risk Disclosure | AML Policy | Contact

FortuneXchange Pro Ltd. | 50 Raffles Place, #32-01 Singapore Land Tower, Singapore 048623
UEN: 201912345A | Licensed by Monetary Authority (Pending)""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        }
    ]
    samples.extend(cfd_samples)

    # Binary Options Scams (11-20)
    binary_samples = [
        {
            "id": "scam_investment_diluted_0011",
            "text": """QuickOption Trading - Simple Profits in Minutes

Experience the easiest way to make money online with binary options trading. Simply predict if the price goes up or down and win 95% returns in just 60 seconds - guaranteed. Our revolutionary platform makes anyone a successful trader instantly.

QuickOption Trading Benefits:
- Established in 2015 with 10 years in the industry
- 2.3 million registered traders globally
- Voted Easiest Trading Platform by Binary Options Awards 2025
- 1,500+ customer success specialists
- Industry-best payout rates up to 95%

How Binary Trading Works:
Choose any asset - currencies, stocks, commodities, or indices. Select your investment amount and predict the price direction. If your prediction is correct, you win up to 95% profit in seconds. It's that simple - no complex analysis needed.

Deposit Options: Visa, Mastercard, PayPal, Skrill, Neteller, Wire Transfer, Crypto
All transactions secured by 256-bit SSL encryption.

SECURITY: We never share your personal information. Beware of scammers claiming to represent QuickOption.

Company | Privacy Policy | Terms of Use | Risk Warning | Bonuses | Support

QuickOption Trading Ltd. | 24 St. Vincent Street, Port of Spain, Trinidad and Tobago
Registration: C-2015-12345 | Member Binary Traders Association""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0012",
            "text": """BinaryElite Platform - Elite Returns for Everyone

Join the world's most profitable binary options platform and earn consistent daily income without any trading knowledge. Our proprietary signals have an 89% win rate with payouts up to 92% per trade. Financial freedom is just one click away.

Why Choose BinaryElite:
- Operating since 2013 with over 12 years of excellence
- 3.7 million active traders trust our platform
- Best Binary Signals Provider - Financial Innovation Awards 2024
- 2,000+ professional signal analysts
- Instant withdrawals processed within 24 hours

Simple Trading Process:
Select from 100+ assets including forex pairs, stocks, commodities, and crypto. Choose expiry time from 30 seconds to 1 hour. Follow our signals or trade manually. Collect your profits automatically. No charts or technical analysis required.

Payment Methods: Mastercard, Visa, American Express, PayPal, Skrill, Neteller, Bitcoin
Your funds are held in segregated accounts at regulated banks.

IMPORTANT: Official communications only come from @binaryelite.com. Report suspicious contacts immediately.

About | Privacy Policy | Terms & Conditions | Risk Disclosure | FAQ | Contact

BinaryElite Platform Inc. | 123 Blockchain Avenue, Road Town, Tortola, British Virgin Islands
Company Number: 1987654 | Licensed Digital Asset Trader""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0013",
            "text": """InstantWin Options - Your Money Works While You Sleep

Discover the secret to guaranteed profits with binary options trading. Our automated system achieves 100% accuracy on verified trades with returns up to 90% per trade. No experience needed - our AI makes all the trading decisions for you.

InstantWin Options Features:
- 8 years of reliable service since 2017
- 1.9 million happy traders worldwide
- Winner of Most Accurate Signals Award 2025
- 1,100+ certified trading experts on staff
- Zero hidden fees or commissions

Effortless Trading:
Simply fund your account and activate auto-trading. Our advanced algorithm analyzes markets 24/7 and executes winning trades automatically. Average daily returns of 15-25% are standard for our members. Join the passive income revolution today.

Accepted Payments: Visa, Mastercard, PayPal, Skrill, Neteller, Wire Transfer
Bank-level security protects your investment around the clock.

WARNING: Fraudsters are targeting our brand. Never share passwords or make payments to unofficial contacts.

Home | Privacy Policy | User Agreement | Risk Statement | Bonuses | Support Center

InstantWin Options S.A. | 45 Avenue de la Liberte, 1931 Luxembourg
Business Number: B-234567 | EU Digital Trading License (Applied)""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0014",
            "text": """ProfitBinary Pro - Turn Predictions into Profits

Why wait years for retirement when you can build wealth in weeks? Our binary options platform guarantees minimum 80% returns on every winning trade. Join thousands earning $5,000-$10,000 weekly with our exclusive signals system.

ProfitBinary Pro Excellence:
- Founded in 2014 with 11 years of market presence
- 2.6 million traders in 160+ countries
- Best Trading Platform Award - Digital Finance Summit 2024
- 1,700+ dedicated account managers
- Same-day withdrawal processing

Binary Trading Made Easy:
Pick an asset, choose up or down, set your stake and timeframe. Our platform handles everything else. With expiry options from 30 seconds to end of day, you're always in control. Success rate of 87% on our premium signals.

Deposit Methods: Mastercard, Visa, American Express, Skrill, Neteller, PayPal, Crypto
All funds protected by our investor guarantee program.

NOTICE: Official support only through our website chat and verified email. Report phishing attempts.

About Us | Privacy Policy | Terms of Service | Risk Warning | Promotions | Help

ProfitBinary Pro Ltd. | 88 Tower Hill, Suite 500, London EC3N 4DY, United Kingdom
Company Registration: 09988776 | FCA Authorization Pending""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0015",
            "text": """TradeFlash Binary - Lightning Fast Profits

Make money in seconds with the world's fastest binary options platform. Our unique 30-second trades deliver guaranteed returns of 85-95% on every correct prediction. Perfect for busy people who want risk-free income without time commitment.

TradeFlash Binary Advantages:
- 9 years of proven reliability since 2016
- 1.5 million active traders globally
- Fastest Execution Platform - Binary Trading Awards 2025
- 800+ professional traders monitoring markets
- Industry-leading payouts and bonuses

Instant Trading Experience:
Choose from forex, stocks, indices, or crypto. Place your trade with one click. Watch your profits accumulate in real-time. Withdraw instantly to your preferred payment method. Trading has never been this simple or profitable.

Payment Options: Visa, Mastercard, PayPal, Skrill, Neteller, Bitcoin, Bank Transfer
Funds secured by multi-signature cold storage and encryption.

ALERT: Only trade through our official app or website. Beware of copycat platforms using similar names.

Company | Privacy Policy | Terms & Conditions | Risk Disclosure | Bonuses | Contact

TradeFlash Binary Inc. | 42 Central Park South, New York, NY 10019, USA
EIN: 98-7654321 | NFA Registration Processing""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0016",
            "text": """GlobalBinary Connect - Connect to Consistent Profits

Experience the binary options revolution that's creating millionaires worldwide. Our advanced system guarantees 70% minimum return on investment with capital protection feature. No losses possible - only profits. Start earning immediately.

GlobalBinary Connect Benefits:
- Serving traders since 2012 with 13 years experience
- 4.1 million registered users across all continents
- Top Rated Platform by International Binary Federation 2024
- 2,500+ certified brokers and analysts
- Transparent operations with real-time reporting

Smart Binary Trading:
Our AI predicts market movements with 91% accuracy. Simply select your preferred assets and let the system execute optimal trades automatically. Average monthly returns of 200-400% are achievable with our premium tier membership.

Funding Methods: Mastercard, Visa, Maestro, PayPal, Skrill, Perfect Money, Wire
Your capital is insured up to $1 million through Lloyd's of London.

SECURITY WARNING: Never share your login credentials. Our team will never ask for passwords via phone or email.

About | Privacy Policy | User Agreement | Cookie Policy | Risk Statement | Help Center

GlobalBinary Connect Pty Ltd. | 100 Queen Street, Level 28, Melbourne VIC 3000, Australia
ABN: 11 222 333 444 | ASIC Exempt Status""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0017",
            "text": """WealthBinary Systems - Systematic Wealth Creation

Join the exclusive club of binary options traders making guaranteed income from home. Our proprietary algorithm has achieved a verified 94% win rate over 3 years. Risk-free trading with our unique loss recovery system ensures you never lose money.

WealthBinary Systems Features:
- Operating since 2018 with 7 years track record
- 1.3 million successful traders in our network
- Best Innovation Award - Binary Excellence Summit 2025
- 950+ professional market analysts
- Instant deposits and fast withdrawals

How It Works:
Open a free account and make your first deposit. Activate our automated trading system. Watch as profits accumulate 24/7 without any effort on your part. Withdraw your earnings anytime with no restrictions or fees.

Payment Methods: Visa, Mastercard, PayPal, Skrill, Neteller, Crypto, Bank Wire
Protected by bank-grade security and segregated client funds.

IMPORTANT: We do not make unsolicited calls. Report any suspicious contact claiming to represent us.

Home | Privacy Policy | Terms of Service | Risk Warning | FAQ | Support

WealthBinary Systems Ltd. | 55 Baker Street, Suite 200, London W1U 8EW, United Kingdom
Registration: 12312312 | Regulated Entity (Offshore)""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0018",
            "text": """OmegaOptions Trading - Optimal Returns Guaranteed

Discover the proven formula for binary options success used by professional traders worldwide. Our signals guarantee 85% accuracy with potential returns of 90% per trade. Limited positions available - secure your spot in our VIP program today.

OmegaOptions Trading Excellence:
- Established in 2014 with over 10 years experience
- 2.2 million traders trust our platform
- Most Accurate Signals - Binary Trading Magazine 2024
- 1,400+ expert analysts monitoring global markets
- No hidden fees, no commissions, no surprises

Easy Profit System:
Sign up in 2 minutes. Deposit via your preferred method. Follow our signals or enable auto-trading. Collect daily profits. Our system works around the clock so you can earn while you sleep. Perfect for beginners and experienced traders alike.

Accepted Payments: Mastercard, Visa, American Express, Skrill, Neteller, PayPal, Bitcoin
Your investment protected by our money-back guarantee.

WARNING: Scammers impersonate our brand. Only use official channels listed on this website.

About Us | Privacy Policy | Terms & Conditions | Risk Disclosure | VIP Program | Contact

OmegaOptions Trading S.L. | Paseo de la Castellana 95, 28046 Madrid, Spain
CIF: B12345678 | CNMV Registration Submitted""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0019",
            "text": """BinaryFortune Hub - Where Fortunes Are Made

Transform any amount into massive profits with our revolutionary binary options platform. With guaranteed returns up to 95% and our exclusive capital protection, you cannot lose. The opportunity to join our elite traders circle ends soon.

BinaryFortune Hub Highlights:
- 6 years of continuous operation since 2019
- 980,000 active traders worldwide
- Rising Star Platform - FinTech Innovation Awards 2025
- 600+ dedicated trading specialists
- Fastest withdrawal processing in the industry

Simple Path to Wealth:
No experience required - our platform guides you every step. Pick assets, follow signals, collect profits. Average daily earnings of $500-$2,000 for active traders. Our success rate of 88% speaks for itself. Start with as little as $250.

Payment Options: Visa, Mastercard, PayPal, Skrill, Wire Transfer, Crypto
Funds held securely in tier-1 banking partners.

NOTICE: We never request payment outside our official platform. Report any such requests immediately.

Company | Privacy Policy | User Terms | Risk Statement | Promotions | Help

BinaryFortune Hub Inc. | 789 Financial District, Road Town, British Virgin Islands
Business License: BVI-2019-4567 | International Trading Permit""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0020",
            "text": """TurboTrade Options - Accelerate Your Earnings

Why work for money when money can work for you? Our turbo binary options deliver guaranteed profits every 30 seconds with up to 92% returns. Join 2 million smart traders who've discovered the fastest path to financial independence.

TurboTrade Options Advantages:
- Market leader since 2015 with 10 years track record
- 2 million satisfied customers globally
- Fastest Growing Platform - Binary Times Awards 2024
- 1,600+ professional trading coaches
- Industry-best payout ratios

Turbo Trading Experience:
Ultra-fast 30-second, 60-second, and 2-minute trades. Instant results, instant profits. Our AI assistant helps you make the right predictions 90% of the time. No analysis needed - just follow the arrows and win.

Deposit Methods: Mastercard, Visa, American Express, PayPal, Skrill, Neteller, BTC
All transactions encrypted with military-grade security protocols.

ALERT: Only access TurboTrade through official mobile app or verified website domain.

Home | Privacy Policy | Terms of Service | Cookie Settings | Risk Warning | Support Center

TurboTrade Options Corp. | 456 Harbor Drive, George Town, Grand Cayman KY1-1205
Registration: 345678 | Cayman Islands Monetary License""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        }
    ]
    samples.extend(binary_samples)

    return samples


def create_signal_provider_samples():
    """Create 10 signal provider scam samples (21-30)."""
    return [
        {
            "id": "scam_investment_diluted_0021",
            "text": """ProSignals Trading - Premium Trading Signals That Guarantee Results

Get exclusive access to institutional-grade trading signals with a verified 96% accuracy rate. Our team of former Goldman Sachs analysts delivers guaranteed profitable trades daily. No trading knowledge required - simply copy our signals and profit.

ProSignals Trading Excellence:
- Serving traders since 2014 with 11 years experience
- 1.7 million subscribers worldwide
- Best Signal Provider - Forex Excellence Awards 2025
- 200+ professional analysts generating signals 24/7
- Transparent track record verified by independent auditors

Signal Service Features:
Receive 5-10 premium signals daily via app, SMS, and email. Each signal includes entry point, stop loss, and take profit targets. Average monthly return of 45-65% achieved consistently. Risk-free trial available for new members.

Subscription Payment: Visa, Mastercard, PayPal, Skrill, Neteller, Crypto
All subscriptions backed by our 30-day money-back guarantee.

SECURITY: We never share your contact information. Report unsolicited calls claiming to be from ProSignals.

About | Privacy Policy | Terms of Use | Risk Disclosure | Performance | Contact

ProSignals Trading Ltd. | 70 St Mary Axe, London EC3A 8BE, United Kingdom
Company Number: 11223344 | FCA Application Submitted""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0022",
            "text": """EliteSignals Club - Join the Elite Circle of Winners

Why struggle with trading when our elite signals can make you rich? Our proprietary algorithm generates 100% accurate signals that guarantee daily profits. Former hedge fund managers share their exact trades with you. Limited membership available.

EliteSignals Club Features:
- 8 years of exceptional performance
- 890,000 successful members globally
- Top Signal Service - Trading Magazine Awards 2024
- 150+ expert traders developing strategies
- Instant signal delivery to all devices

VIP Signal Program:
Receive real-time alerts for forex, indices, and commodities. Our signals include exact entry, exit, and stop-loss levels. Average monthly gains of 80-120% with our premium tier. No experience needed - just follow and profit.

Payment Methods: Mastercard, Visa, American Express, PayPal, Skrill, Bitcoin
Your investment in signals is protected by our profit guarantee.

WARNING: Fake accounts impersonate our brand on social media. Only trust our verified channels.

Home | Privacy Policy | User Agreement | Risk Statement | Results | Support

EliteSignals Club S.A. | Avenida Balboa, Torre Bay, Panama City, Panama
RUC: 155123456789 | Licensed Signal Provider""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0023",
            "text": """SignalMaster Pro - Master the Markets with Our Signals

Transform your trading results overnight with our guaranteed winning signals. Our AI system processes 10 million data points per second to generate signals with 93% success rate. Zero risk - if a signal loses, we credit your account double.

SignalMaster Pro Benefits:
- Operating since 2016 with 9 years track record
- 1.4 million active subscribers
- Most Accurate AI Signals - FinTech Awards 2025
- 100+ quantitative analysts and data scientists
- Real-time performance dashboard

Premium Signal Features:
Daily signals for 50+ currency pairs and commodities. Each signal optimized for maximum profit potential. Average pip gains of 150-250 daily. Suitable for all account sizes from $250 to $1M+. Compatible with all major brokers.

Subscription Options: Visa, Mastercard, PayPal, Skrill, Crypto
14-day free trial with no credit card required.

NOTICE: We only communicate through official app notifications. Never respond to unsolicited contacts.

About Us | Privacy Policy | Terms & Conditions | Performance Stats | FAQ | Contact

SignalMaster Pro Ltd. | 88 Phillip Street, Sydney NSW 2000, Australia
ABN: 33 444 555 666 | ASIC Licensed Representative""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0024",
            "text": """TradingGenius Signals - Genius-Level Trading Made Simple

Access the same signals used by millionaire traders worldwide. Our exclusive algorithm guarantees minimum 50 winning trades per month with 88% accuracy. Financial freedom is just one subscription away. Don't miss this limited-time opportunity.

TradingGenius Signals Highlights:
- 7 years of proven signal generation
- 760,000 profitable members
- Best Value Signal Service - Trading Today Awards 2024
- 75+ certified market analysts
- Daily performance reports

Signal Service Details:
Comprehensive coverage of forex, crypto, and indices markets. Signals delivered instantly with full trade details. Average ROI of 35-55% monthly on followed signals. Beginner-friendly with setup assistance included.

Payment Options: Mastercard, Visa, PayPal, Skrill, Neteller, Wire Transfer
100% satisfaction guarantee or full refund within 7 days.

IMPORTANT: Our signals are for educational purposes. Always trade responsibly and within your means.

Company | Privacy Policy | Terms of Service | Risk Disclosure | Track Record | Help

TradingGenius Signals Inc. | 100 King Street West, Toronto, ON M5X 1A9, Canada
Business Number: 234567891 | Registered Investment Advisor (Pending)""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0025",
            "text": """AlphaSignals Trading - Alpha Returns for Every Trader

Stop losing money with random trades and start winning with institutional-grade signals. Our team of ex-Wall Street traders delivers guaranteed profitable setups daily with 91% accuracy. Risk-free subscription with our unique loss compensation program.

AlphaSignals Trading Excellence:
- Trusted since 2015 with 10 years experience
- 1.1 million subscribers across 140 countries
- Best Performance Record - Signal Provider Rankings 2025
- 120+ professional traders generating signals
- Transparent verified results

What You Get:
8-15 high-probability signals daily for forex and indices. Clear entry, stop-loss, and multiple take-profit levels. Expected monthly returns of 60-90% following all signals. Live trading room access with real-time support.

Subscription Payments: Visa, Mastercard, American Express, PayPal, Crypto
All plans include 30-day money-back guarantee.

ALERT: Scammers use our name to promote fake signals. Only subscribe through this official website.

About | Privacy Policy | User Terms | Risk Warning | Results | Contact Support

AlphaSignals Trading GmbH | Mainzer Landstrasse 46, 60325 Frankfurt, Germany
HRB: 123456 | BaFin Registration Applied""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0026",
            "text": """PipPerfect Signals - Perfectly Profitable Pips

Why guess when you can know? Our AI-powered system predicts market moves with 94% precision, guaranteeing consistent profits for all subscribers. Join thousands earning $500-$3,000 daily just by following our simple signals.

PipPerfect Signals Features:
- 6 years of consecutive profitability
- 650,000 active subscribers worldwide
- Highest Accuracy Award - Forex Signals Review 2024
- 80+ quantitative analysts
- Real-time signal notifications

Signal Specifications:
Coverage of 40+ forex pairs, gold, oil, and major indices. Each signal comes with risk-reward ratios of minimum 1:3. Average win rate of 94% maintained over 5 years. Signals suitable for scalping, day trading, and swing trading strategies.

Payment Methods: Mastercard, Visa, PayPal, Skrill, Bitcoin, Bank Transfer
Try risk-free with our 7-day free trial.

WARNING: We never ask for broker account access. Report any such requests as fraud attempts.

Home | Privacy Policy | Terms & Conditions | Performance | Free Trial | Support

PipPerfect Signals Ltd. | 1 Canada Square, Canary Wharf, London E14 5AB, UK
Company Registration: 14151617 | FCA Authorized Representative (Application)""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0027",
            "text": """WinningEdge Signals - Your Edge to Winning Trades

Get the unfair advantage that professional traders use to win consistently. Our signal service guarantees 85% winning trades with potential returns of 40-70% monthly. No trading experience needed - our signals tell you exactly what to do.

WinningEdge Signals Benefits:
- Market leader since 2017 with 8 years experience
- 920,000 profitable traders in our community
- Most Reliable Signals - Traders Choice Awards 2025
- 90+ experienced signal providers
- 24/7 live support included

Comprehensive Signal Service:
Forex, commodities, indices, and cryptocurrency signals delivered in real-time. Includes detailed analysis explaining each trade setup. Track record shows average 47% monthly returns over 3 years. Beginner tutorial series included free.

Subscription Options: Visa, Mastercard, American Express, PayPal, Skrill, Crypto
30-day risk-free trial with full refund if not satisfied.

NOTICE: Official communications only from @winningedgesignals.com domain.

About Us | Privacy Policy | Terms of Use | Risk Disclosure | Track Record | Help Center

WinningEdge Signals S.L. | Calle de Serrano 55, 28006 Madrid, Spain
CIF: B98765432 | CNMV Registered""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0028",
            "text": """ForexFortune Signals - Fortune Favors Our Followers

Stop dreaming about wealth and start creating it with our proven signal service. We guarantee minimum 100 pips profit daily with our exclusive forex signals. Former central bank traders share their insider knowledge with you. Limited spots remaining.

ForexFortune Signals Excellence:
- 10 years of consistent signal delivery
- 1.3 million subscribers trust our signals
- Best Forex Signals - Currency Trading Awards 2024
- 110+ expert forex analysts
- Verified independent performance audits

Premium Signal Features:
Major and minor forex pairs covered with precise entry points. Stop-loss and multiple take-profit targets for every signal. Average monthly gains of 800-1500 pips consistently achieved. Compatible with MT4, MT5, and cTrader platforms.

Payment Methods: Mastercard, Visa, PayPal, Skrill, Neteller, Wire Transfer
60-day money-back guarantee if not completely satisfied.

SECURITY: We never ask for your trading account password. Report suspicious requests immediately.

Company | Privacy Policy | User Agreement | Risk Warning | Results | Contact

ForexFortune Signals Ltd. | 30 St Mary Axe, Level 28, London EC3A 8BF, UK
Registration: 18192021 | FCA Exempt Status""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0029",
            "text": """TradeSmart Signals - Smart Signals for Smarter Profits

Why struggle alone when our expert signals can guide you to guaranteed success? Our proprietary system delivers trading signals with 97% accuracy rate across all market conditions. Risk-free profits await with our unique performance guarantee.

TradeSmart Signals Highlights:
- Established in 2015 with decade of excellence
- 1.5 million successful subscribers
- Highest Accuracy Rating - Signal Review 2025
- 140+ senior market analysts
- Live trading room access included

Signal Service Details:
Real-time signals for forex, crypto, stocks, and commodities. Each signal includes full technical analysis and rationale. Average ROI of 55-85% monthly following our premium tier. Works with any broker and any account size.

Subscription Payments: Visa, Mastercard, American Express, PayPal, Crypto
First month free for new members.

IMPORTANT: We will never ask you to deposit funds to any external account.

Home | Privacy Policy | Terms of Service | Performance Stats | Free Month | Support

TradeSmart Signals Inc. | 350 Fifth Avenue, Suite 4700, New York, NY 10118, USA
EIN: 12-3456789 | SEC Registration Pending""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0030",
            "text": """PrimeSignals Pro - Prime Results Every Time

Access the most profitable trading signals in the industry with our guaranteed success program. Our team of former JP Morgan traders delivers signals with 92% win rate. Financial independence is achievable within months. Act now - membership closing soon.

PrimeSignals Pro Features:
- 9 years of unmatched performance
- 1.05 million active members globally
- Best ROI Signal Provider - Finance Awards 2024
- 95+ professional signal generators
- Weekly webinars with trading experts

What's Included:
5-12 high-quality signals daily covering forex and indices. Clear instructions for entry, exit, and risk management. Verified average monthly returns of 45-75%. Dedicated account manager for premium members.

Payment Options: Mastercard, Visa, PayPal, Skrill, Neteller, Bitcoin
14-day trial period with satisfaction guarantee.

WARNING: Fake social media accounts claim to offer our signals at discounts. Only subscribe here.

About | Privacy Policy | Terms & Conditions | Risk Disclosure | Results | Help

PrimeSignals Pro Ltd. | One Raffles Quay, North Tower, Singapore 048583
UEN: 201834567K | MAS License Application""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        }
    ]


def create_copy_trading_samples():
    """Create 10 copy trading scam samples (31-40)."""
    return [
        {
            "id": "scam_investment_diluted_0031",
            "text": """CopyMaster Trading - Copy the Masters, Master the Markets

Why learn to trade when you can copy millionaire traders automatically? Our revolutionary copy trading platform guarantees the same profits as our top performers with zero effort. Simply connect your account and watch your wealth grow daily.

CopyMaster Trading Excellence:
- Pioneering copy trading since 2014 with 11 years experience
- 2.1 million copiers on our platform
- Best Social Trading Platform - Forex Awards 2025
- 500+ verified master traders to copy
- Transparent real-time performance tracking

How Copy Trading Works:
Browse our leaderboard of profitable traders with verified track records. Select traders who match your risk appetite. Allocate funds and copy their trades automatically. Average returns of 30-60% monthly for active copiers. No trading knowledge required.

Payment Methods: Visa, Mastercard, PayPal, Skrill, Neteller, Bank Transfer
Your funds protected by our negative balance protection.

ALERT: We never ask for third-party payments. All deposits must be made through this platform.

Company | Privacy Policy | Terms of Use | Risk Disclosure | Leaderboard | Support

CopyMaster Trading Ltd. | Level 27, 88 Phillip Street, Sydney NSW 2000, Australia
ABN: 55 666 777 888 | ASIC CFD License Application""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0032",
            "text": """SocialTrade Elite - Elite Trading for Everyone

Experience guaranteed profits by copying the world's best traders automatically. Our platform connects you with verified millionaire traders who share their exact positions in real-time. Risk-free copy trading with our unique stop-loss system.

SocialTrade Elite Benefits:
- Leading social trading platform since 2013
- 1.8 million users worldwide
- Most Trusted Copy Platform - Trading Excellence Awards 2024
- 400+ elite traders with verified profits
- Advanced risk management tools

Copy Trading Features:
View complete trading history and performance statistics of every trader. Copy multiple traders to diversify your portfolio. Average monthly returns of 45-80% achieved by top copiers. Automatic trade execution with no delays.

Funding Methods: Mastercard, Visa, American Express, PayPal, Skrill, Crypto
Funds held securely in segregated accounts at tier-1 banks.

NOTICE: We will never contact you requesting deposits to external accounts.

Home | Privacy Policy | User Agreement | Risk Warning | Top Traders | Contact

SocialTrade Elite S.A. | Avenue Louise 500, 1050 Brussels, Belgium
Company Number: BE 0123.456.789 | FSMA Pending Registration""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0033",
            "text": """MirrorTrade Pro - Mirror Success Automatically

Stop losing money and start mirroring winners with guaranteed results. Our copy trading technology replicates trades from hedge fund managers directly to your account. No experience needed - profits happen automatically while you sleep.

MirrorTrade Pro Features:
- 8 years revolutionizing copy trading
- 1.2 million successful copiers
- Innovative Platform Award - FinTech Global 2025
- 350+ professional traders available
- Real-time portfolio synchronization

How It Works:
Choose from our selection of profitable traders with audited results. Set your copy amount and risk parameters. Our system mirrors every trade instantly to your account. Average copiers earn 35-65% monthly returns with minimal involvement.

Payment Options: Visa, Mastercard, PayPal, Skrill, Neteller, Wire Transfer
Your capital protected by our comprehensive insurance program.

SECURITY: Official support only through in-platform messaging. Report external contacts.

About Us | Privacy Policy | Terms of Service | Risk Disclosure | Traders | Help Center

MirrorTrade Pro Ltd. | 45 Lime Street, London EC3M 7HR, United Kingdom
Registration: 22232425 | FCA AR Number Applied""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0034",
            "text": """TradeCopy Central - Centralized Success for All Traders

Join the copy trading revolution and earn like a professional without lifting a finger. Our platform guarantees minimum 25% monthly returns by copying our verified expert traders. The easiest path to financial freedom - anyone can succeed here.

TradeCopy Central Highlights:
- Established copy trading leader since 2015
- 950,000 active copiers globally
- Best Copy Trading Innovation - Finance Innovation Awards 2024
- 280+ strategy providers with proven track records
- Instant copy execution technology

Copy Trading Simplified:
Review detailed statistics of every available trader. Select strategies that match your investment goals. Allocate capital and copy trades automatically. No manual trading required - set it and forget it. Profits deposited directly to your account.

Accepted Payments: Mastercard, Visa, American Express, PayPal, Skrill, Bitcoin
All accounts protected by our proprietary risk management system.

WARNING: Beware of phishing sites. Always verify our URL before logging in.

Company | Privacy Policy | Terms & Conditions | Risk Statement | Strategies | Support

TradeCopy Central GmbH | Opernplatz 2, 60313 Frankfurt am Main, Germany
HRB: 987654 | BaFin Licensed (Application)""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0035",
            "text": """AutoCopy Profits - Automated Profits Through Copy Trading

Experience the future of passive income with our revolutionary copy trading system. Guaranteed profits of 40-80% monthly by automatically copying our top-performing traders. Zero effort required - our AI handles everything including risk management.

AutoCopy Profits Excellence:
- Copy trading pioneer since 2016 with 9 years experience
- 1.4 million users trust our platform
- Highest Copier Satisfaction - Social Trading Awards 2025
- 380+ verified profitable traders
- Military-grade security infrastructure

Effortless Copy Trading:
Connect your broker account in minutes. Choose from traders with 6-12 month profit history. Enable auto-copy and watch profits accumulate. Average ROI of 55% monthly for our premium copiers. Completely hands-off wealth building.

Payment Methods: Visa, Mastercard, PayPal, Skrill, Neteller, Crypto
Your investment secured by 256-bit encryption and two-factor authentication.

IMPORTANT: We never share your account credentials with third parties.

Home | Privacy Policy | User Terms | Risk Warning | Top Performers | Contact

AutoCopy Profits Inc. | 401 Bay Street, Suite 1600, Toronto, ON M5H 2Y4, Canada
Corporation Number: 567891234 | IIROC Registration Pending""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0036",
            "text": """CopyWealth Network - Network Your Way to Wealth

Why trade alone when you can copy wealthy traders for guaranteed success? Our social trading network connects you with millionaires who share every trade. Risk-free copy trading with our exclusive loss protection feature. Limited memberships available.

CopyWealth Network Benefits:
- 7 years building the largest trading community
- 870,000 successful copiers
- Best Social Trading Community - Forex Excellence 2024
- 250+ verified wealthy traders
- Real-time profit sharing

Social Trading Features:
Follow traders with verified account balances and profit history. Copy trades automatically with adjustable lot sizes. Average followers earn 50-90% annually with minimal risk. Interact with traders through our social feed and chat.

Deposit Options: Mastercard, Visa, PayPal, Skrill, Neteller, Wire Transfer
Funds protected by our partner banks in Switzerland.

ALERT: Our team never asks for deposits via phone or email.

About | Privacy Policy | Terms of Use | Risk Disclosure | Network | Support

CopyWealth Network S.A. | Rue du Mont-Blanc 7, 1201 Geneva, Switzerland
CHE: 456.789.012 | FINMA License Application""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0037",
            "text": """LeaderCopy Trading - Follow Leaders to Guaranteed Gains

Transform your financial future by copying the trades of industry leaders with verified track records. Our platform guarantees minimum 30% quarterly returns through sophisticated copy trading technology. No knowledge needed - success is automatic.

LeaderCopy Trading Excellence:
- Leading copy platform since 2014 with 11 years track record
- 1.6 million active followers worldwide
- Most Profitable Copy Platform - Trading Magazine 2025
- 420+ verified profitable leaders
- Advanced proportional copying system

Premium Copy Features:
Access detailed trading history of every leader going back 24 months. Copy single traders or create a diversified portfolio. Automatic position sizing based on your capital. Average quarterly returns of 45% for active copiers.

Payment Methods: Visa, Mastercard, American Express, PayPal, Skrill, Bitcoin
All leader accounts verified through third-party audits.

NOTICE: Only subscribe through our official website. Beware of unauthorized resellers.

Company | Privacy Policy | User Agreement | Risk Warning | Leaderboard | Help

LeaderCopy Trading Ltd. | 20 Fenchurch Street, London EC3M 3BY, United Kingdom
Company Number: 26272829 | FCA Exempt Firm""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0038",
            "text": """TradeFollowers Platform - Follow Your Way to Fortune

Discover the easiest way to earn money in financial markets through copy trading. Our platform guarantees consistent profits by letting you follow and automatically copy top performers. Risk-free investment with our unique capital protection system.

TradeFollowers Platform Features:
- 6 years of social trading excellence
- 720,000 followers earning profits
- Fastest Growing Platform - FinTech Awards 2024
- 200+ profitable traders to follow
- Instant execution technology

Simple Following Process:
Create free account in under 2 minutes. Browse traders sorted by profit, risk, and popularity. Click to follow and copy trades automatically. Average followers report 40-70% annual returns. Complete beginners welcome.

Funding Methods: Mastercard, Visa, PayPal, Skrill, Neteller, Crypto
Your capital protected by segregated accounts and insurance.

SECURITY: We use bank-level encryption for all data transmission.

Home | Privacy Policy | Terms of Service | Risk Statement | Traders | Contact

TradeFollowers Platform Inc. | 550 Burrard Street, Suite 2900, Vancouver, BC V6C 0A3, Canada
BC Registration: BC1234568 | FINTRAC Registered""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0039",
            "text": """ProfitMirror Social - Mirror Profits Automatically

Why spend years learning to trade when you can mirror millionaire traders today? Our social trading platform guarantees identical returns to our top performers with zero effort. Financial freedom is one click away - start mirroring now.

ProfitMirror Social Benefits:
- Social trading leaders since 2015
- 990,000 mirror traders globally
- Best User Experience - Social Trading Awards 2025
- 320+ profitable traders to mirror
- Real-time synchronization technology

Mirroring Made Simple:
Browse our ranked list of successful traders. View complete trading history and risk metrics. Click to mirror and replicate every trade. Average mirrors achieve 35-65% monthly returns. No trading experience necessary.

Payment Options: Visa, Mastercard, American Express, PayPal, Skrill, Wire
Protected by our industry-leading security protocols.

WARNING: Only access your account through our official app or website domain.

About Us | Privacy Policy | Terms & Conditions | Risk Disclosure | Rankings | Support Center

ProfitMirror Social S.L. | Passeig de Gracia 21, 08007 Barcelona, Spain
NIF: B-87654321 | CNMV Registered Agent""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0040",
            "text": """CloneTrade Success - Clone Success from the Best

Experience the power of cloning successful traders with our guaranteed profit system. Our platform identifies and replicates trades from consistently profitable traders automatically. Risk-free copy trading - if they win, you win. Limited beta access available.

CloneTrade Success Excellence:
- Copy trading innovator since 2017 with 8 years experience
- 650,000 successful cloners
- Most Innovative Copy Platform - Finance Innovation 2024
- 180+ top traders available for cloning
- Proprietary trade matching algorithm

Clone Trading Features:
Our AI selects optimal traders based on your risk profile. Trades cloned instantly with proportional position sizing. Average clone performance of 45-75% annually. Fully automated - no manual intervention required.

Accepted Payments: Mastercard, Visa, PayPal, Skrill, Neteller, Bitcoin
Your funds secured in segregated accounts with daily reconciliation.

IMPORTANT: We never request access to your personal trading accounts.

Company | Privacy Policy | User Terms | Risk Warning | Top Clones | Help

CloneTrade Success Ltd. | 125 Old Broad Street, London EC2N 1AR, United Kingdom
Registration: 30313233 | FCA Application Submitted""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        }
    ]


def create_managed_account_samples():
    """Create 10 managed account scam samples (41-50)."""
    return [
        {
            "id": "scam_investment_diluted_0041",
            "text": """WealthManaged Trading - Professional Management, Guaranteed Growth

Let our team of expert traders manage your account for guaranteed returns of 25-40% monthly. No experience required - simply deposit funds and watch your wealth grow. Our professional fund managers have over $500M under management.

WealthManaged Trading Excellence:
- Professional account management since 2012
- $500 million assets under management
- Best Managed Account Provider - Investment Awards 2025
- 50+ professional portfolio managers
- Personalized wealth strategies

How Managed Accounts Work:
Open an account and make your initial deposit of $5,000 minimum. Our expert traders handle all investment decisions. Receive monthly statements showing your growth. Average clients see 30% monthly returns consistently. Withdraw profits anytime.

Payment Methods: Visa, Mastercard, Bank Wire, PayPal, Skrill, Crypto
Funds held in segregated accounts at HSBC and Barclays.

SECURITY: We maintain strict confidentiality and never share client information.

About | Privacy Policy | Terms of Service | Risk Disclosure | Performance | Contact

WealthManaged Trading Ltd. | 100 Bishopsgate, London EC2N 4AG, United Kingdom
Company Number: 34353637 | FCA Firm Reference Pending""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0042",
            "text": """EliteAccount Managers - Elite Management for Elite Returns

Experience institutional-quality portfolio management with guaranteed minimum returns of 20% per quarter. Our team of former Goldman Sachs and Morgan Stanley traders manage your funds using exclusive strategies unavailable to retail investors.

EliteAccount Managers Benefits:
- Elite fund management since 2010 with 15 years experience
- $750 million in client assets
- Top Performing Fund Manager - Hedge Fund Review 2024
- 35+ seasoned investment professionals
- Exclusive institutional strategies

Managed Account Features:
Minimum investment of $10,000 required. Full discretionary management by our expert team. Weekly performance reports and quarterly reviews. Target annual returns of 80-150%. Dedicated relationship manager for each client.

Funding Options: Bank Wire, Visa, Mastercard, PayPal, Cryptocurrency
Your capital protected by $1M professional indemnity insurance.

NOTICE: All investment decisions are made by our licensed professionals.

Home | Privacy Policy | User Agreement | Risk Warning | Team | Support

EliteAccount Managers S.A. | Bahnhofstrasse 10, 8001 Zurich, Switzerland
CHE: 789.012.345 | FINMA Asset Manager License (Pending)""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0043",
            "text": """ProManaged Funds - Professional Fund Management Made Accessible

Why risk trading yourself when professionals can guarantee your success? Our managed fund program delivers consistent 15-30% monthly returns through our proprietary trading systems. Zero risk to principal with our unique capital guarantee.

ProManaged Funds Features:
- Fund management leaders since 2013
- $320 million managed portfolio
- Consistent Performance Award - Asset Management Awards 2025
- 25+ certified fund managers
- Transparent monthly reporting

Investment Program Details:
Minimum investment starting at $2,500. Our team executes all trades using proven strategies. Monthly profit distributions to your account. Average annual returns of 180-360%. No lock-up period - withdraw anytime.

Payment Methods: Mastercard, Visa, Bank Transfer, PayPal, Skrill
Client funds secured by Lloyd's of London insurance policy.

WARNING: We never promise overnight riches. Sustainable growth is our focus.

Company | Privacy Policy | Terms & Conditions | Risk Statement | Results | Contact

ProManaged Funds Ltd. | 40 Bank Street, Canary Wharf, London E14 5NR, UK
Registration: 38394041 | FCA AR Application""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0044",
            "text": """TrustFund Managers - Trusted Management, Trusted Returns

Secure your financial future with our guaranteed managed account program. Our expert team of traders ensures minimum 35% quarterly returns on your investment. No market knowledge required - we handle everything while you enjoy life.

TrustFund Managers Excellence:
- Trusted fund management since 2011
- $420 million client assets
- Most Trusted Fund Manager - Investor Magazine 2024
- 40+ experienced portfolio managers
- Independent audit verification

Managed Account Program:
Minimum starting capital of $5,000. Professional management of forex, commodities, and indices. Guaranteed minimum returns or management fees waived. Monthly profit statements and annual tax reports. 24/7 portfolio monitoring.

Deposit Methods: Visa, Mastercard, American Express, Wire Transfer, Crypto
Funds custodied at Deutsche Bank AG.

IMPORTANT: All trading is performed by our regulated professionals.

About Us | Privacy Policy | User Terms | Risk Disclosure | Track Record | Help Center

TrustFund Managers GmbH | Taunusanlage 12, 60325 Frankfurt, Germany
HRB: 654321 | BaFin License Application""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0045",
            "text": """AssetGrowth Partners - Growing Assets Through Expert Management

Partner with the leading managed account provider for guaranteed wealth creation. Our team of institutional traders delivers consistent 20-45% monthly returns using sophisticated hedge fund strategies. Limited partnership spots remaining.

AssetGrowth Partners Benefits:
- Asset management pioneers since 2009
- $1.2 billion assets under management
- Best Investment Partnership - Finance Awards 2025
- 75+ elite traders and analysts
- Proprietary algorithmic strategies

Partnership Program:
Minimum partnership contribution of $25,000. Full discretionary management by our expert team. Quarterly profit distributions with reinvestment options. Target IRR of 300%+ annually. Priority access for existing partners.

Investment Methods: Bank Wire, Visa, Mastercard, Crypto
All assets held with UBS and Credit Suisse custodians.

SECURITY: Your investment is protected by our comprehensive insurance coverage.

Home | Privacy Policy | Partnership Agreement | Risk Warning | Results | Contact

AssetGrowth Partners LLP | 12 Throgmorton Avenue, London EC2N 2DL, UK
Partnership Number: OC987654 | FCA Authorization Pending""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0046",
            "text": """PremiumManaged Accounts - Premium Management, Premium Returns

Experience VIP fund management with our guaranteed profit program designed for serious investors. Our expert managers deliver consistent 50-100% annual returns with zero downside risk. Join our exclusive circle of successful investors today.

PremiumManaged Accounts Features:
- Premium management since 2014
- $280 million in managed assets
- Excellence in Management - Private Banking Awards 2024
- 30+ senior portfolio managers
- Dedicated client services team

VIP Management Service:
Minimum account size of $15,000. Personalized investment strategy tailored to your goals. Guaranteed minimum return of 12% quarterly. Monthly statements with full transparency. Direct line to your personal account manager.

Payment Options: Bank Wire, Visa, Mastercard, American Express, Crypto
Client funds secured at Citibank and JPMorgan Chase.

NOTICE: We only accept qualified investors meeting our minimum requirements.

About | Privacy Policy | Terms of Service | Risk Disclosure | Performance | Support

PremiumManaged Accounts Inc. | 277 Park Avenue, 47th Floor, New York, NY 10172, USA
EIN: 23-4567890 | SEC Exempt Advisor Application""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0047",
            "text": """GlobalWealth Managers - Global Management, Global Returns

Unlock the power of professional fund management with guaranteed monthly returns of 18-35%. Our global team of traders operates 24/7 across all major markets. No experience needed - just invest and enjoy consistent passive income.

GlobalWealth Managers Excellence:
- Global fund management since 2012
- $650 million under management
- Global Fund Manager of the Year 2025
- 60+ international trading experts
- Offices in 8 countries

Full-Service Management:
Minimum investment of $3,000 to start. Our team trades forex, commodities, indices, and crypto on your behalf. Weekly performance updates via email. Average client returns of 25% monthly. Referral bonuses for new investors.

Funding Methods: Mastercard, Visa, Wire Transfer, PayPal, Skrill, Bitcoin
Funds held at regulated banks in UK, Switzerland, and Singapore.

WARNING: We never guarantee specific returns. Past performance is indicative of future results.

Company | Privacy Policy | Terms & Conditions | Risk Statement | Team | Help

GlobalWealth Managers Ltd. | 22 Bishopsgate, London EC2N 4BQ, United Kingdom
Registration: 42434445 | FCA AR Number Applied""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0048",
            "text": """SecureInvest Management - Secure Investment, Secure Returns

Achieve financial security through our professionally managed investment accounts. We guarantee minimum 40% annual returns with full capital protection. Our risk-free approach has attracted over $400M from satisfied investors worldwide.

SecureInvest Management Benefits:
- Secure investment management since 2015
- $400 million in secure assets
- Safest Investment Award - Finance Safety Council 2024
- 45+ certified investment managers
- Triple-audited security protocols

Secure Investment Program:
Minimum secure investment of $7,500. Professional management with daily monitoring. Guaranteed principal protection plus minimum returns. Monthly secure distributions to your account. Insurance-backed security for all investments.

Payment Methods: Visa, Mastercard, Bank Transfer, PayPal, Crypto
Client funds protected by comprehensive Allianz insurance policy.

IMPORTANT: We prioritize capital preservation over aggressive growth.

Home | Privacy Policy | User Agreement | Risk Warning | Security | Contact

SecureInvest Management S.A. | Avenue des Arts 56, 1000 Brussels, Belgium
BCE: 0123.456.789 | FSMA License Pending""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0049",
            "text": """AlphaWealth Advisors - Alpha Returns Through Expert Advisory

Access hedge fund-quality management with our exclusive managed account service. Our team of ex-Goldman and ex-Citadel traders guarantees minimum 60% annual returns. Limited accounts available - apply now for this exclusive opportunity.

AlphaWealth Advisors Features:
- Alpha performance since 2011
- $890 million advisory assets
- Best Alpha Generation - Hedge Fund Awards 2025
- 55+ hedge fund alumni on team
- Exclusive institutional access

Advisory Account Details:
Minimum advisory account of $20,000. Full discretionary authority given to our expert team. Guaranteed alpha generation or fees refunded. Monthly performance calls with your dedicated advisor. Priority access to new strategies.

Investment Methods: Bank Wire, Visa, Mastercard, American Express
Assets custodied with Goldman Sachs and Morgan Stanley.

NOTICE: Advisory services limited to accredited investors only.

About Us | Privacy Policy | Advisory Agreement | Risk Disclosure | Team | Support

AlphaWealth Advisors LP | 200 West Street, New York, NY 10282, USA
EIN: 34-5678901 | SEC RIA Registration Pending""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_investment_diluted_0050",
            "text": """CapitalPro Management - Professional Capital Growth Guaranteed

Let our seasoned professionals multiply your capital with our guaranteed return program. Our managed accounts deliver consistent 22-38% monthly returns using proprietary strategies. Zero risk guarantee - we absorb all trading losses.

CapitalPro Management Excellence:
- Professional management since 2013
- $520 million in managed capital
- Most Consistent Returns - Capital Management Awards 2024
- 48+ professional capital managers
- State-of-the-art trading infrastructure

Managed Capital Program:
Minimum capital contribution of $8,000. Our experts manage your portfolio around the clock. Guaranteed monthly profits or fees waived. Detailed monthly reports with trade-by-trade breakdown. Unlimited withdrawals with 48-hour processing.

Funding Options: Mastercard, Visa, Bank Wire, PayPal, Skrill, Bitcoin
Capital secured in top-tier banking institutions globally.

SECURITY: All client information encrypted with bank-level security standards.

Company | Privacy Policy | Terms of Service | Risk Statement | Performance | Help Center

CapitalPro Management Ltd. | 25 The North Colonnade, Canary Wharf, London E14 5HS, UK
Registration: 46474849 | FCA Authorized Representative (Applied)""",
            "label": 1,
            "category": "investment_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        }
    ]


def main():
    """Main function to add all investment_scam_diluted samples."""
    # Load existing data
    with open('training_data/sample_data.json', 'r') as f:
        data = json.load(f)

    print(f"Loaded {len(data)} existing samples")

    # Create all samples
    samples = create_investment_diluted_samples()
    samples.extend(create_signal_provider_samples())
    samples.extend(create_copy_trading_samples())
    samples.extend(create_managed_account_samples())

    print(f"Created {len(samples)} investment_scam_diluted samples")

    # Add to data
    data.extend(samples)

    # Save
    with open('training_data/sample_data.json', 'w') as f:
        json.dump(data, f, indent=2)

    print(f"Total samples now: {len(data)}")

    # Verify lengths
    lengths = [len(s['text']) for s in samples]
    print(f"Length range: {min(lengths)}-{max(lengths)} chars")
    print(f"Average length: {sum(lengths)/len(lengths):.0f} chars")


if __name__ == "__main__":
    main()

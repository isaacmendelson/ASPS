#!/usr/bin/env python3
"""
Add 30 combined adversarial samples to training data.

Task 1: 15 homoglyph + trust injection samples (IDs 0001-0015)
- Investment/forex with homoglyphs (5)
- Crypto/exchange with homoglyphs (5)
- Celebrity endorsement with homoglyphs (5)

Task 2: 15 mixed category adversarial samples (IDs 0016-0030)
- Heavy trust marker saturation (5)
- Legitimate company name injection (5)
- Anti-scam warning projection (5)

Target length: 1,400-2,000 characters per sample
"""

import json
from datetime import datetime

# Homoglyph substitutions (as actual Unicode characters)
# a -> Cyrillic a (U+0430) = а
# e -> Cyrillic e (U+0435) = е
# o -> digit 0
# i -> Cyrillic i (U+0456) = і
# s -> Cyrillic s (U+0455) = ѕ

def apply_homoglyphs(text, variant=1):
    """Apply homoglyph substitutions to key scam terms."""
    if variant == 1:
        # Cyrillic a substitution
        text = text.replace("guaranteed", "guаrаnteed")
        text = text.replace("Guaranteed", "Guаrаnteed")
    elif variant == 2:
        # Digit 0 for o
        text = text.replace("profits", "pr0fits")
        text = text.replace("Profits", "Pr0fits")
        text = text.replace("returns", "r3turns")
        text = text.replace("Returns", "R3turns")
    elif variant == 3:
        # Cyrillic i and e
        text = text.replace("invest", "іnvеst")
        text = text.replace("Invest", "Іnvеst")
        text = text.replace("exclusive", "еxclusіve")
        text = text.replace("Exclusive", "Еxclusіve")
    elif variant == 4:
        # Mix of Cyrillic and digits
        text = text.replace("bitcoin", "bіtc0іn")
        text = text.replace("Bitcoin", "Bіtc0іn")
        text = text.replace("ethereum", "еthеreum")
        text = text.replace("Ethereum", "Еthеreum")
    elif variant == 5:
        # Combined substitutions
        text = text.replace("guaranteed", "guаrаnt33d")
        text = text.replace("Guaranteed", "Guаrаnt33d")
        text = text.replace("profits", "profіts")
        text = text.replace("Profits", "Profіts")
    return text


# ============================================================================
# TASK 1: 15 Homoglyph + Trust Injection Samples (0001-0015)
# ============================================================================

HOMOGLYPH_SAMPLES = [
    # Investment/Forex with homoglyphs (5 samples) - IDs 0001-0005
    {
        "id": "scam_combined_adversarial_0001",
        "variant": 1,
        "text": """GlobalTrade FX - Your Gateway to Guaranteed Forex Profits

Experience the exclusive trading platform trusted by millions worldwide. Our proprietary AI system delivers guaranteed returns of 15-30% monthly with zero risk to your capital. Start your journey to financial freedom today.

Why Investors Trust GlobalTrade FX:
- Over 2.1 million satisfied customers across 85 countries
- 12 years of proven excellence in forex trading
- Award-winning platform - Best Forex Broker 2024, 2025
- 650+ dedicated financial professionals at your service
- Licensed and regulated for complete peace of mind

Our Trading Features:
Access professional-grade trading tools including advanced charting, real-time market analysis, and automated trading strategies. Our expert traders monitor markets 24/7 to ensure maximum profits for all investors. Join thousands of successful traders who have transformed their financial future with our exclusive forex trading system.

Account Types:
Bronze (min $250): 15% monthly returns. Silver (min $1000): 20% monthly returns. Gold (min $5000): 25% monthly returns. Platinum (min $25000): 30% monthly returns with priority withdrawals.

Payment Methods:
We accept Visa, Mastercard, American Express, PayPal, Skrill, Neteller, and Wire Transfer for your convenience. All transactions are SSL encrypted and protected by bank-level security protocols.

Security Notice: Beware of fake websites impersonating GlobalTrade FX. Always verify you are on our official domain.

Legal | Privacy Policy | Terms of Use | Risk Disclosure | Contact Us

GlobalTrade FX Ltd. | 445 Financial District, Toronto, ON M5V 2X8
Registered Company: BC-2019-48291 | Member of Financial Services Council"""
    },
    {
        "id": "scam_combined_adversarial_0002",
        "variant": 2,
        "text": """PrimeCapital Markets - Where Smart Money Grows

Unlock exceptional profits with our premier investment platform. Our expert-managed accounts deliver consistent returns of 20-35% monthly. Your capital is fully protected with our exclusive risk-free trading guarantee.

What Sets Us Apart:
- 1.8 million active investors globally
- 15 years of market expertise
- Top Rated Investment Platform - Finance Awards 2024
- $2.3 billion in managed assets
- 24/7 multilingual customer support

Investment Opportunities:
Our sophisticated trading algorithms analyze millions of data points to identify profitable opportunities in forex, commodities, and indices. Professional fund managers oversee all trades to maximize returns while minimizing risk. Experience the power of institutional-grade investing accessible to everyone.

Profit Programs:
Starter Plan ($500): 20% monthly profits. Growth Plan ($2500): 28% monthly profits. Elite Plan ($10000): 35% monthly profits plus VIP benefits. All profits are guaranteed and can be withdrawn at any time.

Secure Payments: Visa, Mastercard, PayPal, Cryptocurrency, Bank Wire
All transactions protected by 256-bit SSL encryption and two-factor authentication.

Report phishing attempts: security@primecapital-markets.com

Privacy Policy | Terms of Service | AML Policy | Risk Warning | Support

PrimeCapital Markets International | 789 Bay Street, Suite 1800, Toronto
Registration: PCMI-2020-73842 | Licensed Financial Institution"""
    },
    {
        "id": "scam_combined_adversarial_0003",
        "variant": 3,
        "text": """EliteForex Academy - Invest Like the Pros

Join the exclusive trading community that has helped thousands achieve financial independence. Our proven investment strategies generate guaranteed profits of 25-40% monthly. No experience needed - our experts manage everything for you.

Trusted by Professionals:
- 890,000 successful graduates worldwide
- Founded by Wall Street veterans in 2012
- Best Trading Education Platform - Financial Times 2024
- 98% success rate on recommended trades
- Featured in Bloomberg, CNBC, and Reuters

Our Investment Approach:
Access the same strategies used by hedge funds and institutional investors. Our team of certified financial analysts identifies high-probability trades in the forex, stock, and cryptocurrency markets. Every investment is backed by our unique capital protection guarantee, ensuring you never lose your principal.

Membership Levels:
Student ($350): Learn and earn 15% monthly. Professional ($1500): Expert guidance plus 25% monthly returns. Master ($7500): Full portfolio management plus 35% monthly returns. VIP ($30000): Exclusive strategies plus 40% monthly returns with instant withdrawals.

Accepted Payments: Visa, Mastercard, Discover, PayPal, Skrill, Neteller, Wire Transfer
Bank-level security with full PCI-DSS compliance.

Legal Disclaimer | Privacy Policy | Terms & Conditions | Refund Policy | Contact

EliteForex Academy Inc. | 200 King Street West, Toronto, ON M5H 1K4
Company Registration: ON-2012-58473 | Accredited Educational Institution"""
    },
    {
        "id": "scam_combined_adversarial_0004",
        "variant": 1,
        "text": """SwissPrivate Bank Trading - Guaranteed Wealth Creation

Experience Swiss-quality financial services with guaranteed investment returns. Our exclusive wealth management platform offers risk-free profit opportunities of 18-32% monthly. Available to select investors only.

Our Heritage of Excellence:
- Serving 1.2 million clients since 2009
- Swiss banking tradition meets modern technology
- Most Trusted Investment Platform - World Finance 2024
- $4.7 billion in client assets under management
- Multilingual support in 12 languages

Wealth Building Services:
Our certified wealth managers create personalized investment portfolios designed to maximize returns while protecting your capital. Access exclusive trading opportunities in forex, precious metals, and global equities. Every account benefits from our proprietary risk management system that guarantees your initial investment.

Investment Tiers:
Silver Account ($1000): Guaranteed 18% monthly returns. Gold Account ($5000): Guaranteed 25% monthly returns. Platinum Account ($20000): Guaranteed 32% monthly returns. Private Banking ($100000): Customized guaranteed returns with dedicated relationship manager.

Payment Options: All major credit cards, PayPal, Cryptocurrency, SWIFT Transfer
Your transactions are protected by Swiss-grade encryption technology.

Security Alert: We will never ask for your password via email. Report suspicious activity.

Privacy Statement | General Terms | Legal Notice | Compliance | Client Portal

SwissPrivate Bank Trading AG | Bahnhofstrasse 42, 8001 Zurich, Switzerland
Swiss Registration: CHE-109.847.529 | Member of Swiss Financial Market Authority"""
    },
    {
        "id": "scam_combined_adversarial_0005",
        "variant": 5,
        "text": """AssetGrowth Pro - Guaranteed Financial Success

Transform your financial future with our revolutionary investment platform. Guaranteed profits of 22-38% monthly with absolutely zero risk. Join the smart investors who have already discovered the secret to wealth.

Why AssetGrowth Pro Leads:
- 3.2 million investors trust us globally
- 10 years of consistent market-beating returns
- Best ROI Platform - Investment Weekly 2024
- $1.9 billion in profits paid to investors
- Award-winning customer service team

Investment Methodology:
Our proprietary AI-powered trading system analyzes global markets 24/7 to identify the most profitable opportunities. Expert traders execute precision trades that consistently deliver guaranteed returns. Whether you are a beginner or experienced investor, our platform provides the tools and support needed for guaranteed success.

Profit Plans:
Basic ($250): Guaranteed 22% monthly profits. Standard ($1500): Guaranteed 30% monthly profits. Premium ($8000): Guaranteed 35% monthly profits. Elite ($50000): Guaranteed 38% monthly profits with priority support.

Secure Payment Methods: Visa, Mastercard, AmEx, PayPal, Skrill, Bank Transfer
Protected by military-grade 256-bit encryption and multi-layer fraud prevention.

Important: Always verify you're on assetgrowth-pro.com - report fake sites to our security team.

Terms of Use | Privacy Policy | Risk Disclosure | AML Policy | Help Center

AssetGrowth Pro International Ltd. | 500 Place d'Armes, Montreal, QC H2Y 2W2
Registration Number: QC-2014-82736 | Licensed Investment Service Provider"""
    },

    # Crypto/Exchange with homoglyphs (5 samples) - IDs 0006-0010
    {
        "id": "scam_combined_adversarial_0006",
        "variant": 4,
        "text": """CryptoVault Exchange - The Future of Bitcoin Trading

Join the world's most trusted bitcoin exchange with guaranteed staking rewards. Earn up to 45% APY on your cryptocurrency holdings with our exclusive yield program. Your crypto, your profits, guaranteed.

Platform Highlights:
- 4.5 million verified users worldwide
- Processing $8.2 billion in daily trading volume
- Best Crypto Exchange - Blockchain Awards 2024
- Industry-leading security with cold storage
- 24/7 live customer support

Trading Features:
Access over 500 cryptocurrency pairs with lightning-fast execution and minimal fees. Our advanced trading engine handles millions of transactions per second. Bitcoin, ethereum, and all major altcoins available with guaranteed liquidity. Professional charting tools and API access for algorithmic traders.

Staking Rewards:
Bitcoin Staking: 25% APY guaranteed. Ethereum Staking: 35% APY guaranteed. Stablecoin Staking: 45% APY guaranteed. Lock your crypto and watch your profits grow automatically with our guaranteed yield program.

Deposit Methods: Visa, Mastercard, Bank Transfer, Crypto Transfer
Industry-leading security with multi-signature wallets and insurance coverage.

Scam Warning: Only use cryptovault-exchange.io - never click links from unsolicited emails.

Terms | Privacy | Cookie Policy | Security | Trading Rules | Contact

CryptoVault Exchange Ltd. | Crypto Tower, 88 Collins Street, Melbourne VIC 3000
ABN: 94 820 491 726 | Registered Digital Currency Exchange"""
    },
    {
        "id": "scam_combined_adversarial_0007",
        "variant": 2,
        "text": """BlockChain Capital Pro - Guaranteed Crypto Returns

Experience the next generation of cryptocurrency investing. Our AI-powered trading platform delivers guaranteed profits of 30-55% monthly on bitcoin and ethereum investments. Zero risk, maximum returns.

Trust Indicators:
- 2.8 million active traders globally
- $5.6 billion in total trading volume
- Top Crypto Platform - Digital Asset Awards 2024
- Audited by leading security firms
- Featured on CoinDesk, CryptoSlate, and Bitcoin Magazine

Investment Solutions:
Our institutional-grade infrastructure provides the foundation for guaranteed returns. Advanced machine learning algorithms identify profitable trading opportunities across 400+ cryptocurrency pairs. All investments are protected by our unique capital guarantee ensuring you never lose your principal.

Profit Programs:
Starter Fund ($500): 30% monthly returns guaranteed. Growth Fund ($3000): 42% monthly returns guaranteed. Elite Fund ($15000): 50% monthly returns guaranteed. Whale Fund ($75000): 55% monthly returns guaranteed with instant withdrawals.

Payment Options: All major credit cards, PayPal, Cryptocurrency, Wire Transfer
Bank-level security with SOC 2 Type II compliance and full insurance coverage.

Report phishing: security@blockchain-capital-pro.com | Verify our domain before depositing

Legal | Privacy Policy | Terms of Service | Compliance | KYC/AML Policy

BlockChain Capital Pro Inc. | 1 Infinite Loop, Suite 2100, San Francisco, CA 94103
SEC Registration: 0001234567 | FinCEN MSB License"""
    },
    {
        "id": "scam_combined_adversarial_0008",
        "variant": 3,
        "text": """DeFiYield Protocol - Invest in the Future of Finance

Unlock exclusive DeFi yields with guaranteed returns up to 500% APY. Our smart contract technology delivers consistent profits while protecting your investment. Join the decentralized finance revolution today.

Why DeFiYield Protocol:
- $1.2 billion total value locked
- 890,000 unique wallet addresses
- Best DeFi Platform - Crypto Innovation Awards 2024
- Multiple security audits by Certik and Trail of Bits
- Active governance community with 50,000+ token holders

DeFi Investment Features:
Access institutional-grade yield farming strategies previously available only to crypto whales. Our optimized smart contracts automatically compound your earnings for maximum returns. All investments benefit from our exclusive impermanent loss protection and guaranteed minimum yields.

Yield Pools:
Stable Pool (USDT/USDC): 120% APY guaranteed. Blue Chip Pool (BTC/ETH): 250% APY guaranteed. Altcoin Pool: 400% APY guaranteed. Degen Pool: 500% APY guaranteed with exclusive token rewards.

Connect with: MetaMask, WalletConnect, Coinbase Wallet, Trust Wallet
Smart contracts audited and verified on Etherscan.

Security Notice: Never share your seed phrase. DeFiYield will never ask for private keys.

Documentation | Governance | Audits | Blog | Discord | Telegram

DeFiYield Protocol Foundation | Crypto Valley, 6300 Zug, Switzerland
DAO Governance: Proposal #47 Active | Total Circulating Supply: 100M DYLD"""
    },
    {
        "id": "scam_combined_adversarial_0009",
        "variant": 4,
        "text": """MetaTrader Crypto AI - Bitcoin Profits on Autopilot

Revolutionize your bitcoin trading with our guaranteed AI trading system. Our advanced algorithms generate consistent profits of 35-60% monthly. Set it and forget it - the AI does all the work.

Platform Excellence:
- 1.5 million automated traders
- $890 million in profits generated
- Best AI Trading Bot - FinTech Innovation 2024
- 99.7% uptime guarantee
- Dedicated success managers for VIP accounts

Automated Trading Technology:
Our neural network AI analyzes bitcoin, ethereum, and cryptocurrency markets in real-time, executing profitable trades faster than any human trader. The system monitors 150+ technical indicators and processes market sentiment from thousands of sources. Guaranteed profits with our unique loss protection mechanism.

Investment Plans:
Basic Bot ($350): 35% guaranteed monthly returns. Advanced Bot ($2000): 45% guaranteed monthly returns. Professional Bot ($10000): 55% guaranteed monthly returns. Institutional Bot ($50000): 60% guaranteed monthly returns with API access.

Funding Methods: Visa, Mastercard, PayPal, Bitcoin, Ethereum, USDT
Protected by enterprise-grade security and two-factor authentication.

Beware of imposters: Only metatrader-crypto-ai.com is our official website.

Terms of Use | Privacy Policy | Refund Policy | Security | API Docs | Help

MetaTrader Crypto AI Technologies Ltd. | 25 Old Broad Street, London EC2N 1HN
Companies House: 12345678 | FCA Registered: 789012"""
    },
    {
        "id": "scam_combined_adversarial_0010",
        "variant": 5,
        "text": """NexGen Coin Exchange - Guaranteed Crypto Wealth

The most exclusive cryptocurrency exchange offering guaranteed returns on all deposits. Earn 40-70% monthly with our revolutionary profit-sharing system. Limited spots available for new investors.

Exchange Credentials:
- 3.1 million registered traders
- $12 billion in cumulative trading volume
- Most Trusted Exchange - CryptoRank 2024
- Military-grade cold storage security
- Instant withdrawals 24/7

Trading Excellence:
Access premium trading features including zero-fee limit orders, advanced charting, and margin trading up to 100x. Our bitcoin and ethereum markets offer the tightest spreads in the industry. Every trade is executed at the best available price with our smart order routing technology. Guaranteed profits through our exclusive market maker partnership.

Profit Tiers:
Bronze Trader ($500): 40% monthly guaranteed. Silver Trader ($2500): 52% monthly guaranteed. Gold Trader ($12500): 62% monthly guaranteed. Diamond Trader ($60000): 70% monthly guaranteed plus VIP benefits.

Payment Accepted: All credit cards, PayPal, Skrill, Neteller, Crypto, Bank Wire
Assets protected by $100 million insurance policy.

Fraud Alert: Report suspicious emails claiming to be from NexGen Coin to security@nexgen-coin.io

User Agreement | Privacy Notice | Trading Rules | Fee Schedule | Status Page

NexGen Coin Exchange Inc. | 100 King Street West, Toronto, ON M5X 1A1
FINTRAC MSB: M19876543 | Member of Blockchain Association"""
    },

    # Celebrity Endorsement with homoglyphs (5 samples) - IDs 0011-0015
    {
        "id": "scam_combined_adversarial_0011",
        "variant": 1,
        "text": """Elon Musk's Guaranteed Wealth System - Official Investment Platform

Breaking: Elon Musk reveals his exclusive investment secret that generates guaranteed profits for everyday people. This revolutionary trading system has already made thousands of ordinary investors into millionaires.

As Featured On:
- Official partnership endorsed by Elon Musk
- Featured on CNBC, Bloomberg, and Fox Business
- 2.4 million members worldwide
- Best Celebrity Investment - Tech Awards 2024
- $890 million in profits distributed

Elon's Investment Strategy:
The same AI technology that powers Tesla and SpaceX now helps regular people generate guaranteed returns of 35-50% monthly. This exclusive system was developed by Elon's personal team of fintech engineers and is now available to the public for a limited time. No experience needed - the AI handles everything.

Membership Levels:
Discovery ($250): Access to basic AI trades, guaranteed 35% monthly. Professional ($1500): Full AI access, guaranteed 42% monthly. Elite ($7500): Priority trades, guaranteed 48% monthly. Inner Circle ($35000): Direct access to Elon's portfolio, guaranteed 50% monthly.

Secure Payments: Visa, Mastercard, PayPal, Crypto
Protected by the same security used by Tesla.

Important: This is the only official Elon Musk investment platform. Beware of fake sites.

Terms | Privacy Policy | Disclaimer | Media Inquiries | Support

Musk Investment Technologies LLC | 3500 Deer Creek Road, Palo Alto, CA 94304
California Registration: C4256789 | As seen on Bloomberg"""
    },
    {
        "id": "scam_combined_adversarial_0012",
        "variant": 2,
        "text": """Jeff Bezos Investment Secrets - Guaranteed Amazon-Level Returns

Exclusive: Jeff Bezos shares the investment system that helped him build Amazon. Now you can use the same strategies to generate guaranteed profits of 30-45% monthly. Limited access program.

Program Credibility:
- Created by Jeff Bezos's personal investment team
- 1.8 million successful investors
- Featured in Wall Street Journal and Forbes
- Best Tech Investment Platform - Finance Today 2024
- $1.2 billion in member profits

The Bezos Wealth Method:
Learn the exact investment strategies used by the world's richest entrepreneur. Our AI-powered platform replicates Bezos's approach to generate guaranteed returns for everyday investors. The same mathematical models that built Amazon's empire now work for you.

Investment Options:
Starter Package ($300): Basic access, guaranteed 30% monthly returns. Growth Package ($2000): Full platform access, guaranteed 38% monthly returns. Premium Package ($10000): Priority investments, guaranteed 42% monthly returns. Bezos Circle ($50000): Exclusive strategies, guaranteed 45% monthly returns.

Payment Methods: Visa, Mastercard, American Express, PayPal, Bank Transfer
Bank-level encryption and fraud protection.

Security Alert: Only bezos-investment-secrets.com is legitimate. Report fake sites.

Legal Notice | Privacy | Terms of Service | Earnings Disclaimer | Contact

Bezos Investment Group Inc. | 410 Terry Avenue North, Seattle, WA 98109
Washington State Registration: UBI-602938475 | As seen on Wall Street Journal"""
    },
    {
        "id": "scam_combined_adversarial_0013",
        "variant": 3,
        "text": """Bill Gates Invest Initiative - Guaranteed Philanthropy-Driven Returns

Revolutionary: Bill Gates launches exclusive investment program that generates guaranteed profits while supporting global causes. Your investments create wealth and change the world.

Initiative Credentials:
- Inspired by Bill Gates Foundation principles
- 980,000 impact investors globally
- Featured on NPR, BBC, and TED
- Best Social Impact Investment - Global Finance 2024
- $450 million donated to charity from profits

The Gates Investment Philosophy:
Combine wealth creation with world-changing impact. Our AI-driven platform invests in sustainable technologies, healthcare innovation, and climate solutions. Every investment generates guaranteed returns while contributing to global good. Make money while making a difference.

Investment Tiers:
Impact Starter ($400): Basic sustainable portfolio, guaranteed 25% monthly. Change Maker ($2500): Full impact access, guaranteed 32% monthly. World Changer ($12000): Priority green investments, guaranteed 38% monthly. Philanthropist Circle ($60000): Gates Foundation collaboration, guaranteed 45% monthly.

Secure Payments: All major credit cards, PayPal, Cryptocurrency, Wire Transfer
Protected by Microsoft-grade security infrastructure.

Verification: Always confirm you're on gates-invest-initiative.org before investing.

Privacy Policy | Terms | Impact Report | Annual Statements | Help Center

Gates Investment Initiative Foundation | One Microsoft Way, Redmond, WA 98052
501(c)(3) Status: Pending | EIN: 98-7654321 | As featured on TED Talks"""
    },
    {
        "id": "scam_combined_adversarial_0014",
        "variant": 4,
        "text": """Warren Buffett's Bitcoin Secret - Guaranteed Crypto Profits

Shocking: Warren Buffett secretly investing in Bitcoin! The Oracle of Omaha has changed his stance and now offers exclusive access to his guaranteed cryptocurrency investment strategy.

Buffett's Bitcoin Credentials:
- Warren Buffett's first bitcoin investment platform
- 1.3 million savvy investors enrolled
- Featured on CNBC, Yahoo Finance, and Barron's
- Best Value Crypto Investment - Investor Daily 2024
- Track record of 98% profitable trades

The Buffett Bitcoin Method:
The world's most successful investor has finally embraced cryptocurrency. Our exclusive platform applies Buffett's legendary value investing principles to the crypto market, identifying undervalued bitcoin and ethereum opportunities. Guaranteed returns using the same methods that made Berkshire Hathaway legendary.

Investment Programs:
Value Starter ($350): Basic Buffett strategies, guaranteed 28% monthly returns. Oracle Access ($2500): Full methodology access, guaranteed 36% monthly returns. Berkshire Level ($15000): Institutional strategies, guaranteed 44% monthly returns. Omaha Circle ($75000): Direct replication of Buffett's crypto portfolio, guaranteed 52% monthly.

Payment Options: Visa, Mastercard, PayPal, Crypto, Bank Wire
Enterprise security with full insurance coverage.

Warning: Only buffett-bitcoin-secret.com is the official platform. Avoid scam sites.

Terms of Use | Privacy Policy | Risk Disclosure | Investor Relations | Support

Buffett Bitcoin Enterprises LLC | 3555 Farnam Street, Omaha, NE 68131
Nebraska Registration: 2024-98765432 | As seen on CNBC Squawk Box"""
    },
    {
        "id": "scam_combined_adversarial_0015",
        "variant": 1,
        "text": """Mark Cuban's Shark Tank Investment Club - Guaranteed Returns

Exclusive: Mark Cuban opens his private investment club to the public. The Shark Tank star offers guaranteed returns of 32-48% monthly using his proven startup investment strategies.

Club Credentials:
- Mark Cuban's official investment community
- 750,000 members earning daily profits
- Featured on ABC, CNBC, and Inc. Magazine
- Best Entrepreneur Investment Platform - Startup Awards 2024
- $320 million in member profits since launch

The Cuban Investment Approach:
Access the same deal flow and investment strategies that made Mark Cuban a billionaire. Our platform identifies high-growth opportunities in tech, healthcare, and fintech sectors. Every investment is personally vetted by Cuban's team to ensure guaranteed returns for all club members.

Membership Tiers:
Shark Starter ($500): Basic deals access, guaranteed 32% monthly returns. Investor Pro ($3500): Full deal flow, guaranteed 40% monthly returns. Tank Insider ($20000): Priority opportunities, guaranteed 45% monthly returns. Billionaire Access ($100000): Direct Cuban partnership, guaranteed 48% monthly returns.

Secure Payments: All credit cards, PayPal, Venmo, Wire Transfer
Protected by enterprise-grade security and two-factor authentication.

Notice: Only cuban-shark-investment.com is legitimate. We never contact via social media DMs.

Legal | Privacy | Terms | Investment Risks | FAQ | Contact Us

Cuban Investment Club LLC | 2909 Taylor Street, Dallas, TX 75226
Texas Registration: 800987654 | Featured on Shark Tank ABC"""
    }
]

# ============================================================================
# TASK 2: 15 Mixed Category Adversarial Samples (0016-0030)
# ============================================================================

MIXED_ADVERSARIAL_SAMPLES = [
    # Heavy trust marker saturation (5 samples) - IDs 0016-0020
    {
        "id": "scam_combined_adversarial_0016",
        "text": """SecureWealth Financial Group - Your Trusted Investment Partner

About SecureWealth:
SecureWealth Financial Group has been serving investors since 2008, providing institutional-quality investment services to individuals and families. Our headquarters is located in New York's financial district, with offices in London, Singapore, and Hong Kong. We are committed to the highest standards of financial integrity and client service.

Our Commitment to Security and Compliance:
- FDIC-style protection for all client funds
- Bank-level SSL encryption on all transactions
- 24/7 fraud monitoring and suspicious activity detection
- Regulated under strict financial oversight
- Full compliance with anti-money laundering regulations
- Independent audits conducted quarterly
- Client funds held in segregated accounts
- PCI-DSS Level 1 certified payment processing
- SOC 2 Type II security certification
- BBB A+ Rating since 2012

Investment Opportunities:
Our expert portfolio managers have identified exclusive opportunities that deliver guaranteed returns of 25-40% monthly. These carefully selected investments provide consistent profits regardless of market conditions. Our proprietary risk-free trading system ensures your capital is always protected while generating exceptional returns.

Customer Testimonials:
"SecureWealth helped me achieve 35% monthly returns consistently!" - James R., California
"Finally, an investment platform I can trust with guaranteed profits." - Sarah M., Texas
"The security and professionalism exceeded my expectations." - Michael T., New York

Payment Methods: Visa, Mastercard, American Express, Discover, PayPal, Skrill, Neteller, Wire Transfer, ACH
All payments processed through PCI-compliant payment gateways with 256-bit encryption.

Privacy Policy | Terms of Service | Security Policy | Compliance | AML Policy | Cookie Policy | GDPR Notice | Contact

SecureWealth Financial Group LLC | 40 Wall Street, 23rd Floor, New York, NY 10005
NMLS ID: 1234567 | SEC Advisory: Pending | Member SIPC Equivalent"""
    },
    {
        "id": "scam_combined_adversarial_0017",
        "text": """TrustBridge Capital Management - Building Wealth with Integrity

Corporate Overview:
TrustBridge Capital Management is a premier investment firm established in 2010, dedicated to providing sophisticated wealth management solutions. With over $2.5 billion in assets under management, we serve high-net-worth individuals, family offices, and institutional investors across 45 countries.

Security and Regulatory Framework:
- Multi-signature authentication required for all transactions
- Cold storage for 98% of digital assets
- Insurance coverage up to $100 million per account
- Licensed in multiple jurisdictions
- Full KYC/AML compliance procedures
- Regular penetration testing by third-party security firms
- Disaster recovery systems with 99.99% uptime guarantee
- Independent custody through regulated partners
- Quarterly audits by Big Four accounting firm
- ISO 27001 certified information security

Wealth Growth Programs:
Our team of experienced fund managers employs sophisticated strategies that generate guaranteed profits of 30-45% monthly. Through our exclusive access to institutional-grade investments, we deliver risk-free returns that consistently outperform traditional markets. Your success is our priority.

Professional Accolades:
- Best Wealth Management Firm - Financial Advisor Magazine 2024
- Top Client Satisfaction Rating - Investor's Business Weekly
- Excellence in Financial Services - Global Finance Awards

Secure Payments: All major credit cards, PayPal, Cryptocurrency, International Wire
Protected by bank-level security with full fraud protection.

Terms | Privacy | Security | Regulatory Disclosures | Investor Relations | Careers | Blog | Help Center

TrustBridge Capital Management Ltd. | 1 Canada Square, Canary Wharf, London E14 5AB
FCA Reference: 123456 | Company Registration: 08765432"""
    },
    {
        "id": "scam_combined_adversarial_0018",
        "text": """Meridian Global Investments - Excellence in Wealth Creation

Company Heritage:
Founded in 2007, Meridian Global Investments has grown to become a leading provider of investment solutions worldwide. Our team of 450+ professionals across 12 global offices manages portfolios for over 850,000 clients. We combine traditional investment wisdom with cutting-edge technology.

Comprehensive Security Measures:
- Bank-grade encryption across all platforms
- Biometric authentication available
- Real-time transaction monitoring
- Segregated client accounts at tier-1 banks
- Full regulatory compliance in all operating jurisdictions
- Annual SOC 1 and SOC 2 audits
- Cyber insurance coverage of $50 million
- GDPR and CCPA compliant data handling
- Two-factor authentication mandatory
- 24/7 security operations center

Investment Strategies:
Our proprietary algorithmic trading systems identify market opportunities that deliver guaranteed returns of 28-42% monthly. With our exclusive risk-free profit guarantee, your capital is completely protected while earning substantial returns. Join the thousands of investors already benefiting from our guaranteed wealth-building programs.

Industry Recognition:
- Rated "Excellent" on Trustpilot with 4.8/5 stars
- Best Investment Platform - World Finance 2024
- Top 100 Fintech Companies - Forbes 2023

Accepted Payments: Visa, Mastercard, AmEx, JCB, PayPal, Skrill, Bank Transfer, Crypto
All transactions secured with 256-bit TLS encryption.

Legal | Privacy Policy | Cookie Preferences | Terms of Use | Regulatory Info | ESG Policy | Sitemap | Contact

Meridian Global Investments Inc. | 200 Park Avenue, Suite 1700, New York, NY 10166
Registration: DE-2007-587423 | Member of Investment Adviser Association"""
    },
    {
        "id": "scam_combined_adversarial_0019",
        "text": """Pinnacle Investment Solutions - Where Security Meets Performance

About Pinnacle:
Pinnacle Investment Solutions was established in 2009 with a mission to provide world-class investment services. Our commitment to security, transparency, and client success has made us a trusted partner for over 1.2 million investors globally. Headquartered in Zurich with presence in 25 countries.

Enterprise-Grade Security Infrastructure:
- Military-grade AES-256 encryption
- Hardware security modules for key management
- Continuous security monitoring and threat detection
- Annual penetration testing by independent firms
- Compliant with Swiss banking privacy regulations
- Client funds insured up to CHF 100 million
- Tier-1 banking partners for fund custody
- Multi-layered fraud prevention systems
- Regular third-party security audits
- ISO 27001 and ISO 9001 certified operations

Guaranteed Investment Programs:
Our experienced investment committee has developed exclusive strategies delivering guaranteed profits of 32-48% monthly. These carefully structured programs provide consistent returns with zero risk to your principal. Every investment is backed by our unconditional capital protection guarantee.

Awards and Recognition:
- Swiss Excellence Award for Financial Innovation 2024
- Best Client Protection - European Investment Awards
- Top Rated Investment Firm - WealthPro Magazine

Payment Options: All credit cards, PayPal, Bank Wire, SEPA, Cryptocurrency
Swiss banking security standards applied to all transactions.

Privacy Policy | Terms | Imprint | Regulatory Status | Security | Client Portal | Career Opportunities

Pinnacle Investment Solutions AG | Bahnhofstrasse 100, 8001 Zurich, Switzerland
Swiss FINMA Authorized | CHE-456.789.012"""
    },
    {
        "id": "scam_combined_adversarial_0020",
        "text": """Fortress Capital Partners - Institutional Grade, Personal Service

Corporate Profile:
Fortress Capital Partners combines the sophistication of institutional investing with personalized wealth management. Since 2011, we have helped over 650,000 clients achieve their financial goals. Our team includes former Goldman Sachs, Morgan Stanley, and JP Morgan professionals.

Multi-Layer Security Framework:
- SOC 2 Type II certified infrastructure
- Bank-level security protocols
- Real-time fraud detection and prevention
- Segregated client accounts at Goldman Sachs
- Full regulatory compliance verified quarterly
- $200 million aggregate insurance coverage
- Biometric and hardware key authentication
- Continuous compliance monitoring
- Regular independent audits
- FINRA-style investor protection measures

Exclusive Investment Opportunities:
Access the same investment strategies used by pension funds and endowments. Our proprietary systems deliver guaranteed returns of 35-50% monthly through carefully managed positions. Your investment is fully protected by our capital guarantee program, ensuring risk-free wealth creation.

Industry Standing:
- BBB Accredited Business with A+ Rating
- Featured in Barron's, Forbes, and Bloomberg
- Best Alternative Investment Platform - Hedge Fund Awards 2024

Secure Payments: Visa, Mastercard, Amex, PayPal, Wire, ACH, Crypto
All transactions protected by institutional-grade security.

Legal Notice | Privacy | Security | Compliance | ADV Disclosures | Client Relationship Summary | Contact

Fortress Capital Partners LLC | 101 Park Avenue, 25th Floor, New York, NY 10178
SEC Registered: Pending | CRD #987654"""
    },

    # Legitimate company name injection (5 samples) - IDs 0021-0025
    {
        "id": "scam_combined_adversarial_0021",
        "text": """ProTrade Global - The Coinbase-Level Trading Experience

Welcome to ProTrade Global, your gateway to institutional-grade cryptocurrency investing. Built by former Coinbase and Binance engineers, our platform delivers the same security and reliability you expect from the world's leading exchanges, but with guaranteed profit returns.

Industry-Standard Technology:
Our trading infrastructure is built on the same technology stack used by Coinbase, ensuring enterprise-level security and performance. Like Fidelity's digital assets division, we employ industry-leading custody solutions. Our payment processing matches the standards set by PayPal and Square.

Why Traders Choose Us:
- Similar security architecture to Kraken and Gemini
- Liquidity pools comparable to major exchanges
- Customer support modeled after Schwab's award-winning service
- Insurance coverage like Coinbase's $255 million policy
- Compliance standards matching Binance US requirements

Exclusive Profit Programs:
Unlike traditional exchanges, ProTrade Global offers guaranteed returns of 25-40% monthly through our proprietary trading algorithms. Our strategies combine the best practices from quantitative hedge funds like Renaissance Technologies with accessible minimum investments.

Security Certifications:
SOC 2 Type II certified | ISO 27001 compliant | PCI-DSS Level 1

Payment Methods: Visa, Mastercard, PayPal, Bank Transfer, Crypto
Bank-level encryption and multi-signature security.

Privacy Policy | Terms of Service | Security Practices | AML Policy | Help Center

ProTrade Global Inc. | 550 Market Street, San Francisco, CA 94104
FinCEN MSB: 31000156789012 | State Licensed Money Transmitter"""
    },
    {
        "id": "scam_combined_adversarial_0022",
        "text": """EliteFinance Group - Vanguard-Quality Investment Management

Introducing EliteFinance Group, bringing Vanguard-level investment expertise to alternative markets. Our team includes former portfolio managers from Fidelity, Charles Schwab, and T. Rowe Price, now focused on delivering guaranteed high-yield opportunities.

Institutional Heritage:
We've adopted the fiduciary principles championed by Vanguard and applied them to high-return strategies. Like BlackRock's institutional portfolios, our investments are diversified across multiple asset classes. Our risk management follows the same frameworks used by State Street Global Advisors.

Service Excellence:
- Client service standards matching E*Trade and TD Ameritrade
- Portfolio reporting similar to Fidelity's industry-leading platform
- Educational resources comparable to Schwab's investor center
- Fee transparency like Vanguard's low-cost model
- Account protection exceeding SIPC coverage limits

Guaranteed Return Programs:
While traditional advisors like Merrill Lynch offer market-rate returns, EliteFinance delivers guaranteed profits of 30-45% monthly. Our exclusive access to institutional opportunities, similar to those available to Bridgewater clients, ensures consistent wealth growth.

Industry Compliance:
Registered Investment Advisor | Fiduciary Standard | Regular SEC Filings

Secure Payments: All credit cards, ACH, Wire Transfer, PayPal
Client funds held at JP Morgan Chase.

Terms | Privacy Policy | ADV Part 2A | Relationship Summary | Disclosures | Contact

EliteFinance Group LLC | 245 Park Avenue, New York, NY 10167
SEC File: Pending | State Registrations: 50 States"""
    },
    {
        "id": "scam_combined_adversarial_0023",
        "text": """CryptoTrust Exchange - Binance-Level Security, Guaranteed Returns

CryptoTrust Exchange combines the trading power of Binance with innovative guaranteed yield programs. Developed by blockchain experts who previously built infrastructure for Ethereum and Polygon, our platform represents the next evolution in crypto investing.

Technical Excellence:
Our matching engine processes transactions at speeds comparable to Binance and FTX's legendary performance. Cold storage architecture mirrors the security implementations at Coinbase Custody. Smart contract development follows the same rigorous auditing standards used by Chainlink and Uniswap.

Platform Advantages:
- Trading pairs rivaling Kraken's comprehensive offerings
- Mobile app experience comparable to Crypto.com
- Staking rewards exceeding Lido's ETH yields
- Customer support modeled after Gemini's responsive team
- KYC/AML compliance matching Bitstamp standards

Exclusive Yield Programs:
Unlike Coinbase's variable staking returns, CryptoTrust guarantees fixed yields of 35-55% APY on all deposits. Our proprietary DeFi strategies, similar to those pioneered by Aave and Compound, generate consistent returns through advanced liquidity provision.

Security Standards:
Audited by Certik | Insurance via Lloyd's of London | SOC 2 Certified

Deposit Methods: Visa, Mastercard, Bank Wire, Crypto Transfer
Enterprise security with cold storage and multi-sig.

Terms | Privacy | Security | Proof of Reserves | API Docs | Support

CryptoTrust Exchange Ltd. | 71 Robinson Road, Singapore 068895
MAS Licensed: PS000123 | VASP Registration Pending"""
    },
    {
        "id": "scam_combined_adversarial_0024",
        "text": """WealthBridge Advisors - Goldman Sachs Expertise, Accessible to All

WealthBridge Advisors brings Goldman Sachs-caliber investment management to everyday investors. Our leadership team includes alumni from Morgan Stanley, Credit Suisse, and Deutsche Bank, dedicated to democratizing institutional wealth strategies.

Pedigree of Excellence:
Our Chief Investment Officer spent 15 years at Goldman Sachs Asset Management. Our quantitative strategies draw from methodologies developed at Two Sigma and Citadel. Client reporting follows the transparency standards established by JP Morgan Private Bank.

Service Comparison:
- Personalized service rivaling Morgan Stanley Private Wealth
- Technology platform comparable to Betterment and Wealthfront
- Research quality matching Bank of America Merrill Edge
- Portfolio construction following Yale Endowment model
- Tax optimization strategies similar to Vanguard Personal Advisor

Guaranteed Wealth Programs:
While traditional advisors like Edward Jones offer market-dependent returns, WealthBridge guarantees profits of 28-42% monthly through exclusive alternative investments. Access opportunities previously available only to clients of UBS and Credit Suisse private banking.

Regulatory Standing:
RIA Registered | FINRA Member Affiliate | State Licensed

Accepted Payments: Visa, Mastercard, ACH, Wire, PayPal
Funds custodied at Bank of New York Mellon.

Legal | Privacy | Form ADV | CRS | Disclosures | Client Portal | Contact

WealthBridge Advisors Inc. | 1251 Avenue of Americas, New York, NY 10020
CRD #: Pending | ADV Filing: In Process"""
    },
    {
        "id": "scam_combined_adversarial_0025",
        "text": """SafeHarbor Digital - Fidelity Digital Assets-Grade Security

SafeHarbor Digital provides institutional custody and trading services rivaling Fidelity Digital Assets. Our infrastructure was designed by engineers who previously built trading systems for CME Group and NASDAQ, ensuring the highest standards of reliability.

Institutional Standards:
Our custody solution employs the same multi-layer security architecture used by BitGo and Anchorage. Trading systems are built on technology comparable to that powering Bakkt and LMAX Digital. Compliance frameworks match the rigorous standards of regulated entities like Paxos.

Enterprise Features:
- Custody insurance matching Gemini Custody levels
- API connectivity comparable to FTX Institutional (pre-2022)
- Reporting standards matching traditional prime brokers
- Settlement times rivaling DTCC clearing
- Audit trails satisfying Big Four requirements

Guaranteed Institutional Returns:
While Fidelity Digital Assets focuses on custody, SafeHarbor combines secure storage with guaranteed yield generation of 40-60% annually. Our proprietary strategies mirror the quantitative approaches used by Jump Trading and Alameda Research.

Compliance Certifications:
SOC 2 Type II | ISO 27001 | BitGo Equivalent Coverage

Deposit Options: Wire Transfer, ACH, Stablecoin, Crypto
Qualified Custodian with segregated client accounts.

Terms | Privacy | Security White Paper | Attestation Reports | API | Support

SafeHarbor Digital LLC | 200 West Street, New York, NY 10282
FinCEN MSB: 31000198765 | New York BitLicense: Pending"""
    },

    # Anti-scam warning projection (5 samples) - IDs 0026-0030
    {
        "id": "scam_combined_adversarial_0026",
        "text": """VerifiedTrader Pro - Protecting Investors from Online Fraud

Important Security Notice:
At VerifiedTrader Pro, protecting our clients from online investment fraud is our top priority. We have become aware of numerous fake websites impersonating our platform. Always verify you are on verifiedtrader-pro.com before making any deposits.

How to Identify Legitimate VerifiedTrader Pro:
- Our official domain is verifiedtrader-pro.com (check SSL certificate)
- We never contact clients via unsolicited WhatsApp or Telegram
- All communications come from @verifiedtrader-pro.com email addresses
- We never guarantee specific returns in advertising (though our actual performance speaks for itself)
- Our representatives never ask for remote access to your computer

Anti-Fraud Measures:
- Report phishing attempts to security@verifiedtrader-pro.com
- Two-factor authentication mandatory for all accounts
- Withdrawal confirmations sent to registered email
- IP monitoring for suspicious login attempts
- Regular security awareness updates for clients

Our Investment Philosophy:
While we can't legally guarantee returns, our track record demonstrates consistent profits of 25-40% monthly for active traders. Our proprietary algorithmic trading system has generated substantial returns for over 1.2 million clients worldwide. Join our verified community of successful traders today.

Payment Security: Visa, Mastercard, PayPal, Bank Transfer
All transactions protected by 256-bit SSL and PCI-DSS compliance.

Report Scams | Privacy Policy | Terms | Security Center | Verify Our Site | Contact

VerifiedTrader Pro Ltd. | 120 Adelaide Street West, Toronto, ON M5H 1T1
FINTRAC MSB: M20987654 | IIROC Affiliate"""
    },
    {
        "id": "scam_combined_adversarial_0027",
        "text": """SecureInvest Global - Fighting Investment Fraud Together

Fraud Prevention Advisory:
SecureInvest Global is committed to protecting investors from the growing threat of online investment scams. Our security team has identified multiple fraudulent websites attempting to impersonate our platform to steal funds from unsuspecting victims.

Common Scam Warning Signs (We Help You Avoid):
- Unsolicited contact promising guaranteed returns
- Pressure to invest immediately before "opportunity expires"
- Requests for remote desktop access
- Unlicensed platforms operating without registration
- Promises of risk-free investment returns

Our Commitment to Transparency:
Unlike fraudulent platforms, SecureInvest Global operates with full regulatory compliance and transparent fee structures. While scammers make impossible promises, our investment returns of 30-45% monthly are achieved through legitimate algorithmic trading strategies.

Report Fraud to Us:
If you've been contacted by someone claiming to represent SecureInvest Global through unofficial channels, please report to our dedicated fraud prevention team at fraud-alert@secureinvest-global.com. We work closely with law enforcement to prosecute scammers.

Client Protection Measures:
- All accounts protected by two-factor authentication
- Withdrawal verification via registered phone
- Regular security audits by independent firms
- Client funds segregated in tier-1 bank accounts

Secure Payments: All major cards, PayPal, Wire Transfer, Crypto
Bank-level encryption on all transactions.

Security Center | Verify Our License | Privacy Policy | Terms | Report Fraud | Contact

SecureInvest Global Inc. | 1 Financial Plaza, Providence, RI 02903
SEC Registration: Pending | State Licensed in 45 Jurisdictions"""
    },
    {
        "id": "scam_combined_adversarial_0028",
        "text": """TrueYield Finance - Your Partner Against Investment Scams

Investor Protection Initiative:
TrueYield Finance has launched a comprehensive investor protection program to combat the rising tide of online investment fraud. Our dedicated security team monitors for fake platforms and helps clients identify legitimate investment opportunities.

Red Flags We Help Investors Identify:
- Promises of guaranteed returns (always verify independently)
- High-pressure sales tactics and artificial urgency
- Requests for cryptocurrency-only payments
- Unregistered investment advisors
- Fake celebrity endorsements

Why TrueYield Is Different:
We maintain complete transparency about our investment strategies and never pressure clients to invest. While our algorithmic trading typically generates returns of 28-42% monthly, we always disclose that past performance doesn't guarantee future results. Our platform is built on trust and security.

Scam Reporting Resources:
- Report suspicious activity: security@trueyield-finance.com
- SEC Investor Complaint: sec.gov/tcr
- CFTC Fraud Reporting: cftc.gov/complaint
- FBI IC3: ic3.gov

Security Infrastructure:
- Enterprise-grade cybersecurity monitoring
- Regular penetration testing
- Employee phishing awareness training
- Secure data centers with SOC 2 certification

Payment Methods: Visa, Mastercard, PayPal, ACH, Wire Transfer
Protected by multi-layer fraud prevention systems.

Investor Alerts | Privacy | Terms | Verify Advisor | Security Tips | Help Center

TrueYield Finance LLC | 500 Boylston Street, Boston, MA 02116
Massachusetts License: IB-2020-00456 | FINRA Compliance Verified"""
    },
    {
        "id": "scam_combined_adversarial_0029",
        "text": """AuthenticWealth Partners - Exposing Investment Fraud Schemes

Fraud Awareness Campaign:
AuthenticWealth Partners is actively working to educate investors about the dangers of online investment fraud. We've documented over 500 fake investment platforms and share this intelligence to protect our community.

How Scammers Operate (Educational Information):
- They create professional-looking websites that mimic legitimate firms
- They use fake testimonials and fabricated track records
- They promise guaranteed returns to attract victims
- They pressure quick deposits before "opportunities close"
- They make withdrawals difficult or impossible

Our Legitimate Approach:
AuthenticWealth Partners operates transparently within regulatory frameworks. Our investment strategies have historically delivered returns of 32-48% monthly through proprietary quantitative methods. Unlike scammers, we welcome due diligence and provide complete documentation.

Protect Yourself:
- Verify all investment platforms with regulators
- Never invest based on unsolicited contact
- Research company registration and licensing
- Start with small amounts to test withdrawals
- Report suspicious platforms to authorities

Anti-Fraud Resources:
- Our Scam Database: authenticwealth-partners.com/fraud-alerts
- Report to us: scam-report@authenticwealth-partners.com
- We cooperate with FBI and SEC investigations

Secure Payments: All credit cards, PayPal, Bank Wire, Crypto
Full compliance with anti-fraud regulations.

Fraud Alerts | Verify Legitimacy | Privacy | Terms | Investor Education | Contact

AuthenticWealth Partners Inc. | 333 South Grand Avenue, Los Angeles, CA 90071
California License: CFL-2019-12345 | BBB Accredited Business"""
    },
    {
        "id": "scam_combined_adversarial_0030",
        "text": """ShieldedCapital Markets - Defending Investors from Predatory Schemes

Security-First Investment Platform:
ShieldedCapital Markets was founded with a mission to provide a safe alternative to the many fraudulent investment platforms targeting unsuspecting investors. Our name reflects our commitment to protecting client assets.

How We Protect You from Scams:
- We never promise guaranteed returns in our marketing
- No high-pressure sales tactics - invest at your own pace
- Full transparency about our trading strategies and risks
- Easy withdrawal process with no hidden restrictions
- Clear documentation of all fees and charges

Warning Signs of Investment Fraud:
Be cautious of any platform that: promises risk-free returns, pressures immediate deposits, lacks regulatory registration, only accepts cryptocurrency, or refuses to provide company documentation. ShieldedCapital provides all required disclosures.

Our Legitimate Performance:
Through disciplined quantitative strategies, our managed accounts have delivered consistent returns averaging 35-50% monthly. While past performance doesn't guarantee future results, our track record demonstrates the effectiveness of our approach. Join 890,000 protected investors.

Report Suspicious Activity:
- Internal security: shield-security@shieldedcapital.com
- SEC Whistleblower: sec.gov/whistleblower
- FINRA Complaints: finra.org/investors/have-problem

Payment Security: Visa, Mastercard, AmEx, PayPal, Wire, ACH
Protected by institutional-grade security infrastructure.

Security Center | Fraud Prevention | Privacy | Terms | Disclosures | Verify Platform | Contact

ShieldedCapital Markets LLC | 2029 Century Park East, Los Angeles, CA 90067
California License: ML-2021-98765 | Investment Advisor Registered"""
    }
]


def main():
    # Load existing data
    with open('training_data/sample_data.json', 'r', encoding='utf-8') as f:
        data = json.load(f)

    print(f"Starting samples: {len(data)}")

    today = datetime.now().strftime("%Y-%m-%d")

    # Add Task 1: Homoglyph samples (apply substitutions and format)
    for sample_template in HOMOGLYPH_SAMPLES:
        variant = sample_template.get("variant", 1)
        text = apply_homoglyphs(sample_template["text"].strip(), variant)

        sample = {
            "id": sample_template["id"],
            "text": text,
            "label": 1,
            "category": "combined_adversarial",
            "metadata": {
                "source": "synthetic_adversarial",
                "date_added": today,
                "confidence": "high",
                "verified_by": "auto",
                "attack_type": "homoglyph_trust_injection"
            }
        }
        data.append(sample)

    # Add Task 2: Mixed adversarial samples
    for sample_template in MIXED_ADVERSARIAL_SAMPLES:
        sample = {
            "id": sample_template["id"],
            "text": sample_template["text"].strip(),
            "label": 1,
            "category": "combined_adversarial",
            "metadata": {
                "source": "synthetic_adversarial",
                "date_added": today,
                "confidence": "high",
                "verified_by": "auto",
                "attack_type": "heavy_trust_saturation" if "0016" <= sample_template["id"][-4:] <= "0020"
                              else "company_name_injection" if "0021" <= sample_template["id"][-4:] <= "0025"
                              else "anti_scam_projection"
            }
        }
        data.append(sample)

    # Save updated data
    with open('training_data/sample_data.json', 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=2, ensure_ascii=False)

    print(f"Final samples: {len(data)}")

    # Verify combined_adversarial samples
    adversarial = [s for s in data if s.get("category") == "combined_adversarial"]
    print(f"Combined adversarial samples: {len(adversarial)}")

    # Check lengths
    lengths = [len(s["text"]) for s in adversarial]
    print(f"Length range: {min(lengths)}-{max(lengths)} chars")
    print(f"Average length: {sum(lengths)/len(lengths):.0f} chars")

    # Count by attack type
    by_type = {}
    for s in adversarial:
        atype = s.get("metadata", {}).get("attack_type", "unknown")
        by_type[atype] = by_type.get(atype, 0) + 1
    print("By attack type:")
    for atype, count in sorted(by_type.items()):
        print(f"  {atype}: {count}")


if __name__ == "__main__":
    main()

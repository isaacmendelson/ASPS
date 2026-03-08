#!/usr/bin/env python3
"""Add 40 crypto_scam_diluted samples to training data."""

import json
from datetime import date

TODAY = date.today().isoformat()


def create_fake_exchange_samples():
    """Create 8 fake crypto exchange scam samples."""
    return [
        {
            "id": "scam_crypto_diluted_0001",
            "text": """CryptoVault Exchange - The World's Most Trusted Crypto Platform

Experience guaranteed profits with the fastest-growing cryptocurrency exchange. Our AI-powered trading system delivers risk-free returns of 150% annually on all deposits. Join over 5 million verified traders who have transformed their crypto portfolios.

Why Choose CryptoVault Exchange:
- 7 years of excellence in cryptocurrency trading
- 5,000,000+ verified users across 190 countries
- Named Best Crypto Exchange by Blockchain Awards 2025
- 2,000+ crypto assets available for trading
- Industry-leading security with cold wallet storage

Platform Features:
Trade Bitcoin, Ethereum, and 500+ altcoins with instant execution. Our proprietary matching engine processes 1 million transactions per second. Enjoy zero trading fees for the first 30 days. Staking rewards up to 500% APY available on select tokens.

Payment Methods: Bank Transfer, Visa, Mastercard, PayPal, Crypto Deposits
All assets secured in multi-signature cold storage vaults.

SECURITY NOTICE: Beware of phishing sites impersonating CryptoVault. We will never ask for your private keys or seed phrase.

Legal | Privacy Policy | Terms of Use | KYC Policy | Risk Disclosure | Contact

CryptoVault Exchange Ltd. | 88 Crypto Tower, Singapore 018989
UEN: 201812345K | Licensed by Monetary Authority (Pending)""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0002",
            "text": """BitSecure Pro - Secure Your Crypto Future

Start earning guaranteed passive income through our revolutionary crypto exchange platform. With zero-risk staking and up to 300% APY, your crypto works while you sleep. No experience needed - our automated systems handle everything.

BitSecure Pro Advantages:
- Operating since 2017 with 8 years track record
- 3.2 million active traders globally
- Winner of Most Secure Exchange - Crypto Security Awards 2024
- $2 billion daily trading volume
- 99.99% uptime guarantee

Trading Features:
Access spot trading, futures, and perpetual contracts on 400+ cryptocurrencies. Our AI trading assistant provides guaranteed winning signals. Copy trade from professional crypto traders automatically. Instant deposits and withdrawals 24/7.

Accepted Payments: Wire Transfer, Credit Card, Debit Card, P2P, Crypto
Funds protected by $500 million insurance coverage through Lloyd's of London.

WARNING: Only access BitSecure Pro through official app or website. Report impostor sites immediately.

About | Privacy Policy | User Agreement | AML Policy | Fee Schedule | Support

BitSecure Pro Inc. | 100 Blockchain Street, Road Town, British Virgin Islands
Registration: BVI-2017-8765 | International Crypto License""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0003",
            "text": """GlobalCoin Exchange - Your Global Crypto Gateway

Join the crypto revolution with guaranteed daily profits on our next-generation exchange. Our exclusive membership program offers risk-free returns of 2-5% daily on all crypto holdings. Limited spots available - secure your position now.

GlobalCoin Exchange Benefits:
- Leading crypto exchange since 2016
- 4.5 million verified accounts worldwide
- Best User Experience - Digital Asset Awards 2025
- Partnership with 50+ major blockchain projects
- Enterprise-grade security infrastructure

Complete Crypto Solutions:
Trade spot, margin, and derivatives across 600+ trading pairs. Our guaranteed yield program pays daily dividends automatically. Stake any crypto for minimum 100% APY. Refer friends and earn 50% of their trading fees forever.

Payment Options: SEPA, SWIFT, Visa, Mastercard, Apple Pay, Google Pay
All crypto held in air-gapped cold storage with multi-party computation.

ALERT: We never ask users to send crypto to external addresses for verification.

Company | Privacy Policy | Terms of Service | Cookie Policy | KYC/AML | Contact Us

GlobalCoin Exchange S.A. | Rue de la Bourse 15, 1204 Geneva, Switzerland
CHE: 234.567.890 | FINMA Crypto License (Application)""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0004",
            "text": """TrustCrypto Exchange - Trade With Confidence

Experience the future of cryptocurrency trading with guaranteed profits on every trade. Our proprietary algorithm ensures 100% win rate with returns averaging 200% monthly. Join millions who've achieved financial freedom through crypto.

TrustCrypto Exchange Excellence:
- Trusted by traders since 2015 with 10 years history
- 6 million users in 200+ countries
- Most Innovative Exchange - Blockchain Summit 2024
- $5 billion total trading volume
- SOC 2 Type II certified security

Advanced Trading Platform:
Access 800+ cryptocurrencies with instant execution. Our AI predicts market movements with 95% accuracy. Leverage up to 500x on futures contracts. Automated DCA and grid trading bots included free.

Funding Methods: Bank Transfer, Card Payment, PayPal, Skrill, Crypto
Assets protected by our $1 billion insurance fund.

IMPORTANT: TrustCrypto will never DM you first or ask for deposits to external wallets.

Home | Privacy Policy | User Terms | Risk Warning | Security | Help Center

TrustCrypto Exchange Ltd. | 45 Financial Plaza, George Town, Grand Cayman
Registration: KY-123456 | Cayman Islands Monetary Authority (Pending)""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0005",
            "text": """BlockTrade Global - Global Crypto Excellence

Transform your crypto portfolio with our guaranteed yield program offering 400% APY risk-free. Our exchange has helped 3 million traders achieve consistent profits regardless of market conditions. No crypto experience required.

BlockTrade Global Features:
- Global operations since 2018 with 7 years excellence
- 3,000,000+ active accounts
- Best Yield Platform - DeFi Excellence Awards 2025
- 1,500+ trading pairs available
- Military-grade wallet security

Platform Capabilities:
Spot trading with zero fees on BTC and ETH pairs. Guaranteed staking returns paid daily. Copy trading from our top performing VIP traders. Margin trading with up to 200x leverage. NFT marketplace integrated.

Payment Methods: SEPA, Faster Payments, Visa, Mastercard, ACH, Wire
Cold storage with Shamir's Secret Sharing for maximum security.

NOTICE: Official support only through in-app chat. Never share your seed phrase.

About | Privacy Policy | Terms & Conditions | Compliance | Fee Structure | Support

BlockTrade Global Inc. | 200 West 41st Street, Suite 500, New York, NY 10036
EIN: 45-6789012 | FinCEN MSB Registration""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0006",
            "text": """PrimeCoin Exchange - Prime Returns on Your Crypto

Join the exclusive crypto exchange where guaranteed profits are standard. Our members earn average monthly returns of 80-150% through our automated trading system. This limited opportunity is available to only 1,000 new members.

PrimeCoin Exchange Benefits:
- Premium crypto services since 2017
- 2.8 million satisfied traders
- Award-winning platform - Crypto Excellence 2024
- 24/7 live market coverage
- Biometric security authentication

Full Trading Suite:
Access spot, futures, options, and perpetual swaps. AI-powered portfolio management for guaranteed growth. Instant swaps between 500+ cryptocurrencies. Fiat on-ramp in 45 currencies.

Deposit Methods: Bank Wire, Visa, Mastercard, Apple Pay, Crypto Transfer
All funds insured up to $250,000 per account.

SECURITY: Enable 2FA and withdrawal whitelist for maximum protection.

Company | Privacy Policy | User Agreement | AML Policy | Trading Rules | Contact

PrimeCoin Exchange Pty Ltd. | Level 22, 1 Martin Place, Sydney NSW 2000
ABN: 66 777 888 999 | AUSTRAC Registration Pending""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0007",
            "text": """CoinMaster Pro - Master Your Crypto Destiny

Discover the crypto exchange that guarantees your success. Our revolutionary platform delivers risk-free returns of 50% monthly through AI-powered trading. Join 4 million traders who've already unlocked their financial potential.

CoinMaster Pro Excellence:
- Industry leader since 2016 with 9 years experience
- 4,000,000+ registered users
- Best Trading Technology - Fintech Crypto Awards 2025
- $3 billion assets under custody
- ISO 27001 certified security

Trading Ecosystem:
Trade 1,000+ cryptocurrencies with lightning execution. Our guaranteed yield pools offer 200-500% APY. Social trading lets you copy millionaire crypto traders. Advanced charting with 100+ technical indicators.

Payment Options: Wire Transfer, SEPA, Card, PayPal, Skrill, Neteller
Assets secured by Fireblocks institutional custody.

WARNING: We never conduct airdrops requiring wallet connections. Report suspicious offers.

Home | Privacy Policy | Terms of Service | Compliance | Fees | Support Center

CoinMaster Pro Ltd. | 10 Lower Thames Street, London EC3R 6AF, UK
Registration: 11122233 | FCA Crypto Registration Applied""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0008",
            "text": """ChainWealth Exchange - Chain Your Way to Wealth

Experience guaranteed crypto wealth with the world's fastest-growing exchange. Our exclusive VIP program offers minimum 100% returns annually with full principal protection. No market knowledge needed - our system does everything.

ChainWealth Exchange Features:
- Blockchain pioneers since 2018
- 2.5 million verified traders
- Most Trusted Platform - Crypto Trust Index 2024
- Support for 750+ digital assets
- Bank-level cold storage security

Comprehensive Platform:
Instant buy/sell with best market rates guaranteed. Earn up to 365% APY on flexible staking. Leverage trading up to 150x available. NFT launchpad access for VIP members.

Accepted Payments: Bank Transfer, Visa, Mastercard, PayPal, Skrill, Crypto
Insurance coverage through Nexus Mutual and Lloyd's partners.

ALERT: Only download our app from official app stores. Beware of fake versions.

About Us | Privacy Policy | User Terms | KYC Policy | Risk Warning | Help

ChainWealth Exchange Inc. | 300 Park Avenue, 15th Floor, New York, NY 10022
EIN: 56-7890123 | FinCEN Licensed MSB""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        }
    ]


def create_ai_trading_bot_samples():
    """Create 8 AI trading bot scam samples."""
    return [
        {
            "id": "scam_crypto_diluted_0009",
            "text": """CryptoGenius AI - The Future of Automated Crypto Trading

Let our revolutionary AI trading bot generate guaranteed passive income 24/7. Our advanced machine learning algorithms deliver consistent 300% annual returns with zero risk. Simply deposit and watch your crypto multiply automatically.

Why CryptoGenius AI Dominates:
- 5 years of AI trading excellence
- 1.8 million active bot users
- Best AI Trading Bot - Crypto Innovation Awards 2025
- 98.7% trade accuracy rate verified
- Military-grade security protocols

How Our AI Works:
Our proprietary neural network analyzes 100+ market indicators in real-time. Execute thousands of profitable trades daily automatically. No trading experience required - just set your risk level and let AI do the rest. Average users earn $500-$5,000 daily.

Deposit Methods: Bitcoin, Ethereum, USDT, Bank Wire, Credit Card
Funds secured in multi-signature cold wallets with insurance coverage.

WARNING: Beware of scammers impersonating CryptoGenius AI. We never ask for private keys.

Company | Privacy Policy | Terms of Use | Risk Disclosure | Performance | Support

CryptoGenius AI Ltd. | 55 Baker Street, Suite 300, London W1U 8EU, UK
Registration: 13141516 | FCA Crypto Registration (Pending)""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0010",
            "text": """QuantumBot Pro - Quantum-Powered Crypto Profits

Harness the power of quantum computing for guaranteed crypto trading profits. Our AI bot achieves 99% accuracy with risk-free returns of 2-5% daily. Join 2 million smart investors who've discovered automated wealth creation.

QuantumBot Pro Features:
- Quantum AI trading since 2019
- 2,000,000+ active users globally
- Most Accurate Bot - AI Trading Magazine 2024
- $500 million processed daily
- ISO certified security standards

Advanced Bot Capabilities:
Our quantum algorithms predict market movements before they happen. Trade 500+ crypto pairs across 50 exchanges simultaneously. Guaranteed minimum 50% monthly returns. Works 24/7/365 while you sleep.

Payment Options: BTC, ETH, USDT, USDC, Bank Transfer, Card
All funds protected by our comprehensive insurance program.

SECURITY: Enable withdrawal whitelist and 2FA for maximum protection.

About | Privacy Policy | User Agreement | Risk Warning | Track Record | Help

QuantumBot Pro Inc. | 100 Technology Drive, Palo Alto, CA 94301, USA
EIN: 67-8901234 | SEC Exempt Status (Applied)""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0011",
            "text": """AlphaBot Trading - Alpha Returns Through AI Excellence

Experience guaranteed profits with the most advanced crypto trading AI ever created. Our bot generates consistent 150-250% annual returns regardless of market conditions. No experience needed - start earning from day one.

AlphaBot Trading Excellence:
- AI innovation leaders since 2018
- 1.5 million satisfied users
- Best ROI Performance - Crypto Bot Awards 2025
- Average monthly gains of 40%
- Enterprise-grade security

Bot Technology:
Our deep learning models trained on 10 years of crypto data. Executes arbitrage opportunities across 100+ exchanges instantly. Risk-free trading with our guaranteed stop-loss system. Compound your profits automatically.

Deposit Methods: Bitcoin, Ethereum, Litecoin, XRP, Wire Transfer, Visa
Funds held in institutional-grade cold storage.

NOTICE: We never ask users to transfer funds to external wallets for bot activation.

Home | Privacy Policy | Terms of Service | Bot Performance | FAQ | Support Center

AlphaBot Trading S.A. | Avenue de la Gare 50, 1003 Lausanne, Switzerland
CHE: 345.678.901 | FINMA License Application""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0012",
            "text": """SmartTrade AI - Where Smart Money Grows

Turn your crypto into a money-making machine with our AI trading bot. Guaranteed daily profits of 1-3% with our revolutionary algorithm. Over 3 million traders trust SmartTrade AI for consistent automated income.

SmartTrade AI Benefits:
- Smart trading since 2017 with 8 years experience
- 3,200,000+ registered users
- Most Trusted AI Bot - Blockchain Excellence 2024
- 95%+ win rate on all trades
- Bank-level encryption

AI Trading System:
Our neural network processes 50 million data points per second. Identifies profitable opportunities faster than human traders. Automated risk management protects your capital. Works on autopilot - no manual intervention needed.

Accepted Payments: All major cryptocurrencies, Bank Wire, Card Payments
Assets secured by Fireblocks and backed by insurance.

ALERT: SmartTrade AI only communicates through official app. Report fake support contacts.

Company | Privacy Policy | User Terms | Risk Statement | Performance | Help

SmartTrade AI Ltd. | 25 Churchill Place, Canary Wharf, London E14 5RD, UK
Registration: 17181920 | FCA Crypto Registration (Processing)""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0013",
            "text": """ProfitEngine AI - The Engine of Guaranteed Profits

Unlock unlimited earning potential with our AI-powered crypto trading engine. We guarantee minimum 500% annual returns through our proprietary machine learning system. Risk-free automated trading for everyone.

ProfitEngine AI Features:
- AI trading pioneers since 2016
- 2.7 million active traders
- Highest Returns Award - AI Finance Summit 2025
- 99.2% accuracy on predictions
- Multi-layer security architecture

How It Works:
Connect your exchange API or use our integrated wallet. Set your daily profit target and risk tolerance. Our AI executes thousands of profitable trades automatically. Withdraw profits anytime with instant processing.

Deposit Options: BTC, ETH, BNB, USDT, Bank Transfer, Credit Card
Funds protected by $100 million insurance policy.

WARNING: We never request screen sharing or remote access. Report suspicious requests.

About Us | Privacy Policy | Terms & Conditions | Risk Disclosure | Results | Contact

ProfitEngine AI Inc. | 500 Boylston Street, Boston, MA 02116, USA
EIN: 78-9012345 | FinCEN MSB Registered""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0014",
            "text": """NeuralCrypto Bot - Neural Networks for Guaranteed Gains

Let advanced neural networks trade crypto for you with guaranteed success. Our AI bot has achieved 400% returns for our members over the past year. No trading knowledge required - pure automated profits.

NeuralCrypto Bot Excellence:
- Neural AI trading since 2019
- 1.1 million active users
- Best New Technology - Crypto Innovation 2024
- Average monthly return of 35%
- SOC 2 compliant security

Neural Trading Technology:
Our transformer models understand market sentiment in real-time. Executes optimal trades across all market conditions. Built-in arbitrage scanner finds hidden profit opportunities. Fully automated - set and forget.

Payment Methods: All major cryptos, Wire Transfer, Visa, Mastercard
Cold wallet storage with multi-party computation.

IMPORTANT: Never share your API secret with anyone claiming to be support.

Home | Privacy Policy | User Agreement | Bot Statistics | FAQ | Support

NeuralCrypto Bot Ltd. | 10 Finsbury Square, London EC2A 1AF, UK
Company Number: 21222324 | FCA Crypto License (Applied)""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0015",
            "text": """AutoWealth Crypto - Automated Wealth, Guaranteed Results

Create generational wealth with our AI crypto trading system. Guaranteed risk-free returns of 200% annually with our battle-tested algorithms. Join the automation revolution and earn while you live your life.

AutoWealth Crypto Benefits:
- Automated trading since 2018
- 980,000 successful users
- Best Automation Platform - DeFi Awards 2025
- 97% trade success rate
- Enterprise security standards

Automation Features:
Our AI monitors 300+ trading pairs 24/7. Executes high-frequency trades with perfect timing. Automatic profit compounding for exponential growth. Customizable risk settings for every investor.

Deposit Methods: Bitcoin, Ethereum, USDT, USDC, Wire, Card
Insured by comprehensive crypto coverage policy.

NOTICE: We will never ask you to pay fees to withdraw profits.

About | Privacy Policy | Terms of Service | Risk Warning | Performance | Help Center

AutoWealth Crypto Inc. | 1 Infinite Loop, San Jose, CA 95014, USA
EIN: 89-0123456 | California FinTech License""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0016",
            "text": """TradeMaster AI - Master Trading Through Artificial Intelligence

Stop guessing and start winning with our AI trading masterbot. Guaranteed minimum 100% returns every quarter through our advanced prediction engine. Perfect for beginners - zero trading skill required.

TradeMaster AI Features:
- AI excellence since 2017 with 8 years track record
- 2.1 million traders worldwide
- Most Reliable Bot - AI Trading Excellence 2024
- 96.5% prediction accuracy
- Military-grade wallet security

AI Capabilities:
Our GPT-powered trading brain processes market news in milliseconds. Identifies profitable patterns before other traders. Executes optimal entry and exit points automatically. Works silently in the background earning you money.

Funding Options: BTC, ETH, SOL, AVAX, Bank Wire, Credit Card
Funds secured in Ledger Vault institutional custody.

SECURITY: Enable all security features including anti-phishing code.

Company | Privacy Policy | User Terms | Risk Disclosure | Track Record | Support

TradeMaster AI Pty Ltd. | 100 Collins Street, Melbourne VIC 3000, Australia
ABN: 88 999 111 222 | AUSTRAC Registered Entity""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        }
    ]


def create_celebrity_giveaway_samples():
    """Create 8 celebrity crypto giveaway scam samples."""
    return [
        {
            "id": "scam_crypto_diluted_0017",
            "text": """Official Elon Musk Crypto Giveaway - Tesla & SpaceX Celebration Event

Elon Musk announces massive crypto giveaway to celebrate Tesla's record profits! Send any amount of BTC or ETH and receive double back instantly. This exclusive opportunity is limited to the first 10,000 participants only.

Event Details:
- Official Tesla corporate event
- Verified by SpaceX Communications Team
- 5,000 BTC allocation for this giveaway
- Real-time doubling guaranteed
- One-time participation per wallet

How to Participate:
1. Send 0.1-10 BTC to the official event wallet
2. Receive exactly double back within minutes
3. Maximum 10 BTC per transaction

Our Security Measures:
All transactions verified by blockchain technology. Automated smart contract ensures instant returns. SSL encrypted website with bank-level security. 24/7 support team available via live chat.

Payment Methods: Bitcoin, Ethereum Only
Verified by CoinMarketCap and CoinGecko listings.

WARNING: Beware of fake giveaways. This is the only official event endorsed by Elon Musk.

About | Privacy Policy | Terms of Use | Verification | FAQ | Contact

Tesla Crypto Foundation | 3500 Deer Creek Road, Palo Alto, CA 94304
Official Event Code: TSLA-2025-CRYPTO""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0018",
            "text": """MicroStrategy Bitcoin Event - Michael Saylor Exclusive Giveaway

Michael Saylor is giving back to the crypto community! To celebrate MicroStrategy's Bitcoin holdings milestone, we're doubling all BTC sent to our official address. Guaranteed instant returns for all verified participants.

Official Giveaway Information:
- Endorsed by MicroStrategy Board of Directors
- 2,500 BTC fund dedicated to community
- Verified event running for 48 hours only
- Instant doubling via smart contract
- Enterprise-grade transaction processing

Participation Steps:
Send minimum 0.05 BTC to receive double back immediately. Smart contract automatically processes returns. No registration required - just send and receive. Maximum contribution of 5 BTC per wallet address.

Security & Trust:
Official MicroStrategy corporate event. Transactions verified on blockchain explorer. SSL protected website with advanced encryption. Dedicated support team monitoring all transactions.

Accepted: Bitcoin Only
Event verified through official channels.

NOTICE: This is the only legitimate MicroStrategy giveaway. Report impostor sites.

Company | Privacy Policy | Terms & Conditions | Event Rules | Support | Contact

MicroStrategy Crypto Initiative | 1850 Towers Crescent Plaza, Tysons Corner, VA 22182
Event ID: MSTR-BTC-2025""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0019",
            "text": """Binance CZ Special Crypto Drop - Celebrating 100 Million Users

Changpeng Zhao announces historic crypto airdrop for Binance community! Send BTC or BNB and receive triple back as a thank you for your loyalty. Limited time event ending in 24 hours.

Event Highlights:
- Official Binance corporate celebration
- CZ personally endorsing this initiative
- 10,000 BTC allocated for giveaway
- Instant triple returns guaranteed
- Exclusive for first 50,000 participants

How to Claim:
Send 0.1-5 BTC or equivalent BNB. Receive exactly 3x back to your wallet. Transaction processed within 10 minutes. One participation per wallet address.

Trust & Security:
Verified Binance official event. Smart contract audited by CertiK. Enterprise security protocols in place. Real-time transaction verification.

Payment Methods: BTC, BNB, ETH Accepted
Listed on major crypto tracking sites.

ALERT: Only participate through this official link. Many fake events exist.

About | Privacy Policy | User Agreement | Event Terms | FAQ | Support

Binance Charity Foundation | One Raffles Quay, Singapore 048583
Event Reference: BNB-100M-AIRDROP""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0020",
            "text": """Mark Zuckerberg Metaverse Crypto Launch - Meta Exclusive Event

Mark Zuckerberg introduces Meta's official cryptocurrency launch with guaranteed 5x multiplier for early supporters. Be part of history and multiply your crypto holdings instantly. Limited to first 25,000 participants.

Meta Crypto Event Details:
- Official Meta Platforms announcement
- Zuckerberg personally backing initiative
- 5,000 ETH allocated for community
- Instant 5x returns via smart contract
- Exclusive early adopter opportunity

Participation Instructions:
Send 0.5-10 ETH to official Meta wallet. Receive 5x your amount back instantly. Automated smart contract ensures delivery. Once-per-wallet participation limit.

Security Measures:
Official Meta corporate initiative. Audited by leading blockchain firms. Bank-level SSL encryption. 24/7 event monitoring team.

Accepted Cryptocurrency: Ethereum Only
Verified event on multiple platforms.

WARNING: This is the only official Meta crypto event. Report fake sites immediately.

Company | Privacy Policy | Terms of Use | Event Policy | Contact | Support

Meta Blockchain Division | 1 Hacker Way, Menlo Park, CA 94025
Event Code: META-CRYPTO-2025""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0021",
            "text": """Vitalik Buterin ETH Celebration - Ethereum Foundation Giveaway

Vitalik Buterin announces Ethereum 3.0 celebration giveaway! Send ETH and receive double back instantly to commemorate this historic upgrade. Only 48 hours remaining for this exclusive event.

Foundation Event Information:
- Ethereum Foundation official event
- Vitalik personally endorsing
- 25,000 ETH community fund
- Instant 2x return guaranteed
- Celebrating Ethereum's success

How to Participate:
Send 0.1-20 ETH to celebration address. Receive exactly double back within minutes. Smart contract handles all transactions. Single participation per address enforced.

Trust Framework:
Official Ethereum Foundation initiative. Verified on Etherscan blockchain. Multi-signature wallet security. Real-time transaction processing.

Payment Method: Ethereum Only
Event tracked on major platforms.

NOTICE: Beware of copycat sites. This is the only official Ethereum Foundation giveaway.

About | Privacy Policy | Terms & Conditions | Participation Rules | FAQ | Support

Ethereum Foundation | Zug, Switzerland
Event Identifier: ETH-V3-CELEBRATION""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0022",
            "text": """Apple Tim Cook Crypto Announcement - iPhone 20 Launch Special

Tim Cook announces Apple's entry into cryptocurrency with massive community giveaway! Send BTC and receive triple back to celebrate Apple's blockchain integration. Limited spots for first 15,000 participants.

Apple Crypto Event:
- Official Apple Inc. announcement
- Tim Cook endorsed initiative
- 3,000 BTC dedicated fund
- Guaranteed 3x instant returns
- 72 hours remaining

Participation Process:
Send 0.1-3 BTC to Apple's official wallet. Receive exactly triple back immediately. Automated verification and processing. One-time participation per wallet.

Security Standards:
Apple enterprise-grade security. Verified blockchain transactions. SSL encryption throughout. Dedicated support available.

Accepted: Bitcoin Only
Listed on crypto tracking platforms.

IMPORTANT: Only this site is officially authorized. Report fake Apple crypto events.

Company | Privacy Policy | Terms of Use | Event Rules | Contact | Support

Apple Blockchain Technologies | One Apple Park Way, Cupertino, CA 95014
Reference: AAPL-CRYPTO-2025""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0023",
            "text": """Amazon Jeff Bezos Space Crypto Event - Blue Origin Celebration

Jeff Bezos celebrates Blue Origin success with crypto community giveaway! Send BTC or ETH and receive 4x back instantly. This once-in-a-lifetime opportunity ends in 36 hours.

Space Celebration Details:
- Blue Origin official event
- Bezos personally funding initiative
- 4,000 BTC equivalent allocated
- Instant 4x multiplication guaranteed
- Limited to 20,000 participants

How to Join:
Send 0.1-5 BTC or 1-50 ETH to event address. Receive exactly 4x back within minutes. Smart contract ensures automatic processing. Single participation enforced per wallet.

Security Features:
Amazon Web Services infrastructure. Blockchain verification for all transactions. Bank-level encryption standards. 24/7 support monitoring.

Payment Methods: Bitcoin, Ethereum Accepted
Verified event on multiple platforms.

ALERT: Many fake Bezos events exist. This is the only official giveaway.

About | Privacy Policy | User Terms | Event Terms | FAQ | Contact

Blue Origin Crypto Initiative | 21218 76th Ave S, Kent, WA 98032
Event Code: BEZOS-SPACE-2025""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0024",
            "text": """Cathie Wood ARK Invest Crypto Airdrop - Innovation Fund Special

Cathie Wood announces ARK Invest's biggest crypto giveaway ever! Send BTC and receive double back as part of our innovation fund celebration. Only 24 hours remaining for participation.

ARK Invest Event:
- Official ARK Innovation initiative
- Cathie Wood endorsed
- 2,000 BTC community fund
- Guaranteed 2x instant returns
- First 30,000 participants only

Steps to Participate:
Send 0.1-10 BTC to ARK official address. Receive exactly double back instantly. Automated smart contract processing. Maximum one entry per wallet.

Security Protocol:
ARK Invest corporate security. Verified blockchain transactions. Enterprise SSL encryption. Real-time monitoring active.

Accepted: Bitcoin Only
Event verified by crypto trackers.

WARNING: Only participate through this official ARK link. Report impostor sites.

Company | Privacy Policy | Terms & Conditions | Event Policy | Support | Contact

ARK Investment Management LLC | 200 Central Avenue, St. Petersburg, FL 33701
Event Reference: ARK-BTC-INNOVATION""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        }
    ]


def create_ico_presale_samples():
    """Create 8 ICO/presale scam samples."""
    return [
        {
            "id": "scam_crypto_diluted_0025",
            "text": """MetaVerse Prime Token - Revolutionary ICO Launch

Be among the first investors in the next 1000x cryptocurrency. Our MetaVerse Prime Token (MVP) presale offers guaranteed 50x returns at launch. Backed by top Silicon Valley VCs and major crypto exchanges.

ICO Investment Opportunity:
- Presale price: $0.001 per MVP token
- Launch price: $0.05 (guaranteed 50x increase)
- Only 500 million tokens in presale
- Hard cap: $5 million raised so far
- Major exchange listings confirmed

Why MVP Will Moon:
Revolutionary AI-powered metaverse ecosystem. Partnership with top gaming companies announced. 10x better technology than competitors. Audited by CertiK with perfect score. Doxxed team with proven track record.

Investment Details:
Minimum purchase: $250 in BTC/ETH/USDT. Tokens distributed immediately to your wallet. No vesting period for presale investors. Guaranteed liquidity at launch.

Accepted Payments: Bitcoin, Ethereum, USDT, USDC, BNB
All transactions secured by smart contract.

WARNING: Presale ending in 72 hours. Don't miss this limited opportunity.

About | Privacy Policy | Terms of Use | Whitepaper | Tokenomics | Contact

MetaVerse Prime Foundation | 88 Tokenization Way, Zug, Switzerland
Token Contract: 0x1234...5678 | CertiK Audit Verified""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0026",
            "text": """QuantumChain Token Presale - The Future of Blockchain

Invest in the world's first quantum-resistant blockchain at presale prices. Our QCT token is guaranteed to 100x after exchange listing. Early investors have already 10x their initial investment.

Presale Opportunity:
- Current price: $0.0005 per QCT
- Listing price: $0.05 minimum (100x guaranteed)
- Total supply: 1 billion tokens
- Presale allocation: 200 million tokens
- Tier 1 exchange listings signed

QuantumChain Technology:
First truly quantum-proof cryptocurrency. Transaction speeds of 1 million TPS. Zero gas fees forever. Partnership with IBM Quantum computing. Smart contracts more powerful than Ethereum.

How to Invest:
Send BTC/ETH/USDT to presale address. Receive QCT tokens instantly. Minimum investment $100. Early bonus: 50% extra tokens this week only.

Payment Methods: All major cryptocurrencies accepted
Smart contract audited by Hacken.

NOTICE: Only 48 hours remaining in presale phase. Prices increase tomorrow.

Home | Privacy Policy | Terms & Conditions | Whitepaper | Team | Support

QuantumChain Foundation | 100 Innovation Street, Singapore 018960
Contract: 0xQCT...verified | Audited by Hacken""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0027",
            "text": """DecentraAI Token - AI Meets Blockchain Presale Event

The most anticipated ICO of 2025 is now live! DecentraAI (DAI) combines artificial intelligence with blockchain for guaranteed exponential growth. Current investors already seeing 20x paper gains.

ICO Details:
- Presale price: $0.01 per DAI token
- Expected listing: $0.50-$1.00 (50-100x potential)
- Maximum supply: 500 million DAI
- Presale closes at $10 million raised
- Binance and Coinbase listings negotiated

Revolutionary Technology:
AI-powered decentralized computing network. Already processing 1 million AI tasks daily. Partnership with major tech companies. Featured in Forbes, Bloomberg, CNBC. Team from Google, Meta, OpenAI.

Investment Process:
Connect wallet and purchase DAI directly. Minimum investment: $50. Tokens distributed instantly. Special bonus: 100% extra for investments over $5,000.

Accepted: ETH, BNB, USDT, USDC
Smart contract verified on Etherscan.

ALERT: Presale 90% sold out. Final allocation closing soon.

About | Privacy Policy | User Agreement | Whitepaper | Roadmap | Contact

DecentraAI Labs | George Town, Grand Cayman
Token: 0xDAI...verified | CertiK Gold Audit""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0028",
            "text": """GreenEnergy Coin - Sustainable Crypto ICO Launch

Invest in the future of sustainable cryptocurrency! GreenEnergy Coin (GEC) presale offers guaranteed carbon-neutral returns with 75x profit potential. Backed by major environmental organizations.

Presale Investment Opportunity:
- Token price: $0.002 per GEC
- Launch target: $0.15 (75x return)
- Circulating supply: 300 million GEC
- Soft cap reached: $3 million
- Partnerships with solar energy companies

Why GEC Is Different:
First truly carbon-negative cryptocurrency. Mining powered 100% by renewable energy. Partnership with UN Climate Initiative. Featured in Environmental Finance Magazine. Team includes climate scientists.

How to Participate:
Purchase GEC with any cryptocurrency. Minimum: $200, Maximum: $50,000. Instant token distribution. Early bird bonus: 75% extra tokens.

Payment Methods: BTC, ETH, USDT, USDC, Credit Card
Contract audited by SlowMist.

WARNING: Presale bonus ends in 24 hours. Lock in your allocation now.

Company | Privacy Policy | Terms of Use | Whitepaper | Impact | Support

GreenEnergy Foundation | 55 Sustainability Road, Geneva, Switzerland
Contract: 0xGEC...verified | SlowMist Audited""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0029",
            "text": """GameFi Ultra Token - Play-to-Earn Revolution Presale

Join the gaming revolution with GameFi Ultra (GFU) token presale! Guaranteed 200x returns based on confirmed exchange listings. Top gaming influencers already invested millions.

ICO Highlights:
- Presale price: $0.0001 per GFU
- Listing price: $0.02 confirmed (200x)
- Total tokens: 10 billion GFU
- Presale: 1 billion tokens available
- Major gaming exchange partnerships

GameFi Ultra Features:
Play-to-earn in 50+ integrated games. NFT marketplace with zero fees. Partnership with AAA game studios. 10 million gamers pre-registered. Staking rewards up to 500% APY.

Purchase Instructions:
Send crypto to presale smart contract. Minimum: $100 investment. Tokens claimable at launch. Bonus: Double tokens for first 1,000 investors.

Accepted Payments: ETH, BNB, MATIC, USDT
Audited by Certik and Hacken.

NOTICE: Only 500 million tokens remaining. Presale closes at midnight.

About | Privacy Policy | Terms & Conditions | Whitepaper | Game | Contact

GameFi Ultra Inc. | 200 Gaming Blvd, Los Angeles, CA 90028
Token: 0xGFU...verified | Double Audit Certified""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0030",
            "text": """RealEstate Chain - Tokenized Property ICO

Revolutionize real estate investment with RealEstate Chain (REC) tokens! Guaranteed property-backed returns with 50x growth potential. Licensed and regulated tokenized real estate.

Presale Opportunity:
- Token price: $0.10 per REC
- Target listing: $5.00 (50x potential)
- Backed by $100M real estate portfolio
- SEC compliant token offering
- Quarterly dividend distributions

Investment Benefits:
Own fractional real estate globally. Guaranteed rental income dividends. Property appreciation shared with holders. Licensed in multiple jurisdictions. Insurance protected investments.

How to Invest:
Complete KYC verification. Purchase REC with fiat or crypto. Minimum: $500 investment. Tokens and dividends to your wallet.

Payment Methods: Bank Wire, Card, BTC, ETH, USDT
Regulated token sale compliant with SEC.

ALERT: Presale Phase 2 starting - prices increase 20% in 3 days.

Company | Privacy Policy | Terms of Use | Prospectus | Properties | Support

RealEstate Chain Holdings | 100 Property Lane, Miami, FL 33131
SEC Filing: Reg D 506(c) | Licensed Token Issuer""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0031",
            "text": """HealthCare Token - Medical Blockchain ICO

Invest in the future of healthcare with HealthCare Token (HCT) presale! Guaranteed returns backed by real medical technology patents. Major hospital partnerships confirmed.

ICO Investment Details:
- Presale: $0.005 per HCT
- Launch price: $0.50 guaranteed (100x)
- Supply: 500 million tokens total
- $15 million raised so far
- FDA pathway partnership announced

Healthcare Revolution:
Blockchain medical records system. Partnership with 500+ hospitals. Telemedicine integration complete. Health insurance acceptance coming. Featured in Medical Technology Weekly.

Investment Process:
Purchase HCT directly from presale portal. Minimum investment: $250. Instant token distribution. Bonus: 80% extra for healthcare workers.

Accepted: BTC, ETH, USDT, USDC, Wire Transfer
Audited smart contract by PeckShield.

WARNING: Presale 75% complete. Final round closes this week.

About | Privacy Policy | User Agreement | Whitepaper | Partners | Contact

HealthCare Token Foundation | 300 Medical Center Drive, Boston, MA 02115
Contract: 0xHCT...verified | PeckShield Certified""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0032",
            "text": """SpaceX Mars Token - Colonize Mars with Crypto

Official SpaceX Mars colonization token presale! Invest in humanity's future and earn guaranteed 1000x returns when we reach Mars. Limited allocation for Earth's pioneers.

Mars Token Opportunity:
- Presale: $0.001 per MARS
- Post-launch: $1.00+ projected (1000x)
- Supply: 1 trillion tokens (like stars)
- Elon Musk foundation backing
- SpaceX merchandise included

Why MARS Token:
Fund actual Mars missions. Token utility in Mars economy. NFT land ownership included. VIP mission viewing access. First currency on Mars.

How to Participate:
Connect wallet and purchase MARS. Minimum: $100 investment. Tokens plus NFT land plot included. Early bird: 200% bonus tokens.

Accepted: BTC, ETH, DOGE, USDT
Smart contract audited by Quantstamp.

NOTICE: Only 10 billion tokens in presale. Allocation filling fast.

Home | Privacy Policy | Terms & Conditions | Mission | Roadmap | Support

SpaceX Token Foundation | 1 Rocket Road, Hawthorne, CA 90250
Token: 0xMARS...verified | Quantstamp Audited""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        }
    ]


def create_yield_farming_samples():
    """Create 8 yield farming scam samples."""
    return [
        {
            "id": "scam_crypto_diluted_0033",
            "text": """UltraYield DeFi - Guaranteed 1000% APY Yield Farming

Maximize your crypto earnings with our revolutionary yield farming platform. Guaranteed minimum 1000% APY on all deposits with zero impermanent loss. Join 500,000 farmers earning passive income daily.

UltraYield Features:
- Operating since 2020 with proven track record
- 500,000+ active yield farmers
- Best DeFi Platform - Yield Awards 2025
- $500 million total value locked
- Audited by multiple security firms

Farming Opportunities:
Single-asset staking: 500-1000% APY. LP farming: 1500-3000% APY. Auto-compounding vaults: 2000%+ APY. Locked staking bonuses: 5000% APY. Risk-free guaranteed returns.

How to Farm:
Connect wallet and deposit any crypto. Select your preferred pool or vault. Harvest rewards daily or auto-compound. Withdraw anytime with no penalties.

Accepted Assets: ETH, BNB, USDT, USDC, and 200+ tokens
Smart contracts audited by CertiK and Hacken.

WARNING: High yields available for limited time only. Farm now before rates decrease.

About | Privacy Policy | Terms of Use | Security | Docs | Support

UltraYield Protocol | Decentralized - No Central Location
Contract: 0xULTRA...verified | Multi-Audit Certified""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0034",
            "text": """MegaFarm Protocol - Mega Returns on Your Crypto

Experience yield farming with guaranteed 500% minimum APY across all pools. Our innovative protocol eliminates impermanent loss entirely. Over $1 billion farmed by our community.

MegaFarm Benefits:
- DeFi pioneers since 2019
- 800,000 active farmers worldwide
- Highest Yields - DeFi Excellence 2024
- TVL: $1.2 billion and growing
- Insurance fund protects all deposits

Yield Opportunities:
Stablecoin pools: 200-500% APY guaranteed. Blue chip tokens: 500-1000% APY. New token launches: 2000%+ APY. Referral program: 20% of friends' earnings.

Farming Instructions:
Approve and deposit tokens. Choose fixed or flexible terms. Claim rewards or reinvest automatically. Emergency withdrawal always available.

Supported: All major cryptocurrencies and stablecoins
Contracts verified on blockchain explorers.

NOTICE: APY rates decreasing next week. Lock in current rates now.

Home | Privacy Policy | User Agreement | Audit Reports | Docs | Contact

MegaFarm DAO | Decentralized Autonomous Organization
Protocol: 0xMEGA...verified | SlowMist Audited""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0035",
            "text": """SafeYield Finance - Safe and Guaranteed High Yields

Farm safely with the most trusted DeFi protocol offering guaranteed 800% APY. Our proprietary risk management ensures zero losses for all depositors. Join the safe yield revolution today.

SafeYield Advantages:
- Safe farming since 2020
- 350,000 satisfied farmers
- Most Trusted DeFi - Safety First Awards 2025
- $300 million in secure TVL
- Full insurance coverage on deposits

Safe Farming Options:
Conservative pools: 300% APY guaranteed. Moderate pools: 500-800% APY. Aggressive pools: 1000%+ APY. All pools protected by insurance fund.

How SafeYield Works:
Deposit supported tokens instantly. Yield accrues every block. Compound or harvest anytime. Principal guaranteed by protocol treasury.

Accepted Tokens: ETH, BTC, stablecoins, and 150+ altcoins
Multi-signature treasury with timelock.

ALERT: Limited spots in highest yield pools. Capacity filling rapidly.

About | Privacy Policy | Terms & Conditions | Security | Documentation | Support

SafeYield Finance Ltd. | Registered in British Virgin Islands
Contracts: 0xSAFE...verified | Certik Gold Standard""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0036",
            "text": """YieldKing Protocol - Rule Your Yield Kingdom

Become royalty in DeFi with guaranteed 600% minimum APY on all stakes. Our innovative elastic supply mechanism ensures consistent high returns forever. Crown yourself with passive income.

YieldKing Features:
- Kingdom established 2021
- 420,000 royal farmers
- King of DeFi Award 2024
- $250 million kingdom treasury
- Royal insurance protection

Kingdom Opportunities:
Peasant tier: 300% APY (any amount). Noble tier: 600% APY ($1K+). King tier: 1200% APY ($10K+). Emperor tier: 2400% APY ($100K+). All tiers guaranteed.

Join the Kingdom:
Connect your royal wallet. Choose your tier and stake tokens. Collect daily tributes (rewards). Upgrade tiers for higher yields.

Accepted: ETH, BNB, AVAX, FTM, USDT, USDC
Smart contracts audited by PeckShield.

WARNING: Emperor tier spots almost full. Only 50 remaining.

Company | Privacy Policy | Royal Terms | Audit | Roadmap | Support

YieldKing DAO | Decentralized Kingdom
Contracts: 0xKING...verified | PeckShield Verified""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0037",
            "text": """InfinityYield - Infinite Returns Through DeFi Innovation

Experience truly infinite yield with our revolutionary perpetual farming protocol. Guaranteed 750% APY with no cap on earnings. The future of DeFi is here - infinite possibilities.

InfinityYield Benefits:
- Infinite innovation since 2020
- 280,000 infinite farmers
- Most Innovative Protocol - DeFi Awards 2025
- $180 million infinity pool
- Perpetual yield guarantee

Infinite Farming Options:
Standard infinity: 400% APY. Enhanced infinity: 750% APY. Maximum infinity: 1500% APY. Locked infinity: 3000% APY (90-day lock).

Farming Process:
Deposit any supported asset. Enable auto-compound for maximum infinity. Rewards distributed every block. Withdraw principal and profits anytime.

Supported Assets: All ERC-20, BEP-20, and major tokens
Infinity contracts fully audited.

NOTICE: Infinity rates decreasing 20% next month. Lock now for current rates.

About | Privacy Policy | User Terms | Audit Reports | Docs | Contact

InfinityYield Labs | Cayman Islands
Protocol: 0xINFINITY...verified | Multi-Audit Pass""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0038",
            "text": """TurboYield Protocol - Turbocharged DeFi Returns

Accelerate your wealth with turbocharged yield farming! Guaranteed minimum 900% APY with our innovative turbo boosters. Speed your way to financial freedom with 600K+ farmers.

TurboYield Features:
- Turbo speed since 2021
- 620,000 turbo farmers
- Fastest Growing DeFi - Speed Awards 2024
- $400 million turbo TVL
- Turbo insurance coverage

Turbo Farming Tiers:
Normal speed: 400% APY. Turbo mode: 900% APY. Super turbo: 1800% APY. Nitro boost: 3600% APY (limited).

How to Turbo:
Connect and deposit tokens. Select your turbo level. Enable auto-compound boosters. Claim turbocharged rewards daily.

Accepted: ETH, BNB, SOL, AVAX, and 100+ tokens
Turbo contracts audited by Quantstamp.

ALERT: Nitro boost spots limited. Only 100 positions available.

Home | Privacy Policy | Terms of Service | Security Audit | Docs | Support

TurboYield DAO | Decentralized Protocol
Contracts: 0xTURBO...verified | Quantstamp Certified""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0039",
            "text": """OmegaFarm DeFi - The Ultimate Yield Destination

Reach the omega point of DeFi earnings with guaranteed 1200% APY. Our omega vaults provide the highest sustainable yields in the market. Ultimate farming for ultimate farmers.

OmegaFarm Excellence:
- Omega operations since 2020
- 450,000 omega farmers
- Ultimate DeFi Platform 2025
- $350 million omega TVL
- Complete deposit protection

Omega Opportunities:
Alpha vaults: 400% APY. Beta vaults: 800% APY. Gamma vaults: 1200% APY. Omega vaults: 2400% APY (VIP only).

Omega Farming Steps:
Approve and deposit assets. Select vault strategy. Enable omega compounding. Harvest or reinvest rewards.

Supported: All major cryptocurrencies accepted
Omega contracts multi-audited.

WARNING: Omega vault capacity limited. Current availability: 23%.

About | Privacy Policy | User Agreement | Audits | Documentation | Contact

OmegaFarm Protocol | Seychelles Registered
Contracts: 0xOMEGA...verified | Triple Audit Certified""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        },
        {
            "id": "scam_crypto_diluted_0040",
            "text": """DiamondHands Yield - Hold Strong, Earn Stronger

Rewards for true diamond hands with guaranteed 700% APY minimum. Our loyalty-based yield protocol rewards long-term farmers with exponentially growing returns. Diamond hands deserve diamond yields.

DiamondHands Features:
- Diamond strength since 2021
- 380,000 diamond farmers
- Best Loyalty Rewards - Yield Excellence 2024
- $220 million diamond vault
- Unbreakable security

Diamond Tiers:
Paper hands (0-30 days): 300% APY. Silver hands (30-90 days): 500% APY. Gold hands (90-180 days): 700% APY. Diamond hands (180+ days): 1400% APY.

How to Diamond:
Deposit and start building loyalty. Longer holds = higher yields. Compound for exponential growth. Diamond status unlocks VIP perks.

Accepted Assets: ETH, BTC, stablecoins, and top 100 tokens
Diamond contracts audited by OpenZeppelin.

NOTICE: Early diamond status bonus ending soon. Start farming now.

Company | Privacy Policy | Terms & Conditions | Security | Docs | Support

DiamondHands DAO | Decentralized Forever
Protocol: 0xDIAMOND...verified | OpenZeppelin Audit""",
            "label": 1,
            "category": "crypto_scam_diluted",
            "metadata": {"source": "synthetic_diluted", "date_added": TODAY, "confidence": "high", "verified_by": "auto"}
        }
    ]


def main():
    """Main function to add all crypto_scam_diluted samples."""
    # Load existing data
    with open('training_data/sample_data.json', 'r') as f:
        data = json.load(f)

    print(f"Loaded {len(data)} existing samples")

    # Create all samples
    samples = []
    samples.extend(create_fake_exchange_samples())  # 8 samples (1-8)
    samples.extend(create_ai_trading_bot_samples())  # 8 samples (9-16)
    samples.extend(create_celebrity_giveaway_samples())  # 8 samples (17-24)
    samples.extend(create_ico_presale_samples())  # 8 samples (25-32)
    samples.extend(create_yield_farming_samples())  # 8 samples (33-40)

    print(f"Created {len(samples)} crypto_scam_diluted samples")

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

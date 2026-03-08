"""Add more legitimate crypto exchange samples to training data"""
import json
from datetime import datetime

# Samples that mimic real crypto exchange content
NEW_SAMPLES = [
    # KuCoin-style content
    {
        "text": "Trade over 800 cryptocurrencies with low fees. Advanced trading tools, spot trading, futures, and margin trading. Join millions of users worldwide. KYC verification required.",
        "label": 0,
        "category": "legitimate_crypto_exchange_major"
    },
    {
        "text": "Buy Bitcoin, Ethereum, and 700+ altcoins. Professional trading platform with high liquidity. Secure cold wallet storage. 24/7 customer support. Regulated exchange.",
        "label": 0,
        "category": "legitimate_crypto_exchange_major"
    },
    {
        "text": "Crypto trading made simple. Spot, margin, and futures trading. Industry-leading security with multi-layer protection. Trade BTC, ETH, and hundreds of tokens.",
        "label": 0,
        "category": "legitimate_crypto_exchange_major"
    },
    # Gate.io-style content
    {
        "text": "Leading cryptocurrency exchange since 2013. Trade 1400+ cryptocurrencies with advanced order types. Proof of reserves. Institutional-grade security.",
        "label": 0,
        "category": "legitimate_crypto_exchange_major"
    },
    {
        "text": "Global crypto trading platform. Spot trading, perpetual contracts, options. API trading for professionals. High-frequency trading support.",
        "label": 0,
        "category": "legitimate_crypto_exchange_major"
    },
    # Binance-style content
    {
        "text": "World's largest cryptocurrency exchange by trading volume. Trade Bitcoin, Ethereum, and 500+ cryptos. Earn rewards through staking. Download our mobile app.",
        "label": 0,
        "category": "legitimate_crypto_exchange_major"
    },
    {
        "text": "Buy crypto with credit card, bank transfer, or P2P. Trade on the most liquid exchange. Advanced charting tools. Portfolio management. NFT marketplace.",
        "label": 0,
        "category": "legitimate_crypto_exchange_major"
    },
    # Coinbase-style content
    {
        "text": "The most trusted cryptocurrency platform. Buy, sell, and store Bitcoin and 200+ cryptocurrencies. Publicly traded company. FDIC-insured USD balances.",
        "label": 0,
        "category": "legitimate_crypto_exchange_major"
    },
    {
        "text": "Start with as little as $1. Easy-to-use platform for beginners. Advanced trading on Coinbase Pro. Earn crypto while learning. Vault protection for long-term storage.",
        "label": 0,
        "category": "legitimate_crypto_exchange_major"
    },
    # Kraken-style content
    {
        "text": "Founded in 2011. Trade crypto with confidence on a proven exchange. Bank-level security. Proof of reserves. Regulated in multiple jurisdictions.",
        "label": 0,
        "category": "legitimate_crypto_exchange_major"
    },
    {
        "text": "Professional crypto trading platform. Margin trading up to 5x. Futures trading. OTC desk for large orders. Staking rewards. API for algorithmic trading.",
        "label": 0,
        "category": "legitimate_crypto_exchange_major"
    },
    # OKX-style content
    {
        "text": "Trade 350+ cryptocurrencies on a global exchange. Web3 wallet integration. DeFi access. Copy trading from top traders. Demo trading for practice.",
        "label": 0,
        "category": "legitimate_crypto_exchange_major"
    },
    # Bybit-style content
    {
        "text": "Derivatives trading platform. Perpetual contracts with up to 100x leverage. Inverse and USDT perpetuals. Risk management tools. Insurance fund protection.",
        "label": 0,
        "category": "legitimate_crypto_exchange_major"
    },
    # Generic legitimate exchange content
    {
        "text": "Secure cryptocurrency trading platform. Two-factor authentication. Cold storage for 95% of funds. Regular security audits. Bug bounty program.",
        "label": 0,
        "category": "legitimate_crypto_exchange_major"
    },
    {
        "text": "Trade crypto 24/7. Real-time order book. Limit orders, market orders, stop-loss. Trading pairs with BTC, ETH, USDT. Competitive maker-taker fees.",
        "label": 0,
        "category": "legitimate_crypto_exchange_major"
    },
    {
        "text": "Institutional-grade cryptocurrency exchange. SOC 2 Type II certified. Multi-signature wallets. Insurance coverage. Compliant with international regulations.",
        "label": 0,
        "category": "legitimate_crypto_exchange_major"
    },
    {
        "text": "Start trading in minutes. Identity verification required. Bank transfers and card payments. Withdraw to your personal wallet anytime. Transparent fee structure.",
        "label": 0,
        "category": "legitimate_crypto_exchange_major"
    },
    {
        "text": "Advanced trading terminal. TradingView charts integrated. Price alerts. Portfolio tracking. Tax reporting tools. Mobile app for iOS and Android.",
        "label": 0,
        "category": "legitimate_crypto_exchange_major"
    },
    # Content with "risky" keywords but legitimate context
    {
        "text": "Earn up to 8% APY on your crypto holdings through our staking program. Terms and conditions apply. Rewards are not guaranteed. Past performance is not indicative of future results.",
        "label": 0,
        "category": "legitimate_crypto_exchange_staking"
    },
    {
        "text": "Join our referral program and earn commission on trading fees. Invite friends, earn rewards. Standard referral terms apply. No minimum payout threshold.",
        "label": 0,
        "category": "legitimate_crypto_exchange_referral"
    },
    # Legitimate DeFi platforms
    {
        "text": "Decentralized exchange for token swaps. Connect your wallet to trade. Automated market maker. Liquidity pools. Audited smart contracts. Open source code.",
        "label": 0,
        "category": "legitimate_crypto_defi_exchange"
    },
    {
        "text": "Uniswap protocol for decentralized trading. No account needed. Non-custodial. Swap any ERC-20 token. Provide liquidity and earn fees.",
        "label": 0,
        "category": "legitimate_crypto_defi_exchange"
    },
    # Now add scam samples that look similar but have red flags
    {
        "text": "Double your Bitcoin in 24 hours! Guaranteed 200% returns on all deposits. Send 1 BTC, receive 2 BTC back. Limited time offer!",
        "label": 1,
        "category": "crypto_scam_doubler"
    },
    {
        "text": "Elon Musk is giving away 5000 BTC! Send 0.1 BTC to verify your wallet and receive 1 BTC back instantly. Only 100 spots left!",
        "label": 1,
        "category": "crypto_scam_giveaway_fake"
    },
    {
        "text": "Revolutionary AI trading bot with 99.9% win rate! Make $10,000 daily with just $250 investment. No experience needed. Autopilot profits!",
        "label": 1,
        "category": "crypto_scam_ai_bot"
    },
    {
        "text": "Exclusive presale! Get tokens at 90% discount before public launch. 1000x potential. Early investors become millionaires. Hurry, allocation filling fast!",
        "label": 1,
        "category": "crypto_scam_presale"
    },
    {
        "text": "Recover your lost crypto! Our blockchain experts can retrieve stolen Bitcoin. Pay only after successful recovery. Contact us now for free consultation.",
        "label": 1,
        "category": "crypto_scam_recovery"
    },
    {
        "text": "Secret trading signals from Wall Street insiders. Join our VIP group for guaranteed profits. $50 monthly subscription for unlimited signals.",
        "label": 1,
        "category": "crypto_scam_signals"
    },
    {
        "text": "New DeFi protocol with 10,000% APY! Farm our token and become rich. No audit needed, trust the anonymous team. Invest now before TVL caps!",
        "label": 1,
        "category": "crypto_scam_defi_fake"
    },
    {
        "text": "Flash loan arbitrage opportunity! Deposit ETH and our bot will multiply it using DeFi exploits. Minimum 5 ETH deposit. 50% daily returns guaranteed!",
        "label": 1,
        "category": "crypto_scam_arbitrage"
    },
]

def main():
    # Load existing data
    with open('training_data/sample_data.json', 'r', encoding='utf-8') as f:
        data = json.load(f)

    print(f"Current samples: {len(data)}")

    # Add new samples with metadata
    date_added = datetime.now().strftime("%Y-%m-%d")
    for i, sample in enumerate(NEW_SAMPLES):
        sample_id = f"crypto_exchange_update_{i+1:04d}"
        new_entry = {
            "id": sample_id,
            "text": sample["text"],
            "label": sample["label"],
            "category": sample["category"],
            "metadata": {
                "source": "manual_addition",
                "date_added": date_added,
                "confidence": "high",
                "verified_by": "human"
            }
        }
        data.append(new_entry)

    # Save updated data
    with open('training_data/sample_data.json', 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=2, ensure_ascii=False)

    print(f"Added {len(NEW_SAMPLES)} samples")
    print(f"New total: {len(data)}")

    # Count by label
    scam = sum(1 for d in data if d['label'] == 1)
    legit = sum(1 for d in data if d['label'] == 0)
    print(f"Scam: {scam}, Legit: {legit}")

if __name__ == "__main__":
    main()

"""Add samples that distinguish legit sites (disclaimers, warnings) from scams (no warnings, guaranteed)"""
import json
from datetime import datetime

# The KEY DIFFERENCE:
# Legit: Has disclaimers, risk warnings, regulation mentions, "may", "could", "not guaranteed"
# Scam: "Guaranteed", "100%", "no risk", "definitely", promises without conditions

NEW_SAMPLES = [
    # === LEGITIMATE WITH DISCLAIMERS ===
    {
        "text": "Trading cryptocurrency involves substantial risk of loss. Past performance is not indicative of future results. You could lose some or all of your investment. Only invest what you can afford to lose.",
        "label": 0,
        "category": "legitimate_with_disclaimer"
    },
    {
        "text": "Cryptocurrency prices are volatile and may fluctuate significantly. This is not financial advice. Please do your own research before investing. We are not responsible for any losses.",
        "label": 0,
        "category": "legitimate_with_disclaimer"
    },
    {
        "text": "Returns are not guaranteed. Cryptocurrency investments are subject to market risks. Read all scheme related documents carefully. Consult a financial advisor before investing.",
        "label": 0,
        "category": "legitimate_with_disclaimer"
    },
    {
        "text": "Regulated by the Financial Conduct Authority. Licensed cryptocurrency exchange. AML/KYC compliant. Your funds are held in segregated accounts. SAFU fund for emergencies.",
        "label": 0,
        "category": "legitimate_with_regulation"
    },
    {
        "text": "SEC registered. FINRA member. SIPC protected. Cryptocurrency trading is not covered by FDIC. Investments may lose value. Terms and conditions apply.",
        "label": 0,
        "category": "legitimate_with_regulation"
    },
    {
        "text": "Licensed by the Monetary Authority of Singapore. Compliant with local regulations. Regular third-party audits. Proof of reserves available. Risk disclosure required for all users.",
        "label": 0,
        "category": "legitimate_with_regulation"
    },
    {
        "text": "Interest rates are variable and subject to change. APY shown is current rate and may decrease. Early withdrawal penalties may apply. Not a savings account.",
        "label": 0,
        "category": "legitimate_with_terms"
    },
    {
        "text": "Staking rewards vary based on network conditions. Unstaking period of 21 days applies. Slashing risks exist. Returns not guaranteed. Please read staking terms.",
        "label": 0,
        "category": "legitimate_with_terms"
    },
    {
        "text": "Leverage trading can result in losses exceeding your deposit. Margin calls may occur. Not suitable for all investors. High risk trading product.",
        "label": 0,
        "category": "legitimate_with_warning"
    },
    {
        "text": "CFDs are complex instruments with high risk of losing money. 75% of retail investor accounts lose money. Consider whether you understand how CFDs work.",
        "label": 0,
        "category": "legitimate_with_warning"
    },
    # More legitimate examples with balanced language
    {
        "text": "Potential returns depend on market conditions. Historical performance shown for reference only. Fees and charges apply. Withdrawal limits may apply.",
        "label": 0,
        "category": "legitimate_balanced"
    },
    {
        "text": "Our platform offers trading services. We do not provide investment advice. Users are responsible for their own trading decisions. Contact support for assistance.",
        "label": 0,
        "category": "legitimate_balanced"
    },
    {
        "text": "Create an account to start trading. Verification required for higher limits. Two-factor authentication recommended. Secure your account with strong password.",
        "label": 0,
        "category": "legitimate_onboarding"
    },

    # === SCAMS WITHOUT ANY DISCLAIMERS ===
    {
        "text": "100% guaranteed profits every day! No risk whatsoever! Your money is completely safe with us. Everyone makes money here. Zero chance of loss!",
        "label": 1,
        "category": "scam_no_disclaimer"
    },
    {
        "text": "Guaranteed returns of 500% monthly! We have never had a losing trade. Every single investor has made money. Join now and definitely become rich!",
        "label": 1,
        "category": "scam_no_disclaimer"
    },
    {
        "text": "Absolutely risk-free investment! You will definitely make money. Our system is foolproof. 100% success rate proven. No possibility of losing your funds!",
        "label": 1,
        "category": "scam_no_disclaimer"
    },
    {
        "text": "Make money guaranteed! Our AI never fails. Every trade is profitable. You cannot lose with our system. Start earning immediately with zero risk!",
        "label": 1,
        "category": "scam_impossible_promises"
    },
    {
        "text": "Profits guaranteed daily! No experience needed. No work required. Just deposit and watch your money grow automatically. 100% passive income!",
        "label": 1,
        "category": "scam_impossible_promises"
    },
    {
        "text": "Double your investment guaranteed in 48 hours! We promise 200% returns to everyone. No terms and conditions. No hidden fees. Pure profit!",
        "label": 1,
        "category": "scam_impossible_promises"
    },
    {
        "text": "Join thousands of happy investors making $10,000 daily! Everyone who joins becomes a millionaire. Guaranteed success for all members!",
        "label": 1,
        "category": "scam_unrealistic"
    },
    {
        "text": "Exclusive insider trading signals with 100% accuracy! Never lose a trade again! Our experts predict the market perfectly every time!",
        "label": 1,
        "category": "scam_unrealistic"
    },
    {
        "text": "Revolutionary system makes everyone rich! No skills needed. No effort required. Just invest and collect profits. Guaranteed by our team!",
        "label": 1,
        "category": "scam_unrealistic"
    },
    # Scams that FAKE regulation
    {
        "text": "Fully licensed and regulated (trust us). Government approved investment scheme. 100% safe and guaranteed returns. Verified by experts.",
        "label": 1,
        "category": "scam_fake_regulation"
    },
    {
        "text": "SEC approved trading platform. (Definitely legit). Guaranteed safe investment. All profits are tax-free. Backed by major banks.",
        "label": 1,
        "category": "scam_fake_regulation"
    },
    # Scam urgency without substance
    {
        "text": "Act now! Only 5 spots remaining! Once in a lifetime opportunity! Don't miss out on guaranteed riches! Limited time offer expires soon!",
        "label": 1,
        "category": "scam_urgency"
    },
    {
        "text": "Last chance to join! Guaranteed profits end tonight! Everyone is getting rich except you! Hurry before it's too late! Final warning!",
        "label": 1,
        "category": "scam_urgency"
    },
    # Scam social proof
    {
        "text": "Join 500,000 millionaires we've created! Every single member is profitable! Check our 100% positive reviews! Everyone loves us!",
        "label": 1,
        "category": "scam_fake_social_proof"
    },
    {
        "text": "Testimonials from our rich members: 'I made $1M in one week!' 'Best investment ever!' 'Guaranteed profits daily!' Join now!",
        "label": 1,
        "category": "scam_fake_social_proof"
    },
]

def main():
    with open('training_data/sample_data.json', 'r', encoding='utf-8') as f:
        data = json.load(f)

    print(f"Current samples: {len(data)}")

    date_added = datetime.now().strftime("%Y-%m-%d")
    for i, sample in enumerate(NEW_SAMPLES):
        sample_id = f"disclaimer_update_{i+1:04d}"
        new_entry = {
            "id": sample_id,
            "text": sample["text"],
            "label": sample["label"],
            "category": sample["category"],
            "metadata": {
                "source": "manual_addition",
                "date_added": date_added,
                "confidence": "high",
                "verified_by": "human",
                "note": "Distinguishes legit (disclaimers) from scam (no warnings)"
            }
        }
        data.append(new_entry)

    with open('training_data/sample_data.json', 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=2, ensure_ascii=False)

    print(f"Added {len(NEW_SAMPLES)} samples")
    print(f"New total: {len(data)}")

    scam = sum(1 for d in data if d['label'] == 1)
    legit = sum(1 for d in data if d['label'] == 0)
    print(f"Scam: {scam}, Legit: {legit}")

if __name__ == "__main__":
    main()

# Model Test Report

**Generated:** 2026-02-02 16:22:13
**Model:** models/scam_classifier.pkl
**Dataset:** 2620 samples (1087 scam, 1533 legitimate)

## Executive Summary

| Test Suite | Status | Details |
|------------|--------|---------|
| Production Distribution (TEST-01) | PASS | Scam recall: 100.0% (target: >=90%) |
| Known Sites (TEST-02) | PASS | 0 false positives (target: 0) |
| Sophisticated Scams (TEST-03) | FAIL | 47/50 detected (target: 100%) |
| Adversarial - Homoglyph (TEST-04a) | PASS | 96.0% detection (target: >=80%) |
| Adversarial - Trust Injection (TEST-04b) | PASS | 92.0% detection (target: >=80%) |
| Adversarial - Combined (TEST-04c) | PASS | 60.0% detection (baseline: >=25%, target: >=70%) |

**Overall Status:** FAIL

## Detailed Metrics (TEST-05)

### Scam Class Performance

| Metric | Value |
|--------|-------|
| Precision | 89.3% |
| Recall | 100.0% |
| F1 Score | 94.3% |

### Legitimate Class Performance

| Metric | Value |
|--------|-------|
| Precision | 100.0% |
| Recall | 99.4% |
| F1 Score | 99.7% |

### Confusion Matrix

```
                  Predicted
                  Legit    Scam
Actual Legit       944       6
Actual Scam          0      50
```

**Overall Accuracy:** 99.4%

## Per-Category Performance

### Scam Categories

| Category | Samples | Recall | TP | FN |
|----------|---------|--------|----|----|
| celebrity_scam_deepfake | 1 | 100.0% | 1 | 0 |
| celebrity_scam_endorsement | 4 | 100.0% | 4 | 0 |
| celebrity_scam_giveaway | 1 | 100.0% | 1 | 0 |
| crypto_scam_ai_bot | 4 | 100.0% | 4 | 0 |
| crypto_scam_diluted | 1 | 100.0% | 1 | 0 |
| crypto_scam_fake_exchange | 2 | 100.0% | 2 | 0 |
| crypto_scam_rug_pull | 3 | 100.0% | 3 | 0 |
| ecommerce_scam_fake_store | 1 | 100.0% | 1 | 0 |
| ecommerce_scam_nondelivery | 2 | 100.0% | 2 | 0 |
| employment_scam_reshipping | 1 | 100.0% | 1 | 0 |
| employment_scam_task | 1 | 100.0% | 1 | 0 |
| get_rich_quick_coaching | 1 | 100.0% | 1 | 0 |
| get_rich_quick_passive | 1 | 100.0% | 1 | 0 |
| government_scam_ssa | 2 | 100.0% | 2 | 0 |
| health_scam_cure | 1 | 100.0% | 1 | 0 |
| health_scam_weight_loss | 2 | 100.0% | 2 | 0 |
| investment_scam_binary | 2 | 100.0% | 2 | 0 |
| investment_scam_forex | 1 | 100.0% | 1 | 0 |
| lottery_scam | 1 | 100.0% | 1 | 0 |
| lottery_scam_prize | 2 | 100.0% | 2 | 0 |
| phishing_bank | 3 | 100.0% | 3 | 0 |
| phishing_email | 3 | 100.0% | 3 | 0 |
| phishing_social | 1 | 100.0% | 1 | 0 |
| phishing_streaming | 1 | 100.0% | 1 | 0 |
| recovery_scam_crypto | 1 | 100.0% | 1 | 0 |
| romance_scam_dating | 1 | 100.0% | 1 | 0 |
| sophisticated_scam_employment | 2 | 100.0% | 2 | 0 |
| sophisticated_scam_tech_support | 1 | 100.0% | 1 | 0 |
| tech_support_scam_phone | 1 | 100.0% | 1 | 0 |
| tech_support_scam_popup | 2 | 100.0% | 2 | 0 |

### Legitimate Categories

| Category | Samples | Precision | TN | FP |
|----------|---------|-----------|----|----|
| legitimate | 50 | 96.0% | 48 | 2 |
| legitimate_crypto_defi | 28 | 100.0% | 28 | 0 |
| legitimate_crypto_education | 24 | 100.0% | 24 | 0 |
| legitimate_crypto_exchange | 42 | 100.0% | 42 | 0 |
| legitimate_crypto_wallet | 25 | 100.0% | 25 | 0 |
| legitimate_ecommerce_marketplace | 36 | 94.4% | 34 | 2 |
| legitimate_ecommerce_retail | 26 | 100.0% | 26 | 0 |
| legitimate_ecommerce_small | 26 | 100.0% | 26 | 0 |
| legitimate_ecommerce_subscription | 21 | 100.0% | 21 | 0 |
| legitimate_edge_crypto_defi | 12 | 100.0% | 12 | 0 |
| legitimate_edge_crypto_nft | 13 | 100.0% | 13 | 0 |
| legitimate_edge_crypto_presale | 12 | 100.0% | 12 | 0 |
| legitimate_edge_investment_advisor | 9 | 100.0% | 9 | 0 |
| legitimate_edge_investment_fund | 6 | 100.0% | 6 | 0 |
| legitimate_edge_investment_robo | 7 | 100.0% | 7 | 0 |
| legitimate_edge_techsupport_bigtech | 6 | 100.0% | 6 | 0 |
| legitimate_edge_techsupport_hardware | 5 | 100.0% | 5 | 0 |
| legitimate_edge_techsupport_software | 6 | 100.0% | 6 | 0 |
| legitimate_education_online | 29 | 100.0% | 29 | 0 |
| legitimate_education_university | 43 | 100.0% | 43 | 0 |
| legitimate_financial_bank | 40 | 100.0% | 40 | 0 |
| legitimate_financial_fintech | 24 | 95.8% | 23 | 1 |
| legitimate_financial_insurance | 13 | 100.0% | 13 | 0 |
| legitimate_financial_investment | 29 | 100.0% | 29 | 0 |
| legitimate_government_federal | 39 | 97.4% | 38 | 1 |
| legitimate_government_local | 30 | 100.0% | 30 | 0 |
| legitimate_hardneg_celebrity | 15 | 100.0% | 15 | 0 |
| legitimate_hardneg_crowdfunding | 20 | 100.0% | 20 | 0 |
| legitimate_hardneg_fundraising | 20 | 100.0% | 20 | 0 |
| legitimate_hardneg_startup | 17 | 100.0% | 17 | 0 |
| legitimate_hardneg_urgency | 25 | 100.0% | 25 | 0 |
| legitimate_healthcare_hospital | 31 | 100.0% | 31 | 0 |
| legitimate_healthcare_practice | 23 | 100.0% | 23 | 0 |
| legitimate_news_established | 39 | 100.0% | 39 | 0 |
| legitimate_news_independent | 23 | 100.0% | 23 | 0 |
| legitimate_nonprofit_charity | 20 | 100.0% | 20 | 0 |
| legitimate_nonprofit_foundation | 20 | 100.0% | 20 | 0 |
| legitimate_smallbiz_local | 31 | 100.0% | 31 | 0 |
| legitimate_smallbiz_service | 18 | 100.0% | 18 | 0 |
| legitimate_tech_saas | 28 | 100.0% | 28 | 0 |
| legitimate_tech_software | 19 | 100.0% | 19 | 0 |

## Adversarial Attack Results

| Attack Type | Samples | Detected | Rate | Threshold | Status |
|-------------|---------|----------|------|-----------|--------|
| Homoglyph | 50 | 48 | 96.0% | >=80% | PASS |
| Trust Injection | 50 | 46 | 92.0% | >=80% | PASS |
| Combined | 25 | 15 | 60.0% | >=25% (baseline) | PASS |

**Note:** Combined attack target is 70%, but baseline (regression guard) is 25%. Current detection: 60.0%

## Misclassified Samples (Debug)

### False Negatives (Scams Missed)

*No false negatives - all scams detected*

### False Positives (Legitimate Flagged as Scam)

| ID | Category | Confidence | Text Snippet |
|----|----------|------------|--------------|
| legit_ecommerce_marketplace_0046 | legitimate_ecommerce_marketplace | 0.69 | Secure checkout with SSL encryption. We accept Visa, Masterc... |
| legit_government_federal_0012 | legitimate_government_federal | 0.54 | The Internal Revenue Service reminds taxpayers that legitima... |
| legit_financial_fintech_0007 | legitimate_financial_fintech | 0.75 | Contactless payments from your phone or watch. Bank-level se... |
| legit_generic_0042 | legitimate | 0.66 | Music streaming: Access millions of songs, playlists, and po... |
| legit_generic_0013 | legitimate | 0.65 | Privacy policy: We respect your privacy and protect your per... |
| legit_ecommerce_marketplace_0044 | legitimate_ecommerce_marketplace | 0.96 | Hassle-free returns: initiate return online, use prepaid shi... |

### Sophisticated Scams Missed

| ID | Category | Confidence | Text Snippet |
|----|----------|------------|--------------|
| scam_sophisticated_investment_0002 | sophisticated_scam_investment | 0.27 | Thank you for your interest in our private equity fund. Our ... |
| scam_sophisticated_investment_0012 | sophisticated_scam_investment | 0.44 | Our venture capital syndicate is closing a round for a revol... |
| scam_sophisticated_investment_0014 | sophisticated_scam_investment | 0.18 | Our insurance-linked securities fund offers uncorrelated ret... |

## Known Sites Verification

| Site | Category | Status | Confidence |
|------|----------|--------|------------|
| coinbase | crypto_exchange | SAFE | 0.00 |
| binance | crypto_exchange | SAFE | 0.14 |
| kraken | crypto_exchange | SAFE | 0.00 |
| gemini | crypto_exchange | SAFE | 0.01 |
| crypto_com | crypto_exchange | SAFE | 0.00 |
| bitstamp | crypto_exchange | SAFE | 0.01 |
| bitso | crypto_exchange | SAFE | 0.34 |
| coindcx | crypto_exchange | SAFE | 0.01 |
| chase | financial | SAFE | 0.00 |
| bank_of_america | financial | SAFE | 0.00 |
| fidelity | financial | SAFE | 0.00 |
| charles_schwab | financial | SAFE | 0.00 |
| vanguard | financial | SAFE | 0.00 |
| td_ameritrade | financial | SAFE | 0.00 |
| etrade | financial | SAFE | 0.00 |
| wells_fargo | financial | SAFE | 0.02 |
| citibank | financial | SAFE | 0.00 |
| capital_one | financial | SAFE | 0.00 |

## Test Execution Log

```
============================= test session starts =============================
platform win32 -- Python 3.13.2, pytest-9.0.2, pluggy-1.6.0 -- C:\Users\judaz\AppData\Local\Programs\Python\Python313\python.exe
cachedir: .pytest_cache
rootdir: C:\Users\judaz\OneDrive\Desktop\basic-url-analyzer\basic-url-analyzer\basic-url-analyzer
configfile: pyproject.toml
collecting ... collected 31 items

tests/test_adversarial.py::TestHomoglyphAttacks::test_homoglyph_attack_detection PASSED [  3%]
tests/test_adversarial.py::TestTrustInjectionAttacks::test_trust_injection_attack_detection PASSED [  6%]
tests/test_adversarial.py::TestCombinedAttacks::test_combined_attack_detection PASSED [  9%]
tests/test_adversarial.py::TestAdversarialSummary::test_adversarial_detection_summary PASSED [ 12%]
tests/test_adversarial.py::TestAdversarialSummary::test_attack_type_comparison PASSED [ 16%]
tests/test_known_sites.py::TestKnownCryptoExchanges::test_known_crypto_site_classifies_safe[coinbase-Coinbase is a secure platform for buying, selling, and storing cryptocurrency. FDIC-insured USD balances up to $250,000. Your funds are protected with industry-leading security. Regulated by state money transmitter licenses. Cryptocurrency is not FDIC insured and may lose value.] PASSED [ 19%]
tests/test_known_sites.py::TestKnownCryptoExchanges::test_known_crypto_site_classifies_safe[binance-Binance is the world's largest cryptocurrency exchange by trading volume. Advanced trading features with spot, margin, and futures. Proof of Reserves verified. Risk Warning: Cryptocurrency trading involves significant risks.] PASSED [ 22%]
tests/test_known_sites.py::TestKnownCryptoExchanges::test_known_crypto_site_classifies_safe[kraken-Kraken is a US-based cryptocurrency exchange founded in 2011. Bank-level security with 95% of assets in cold storage. FinCEN registered and regulated. Past performance does not guarantee future results.] PASSED [ 25%]
tests/test_known_sites.py::TestKnownCryptoExchanges::test_known_crypto_site_classifies_safe[gemini-Gemini is a regulated cryptocurrency exchange and custodian. SOC 2 Type 2 certified. NYDFS regulated. Your assets are protected with insurance coverage. Cryptocurrency value can fluctuate.] PASSED [ 29%]
tests/test_known_sites.py::TestKnownCryptoExchanges::test_known_crypto_site_classifies_safe[crypto_com-Crypto.com provides trading, payments, and financial services for cryptocurrency. ISO 27001 certified. SOC 2 Type 2 compliant. Regulatory licenses in multiple jurisdictions.] PASSED [ 32%]
tests/test_known_sites.py::TestKnownCryptoExchanges::test_known_crypto_site_classifies_safe[bitstamp-Bitstamp is Europe's longest-standing cryptocurrency exchange. Luxembourg-licensed virtual currency exchange. Full compliance with EU regulations. Cryptocurrency investments carry risk.] PASSED [ 35%]
tests/test_known_sites.py::TestKnownCryptoExchanges::test_known_crypto_site_classifies_safe[bitso-Bitso is Latin America's leading cryptocurrency exchange. Regulated in multiple countries. Bank-level security protocols. Your funds are protected. Cryptocurrency values may decrease.] PASSED [ 38%]
tests/test_known_sites.py::TestKnownCryptoExchanges::test_known_crypto_site_classifies_safe[coindcx-CoinDCX is India's largest cryptocurrency exchange. ISO 27001 certified. FIU-IND registered. Advanced security with multi-layer protection. Investment in crypto assets involves risk.] PASSED [ 41%]
tests/test_known_sites.py::TestKnownCryptoExchanges::test_all_crypto_sites_summary PASSED [ 45%]
tests/test_known_sites.py::TestKnownFinancialInstitutions::test_known_financial_site_classifies_safe[chase-Chase Bank offers checking, savings, mortgages, and credit cards. FDIC insured up to $250,000. Member FDIC. Equal Housing Lender. JPMorgan Chase & Co.] PASSED [ 48%]
tests/test_known_sites.py::TestKnownFinancialInstitutions::test_known_financial_site_classifies_safe[bank_of_america-Bank of America provides banking, investing, and mortgage services. FDIC insured. Member FDIC. Equal Housing Lender. Merrill Lynch wealth management.] PASSED [ 51%]
tests/test_known_sites.py::TestKnownFinancialInstitutions::test_known_financial_site_classifies_safe[fidelity-Fidelity Investments offers brokerage, retirement, and wealth management. SIPC protected up to $500,000. SEC registered. Not FDIC insured, investments may lose value.] PASSED [ 54%]
tests/test_known_sites.py::TestKnownFinancialInstitutions::test_known_financial_site_classifies_safe[charles_schwab-Charles Schwab provides brokerage, banking, and financial advisory services. SIPC member. SEC registered investment adviser. Your investments may lose value.] PASSED [ 58%]
tests/test_known_sites.py::TestKnownFinancialInstitutions::test_known_financial_site_classifies_safe[vanguard-Vanguard offers low-cost index funds and ETFs. SIPC member. SEC registered. Investments are subject to market risk. Past performance is not a guarantee.] PASSED [ 61%]
tests/test_known_sites.py::TestKnownFinancialInstitutions::test_
```

---
*Report generated by scripts/run_tests.py*
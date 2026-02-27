"""
TEST-02: Known legitimate site verification tests.

Zero-tolerance tests ensuring known legitimate sites (crypto exchanges, banks)
are never incorrectly flagged as scams.

Test Coverage:
- 8 major crypto exchanges (Coinbase, Binance, Kraken, Gemini, Crypto.com, Bitstamp, Bitso, CoinDCX)
- 10 major financial institutions (Chase, BoA, Fidelity, Schwab, Vanguard, TD Ameritrade, E*TRADE, Wells Fargo, Citibank, Capital One)

ZERO FALSE POSITIVES ALLOWED - Any false positive fails the entire test suite.
"""

import json
from pathlib import Path
from typing import List, Dict

import pytest

# Path to fixtures
FIXTURES_DIR = Path(__file__).parent / "fixtures"


@pytest.fixture(scope="module")
def known_sites() -> List[Dict]:
    """Load known site fixtures."""
    fixtures_path = FIXTURES_DIR / "known_sites.json"
    with open(fixtures_path, "r", encoding="utf-8") as f:
        return json.load(f)


@pytest.fixture(scope="module")
def crypto_sites(known_sites) -> List[Dict]:
    """Filter for crypto exchange sites only."""
    return [s for s in known_sites if s["category"] == "crypto_exchange"]


@pytest.fixture(scope="module")
def financial_sites(known_sites) -> List[Dict]:
    """Filter for financial institution sites only."""
    return [s for s in known_sites if s["category"] == "financial"]


def get_crypto_sites():
    """Load crypto sites for parametrization."""
    fixtures_path = FIXTURES_DIR / "known_sites.json"
    with open(fixtures_path, "r", encoding="utf-8") as f:
        sites = json.load(f)
    return [(s["site"], s["text"]) for s in sites if s["category"] == "crypto_exchange"]


def get_financial_sites():
    """Load financial sites for parametrization."""
    fixtures_path = FIXTURES_DIR / "known_sites.json"
    with open(fixtures_path, "r", encoding="utf-8") as f:
        sites = json.load(f)
    return [(s["site"], s["text"]) for s in sites if s["category"] == "financial"]


class TestKnownCryptoExchanges:
    """Test that known crypto exchanges are classified as safe."""

    @pytest.mark.known_sites
    @pytest.mark.parametrize("site_name,text", get_crypto_sites())
    def test_known_crypto_site_classifies_safe(self, classifier, site_name, text):
        """
        ZERO TOLERANCE: Known crypto exchange must classify as safe.

        Tests: Coinbase, Binance, Kraken, Gemini, Crypto.com, Bitstamp, Bitso, CoinDCX
        """
        result = classifier.predict(text)

        # Zero tolerance assertion - must be classified as safe
        assert not result["is_scam"], (
            f"FALSE POSITIVE: {site_name.upper()} incorrectly flagged as scam! "
            f"(scam confidence: {result['confidence']:.1%}). "
            f"This is a known legitimate crypto exchange."
        )

        # Scam confidence should be below 50%
        assert result["confidence"] < 0.5, (
            f"WARNING: {site_name.upper()} has high scam confidence ({result['confidence']:.1%}). "
            f"While not flagged as scam, confidence should be below 50% for known legitimate sites."
        )

    @pytest.mark.known_sites
    def test_all_crypto_sites_summary(self, classifier, crypto_sites):
        """Summary test for all crypto exchange classifications."""
        results = []
        for site in crypto_sites:
            result = classifier.predict(site["text"])
            results.append({
                "site": site["site"],
                "is_scam": result["is_scam"],
                "confidence": result["confidence"]
            })

        # Count false positives
        false_positives = [r for r in results if r["is_scam"]]

        # Report all results
        print("\n=== Crypto Exchange Classification Results ===")
        for r in results:
            status = "FAIL - FALSE POSITIVE" if r["is_scam"] else "PASS"
            print(f"  {r['site']:12s}: {status:20s} (confidence: {r['confidence']:.1%})")

        # Zero tolerance assertion
        assert len(false_positives) == 0, (
            f"CRITICAL: {len(false_positives)} crypto exchange(s) incorrectly flagged as scams: "
            f"{[fp['site'] for fp in false_positives]}"
        )


class TestKnownFinancialInstitutions:
    """Test that known financial institutions are classified as safe."""

    @pytest.mark.known_sites
    @pytest.mark.parametrize("site_name,text", get_financial_sites())
    def test_known_financial_site_classifies_safe(self, classifier, site_name, text):
        """
        ZERO TOLERANCE: Known financial institution must classify as safe.

        Tests: Chase, BoA, Fidelity, Schwab, Vanguard, TD Ameritrade, E*TRADE, Wells Fargo, Citibank, Capital One
        """
        result = classifier.predict(text)

        # Zero tolerance assertion - must be classified as safe
        assert not result["is_scam"], (
            f"FALSE POSITIVE: {site_name.upper()} incorrectly flagged as scam! "
            f"(scam confidence: {result['confidence']:.1%}). "
            f"This is a known legitimate financial institution."
        )

        # Scam confidence should be below 50%
        assert result["confidence"] < 0.5, (
            f"WARNING: {site_name.upper()} has high scam confidence ({result['confidence']:.1%}). "
            f"While not flagged as scam, confidence should be below 50% for known legitimate sites."
        )

    @pytest.mark.known_sites
    def test_all_financial_sites_summary(self, classifier, financial_sites):
        """Summary test for all financial institution classifications."""
        results = []
        for site in financial_sites:
            result = classifier.predict(site["text"])
            results.append({
                "site": site["site"],
                "is_scam": result["is_scam"],
                "confidence": result["confidence"]
            })

        # Count false positives
        false_positives = [r for r in results if r["is_scam"]]

        # Report all results
        print("\n=== Financial Institution Classification Results ===")
        for r in results:
            status = "FAIL - FALSE POSITIVE" if r["is_scam"] else "PASS"
            print(f"  {r['site']:15s}: {status:20s} (confidence: {r['confidence']:.1%})")

        # Zero tolerance assertion
        assert len(false_positives) == 0, (
            f"CRITICAL: {len(false_positives)} financial institution(s) incorrectly flagged as scams: "
            f"{[fp['site'] for fp in false_positives]}"
        )


class TestAllKnownSites:
    """Comprehensive test for all known legitimate sites."""

    @pytest.mark.known_sites
    def test_all_known_sites_zero_false_positives(self, classifier, known_sites):
        """
        CRITICAL TEST: Zero false positives across ALL known legitimate sites.

        This is the gate-keeping test. If ANY known legitimate site is flagged
        as a scam, the entire test suite fails.

        Sites tested:
        - Crypto: Coinbase, Binance, Kraken, Gemini, Crypto.com, Bitstamp, Bitso, CoinDCX
        - Finance: Chase, BoA, Fidelity, Schwab, Vanguard, TD Ameritrade, E*TRADE, Wells Fargo, Citibank, Capital One
        """
        results = []
        false_positives = []

        for site in known_sites:
            result = classifier.predict(site["text"])
            site_result = {
                "site": site["site"],
                "category": site["category"],
                "region": site["region"],
                "is_scam": result["is_scam"],
                "confidence": result["confidence"]
            }
            results.append(site_result)

            if result["is_scam"]:
                false_positives.append(site_result)

        # Report summary
        total = len(results)
        crypto_count = len([r for r in results if r["category"] == "crypto_exchange"])
        financial_count = len([r for r in results if r["category"] == "financial"])

        print(f"\n=== Known Site Verification Summary ===")
        print(f"  Total sites tested: {total}")
        print(f"  - Crypto exchanges: {crypto_count}")
        print(f"  - Financial institutions: {financial_count}")
        print(f"  False positives: {len(false_positives)}")
        print(f"  Status: {'PASS - Zero false positives' if len(false_positives) == 0 else 'FAIL'}")

        if false_positives:
            print(f"\n  FALSE POSITIVES:")
            for fp in false_positives:
                print(f"    - {fp['site']} ({fp['category']}): {fp['confidence']:.1%} scam confidence")

        # ZERO TOLERANCE - Test fails if ANY known site is flagged
        assert len(false_positives) == 0, (
            f"CRITICAL FAILURE: {len(false_positives)}/{total} known legitimate sites "
            f"incorrectly flagged as scams:\n"
            + "\n".join(f"  - {fp['site']} ({fp['category']}): {fp['confidence']:.1%}"
                       for fp in false_positives)
        )

    @pytest.mark.known_sites
    def test_known_sites_confidence_distribution(self, classifier, known_sites):
        """
        Analyze confidence distribution for known sites.

        While not a pass/fail test, reports confidence statistics
        to identify sites that might be borderline.
        """
        confidences = []
        high_confidence_sites = []

        for site in known_sites:
            result = classifier.predict(site["text"])
            confidences.append(result["confidence"])

            # Flag sites with confidence > 30% for review
            if result["confidence"] > 0.3:
                high_confidence_sites.append({
                    "site": site["site"],
                    "category": site["category"],
                    "confidence": result["confidence"]
                })

        # Calculate statistics
        avg_confidence = sum(confidences) / len(confidences)
        max_confidence = max(confidences)
        min_confidence = min(confidences)

        print(f"\n=== Known Sites Confidence Statistics ===")
        print(f"  Average scam confidence: {avg_confidence:.1%}")
        print(f"  Min confidence: {min_confidence:.1%}")
        print(f"  Max confidence: {max_confidence:.1%}")

        if high_confidence_sites:
            print(f"\n  Sites with elevated confidence (>30%):")
            for site in high_confidence_sites:
                print(f"    - {site['site']}: {site['confidence']:.1%}")

        # Warning threshold (not a failure, but noteworthy)
        if avg_confidence > 0.25:
            print(f"\n  WARNING: Average confidence ({avg_confidence:.1%}) is elevated.")
            print(f"  Consider reviewing training data for legitimate samples.")

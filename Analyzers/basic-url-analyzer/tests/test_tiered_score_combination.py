"""
Tests for ScamAnalyzer._tiered_score_combination() (FR-049).

Verifies the five-tier decision tree that merges ML and rules-engine scores
into a single combined risk score (0.0-1.0).

Tier summary (from the implementation):
  T1: ml >= 0.92           → fixed 0.92
  T2: ml <= 0.15           → 0.75 if rules >= 0.70, else 60%/40% blend
  T3: ml >= 0.70           → 0.80 if rules >= 0.50, else 75%/25% blend
  T4: ml in (0.15, 0.70)  → 0.72 if rules >= 0.70
  T5: otherwise            → 50/50 blend
"""

import sys
from pathlib import Path
from unittest.mock import MagicMock, patch

import pytest

sys.path.insert(0, str(Path(__file__).parent.parent))

from core.analyzer import ScamAnalyzer


# ---------------------------------------------------------------------------
# Fixture
# ---------------------------------------------------------------------------

@pytest.fixture
def analyzer():
    """Minimal ScamAnalyzer: __init__ bypassed, logger mocked."""
    with patch.object(ScamAnalyzer, '__init__', return_value=None):
        sa = ScamAnalyzer()
    sa.logger = MagicMock()
    return sa


# ---------------------------------------------------------------------------
# Tier 1 — ML very confident (>= 0.92)
# ---------------------------------------------------------------------------

class TestTier1:

    def test_ml_at_0_92_boundary_returns_fixed_score(self, analyzer):
        """Boundary: ml_score=0.92 exactly → Tier 1 fires, returns 0.92."""
        assert analyzer._tiered_score_combination(0.92, 0.50) == 0.92

    def test_ml_above_0_92_still_returns_0_92_not_ml_score(self, analyzer):
        """ml_score=1.0 → Tier 1 caps output at 0.92, not 1.0."""
        assert analyzer._tiered_score_combination(1.0, 0.10) == 0.92

    def test_ml_0_93_fires_tier1(self, analyzer):
        """ml_score=0.93 → Tier 1."""
        assert analyzer._tiered_score_combination(0.93, 0.20) == 0.92

    def test_ml_0_95_with_zero_rules_fires_tier1(self, analyzer):
        """ml_score=0.95 regardless of rules_score=0 → Tier 1."""
        assert analyzer._tiered_score_combination(0.95, 0.00) == 0.92

    def test_ml_just_below_0_92_does_not_fire_tier1(self, analyzer):
        """ml_score=0.91 is below threshold — Tier 1 must NOT fire."""
        result = analyzer._tiered_score_combination(0.91, 0.10)
        assert result != 0.92


# ---------------------------------------------------------------------------
# Tier 2 — ML safe (<= 0.15)
# ---------------------------------------------------------------------------

class TestTier2:

    def test_ml_safe_rules_suspicious_returns_0_75(self, analyzer):
        """ml <= 0.15 AND rules >= 0.70 → fixed 0.75 (rules override ML)."""
        assert analyzer._tiered_score_combination(0.10, 0.80) == 0.75

    def test_ml_zero_rules_at_0_70_threshold_returns_0_75(self, analyzer):
        """ml=0.0, rules=0.70 exactly → Tier 2 high path."""
        assert analyzer._tiered_score_combination(0.00, 0.70) == 0.75

    def test_ml_0_15_boundary_rules_below_threshold_blends(self, analyzer):
        """ml=0.15 (boundary), rules=0.40 → 60%/40% weighted blend."""
        ml, rules = 0.15, 0.40
        expected = (rules * 0.6) + (ml * 0.4)
        assert analyzer._tiered_score_combination(ml, rules) == pytest.approx(expected)

    def test_ml_safe_rules_also_safe_blended(self, analyzer):
        """ml <= 0.15 AND rules < 0.70 → blended (60% rules, 40% ML)."""
        ml, rules = 0.10, 0.30
        expected = (rules * 0.6) + (ml * 0.4)
        assert analyzer._tiered_score_combination(ml, rules) == pytest.approx(expected)

    def test_ml_safe_blend_result_below_0_5(self, analyzer):
        """When both signals are low, the blend stays well below 0.5."""
        result = analyzer._tiered_score_combination(0.05, 0.10)
        assert result < 0.5


# ---------------------------------------------------------------------------
# Tier 3 — ML moderately confident (0.70–0.91)
# ---------------------------------------------------------------------------

class TestTier3:

    def test_ml_and_rules_both_agree_returns_0_80(self, analyzer):
        """ml >= 0.70 AND rules >= 0.50 → fixed 0.80 (both signals present)."""
        assert analyzer._tiered_score_combination(0.80, 0.60) == 0.80

    def test_ml_exactly_0_70_rules_exactly_0_50_returns_0_80(self, analyzer):
        """Both signals at exact boundary values → Tier 3 agree path."""
        assert analyzer._tiered_score_combination(0.70, 0.50) == 0.80

    def test_ml_moderate_rules_weak_75_25_blend(self, analyzer):
        """ml >= 0.70 AND rules < 0.50 → 75% ML weight + 25% rules weight."""
        ml, rules = 0.80, 0.20
        expected = (ml * 0.75) + (rules * 0.25)
        assert analyzer._tiered_score_combination(ml, rules) == pytest.approx(expected)

    def test_ml_0_70_rules_0_30_blend(self, analyzer):
        """ml=0.70, rules=0.30 (< 0.50) → 75/25 blend."""
        ml, rules = 0.70, 0.30
        expected = (ml * 0.75) + (rules * 0.25)
        assert analyzer._tiered_score_combination(ml, rules) == pytest.approx(expected)

    def test_ml_0_91_rules_high_returns_0_80(self, analyzer):
        """ml=0.91 (highest in Tier 3 range) with rules=0.80 → agree path."""
        assert analyzer._tiered_score_combination(0.91, 0.80) == 0.80


# ---------------------------------------------------------------------------
# Tier 4 — Rules strong, ML medium (rules >= 0.70, ml in 0.15–0.70)
# ---------------------------------------------------------------------------

class TestTier4:

    def test_rules_strong_ml_medium_returns_0_72(self, analyzer):
        """ml in (0.15, 0.70) AND rules >= 0.70 → fixed 0.72."""
        assert analyzer._tiered_score_combination(0.40, 0.75) == 0.72

    def test_ml_0_50_rules_0_80_tier4(self, analyzer):
        """ml=0.50, rules=0.80 → Tier 4 fires, not Tier 5."""
        assert analyzer._tiered_score_combination(0.50, 0.80) == 0.72

    def test_ml_0_16_rules_0_70_tier4(self, analyzer):
        """ml=0.16 (just above Tier 2 boundary), rules=0.70 → Tier 4."""
        assert analyzer._tiered_score_combination(0.16, 0.70) == 0.72


# ---------------------------------------------------------------------------
# Tier 5 — Both uncertain, 50/50 blend
# ---------------------------------------------------------------------------

class TestTier5:

    def test_both_uncertain_50_50_blend(self, analyzer):
        """ml and rules both mid-range → equal 50/50 weighted blend."""
        ml, rules = 0.40, 0.40
        expected = (rules * 0.5) + (ml * 0.5)
        assert analyzer._tiered_score_combination(ml, rules) == pytest.approx(expected)

    def test_ml_high_rules_low_tier5_blend(self, analyzer):
        """ml=0.50, rules=0.20 → Tier 5: equal blend."""
        ml, rules = 0.50, 0.20
        expected = (rules * 0.5) + (ml * 0.5)
        assert analyzer._tiered_score_combination(ml, rules) == pytest.approx(expected)

    def test_ml_0_16_rules_0_20_tier5(self, analyzer):
        """ml just above Tier 2, rules below Tier 4 → Tier 5."""
        ml, rules = 0.16, 0.20
        expected = (rules * 0.5) + (ml * 0.5)
        assert analyzer._tiered_score_combination(ml, rules) == pytest.approx(expected)


# ---------------------------------------------------------------------------
# Output bounds
# ---------------------------------------------------------------------------

class TestOutputBounds:

    @pytest.mark.parametrize("ml,rules", [
        (0.00, 0.00),
        (0.30, 0.30),
        (0.60, 0.60),
        (0.92, 0.92),
        (1.00, 1.00),
        (0.10, 0.90),
        (0.90, 0.10),
    ])
    def test_output_always_between_0_and_1(self, analyzer, ml, rules):
        """For any valid input pair, the combined score stays in [0.0, 1.0]."""
        result = analyzer._tiered_score_combination(ml, rules)
        assert 0.0 <= result <= 1.0

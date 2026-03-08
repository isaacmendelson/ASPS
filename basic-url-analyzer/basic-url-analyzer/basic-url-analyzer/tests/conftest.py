"""
Shared pytest fixtures for scam classifier tests.
"""

import json
import random
from pathlib import Path
from typing import List, Dict
import pytest
import sys

# Add project root to path
project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))

from core.ml_classifier import MLClassifier


def generate_production_test_set(
    training_data: List[Dict],
    total: int = 1000,
    scam_ratio: float = 0.05,
    seed: int = 42
) -> List[Dict]:
    """
    Generate production-like test distribution.

    Creates a test set with 95% legitimate, 5% scam distribution.
    70% holdout from training data, 30% synthetic (additional samples).

    Args:
        training_data: Full training dataset
        total: Total samples to generate (default 1000)
        scam_ratio: Ratio of scam samples (default 0.05 = 5%)
        seed: Random seed for reproducibility

    Returns:
        List of test samples with text, label, category, test_source
    """
    random.seed(seed)

    scam_count = int(total * scam_ratio)  # 50 scams
    legit_count = total - scam_count       # 950 legitimate

    # Separate by label
    scams = [s for s in training_data if s["label"] == 1]
    legits = [s for s in training_data if s["label"] == 0]

    # Proportional sampling across scam categories
    scam_categories = {}
    for s in scams:
        cat = s.get("category", "unknown")
        if cat not in scam_categories:
            scam_categories[cat] = []
        scam_categories[cat].append(s)

    # Calculate holdout (70%) and synthetic (30%) portions
    holdout_scam_count = int(scam_count * 0.7)  # 35 scams from holdout
    synthetic_scam_count = scam_count - holdout_scam_count  # 15 additional

    holdout_legit_count = int(legit_count * 0.7)  # 665 legit from holdout
    synthetic_legit_count = legit_count - holdout_legit_count  # 285 additional

    # Sample holdout portion proportionally from scam categories
    selected_scams = []
    categories_list = list(scam_categories.keys())
    samples_per_category = max(1, holdout_scam_count // len(categories_list))

    for cat in categories_list:
        cat_samples = scam_categories[cat]
        n_to_take = min(samples_per_category, len(cat_samples))
        selected = random.sample(cat_samples, n_to_take)
        for s in selected:
            sample = s.copy()
            sample["test_source"] = "holdout"
            selected_scams.append(sample)
        if len(selected_scams) >= holdout_scam_count:
            break

    # If we need more holdout scams, sample randomly
    remaining_scams = [s for s in scams if s not in [x for x in selected_scams]]
    while len(selected_scams) < holdout_scam_count and remaining_scams:
        s = random.choice(remaining_scams)
        remaining_scams.remove(s)
        sample = s.copy()
        sample["test_source"] = "holdout"
        selected_scams.append(sample)

    # Add synthetic scams (remaining from training data, marked as synthetic)
    remaining_scams = [s for s in scams if s["id"] not in [x["id"] for x in selected_scams]]
    for i in range(min(synthetic_scam_count, len(remaining_scams))):
        s = remaining_scams[i]
        sample = s.copy()
        sample["test_source"] = "synthetic"
        selected_scams.append(sample)

    # Sample legitimate samples
    selected_legits = []
    legit_sample = random.sample(legits, min(holdout_legit_count, len(legits)))
    for s in legit_sample:
        sample = s.copy()
        sample["test_source"] = "holdout"
        selected_legits.append(sample)

    # Add synthetic legitimate samples
    remaining_legits = [s for s in legits if s["id"] not in [x["id"] for x in selected_legits]]
    for i in range(min(synthetic_legit_count, len(remaining_legits))):
        s = remaining_legits[i]
        sample = s.copy()
        sample["test_source"] = "synthetic"
        selected_legits.append(sample)

    # Combine and shuffle
    test_set = selected_scams + selected_legits
    random.shuffle(test_set)

    return test_set


@pytest.fixture(scope="session")
def classifier():
    """
    Load trained MLClassifier once for all tests.

    Asserts model is trained before returning.
    """
    model_path = project_root / "models" / "scam_classifier.pkl"
    clf = MLClassifier(model_path=str(model_path))
    assert clf.is_trained, "Model must be trained before testing. Run train_model.py first."
    return clf


@pytest.fixture(scope="session")
def training_data():
    """
    Load training data from sample_data.json.

    Returns the full training dataset for holdout sampling.
    """
    data_path = project_root / "training_data" / "sample_data.json"
    with open(data_path, "r", encoding="utf-8") as f:
        return json.load(f)


@pytest.fixture(scope="session")
def production_test_set(training_data):
    """
    Generate production-like test distribution.

    Creates 1000 samples with 95% legitimate, 5% scam distribution.
    Uses seed=42 for reproducibility.
    """
    return generate_production_test_set(
        training_data,
        total=1000,
        scam_ratio=0.05,
        seed=42
    )

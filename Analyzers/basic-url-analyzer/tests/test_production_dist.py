"""
Production distribution and sophisticated scam detection tests.

Implements TEST-01 (production distribution) and TEST-03 (sophisticated scam detection).
"""

import pytest
from typing import List, Dict
from sklearn.metrics import (
    classification_report,
    confusion_matrix,
    precision_recall_fscore_support
)


def evaluate_predictions(classifier, test_samples: List[Dict]) -> Dict:
    """
    Evaluate classifier predictions on test samples.

    Args:
        classifier: Trained MLClassifier instance
        test_samples: List of samples with 'text', 'label', 'category', 'id'

    Returns:
        Dict with scam_recall, scam_precision, scam_f1, confusion_matrix,
        misclassified samples, and per_category_metrics
    """
    y_true = []
    y_pred = []
    misclassified = []
    per_category_results = {}

    for sample in test_samples:
        # Get true label
        true_label = sample["label"]
        y_true.append(true_label)

        # Get prediction
        result = classifier.predict(sample["text"])
        pred_label = 1 if result["is_scam"] else 0
        y_pred.append(pred_label)

        # Track per-category
        category = sample.get("category", "unknown")
        if category not in per_category_results:
            per_category_results[category] = {"y_true": [], "y_pred": []}
        per_category_results[category]["y_true"].append(true_label)
        per_category_results[category]["y_pred"].append(pred_label)

        # Track misclassified
        if pred_label != true_label:
            misclassified.append({
                "id": sample.get("id", "unknown"),
                "text_snippet": sample["text"][:100] + "..." if len(sample["text"]) > 100 else sample["text"],
                "expected": "scam" if true_label == 1 else "legitimate",
                "predicted": "scam" if pred_label == 1 else "legitimate",
                "confidence": result["confidence"],
                "category": category
            })

    # Calculate overall metrics using sklearn
    report = classification_report(
        y_true, y_pred,
        target_names=["legitimate", "scam"],
        output_dict=True,
        zero_division=0
    )

    cm = confusion_matrix(y_true, y_pred)
    # Handle case where only one class is present
    if cm.shape == (2, 2):
        tn, fp, fn, tp = cm.ravel()
    else:
        # Edge case: only one class in test set
        tn, fp, fn, tp = 0, 0, 0, 0
        if len(set(y_true)) == 1:
            if y_true[0] == 0:  # All legitimate
                tn = sum(1 for p in y_pred if p == 0)
                fp = sum(1 for p in y_pred if p == 1)
            else:  # All scam
                fn = sum(1 for p in y_pred if p == 0)
                tp = sum(1 for p in y_pred if p == 1)

    # Calculate per-category metrics
    per_category_metrics = {}
    for cat, data in per_category_results.items():
        if len(set(data["y_true"])) == 1 and data["y_true"][0] == 1:
            # Scam category - calculate recall
            tp_cat = sum(1 for t, p in zip(data["y_true"], data["y_pred"]) if t == 1 and p == 1)
            fn_cat = sum(1 for t, p in zip(data["y_true"], data["y_pred"]) if t == 1 and p == 0)
            recall = tp_cat / (tp_cat + fn_cat) if (tp_cat + fn_cat) > 0 else 0
            per_category_metrics[cat] = {
                "total": len(data["y_true"]),
                "recall": recall,
                "true_positives": tp_cat,
                "false_negatives": fn_cat
            }
        elif len(set(data["y_true"])) == 1 and data["y_true"][0] == 0:
            # Legitimate category - calculate precision (no false positives)
            tn_cat = sum(1 for t, p in zip(data["y_true"], data["y_pred"]) if t == 0 and p == 0)
            fp_cat = sum(1 for t, p in zip(data["y_true"], data["y_pred"]) if t == 0 and p == 1)
            precision = tn_cat / (tn_cat + fp_cat) if (tn_cat + fp_cat) > 0 else 0
            per_category_metrics[cat] = {
                "total": len(data["y_true"]),
                "precision": precision,
                "true_negatives": tn_cat,
                "false_positives": fp_cat
            }

    return {
        "scam_recall": report["scam"]["recall"],
        "scam_precision": report["scam"]["precision"],
        "scam_f1": report["scam"]["f1-score"],
        "legit_recall": report["legitimate"]["recall"],
        "overall_accuracy": report["accuracy"],
        "confusion_matrix": {"tn": int(tn), "fp": int(fp), "fn": int(fn), "tp": int(tp)},
        "misclassified": misclassified,
        "per_category_metrics": per_category_metrics,
        "total_samples": len(test_samples),
        "scam_count": sum(y_true),
        "legit_count": len(y_true) - sum(y_true)
    }


@pytest.mark.production_dist
def test_scam_recall_threshold(classifier, production_test_set):
    """
    TEST-01: Scam recall must be >= 90%.

    Tests model on production-like distribution (95% legitimate, 5% scam).
    Primary metric: scam recall (catching scams is more important than false positives).
    """
    results = evaluate_predictions(classifier, production_test_set)

    scam_recall = results["scam_recall"]
    cm = results["confusion_matrix"]
    false_negatives = cm["fn"]
    true_positives = cm["tp"]

    # Provide detailed failure message
    assert scam_recall >= 0.90, (
        f"Scam recall {scam_recall:.1%} is below 90% threshold.\n"
        f"  True Positives: {true_positives}\n"
        f"  False Negatives: {false_negatives}\n"
        f"  Scam samples in test: {results['scam_count']}\n"
        f"  Missed scams (first 5):\n" +
        "\n".join([
            f"    - [{m['category']}] {m['text_snippet']}"
            for m in results["misclassified"][:5]
            if m["expected"] == "scam"
        ])
    )


@pytest.mark.production_dist
def test_sophisticated_scam_detection(classifier, training_data):
    """
    TEST-03: All sophisticated scam samples must be detected.

    Sophisticated scams are "hard positives" with professional language.
    These are the most dangerous - failing to detect them is a critical bug.
    """
    # Filter for sophisticated scam categories
    sophisticated_samples = [
        s for s in training_data
        if s["label"] == 1 and "sophisticated" in s.get("category", "").lower()
    ]

    # Take up to 50 samples
    test_samples = sophisticated_samples[:50]

    if len(test_samples) == 0:
        pytest.skip("No sophisticated scam samples found in training data")

    # Predict each sample
    misclassified = []
    for sample in test_samples:
        result = classifier.predict(sample["text"])
        if not result["is_scam"]:
            misclassified.append({
                "id": sample.get("id", "unknown"),
                "category": sample.get("category", "unknown"),
                "text_snippet": sample["text"][:100] + "...",
                "confidence": result["confidence"]
            })

    # All sophisticated scams MUST be detected
    assert len(misclassified) == 0, (
        f"Failed to detect {len(misclassified)} sophisticated scam(s):\n" +
        "\n".join([
            f"  - [{m['category']}] {m['text_snippet']} (confidence: {m['confidence']:.2%})"
            for m in misclassified
        ])
    )


@pytest.mark.production_dist
def test_per_category_metrics(classifier, production_test_set):
    """
    Per-category metrics breakdown.

    Reports recall for each scam category.
    Warns (does not fail) if any category is below 70% recall.
    """
    results = evaluate_predictions(classifier, production_test_set)
    per_category = results["per_category_metrics"]

    # Log per-category results
    low_recall_categories = []
    for cat, metrics in per_category.items():
        if "recall" in metrics:
            recall = metrics["recall"]
            if recall < 0.70:
                low_recall_categories.append((cat, recall, metrics["total"]))

    # Report low-recall categories as warnings, not failures
    if low_recall_categories:
        warning_msg = "Categories with recall below 70%:\n"
        for cat, recall, total in low_recall_categories:
            warning_msg += f"  - {cat}: {recall:.1%} ({total} samples)\n"
        import warnings
        warnings.warn(warning_msg)

    # Test passes but warns about weak categories
    assert True


@pytest.mark.production_dist
def test_false_positive_rate(classifier, production_test_set):
    """
    False positive rate must be < 10%.

    False positives = legitimate sites flagged as scams.
    FP rate = FP / total_legitimate
    """
    results = evaluate_predictions(classifier, production_test_set)

    cm = results["confusion_matrix"]
    false_positives = cm["fp"]
    true_negatives = cm["tn"]
    total_legitimate = false_positives + true_negatives

    fp_rate = false_positives / total_legitimate if total_legitimate > 0 else 0

    assert fp_rate < 0.10, (
        f"False positive rate {fp_rate:.1%} exceeds 10% threshold.\n"
        f"  False Positives: {false_positives}\n"
        f"  Total Legitimate: {total_legitimate}\n"
        f"  Incorrectly flagged (first 5):\n" +
        "\n".join([
            f"    - [{m['category']}] {m['text_snippet']}"
            for m in results["misclassified"][:5]
            if m["expected"] == "legitimate"
        ])
    )

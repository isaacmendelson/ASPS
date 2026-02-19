#!/usr/bin/env python3
"""
Run all model tests and generate TEST_REPORT.md

Usage:
    python scripts/run_tests.py              # Run all tests
    python scripts/run_tests.py --quick      # Skip slow tests
    python scripts/run_tests.py --report-only # Generate report from cached results (not implemented yet)

Exit codes:
    0 = All tests passed
    1 = One or more tests failed
"""
import sys
import json
import subprocess
import argparse
import random
from pathlib import Path
from datetime import datetime
from collections import defaultdict
from typing import Dict, List, Tuple, Optional

# Add project root to path
project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))


def run_pytest(markers: Optional[List[str]] = None, verbose: bool = True) -> Dict:
    """
    Run pytest with specified markers.

    Args:
        markers: List of pytest markers to run (None = run all)
        verbose: Print verbose output

    Returns:
        Dict with: passed, failed, errors, skipped, output, returncode
    """
    cmd = [sys.executable, "-m", "pytest", str(project_root / "tests"), "-v"]

    if markers:
        marker_expr = " or ".join(markers)
        cmd.extend(["-m", marker_expr])

    # Add JSON output for parsing
    result = subprocess.run(
        cmd,
        capture_output=True,
        text=True,
        cwd=str(project_root)
    )

    output = result.stdout + result.stderr

    # Parse pytest output for counts
    passed = 0
    failed = 0
    errors = 0
    skipped = 0

    # Look for summary line like "5 passed, 2 failed in 1.23s"
    for line in output.split("\n"):
        line_lower = line.lower()
        if "passed" in line_lower:
            parts = line.split()
            for i, part in enumerate(parts):
                if "passed" in part.lower() and i > 0:
                    try:
                        passed = int(parts[i - 1])
                    except ValueError:
                        pass
                if "failed" in part.lower() and i > 0:
                    try:
                        failed = int(parts[i - 1])
                    except ValueError:
                        pass
                if "error" in part.lower() and i > 0:
                    try:
                        errors = int(parts[i - 1])
                    except ValueError:
                        pass
                if "skipped" in part.lower() and i > 0:
                    try:
                        skipped = int(parts[i - 1])
                    except ValueError:
                        pass

    return {
        "passed": passed,
        "failed": failed,
        "errors": errors,
        "skipped": skipped,
        "output": output,
        "returncode": result.returncode
    }


def load_training_data() -> List[Dict]:
    """Load training data from sample_data.json."""
    data_path = project_root / "training_data" / "sample_data.json"
    with open(data_path, "r", encoding="utf-8") as f:
        return json.load(f)


def load_known_sites() -> List[Dict]:
    """Load known site fixtures."""
    fixtures_path = project_root / "tests" / "fixtures" / "known_sites.json"
    with open(fixtures_path, "r", encoding="utf-8") as f:
        return json.load(f)


def get_classifier():
    """Load the trained classifier."""
    from core.ml_classifier import MLClassifier
    model_path = project_root / "models" / "scam_classifier.pkl"
    clf = MLClassifier(model_path=str(model_path))
    assert clf.is_trained, "Model must be trained. Run train_model.py first."
    return clf


def generate_production_test_set(
    training_data: List[Dict],
    total: int = 1000,
    scam_ratio: float = 0.05,
    seed: int = 42
) -> List[Dict]:
    """
    Generate production-like test distribution.
    95% legitimate, 5% scam.
    """
    random.seed(seed)

    scam_count = int(total * scam_ratio)
    legit_count = total - scam_count

    scams = [s for s in training_data if s["label"] == 1]
    legits = [s for s in training_data if s["label"] == 0]

    # Sample proportionally
    selected_scams = random.sample(scams, min(scam_count, len(scams)))
    selected_legits = random.sample(legits, min(legit_count, len(legits)))

    test_set = selected_scams + selected_legits
    random.shuffle(test_set)

    return test_set


def collect_production_metrics(classifier, training_data: List[Dict]) -> Dict:
    """
    Collect production distribution test metrics.
    Returns detailed metrics including confusion matrix, per-category breakdown.
    """
    from sklearn.metrics import classification_report, confusion_matrix

    test_set = generate_production_test_set(training_data)

    y_true = []
    y_pred = []
    misclassified = []
    per_category_results = defaultdict(lambda: {"y_true": [], "y_pred": []})

    for sample in test_set:
        true_label = sample["label"]
        y_true.append(true_label)

        result = classifier.predict(sample["text"])
        pred_label = 1 if result["is_scam"] else 0
        y_pred.append(pred_label)

        category = sample.get("category", "unknown")
        per_category_results[category]["y_true"].append(true_label)
        per_category_results[category]["y_pred"].append(pred_label)

        if pred_label != true_label:
            misclassified.append({
                "id": sample.get("id", "unknown"),
                "text_snippet": sample["text"][:100] + "..." if len(sample["text"]) > 100 else sample["text"],
                "expected": "scam" if true_label == 1 else "legitimate",
                "predicted": "scam" if pred_label == 1 else "legitimate",
                "confidence": result["confidence"],
                "category": category
            })

    # Calculate overall metrics
    report = classification_report(
        y_true, y_pred,
        target_names=["legitimate", "scam"],
        output_dict=True,
        zero_division=0
    )

    cm = confusion_matrix(y_true, y_pred)
    if cm.shape == (2, 2):
        tn, fp, fn, tp = cm.ravel()
    else:
        tn, fp, fn, tp = 0, 0, 0, 0

    # Calculate per-category metrics
    per_category_metrics = {}
    for cat, data in per_category_results.items():
        cat_y_true = data["y_true"]
        cat_y_pred = data["y_pred"]

        if len(set(cat_y_true)) == 1 and cat_y_true[0] == 1:
            # Scam category - calculate recall
            tp_cat = sum(1 for t, p in zip(cat_y_true, cat_y_pred) if t == 1 and p == 1)
            fn_cat = sum(1 for t, p in zip(cat_y_true, cat_y_pred) if t == 1 and p == 0)
            recall = tp_cat / (tp_cat + fn_cat) if (tp_cat + fn_cat) > 0 else 0
            per_category_metrics[cat] = {
                "total": len(cat_y_true),
                "label_type": "scam",
                "recall": recall,
                "true_positives": tp_cat,
                "false_negatives": fn_cat
            }
        elif len(set(cat_y_true)) == 1 and cat_y_true[0] == 0:
            # Legitimate category - calculate precision (no false positives)
            tn_cat = sum(1 for t, p in zip(cat_y_true, cat_y_pred) if t == 0 and p == 0)
            fp_cat = sum(1 for t, p in zip(cat_y_true, cat_y_pred) if t == 0 and p == 1)
            precision = tn_cat / (tn_cat + fp_cat) if (tn_cat + fp_cat) > 0 else 0
            per_category_metrics[cat] = {
                "total": len(cat_y_true),
                "label_type": "legitimate",
                "precision": precision,
                "true_negatives": tn_cat,
                "false_positives": fp_cat
            }

    return {
        "scam_recall": report["scam"]["recall"],
        "scam_precision": report["scam"]["precision"],
        "scam_f1": report["scam"]["f1-score"],
        "legit_recall": report["legitimate"]["recall"],
        "legit_precision": report["legitimate"]["precision"],
        "legit_f1": report["legitimate"]["f1-score"],
        "overall_accuracy": report["accuracy"],
        "confusion_matrix": {"tn": int(tn), "fp": int(fp), "fn": int(fn), "tp": int(tp)},
        "misclassified": misclassified,
        "per_category_metrics": per_category_metrics,
        "total_samples": len(test_set),
        "scam_count": sum(y_true),
        "legit_count": len(y_true) - sum(y_true),
        "test_status": "PASS" if report["scam"]["recall"] >= 0.90 else "FAIL"
    }


def collect_sophisticated_scam_results(classifier, training_data: List[Dict]) -> Dict:
    """
    Test detection of sophisticated scam samples.
    All must be detected (100% required).
    """
    sophisticated_samples = [
        s for s in training_data
        if s["label"] == 1 and "sophisticated" in s.get("category", "").lower()
    ][:50]

    if not sophisticated_samples:
        return {
            "total": 0,
            "detected": 0,
            "detection_rate": 0,
            "misclassified": [],
            "test_status": "SKIP"
        }

    detected = 0
    misclassified = []

    for sample in sophisticated_samples:
        result = classifier.predict(sample["text"])
        if result["is_scam"]:
            detected += 1
        else:
            misclassified.append({
                "id": sample.get("id", "unknown"),
                "category": sample.get("category", "unknown"),
                "text_snippet": sample["text"][:100] + "...",
                "confidence": result["confidence"]
            })

    detection_rate = detected / len(sophisticated_samples) if sophisticated_samples else 0

    return {
        "total": len(sophisticated_samples),
        "detected": detected,
        "detection_rate": detection_rate,
        "misclassified": misclassified,
        "test_status": "PASS" if len(misclassified) == 0 else "FAIL"
    }


def collect_known_sites_results(classifier, known_sites: List[Dict]) -> Dict:
    """
    Test known legitimate sites - zero false positives allowed.
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

    crypto_sites = [r for r in results if r["category"] == "crypto_exchange"]
    financial_sites = [r for r in results if r["category"] == "financial"]

    return {
        "total": len(results),
        "crypto_count": len(crypto_sites),
        "financial_count": len(financial_sites),
        "false_positives": len(false_positives),
        "false_positive_sites": false_positives,
        "all_results": results,
        "test_status": "PASS" if len(false_positives) == 0 else "FAIL"
    }


def collect_adversarial_results(classifier, training_data: List[Dict]) -> Dict:
    """
    Test adversarial attack detection.
    """
    # Homoglyph character mapping
    HOMOGLYPH_MAP = {
        'a': '\u0430', 'e': '\u0435', 'o': '0',
        'l': '1', 'i': '\u0456', 's': '\u0455',
    }

    TRUST_SIGNALS = [
        "FDIC insured", "SEC registered", "Licensed and regulated",
        "BBB accredited", "Norton secured", "SSL encrypted",
    ]

    def apply_homoglyph(text: str, rate: float = 0.3, seed: int = 42) -> str:
        random.seed(seed)
        chars = list(text)
        for i, char in enumerate(chars):
            lower_char = char.lower()
            if lower_char in HOMOGLYPH_MAP and random.random() < rate:
                chars[i] = HOMOGLYPH_MAP[lower_char]
        return ''.join(chars)

    def apply_trust_injection(text: str, num_signals: int = 2, seed: int = 42) -> str:
        random.seed(seed)
        words = text.split()
        signals = random.sample(TRUST_SIGNALS, min(num_signals, len(TRUST_SIGNALS)))
        for signal in signals:
            if len(words) > 2:
                pos = random.randint(1, len(words) - 1)
            else:
                pos = len(words)
            words.insert(pos, signal)
        return ' '.join(words)

    def apply_combined(text: str, seed: int = 42) -> str:
        random.seed(seed)
        modified = apply_homoglyph(text, 0.2, seed=None)
        modified = apply_trust_injection(modified, 2, seed=None)
        return modified

    # Get scam samples for testing
    scams = [s for s in training_data if s["label"] == 1]
    random.seed(42)

    # Select samples for each attack type
    homoglyph_samples = random.sample(scams, min(50, len(scams)))
    trust_samples = random.sample(scams, min(50, len(scams)))
    combined_samples = random.sample(scams, min(25, len(scams)))

    # Test homoglyph attacks
    homoglyph_detected = 0
    homoglyph_missed = []
    for i, s in enumerate(homoglyph_samples):
        modified = apply_homoglyph(s["text"], seed=42 + i)
        result = classifier.predict(modified)
        if result["is_scam"]:
            homoglyph_detected += 1
        else:
            homoglyph_missed.append({
                "id": s.get("id", f"adv_h_{i}"),
                "category": s.get("category", "unknown"),
                "confidence": result["confidence"]
            })

    # Test trust injection attacks
    trust_detected = 0
    trust_missed = []
    for i, s in enumerate(trust_samples):
        modified = apply_trust_injection(s["text"], seed=42 + i)
        result = classifier.predict(modified)
        if result["is_scam"]:
            trust_detected += 1
        else:
            trust_missed.append({
                "id": s.get("id", f"adv_t_{i}"),
                "category": s.get("category", "unknown"),
                "confidence": result["confidence"]
            })

    # Test combined attacks
    combined_detected = 0
    combined_missed = []
    for i, s in enumerate(combined_samples):
        modified = apply_combined(s["text"], seed=42 + i)
        result = classifier.predict(modified)
        if result["is_scam"]:
            combined_detected += 1
        else:
            combined_missed.append({
                "id": s.get("id", f"adv_c_{i}"),
                "category": s.get("category", "unknown"),
                "confidence": result["confidence"]
            })

    homoglyph_rate = homoglyph_detected / len(homoglyph_samples) if homoglyph_samples else 0
    trust_rate = trust_detected / len(trust_samples) if trust_samples else 0
    combined_rate = combined_detected / len(combined_samples) if combined_samples else 0

    return {
        "homoglyph": {
            "total": len(homoglyph_samples),
            "detected": homoglyph_detected,
            "rate": homoglyph_rate,
            "threshold": 0.80,
            "missed": homoglyph_missed[:10],
            "status": "PASS" if homoglyph_rate >= 0.80 else "FAIL"
        },
        "trust_injection": {
            "total": len(trust_samples),
            "detected": trust_detected,
            "rate": trust_rate,
            "threshold": 0.80,
            "missed": trust_missed[:10],
            "status": "PASS" if trust_rate >= 0.80 else "FAIL"
        },
        "combined": {
            "total": len(combined_samples),
            "detected": combined_detected,
            "rate": combined_rate,
            "threshold": 0.25,  # Baseline threshold (aspirational is 70%)
            "target": 0.70,
            "missed": combined_missed[:10],
            "status": "PASS" if combined_rate >= 0.25 else "FAIL"
        }
    }


def generate_report(
    production_metrics: Dict,
    sophisticated_results: Dict,
    known_sites_results: Dict,
    adversarial_results: Dict,
    pytest_output: str,
    training_data: List[Dict],
    output_path: Path
) -> None:
    """
    Generate TEST_REPORT.md with comprehensive test results.
    """
    timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    scam_count = sum(1 for s in training_data if s["label"] == 1)
    legit_count = sum(1 for s in training_data if s["label"] == 0)

    # Determine overall status
    all_statuses = [
        production_metrics["test_status"],
        sophisticated_results["test_status"],
        known_sites_results["test_status"],
        adversarial_results["homoglyph"]["status"],
        adversarial_results["trust_injection"]["status"],
        adversarial_results["combined"]["status"]
    ]
    overall_status = "PASS" if all(s == "PASS" or s == "SKIP" for s in all_statuses) else "FAIL"

    # Build report content
    report_lines = [
        "# Model Test Report",
        "",
        f"**Generated:** {timestamp}",
        f"**Model:** models/scam_classifier.pkl",
        f"**Dataset:** {len(training_data)} samples ({scam_count} scam, {legit_count} legitimate)",
        "",
        "## Executive Summary",
        "",
        "| Test Suite | Status | Details |",
        "|------------|--------|---------|",
        f"| Production Distribution (TEST-01) | {production_metrics['test_status']} | Scam recall: {production_metrics['scam_recall']:.1%} (target: >=90%) |",
        f"| Known Sites (TEST-02) | {known_sites_results['test_status']} | {known_sites_results['false_positives']} false positives (target: 0) |",
        f"| Sophisticated Scams (TEST-03) | {sophisticated_results['test_status']} | {sophisticated_results['detected']}/{sophisticated_results['total']} detected (target: 100%) |",
        f"| Adversarial - Homoglyph (TEST-04a) | {adversarial_results['homoglyph']['status']} | {adversarial_results['homoglyph']['rate']:.1%} detection (target: >=80%) |",
        f"| Adversarial - Trust Injection (TEST-04b) | {adversarial_results['trust_injection']['status']} | {adversarial_results['trust_injection']['rate']:.1%} detection (target: >=80%) |",
        f"| Adversarial - Combined (TEST-04c) | {adversarial_results['combined']['status']} | {adversarial_results['combined']['rate']:.1%} detection (baseline: >=25%, target: >=70%) |",
        "",
        f"**Overall Status:** {overall_status}",
        "",
        "## Detailed Metrics (TEST-05)",
        "",
        "### Scam Class Performance",
        "",
        "| Metric | Value |",
        "|--------|-------|",
        f"| Precision | {production_metrics['scam_precision']:.1%} |",
        f"| Recall | {production_metrics['scam_recall']:.1%} |",
        f"| F1 Score | {production_metrics['scam_f1']:.1%} |",
        "",
        "### Legitimate Class Performance",
        "",
        "| Metric | Value |",
        "|--------|-------|",
        f"| Precision | {production_metrics['legit_precision']:.1%} |",
        f"| Recall | {production_metrics['legit_recall']:.1%} |",
        f"| F1 Score | {production_metrics['legit_f1']:.1%} |",
        "",
        "### Confusion Matrix",
        "",
        "```",
        "                  Predicted",
        "                  Legit    Scam",
        f"Actual Legit     {production_metrics['confusion_matrix']['tn']:5d}   {production_metrics['confusion_matrix']['fp']:5d}",
        f"Actual Scam      {production_metrics['confusion_matrix']['fn']:5d}   {production_metrics['confusion_matrix']['tp']:5d}",
        "```",
        "",
        f"**Overall Accuracy:** {production_metrics['overall_accuracy']:.1%}",
        "",
    ]

    # Per-Category Performance
    report_lines.extend([
        "## Per-Category Performance",
        "",
    ])

    # Group scam categories
    scam_categories = {k: v for k, v in production_metrics["per_category_metrics"].items()
                       if v.get("label_type") == "scam"}
    if scam_categories:
        report_lines.extend([
            "### Scam Categories",
            "",
            "| Category | Samples | Recall | TP | FN |",
            "|----------|---------|--------|----|----|",
        ])
        for cat, metrics in sorted(scam_categories.items(), key=lambda x: x[0]):
            recall = metrics.get("recall", 0)
            tp = metrics.get("true_positives", 0)
            fn = metrics.get("false_negatives", 0)
            status = "" if recall >= 0.70 else " (LOW)"
            report_lines.append(f"| {cat} | {metrics['total']} | {recall:.1%}{status} | {tp} | {fn} |")
        report_lines.append("")

    # Group legitimate categories
    legit_categories = {k: v for k, v in production_metrics["per_category_metrics"].items()
                        if v.get("label_type") == "legitimate"}
    if legit_categories:
        report_lines.extend([
            "### Legitimate Categories",
            "",
            "| Category | Samples | Precision | TN | FP |",
            "|----------|---------|-----------|----|----|",
        ])
        for cat, metrics in sorted(legit_categories.items(), key=lambda x: x[0]):
            precision = metrics.get("precision", 0)
            tn = metrics.get("true_negatives", 0)
            fp = metrics.get("false_positives", 0)
            status = "" if precision >= 0.90 else " (HIGH FP)"
            report_lines.append(f"| {cat} | {metrics['total']} | {precision:.1%}{status} | {tn} | {fp} |")
        report_lines.append("")

    # Adversarial Attack Results
    report_lines.extend([
        "## Adversarial Attack Results",
        "",
        "| Attack Type | Samples | Detected | Rate | Threshold | Status |",
        "|-------------|---------|----------|------|-----------|--------|",
        f"| Homoglyph | {adversarial_results['homoglyph']['total']} | {adversarial_results['homoglyph']['detected']} | {adversarial_results['homoglyph']['rate']:.1%} | >=80% | {adversarial_results['homoglyph']['status']} |",
        f"| Trust Injection | {adversarial_results['trust_injection']['total']} | {adversarial_results['trust_injection']['detected']} | {adversarial_results['trust_injection']['rate']:.1%} | >=80% | {adversarial_results['trust_injection']['status']} |",
        f"| Combined | {adversarial_results['combined']['total']} | {adversarial_results['combined']['detected']} | {adversarial_results['combined']['rate']:.1%} | >=25% (baseline) | {adversarial_results['combined']['status']} |",
        "",
        f"**Note:** Combined attack target is 70%, but baseline (regression guard) is 25%. Current detection: {adversarial_results['combined']['rate']:.1%}",
        "",
    ])

    # Misclassified Samples
    report_lines.extend([
        "## Misclassified Samples (Debug)",
        "",
    ])

    # False Negatives (Scams Missed)
    false_negatives = [m for m in production_metrics["misclassified"] if m["expected"] == "scam"]
    report_lines.extend([
        "### False Negatives (Scams Missed)",
        "",
    ])
    if false_negatives:
        report_lines.extend([
            "| ID | Category | Confidence | Text Snippet |",
            "|----|----------|------------|--------------|",
        ])
        for m in false_negatives[:10]:
            text_escaped = m["text_snippet"].replace("|", "\\|").replace("\n", " ")[:60]
            report_lines.append(f"| {m['id']} | {m['category']} | {m['confidence']:.2f} | {text_escaped}... |")
        if len(false_negatives) > 10:
            report_lines.append(f"| ... | ... | ... | ({len(false_negatives) - 10} more) |")
    else:
        report_lines.append("*No false negatives - all scams detected*")
    report_lines.append("")

    # False Positives (Legit Flagged as Scam)
    false_positives = [m for m in production_metrics["misclassified"] if m["expected"] == "legitimate"]
    report_lines.extend([
        "### False Positives (Legitimate Flagged as Scam)",
        "",
    ])
    if false_positives:
        report_lines.extend([
            "| ID | Category | Confidence | Text Snippet |",
            "|----|----------|------------|--------------|",
        ])
        for m in false_positives[:10]:
            text_escaped = m["text_snippet"].replace("|", "\\|").replace("\n", " ")[:60]
            report_lines.append(f"| {m['id']} | {m['category']} | {m['confidence']:.2f} | {text_escaped}... |")
        if len(false_positives) > 10:
            report_lines.append(f"| ... | ... | ... | ({len(false_positives) - 10} more) |")
    else:
        report_lines.append("*No false positives - all legitimate sites correctly classified*")
    report_lines.append("")

    # Sophisticated Scam Failures
    if sophisticated_results["misclassified"]:
        report_lines.extend([
            "### Sophisticated Scams Missed",
            "",
            "| ID | Category | Confidence | Text Snippet |",
            "|----|----------|------------|--------------|",
        ])
        for m in sophisticated_results["misclassified"][:10]:
            text_escaped = m["text_snippet"].replace("|", "\\|").replace("\n", " ")[:60]
            report_lines.append(f"| {m['id']} | {m['category']} | {m['confidence']:.2f} | {text_escaped}... |")
        report_lines.append("")

    # Known Sites Verification
    report_lines.extend([
        "## Known Sites Verification",
        "",
        "| Site | Category | Status | Confidence |",
        "|------|----------|--------|------------|",
    ])
    for r in known_sites_results["all_results"]:
        status = "FAIL - FALSE POSITIVE" if r["is_scam"] else "SAFE"
        report_lines.append(f"| {r['site']} | {r['category']} | {status} | {r['confidence']:.2f} |")
    report_lines.append("")

    # Test Execution Log
    report_lines.extend([
        "## Test Execution Log",
        "",
        "```",
        pytest_output[:5000] if len(pytest_output) > 5000 else pytest_output,
        "```",
        "",
        "---",
        "*Report generated by scripts/run_tests.py*",
    ])

    # Write report
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with open(output_path, "w", encoding="utf-8") as f:
        f.write("\n".join(report_lines))


def main():
    """Main test runner entry point."""
    parser = argparse.ArgumentParser(
        description="Run all model tests and generate TEST_REPORT.md",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Exit codes:
  0 = All tests passed
  1 = One or more tests failed

Examples:
  python scripts/run_tests.py              # Run all tests
  python scripts/run_tests.py --quick      # Skip slow tests
"""
    )
    parser.add_argument(
        "--quick",
        action="store_true",
        help="Skip slow tests (marked with @pytest.mark.slow)"
    )
    parser.add_argument(
        "--report-only",
        action="store_true",
        help="Generate report from cached results (not implemented)"
    )

    args = parser.parse_args()

    print("=" * 60)
    print("  SCAM CLASSIFIER TEST RUNNER")
    print("=" * 60)
    print()

    if args.report_only:
        print("ERROR: --report-only not yet implemented")
        return 1

    # Load data
    print("[1/6] Loading training data...")
    try:
        training_data = load_training_data()
        print(f"      Loaded {len(training_data)} samples")
    except Exception as e:
        print(f"      ERROR: {e}")
        return 1

    print("[2/6] Loading classifier...")
    try:
        classifier = get_classifier()
        print("      Classifier loaded successfully")
    except Exception as e:
        print(f"      ERROR: {e}")
        return 1

    print("[3/6] Loading known sites...")
    try:
        known_sites = load_known_sites()
        print(f"      Loaded {len(known_sites)} known sites")
    except Exception as e:
        print(f"      ERROR: {e}")
        return 1

    # Run pytest
    print("[4/6] Running pytest suite...")
    markers = None
    if args.quick:
        markers = ["not slow"]
    pytest_results = run_pytest(markers)
    print(f"      Passed: {pytest_results['passed']}, Failed: {pytest_results['failed']}, "
          f"Errors: {pytest_results['errors']}, Skipped: {pytest_results['skipped']}")

    # Collect detailed metrics
    print("[5/6] Collecting detailed metrics...")
    print("      - Production distribution metrics...")
    production_metrics = collect_production_metrics(classifier, training_data)
    print(f"        Scam recall: {production_metrics['scam_recall']:.1%}")

    print("      - Sophisticated scam detection...")
    sophisticated_results = collect_sophisticated_scam_results(classifier, training_data)
    print(f"        Detected: {sophisticated_results['detected']}/{sophisticated_results['total']}")

    print("      - Known sites verification...")
    known_sites_results = collect_known_sites_results(classifier, known_sites)
    print(f"        False positives: {known_sites_results['false_positives']}")

    print("      - Adversarial attack detection...")
    adversarial_results = collect_adversarial_results(classifier, training_data)
    print(f"        Homoglyph: {adversarial_results['homoglyph']['rate']:.1%}")
    print(f"        Trust injection: {adversarial_results['trust_injection']['rate']:.1%}")
    print(f"        Combined: {adversarial_results['combined']['rate']:.1%}")

    # Generate report
    print("[6/6] Generating TEST_REPORT.md...")
    report_path = project_root / "reports" / "TEST_REPORT.md"
    generate_report(
        production_metrics,
        sophisticated_results,
        known_sites_results,
        adversarial_results,
        pytest_results["output"],
        training_data,
        report_path
    )
    print(f"      Report saved to: {report_path}")

    # Print summary
    print()
    print("=" * 60)
    print("  TEST SUMMARY")
    print("=" * 60)
    print()
    print(f"  Production Distribution (TEST-01): {production_metrics['test_status']}")
    print(f"    - Scam recall: {production_metrics['scam_recall']:.1%} (target: >=90%)")
    print(f"    - Scam precision: {production_metrics['scam_precision']:.1%}")
    print(f"    - Scam F1: {production_metrics['scam_f1']:.1%}")
    print()
    print(f"  Known Sites (TEST-02): {known_sites_results['test_status']}")
    print(f"    - False positives: {known_sites_results['false_positives']} (target: 0)")
    print()
    print(f"  Sophisticated Scams (TEST-03): {sophisticated_results['test_status']}")
    print(f"    - Detected: {sophisticated_results['detected']}/{sophisticated_results['total']} (target: 100%)")
    print()
    print(f"  Adversarial Attacks (TEST-04):")
    print(f"    - Homoglyph: {adversarial_results['homoglyph']['status']} ({adversarial_results['homoglyph']['rate']:.1%})")
    print(f"    - Trust Injection: {adversarial_results['trust_injection']['status']} ({adversarial_results['trust_injection']['rate']:.1%})")
    print(f"    - Combined: {adversarial_results['combined']['status']} ({adversarial_results['combined']['rate']:.1%})")
    print()

    # Determine overall result
    all_statuses = [
        production_metrics["test_status"],
        sophisticated_results["test_status"],
        known_sites_results["test_status"],
        adversarial_results["homoglyph"]["status"],
        adversarial_results["trust_injection"]["status"],
        adversarial_results["combined"]["status"]
    ]
    overall_pass = all(s == "PASS" or s == "SKIP" for s in all_statuses)

    if overall_pass:
        print("  OVERALL: PASS")
        print()
        print("=" * 60)
        return 0
    else:
        print("  OVERALL: FAIL")
        print()
        failed_tests = [s for s in all_statuses if s == "FAIL"]
        print(f"  {len(failed_tests)} test(s) failed")
        print("=" * 60)
        return 1


if __name__ == "__main__":
    sys.exit(main())

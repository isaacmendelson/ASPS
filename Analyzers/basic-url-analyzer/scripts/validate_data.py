#!/usr/bin/env python3
"""
Training Data Validation and Deduplication Script

Validates training_data/sample_data.json for:
- Required fields (id, text, label, category, metadata)
- ID format and uniqueness
- Text quality (non-empty, min length)
- Label validity (0 or 1, matches category)
- Metadata completeness
- Exact and fuzzy duplicate detection

Usage:
    python scripts/validate_data.py                    # Validate only
    python scripts/validate_data.py --fix              # Validate and auto-fix
    python scripts/validate_data.py --report report.md # Save report to file
"""

import argparse
import json
import re
import sys
from collections import defaultdict
from datetime import datetime
from difflib import SequenceMatcher
from pathlib import Path


# Constants
DATA_FILE = Path(__file__).parent.parent / "training_data" / "sample_data.json"
MIN_TEXT_LENGTH = 50

# Valid metadata values
VALID_SOURCES = {"manual", "synthetic", "scraped", "research"}
VALID_CONFIDENCE = {"high", "medium", "low"}

# ID pattern: (scam|legit)_[a-z_]+_[0-9]{4}
ID_PATTERN = re.compile(r"^(scam|legit)_[a-z_]+_\d{4}$")

# Date pattern: YYYY-MM-DD
DATE_PATTERN = re.compile(r"^\d{4}-\d{2}-\d{2}$")

# Known scam category patterns
SCAM_PATTERNS = [
    "scam", "phishing", "fraud", "fake", "ponzi", "mlm",
    "get_rich_quick", "lottery", "romance", "recovery",
    "government_impersonation", "tech_support", "employment_scam",
    "ecommerce_scam", "health_scam", "celebrity_scam", "sophisticated"
]

# Known legitimate category patterns
LEGIT_PATTERNS = [
    "legitimate", "legit", "bank", "credit_union", "insurance",
    "fintech", "investment_legitimate", "crypto_exchange", "crypto_wallet",
    "crypto_defi", "crypto_education", "marketplace", "retail",
    "small_business", "subscription", "news", "media", "government",
    "federal", "state_local", "university", "online_education",
    "hospital", "practice", "saas", "software", "local_business",
    "service_provider", "charity", "foundation", "hard_negative"
]


class ValidationResult:
    """Holds validation results"""
    def __init__(self):
        self.errors = []
        self.warnings = []
        self.fixes_applied = []
        self.stats = {}

    @property
    def passed(self):
        return len(self.errors) == 0


def load_data(file_path: Path) -> list:
    """Load training data from JSON file"""
    with open(file_path, "r", encoding="utf-8") as f:
        return json.load(f)


def save_data(file_path: Path, data: list):
    """Save training data to JSON file"""
    with open(file_path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)


def is_scam_category(category: str) -> bool:
    """Determine if a category should have label=1 (scam)"""
    category_lower = category.lower()
    # Check for scam patterns
    for pattern in SCAM_PATTERNS:
        if pattern in category_lower:
            return True
    return False


def is_legit_category(category: str) -> bool:
    """Determine if a category should have label=0 (legitimate)"""
    category_lower = category.lower()
    # Check for legitimate patterns
    for pattern in LEGIT_PATTERNS:
        if pattern in category_lower:
            return True
    return False


def validate_required_fields(sample: dict, idx: int, result: ValidationResult):
    """Validate that all required fields exist"""
    required_top = ["id", "text", "label", "category", "metadata"]
    required_metadata = ["source", "date_added", "confidence", "verified_by"]

    for field in required_top:
        if field not in sample:
            result.errors.append(f"Sample {idx}: Missing required field '{field}'")

    if "metadata" in sample and isinstance(sample["metadata"], dict):
        for field in required_metadata:
            if field not in sample["metadata"]:
                result.errors.append(f"Sample {idx} (id={sample.get('id', 'unknown')}): Missing metadata field '{field}'")


def validate_id(sample: dict, idx: int, seen_ids: set, result: ValidationResult):
    """Validate ID format and uniqueness"""
    sample_id = sample.get("id", "")
    label = sample.get("label")

    # Check format
    if not ID_PATTERN.match(sample_id):
        result.errors.append(f"Sample {idx}: Invalid ID format '{sample_id}' (expected: (scam|legit)_[a-z_]+_[0-9]{{4}})")

    # Check uniqueness
    if sample_id in seen_ids:
        result.errors.append(f"Sample {idx}: Duplicate ID '{sample_id}'")
    seen_ids.add(sample_id)

    # Check prefix matches label
    if sample_id.startswith("scam_") and label != 1:
        result.errors.append(f"Sample {idx}: ID prefix 'scam_' but label={label} (expected 1)")
    elif sample_id.startswith("legit_") and label != 0:
        result.errors.append(f"Sample {idx}: ID prefix 'legit_' but label={label} (expected 0)")


def validate_text(sample: dict, idx: int, result: ValidationResult, fix: bool) -> bool:
    """Validate text field, optionally fix whitespace"""
    text = sample.get("text", "")
    sample_id = sample.get("id", "unknown")
    fixed = False

    # Check empty
    if not text:
        result.errors.append(f"Sample {idx} (id={sample_id}): Empty text field")
        return False

    # Trim whitespace (auto-fix)
    trimmed = text.strip()
    if trimmed != text:
        if fix:
            sample["text"] = trimmed
            result.fixes_applied.append(f"Sample {idx} (id={sample_id}): Trimmed whitespace from text")
            fixed = True
        else:
            result.warnings.append(f"Sample {idx} (id={sample_id}): Text has leading/trailing whitespace (use --fix)")
        text = trimmed

    # Check minimum length
    if len(text) < MIN_TEXT_LENGTH:
        result.errors.append(f"Sample {idx} (id={sample_id}): Text too short ({len(text)} chars, minimum {MIN_TEXT_LENGTH})")

    return fixed


def validate_label(sample: dict, idx: int, result: ValidationResult):
    """Validate label is 0 or 1 and matches category"""
    label = sample.get("label")
    category = sample.get("category", "")
    sample_id = sample.get("id", "unknown")

    # Check valid label values
    if label not in [0, 1]:
        result.errors.append(f"Sample {idx} (id={sample_id}): Invalid label '{label}' (must be 0 or 1)")
        return

    # Check label matches category type
    if label == 1 and not is_scam_category(category):
        if is_legit_category(category):
            result.errors.append(f"Sample {idx} (id={sample_id}): Label=1 (scam) but category '{category}' appears legitimate")
        else:
            result.warnings.append(f"Sample {idx} (id={sample_id}): Label=1 but category '{category}' not recognized as scam")

    if label == 0 and not is_legit_category(category):
        if is_scam_category(category):
            result.errors.append(f"Sample {idx} (id={sample_id}): Label=0 (legit) but category '{category}' appears to be scam")
        else:
            result.warnings.append(f"Sample {idx} (id={sample_id}): Label=0 but category '{category}' not recognized as legitimate")


def validate_category(sample: dict, idx: int, result: ValidationResult):
    """Validate category field"""
    category = sample.get("category", "")
    sample_id = sample.get("id", "unknown")

    if not category:
        result.errors.append(f"Sample {idx} (id={sample_id}): Empty category field")
        return

    if not isinstance(category, str):
        result.errors.append(f"Sample {idx} (id={sample_id}): Category must be a string")
        return

    # Check if category is recognized
    if not is_scam_category(category) and not is_legit_category(category):
        result.warnings.append(f"Sample {idx} (id={sample_id}): Unknown category '{category}'")


def validate_metadata(sample: dict, idx: int, result: ValidationResult):
    """Validate metadata fields"""
    metadata = sample.get("metadata", {})
    sample_id = sample.get("id", "unknown")

    if not isinstance(metadata, dict):
        result.errors.append(f"Sample {idx} (id={sample_id}): Metadata must be an object")
        return

    # Validate source
    source = metadata.get("source", "")
    if source not in VALID_SOURCES:
        result.errors.append(f"Sample {idx} (id={sample_id}): Invalid source '{source}' (must be one of: {', '.join(sorted(VALID_SOURCES))})")

    # Validate confidence
    confidence = metadata.get("confidence", "")
    if confidence not in VALID_CONFIDENCE:
        result.errors.append(f"Sample {idx} (id={sample_id}): Invalid confidence '{confidence}' (must be one of: {', '.join(sorted(VALID_CONFIDENCE))})")

    # Validate date_added
    date_added = metadata.get("date_added", "")
    if not DATE_PATTERN.match(str(date_added)):
        result.errors.append(f"Sample {idx} (id={sample_id}): Invalid date_added '{date_added}' (must be YYYY-MM-DD)")
    else:
        # Verify it's a valid date
        try:
            datetime.strptime(date_added, "%Y-%m-%d")
        except ValueError:
            result.errors.append(f"Sample {idx} (id={sample_id}): Invalid date '{date_added}'")

    # Validate verified_by
    verified_by = metadata.get("verified_by", "")
    if not verified_by:
        result.errors.append(f"Sample {idx} (id={sample_id}): Empty verified_by field")


def find_duplicates(data: list, result: ValidationResult, fix: bool) -> list:
    """Find exact and fuzzy duplicates"""
    # Build text index
    text_to_samples = defaultdict(list)
    for idx, sample in enumerate(data):
        text = sample.get("text", "").strip().lower()
        text_to_samples[text].append((idx, sample))

    # Find exact duplicates
    exact_duplicates = []
    indices_to_remove = set()

    for text, samples in text_to_samples.items():
        if len(samples) > 1:
            # Keep the first occurrence, mark others for removal
            kept = samples[0]
            for dup_idx, dup_sample in samples[1:]:
                exact_duplicates.append((dup_idx, dup_sample["id"], kept[1]["id"]))
                if fix:
                    indices_to_remove.add(dup_idx)
                    result.fixes_applied.append(f"Removed exact duplicate: {dup_sample['id']} (duplicate of {kept[1]['id']})")

    if exact_duplicates and not fix:
        for dup_idx, dup_id, orig_id in exact_duplicates:
            result.errors.append(f"Exact duplicate: {dup_id} is identical to {orig_id} (use --fix to remove)")

    # Remove exact duplicates if fixing
    if fix and indices_to_remove:
        data = [s for i, s in enumerate(data) if i not in indices_to_remove]

    # Find fuzzy duplicates (90%+ similarity) - only on non-duplicate samples
    fuzzy_duplicates = []
    checked_pairs = set()

    # Get unique texts (one sample per text)
    unique_samples = []
    seen_texts = set()
    for sample in data:
        text = sample.get("text", "").strip().lower()
        if text not in seen_texts:
            unique_samples.append(sample)
            seen_texts.add(text)

    # Compare all pairs (O(n^2) but necessary for fuzzy matching)
    # For performance, only compare samples of same label
    scam_samples = [s for s in unique_samples if s.get("label") == 1]
    legit_samples = [s for s in unique_samples if s.get("label") == 0]

    for samples_group in [scam_samples, legit_samples]:
        n = len(samples_group)
        for i in range(n):
            text_i = samples_group[i].get("text", "").strip().lower()
            id_i = samples_group[i].get("id", "")

            for j in range(i + 1, n):
                text_j = samples_group[j].get("text", "").strip().lower()
                id_j = samples_group[j].get("id", "")

                pair_key = tuple(sorted([id_i, id_j]))
                if pair_key in checked_pairs:
                    continue
                checked_pairs.add(pair_key)

                # Quick length check - if lengths differ by >20%, skip
                len_ratio = min(len(text_i), len(text_j)) / max(len(text_i), len(text_j)) if max(len(text_i), len(text_j)) > 0 else 0
                if len_ratio < 0.8:
                    continue

                similarity = SequenceMatcher(None, text_i, text_j).ratio()
                if similarity >= 0.9:
                    fuzzy_duplicates.append((id_i, id_j, similarity))
                    result.warnings.append(f"Fuzzy duplicate ({similarity:.1%} similar): {id_i} <-> {id_j}")

                    # Mark in metadata if fixing
                    if fix:
                        for sample in data:
                            if sample.get("id") == id_j:
                                if "metadata" in sample and isinstance(sample["metadata"], dict):
                                    sample["metadata"]["fuzzy_duplicate_of"] = id_i
                                    result.fixes_applied.append(f"Marked {id_j} as fuzzy duplicate of {id_i}")
                                break

    result.stats["exact_duplicates"] = len(exact_duplicates)
    result.stats["fuzzy_duplicates"] = len(fuzzy_duplicates)

    return data


def check_balance(data: list, result: ValidationResult):
    """Check scam/legit balance"""
    scam_count = sum(1 for s in data if s.get("label") == 1)
    legit_count = sum(1 for s in data if s.get("label") == 0)
    total = len(data)

    if total == 0:
        result.errors.append("No samples in dataset")
        return

    scam_ratio = scam_count / total

    result.stats["total_samples"] = total
    result.stats["scam_count"] = scam_count
    result.stats["legit_count"] = legit_count
    result.stats["scam_ratio"] = scam_ratio

    if scam_ratio < 0.30:
        result.warnings.append(f"Scam ratio ({scam_ratio:.1%}) is below 30% - may cause model imbalance")
    elif scam_ratio > 0.50:
        result.warnings.append(f"Scam ratio ({scam_ratio:.1%}) is above 50% - may cause model imbalance")


def count_categories(data: list, result: ValidationResult):
    """Count samples per category"""
    category_counts = defaultdict(int)
    for sample in data:
        category = sample.get("category", "unknown")
        category_counts[category] += 1

    result.stats["category_counts"] = dict(sorted(category_counts.items()))


def validate(data: list, fix: bool = False) -> tuple[list, ValidationResult]:
    """Run all validations on the data"""
    result = ValidationResult()
    seen_ids = set()

    # Validate each sample
    for idx, sample in enumerate(data):
        validate_required_fields(sample, idx, result)

        if "id" in sample:
            validate_id(sample, idx, seen_ids, result)

        if "text" in sample:
            validate_text(sample, idx, result, fix)

        if "label" in sample:
            validate_label(sample, idx, result)

        if "category" in sample:
            validate_category(sample, idx, result)

        if "metadata" in sample:
            validate_metadata(sample, idx, result)

    # Check for duplicates
    data = find_duplicates(data, result, fix)

    # Check balance
    check_balance(data, result)

    # Count categories
    count_categories(data, result)

    return data, result


def generate_report(result: ValidationResult, original_count: int, final_count: int) -> str:
    """Generate a human-readable validation report"""
    lines = []
    timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")

    lines.append("# Training Data Validation Report")
    lines.append("")
    lines.append(f"**Generated:** {timestamp}")
    lines.append(f"**Data file:** training_data/sample_data.json")
    lines.append("")

    # Summary
    status = "PASS" if result.passed else "FAIL"
    lines.append(f"## Summary: {status}")
    lines.append("")
    lines.append(f"- Total samples: {result.stats.get('total_samples', 0)}")
    lines.append(f"- Scam samples: {result.stats.get('scam_count', 0)} ({result.stats.get('scam_ratio', 0):.1%})")
    lines.append(f"- Legitimate samples: {result.stats.get('legit_count', 0)} ({1 - result.stats.get('scam_ratio', 0):.1%})")
    lines.append(f"- Errors: {len(result.errors)}")
    lines.append(f"- Warnings: {len(result.warnings)}")
    lines.append(f"- Fixes applied: {len(result.fixes_applied)}")
    lines.append("")

    # Deduplication
    lines.append("## Deduplication Results")
    lines.append("")
    lines.append(f"- Exact duplicates found: {result.stats.get('exact_duplicates', 0)}")
    lines.append(f"- Fuzzy duplicates found: {result.stats.get('fuzzy_duplicates', 0)}")
    if original_count != final_count:
        lines.append(f"- Samples removed: {original_count - final_count}")
    lines.append("")

    # Category breakdown
    lines.append("## Category Distribution")
    lines.append("")
    lines.append("| Category | Count |")
    lines.append("|----------|-------|")

    category_counts = result.stats.get("category_counts", {})
    for category, count in sorted(category_counts.items()):
        lines.append(f"| {category} | {count} |")
    lines.append("")

    # Errors
    if result.errors:
        lines.append("## Errors (must fix)")
        lines.append("")
        for error in result.errors[:50]:  # Limit to first 50
            lines.append(f"- {error}")
        if len(result.errors) > 50:
            lines.append(f"- ... and {len(result.errors) - 50} more errors")
        lines.append("")

    # Warnings
    if result.warnings:
        lines.append("## Warnings (review recommended)")
        lines.append("")
        for warning in result.warnings[:30]:  # Limit to first 30
            lines.append(f"- {warning}")
        if len(result.warnings) > 30:
            lines.append(f"- ... and {len(result.warnings) - 30} more warnings")
        lines.append("")

    # Fixes applied
    if result.fixes_applied:
        lines.append("## Fixes Applied")
        lines.append("")
        for fix in result.fixes_applied[:30]:  # Limit to first 30
            lines.append(f"- {fix}")
        if len(result.fixes_applied) > 30:
            lines.append(f"- ... and {len(result.fixes_applied) - 30} more fixes")
        lines.append("")

    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(
        description="Validate and deduplicate training data",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python scripts/validate_data.py                    # Validate only
  python scripts/validate_data.py --fix              # Validate and auto-fix
  python scripts/validate_data.py --report report.md # Save report to file
        """
    )
    parser.add_argument("--fix", action="store_true", help="Auto-fix issues where possible")
    parser.add_argument("--report", type=str, help="Save validation report to specified file")
    parser.add_argument("--data", type=str, help="Path to data file (default: training_data/sample_data.json)")

    args = parser.parse_args()

    # Determine data file
    data_file = Path(args.data) if args.data else DATA_FILE

    if not data_file.exists():
        print(f"Error: Data file not found: {data_file}")
        sys.exit(1)

    print(f"Loading data from {data_file}...")
    data = load_data(data_file)
    original_count = len(data)
    print(f"Loaded {original_count} samples")

    print("Running validation...")
    data, result = validate(data, fix=args.fix)
    final_count = len(data)

    # Save fixed data if needed
    if args.fix and result.fixes_applied:
        print(f"Saving fixed data to {data_file}...")
        save_data(data_file, data)
        print(f"Saved {final_count} samples")

    # Generate and display report
    report = generate_report(result, original_count, final_count)
    print()
    print(report)

    # Save report if requested
    if args.report:
        report_path = Path(args.report)
        with open(report_path, "w", encoding="utf-8") as f:
            f.write(report)
        print(f"\nReport saved to {report_path}")

    # Exit code
    if result.passed:
        print("\nValidation PASSED")
        sys.exit(0)
    else:
        print(f"\nValidation FAILED with {len(result.errors)} error(s)")
        sys.exit(1)


if __name__ == "__main__":
    main()

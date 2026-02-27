#!/usr/bin/env python3
"""
Migration script to add unique IDs and metadata to training samples.

This script transforms the training data from:
  {"text": "...", "label": 0/1, "category": "..."}

To:
  {"id": "...", "text": "...", "label": 0/1, "category": "...", "metadata": {...}}

ID Format: (scam|legit)_[subcategory]_[0001-9999]
- scam_ prefix for label=1
- legit_ prefix for label=0
- Subcategory derived from category field
- 4-digit sequential number per unique (prefix + subcategory)

Usage:
  python migrate_data.py --dry-run  # Preview changes
  python migrate_data.py            # Run migration
"""

import argparse
import json
import os
import re
import shutil
from collections import defaultdict
from datetime import date
from pathlib import Path


def derive_subcategory(category: str, label: int) -> str:
    """
    Derive subcategory from category field.

    Rules:
    - If category contains '_scam' (and is a scam): extract parts around it
      - crypto_scam_aibot -> crypto_aibot
      - investment_scam_forex -> investment_forex
      - phishing_bank -> phishing_bank (no _scam suffix)
    - If legitimate: strip 'legitimate_' prefix
      - legitimate_ecommerce_marketplace -> ecommerce_marketplace
      - legitimate_edge_crypto_defi -> edge_crypto_defi
    - Handle edge cases like bare category names
    """
    # Normalize
    cat = category.lower().strip()

    # For legitimate samples (label=0)
    if label == 0:
        # Strip legitimate_ prefix if present
        if cat.startswith('legitimate_'):
            return cat[11:]  # len('legitimate_') = 11
        # Edge case: bare 'legitimate'
        if cat == 'legitimate':
            return 'generic'
        return cat

    # For scam samples (label=1)
    # Pattern 1: category_scam_subcategory (e.g., crypto_scam_aibot)
    match = re.match(r'^(.+?)_scam_(.+)$', cat)
    if match:
        return f"{match.group(1)}_{match.group(2)}"

    # Pattern 2: category_scam (e.g., crypto_scam with no subcategory)
    match = re.match(r'^(.+?)_scam$', cat)
    if match:
        return match.group(1)

    # Pattern 3: no _scam in name (e.g., phishing_bank, sophisticated_scam_bec)
    # sophisticated_scam_bec -> sophisticated_bec
    if '_scam_' in cat:
        parts = cat.split('_scam_')
        return f"{parts[0]}_{parts[1]}"

    # For categories like phishing_bank (no _scam but has subcategory)
    # Keep as-is since there's no _scam to remove
    return cat


def generate_id(prefix: str, subcategory: str, counter: int) -> str:
    """Generate ID in format: prefix_subcategory_0001"""
    return f"{prefix}_{subcategory}_{counter:04d}"


def add_metadata(sample: dict, migration_date: str) -> dict:
    """Add metadata fields to a sample."""
    return {
        "source": "synthetic",
        "date_added": migration_date,
        "confidence": "high",
        "verified_by": "auto"
    }


def migrate_samples(samples: list, migration_date: str) -> tuple[list, dict]:
    """
    Migrate all samples to new format with IDs and metadata.

    Returns:
        tuple: (migrated_samples, stats_dict)
    """
    # Track counters per (prefix, subcategory)
    counters = defaultdict(int)

    # Stats
    stats = {
        'total': len(samples),
        'by_prefix': defaultdict(int),
        'by_subcategory': defaultdict(int),
        'issues': []
    }

    migrated = []
    seen_ids = set()

    for i, sample in enumerate(samples):
        # Determine prefix
        label = sample.get('label')
        if label not in (0, 1):
            stats['issues'].append(f"Sample {i}: Invalid label {label}")
            label = 1 if 'scam' in sample.get('category', '').lower() else 0

        prefix = 'scam' if label == 1 else 'legit'

        # Get category
        category = sample.get('category', 'unknown')
        if not category:
            stats['issues'].append(f"Sample {i}: Missing category")
            category = 'unknown'

        # Derive subcategory
        subcategory = derive_subcategory(category, label)

        # Increment counter and generate ID
        key = (prefix, subcategory)
        counters[key] += 1
        sample_id = generate_id(prefix, subcategory, counters[key])

        # Check for duplicate IDs (shouldn't happen with proper counters)
        if sample_id in seen_ids:
            stats['issues'].append(f"Duplicate ID generated: {sample_id}")
        seen_ids.add(sample_id)

        # Track stats
        stats['by_prefix'][prefix] += 1
        stats['by_subcategory'][subcategory] += 1

        # Create migrated sample
        migrated_sample = {
            'id': sample_id,
            'text': sample.get('text', ''),
            'label': label,
            'category': category,
            'metadata': add_metadata(sample, migration_date)
        }

        migrated.append(migrated_sample)

    return migrated, stats


def print_summary(stats: dict, dry_run: bool = False):
    """Print migration summary."""
    mode = "DRY RUN" if dry_run else "MIGRATION"
    print(f"\n{'='*60}")
    print(f"  {mode} SUMMARY")
    print(f"{'='*60}")

    print(f"\nTotal samples: {stats['total']}")

    print(f"\nBy prefix:")
    for prefix, count in sorted(stats['by_prefix'].items()):
        print(f"  {prefix}: {count}")

    print(f"\nBy subcategory (top 20):")
    sorted_subcats = sorted(stats['by_subcategory'].items(), key=lambda x: -x[1])
    for subcat, count in sorted_subcats[:20]:
        print(f"  {subcat}: {count}")
    if len(sorted_subcats) > 20:
        print(f"  ... and {len(sorted_subcats) - 20} more subcategories")

    print(f"\nTotal unique subcategories: {len(stats['by_subcategory'])}")

    if stats['issues']:
        print(f"\nIssues found ({len(stats['issues'])}):")
        for issue in stats['issues'][:10]:
            print(f"  - {issue}")
        if len(stats['issues']) > 10:
            print(f"  ... and {len(stats['issues']) - 10} more issues")
    else:
        print("\nNo issues found.")

    print(f"\n{'='*60}\n")


def main():
    parser = argparse.ArgumentParser(
        description='Migrate training data to add IDs and metadata'
    )
    parser.add_argument(
        '--dry-run',
        action='store_true',
        help='Preview changes without modifying files'
    )
    parser.add_argument(
        '--input',
        default='training_data/sample_data.json',
        help='Input file path (default: training_data/sample_data.json)'
    )
    parser.add_argument(
        '--output',
        default=None,
        help='Output file path (default: same as input)'
    )
    parser.add_argument(
        '--backup',
        default=None,
        help='Backup file path (default: input with .backup.json extension)'
    )
    parser.add_argument(
        '--date',
        default=str(date.today()),
        help='Migration date for metadata (default: today)'
    )

    args = parser.parse_args()

    # Resolve paths
    script_dir = Path(__file__).parent
    project_root = script_dir.parent

    input_path = project_root / args.input
    output_path = project_root / (args.output or args.input)

    if args.backup:
        backup_path = project_root / args.backup
    else:
        backup_path = input_path.with_suffix('.backup.json')

    # Load data
    print(f"Loading data from: {input_path}")
    if not input_path.exists():
        print(f"ERROR: Input file not found: {input_path}")
        return 1

    with open(input_path, 'r', encoding='utf-8') as f:
        samples = json.load(f)

    print(f"Loaded {len(samples)} samples")

    # Check if already migrated
    if samples and 'id' in samples[0]:
        print("WARNING: Data appears to already have IDs. Skipping migration.")
        return 0

    # Perform migration
    print(f"Migrating with date: {args.date}")
    migrated, stats = migrate_samples(samples, args.date)

    # Print summary
    print_summary(stats, dry_run=args.dry_run)

    if args.dry_run:
        # Show sample transformations
        print("Sample transformations (first 5):")
        for i in range(min(5, len(migrated))):
            orig = samples[i]
            new = migrated[i]
            print(f"\n  Original: {json.dumps(orig, indent=4)}")
            print(f"\n  Migrated: {json.dumps(new, indent=4)}")
            print("-" * 40)
        return 0

    # Create backup
    print(f"Creating backup at: {backup_path}")
    shutil.copy2(input_path, backup_path)

    # Write migrated data
    print(f"Writing migrated data to: {output_path}")
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(migrated, f, indent=2, ensure_ascii=False)

    print("Migration complete!")

    # Verification
    print("\nVerification:")
    with open(output_path, 'r', encoding='utf-8') as f:
        verify_data = json.load(f)

    all_ids = [s['id'] for s in verify_data]
    unique_ids = set(all_ids)

    print(f"  Total samples: {len(verify_data)}")
    print(f"  Unique IDs: {len(unique_ids)}")
    print(f"  All IDs unique: {len(all_ids) == len(unique_ids)}")

    # Check structure
    required_fields = {'id', 'text', 'label', 'category', 'metadata'}
    metadata_fields = {'source', 'date_added', 'confidence', 'verified_by'}

    structure_ok = True
    for i, sample in enumerate(verify_data[:10]):
        if not required_fields.issubset(sample.keys()):
            print(f"  Sample {i} missing fields: {required_fields - set(sample.keys())}")
            structure_ok = False
        if 'metadata' in sample and not metadata_fields.issubset(sample['metadata'].keys()):
            print(f"  Sample {i} metadata missing: {metadata_fields - set(sample['metadata'].keys())}")
            structure_ok = False

    if structure_ok:
        print("  Structure verification: PASSED")

    return 0


if __name__ == '__main__':
    exit(main())

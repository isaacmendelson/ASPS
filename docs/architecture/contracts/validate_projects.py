#!/usr/bin/env python
"""ASPS-770 — schema-validation check for the MASDP project-config contract.

This is a config/contract artifact (ADR-006, JSON Schema for projects.json),
not application code — per CLAUDE.md's TDD rule item 9, declarative
configuration is verified with validation/contract checks instead of
Red/Green unit tests. This script IS that verification:

  1. POSITIVE — projects.example.json (a documented, valid registry: MASDP as
     tenant #0 + SPS as project #1) MUST validate cleanly against
     projects.schema.json.
  2. NEGATIVE — projects.invalid.example.json (a deliberately malformed
     registry — missing required fields, wrong types, bad patterns) MUST
     FAIL validation.

Exit code 0 only if both checks behave as expected (valid passes, malformed
fails). Any other outcome (valid fails, or malformed unexpectedly passes) is
exit code 1 — that would mean the schema itself is wrong or too weak.

Usage:
    python validate_projects.py
    KnowledgeEngine/.venv/Scripts/python.exe docs/architecture/contracts/validate_projects.py
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

from jsonschema import Draft202012Validator

HERE = Path(__file__).resolve().parent
SCHEMA_PATH = HERE / "projects.schema.json"
VALID_PATH = HERE / "projects.example.json"
INVALID_PATH = HERE / "projects.invalid.example.json"


def load(path: Path) -> dict:
    with path.open("r", encoding="utf-8") as f:
        return json.load(f)


def main() -> int:
    schema = load(SCHEMA_PATH)
    Draft202012Validator.check_schema(schema)  # the schema itself must be well-formed
    validator = Draft202012Validator(schema)

    overall_ok = True

    # --- 1. POSITIVE: the documented example must validate cleanly ---
    valid_doc = load(VALID_PATH)
    valid_errors = sorted(validator.iter_errors(valid_doc), key=lambda e: e.path)
    print(f"[POSITIVE] {VALID_PATH.name} against {SCHEMA_PATH.name}")
    if not valid_errors:
        print("  PASS — 0 validation errors (expected).")
    else:
        overall_ok = False
        print(f"  FAIL — expected 0 errors, got {len(valid_errors)}:")
        for err in valid_errors:
            print(f"    - {list(err.path)}: {err.message}")

    # --- 2. NEGATIVE: the malformed fixture must fail validation ---
    invalid_doc = load(INVALID_PATH)
    invalid_errors = sorted(validator.iter_errors(invalid_doc), key=lambda e: e.path)
    print(f"\n[NEGATIVE] {INVALID_PATH.name} against {SCHEMA_PATH.name}")
    if invalid_errors:
        print(f"  PASS — validation correctly FAILED with {len(invalid_errors)} error(s):")
        for err in invalid_errors:
            print(f"    - {list(err.path)}: {err.message}")
    else:
        overall_ok = False
        print("  FAIL — expected validation errors, but the malformed fixture validated cleanly "
              "(the schema is not catching what it should).")

    print("\n" + ("RESULT: PASS (positive passes, negative fails as expected)" if overall_ok
                   else "RESULT: FAIL"))
    return 0 if overall_ok else 1


if __name__ == "__main__":
    sys.exit(main())

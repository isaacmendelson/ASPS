"""Validate ASPS test results against the explicit ASPS-626 exception manifest."""

from __future__ import annotations

import argparse
import hashlib
import json
from datetime import date
from pathlib import Path
import sys
import xml.etree.ElementTree as ET


TRX_NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def digest(values: list[str]) -> str:
    payload = "\n".join(sorted(values)).encode("utf-8")
    return hashlib.sha256(payload).hexdigest()


def read_trx(path: Path) -> tuple[dict[str, list[str]], str]:
    root = ET.parse(path).getroot()
    failures: dict[str, list[str]] = {}
    for result in root.findall(".//t:UnitTestResult", TRX_NS):
        if result.attrib.get("outcome") != "Failed":
            continue
        test_id = result.attrib["testName"]
        group = test_id.rsplit(".", 1)[0]
        failures.setdefault(group, []).append(test_id)
    counters = root.find(".//t:Counters", TRX_NS)
    if counters is None:
        raise ValueError(f"TRX has no counters: {path}")
    summary = (
        f"{counters.attrib.get('passed', '0')} passed, "
        f"{counters.attrib.get('failed', '0')} failed, "
        f"{counters.attrib.get('notExecuted', '0')} not executed"
    )
    return failures, summary


def read_junit(path: Path) -> tuple[dict[str, list[str]], str]:
    root = ET.parse(path).getroot()
    failures: dict[str, list[str]] = {}
    cases = root.findall(".//testcase")
    for case in cases:
        if case.find("failure") is None and case.find("error") is None:
            continue
        group = case.attrib.get("classname", "<unknown>")
        test_id = f"{group}::{case.attrib.get('name', '<unknown>')}"
        failures.setdefault(group, []).append(test_id)
    failed_count = sum(
        case.find("failure") is not None or case.find("error") is not None
        for case in cases
    )
    error_count = sum(case.find("error") is not None for case in cases)
    skipped_count = sum(case.find("skipped") is not None for case in cases)
    summary = (
        f"{len(cases)} tests, "
        f"{failed_count} failures, "
        f"{error_count} errors, "
        f"{skipped_count} skipped"
    )
    return failures, summary


def read_jest(path: Path) -> tuple[dict[str, list[str]], str]:
    data = json.loads(path.read_text(encoding="utf-8"))
    failures: dict[str, list[str]] = {}
    marker = "/apps/extension/chrome/tests/"
    for suite in data["testResults"]:
        normalized = suite["name"].replace("\\", "/")
        if marker not in normalized:
            raise ValueError(f"Unexpected Jest suite path: {suite['name']}")
        group = normalized.split(marker, 1)[1]
        for result in suite["assertionResults"]:
            if result["status"] != "failed":
                continue
            test_id = " > ".join([*result["ancestorTitles"], result["title"]])
            failures.setdefault(group, []).append(test_id)
    summary = (
        f"{data['numPassedTests']} passed, "
        f"{data['numFailedTests']} failed, "
        f"{data['numPendingTests']} pending"
    )
    return failures, summary


def validate_component(
    component: str,
    actual: dict[str, list[str]],
    expected_groups: list[dict[str, object]],
) -> list[str]:
    errors: list[str] = []
    expected = {str(item["selector"]): item for item in expected_groups}
    unexpected_groups = sorted(set(actual) - set(expected))
    missing_groups = sorted(set(expected) - set(actual))
    if unexpected_groups:
        errors.append(f"{component}: unexpected failing groups: {unexpected_groups}")
    if missing_groups:
        errors.append(
            f"{component}: expected exception groups no longer fail; remove/update manifest: "
            f"{missing_groups}"
        )

    today = date.today()
    for selector in sorted(set(actual) & set(expected)):
        item = expected[selector]
        expires = date.fromisoformat(str(item["expires"]))
        if expires < today:
            errors.append(
                f"{component}:{selector}: exception expired on {expires.isoformat()}"
            )
        count = len(actual[selector])
        expected_count = int(item["expectedFailureCount"])
        if count != expected_count:
            errors.append(
                f"{component}:{selector}: expected {expected_count} failures, got {count}"
            )
        actual_hash = digest(actual[selector])
        if actual_hash != item["failureIdSha256"]:
            errors.append(
                f"{component}:{selector}: failure IDs changed "
                f"(expected {item['failureIdSha256']}, got {actual_hash})"
            )
    return errors


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--dotnet", required=True, type=Path)
    parser.add_argument("--analyzer", required=True, type=Path)
    parser.add_argument("--desktop", required=True, type=Path)
    parser.add_argument("--extension", required=True, type=Path)
    args = parser.parse_args()

    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    readers = {
        "dotnet": (read_trx, args.dotnet),
        "analyzer": (read_junit, args.analyzer),
        "desktop": (read_junit, args.desktop),
        "extension": (read_jest, args.extension),
    }

    errors: list[str] = []
    for component, (reader, path) in readers.items():
        if not path.is_file():
            errors.append(f"{component}: missing result file: {path}")
            continue
        try:
            actual, summary = reader(path)
            print(f"{component}: {summary}")
            errors.extend(
                validate_component(
                    component,
                    actual,
                    manifest["components"].get(component, []),
                )
            )
        except (KeyError, TypeError, ValueError, ET.ParseError, json.JSONDecodeError) as exc:
            errors.append(f"{component}: invalid result: {exc}")

    today = date.today()
    for exclusion in manifest.get("excluded", []):
        expires = date.fromisoformat(exclusion["expires"])
        if expires < today:
            errors.append(
                f"excluded:{exclusion['selector']}: expired on {expires.isoformat()}"
            )

    if errors:
        print("Baseline gate: FAIL", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1

    exception_count = sum(
        int(item["expectedFailureCount"])
        for groups in manifest["components"].values()
        for item in groups
    )
    print(
        f"Baseline gate: PASS; {exception_count} exact known failures remain "
        "visible and expiry-controlled."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

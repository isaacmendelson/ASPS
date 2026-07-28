"""Machine-only ASPS messaging v1 stdin/stdout adapter."""
from __future__ import annotations

import json
import sys
from typing import Callable, TextIO

from generated.messaging.v1.message_envelope import (
    ContractError,
    canonicalize_url,
    create_envelope,
    validate_envelope,
)
from scrapers.deadline import DeadlineWatchdog


def _error_response(request, code: str, message: str, retryable: bool = False):
    context = request.get("context") if isinstance(request, dict) else None
    if not isinstance(context, dict) or set(context) != {"deviceId", "tabId", "url"}:
        context = {"deviceId": None, "tabId": None, "url": "https://invalid.invalid/"}
    else:
        try:
            context = {**context, "url": canonicalize_url(context["url"])}
        except Exception:
            context = {"deviceId": context.get("deviceId"), "tabId": None, "url": "https://invalid.invalid/"}

    request_id = request.get("requestId") if isinstance(request, dict) else None
    correlation_id = request.get("correlationId") if isinstance(request, dict) else None
    try:
        return create_envelope(
            "url_scan.error",
            "analyzer",
            context,
            {},
            request_id=request_id,
            correlation_id=correlation_id,
            outcome={
                "status": "error",
                "error": {
                    "code": code,
                    "message": message,
                    "retryable": retryable,
                    "details": {},
                },
            },
        )
    except ContractError:
        return create_envelope(
            "url_scan.error",
            "analyzer",
            context,
            {},
            outcome={
                "status": "error",
                "error": {
                    "code": code,
                    "message": message,
                    "retryable": retryable,
                    "details": {},
                },
            },
        )


def _success_response(request: dict, analysis: dict):
    risk = analysis.get("risk_assessment", {})
    result = {
        **analysis,
        "score": risk.get("risk_score"),
        "riskLevel": risk.get("risk_level", "UNKNOWN"),
        "riskType": analysis.get("red_flags", []),
        "finalUrl": analysis.get("url", request["context"]["url"]),
    }
    return create_envelope(
        "url_scan.result",
        "analyzer",
        request["context"],
        {},
        request_id=request["requestId"],
        correlation_id=request["correlationId"],
        outcome={"status": "success", "result": result},
    )


def _load_deadline_seconds() -> float:
    """Read deadline_seconds from settings.json; fall back to 25s if missing."""
    import json as _json
    from pathlib import Path as _Path

    config_path = _Path(__file__).parent / "config" / "settings.json"
    try:
        with open(config_path, encoding="utf-8") as _f:
            settings = _json.load(_f)
        return float(settings["scraping"]["deadline_seconds"])
    except Exception:
        return 25.0


def process_stream(
    stdin: TextIO,
    stdout: TextIO,
    stderr: TextIO,
    analyze: Callable[[str], dict],
) -> int:
    request = {}
    # Arm the deadline watchdog before any analysis.  If the process exceeds
    # deadline_seconds the watchdog calls os._exit(1), which terminates all
    # threads including the Playwright/Chromium subprocess hierarchy.
    _deadline = _load_deadline_seconds()
    watchdog = DeadlineWatchdog(deadline_seconds=_deadline)
    watchdog.start()
    try:
        request = json.loads(stdin.read())
        validate_envelope(request, require_device_id=True)
        if request["messageType"] != "url_scan.request":
            raise ContractError(
                "protocol.unsupported_discriminator",
                "Analyzer accepts only url_scan.request",
            )
        analysis = analyze(request["context"]["url"])
        # Detect failure via the new structured contract (ASPS-612): success=False
        # or via a non-null/non-empty "error" field (supports both the new dict
        # schema and the legacy bare-string schema for backward compatibility).
        err = analysis.get("error")
        is_failure = analysis.get("success") is False or bool(err)
        if is_failure:
            if isinstance(err, dict):
                # New structured error: forward code and message from the analyzer.
                err_code = err.get("code") or "analyzer.analysis_failed"
                err_msg = err.get("message") or "URL analysis failed"
            elif isinstance(err, str) and err:
                # Legacy bare-string error.
                err_code = "analyzer.analysis_failed"
                err_msg = err
            else:
                err_code = "analyzer.analysis_failed"
                err_msg = "URL analysis failed"
            response = _error_response(request, err_code, err_msg)
        else:
            response = _success_response(request, analysis)
    except ContractError as exc:
        response = _error_response(request, exc.code, str(exc))
    except (json.JSONDecodeError, TypeError, ValueError):
        response = _error_response(
            request,
            "protocol.malformed_envelope",
            "Input is not a valid messaging envelope",
        )
    except TimeoutError:
        # Raised by the scraper when Playwright times out internally before
        # the deadline watchdog fires.  Map to a retryable analyzer.timeout.
        response = _error_response(
            request,
            "analyzer.timeout",
            "Analyzer timed out during scraping",
            retryable=True,
        )
    except Exception as exc:
        print(f"Analyzer failure: {exc}", file=stderr)
        response = _error_response(
            request,
            "analyzer.internal_error",
            "Analyzer failed",
            retryable=True,
        )
    finally:
        watchdog.cancel()

    stdout.write(json.dumps(response, separators=(",", ":")) + "\n")
    stdout.flush()
    return 0


def main() -> int:
    analyzer = None

    def analyze(url: str) -> dict:
        nonlocal analyzer
        if analyzer is None:
            from core.analyzer import ScamAnalyzer
            analyzer = ScamAnalyzer(use_cache=True, use_ml=True, no_explain=True)
        return analyzer.analyze_url(url)

    return process_stream(sys.stdin, sys.stdout, sys.stderr, analyze)


if __name__ == "__main__":
    raise SystemExit(main())

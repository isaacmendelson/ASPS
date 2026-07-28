import io
import json
import sys
import subprocess
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))


def request_envelope():
    return {
        "schemaVersion": "1.0",
        "messageId": "11111111-1111-4111-8111-111111111111",
        "correlationId": "22222222-2222-4222-8222-222222222222",
        "requestId": "33333333-3333-4333-8333-333333333333",
        "messageType": "url_scan.request",
        "sentAt": "2026-07-28T12:00:00.000Z",
        "source": "backend",
        "context": {
            "deviceId": "device-1",
            "tabId": "12",
            "url": "https://example.com/",
        },
        "outcome": None,
        "payload": {},
    }


def test_stdio_v1_success_is_one_envelope_and_echoes_identity():
    from v1_stdio import process_stream

    stdout = io.StringIO()
    stderr = io.StringIO()
    process_stream(
        io.StringIO(json.dumps(request_envelope())),
        stdout,
        stderr,
        analyze=lambda _: {
            "risk_assessment": {"risk_score": 17, "risk_level": "LOW"},
            "red_flags": [],
        },
    )

    response = json.loads(stdout.getvalue())
    assert stdout.getvalue().count("\n") == 1
    assert response["messageType"] == "url_scan.result"
    assert response["requestId"] == request_envelope()["requestId"]
    assert response["correlationId"] == request_envelope()["correlationId"]
    assert response["context"] == request_envelope()["context"]
    assert response["messageId"] != request_envelope()["messageId"]
    assert response["outcome"]["status"] == "success"
    assert response["outcome"]["result"]["score"] == 17
    assert stderr.getvalue() == ""


def test_stdio_v1_analysis_failure_has_no_score_and_keeps_stdout_pure():
    from v1_stdio import process_stream

    stdout = io.StringIO()
    stderr = io.StringIO()

    def fail(_):
        raise RuntimeError("internal diagnostic")

    process_stream(io.StringIO(json.dumps(request_envelope())), stdout, stderr, fail)
    response = json.loads(stdout.getvalue())

    assert response["messageType"] == "url_scan.error"
    assert response["outcome"]["status"] == "error"
    assert "score" not in response["outcome"]
    assert "internal diagnostic" not in stdout.getvalue()
    assert "internal diagnostic" in stderr.getvalue()


@pytest.mark.parametrize("mutation", [
    lambda value: value.update(schemaVersion="2.0"),
    lambda value: value.pop("requestId"),
    lambda value: value["context"].update(url="https://EXAMPLE.com"),
])
def test_stdio_v1_rejects_incompatible_or_malformed_input_before_analysis(mutation):
    from v1_stdio import process_stream

    request = request_envelope()
    mutation(request)
    calls = []
    stdout = io.StringIO()

    process_stream(
        io.StringIO(json.dumps(request)),
        stdout,
        io.StringIO(),
        lambda url: calls.append(url),
    )

    response = json.loads(stdout.getvalue())
    assert response["messageType"] == "url_scan.error"
    assert response["outcome"]["error"]["code"].startswith(("protocol.", "validation."))
    assert calls == []


def test_cli_contract_version_uses_pure_stdout_for_protocol_rejection():
    request = request_envelope()
    request["schemaVersion"] = "2.0"
    completed = subprocess.run(
        [sys.executable, str(ROOT / "analyze.py"), "--contract-version", "1"],
        input=json.dumps(request),
        text=True,
        capture_output=True,
        check=True,
        cwd=ROOT,
    )

    response = json.loads(completed.stdout)
    assert completed.stdout.count("\n") == 1
    assert response["messageType"] == "url_scan.error"
    assert response["outcome"]["error"]["code"] == "protocol.unsupported_schema_version"

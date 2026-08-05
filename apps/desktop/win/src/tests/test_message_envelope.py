"""
ASPS-669 (follow-on): Tests for validate_envelope's sentAt tolerance.

The .NET backend emits sentAt values using either:
  - "Z" suffix with milliseconds:  2026-08-05T20:02:59.603Z
  - explicit UTC offset with ticks-precision fractional seconds:
    2026-08-05T20:02:59.6033682+00:00

validate_envelope must accept both forms (they are both UTC) and must still
reject non-UTC offsets and malformed/missing timestamps.
"""

import os
import sys
import uuid

# ---------------------------------------------------------------------------
# Ensure the src directory is on the path so module imports resolve correctly.
# ---------------------------------------------------------------------------
SRC_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
if SRC_DIR not in sys.path:
    sys.path.insert(0, SRC_DIR)

from generated.messaging.v1.message_envelope import ContractError, validate_envelope  # noqa: E402


def _make_envelope(sent_at: str) -> dict:
    """Build a minimal, otherwise-valid url_scan.request envelope."""
    return {
        "schemaVersion": "1.0",
        "messageId": str(uuid.uuid4()),
        "correlationId": str(uuid.uuid4()),
        "requestId": str(uuid.uuid4()),
        "messageType": "url_scan.request",
        "sentAt": sent_at,
        "source": "extension",
        "context": {"deviceId": "device-1", "tabId": "1", "url": "https://example.com/"},
        "outcome": None,
        "payload": {},
    }


def test_z_suffix_milliseconds_passes():
    envelope = _make_envelope("2026-08-05T20:02:59.603Z")
    assert validate_envelope(envelope) is envelope


def test_z_suffix_ticks_precision_passes():
    envelope = _make_envelope("2026-08-05T20:02:59.6033682Z")
    assert validate_envelope(envelope) is envelope


def test_plus_zero_offset_milliseconds_passes():
    envelope = _make_envelope("2026-08-05T20:02:59.603+00:00")
    assert validate_envelope(envelope) is envelope


def test_plus_zero_offset_ticks_precision_passes():
    """.NET DateTime.ToString('o') style output: 7 fractional digits + explicit offset."""
    envelope = _make_envelope("2026-08-05T20:02:59.6033682+00:00")
    assert validate_envelope(envelope) is envelope


def test_non_utc_positive_offset_rejected():
    envelope = _make_envelope("2026-08-05T20:02:59.603+02:00")
    try:
        validate_envelope(envelope)
        assert False, "expected ContractError for non-UTC offset"
    except ContractError as exc:
        assert exc.code == "protocol.invalid_sent_at"


def test_non_utc_negative_offset_rejected():
    envelope = _make_envelope("2026-08-05T20:02:59.603-05:00")
    try:
        validate_envelope(envelope)
        assert False, "expected ContractError for non-UTC offset"
    except ContractError as exc:
        assert exc.code == "protocol.invalid_sent_at"


def test_missing_sent_at_rejected():
    envelope = _make_envelope("2026-08-05T20:02:59.603Z")
    del envelope["sentAt"]
    try:
        validate_envelope(envelope)
        assert False, "expected ContractError for missing sentAt"
    except ContractError as exc:
        assert exc.code == "protocol.malformed_envelope"


def test_garbage_sent_at_rejected():
    envelope = _make_envelope("not-a-timestamp")
    try:
        validate_envelope(envelope)
        assert False, "expected ContractError for malformed sentAt"
    except ContractError as exc:
        assert exc.code == "protocol.invalid_sent_at"


def test_naive_sent_at_without_offset_rejected():
    envelope = _make_envelope("2026-08-05T20:02:59.603")
    try:
        validate_envelope(envelope)
        assert False, "expected ContractError for naive (offset-less) sentAt"
    except ContractError as exc:
        assert exc.code == "protocol.invalid_sent_at"

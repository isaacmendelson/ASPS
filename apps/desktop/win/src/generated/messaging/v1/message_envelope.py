"""Generated ASPS messaging v1 binding. Do not edit."""
from __future__ import annotations
from datetime import datetime, timedelta, timezone
from urllib.parse import urlsplit, urlunsplit
from uuid import UUID, uuid4

SCHEMA_VERSION = "1.0"
SCHEMA_SHA256 = "b2de9ddb5eca827d84359a1e19afc8fd8afeef4828da0b51cb12e5d2f9a58ba4"
_MESSAGE_TYPE_PREFIXES = ("url_scan", "track_url", "tab_closed", "tab_changed", "remote_access")
_MESSAGE_TYPE_SUFFIXES = ("request", "accepted", "result", "error")
MESSAGE_TYPES = {
    f"{prefix}.{suffix}" for prefix in _MESSAGE_TYPE_PREFIXES for suffix in _MESSAGE_TYPE_SUFFIXES
}

# messageType suffixes whose outcome must be null (request-shaped envelopes --
# ASPS-732 extends this pattern from url_scan.* to every alert-carrying type).
_NULL_OUTCOME_SUFFIXES = (".request", ".accepted")
SOURCES = {"extension", "desktop", "backend", "analyzer"}

class ContractError(ValueError):
    def __init__(self, code: str, message: str):
        super().__init__(message)
        self.code = code

def canonicalize_url(value: str) -> str:
    try:
        parsed = urlsplit(value)
        if parsed.scheme.lower() not in ("http", "https") or not parsed.hostname:
            raise ValueError
        if parsed.username is not None or parsed.password is not None:
            raise ContractError("validation.invalid_canonical_url", "URL user-info is forbidden")
        host = parsed.hostname.encode("idna").decode("ascii").rstrip(".").lower()
        port = parsed.port
        authority = f"[{host}]" if ":" in host else host
        if port and not ((parsed.scheme.lower() == "http" and port == 80) or (parsed.scheme.lower() == "https" and port == 443)):
            authority += f":{port}"
        return urlunsplit((parsed.scheme.lower(), authority, parsed.path or "/", parsed.query, ""))
    except ContractError:
        raise
    except Exception as exc:
        raise ContractError("validation.invalid_canonical_url", "URL must be absolute HTTP or HTTPS") from exc

def validate_envelope(value: dict, require_device_id: bool = False) -> dict:
    required = {"schemaVersion", "messageId", "correlationId", "requestId", "messageType", "sentAt", "source", "context", "outcome", "payload"}
    if not isinstance(value, dict) or not required.issubset(value):
        raise ContractError("protocol.malformed_envelope", "Required envelope field is missing")
    version_parts = value["schemaVersion"].split(".", 1) if isinstance(value["schemaVersion"], str) else []
    if len(version_parts) != 2 or version_parts[0] != "1" or not version_parts[1].isdigit():
        raise ContractError("protocol.unsupported_schema_version", "Only schema major 1 is supported")
    for name in ("messageId", "correlationId", "requestId"):
        try:
            parsed = UUID(value[name])
            if str(parsed) != value[name].lower():
                raise ValueError
        except Exception as exc:
            raise ContractError("protocol.invalid_identifier", f"{name} must be a canonical UUID") from exc
    if value["messageType"] not in MESSAGE_TYPES or value["source"] not in SOURCES:
        raise ContractError("protocol.unsupported_discriminator", "Unsupported message type or source")
    try:
        raw_sent_at = value["sentAt"]
        # Accept both "Z" suffix and explicit "+00:00" offset notation; both mean UTC.
        # datetime.fromisoformat (3.11+) natively tolerates 3-7+ fractional digits.
        normalized_sent_at = raw_sent_at[:-1] + "+00:00" if raw_sent_at.endswith("Z") else raw_sent_at
        sent_at = datetime.fromisoformat(normalized_sent_at)
        if sent_at.tzinfo is None or sent_at.utcoffset() != timedelta(0):
            raise ValueError("sentAt must be UTC")
    except Exception as exc:
        raise ContractError("protocol.invalid_sent_at", "sentAt must be UTC with milliseconds") from exc
    context = value["context"]
    if not isinstance(context, dict) or set(context) != {"deviceId", "tabId", "url"}:
        raise ContractError("protocol.malformed_context", "context fields are invalid")
    if require_device_id and not context["deviceId"]:
        raise ContractError("validation.missing_device_id", "deviceId is required")
    if context["tabId"] is not None and (not isinstance(context["tabId"], str) or not context["tabId"].isdigit()):
        raise ContractError("validation.invalid_tab_id", "tabId must be a decimal string")
    if canonicalize_url(context["url"]) != context["url"]:
        raise ContractError("validation.canonical_url_mismatch", "context.url must be canonical")
    if not isinstance(value["payload"], dict):
        raise ContractError("protocol.invalid_payload", "payload must be an object")
    _validate_outcome(value["messageType"], value["outcome"])
    return value

def _validate_outcome(message_type: str, outcome) -> None:
    if message_type.endswith(_NULL_OUTCOME_SUFFIXES):
        if outcome is not None:
            raise ContractError("protocol.invalid_outcome", "Request outcome must be null")
        return
    expected = "success" if message_type.endswith(".result") else "error"
    if not isinstance(outcome, dict) or outcome.get("status") != expected:
        raise ContractError("protocol.invalid_outcome", "Outcome discriminator does not match message type")
    if expected == "success" and (set(outcome) != {"status", "result"} or not isinstance(outcome["result"], dict)):
        raise ContractError("protocol.invalid_outcome", "Success requires only result")
    if expected == "error":
        error = outcome.get("error")
        if set(outcome) != {"status", "error"} or not isinstance(error, dict) or not {"code", "message", "retryable"}.issubset(error):
            raise ContractError("protocol.invalid_outcome", "Error requires structured error")

def create_envelope(message_type: str, source: str, context: dict, payload: dict, *, request_id=None, correlation_id=None, outcome=None) -> dict:
    value = {
        "schemaVersion": SCHEMA_VERSION, "messageId": str(uuid4()),
        "correlationId": correlation_id or str(uuid4()), "requestId": request_id or str(uuid4()),
        "messageType": message_type,
        "sentAt": datetime.now(timezone.utc).isoformat(timespec="milliseconds").replace("+00:00", "Z"),
        "source": source, "context": context, "outcome": outcome, "payload": payload
    }
    return validate_envelope(value)

class RequestTracker:
    """Correlates concurrent/out-of-order responses strictly by immutable request identity."""
    def __init__(self):
        self._pending: dict[str, dict] = {}

    def add(self, envelope: dict) -> None:
        request_id = envelope["requestId"]
        if request_id in self._pending:
            raise ContractError("protocol.duplicate_request", "requestId is already pending")
        self._pending[request_id] = envelope

    def resolve(self, envelope: dict) -> dict:
        request = self._pending.get(envelope["requestId"])
        if request is None:
            raise ContractError("protocol.unknown_request", "requestId is not pending")
        if (request["correlationId"] != envelope["correlationId"] or
                request["context"] != envelope["context"]):
            raise ContractError("validation.immutable_context_mismatch", "Response context changed")
        del self._pending[envelope["requestId"]]
        return request

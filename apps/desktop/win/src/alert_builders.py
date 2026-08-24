"""
AntiScam Desktop App - Shared alert/token payload builders (ASPS-721)

Pure, transport-agnostic functions that build the exact JSON dict bodies
sent to the Backend for every alert/token message type. Both zmq_client.py
(ZMQ REQ transport) and ws_client.py (WebSocket transport) call these so
the two transports always produce byte-for-byte identical payloads, per
docs/architecture/WS-AGENT-PROTOCOL.md section 10:
"Message payloads are identical ... No field renaming, no casing changes,
no structural transformation."

No I/O, no transport concerns — safe to unit test directly.
"""

import uuid
from datetime import datetime
from typing import Any, Dict, List, Optional

from generated.messaging.v1.message_envelope import canonicalize_url, create_envelope, validate_envelope


# Priority enum mirror of Common.Enums.Priority on the backend.
class Priority:
    LOW = 0
    MEDIUM = 1
    HIGH = 2
    CRITICAL = 3


def _is_danger_active() -> bool:
    """Lazy import to avoid a circular dep — services/__init__.py loads
    monitor_service which imports get_local_ip from zmq_client."""
    try:
        from services.danger_mode import danger_mode
        return danger_mode.active
    except Exception:
        return False


def effective_priority(default: int) -> int:
    """While the agent is in ImmediateDanger mode every outbound alert is
    elevated to Critical; otherwise the caller's default is preserved."""
    return Priority.CRITICAL if _is_danger_active() else default


def attach_immediate_danger(device_info: Dict[str, Any]) -> Dict[str, Any]:
    """Stamp DeviceInfo with the current ImmediateDanger flag so the backend
    can correlate any alert to the danger window it was sent during."""
    device_info["ImmediateDanger"] = _is_danger_active()
    return device_info


def build_url_alert(
    device_uid: str, url: str, token: str = "",
    trackers: Optional[list] = None, iframes: Optional[list] = None,
    mac: str = "00:11:22:33:44:55",
    device_type: int = 1, os_type: int = 1,
    ip_address: str = "",
    tab_id: str = "",
) -> Dict[str, Any]:
    """Build a UrlAlert payload."""
    if token is None:
        token = ""
    return {
        "AlertId": str(uuid.uuid4()),
        "AlertType": "UrlAlert",
        "DeviceInfo": attach_immediate_danger({
            "DeviceUid": device_uid,
            "DeviceType": device_type,
            "OperatingSystem": os_type,
            "MACAddress": mac,
            "IP": ip_address
        }),
        "Timestamp": datetime.utcnow().isoformat() + "Z",
        "Priority": effective_priority(Priority.MEDIUM),
        "Token": token,
        "Url": url,
        "Trackers": trackers or [],
        "IFrameDomains": iframes or [],
        "UserAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
        "TabId": tab_id
    }


def wrap_url_alert_envelope(
    alert: Dict[str, Any], envelope: Dict[str, Any], device_uid: str, url: str, tab_id: str
) -> Any:
    """
    Wrap a UrlAlert in a MessageEnvelopeV1 request using a caller-supplied
    envelope (ASPS-611) -- e.g. the url_scan.request envelope the browser
    extension attaches to its scan. Returns the wire message to send
    (the envelope, with `alert` embedded in `payload`) together with the
    context dict used to validate the eventual response.

    Raises ValueError('validation.immutable_context_mismatch') if the
    envelope's context does not match the alert being sent.
    """
    validate_envelope(envelope)
    context = dict(envelope["context"])
    context["deviceId"] = device_uid
    if context["url"] != url or context["tabId"] != tab_id:
        raise ValueError("validation.immutable_context_mismatch")
    wire_message = create_envelope(
        "url_scan.request", "desktop", context, {"alert": alert},
        request_id=envelope["requestId"],
        correlation_id=envelope["correlationId"])
    return wire_message, context


def wrap_url_alert_default_envelope(
    alert: Dict[str, Any], device_uid: str, url: str, tab_id: str
) -> Any:
    """
    Build and apply a synthetic v1 envelope when no browser-supplied envelope
    exists -- e.g. the legacy `url_check` extension message handled by
    ExtensionHandler._handle_url_check, or any other UrlAlert caller that
    predates the ASPS-611 envelope protocol.

    The Azure backend runs with Messaging:AcceptLegacyV0=false, so any
    message without a `schemaVersion` field is routed to
    AlertProcessor.ProcessLegacyAlertAsync and rejected with "Legacy
    messaging v0 is disabled". Every UrlAlert must therefore travel inside a
    url_scan.request envelope -- even when the caller has no envelope of its
    own to forward.

    Canonicalizes the URL (context.url must already be canonical per
    validate_envelope) and normalizes tab_id to a non-empty decimal string,
    defaulting to "0" when unknown. tab_id is deliberately never left as
    None/"": AlertProcessor.ProcessEnvelopeAsync compares the wire alert's
    TabId against envelope.Context.TabId using JToken.ToString(), which
    renders a JSON null as "" -- while the typed Context.TabId deserializes
    a JSON null to a real C# null. "" != null, so a null tabId would fail
    the backend's immutable-context check on every request. Using the same
    non-null decimal string on both sides avoids that mismatch.

    Returns (wire_message, context), matching wrap_url_alert_envelope.
    """
    canonical_url = canonicalize_url(url)
    normalized_tab_id = str(tab_id) if tab_id else "0"
    if alert.get("Url") != canonical_url:
        alert = {**alert, "Url": canonical_url}
    if alert.get("TabId") != normalized_tab_id:
        alert = {**alert, "TabId": normalized_tab_id}
    context = {"deviceId": device_uid, "tabId": normalized_tab_id, "url": canonical_url}
    default_envelope = create_envelope("url_scan.request", "desktop", context, {})
    return wrap_url_alert_envelope(alert, default_envelope, device_uid, canonical_url, normalized_tab_id)


def validate_url_alert_envelope_response(
    response: Dict[str, Any], envelope: Dict[str, Any], context: Dict[str, Any]
) -> None:
    """Validate a MessageEnvelopeV1 response against the request envelope.
    Raises ValueError on mismatch."""
    validate_envelope(response, require_device_id=True)
    if (response["requestId"] != envelope["requestId"] or
            response["correlationId"] != envelope["correlationId"] or
            response["context"] != context):
        raise ValueError("validation.immutable_context_mismatch")


def build_track_url_alert(
    device_uid: str,
    url: str,
    from_url: str = '',
    duration: int = 0,
    scam_in_progress_key: str = '',
    ip_address: str = '',
    user_agent: str = '',
    tab_id: str = '',
    timezone: str = '',
    token: str = '',
    mac: str = "00:11:22:33:44:55",
    device_type: int = 1,
    os_type: int = 1
) -> Dict[str, Any]:
    """Build a TrackUrlAlert payload."""
    return {
        "AlertId": str(uuid.uuid4()),
        "AlertType": "TrackUrlAlert",
        "DeviceInfo": attach_immediate_danger({
            "DeviceUid": device_uid,
            "DeviceType": device_type,
            "OperatingSystem": os_type,
            "MACAddress": mac,
            "IP": ip_address
        }),
        "Timestamp": datetime.utcnow().isoformat() + "Z",
        "Priority": effective_priority(Priority.MEDIUM),
        "Token": token or "",
        "Url": url,
        "FromUrl": from_url,
        "Duration": duration,
        "ScamInProgressKey": scam_in_progress_key,
        "UserAgent": user_agent or "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
        "TabId": tab_id,
        "Timezone": timezone
    }


def wrap_track_url_alert_default_envelope(
    alert: Dict[str, Any], device_uid: str, url: str, tab_id: str
) -> Any:
    """
    Build and apply a synthetic v1 envelope for a TrackUrlAlert (ASPS-732),
    mirroring wrap_url_alert_default_envelope: the Azure backend
    (Messaging:AcceptLegacyV0=false) rejects any schemaVersion-less message
    with "Legacy messaging v0 is disabled", so TrackUrlAlert must travel
    inside a track_url.request envelope.

    Canonicalizes the URL and normalizes tab_id to a non-empty decimal
    string (default "0") for the same immutable-context reasons documented
    on wrap_url_alert_default_envelope.

    Returns (wire_message, context).
    """
    canonical_url = canonicalize_url(url)
    normalized_tab_id = str(tab_id) if tab_id else "0"
    if alert.get("Url") != canonical_url:
        alert = {**alert, "Url": canonical_url}
    if alert.get("TabId") != normalized_tab_id:
        alert = {**alert, "TabId": normalized_tab_id}
    context = {"deviceId": device_uid, "tabId": normalized_tab_id, "url": canonical_url}
    wire_message = create_envelope("track_url.request", "desktop", context, {"alert": alert})
    return wire_message, context


def wrap_tab_closed_alert_default_envelope(
    alert: Dict[str, Any], device_uid: str, url: str, tab_id: str
) -> Any:
    """
    Build and apply a synthetic v1 envelope for a TabClosedAlert (ASPS-732).
    Same canonicalization / tab_id-normalization rules as
    wrap_url_alert_default_envelope. Returns (wire_message, context).
    """
    canonical_url = canonicalize_url(url)
    normalized_tab_id = str(tab_id) if tab_id else "0"
    if alert.get("Url") != canonical_url:
        alert = {**alert, "Url": canonical_url}
    if alert.get("TabId") != normalized_tab_id:
        alert = {**alert, "TabId": normalized_tab_id}
    context = {"deviceId": device_uid, "tabId": normalized_tab_id, "url": canonical_url}
    wire_message = create_envelope("tab_closed.request", "desktop", context, {"alert": alert})
    return wire_message, context


def wrap_tab_changed_alert_default_envelope(
    alert: Dict[str, Any], device_uid: str, url: str, tab_id: str
) -> Any:
    """
    Build and apply a synthetic v1 envelope for a TabChangedAlert
    (ASPS-732). Same canonicalization / tab_id-normalization rules as
    wrap_url_alert_default_envelope. Returns (wire_message, context).
    """
    canonical_url = canonicalize_url(url)
    normalized_tab_id = str(tab_id) if tab_id else "0"
    if alert.get("Url") != canonical_url:
        alert = {**alert, "Url": canonical_url}
    if alert.get("TabId") != normalized_tab_id:
        alert = {**alert, "TabId": normalized_tab_id}
    context = {"deviceId": device_uid, "tabId": normalized_tab_id, "url": canonical_url}
    wire_message = create_envelope("tab_changed.request", "desktop", context, {"alert": alert})
    return wire_message, context


# Sentinel context URL for RemoteAccessAlert when no ConnectionUrl is known --
# RemoteAccessAlert has no Url field of its own, but the v1 envelope's
# context.url is mandatory and must be canonical (validate_envelope enforces
# an absolute http(s) URL).
_REMOTE_ACCESS_SENTINEL_URL = "https://remote-access.internal/"


def wrap_remote_access_alert_default_envelope(
    alert: Dict[str, Any], device_uid: str
) -> Any:
    """
    Build and apply a synthetic v1 envelope for a RemoteAccessAlert
    (ASPS-732). Unlike every other alert type, RemoteAccessAlert has no Url
    or TabId field -- there is no browser tab involved. context.url is
    therefore derived from the alert's own ConnectionUrl (which may be a
    bare IP such as "192.168.1.1" -- "https://" is prepended when no scheme
    is present) or, when ConnectionUrl is absent/unusable, the fixed
    _REMOTE_ACCESS_SENTINEL_URL. context.tabId is always "0" -- there is no
    tab concept for a remote-access session.

    Returns (wire_message, context).
    """
    connection_url = (alert.get("ConnectionUrl") or "").strip()
    if connection_url:
        candidate = connection_url if "://" in connection_url else f"https://{connection_url}"
        try:
            canonical_url = canonicalize_url(candidate)
        except Exception:
            canonical_url = _REMOTE_ACCESS_SENTINEL_URL
    else:
        canonical_url = _REMOTE_ACCESS_SENTINEL_URL

    context = {"deviceId": device_uid, "tabId": "0", "url": canonical_url}
    wire_message = create_envelope("remote_access.request", "desktop", context, {"alert": alert})
    return wire_message, context


def build_remote_access_alert(
    device_uid: str,
    remote_app: str,
    running_processes: int,
    connection_url: str,
    connection_status: str,
    session_status: str,
    token: str = "",
    mac: str = "00:11:22:33:44:55",
    device_type: int = 1,
    os_type: int = 1,
    direction: str = "unknown",
    confidence: str = "low",
    remote_country: str = "",
    remote_country_code: str = "",
    browser_tabs: Optional[List[Dict[str, Any]]] = None,
    ip_address: str = "",
    remote_os: str = "",
    remote_version: str = "",
    connection_type: str = "",
    file_transfer_active: bool = False,
    file_transfers: int = 0,
    remote_id: str = "",
    remote_name: str = "",
    logged_user: str = "",
    connection_id: str = "",
    software: str = "",
) -> Dict[str, Any]:
    """Build a RemoteAccessAlert payload with enhanced detection data."""
    if token is None:
        token = ""

    alert = {
        "AlertId": str(uuid.uuid4()),
        "AlertType": "RemoteAccessAlert",
        "DeviceInfo": attach_immediate_danger({
            "DeviceUid": device_uid,
            "DeviceType": device_type,
            "OperatingSystem": os_type,
            "MACAddress": mac,
            "IP": ip_address
        }),
        "Timestamp": datetime.utcnow().isoformat() + "Z",
        "Priority": effective_priority(Priority.MEDIUM),
        "Token": token,
        "RemoteAccessApp": remote_app,
        "RunningProcesses": running_processes,
        "ConnectionUrl": connection_url,
        "ConnectionStatus": connection_status,
        "SessionStatus": session_status,
        "Direction": direction,
        "Confidence": confidence,
        "RemoteCountry": remote_country,
        "RemoteCountryCode": remote_country_code,
        "RemoteOS": remote_os,
        "RemoteVersion": remote_version,
        "ConnectionType": connection_type,
        "FileTransferActive": file_transfer_active,
        "FileTransfers": file_transfers,
        "RemoteId": remote_id,
        "RemoteName": remote_name,
        "LoggedUser": logged_user,
        "ConnectionId": connection_id,
        "Software": software,
    }

    if browser_tabs is not None:
        alert["BrowserTabs"] = browser_tabs

    return alert


def build_tab_closed_alert(
    device_uid: str,
    tab_id: str,
    url: str,
    token: str = '',
    ip_address: str = '',
    mac: str = "00:11:22:33:44:55",
    device_type: int = 1,
    os_type: int = 1,
) -> Dict[str, Any]:
    """Build a TabClosedAlert payload."""
    return {
        "AlertId": str(uuid.uuid4()),
        "AlertType": "TabClosedAlert",
        "DeviceInfo": attach_immediate_danger({
            "DeviceUid": device_uid,
            "DeviceType": device_type,
            "OperatingSystem": os_type,
            "MACAddress": mac,
            "IP": ip_address,
        }),
        "Timestamp": datetime.utcnow().isoformat() + "Z",
        "Priority": effective_priority(Priority.HIGH),
        "Token": token or "",
        "TabId": tab_id,
        "Url": url,
    }


def build_tab_changed_alert(
    device_uid: str,
    tab_id: str,
    url: str,
    is_sensitive_website: Optional[bool] = None,
    is_logged_in: Optional[bool] = None,
    token: str = '',
    ip_address: str = '',
    mac: str = "00:11:22:33:44:55",
    device_type: int = 1,
    os_type: int = 1,
) -> Dict[str, Any]:
    """Build a TabChangedAlert payload."""
    return {
        "AlertId": str(uuid.uuid4()),
        "AlertType": "TabChangedAlert",
        "DeviceInfo": attach_immediate_danger({
            "DeviceUid": device_uid,
            "DeviceType": device_type,
            "OperatingSystem": os_type,
            "MACAddress": mac,
            "IP": ip_address,
        }),
        "Timestamp": datetime.utcnow().isoformat() + "Z",
        "Priority": effective_priority(Priority.HIGH),
        "Token": token or "",
        "TabId": tab_id,
        "Url": url,
        "IsSensitiveWebsite": is_sensitive_website,
        "IsLoggedIn": is_logged_in,
    }


def build_request_token_message(device_uid: str, email: str = "") -> Dict[str, Any]:
    """Build a RequestToken message.

    SupportedSchemaMajors advertises the messaging schema major versions
    this agent understands (ASPS: v1 protocol negotiation). The Backend
    (AlertProcessor.HandleRegisterDevice) defaults a missing/absent field
    to [0], which fails negotiation in environments where AcceptLegacyV0
    is false (e.g. Azure). Must always be sent.
    """
    return {
        "MessageType": "RequestToken",
        "DeviceUid": device_uid,
        "Email": email,
        "SupportedSchemaMajors": [1],
    }


def build_refresh_token_message(device_uid: str, old_token: str) -> Dict[str, Any]:
    """Build a RefreshToken message.

    SupportedSchemaMajors is included for forward compatibility even
    though HandleRefreshToken does not currently gate on it.
    """
    return {
        "MessageType": "RefreshToken",
        "DeviceUid": device_uid,
        "Token": old_token,
        "Timestamp": datetime.utcnow().isoformat() + "Z",
        "SupportedSchemaMajors": [1],
    }

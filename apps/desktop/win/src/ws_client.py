"""
AntiScam Desktop App - WebSocket Transport Client (ASPS-721)

Implements the desktop-agent side of docs/architecture/WS-AGENT-PROTOCOL.md
(ADR-004 / ASPS-718). A single persistent WebSocket connection to the
WebApi gateway (`wss://{host}/ws/agent`) carries both:

  - request/response alert & token traffic  (replaces ZMQClient's REQ/REP)
  - notification push traffic               (replaces NotificationClient's SUB)

Selected via TRANSPORT_MODE == "ws" in core/container.py. WSClient exposes
the same synchronous, blocking call surface used today by ScanService,
MonitorService and AuthManager (send_url_alert, send_remote_access_alert,
send_request_token, on_notification, start/stop, ...) so it is a drop-in
replacement for ZMQClient + NotificationClient combined.

Internally, a dedicated background thread runs an asyncio event loop that
owns the WebSocket connection, the receive loop, and the reconnect-with-
backoff state machine. Public (synchronous) methods marshal work onto that
loop via asyncio.run_coroutine_threadsafe() and block — with a timeout —
for the result, mirroring the blocking contract of ZMQClient.send_alert().

CURVE is not used over this transport — TLS (wss://) is the transport
security boundary (see ADR-004 §"Security analysis").
"""

import asyncio
import concurrent.futures
import json
import logging
import random
import threading
import uuid
from datetime import datetime, timezone
from typing import Any, Callable, Dict, List, Optional

import websockets

from alert_builders import (
    build_url_alert,
    wrap_url_alert_envelope,
    validate_url_alert_envelope_response,
    build_track_url_alert,
    build_remote_access_alert,
    build_tab_closed_alert,
    build_tab_changed_alert,
    build_request_token_message,
    build_refresh_token_message,
)

logger = logging.getLogger(__name__)

SUBPROTOCOL = "asps-agent-v1"
REQUEST_TIMEOUT_SECONDS = 15
CONNECT_TIMEOUT_SECONDS = 10
FRAME_SEND_TIMEOUT_SECONDS = 5

# Reconnect backoff schedule per WS-AGENT-PROTOCOL.md section 8:
# "1s, 2s, 4s, 8s, 16s, 30s max" + random 0-1s jitter.
BACKOFF_SCHEDULE = [1, 2, 4, 8, 16, 30]

# Token-response statuses that indicate a successful RequestToken/RefreshToken
# exchange, per WS-AGENT-PROTOCOL.md section 4.2.
_AUTH_SUCCESS_STATUSES = ("TokenCreated", "ExistingToken", "TokenRefreshed")


# ─────────────────────────────────────────────────────────────────────────
# Pure helpers — frame construction / parsing / backoff (no I/O; unit-testable)
# ─────────────────────────────────────────────────────────────────────────

def new_correlation_id() -> str:
    """Generate a UUID v4 correlation id for a 'request' frame."""
    return str(uuid.uuid4())


def build_request_frame(payload: Dict[str, Any], msg_id: Optional[str] = None) -> Dict[str, Any]:
    """Build a client->server 'request' frame."""
    return {"frame": "request", "id": msg_id or new_correlation_id(), "payload": payload}


def build_subscribe_frame(device_uid: str) -> Dict[str, Any]:
    """Build a client->server 'subscribe' frame."""
    return {"frame": "subscribe", "deviceUid": device_uid}


def build_pong_frame() -> Dict[str, Any]:
    """Build a client->server application-level 'pong' frame."""
    ts = datetime.now(timezone.utc).isoformat(timespec="milliseconds").replace("+00:00", "Z")
    return {"frame": "pong", "ts": ts}


def parse_frame(raw: str) -> Dict[str, Any]:
    """Parse a raw WS text frame into a dict.

    Raises ValueError on invalid JSON, a non-object payload, or a missing/
    non-string 'frame' discriminator (mirrors gateway error
    'gateway.invalid_frame' semantics on the agent side).
    """
    try:
        data = json.loads(raw)
    except (json.JSONDecodeError, TypeError) as e:
        raise ValueError(f"invalid JSON frame: {e}") from e
    if not isinstance(data, dict) or not isinstance(data.get("frame"), str):
        raise ValueError("frame missing 'frame' discriminator")
    return data


def compute_backoff_delay(attempt: int, *, jitter: Optional[float] = None) -> float:
    """
    Exponential reconnect backoff per WS-AGENT-PROTOCOL.md section 8:
    1s, 2s, 4s, 8s, 16s, 30s max, plus random 0-1s jitter.

    `attempt` is 0-based (0 -> first reconnect attempt -> ~1s).
    `jitter` overrides the random component for deterministic tests; when
    None, a random value in [0, 1) is used.
    """
    idx = min(max(attempt, 0), len(BACKOFF_SCHEDULE) - 1)
    base = BACKOFF_SCHEDULE[idx]
    j = random.random() if jitter is None else jitter
    return base + j


def redact_frame_for_log(frame: Dict[str, Any]) -> Dict[str, Any]:
    """Return a copy of a 'request' frame with payload.Token redacted, for
    safe logging (mirrors ZMQClient.send_alert's log redaction)."""
    if frame.get("frame") != "request":
        return frame
    payload = frame.get("payload")
    if isinstance(payload, dict) and "Token" in payload:
        return {**frame, "payload": {**payload, "Token": "[REDACTED]"}}
    return frame


# ─────────────────────────────────────────────────────────────────────────
# Request/response correlation registry
# ─────────────────────────────────────────────────────────────────────────

class PendingRequests:
    """
    Thread-safe registry correlating outbound 'request' frames (by id) with
    their eventual 'response'/'error' frame, or a synthetic failure on
    disconnect/timeout.

    concurrent.futures.Future (NOT asyncio.Future) is used deliberately: it
    supports a blocking, thread-safe .result(timeout=...) call from the
    caller's thread, while being resolved from the WS receive loop running
    on the WSClient's dedicated event-loop thread.
    """

    def __init__(self):
        self._lock = threading.Lock()
        self._pending: Dict[str, concurrent.futures.Future] = {}

    def register(self, msg_id: str) -> concurrent.futures.Future:
        fut: concurrent.futures.Future = concurrent.futures.Future()
        with self._lock:
            self._pending[msg_id] = fut
        return fut

    def resolve(self, msg_id: Optional[str], payload: Optional[dict]) -> bool:
        """Resolve a pending request with a response payload.

        Returns False if no matching pending request exists (e.g. a late or
        duplicate frame, or an id echoed for a request we never sent)."""
        if msg_id is None:
            return False
        with self._lock:
            fut = self._pending.pop(msg_id, None)
        if fut is None or fut.done():
            return False
        fut.set_result(payload)
        return True

    def fail_all(self) -> None:
        """Resolve every still-pending request with None (used on disconnect
        so blocked callers don't hang until their individual timeout)."""
        with self._lock:
            pending = list(self._pending.items())
            self._pending.clear()
        for _, fut in pending:
            if not fut.done():
                fut.set_result(None)

    def discard(self, msg_id: str) -> None:
        with self._lock:
            self._pending.pop(msg_id, None)

    def __len__(self) -> int:
        with self._lock:
            return len(self._pending)


class WSClient:
    """
    WebSocket transport client combining the ZMQClient (request/response)
    and NotificationClient (push subscription) roles into a single
    persistent connection, per docs/architecture/WS-AGENT-PROTOCOL.md.
    """

    def __init__(self, device_uid: str, ws_url: str):
        self.device_uid = device_uid
        self.ws_url = ws_url

        # Kept for interface parity with ZMQClient/NotificationClient — WS
        # transport uses TLS, not CURVE, so these are informational no-ops.
        self.server_public_key: Optional[bytes] = None

        self._ws = None  # active websockets.ClientConnection, or None
        self._loop: Optional[asyncio.AbstractEventLoop] = None
        self._thread: Optional[threading.Thread] = None
        self._running = False
        self._connected_event = threading.Event()

        self._pending = PendingRequests()

        # Auth + subscription state, replayed automatically after a
        # reconnect (WS-AGENT-PROTOCOL.md section 8: "Authenticate ...
        # Subscribe ... Resume").
        self._authenticated = False
        self._last_auth_payload: Optional[Dict[str, Any]] = None
        self._subscribed_device: Optional[str] = None

        # Callbacks (same names/shapes as NotificationClient for parity).
        self._on_notification_callback: Optional[Callable[[Dict[str, Any]], None]] = None
        self._on_connected_callback: Optional[Callable[[], None]] = None
        self._on_disconnected_callback: Optional[Callable[[], None]] = None

        print("[WS] Client initialized")
        print(f"[WS] Server: {self.ws_url}")
        print(f"[WS] Device: {self.device_uid}")

    # ── Interface parity: CURVE is a no-op on this transport ──────────────

    def set_server_public_key(self, key: bytes) -> None:
        """No-op for WS transport — TLS is the transport security boundary,
        not CURVE. Kept for interface parity with ZMQClient/NotificationClient
        so callers don't need to branch on transport type."""
        self.server_public_key = key
        logger.debug("[WS] set_server_public_key called — ignored (WS uses TLS, not CURVE)")

    def clear_server_public_key(self) -> None:
        self.server_public_key = None

    # ── Callback registration (NotificationClient parity) ─────────────────

    def on_notification(self, callback: Callable[[Dict[str, Any]], None]) -> None:
        self._on_notification_callback = callback

    def on_connected(self, callback: Callable[[], None]) -> None:
        self._on_connected_callback = callback

    def on_disconnected(self, callback: Callable[[], None]) -> None:
        self._on_disconnected_callback = callback

    # ── Lifecycle ───────────────────────────────────────────────────────

    def _ensure_loop_running(self) -> None:
        if self._thread and self._thread.is_alive():
            return
        self._running = True
        self._connected_event.clear()
        self._thread = threading.Thread(target=self._thread_main, daemon=True, name="WSClient")
        self._thread.start()

    def _thread_main(self) -> None:
        self._loop = asyncio.new_event_loop()
        asyncio.set_event_loop(self._loop)
        try:
            self._loop.run_until_complete(self._run())
        except Exception:
            logger.exception("[WS] background loop crashed")
        finally:
            self._loop.close()
            self._loop = None

    def connect(self) -> bool:
        """Ensure the background connection is up. Idempotent — a no-op
        (returns True immediately) once connected. Used by every send_*
        method, mirroring ZMQClient.connect(), but here the connection is
        persistent rather than opened per-request."""
        self._ensure_loop_running()
        if self._connected_event.is_set() and self._ws is not None:
            return True
        return self._connected_event.wait(timeout=CONNECT_TIMEOUT_SECONDS)

    def start(self) -> bool:
        """NotificationClient-parity entry point. Ensures the connection is
        up; subscription happens automatically once authenticated (see
        send_request_token/send_refresh_token)."""
        ok = self.connect()
        print("[WS] Started listening" if ok else "[WS] WARNING: start() could not connect")
        return ok

    def close(self) -> None:
        """No-op — unlike ZMQClient's per-request REQ socket, the WS
        connection is persistent and shared across all send_* calls."""
        return

    def stop(self) -> None:
        """NotificationClient-parity stop — alias for destroy()."""
        self.destroy()

    def destroy(self) -> None:
        """Fully shut down: close the WS connection and stop the background
        event loop thread. Call only on app shutdown."""
        self._running = False
        loop = self._loop
        if loop and loop.is_running():
            try:
                asyncio.run_coroutine_threadsafe(self._close_ws(), loop).result(timeout=5)
            except Exception:
                logger.debug("[WS] error closing during destroy()", exc_info=True)
        if self._thread and self._thread.is_alive():
            self._thread.join(timeout=5)
        self._connected_event.clear()
        self._pending.fail_all()
        print("[WS] Client destroyed")

    async def _close_ws(self) -> None:
        if self._ws is not None:
            try:
                await self._ws.close()
            except Exception:
                pass

    @property
    def is_running(self) -> bool:
        return self._connected_event.is_set()

    # ── Background connection loop ────────────────────────────────────────

    async def _run(self) -> None:
        attempt = 0
        while self._running:
            try:
                print(f"[WS] Connecting to {self.ws_url} ...")
                async with websockets.connect(
                    self.ws_url,
                    subprotocols=[SUBPROTOCOL],
                    open_timeout=CONNECT_TIMEOUT_SECONDS,
                ) as ws:
                    self._ws = ws
                    attempt = 0
                    print("[WS] Connected")

                    receive_task = asyncio.ensure_future(self._receive_loop(ws))
                    try:
                        await self._on_reconnected()
                        self._connected_event.set()
                        if self._on_connected_callback:
                            self._safe_callback(self._on_connected_callback)
                        await receive_task
                    finally:
                        if not receive_task.done():
                            receive_task.cancel()
                            try:
                                await receive_task
                            except (asyncio.CancelledError, Exception):
                                pass
            except asyncio.CancelledError:
                raise
            except Exception as e:
                logger.warning(f"[WS] connection error: {e}")
                print(f"[WS] WARNING: connection error: {e}")
            finally:
                self._ws = None
                self._connected_event.clear()
                self._pending.fail_all()
                self._authenticated = False
                if self._on_disconnected_callback:
                    self._safe_callback(self._on_disconnected_callback)

            if not self._running:
                break

            delay = compute_backoff_delay(attempt)
            print(f"[WS] Reconnecting in {delay:.1f}s (attempt {attempt + 1})")
            attempt += 1
            try:
                await asyncio.sleep(delay)
            except asyncio.CancelledError:
                raise

    async def _on_reconnected(self) -> None:
        """Replay the last successful auth request and re-subscribe, per
        WS-AGENT-PROTOCOL.md section 8 ("Authenticate ... Subscribe").
        No-op on the very first connection (nothing to replay yet)."""
        self._authenticated = False
        if self._last_auth_payload is None:
            return

        print("[WS] Reconnected — replaying authentication")
        response = await self._do_request(self._last_auth_payload)
        if response and response.get("status") in _AUTH_SUCCESS_STATUSES:
            self._authenticated = True
            if self._subscribed_device:
                await self._send_subscribe(self._subscribed_device)

    @staticmethod
    def _safe_callback(callback: Callable[[], None]) -> None:
        try:
            callback()
        except Exception:
            logger.exception("[WS] callback failed")

    # ── Receive loop / frame dispatch ─────────────────────────────────────

    async def _receive_loop(self, ws) -> None:
        async for raw in ws:
            try:
                frame = parse_frame(raw)
            except ValueError as e:
                logger.warning(f"[WS] invalid frame received: {e}")
                continue
            await self._dispatch_frame(frame)

    async def _dispatch_frame(self, frame: Dict[str, Any]) -> None:
        ftype = frame.get("frame")
        if ftype == "response":
            self._pending.resolve(frame.get("id"), frame.get("payload"))
        elif ftype == "error":
            self._handle_error_frame(frame)
        elif ftype == "notification":
            self._dispatch_notification(frame)
        elif ftype == "ping":
            await self._send_frame_async(build_pong_frame())
        else:
            logger.debug(f"[WS] unhandled frame type: {ftype!r}")

    def _handle_error_frame(self, frame: Dict[str, Any]) -> None:
        code = frame.get("code", "unknown")
        message = frame.get("message", "")
        msg_id = frame.get("id")
        logger.warning(f"[WS] error frame: code={code} id={msg_id} message={message}")
        print(f"[WS] ERROR frame: {code} — {message}")
        if msg_id is not None:
            # Treat as a failed request (mirrors ZMQClient timeout -> None),
            # so callers get the same "no response" contract on any failure.
            self._pending.resolve(msg_id, None)

    def _dispatch_notification(self, frame: Dict[str, Any]) -> None:
        payload = frame.get("payload")
        if not isinstance(payload, dict):
            logger.warning("[WS] notification frame missing object payload")
            return
        if self._on_notification_callback:
            try:
                self._on_notification_callback(payload)
            except Exception:
                logger.exception("[WS] notification callback failed")

    # ── Frame sending (async, runs on the WSClient's own loop) ────────────

    async def _send_frame_async(self, frame: Dict[str, Any]) -> bool:
        if self._ws is None:
            return False
        try:
            text = json.dumps(frame)
            print(f"[WS] SENDING frame: {redact_frame_for_log(frame)}")
            await self._ws.send(text)
            return True
        except websockets.ConnectionClosed:
            logger.warning("[WS] send failed — connection closed")
            return False
        except Exception as e:
            logger.error(f"[WS] send error: {e}")
            return False

    def _send_frame_sync(self, frame: Dict[str, Any]) -> bool:
        """Schedule a frame send on the WSClient's event loop and block for
        the result. Used by subscribe/pong-style fire calls issued from
        outside the loop thread."""
        loop = self._loop
        if loop is None or not loop.is_running():
            return False
        try:
            future = asyncio.run_coroutine_threadsafe(self._send_frame_async(frame), loop)
            return future.result(timeout=FRAME_SEND_TIMEOUT_SECONDS)
        except Exception as e:
            logger.error(f"[WS] send_frame_sync failed: {e}")
            return False

    async def _send_subscribe(self, device_uid: str) -> bool:
        ok = await self._send_frame_async(build_subscribe_frame(device_uid))
        if ok:
            self._subscribed_device = device_uid
            print(f"[WS] Subscribed to device:{device_uid}")
        return ok

    # ── Request/response (async core + sync wrapper) ──────────────────────

    async def _do_request(self, payload: Dict[str, Any], timeout: float = REQUEST_TIMEOUT_SECONDS) -> Optional[Dict[str, Any]]:
        """Send a 'request' frame and await its correlated response — runs
        entirely on the WSClient's own event loop (used by both the sync
        wrapper and internal reconnect replay)."""
        msg_id = new_correlation_id()
        fut = self._pending.register(msg_id)
        frame = build_request_frame(payload, msg_id)

        sent = await self._send_frame_async(frame)
        if not sent:
            self._pending.discard(msg_id)
            return None

        try:
            return await asyncio.wait_for(asyncio.wrap_future(fut, loop=self._loop), timeout=timeout)
        except asyncio.TimeoutError:
            print(f"[WS] WARNING: Timeout waiting for response to request {msg_id[:8]}...")
            self._pending.discard(msg_id)
            return None

    def send_alert(self, alert: Dict[str, Any]) -> Optional[Dict[str, Any]]:
        """Send a request frame and block for the correlated response.
        Same contract as ZMQClient.send_alert(): returns the response dict,
        or None on timeout/failure."""
        if not self.connect():
            print("[WS] ERROR: Not connected. Call connect() first.")
            return None
        loop = self._loop
        if loop is None or not loop.is_running():
            return None
        try:
            future = asyncio.run_coroutine_threadsafe(self._do_request(alert), loop)
            return future.result(timeout=REQUEST_TIMEOUT_SECONDS + 2)
        except concurrent.futures.TimeoutError:
            print("[WS] WARNING: send_alert timed out waiting for scheduling")
            return None
        except Exception as e:
            logger.error(f"[WS] send_alert error: {e}")
            return None

    # ── Alert senders (ZMQClient interface parity — identical payloads) ───

    def send_url_alert(self, device_uid: str, url: str, token: str = "",
                        trackers: list = None, iframes: list = None,
                        mac: str = "00:11:22:33:44:55",
                        device_type: int = 1, os_type: int = 1,
                        ip_address: str = "",
                        tab_id: str = "",
                        envelope: Optional[Dict[str, Any]] = None) -> Optional[Dict[str, Any]]:
        """Send a UrlAlert. Same payload shape and same request-envelope
        support as ZMQClient.send_url_alert()."""
        alert = build_url_alert(
            device_uid=device_uid, url=url, token=token, trackers=trackers,
            iframes=iframes, mac=mac, device_type=device_type, os_type=os_type,
            ip_address=ip_address, tab_id=tab_id,
        )
        wire_message = alert
        context = None
        if envelope is not None:
            wire_message, context = wrap_url_alert_envelope(alert, envelope, device_uid, url, tab_id)

        response = self.send_alert(wire_message)
        if envelope is not None and response is not None:
            validate_url_alert_envelope_response(response, envelope, context)
        return response

    def send_track_url_alert(
        self, device_uid: str, url: str, from_url: str = '', duration: int = 0,
        scam_in_progress_key: str = '', ip_address: str = '', user_agent: str = '',
        tab_id: str = '', timezone: str = '', token: str = '',
        mac: str = "00:11:22:33:44:55", device_type: int = 1, os_type: int = 1,
    ) -> Optional[Dict[str, Any]]:
        """Send a TrackUrlAlert."""
        alert = build_track_url_alert(
            device_uid=device_uid, url=url, from_url=from_url, duration=duration,
            scam_in_progress_key=scam_in_progress_key, ip_address=ip_address,
            user_agent=user_agent, tab_id=tab_id, timezone=timezone, token=token,
            mac=mac, device_type=device_type, os_type=os_type,
        )
        return self.send_alert(alert)

    def send_remote_access_alert(
        self, device_uid: str, remote_app: str, running_processes: int,
        connection_url: str, connection_status: str, session_status: str,
        token: str = "", mac: str = "00:11:22:33:44:55", device_type: int = 1,
        os_type: int = 1, direction: str = "unknown", confidence: str = "low",
        remote_country: str = "", remote_country_code: str = "",
        browser_tabs: Optional[List[Dict[str, Any]]] = None, ip_address: str = "",
        remote_os: str = "", remote_version: str = "", connection_type: str = "",
        file_transfer_active: bool = False, file_transfers: int = 0,
        remote_id: str = "", remote_name: str = "", logged_user: str = "",
        connection_id: str = "", software: str = "",
    ) -> Optional[Dict[str, Any]]:
        """Send a RemoteAccessAlert with enhanced detection data."""
        alert = build_remote_access_alert(
            device_uid=device_uid, remote_app=remote_app, running_processes=running_processes,
            connection_url=connection_url, connection_status=connection_status,
            session_status=session_status, token=token, mac=mac, device_type=device_type,
            os_type=os_type, direction=direction, confidence=confidence,
            remote_country=remote_country, remote_country_code=remote_country_code,
            browser_tabs=browser_tabs, ip_address=ip_address, remote_os=remote_os,
            remote_version=remote_version, connection_type=connection_type,
            file_transfer_active=file_transfer_active, file_transfers=file_transfers,
            remote_id=remote_id, remote_name=remote_name, logged_user=logged_user,
            connection_id=connection_id, software=software,
        )
        return self.send_alert(alert)

    def send_tab_closed_alert(
        self, device_uid: str, tab_id: str, url: str, token: str = '',
        ip_address: str = '', mac: str = "00:11:22:33:44:55",
        device_type: int = 1, os_type: int = 1,
    ) -> Optional[Dict[str, Any]]:
        """Send a TabClosedAlert."""
        alert = build_tab_closed_alert(
            device_uid=device_uid, tab_id=tab_id, url=url, token=token,
            ip_address=ip_address, mac=mac, device_type=device_type, os_type=os_type,
        )
        return self.send_alert(alert)

    def send_tab_changed_alert(
        self, device_uid: str, tab_id: str, url: str,
        is_sensitive_website: Optional[bool] = None, is_logged_in: Optional[bool] = None,
        token: str = '', ip_address: str = '', mac: str = "00:11:22:33:44:55",
        device_type: int = 1, os_type: int = 1,
    ) -> Optional[Dict[str, Any]]:
        """Send a TabChangedAlert."""
        alert = build_tab_changed_alert(
            device_uid=device_uid, tab_id=tab_id, url=url,
            is_sensitive_website=is_sensitive_website, is_logged_in=is_logged_in,
            token=token, ip_address=ip_address, mac=mac, device_type=device_type,
            os_type=os_type,
        )
        return self.send_alert(alert)

    # ── Token lifecycle (auto-subscribes on success; replayed on reconnect) ─

    def send_request_token(self, device_uid: str, email: str = "") -> Optional[Dict[str, Any]]:
        """Send a RequestToken message to authenticate the device. On
        success, automatically subscribes to notifications for this
        deviceUid (WS-AGENT-PROTOCOL.md section 4: authenticate then
        subscribe) and remembers the payload so a future reconnect can
        replay authentication automatically."""
        message = build_request_token_message(device_uid, email)
        self.device_uid = device_uid
        self._last_auth_payload = message
        print(f"[WS] Sending RequestToken for device: {device_uid}")
        response = self.send_alert(message)
        self._handle_auth_response(response, device_uid)
        return response

    def send_refresh_token(self, device_uid: str, old_token: str) -> Optional[Dict[str, Any]]:
        """Send a RefreshToken message to renew an expired token."""
        message = build_refresh_token_message(device_uid, old_token)
        self.device_uid = device_uid
        self._last_auth_payload = message
        print(f"[WS] Sending RefreshToken for device: {device_uid}")
        response = self.send_alert(message)
        self._handle_auth_response(response, device_uid)
        return response

    def _handle_auth_response(self, response: Optional[Dict[str, Any]], device_uid: str) -> None:
        if not response or response.get("status") not in _AUTH_SUCCESS_STATUSES:
            return
        self._authenticated = True
        loop = self._loop
        if loop is None or not loop.is_running():
            return
        try:
            future = asyncio.run_coroutine_threadsafe(self._send_subscribe(device_uid), loop)
            future.result(timeout=FRAME_SEND_TIMEOUT_SECONDS)
        except Exception as e:
            logger.warning(f"[WS] auto-subscribe after auth failed: {e}")

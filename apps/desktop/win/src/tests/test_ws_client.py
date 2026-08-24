"""
ASPS-721: Tests for the WebSocket transport client (ws_client.py).

Covers docs/architecture/WS-AGENT-PROTOCOL.md behavior implemented by
WSClient:
  - Frame serialization/deserialization (request/subscribe/pong, parse_frame)
  - Request/response correlation by 'id' (PendingRequests + WSClient._do_request)
  - Frame dispatch (response / error / notification / ping)
  - Authentication state tracking + auto-subscribe on successful auth
  - Reconnection backoff timing (section 8: 1s,2s,4s,8s,16s,30s + jitter)
  - Payload parity with zmq_client/alert_builders (section 10: identical payloads)
  - Interface parity no-ops (CURVE setters, close())
"""

import asyncio
import json
import os
import sys
import threading
import time
import unittest
from typing import List, Optional
from unittest.mock import MagicMock, patch

# ---------------------------------------------------------------------------
# Ensure the src directory is on the path so module imports resolve correctly.
# ---------------------------------------------------------------------------
SRC_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
sys.path.insert(0, SRC_DIR)

import ws_client
from ws_client import (
    WSClient,
    PendingRequests,
    build_request_frame,
    build_subscribe_frame,
    build_pong_frame,
    parse_frame,
    compute_backoff_delay,
    redact_frame_for_log,
    BACKOFF_SCHEDULE,
)
import alert_builders


# ---------------------------------------------------------------------------
# Test double: a minimal async iterable/sendable fake WebSocket connection.
# ---------------------------------------------------------------------------

class FakeWebSocket:
    """Duck-types the subset of websockets.ClientConnection used by WSClient:
    async send(text), async iteration over incoming text frames, async close()."""

    def __init__(self):
        self.sent: List[str] = []
        self._incoming: "asyncio.Queue" = asyncio.Queue()
        self.closed = False

    def push_incoming(self, raw: str) -> None:
        self._incoming.put_nowait(raw)

    async def send(self, text: str) -> None:
        self.sent.append(text)

    def __aiter__(self):
        return self

    async def __anext__(self):
        item = await self._incoming.get()
        if item is None:
            raise StopAsyncIteration
        return item

    async def close(self) -> None:
        self.closed = True


def _run(coro):
    """Run a coroutine to completion on a fresh event loop (no pytest-asyncio
    dependency in this project's requirements)."""
    return asyncio.run(coro)


# ---------------------------------------------------------------------------
# Pure helper tests — frame construction / parsing
# ---------------------------------------------------------------------------

class TestFrameBuilders(unittest.TestCase):
    def test_build_request_frame_has_uuid_id_and_payload(self):
        frame = build_request_frame({"MessageType": "RequestToken"})
        self.assertEqual(frame["frame"], "request")
        self.assertEqual(frame["payload"], {"MessageType": "RequestToken"})
        # id must look like a UUID v4 (36 chars, 4 dashes)
        self.assertEqual(len(frame["id"]), 36)
        self.assertEqual(frame["id"].count("-"), 4)

    def test_build_request_frame_uses_supplied_id(self):
        frame = build_request_frame({"a": 1}, msg_id="fixed-id-123")
        self.assertEqual(frame["id"], "fixed-id-123")

    def test_two_request_frames_get_different_ids(self):
        f1 = build_request_frame({})
        f2 = build_request_frame({})
        self.assertNotEqual(f1["id"], f2["id"])

    def test_build_subscribe_frame(self):
        frame = build_subscribe_frame("PC-JOHN-001")
        self.assertEqual(frame, {"frame": "subscribe", "deviceUid": "PC-JOHN-001"})

    def test_build_pong_frame_has_iso_timestamp(self):
        frame = build_pong_frame()
        self.assertEqual(frame["frame"], "pong")
        self.assertTrue(frame["ts"].endswith("Z"))

    def test_parse_frame_roundtrip(self):
        original = {"frame": "response", "id": "abc", "payload": {"ok": True}}
        parsed = parse_frame(json.dumps(original))
        self.assertEqual(parsed, original)

    def test_parse_frame_rejects_invalid_json(self):
        with self.assertRaises(ValueError):
            parse_frame("{not json")

    def test_parse_frame_rejects_missing_frame_discriminator(self):
        with self.assertRaises(ValueError):
            parse_frame(json.dumps({"id": "abc", "payload": {}}))

    def test_parse_frame_rejects_non_object_json(self):
        with self.assertRaises(ValueError):
            parse_frame(json.dumps([1, 2, 3]))

    def test_redact_frame_for_log_masks_token(self):
        frame = build_request_frame({"MessageType": "RequestToken", "Token": "secret-abc"})
        redacted = redact_frame_for_log(frame)
        self.assertEqual(redacted["payload"]["Token"], "[REDACTED]")
        # Original frame object must not be mutated.
        self.assertEqual(frame["payload"]["Token"], "secret-abc")

    def test_redact_frame_for_log_passthrough_for_non_request_frames(self):
        frame = build_subscribe_frame("PC-1")
        self.assertEqual(redact_frame_for_log(frame), frame)


class TestBackoffTiming(unittest.TestCase):
    """WS-AGENT-PROTOCOL.md section 8: 1s, 2s, 4s, 8s, 16s, 30s max + 0-1s jitter."""

    def test_backoff_schedule_matches_spec(self):
        self.assertEqual(BACKOFF_SCHEDULE, [1, 2, 4, 8, 16, 30])

    def test_backoff_increases_per_attempt_with_zero_jitter(self):
        delays = [compute_backoff_delay(i, jitter=0) for i in range(6)]
        self.assertEqual(delays, [1, 2, 4, 8, 16, 30])

    def test_backoff_caps_at_30s_for_further_attempts(self):
        self.assertEqual(compute_backoff_delay(6, jitter=0), 30)
        self.assertEqual(compute_backoff_delay(100, jitter=0), 30)

    def test_backoff_never_goes_below_base_for_negative_attempt(self):
        # Defensive: attempt should never be negative in practice, but the
        # helper must not misbehave (e.g. IndexError) if it is.
        self.assertEqual(compute_backoff_delay(-1, jitter=0), 1)

    def test_backoff_jitter_is_added_and_bounded(self):
        delay = compute_backoff_delay(0, jitter=0.5)
        self.assertEqual(delay, 1.5)

    def test_backoff_default_jitter_in_expected_range(self):
        for _ in range(20):
            delay = compute_backoff_delay(0)
            self.assertGreaterEqual(delay, 1.0)
            self.assertLess(delay, 2.0)


# ---------------------------------------------------------------------------
# PendingRequests correlation registry
# ---------------------------------------------------------------------------

class TestPendingRequests(unittest.TestCase):
    def test_register_returns_unresolved_future(self):
        registry = PendingRequests()
        fut = registry.register("id-1")
        self.assertFalse(fut.done())
        self.assertEqual(len(registry), 1)

    def test_resolve_sets_result_and_removes_entry(self):
        registry = PendingRequests()
        fut = registry.register("id-1")
        ok = registry.resolve("id-1", {"status": "ok"})
        self.assertTrue(ok)
        self.assertEqual(fut.result(timeout=1), {"status": "ok"})
        self.assertEqual(len(registry), 0)

    def test_resolve_unknown_id_returns_false(self):
        registry = PendingRequests()
        self.assertFalse(registry.resolve("never-registered", {}))

    def test_resolve_none_id_returns_false(self):
        registry = PendingRequests()
        registry.register("id-1")
        self.assertFalse(registry.resolve(None, {}))

    def test_resolve_twice_second_call_returns_false(self):
        registry = PendingRequests()
        registry.register("id-1")
        self.assertTrue(registry.resolve("id-1", {}))
        self.assertFalse(registry.resolve("id-1", {}))

    def test_fail_all_resolves_every_pending_with_none(self):
        registry = PendingRequests()
        f1 = registry.register("id-1")
        f2 = registry.register("id-2")
        registry.fail_all()
        self.assertIsNone(f1.result(timeout=1))
        self.assertIsNone(f2.result(timeout=1))
        self.assertEqual(len(registry), 0)

    def test_discard_removes_without_resolving(self):
        registry = PendingRequests()
        fut = registry.register("id-1")
        registry.discard("id-1")
        self.assertEqual(len(registry), 0)
        self.assertFalse(fut.done())

    def test_concurrent_requests_resolve_independently(self):
        """Multiple in-flight requests (WS-AGENT-PROTOCOL.md section 5, rule 4)
        must resolve to their own response, not cross-talk."""
        registry = PendingRequests()
        f1 = registry.register("id-1")
        f2 = registry.register("id-2")
        registry.resolve("id-2", {"who": "two"})
        registry.resolve("id-1", {"who": "one"})
        self.assertEqual(f1.result(timeout=1), {"who": "one"})
        self.assertEqual(f2.result(timeout=1), {"who": "two"})


# ---------------------------------------------------------------------------
# WSClient frame dispatch (response / error / notification / ping)
# ---------------------------------------------------------------------------

class TestWSClientDispatch(unittest.TestCase):
    def _make_client(self) -> WSClient:
        client = WSClient("PC-TEST-001", "wss://example.invalid/ws/agent")
        client._ws = FakeWebSocket()
        return client

    def test_response_frame_resolves_matching_pending_request(self):
        client = self._make_client()
        fut = client._pending.register("req-1")

        async def scenario():
            await client._dispatch_frame({"frame": "response", "id": "req-1", "payload": {"success": True}})

        _run(scenario())
        self.assertEqual(fut.result(timeout=1), {"success": True})

    def test_error_frame_with_id_resolves_pending_with_none(self):
        client = self._make_client()
        fut = client._pending.register("req-2")

        async def scenario():
            await client._dispatch_frame({
                "frame": "error", "id": "req-2",
                "code": "gateway.timeout", "message": "Backend did not respond"
            })

        _run(scenario())
        self.assertIsNone(fut.result(timeout=1))

    def test_error_frame_with_null_id_does_not_raise(self):
        client = self._make_client()

        async def scenario():
            await client._dispatch_frame({
                "frame": "error", "id": None,
                "code": "auth.not_authenticated", "message": "not authenticated"
            })

        _run(scenario())  # must not raise

    def test_notification_frame_invokes_callback(self):
        client = self._make_client()
        received = []
        client.on_notification(lambda payload: received.append(payload))

        async def scenario():
            await client._dispatch_frame({
                "frame": "notification",
                "topic": "device:PC-TEST-001",
                "payload": {"Type": "AnalysisResult", "DeviceUid": "PC-TEST-001"}
            })

        _run(scenario())
        self.assertEqual(received, [{"Type": "AnalysisResult", "DeviceUid": "PC-TEST-001"}])

    def test_notification_callback_exception_does_not_propagate(self):
        client = self._make_client()

        def bad_callback(payload):
            raise RuntimeError("boom")

        client.on_notification(bad_callback)

        async def scenario():
            await client._dispatch_frame({"frame": "notification", "topic": "t", "payload": {}})

        _run(scenario())  # must not raise

    def test_ping_frame_triggers_pong_response(self):
        client = self._make_client()

        async def scenario():
            await client._dispatch_frame({"frame": "ping", "ts": "2026-08-19T10:15:30.123Z"})

        _run(scenario())
        self.assertEqual(len(client._ws.sent), 1)
        sent = json.loads(client._ws.sent[0])
        self.assertEqual(sent["frame"], "pong")

    def test_unknown_frame_type_is_ignored_without_raising(self):
        client = self._make_client()

        async def scenario():
            await client._dispatch_frame({"frame": "totally_unknown"})

        _run(scenario())  # must not raise


# ---------------------------------------------------------------------------
# Request/response correlation end-to-end (WSClient._do_request)
# ---------------------------------------------------------------------------

class TestWSClientDoRequest(unittest.TestCase):
    def test_do_request_resolves_from_simulated_gateway_response(self):
        """Send a request, then simulate the gateway replying with a
        'response' frame that echoes the generated id — exactly like a real
        WS-AGENT-PROTOCOL.md exchange, minus the network."""
        client = WSClient("PC-TEST-001", "wss://example.invalid/ws/agent")
        client._ws = FakeWebSocket()

        async def scenario():
            client._loop = asyncio.get_running_loop()

            async def fake_gateway():
                # Wait for the request to be sent, then reply.
                while not client._ws.sent:
                    await asyncio.sleep(0.01)
                sent_frame = json.loads(client._ws.sent[0])
                self.assertEqual(sent_frame["frame"], "request")
                await client._dispatch_frame({
                    "frame": "response",
                    "id": sent_frame["id"],
                    "payload": {"status": "TokenCreated", "token": "abc123"},
                })

            gateway_task = asyncio.ensure_future(fake_gateway())
            result = await client._do_request({"MessageType": "RequestToken"}, timeout=2)
            await gateway_task
            return result

        result = _run(scenario())
        self.assertEqual(result, {"status": "TokenCreated", "token": "abc123"})

    def test_do_request_times_out_and_discards_pending(self):
        client = WSClient("PC-TEST-001", "wss://example.invalid/ws/agent")
        client._ws = FakeWebSocket()

        async def scenario():
            client._loop = asyncio.get_running_loop()
            return await client._do_request({"AlertType": "UrlAlert"}, timeout=0.05)

        result = _run(scenario())
        self.assertIsNone(result)
        self.assertEqual(len(client._pending), 0)

    def test_do_request_returns_none_when_send_fails(self):
        client = WSClient("PC-TEST-001", "wss://example.invalid/ws/agent")
        client._ws = None  # not connected -> _send_frame_async returns False

        async def scenario():
            client._loop = asyncio.get_running_loop()
            return await client._do_request({"AlertType": "UrlAlert"})

        result = _run(scenario())
        self.assertIsNone(result)


# ---------------------------------------------------------------------------
# Authentication state tracking + auto-subscribe (real background thread —
# mirrors production wiring: caller thread blocks on a future resolved by
# the WSClient's own event-loop thread).
# ---------------------------------------------------------------------------

class _BareLoopThread:
    """Runs a bare asyncio event loop on a background thread — without
    WSClient's real _run() reconnect logic — so tests can exercise code
    that schedules work via asyncio.run_coroutine_threadsafe(...), exactly
    as production callers do from outside the WSClient's own thread."""

    def __init__(self):
        self.loop = asyncio.new_event_loop()
        self.thread = threading.Thread(target=self._run_forever, daemon=True)
        self.thread.start()
        # Wait until the loop is actually running before returning control.
        deadline = time.time() + 2
        while not self.loop.is_running() and time.time() < deadline:
            time.sleep(0.005)

    def _run_forever(self):
        asyncio.set_event_loop(self.loop)
        self.loop.run_forever()

    def stop(self):
        self.loop.call_soon_threadsafe(self.loop.stop)
        self.thread.join(timeout=2)
        self.loop.close()


class TestWSClientAuthStateAndSubscribe(unittest.TestCase):
    def setUp(self):
        self.bg = _BareLoopThread()

    def tearDown(self):
        self.bg.stop()

    def _make_connected_client(self) -> WSClient:
        client = WSClient("PC-TEST-001", "wss://example.invalid/ws/agent")
        client._loop = self.bg.loop
        client._ws = FakeWebSocket()
        return client

    def test_successful_request_token_marks_authenticated_and_subscribes(self):
        client = self._make_connected_client()

        # Patch send_alert (the blocking cross-thread sender) so the test
        # doesn't need a real request/response round trip — we're testing
        # the *auth-state* behavior of send_request_token, not _do_request
        # (covered separately above).
        client.send_alert = MagicMock(return_value={
            "status": "TokenCreated", "token": "abc123", "expiration": "2099-01-01T00:00:00Z"
        })

        response = client.send_request_token("PC-TEST-001", "john@example.com")

        self.assertEqual(response["status"], "TokenCreated")
        self.assertTrue(client._authenticated)
        self.assertEqual(client._subscribed_device, "PC-TEST-001")
        self.assertEqual(len(client._ws.sent), 1)
        sent = json.loads(client._ws.sent[0])
        self.assertEqual(sent, {"frame": "subscribe", "deviceUid": "PC-TEST-001"})

    def test_failed_request_token_does_not_subscribe(self):
        client = self._make_connected_client()
        client.send_alert = MagicMock(return_value={"status": "DeviceNotRecognized"})

        client.send_request_token("PC-TEST-001", "john@example.com")

        self.assertFalse(client._authenticated)
        self.assertIsNone(client._subscribed_device)
        self.assertEqual(client._ws.sent, [])

    def test_no_response_does_not_subscribe_or_raise(self):
        client = self._make_connected_client()
        client.send_alert = MagicMock(return_value=None)

        response = client.send_request_token("PC-TEST-001")

        self.assertIsNone(response)
        self.assertFalse(client._authenticated)

    def test_request_token_remembers_payload_for_reconnect_replay(self):
        client = self._make_connected_client()
        client.send_alert = MagicMock(return_value={"status": "TokenCreated", "token": "t"})

        client.send_request_token("PC-TEST-001", "john@example.com")

        self.assertEqual(client._last_auth_payload, {
            "MessageType": "RequestToken", "DeviceUid": "PC-TEST-001", "Email": "john@example.com",
            "SupportedSchemaMajors": [1],
        })

    def test_refresh_token_success_also_marks_authenticated_and_subscribes(self):
        client = self._make_connected_client()
        client.send_alert = MagicMock(return_value={"status": "TokenRefreshed", "token": "new-token"})

        client.send_refresh_token("PC-TEST-001", "old-token")

        self.assertTrue(client._authenticated)
        self.assertEqual(client._subscribed_device, "PC-TEST-001")

    def test_on_reconnected_replays_auth_and_resubscribes(self):
        """WS-AGENT-PROTOCOL.md section 8: after reconnect, 'Authenticate ...
        Subscribe' must happen automatically before resuming alert traffic."""
        client = self._make_connected_client()
        client._last_auth_payload = {"MessageType": "RequestToken", "DeviceUid": "PC-TEST-001", "Email": ""}
        client._subscribed_device = "PC-TEST-001"

        async def scenario():
            async def fake_gateway():
                while not client._ws.sent:
                    await asyncio.sleep(0.01)
                sent_frame = json.loads(client._ws.sent[0])
                await client._dispatch_frame({
                    "frame": "response", "id": sent_frame["id"],
                    "payload": {"status": "TokenCreated", "token": "replayed"}
                })

            gateway_task = asyncio.ensure_future(fake_gateway())
            await client._on_reconnected()
            await gateway_task

        asyncio.run_coroutine_threadsafe(scenario(), self.bg.loop).result(timeout=5)

        self.assertTrue(client._authenticated)
        # Frame 0: replayed RequestToken request. Frame 1: subscribe.
        frames = [json.loads(s) for s in client._ws.sent]
        self.assertEqual(frames[0]["frame"], "request")
        self.assertEqual(frames[0]["payload"]["MessageType"], "RequestToken")
        self.assertEqual(frames[1], {"frame": "subscribe", "deviceUid": "PC-TEST-001"})

    def test_on_reconnected_is_noop_on_first_connection(self):
        """No prior auth attempt -> nothing to replay, no frames sent."""
        client = self._make_connected_client()

        asyncio.run_coroutine_threadsafe(client._on_reconnected(), self.bg.loop).result(timeout=5)

        self.assertFalse(client._authenticated)
        self.assertEqual(client._ws.sent, [])


# ---------------------------------------------------------------------------
# connect() / destroy() / is_running — lifecycle contract without real network
# ---------------------------------------------------------------------------

class TestWSClientLifecycle(unittest.TestCase):
    def test_connect_returns_true_immediately_when_already_connected(self):
        client = WSClient("PC-1", "wss://example.invalid/ws/agent")
        client._ensure_loop_running = MagicMock()  # do not spawn a real thread
        client._connected_event.set()
        client._ws = object()

        self.assertTrue(client.connect())
        client._ensure_loop_running.assert_called_once()

    def test_connect_returns_false_on_timeout_when_never_connects(self):
        client = WSClient("PC-1", "wss://example.invalid/ws/agent")
        client._ensure_loop_running = MagicMock()  # do not spawn a real thread

        original_timeout = ws_client.CONNECT_TIMEOUT_SECONDS
        ws_client.CONNECT_TIMEOUT_SECONDS = 0.05
        try:
            self.assertFalse(client.connect())
        finally:
            ws_client.CONNECT_TIMEOUT_SECONDS = original_timeout

    def test_close_is_a_no_op(self):
        client = WSClient("PC-1", "wss://example.invalid/ws/agent")
        client._ws = "sentinel"
        client.close()
        self.assertEqual(client._ws, "sentinel")  # unchanged — persistent connection

    def test_destroy_without_ever_connecting_does_not_raise(self):
        client = WSClient("PC-1", "wss://example.invalid/ws/agent")
        client.destroy()  # must not raise even though nothing was started
        self.assertFalse(client.is_running)

    def test_set_and_clear_server_public_key_are_interface_noops(self):
        client = WSClient("PC-1", "wss://example.invalid/ws/agent")
        client.set_server_public_key(b"some-curve-key")
        self.assertEqual(client.server_public_key, b"some-curve-key")
        client.clear_server_public_key()
        self.assertIsNone(client.server_public_key)

    def test_on_notification_connected_disconnected_callbacks_registered(self):
        client = WSClient("PC-1", "wss://example.invalid/ws/agent")
        cb1, cb2, cb3 = MagicMock(), MagicMock(), MagicMock()
        client.on_notification(cb1)
        client.on_connected(cb2)
        client.on_disconnected(cb3)
        self.assertIs(client._on_notification_callback, cb1)
        self.assertIs(client._on_connected_callback, cb2)
        self.assertIs(client._on_disconnected_callback, cb3)


# ---------------------------------------------------------------------------
# Payload parity with alert_builders (WS-AGENT-PROTOCOL.md section 10:
# "Message payloads are identical" between ZMQ and WS transports).
# ---------------------------------------------------------------------------

class TestWSClientPayloadParity(unittest.TestCase):
    """WSClient's send_* methods must build the exact same payload shape
    as alert_builders (and therefore as ZMQClient), only routing it through
    a 'request' frame instead of a bare ZMQ REQ send."""

    def setUp(self):
        # _is_danger_active() lazily imports services.danger_mode, which
        # transitively imports config.py — pulling in this dev machine's
        # local CURVE key file state. That's irrelevant to payload-shape
        # parity, so pin it to a deterministic value for this test class.
        patcher = patch.object(alert_builders, "_is_danger_active", return_value=False)
        patcher.start()
        self.addCleanup(patcher.stop)

    def _capture_alert(self, client: WSClient):
        captured = {}
        client.send_alert = lambda alert: captured.setdefault("alert", alert) or {"success": True}
        return captured

    def test_send_url_alert_payload_matches_alert_builders(self):
        """Every UrlAlert now travels inside a v1 url_scan.request envelope
        (the Azure backend's Messaging:AcceptLegacyV0=false rejects raw
        schemaVersion-less alerts) — the alert payload embedded in
        payload.alert must still match alert_builders byte-for-byte."""
        client = WSClient("PC-1", "wss://example.invalid/ws/agent")
        captured = self._capture_alert(client)

        client.send_url_alert(device_uid="PC-1", url="https://example.com", token="tok",
                               trackers=[], iframes=[], tab_id="42")

        canonical_url = "https://example.com/"  # canonicalize_url adds the root path
        expected = alert_builders.build_url_alert(
            device_uid="PC-1", url=canonical_url, token="tok",
            trackers=[], iframes=[], tab_id="42",
        )
        sent = captured["alert"]
        self.assertEqual(sent["schemaVersion"], "1.0")
        self.assertEqual(sent["messageType"], "url_scan.request")
        self.assertEqual(sent["source"], "desktop")
        self.assertEqual(sent["context"], {
            "deviceId": "PC-1", "tabId": "42", "url": canonical_url,
        })
        inner = sent["payload"]["alert"]
        # AlertId/Timestamp are freshly generated per call — compare everything else.
        for key in ("AlertId", "Timestamp"):
            inner.pop(key, None)
            expected.pop(key, None)
        self.assertEqual(inner, expected)

    def test_send_url_alert_with_no_envelope_still_produces_valid_v1_envelope(self):
        """Regression test for the legacy `url_check` extension message path
        (ExtensionHandler._handle_url_check -> ScanService.check_url ->
        send_url_alert with envelope=None): the wire message must be a
        schema-valid url_scan.request envelope, not a raw alert dict."""
        import generated.messaging.v1.message_envelope as message_envelope

        client = WSClient("PC-1", "wss://example.invalid/ws/agent")
        captured = self._capture_alert(client)

        client.send_url_alert(device_uid="PC-1", url="https://example.com/page",
                               tab_id="")

        sent = captured["alert"]
        self.assertIs(message_envelope.validate_envelope(sent), sent)
        self.assertEqual(sent["context"]["tabId"], "0")  # never null/"" — see alert_builders
        self.assertEqual(sent["payload"]["alert"]["TabId"], "0")
        self.assertEqual(sent["payload"]["alert"]["Url"], sent["context"]["url"])

    def test_send_remote_access_alert_payload_matches_alert_builders(self):
        client = WSClient("PC-1", "wss://example.invalid/ws/agent")
        captured = self._capture_alert(client)

        client.send_remote_access_alert(
            device_uid="PC-1", remote_app="1", running_processes=2,
            connection_url="192.168.1.1", connection_status="1", session_status="1",
            direction="incoming", confidence="high",
        )

        expected = alert_builders.build_remote_access_alert(
            device_uid="PC-1", remote_app="1", running_processes=2,
            connection_url="192.168.1.1", connection_status="1", session_status="1",
            direction="incoming", confidence="high",
        )
        sent = captured["alert"]
        for key in ("AlertId", "Timestamp"):
            sent.pop(key, None)
            expected.pop(key, None)
        self.assertEqual(sent, expected)

    def test_send_request_token_message_matches_alert_builders(self):
        client = WSClient("PC-1", "wss://example.invalid/ws/agent")
        captured = self._capture_alert(client)

        client.send_request_token("PC-1", "john@example.com")

        expected = alert_builders.build_request_token_message("PC-1", "john@example.com")
        self.assertEqual(captured["alert"], expected)


if __name__ == "__main__":
    unittest.main(verbosity=2)

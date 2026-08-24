"""
Tests for ZMQClient.send_url_alert's v1-envelope wrapping (transport parity
with WSClient — see test_ws_client.py's TestWSClientPayloadParity and
docs/architecture/WS-AGENT-PROTOCOL.md section 10: "Message payloads are
identical" between the ZMQ and WS transports).

Context: the Azure backend runs with Messaging:AcceptLegacyV0=false. Any
UrlAlert sent without a schemaVersion envelope is routed to
AlertProcessor.ProcessLegacyAlertAsync and rejected with "Legacy messaging
v0 is disabled". ZMQClient.send_url_alert must therefore always wrap the
alert in a url_scan.request envelope, including the legacy no-envelope call
path used by ExtensionHandler._handle_url_check.
"""

import os
import sys
import unittest
from unittest.mock import patch

SRC_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
if SRC_DIR not in sys.path:
    sys.path.insert(0, SRC_DIR)

import alert_builders
from zmq_client import ZMQClient
import generated.messaging.v1.message_envelope as message_envelope


class TestZMQClientSendUrlAlertEnvelopeWrapping(unittest.TestCase):
    def setUp(self):
        patcher = patch.object(alert_builders, "_is_danger_active", return_value=False)
        patcher.start()
        self.addCleanup(patcher.stop)

    def _make_client(self):
        client = ZMQClient("localhost", 50001)
        client.connect = lambda: True
        client.close = lambda: None
        return client

    def _capture_alert(self, client):
        """Capture the wire message and reply the way a real backend would:
        every envelope-wrapped request is answered with an envelope-shaped
        url_scan.accepted response (AlertProcessor.ProcessEnvelopeAsync),
        never a bare `{"success": True}` legacy-style dict."""
        captured = {}

        def fake_send_alert(alert):
            captured["alert"] = alert
            return {
                "schemaVersion": alert["schemaVersion"],
                "messageId": "11111111-1111-4111-8111-111111111111",
                "correlationId": alert["correlationId"],
                "requestId": alert["requestId"],
                "messageType": "url_scan.accepted",
                "sentAt": alert["sentAt"],
                "source": "backend",
                "context": alert["context"],
                "outcome": None,
                "payload": {"accepted": True},
            }

        client.send_alert = fake_send_alert
        return captured

    def test_send_url_alert_without_envelope_wraps_in_v1_envelope(self):
        client = self._make_client()
        captured = self._capture_alert(client)

        client.send_url_alert(device_uid="PC-1", url="https://example.com", token="tok",
                               trackers=[], iframes=[], tab_id="42")

        sent = captured["alert"]
        self.assertIs(message_envelope.validate_envelope(sent), sent)
        self.assertEqual(sent["messageType"], "url_scan.request")
        self.assertEqual(sent["source"], "desktop")
        canonical_url = "https://example.com/"
        self.assertEqual(sent["context"], {
            "deviceId": "PC-1", "tabId": "42", "url": canonical_url,
        })
        inner = sent["payload"]["alert"]
        expected = alert_builders.build_url_alert(
            device_uid="PC-1", url=canonical_url, token="tok",
            trackers=[], iframes=[], tab_id="42",
        )
        for key in ("AlertId", "Timestamp"):
            inner.pop(key, None)
            expected.pop(key, None)
        self.assertEqual(inner, expected)

    def test_send_url_alert_without_envelope_and_no_tab_id_uses_non_null_sentinel(self):
        client = self._make_client()
        captured = self._capture_alert(client)

        client.send_url_alert(device_uid="PC-1", url="https://example.com/page")

        sent = captured["alert"]
        self.assertEqual(sent["context"]["tabId"], "0")
        self.assertEqual(sent["payload"]["alert"]["TabId"], "0")
        self.assertEqual(sent["payload"]["alert"]["Url"], sent["context"]["url"])

    def test_send_url_alert_with_caller_supplied_envelope_still_uses_it(self):
        """Regression guard: the existing ASPS-611 envelope-supplied path
        (browser extension's url_scan.request) must keep working exactly as
        before -- only the no-envelope path gained wrapping."""
        client = self._make_client()
        captured = self._capture_alert(client)

        envelope = message_envelope.create_envelope(
            "url_scan.request", "extension",
            {"deviceId": None, "tabId": "42", "url": "https://example.com/"},
            {},
        )

        client.send_url_alert(device_uid="PC-1", url="https://example.com/", tab_id="42",
                               envelope=envelope)

        sent = captured["alert"]
        self.assertEqual(sent["requestId"], envelope["requestId"])
        self.assertEqual(sent["correlationId"], envelope["correlationId"])
        self.assertEqual(sent["context"]["deviceId"], "PC-1")

    def test_response_validation_runs_even_without_a_caller_supplied_envelope(self):
        """validate_url_alert_envelope_response must be exercised for the
        synthesized envelope too, not only when the caller supplied one."""
        client = self._make_client()

        wire_holder = {}

        def fake_send_alert(alert):
            wire_holder["wire"] = alert
            return {
                "schemaVersion": "1.0",
                "messageId": "11111111-1111-4111-8111-111111111111",
                "correlationId": alert["correlationId"],
                "requestId": alert["requestId"],
                "messageType": "url_scan.accepted",
                "sentAt": alert["sentAt"],
                "source": "backend",
                "context": alert["context"],
                "outcome": None,
                "payload": {"accepted": True},
            }

        client.send_alert = fake_send_alert

        response = client.send_url_alert(device_uid="PC-1", url="https://example.com/", tab_id="9")

        self.assertEqual(response["messageType"], "url_scan.accepted")
        self.assertEqual(response["context"], wire_holder["wire"]["context"])

    def test_mismatched_response_context_raises(self):
        """If a (buggy or malicious) response echoes back a different
        context than the request, validate_url_alert_envelope_response must
        reject it -- proving the no-envelope path is validated, not skipped."""
        client = self._make_client()

        def fake_send_alert(alert):
            tampered_context = dict(alert["context"])
            tampered_context["url"] = "https://attacker.example/"
            return {
                "schemaVersion": "1.0",
                "messageId": "11111111-1111-4111-8111-111111111111",
                "correlationId": alert["correlationId"],
                "requestId": alert["requestId"],
                "messageType": "url_scan.accepted",
                "sentAt": alert["sentAt"],
                "source": "backend",
                "context": tampered_context,
                "outcome": None,
                "payload": {"accepted": True},
            }

        client.send_alert = fake_send_alert

        with self.assertRaises(ValueError):
            client.send_url_alert(device_uid="PC-1", url="https://example.com/", tab_id="9")


if __name__ == "__main__":
    unittest.main(verbosity=2)

import copy
import sys
import threading
import types
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
DESKTOP_SRC = ROOT / "apps/desktop/win/src"
sys.path.insert(0, str(DESKTOP_SRC))
sys.modules.setdefault("zmq", types.SimpleNamespace(
    Again=type("Again", (Exception,), {}),
    ZMQError=type("ZMQError", (Exception,), {}),
    Context=object,
    REQ=0,
))

from generated.messaging.v1.message_envelope import create_envelope
from zmq_client import ZMQClient


class FakeZmqClient(ZMQClient):
    def __init__(self):
        self.server_public_key = None
        self._send_lock = threading.Lock()
        self.sent = None
        self.response_mutator = lambda value: value

    def connect(self):
        self.socket = object()
        return True

    def close(self):
        self.socket = None

    def send_alert(self, alert):
        self.sent = alert
        response = create_envelope(
            "url_scan.accepted",
            "backend",
            alert["context"],
            {"accepted": True},
            request_id=alert["requestId"],
            correlation_id=alert["correlationId"],
        )
        return self.response_mutator(response)


class DesktopV1RuntimeTests(unittest.TestCase):
    def request(self):
        return create_envelope(
            "url_scan.request",
            "extension",
            {"deviceId": None, "tabId": "12", "url": "https://example.com/"},
            {},
        )

    def test_extension_to_backend_preserves_request_and_correlation(self):
        client = FakeZmqClient()
        request = self.request()

        response = client.send_url_alert(
            "device-1", "https://example.com/", tab_id="12", envelope=request)

        self.assertEqual(request["requestId"], client.sent["requestId"])
        self.assertEqual(request["correlationId"], client.sent["correlationId"])
        self.assertNotEqual(request["messageId"], client.sent["messageId"])
        self.assertEqual("device-1", client.sent["context"]["deviceId"])
        self.assertEqual(request["requestId"], response["requestId"])

    def test_backend_context_tampering_is_rejected(self):
        client = FakeZmqClient()

        def tamper(response):
            changed = copy.deepcopy(response)
            changed["context"]["tabId"] = "99"
            return changed

        client.response_mutator = tamper
        with self.assertRaisesRegex(ValueError, "immutable_context_mismatch"):
            client.send_url_alert(
                "device-1", "https://example.com/", tab_id="12",
                envelope=self.request())


if __name__ == "__main__":
    unittest.main()

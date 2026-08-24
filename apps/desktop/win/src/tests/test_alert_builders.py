"""
Tests for alert_builders.py's v1-envelope wrapping helpers.

Covers the fix for: the Azure backend runs with Messaging:AcceptLegacyV0=
false, so any UrlAlert sent without a schemaVersion envelope is routed to
AlertProcessor.ProcessLegacyAlertAsync and rejected with "Legacy messaging
v0 is disabled". ZMQClient.send_url_alert / WSClient.send_url_alert must
therefore ALWAYS wrap the alert in a url_scan.request envelope -- including
the legacy no-envelope call path (e.g. ExtensionHandler._handle_url_check,
which forwards the extension's raw `type: 'url_check'` message).

wrap_url_alert_default_envelope() is the helper that builds a synthetic
envelope for that no-envelope case and reuses wrap_url_alert_envelope() to
apply it -- exactly like the envelope-supplied path.
"""

import os
import sys
import unittest
from unittest.mock import patch

SRC_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
if SRC_DIR not in sys.path:
    sys.path.insert(0, SRC_DIR)

import alert_builders
from alert_builders import (
    build_url_alert,
    wrap_url_alert_envelope,
    wrap_url_alert_default_envelope,
    build_track_url_alert,
    wrap_track_url_alert_default_envelope,
    build_tab_closed_alert,
    wrap_tab_closed_alert_default_envelope,
    build_tab_changed_alert,
    wrap_tab_changed_alert_default_envelope,
    build_remote_access_alert,
    wrap_remote_access_alert_default_envelope,
)
from generated.messaging.v1.message_envelope import validate_envelope, canonicalize_url


class TestWrapUrlAlertDefaultEnvelope(unittest.TestCase):
    def setUp(self):
        # Danger-mode lookup is irrelevant to envelope-shape tests; pin it.
        patcher = patch.object(alert_builders, "_is_danger_active", return_value=False)
        patcher.start()
        self.addCleanup(patcher.stop)

    def test_produces_a_schema_valid_url_scan_request_envelope(self):
        alert = build_url_alert(device_uid="PC-1", url="https://example.com/", tab_id="42")

        wire_message, context = wrap_url_alert_default_envelope(
            alert, device_uid="PC-1", url="https://example.com/", tab_id="42")

        # Must not raise -- this is exactly what the backend's
        # MessageEnvelopeValidator equivalent (Python-side mirror) enforces.
        self.assertIs(validate_envelope(wire_message), wire_message)
        self.assertEqual(wire_message["messageType"], "url_scan.request")
        self.assertEqual(wire_message["source"], "desktop")
        self.assertIsNone(wire_message["outcome"])
        self.assertEqual(context, {"deviceId": "PC-1", "tabId": "42", "url": "https://example.com/"})
        self.assertEqual(wire_message["context"], context)
        self.assertEqual(wire_message["payload"]["alert"], alert)

    def test_canonicalizes_a_non_canonical_url_and_keeps_alert_url_in_sync(self):
        """context.url must be canonical (validate_envelope enforces this),
        and the wire alert's Url must match it exactly -- the backend's
        immutable-context check (AlertProcessor.ProcessEnvelopeAsync) compares
        the two with an ordinal string equality and rejects any mismatch."""
        raw_url = "https://EXAMPLE.com"  # missing trailing slash, mixed case
        alert = build_url_alert(device_uid="PC-1", url=raw_url, tab_id="7")

        wire_message, context = wrap_url_alert_default_envelope(
            alert, device_uid="PC-1", url=raw_url, tab_id="7")

        canonical = canonicalize_url(raw_url)
        self.assertEqual(canonical, "https://example.com/")
        self.assertEqual(context["url"], canonical)
        self.assertEqual(wire_message["payload"]["alert"]["Url"], canonical)

    def test_empty_tab_id_normalizes_to_non_null_sentinel_on_both_sides(self):
        """tabId must never be None/"" in the synthetic envelope: the
        backend compares the wire alert's TabId (via JToken.ToString(), which
        renders a JSON null as "") against the typed envelope.Context.TabId
        (a real C# null for a JSON null) -- "" != null, so a null tabId would
        fail the immutable-context check on every request. Both sides must
        carry the same non-null decimal string."""
        alert = build_url_alert(device_uid="PC-1", url="https://example.com/", tab_id="")

        wire_message, context = wrap_url_alert_default_envelope(
            alert, device_uid="PC-1", url="https://example.com/", tab_id="")

        self.assertEqual(context["tabId"], "0")
        self.assertEqual(wire_message["payload"]["alert"]["TabId"], "0")
        self.assertTrue(context["tabId"].isdigit())

    def test_device_id_is_stamped_into_context(self):
        alert = build_url_alert(device_uid="PC-1", url="https://example.com/", tab_id="1")

        _, context = wrap_url_alert_default_envelope(
            alert, device_uid="PC-1", url="https://example.com/", tab_id="1")

        self.assertEqual(context["deviceId"], "PC-1")

    def test_response_can_be_validated_against_the_returned_context(self):
        """Simulates the backend echoing Context back on url_scan.accepted --
        confirms wrap_url_alert_default_envelope's output is usable with
        validate_url_alert_envelope_response exactly like the
        envelope-supplied path."""
        from alert_builders import validate_url_alert_envelope_response

        alert = build_url_alert(device_uid="PC-1", url="https://example.com/", tab_id="5")
        wire_message, context = wrap_url_alert_default_envelope(
            alert, device_uid="PC-1", url="https://example.com/", tab_id="5")

        response = {
            "schemaVersion": "1.0",
            "messageId": wire_message["messageId"],
            "correlationId": wire_message["correlationId"],
            "requestId": wire_message["requestId"],
            "messageType": "url_scan.accepted",
            "sentAt": wire_message["sentAt"],
            "source": "backend",
            "context": context,
            "outcome": None,
            "payload": {"accepted": True},
        }

        # Must not raise.
        validate_url_alert_envelope_response(response, wire_message, context)


class TestWrapTrackUrlAlertDefaultEnvelope(unittest.TestCase):
    """ASPS-732: TrackUrlAlert must travel inside a track_url.request v1
    envelope for the same reason UrlAlert does -- the Azure backend rejects
    any schemaVersion-less message when AcceptLegacyV0=false."""

    def setUp(self):
        patcher = patch.object(alert_builders, "_is_danger_active", return_value=False)
        patcher.start()
        self.addCleanup(patcher.stop)

    def test_produces_a_schema_valid_track_url_request_envelope(self):
        alert = build_track_url_alert(device_uid="PC-1", url="https://example.com/", tab_id="42")

        wire_message, context = wrap_track_url_alert_default_envelope(
            alert, device_uid="PC-1", url="https://example.com/", tab_id="42")

        self.assertIs(validate_envelope(wire_message), wire_message)
        self.assertEqual(wire_message["messageType"], "track_url.request")
        self.assertEqual(wire_message["source"], "desktop")
        self.assertIsNone(wire_message["outcome"])
        self.assertEqual(context, {"deviceId": "PC-1", "tabId": "42", "url": "https://example.com/"})
        self.assertEqual(wire_message["context"], context)
        self.assertEqual(wire_message["payload"]["alert"], alert)

    def test_canonicalizes_url_and_keeps_alert_url_in_sync(self):
        raw_url = "https://EXAMPLE.com"
        alert = build_track_url_alert(device_uid="PC-1", url=raw_url, tab_id="7")

        wire_message, context = wrap_track_url_alert_default_envelope(
            alert, device_uid="PC-1", url=raw_url, tab_id="7")

        canonical = canonicalize_url(raw_url)
        self.assertEqual(context["url"], canonical)
        self.assertEqual(wire_message["payload"]["alert"]["Url"], canonical)

    def test_empty_tab_id_normalizes_to_zero_sentinel(self):
        alert = build_track_url_alert(device_uid="PC-1", url="https://example.com/", tab_id="")

        wire_message, context = wrap_track_url_alert_default_envelope(
            alert, device_uid="PC-1", url="https://example.com/", tab_id="")

        self.assertEqual(context["tabId"], "0")
        self.assertEqual(wire_message["payload"]["alert"]["TabId"], "0")

    def test_device_id_is_stamped_into_context(self):
        alert = build_track_url_alert(device_uid="PC-1", url="https://example.com/", tab_id="1")

        _, context = wrap_track_url_alert_default_envelope(
            alert, device_uid="PC-1", url="https://example.com/", tab_id="1")

        self.assertEqual(context["deviceId"], "PC-1")


class TestWrapTabClosedAlertDefaultEnvelope(unittest.TestCase):
    """ASPS-732: TabClosedAlert must travel inside a tab_closed.request
    v1 envelope."""

    def setUp(self):
        patcher = patch.object(alert_builders, "_is_danger_active", return_value=False)
        patcher.start()
        self.addCleanup(patcher.stop)

    def test_produces_a_schema_valid_tab_closed_request_envelope(self):
        alert = build_tab_closed_alert(device_uid="PC-1", tab_id="42", url="https://example.com/")

        wire_message, context = wrap_tab_closed_alert_default_envelope(
            alert, device_uid="PC-1", url="https://example.com/", tab_id="42")

        self.assertIs(validate_envelope(wire_message), wire_message)
        self.assertEqual(wire_message["messageType"], "tab_closed.request")
        self.assertEqual(wire_message["source"], "desktop")
        self.assertIsNone(wire_message["outcome"])
        self.assertEqual(context, {"deviceId": "PC-1", "tabId": "42", "url": "https://example.com/"})
        self.assertEqual(wire_message["payload"]["alert"], alert)

    def test_canonicalizes_url_and_keeps_alert_url_in_sync(self):
        raw_url = "https://EXAMPLE.com"
        alert = build_tab_closed_alert(device_uid="PC-1", tab_id="7", url=raw_url)

        wire_message, context = wrap_tab_closed_alert_default_envelope(
            alert, device_uid="PC-1", url=raw_url, tab_id="7")

        canonical = canonicalize_url(raw_url)
        self.assertEqual(context["url"], canonical)
        self.assertEqual(wire_message["payload"]["alert"]["Url"], canonical)

    def test_empty_tab_id_normalizes_to_zero_sentinel(self):
        alert = build_tab_closed_alert(device_uid="PC-1", tab_id="", url="https://example.com/")

        wire_message, context = wrap_tab_closed_alert_default_envelope(
            alert, device_uid="PC-1", url="https://example.com/", tab_id="")

        self.assertEqual(context["tabId"], "0")
        self.assertEqual(wire_message["payload"]["alert"]["TabId"], "0")


class TestWrapTabChangedAlertDefaultEnvelope(unittest.TestCase):
    """ASPS-732: TabChangedAlert must travel inside a tab_changed.request
    v1 envelope."""

    def setUp(self):
        patcher = patch.object(alert_builders, "_is_danger_active", return_value=False)
        patcher.start()
        self.addCleanup(patcher.stop)

    def test_produces_a_schema_valid_tab_changed_request_envelope(self):
        alert = build_tab_changed_alert(device_uid="PC-1", tab_id="42", url="https://example.com/")

        wire_message, context = wrap_tab_changed_alert_default_envelope(
            alert, device_uid="PC-1", url="https://example.com/", tab_id="42")

        self.assertIs(validate_envelope(wire_message), wire_message)
        self.assertEqual(wire_message["messageType"], "tab_changed.request")
        self.assertEqual(wire_message["source"], "desktop")
        self.assertIsNone(wire_message["outcome"])
        self.assertEqual(context, {"deviceId": "PC-1", "tabId": "42", "url": "https://example.com/"})
        self.assertEqual(wire_message["payload"]["alert"], alert)

    def test_canonicalizes_url_and_keeps_alert_url_in_sync(self):
        raw_url = "https://EXAMPLE.com"
        alert = build_tab_changed_alert(device_uid="PC-1", tab_id="7", url=raw_url)

        wire_message, context = wrap_tab_changed_alert_default_envelope(
            alert, device_uid="PC-1", url=raw_url, tab_id="7")

        canonical = canonicalize_url(raw_url)
        self.assertEqual(context["url"], canonical)
        self.assertEqual(wire_message["payload"]["alert"]["Url"], canonical)

    def test_empty_tab_id_normalizes_to_zero_sentinel(self):
        alert = build_tab_changed_alert(device_uid="PC-1", tab_id="", url="https://example.com/")

        wire_message, context = wrap_tab_changed_alert_default_envelope(
            alert, device_uid="PC-1", url="https://example.com/", tab_id="")

        self.assertEqual(context["tabId"], "0")
        self.assertEqual(wire_message["payload"]["alert"]["TabId"], "0")


class TestWrapRemoteAccessAlertDefaultEnvelope(unittest.TestCase):
    """ASPS-732: RemoteAccessAlert must travel inside a remote_access.request
    v1 envelope. Unlike the other alert types, RemoteAccessAlert has no Url
    or TabId field -- context.url is derived from ConnectionUrl (prefixed
    with https:// when it has no scheme, e.g. a bare IP) or a fixed
    sentinel when ConnectionUrl is absent; context.tabId is always "0"."""

    def setUp(self):
        patcher = patch.object(alert_builders, "_is_danger_active", return_value=False)
        patcher.start()
        self.addCleanup(patcher.stop)

    def test_produces_a_schema_valid_remote_access_request_envelope(self):
        alert = build_remote_access_alert(
            device_uid="PC-1", remote_app="1", running_processes=2,
            connection_url="192.168.1.1", connection_status="1", session_status="1")

        wire_message, context = wrap_remote_access_alert_default_envelope(alert, device_uid="PC-1")

        self.assertIs(validate_envelope(wire_message), wire_message)
        self.assertEqual(wire_message["messageType"], "remote_access.request")
        self.assertEqual(wire_message["source"], "desktop")
        self.assertIsNone(wire_message["outcome"])
        self.assertEqual(wire_message["payload"]["alert"], alert)

    def test_bare_ip_connection_url_gets_https_scheme_prepended(self):
        alert = build_remote_access_alert(
            device_uid="PC-1", remote_app="1", running_processes=2,
            connection_url="192.168.1.1", connection_status="1", session_status="1")

        _, context = wrap_remote_access_alert_default_envelope(alert, device_uid="PC-1")

        self.assertEqual(context["url"], "https://192.168.1.1/")

    def test_missing_connection_url_falls_back_to_sentinel(self):
        alert = build_remote_access_alert(
            device_uid="PC-1", remote_app="1", running_processes=2,
            connection_url="", connection_status="1", session_status="1")

        _, context = wrap_remote_access_alert_default_envelope(alert, device_uid="PC-1")

        self.assertEqual(context["url"], "https://remote-access.internal/")

    def test_tab_id_is_always_zero_sentinel(self):
        alert = build_remote_access_alert(
            device_uid="PC-1", remote_app="1", running_processes=2,
            connection_url="192.168.1.1", connection_status="1", session_status="1")

        _, context = wrap_remote_access_alert_default_envelope(alert, device_uid="PC-1")

        self.assertEqual(context["tabId"], "0")

    def test_device_id_is_stamped_into_context(self):
        alert = build_remote_access_alert(
            device_uid="PC-1", remote_app="1", running_processes=2,
            connection_url="192.168.1.1", connection_status="1", session_status="1")

        _, context = wrap_remote_access_alert_default_envelope(alert, device_uid="PC-1")

        self.assertEqual(context["deviceId"], "PC-1")


if __name__ == "__main__":
    unittest.main(verbosity=2)

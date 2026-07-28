"""Cross-source Extension -> browser-history delivery-state regressions."""

import os
import sys
import tempfile
import unittest
from datetime import datetime
from pathlib import Path
from unittest.mock import Mock, patch

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from browser_history import BrowserHistoryMonitor, HistoryEntry
from services.scan_service import ScanService


class TestScanServiceDeliveryState(unittest.TestCase):
    def setUp(self):
        ScanService.clear_pending_url()
        self.temp_dir = tempfile.TemporaryDirectory()
        self.state_file = Path(self.temp_dir.name) / 'delivery.json'
        self.browser = BrowserHistoryMonitor(state_file=self.state_file)
        self.cache = Mock()
        self.cache.get.return_value = None
        self.auth = Mock()
        self.auth.is_valid.return_value = True
        self.auth.get_token.return_value = 'token'
        self.auth.handle_auth_response.return_value = False
        self.zmq = Mock()
        self.event_logger = Mock()
        self.service = ScanService(
            cache=self.cache,
            zmq_client=self.zmq,
            auth_manager=self.auth,
            browser_monitor=self.browser,
            event_logger=self.event_logger,
            device_id='device-1',
        )
        self.url = 'https://cross-source.example.com/path'

    def tearDown(self):
        ScanService.clear_pending_url()
        self.temp_dir.cleanup()

    def test_authentication_loss_keeps_extension_url_queued_and_unseen(self):
        self.auth.is_valid.return_value = False
        self.auth.authenticate.return_value = False

        result = self.service.check_url(self.url)

        self.assertTrue(result['error'])
        self.zmq.send_url_alert.assert_not_called()
        self.assertFalse(self.browser.is_url_seen(self.url))
        self.assertEqual(
            self.browser.get_delivery_state(self.url, 'extension'),
            'queued',
        )

    def test_timeout_marks_failed_and_restart_returns_retry_candidate(self):
        self.zmq.send_url_alert.return_value = None

        self.service.check_url(self.url)

        self.assertEqual(
            self.browser.get_delivery_state(self.url, 'extension'),
            'failed',
        )
        restarted = BrowserHistoryMonitor(state_file=self.state_file)
        with patch.object(restarted, 'get_all_history', return_value=[]):
            candidates = restarted.get_new_entries()
        self.assertEqual([entry.url for entry in candidates], [self.url])
        self.assertFalse(restarted.is_url_seen(self.url))

    def test_explicit_rejection_marks_failed_and_remains_retriable(self):
        self.zmq.send_url_alert.return_value = {
            'success': False,
            'message': 'Rejected',
        }

        result = self.service.check_url(self.url)

        self.assertTrue(result['error'])
        self.assertEqual(
            self.browser.get_delivery_state(self.url, 'extension'),
            'failed',
        )

    def _assert_ambiguous_response_fails_closed(self, response):
        self.zmq.send_url_alert.return_value = response

        result = self.service.check_url(self.url)

        self.assertTrue(result['error'])
        self.assertEqual(
            self.browser.get_delivery_state(self.url, 'extension'),
            'failed',
        )
        self.assertFalse(self.browser.is_url_seen(self.url))
        self.assertFalse(ScanService.is_pending(self.url))
        restarted = BrowserHistoryMonitor(state_file=self.state_file)
        with patch.object(restarted, 'get_all_history', return_value=[]):
            candidates = restarted.get_new_entries()
        self.assertEqual([entry.url for entry in candidates], [self.url])

    def test_string_false_success_fails_closed_and_retries_after_restart(self):
        self._assert_ambiguous_response_fails_closed({
            'success': 'false',
            'message': 'Rejected',
        })

    def test_integer_success_fails_closed_and_retries_after_restart(self):
        self._assert_ambiguous_response_fails_closed({
            'success': 1,
            'message': 'Ambiguous',
        })

    def test_legacy_error_cannot_be_overridden_by_result_fields(self):
        self._assert_ambiguous_response_fails_closed({
            'HasError': True,
            'Score': 0,
            'RiskType': [],
            'ProtectiveAction': 0,
            'ErrorMessage': 'Rejected',
        })

    def test_send_exception_marks_failed_instead_of_acknowledged(self):
        self.zmq.send_url_alert.side_effect = RuntimeError('transport failure')

        result = self.service.check_url(self.url)

        self.assertTrue(result['error'])
        self.assertEqual(
            self.browser.get_delivery_state(self.url, 'extension'),
            'failed',
        )
        self.assertFalse(self.browser.is_url_seen(self.url))

    def test_accepted_response_acknowledges_and_suppresses_history_duplicate(self):
        self.zmq.send_url_alert.return_value = {
            'messageType': 'url_scan.accepted',
            'payload': {'success': True},
        }

        self.service.check_url(self.url)

        self.assertTrue(self.browser.is_url_seen(self.url))
        history_entry = HistoryEntry(
            self.url, 'Same URL', datetime.now(), 'chrome'
        )
        with patch.object(self.browser, 'get_all_history', return_value=[history_entry]):
            self.assertEqual(self.browser.get_new_entries(), [])

    def test_cache_hit_acknowledges_previously_queued_url_without_send(self):
        self.browser.queue_url_for_delivery(self.url, 'extension')
        cached = Mock(
            score=10,
            risk_type=[],
            protective_action=0,
        )
        self.cache.get.return_value = cached

        result = self.service.check_url(self.url)

        self.assertTrue(result['cached'])
        self.zmq.send_url_alert.assert_not_called()
        self.assertTrue(self.browser.is_url_seen(self.url))


if __name__ == '__main__':
    unittest.main(verbosity=2)

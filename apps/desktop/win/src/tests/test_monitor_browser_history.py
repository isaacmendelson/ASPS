"""Delivery-state tests for MonitorService browser-history polling."""

import asyncio
import os
import sys
import tempfile
import unittest
from datetime import datetime
from pathlib import Path
from unittest.mock import Mock

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from browser_history import BrowserHistoryMonitor, HistoryEntry
from services.monitor_service import MonitorService


class TestMonitorBrowserHistoryDelivery(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.entry = HistoryEntry(
            'https://history.example.com', 'History', datetime.now(), 'chrome'
        )
        self.browser = BrowserHistoryMonitor(
            state_file=Path(self.temp_dir.name) / 'delivery.json'
        )
        self.browser.get_all_history = Mock(return_value=[self.entry])
        self.auth = Mock()
        self.auth.is_valid.return_value = True
        self.auth.get_token.return_value = 'token'
        self.auth.handle_auth_response.return_value = False
        self.zmq = Mock()
        self.zmq.send_url_alert.return_value = {'success': True}
        self.service = MonitorService(
            remote_monitor=Mock(),
            browser_monitor=self.browser,
            auth_manager=self.auth,
            zmq_client=self.zmq,
            tray=Mock(),
            event_logger=Mock(),
            cache=Mock(has=Mock(return_value=False)),
            device_id='device-1',
        )

    def tearDown(self):
        self.temp_dir.cleanup()

    def run_poll(self):
        asyncio.run(self.service._process_browser_history_once())

    def test_acknowledged_url_is_delivered_once(self):
        self.run_poll()
        self.run_poll()

        self.assertEqual(self.zmq.send_url_alert.call_count, 1)
        self.assertEqual(
            self.browser.get_delivery_state(self.entry.url, self.entry.browser),
            'acknowledged',
        )

    def test_failed_delivery_retries_on_next_poll(self):
        self.zmq.send_url_alert.side_effect = [None, {'success': True}]

        self.run_poll()
        self.assertEqual(
            self.browser.get_delivery_state(self.entry.url, self.entry.browser),
            'failed',
        )
        self.run_poll()

        self.assertEqual(self.zmq.send_url_alert.call_count, 2)
        self.assertTrue(self.browser.is_url_seen(self.entry.url))

    def test_authentication_loss_keeps_url_queued_until_auth_recovers(self):
        self.auth.is_valid.side_effect = [False, True]

        self.run_poll()
        self.zmq.send_url_alert.assert_not_called()
        self.assertEqual(
            self.browser.get_delivery_state(self.entry.url, self.entry.browser),
            'queued',
        )

        self.run_poll()
        self.zmq.send_url_alert.assert_called_once()
        self.assertTrue(self.browser.is_url_seen(self.entry.url))

    def test_authentication_error_without_recovery_marks_delivery_failed(self):
        self.zmq.send_url_alert.return_value = {'status': 'InvalidToken'}

        self.run_poll()

        self.assertEqual(self.zmq.send_url_alert.call_count, 1)
        self.assertEqual(
            self.browser.get_delivery_state(self.entry.url, self.entry.browser),
            'failed',
        )


if __name__ == '__main__':
    unittest.main(verbosity=2)

"""
ASPS-621 — Concurrent scan routing tests.

Verifies:
1. Multiple concurrent pending URLs are tracked independently.
2. Each entry preserves its own tab_id / request_id / correlation_id.
3. Stale entries are evicted; a cleared entry is gone.
4. NotificationHandler._broadcast_to_extension receives the routing fields
   from the pending entry, not from a global "most recent" slot.
5. Tab switching during analysis does not misroute the result.
"""

import asyncio
import os
import sys
import time
import unittest
from unittest.mock import AsyncMock, MagicMock, Mock, patch, call

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from services.scan_service import ScanService
from handlers.notification_handler import NotificationHandler


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _make_scan_service():
    cache = Mock()
    cache.get.return_value = None
    zmq = Mock()
    auth = Mock()
    auth.is_valid.return_value = True
    auth.get_token.return_value = 'tok'
    browser = Mock()
    event_logger = Mock()
    return ScanService(
        cache=cache,
        zmq_client=zmq,
        auth_manager=auth,
        browser_monitor=browser,
        event_logger=event_logger,
        device_id='dev-1',
    )


# ---------------------------------------------------------------------------
# ScanService._pending_urls — concurrent tracking
# ---------------------------------------------------------------------------

class TestScanServiceConcurrentPending(unittest.TestCase):

    def setUp(self):
        ScanService.clear_pending_url()

    def tearDown(self):
        ScanService.clear_pending_url()

    def test_multiple_urls_tracked_independently(self):
        ScanService.set_pending_url('https://a.com/', tab_id='10', request_id='req-a', correlation_id='cor-a')
        ScanService.set_pending_url('https://b.com/', tab_id='20', request_id='req-b', correlation_id='cor-b')
        ScanService.set_pending_url('https://c.com/', tab_id='30', request_id='req-c', correlation_id='cor-c')

        self.assertTrue(ScanService.is_pending('https://a.com/'))
        self.assertTrue(ScanService.is_pending('https://b.com/'))
        self.assertTrue(ScanService.is_pending('https://c.com/'))

    def test_each_entry_preserves_its_own_routing_metadata(self):
        ScanService.set_pending_url('https://a.com/', tab_id='10', request_id='req-a', correlation_id='cor-a')
        ScanService.set_pending_url('https://b.com/', tab_id='20', request_id='req-b', correlation_id='cor-b')

        entry_a = ScanService.get_pending_entry('https://a.com/')
        entry_b = ScanService.get_pending_entry('https://b.com/')

        self.assertEqual(entry_a['tab_id'], '10')
        self.assertEqual(entry_a['request_id'], 'req-a')
        self.assertEqual(entry_a['correlation_id'], 'cor-a')

        self.assertEqual(entry_b['tab_id'], '20')
        self.assertEqual(entry_b['request_id'], 'req-b')
        self.assertEqual(entry_b['correlation_id'], 'cor-b')

    def test_second_scan_does_not_overwrite_first_entry_metadata(self):
        """Adding a second URL must not pollute the first URL's routing data."""
        ScanService.set_pending_url('https://a.com/', tab_id='10', request_id='req-a')
        ScanService.set_pending_url('https://b.com/', tab_id='99', request_id='req-b')

        entry_a = ScanService.get_pending_entry('https://a.com/')
        self.assertEqual(entry_a['tab_id'], '10')
        self.assertEqual(entry_a['request_id'], 'req-a')

    def test_clear_specific_url_leaves_others_intact(self):
        ScanService.set_pending_url('https://a.com/', tab_id='10')
        ScanService.set_pending_url('https://b.com/', tab_id='20')

        ScanService.clear_pending_url('https://a.com/')

        self.assertFalse(ScanService.is_pending('https://a.com/'))
        self.assertTrue(ScanService.is_pending('https://b.com/'))

    def test_clear_all_removes_everything(self):
        ScanService.set_pending_url('https://a.com/', tab_id='10')
        ScanService.set_pending_url('https://b.com/', tab_id='20')

        ScanService.clear_pending_url()

        self.assertFalse(ScanService.is_pending('https://a.com/'))
        self.assertFalse(ScanService.is_pending('https://b.com/'))

    def test_get_pending_entry_returns_none_for_unknown_url(self):
        self.assertIsNone(ScanService.get_pending_entry('https://unknown.com/'))

    def test_get_pending_url_returns_most_recent(self):
        """get_pending_url() backward-compat: returns the most recently added."""
        ScanService.set_pending_url('https://old.com/', tab_id='10')
        # Ensure at least 1 ms separation so timestamps differ.
        time.sleep(0.002)
        ScanService.set_pending_url('https://new.com/', tab_id='20')

        self.assertEqual(ScanService.get_pending_url(), 'https://new.com/')

    def test_stale_entries_are_evicted(self):
        """Entries older than 60 seconds are purged on the next add."""
        # Inject an already-expired entry directly.
        with ScanService._get_lock():
            ScanService._pending_urls['https://old.com/'] = {
                'timestamp': time.time() - 61,
                'tab_id': '5',
                'request_id': '',
                'correlation_id': '',
            }

        # Adding any new URL triggers cleanup.
        ScanService.set_pending_url('https://new.com/', tab_id='99')

        self.assertFalse(ScanService.is_pending('https://old.com/'))
        self.assertTrue(ScanService.is_pending('https://new.com/'))

    def test_check_url_records_routing_metadata_from_envelope(self):
        """ScanService.check_url() propagates envelope fields into pending entry."""
        svc = _make_scan_service()
        envelope = {
            'requestId': 'req-xyz',
            'correlationId': 'cor-xyz',
            'context': {'tabId': '42', 'url': 'https://example.com/', 'deviceId': None},
        }
        svc.zmq_client.send_url_alert.return_value = {'success': True}

        svc.check_url(
            'https://example.com/',
            tab_id='42',
            envelope=envelope,
        )

        entry = ScanService.get_pending_entry('https://example.com/')
        self.assertIsNotNone(entry)
        self.assertEqual(entry['tab_id'], '42')
        self.assertEqual(entry['request_id'], 'req-xyz')
        self.assertEqual(entry['correlation_id'], 'cor-xyz')


# ---------------------------------------------------------------------------
# NotificationHandler routing — broadcast includes correct tab metadata
# ---------------------------------------------------------------------------

class TestNotificationHandlerRouting(unittest.IsolatedAsyncioTestCase):

    def setUp(self):
        ScanService.clear_pending_url()

    def tearDown(self):
        ScanService.clear_pending_url()

    def _build_handler(self):
        protection_svc = Mock()
        protection_svc.execute_actions = Mock()
        protection_svc.get_cache_action = Mock(return_value=0)
        cache = Mock()
        cache.set = Mock()
        ext_server = Mock()
        ext_server.broadcast = AsyncMock()
        handler = NotificationHandler(
            protection_service=protection_svc,
            cache=cache,
            extension_server=ext_server,
        )
        # Inject a running event loop reference directly.
        handler._event_loop = asyncio.get_event_loop()
        return handler, ext_server

    def _notification(self, url):
        return {
            'Data': {
                'AnalysisResult': {
                    'Url': url,
                    'risk_assessment': {'risk_score': 55, 'is_scam': False, 'risk_level': 'medium'},
                },
                'Indicators': [],
                'protectiveActions': [],
            }
        }

    async def test_broadcast_carries_originating_tab_id(self):
        ScanService.set_pending_url('https://target.com/', tab_id='77', request_id='req-1', correlation_id='cor-1')
        handler, ext_server = self._build_handler()

        handler.handle(self._notification('https://target.com/'))
        await asyncio.sleep(0)  # let any scheduled coroutines run

        self.assertTrue(ext_server.broadcast.called)
        sent = ext_server.broadcast.call_args[0][0]
        self.assertEqual(sent['tabId'], '77')
        self.assertEqual(sent['requestId'], 'req-1')
        self.assertEqual(sent['correlationId'], 'cor-1')

    async def test_two_concurrent_scans_route_to_their_own_tabs(self):
        ScanService.set_pending_url('https://tab10.com/', tab_id='10', request_id='req-10', correlation_id='cor-10')
        ScanService.set_pending_url('https://tab20.com/', tab_id='20', request_id='req-20', correlation_id='cor-20')

        handler, ext_server = self._build_handler()

        # First notification arrives for tab 10
        handler.handle(self._notification('https://tab10.com/'))
        await asyncio.sleep(0)
        first_call = ext_server.broadcast.call_args_list[-1][0][0]
        self.assertEqual(first_call['tabId'], '10')

        # Second notification arrives for tab 20
        handler.handle(self._notification('https://tab20.com/'))
        await asyncio.sleep(0)
        second_call = ext_server.broadcast.call_args_list[-1][0][0]
        self.assertEqual(second_call['tabId'], '20')

    async def test_tab_switch_during_analysis_does_not_affect_routing(self):
        """Simulate: tab 5 starts scan, user switches to tab 9, result arrives.
        The broadcast must still carry tab_id=5, not tab_id=9."""
        ScanService.set_pending_url('https://original.com/', tab_id='5', request_id='req-5')

        # Simulate the "user switched to tab 9" by adding tab 9 as the most
        # recent pending URL — which is what the old global-slot bug would have
        # used as the "current" URL.
        ScanService.set_pending_url('https://switched.com/', tab_id='9', request_id='req-9')

        handler, ext_server = self._build_handler()

        # Notification for the original URL (tab 5) arrives.
        handler.handle(self._notification('https://original.com/'))
        await asyncio.sleep(0)

        sent = ext_server.broadcast.call_args_list[-1][0][0]
        self.assertEqual(sent['url'], 'https://original.com/')
        self.assertEqual(sent['tabId'], '5')

    async def test_pending_entry_cleared_after_notification(self):
        ScanService.set_pending_url('https://clear-me.com/', tab_id='3')
        handler, _ = self._build_handler()

        handler.handle(self._notification('https://clear-me.com/'))
        await asyncio.sleep(0)

        self.assertFalse(ScanService.is_pending('https://clear-me.com/'))


if __name__ == '__main__':
    unittest.main(verbosity=2)

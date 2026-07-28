"""
Unit Tests for BrowserHistoryMonitor
Tests browser history reading from SQLite databases (FR-037)
"""

import unittest
from unittest.mock import Mock, patch, MagicMock
import sys
import os
import sqlite3
import tempfile
from contextlib import contextmanager
from datetime import datetime
from pathlib import Path

# Add parent directory to path for imports
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from browser_history import BrowserHistoryMonitor, HistoryEntry


def _patched_temp_db():
    """Return a contextmanager that yields a fake temp DB path without touching disk."""
    @contextmanager
    def _mock(source_path):
        yield '/fake/temp_history.db'
    return _mock


def _make_mock_connection(rows):
    """Build a mock sqlite3 connection/cursor that returns the given rows on fetchall."""
    mock_conn = MagicMock()
    mock_cursor = MagicMock()
    mock_conn.cursor.return_value = mock_cursor
    mock_cursor.fetchall.return_value = rows
    return mock_conn


class TestBrowserHistoryMonitor(unittest.TestCase):
    """Test suite for BrowserHistoryMonitor"""

    def setUp(self):
        """Set up test fixtures."""
        self.temp_dir = tempfile.TemporaryDirectory()
        self.state_file = Path(self.temp_dir.name) / 'browser-history-delivery.json'
        self.monitor = BrowserHistoryMonitor(state_file=self.state_file)

    def tearDown(self):
        self.temp_dir.cleanup()

    #region Constructor / instantiation

    def test_monitor_instantiates_correctly(self):
        """BrowserHistoryMonitor should initialise with empty dedup state."""
        self.assertIsInstance(self.monitor._seen_urls, set)
        self.assertIsInstance(self.monitor._last_check, dict)
        self.assertEqual(len(self.monitor._seen_urls), 0)

    #endregion

    #region History path tests

    def test_chrome_and_edge_paths_are_different(self):
        """Chrome and Edge history paths must point to different files."""
        chrome_path = self.monitor._get_history_path('chrome')
        edge_path = self.monitor._get_history_path('edge')
        self.assertIsNotNone(chrome_path)
        self.assertIsNotNone(edge_path)
        self.assertNotEqual(chrome_path, edge_path)

    def test_firefox_path_differs_from_chrome_path(self):
        """Firefox profile directory must differ from the Chrome history path."""
        chrome_path = self.monitor._get_history_path('chrome')
        firefox_path = self.monitor._get_history_path('firefox')
        self.assertIsNotNone(chrome_path)
        self.assertIsNotNone(firefox_path)
        self.assertNotEqual(chrome_path, firefox_path)

    def test_unknown_browser_returns_none(self):
        """_get_history_path should return None for an unrecognised browser key."""
        result = self.monitor._get_history_path('opera_gx_unknown')
        self.assertIsNone(result)

    #endregion

    #region Chrome — happy path

    def test_get_chrome_history_returns_history_entries(self):
        """get_chrome_history returns a list of HistoryEntry with correct field values."""
        # Chrome stores last_visit_time as µs since 1601-01-01
        chrome_ts = 13_300_000_000_000_000
        mock_conn = _make_mock_connection([
            ('https://example.com', 'Example Site', 3, chrome_ts),
        ])

        with patch('browser_history.os.path.exists', return_value=True), \
             patch('browser_history.temp_database_copy', _patched_temp_db()), \
             patch('browser_history.sqlite3.connect', return_value=mock_conn):
            entries = self.monitor.get_chrome_history()

        self.assertEqual(len(entries), 1)
        entry = entries[0]
        self.assertEqual(entry.url, 'https://example.com')
        self.assertEqual(entry.title, 'Example Site')
        self.assertEqual(entry.browser, 'chrome')
        self.assertIsInstance(entry.visit_time, datetime)

    def test_get_chrome_history_uses_temp_copy_for_file_lock(self):
        """get_chrome_history copies the locked SQLite DB to a temp file before reading."""
        mock_conn = _make_mock_connection([])

        with patch('browser_history.os.path.exists', return_value=True), \
             patch('browser_history.tempfile.mkstemp', return_value=(0, '/fake/tmp.db')), \
             patch('browser_history.os.close'), \
             patch('browser_history.shutil.copy2') as mock_copy, \
             patch('browser_history.os.remove'), \
             patch('browser_history.sqlite3.connect', return_value=mock_conn):
            self.monitor.get_chrome_history()

        mock_copy.assert_called_once()

    def test_get_chrome_history_multiple_rows(self):
        """get_chrome_history returns all rows returned by the DB cursor."""
        chrome_ts = 13_300_000_000_000_000
        mock_conn = _make_mock_connection([
            ('https://a.com', 'A', 1, chrome_ts),
            ('https://b.com', 'B', 2, chrome_ts),
            ('https://c.com', 'C', 5, chrome_ts),
        ])

        with patch('browser_history.os.path.exists', return_value=True), \
             patch('browser_history.temp_database_copy', _patched_temp_db()), \
             patch('browser_history.sqlite3.connect', return_value=mock_conn):
            entries = self.monitor.get_chrome_history()

        self.assertEqual(len(entries), 3)
        urls = [e.url for e in entries]
        self.assertIn('https://a.com', urls)
        self.assertIn('https://c.com', urls)

    #endregion

    #region Chrome — error paths

    def test_get_chrome_history_returns_empty_when_not_installed(self):
        """get_chrome_history returns [] when the history file does not exist."""
        with patch('browser_history.os.path.exists', return_value=False):
            entries = self.monitor.get_chrome_history()
        self.assertEqual(entries, [])

    def test_get_chrome_history_handles_corrupted_db(self):
        """get_chrome_history returns [] and does not raise on a corrupted database."""
        mock_conn = MagicMock()
        mock_cursor = MagicMock()
        mock_conn.cursor.return_value = mock_cursor
        mock_cursor.execute.side_effect = sqlite3.DatabaseError("file is not a database")

        with patch('browser_history.os.path.exists', return_value=True), \
             patch('browser_history.temp_database_copy', _patched_temp_db()), \
             patch('browser_history.sqlite3.connect', return_value=mock_conn):
            entries = self.monitor.get_chrome_history()

        self.assertEqual(entries, [])

    def test_get_chrome_history_handles_missing_table(self):
        """get_chrome_history returns [] and does not raise on OperationalError (missing table)."""
        mock_conn = MagicMock()
        mock_cursor = MagicMock()
        mock_conn.cursor.return_value = mock_cursor
        mock_cursor.execute.side_effect = sqlite3.OperationalError("no such table: urls")

        with patch('browser_history.os.path.exists', return_value=True), \
             patch('browser_history.temp_database_copy', _patched_temp_db()), \
             patch('browser_history.sqlite3.connect', return_value=mock_conn):
            entries = self.monitor.get_chrome_history()

        self.assertEqual(entries, [])

    #endregion

    #region Edge history

    def test_get_edge_history_returns_empty_when_not_installed(self):
        """get_edge_history returns [] when Edge has never been installed."""
        with patch('browser_history.os.path.exists', return_value=False):
            entries = self.monitor.get_edge_history()
        self.assertEqual(entries, [])

    def test_get_edge_history_tags_entries_with_edge_browser(self):
        """get_edge_history sets browser='edge' on every returned HistoryEntry."""
        chrome_ts = 13_300_000_000_000_000
        mock_conn = _make_mock_connection([
            ('https://bing.com', 'Bing', 1, chrome_ts),
        ])

        with patch('browser_history.os.path.exists', return_value=True), \
             patch('browser_history.temp_database_copy', _patched_temp_db()), \
             patch('browser_history.sqlite3.connect', return_value=mock_conn):
            entries = self.monitor.get_edge_history()

        self.assertEqual(len(entries), 1)
        self.assertEqual(entries[0].browser, 'edge')

    #endregion

    #region Firefox history

    def test_get_firefox_history_returns_empty_when_no_profile_found(self):
        """get_firefox_history returns [] when glob finds no Firefox profile."""
        with patch('browser_history.glob.glob', return_value=[]):
            entries = self.monitor.get_firefox_history()
        self.assertEqual(entries, [])

    def test_get_firefox_history_tags_entries_with_firefox_browser(self):
        """get_firefox_history sets browser='firefox' on every returned HistoryEntry."""
        # Firefox stores visit_date as µs since Unix epoch
        firefox_ts = 1_609_459_200_000_000
        mock_conn = _make_mock_connection([
            ('https://mozilla.org', 'Mozilla', 2, firefox_ts),
        ])

        fake_profile = '/fake/profiles/abc123.default/places.sqlite'
        with patch('browser_history.glob.glob', return_value=[fake_profile]), \
             patch('browser_history.os.path.exists', return_value=True), \
             patch('browser_history.temp_database_copy', _patched_temp_db()), \
             patch('browser_history.sqlite3.connect', return_value=mock_conn):
            entries = self.monitor.get_firefox_history()

        self.assertEqual(len(entries), 1)
        self.assertEqual(entries[0].browser, 'firefox')
        self.assertEqual(entries[0].url, 'https://mozilla.org')

    def test_get_firefox_history_returns_empty_when_profile_db_missing(self):
        """get_firefox_history returns [] when places.sqlite does not exist despite glob match."""
        fake_profile = '/fake/profiles/abc123.default/places.sqlite'
        with patch('browser_history.glob.glob', return_value=[fake_profile]), \
             patch('browser_history.os.path.exists', return_value=False):
            entries = self.monitor.get_firefox_history()
        self.assertEqual(entries, [])

    #endregion

    #region get_all_history

    def test_get_all_history_calls_all_three_browsers(self):
        """get_all_history delegates to Chrome, Edge, and Firefox scanners."""
        now = datetime.now()
        chrome_entry = HistoryEntry('https://chrome.com', 'C', now, 'chrome')
        edge_entry = HistoryEntry('https://edge.com', 'E', now, 'edge')
        ff_entry = HistoryEntry('https://ff.com', 'F', now, 'firefox')

        with patch.object(self.monitor, 'get_chrome_history', return_value=[chrome_entry]) as m_chrome, \
             patch.object(self.monitor, 'get_edge_history', return_value=[edge_entry]) as m_edge, \
             patch.object(self.monitor, 'get_firefox_history', return_value=[ff_entry]) as m_ff:
            entries = self.monitor.get_all_history()

        m_chrome.assert_called_once()
        m_edge.assert_called_once()
        m_ff.assert_called_once()
        self.assertEqual(len(entries), 3)

    #endregion

    #region URL deduplication / seen-URL tracking

    def test_is_url_seen_returns_false_for_new_url(self):
        """is_url_seen returns False when the URL has never been processed."""
        self.assertFalse(self.monitor.is_url_seen('https://brand-new.example.com'))

    def test_acknowledged_delivery_causes_is_url_seen_to_return_true(self):
        """Only explicit delivery acknowledgement causes seen=True."""
        url = 'https://sent.example.com'
        self.monitor.mark_delivery_acknowledged(url, 'chrome')
        self.assertTrue(self.monitor.is_url_seen(url))

    def test_seen_url_key_stored_with_browser_prefix(self):
        """_seen_urls uses 'browser:url' keys so the same URL is tracked per browser."""
        url = 'https://shared.example.com'
        self.monitor.mark_delivery_acknowledged(url, 'edge')
        self.assertIn('edge:https://shared.example.com', self.monitor._seen_urls)

    def test_get_new_entries_skips_already_seen_urls(self):
        """get_new_entries omits entries whose browser:url key is already in _seen_urls."""
        entry = HistoryEntry('https://already-seen.com', 'Seen', datetime.now(), 'chrome')
        self.monitor.mark_delivery_acknowledged('https://already-seen.com', 'chrome')

        with patch.object(self.monitor, 'get_all_history', return_value=[entry]):
            new_entries = self.monitor.get_new_entries()

        self.assertEqual(new_entries, [])

    def test_get_new_entries_returns_unseen_urls(self):
        """get_new_entries returns entries whose URL has not been seen before."""
        entry = HistoryEntry('https://brand-new.com', 'New', datetime.now(), 'chrome')

        with patch.object(self.monitor, 'get_all_history', return_value=[entry]):
            new_entries = self.monitor.get_new_entries()

        self.assertEqual(len(new_entries), 1)
        self.assertEqual(new_entries[0].url, 'https://brand-new.com')

    def test_get_new_entries_queues_without_marking_url_seen(self):
        """Discovery must not mark a URL seen before backend acknowledgement."""
        entry = HistoryEntry('https://once-only.com', 'Once', datetime.now(), 'chrome')

        with patch.object(self.monitor, 'get_all_history', return_value=[entry]):
            self.monitor.get_new_entries()

        self.assertFalse(self.monitor.is_url_seen(entry.url))
        self.assertEqual(
            self.monitor.get_delivery_state(entry.url, entry.browser),
            'queued',
        )

    def test_failed_delivery_is_returned_for_retry(self):
        entry = HistoryEntry('https://retry.example.com', 'Retry', datetime.now(), 'chrome')
        with patch.object(self.monitor, 'get_all_history', return_value=[entry]):
            self.monitor.get_new_entries()

        self.monitor.mark_delivery_sent(entry.url, entry.browser)
        self.monitor.mark_delivery_failed(entry.url, entry.browser)

        with patch.object(self.monitor, 'get_all_history', return_value=[]):
            retry_entries = self.monitor.get_new_entries()

        self.assertEqual([candidate.url for candidate in retry_entries], [entry.url])

    def test_acknowledged_delivery_survives_restart_and_suppresses_duplicate(self):
        entry = HistoryEntry('https://durable.example.com', 'Durable', datetime.now(), 'edge')
        with patch.object(self.monitor, 'get_all_history', return_value=[entry]):
            self.monitor.get_new_entries()
        self.monitor.mark_delivery_sent(entry.url, entry.browser)
        self.monitor.mark_delivery_acknowledged(entry.url, entry.browser)

        restarted = BrowserHistoryMonitor(state_file=self.state_file)
        with patch.object(restarted, 'get_all_history', return_value=[entry]):
            self.assertEqual(restarted.get_new_entries(), [])
        self.assertTrue(restarted.is_url_seen(entry.url))

    def test_restart_recovers_unacknowledged_sent_delivery_as_failed(self):
        entry = HistoryEntry('https://crash-recovery.example.com', 'Recovery', datetime.now(), 'firefox')
        with patch.object(self.monitor, 'get_all_history', return_value=[entry]):
            self.monitor.get_new_entries()
        self.monitor.mark_delivery_sent(entry.url, entry.browser)

        restarted = BrowserHistoryMonitor(state_file=self.state_file)
        with patch.object(restarted, 'get_all_history', return_value=[]):
            retry_entries = restarted.get_new_entries()

        self.assertEqual([candidate.url for candidate in retry_entries], [entry.url])
        self.assertEqual(
            restarted.get_delivery_state(entry.url, entry.browser),
            'failed',
        )

    def test_same_url_discovered_in_two_browsers_is_queued_once(self):
        now = datetime.now()
        chrome = HistoryEntry('https://shared.example.com', 'Shared', now, 'chrome')
        edge = HistoryEntry('https://shared.example.com', 'Shared', now, 'edge')

        with patch.object(self.monitor, 'get_all_history', return_value=[chrome, edge]):
            candidates = self.monitor.get_new_entries()

        self.assertEqual([entry.url for entry in candidates], [chrome.url])

    def test_state_pruning_never_drops_failed_delivery(self):
        self.monitor.MAX_ACKNOWLEDGED_RECORDS = 1
        first = HistoryEntry('https://first.example.com', 'First', datetime.now(), 'chrome')
        second = HistoryEntry('https://second.example.com', 'Second', datetime.now(), 'edge')
        failed = HistoryEntry('https://pending.example.com', 'Pending', datetime.now(), 'firefox')
        with patch.object(self.monitor, 'get_all_history', return_value=[first, second, failed]):
            self.monitor.get_new_entries()
        self.monitor.mark_delivery_acknowledged(first.url, first.browser)
        self.monitor.mark_delivery_acknowledged(second.url, second.browser)
        self.monitor.mark_delivery_failed(failed.url, failed.browser)

        restarted = BrowserHistoryMonitor(state_file=self.state_file)

        self.assertEqual(
            restarted.get_delivery_state(failed.url, failed.browser),
            'failed',
        )

    #endregion


if __name__ == '__main__':
    unittest.main(verbosity=2)

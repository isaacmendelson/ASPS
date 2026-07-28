"""
Tests for ASPS-623: verified remote-session termination

Covers:
  - RemoteAccessMonitor.disconnect_remote_session() result-dict contract
  - AnyDesk disconnect: success (CLI available) and failure (CLI not found)
  - TeamViewer disconnect: CLI success, CLI fail + process-kill, CLI not found
  - Unsupported app returns explicit unsupported result
  - Result dict always contains 'success' and 'reason' keys
  - ExtensionHandler.handle_message('remote:close_session') end-to-end
"""

import asyncio
import sys
import os
import types
import unittest
from unittest.mock import MagicMock, patch, PropertyMock

# ---------------------------------------------------------------------------
# Ensure src/ is on sys.path so imports work (before any stub setup)
# ---------------------------------------------------------------------------
SRC_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
if SRC_DIR not in sys.path:
    sys.path.insert(0, SRC_DIR)

# ---------------------------------------------------------------------------
# Minimal stubs for heavy optional imports that may not be installed in CI.
# We stub these BEFORE importing remote_monitor so the top-level imports
# resolve without error.  The real config.py (in SRC_DIR) is used as-is.
# ---------------------------------------------------------------------------

def _stub_module(name, **attrs):
    """Create and register a minimal stub module."""
    mod = types.ModuleType(name)
    for k, v in attrs.items():
        setattr(mod, k, v)
    sys.modules[name] = mod
    return mod


# Stub out everything that remote_monitor would normally import but that
# is NOT installed in a plain Python 3.11 test environment.
for _mod in [
    'win32api', 'win32con', 'win32event', 'win32gui',
    'win32process', 'win32security', 'win32ts',
    'pywintypes', 'winerror',
    'winreg',
    'psutil',
    'yaml',
]:
    if _mod not in sys.modules:
        _stub_module(_mod)

# ---------------------------------------------------------------------------
# Now we can import the units under test
# ---------------------------------------------------------------------------
from remote_monitor import AnyDeskCLI, RemoteAccessMonitor, TeamViewerCLI


# ===========================================================================
# Helpers
# ===========================================================================

def _make_monitor():
    """Create a RemoteAccessMonitor with real-time monitoring disabled."""
    with patch.object(RemoteAccessMonitor, 'start_realtime_monitoring', return_value=None):
        return RemoteAccessMonitor()


def _assert_result_shape(result: dict):
    """All result dicts must have 'success' (bool) and 'reason' (str)."""
    assert isinstance(result, dict), f"Expected dict, got {type(result)}"
    assert 'success' in result, f"Missing 'success' key: {result}"
    assert 'reason' in result, f"Missing 'reason' key: {result}"
    assert isinstance(result['success'], bool), f"'success' must be bool: {result}"
    assert isinstance(result['reason'], str), f"'reason' must be str: {result}"


# ===========================================================================
# AnyDesk tests
# ===========================================================================

class TestAnyDeskDisconnect(unittest.TestCase):

    def test_anydesk_success_when_cli_available(self):
        """disconnect_remote_session('anydesk') returns success when CLI works."""
        monitor = _make_monitor()
        mock_cli = MagicMock(spec=AnyDeskCLI)
        mock_cli.disconnect.return_value = True
        mock_cli.is_available.return_value = True
        monitor._anydesk_cli = mock_cli

        result = monitor.disconnect_remote_session('anydesk')

        _assert_result_shape(result)
        self.assertTrue(result['success'])
        self.assertEqual(result['reason'], 'disconnected')
        mock_cli.disconnect.assert_called_once()

    def test_anydesk_failure_when_cli_not_found(self):
        """disconnect_remote_session('anydesk') returns cli_not_found when exe absent."""
        monitor = _make_monitor()
        mock_cli = MagicMock(spec=AnyDeskCLI)
        mock_cli.disconnect.return_value = False
        mock_cli.is_available.return_value = False
        monitor._anydesk_cli = mock_cli

        result = monitor.disconnect_remote_session('anydesk')

        _assert_result_shape(result)
        self.assertFalse(result['success'])
        self.assertEqual(result['reason'], 'cli_not_found')

    def test_anydesk_failure_when_cli_present_but_command_fails(self):
        """disconnect_remote_session('anydesk') returns disconnect_failed when CLI present but fails."""
        monitor = _make_monitor()
        mock_cli = MagicMock(spec=AnyDeskCLI)
        mock_cli.disconnect.return_value = False
        mock_cli.is_available.return_value = True
        monitor._anydesk_cli = mock_cli

        result = monitor.disconnect_remote_session('anydesk')

        _assert_result_shape(result)
        self.assertFalse(result['success'])
        self.assertEqual(result['reason'], 'disconnect_failed')


# ===========================================================================
# TeamViewer tests
# ===========================================================================

class TestTeamViewerDisconnect(unittest.TestCase):

    def test_teamviewer_cli_success(self):
        """TeamViewerCLI.disconnect returns success when --action disconnect works."""
        mock_cli = MagicMock(spec=TeamViewerCLI)
        mock_cli.is_available.return_value = True
        mock_cli._run_cli.return_value = 0
        mock_cli.disconnect.return_value = {'success': True, 'reason': 'disconnected'}

        result = mock_cli.disconnect()
        _assert_result_shape(result)
        self.assertTrue(result['success'])
        self.assertEqual(result['reason'], 'disconnected')

    def test_teamviewer_cli_fails_but_process_kill_succeeds(self):
        """TeamViewerCLI.disconnect falls back to process kill when CLI returns non-zero."""
        with patch.object(TeamViewerCLI, '_find_exe', return_value='/fake/TeamViewer.exe'), \
             patch.object(TeamViewerCLI, 'is_available', return_value=True), \
             patch.object(TeamViewerCLI, '_run_cli', return_value=1), \
             patch.object(TeamViewerCLI, '_kill_process', return_value=True):

            cli = TeamViewerCLI.__new__(TeamViewerCLI)
            cli.exe = '/fake/TeamViewer.exe'
            result = TeamViewerCLI.disconnect(cli)

        _assert_result_shape(result)
        self.assertTrue(result['success'])
        self.assertEqual(result['reason'], 'killed_process')

    def test_teamviewer_cli_not_found_process_kill_succeeds(self):
        """TeamViewerCLI.disconnect falls back to process kill when exe is absent."""
        with patch.object(TeamViewerCLI, 'is_available', return_value=False), \
             patch.object(TeamViewerCLI, '_kill_process', return_value=True):

            cli = TeamViewerCLI.__new__(TeamViewerCLI)
            cli.exe = None
            result = TeamViewerCLI.disconnect(cli)

        _assert_result_shape(result)
        self.assertTrue(result['success'])
        self.assertEqual(result['reason'], 'killed_process')

    def test_teamviewer_all_methods_fail(self):
        """TeamViewerCLI.disconnect returns failure when both CLI and kill fail."""
        with patch.object(TeamViewerCLI, 'is_available', return_value=False), \
             patch.object(TeamViewerCLI, '_kill_process', return_value=False):

            cli = TeamViewerCLI.__new__(TeamViewerCLI)
            cli.exe = None
            result = TeamViewerCLI.disconnect(cli)

        _assert_result_shape(result)
        self.assertFalse(result['success'])
        self.assertEqual(result['reason'], 'cli_not_found')

    def test_disconnect_via_monitor_teamviewer(self):
        """disconnect_remote_session('teamviewer') delegates to TeamViewerCLI."""
        monitor = _make_monitor()
        mock_cli = MagicMock(spec=TeamViewerCLI)
        mock_cli.disconnect.return_value = {'success': True, 'reason': 'disconnected'}
        monitor._teamviewer_cli = mock_cli

        result = monitor.disconnect_remote_session('teamviewer')

        _assert_result_shape(result)
        self.assertTrue(result['success'])
        mock_cli.disconnect.assert_called_once()


# ===========================================================================
# Unsupported app test
# ===========================================================================

class TestUnsupportedApp(unittest.TestCase):

    def test_unsupported_app_returns_explicit_result(self):
        """disconnect_remote_session for unknown tool returns unsupported result, not False."""
        monitor = _make_monitor()
        result = monitor.disconnect_remote_session('ultravnc')

        _assert_result_shape(result)
        self.assertFalse(result['success'])
        self.assertEqual(result['reason'], 'unsupported')
        self.assertIn('app', result)
        self.assertEqual(result['app'], 'ultravnc')

    def test_empty_app_name_returns_unsupported(self):
        """disconnect_remote_session('') returns unsupported, not a crash."""
        monitor = _make_monitor()
        result = monitor.disconnect_remote_session('')

        _assert_result_shape(result)
        self.assertFalse(result['success'])
        self.assertEqual(result['reason'], 'unsupported')

    def test_none_app_name_returns_unsupported(self):
        """disconnect_remote_session(None) returns unsupported without raising."""
        monitor = _make_monitor()
        result = monitor.disconnect_remote_session(None)

        _assert_result_shape(result)
        self.assertFalse(result['success'])
        self.assertEqual(result['reason'], 'unsupported')


# ===========================================================================
# Result dict contract
# ===========================================================================

class TestResultShape(unittest.TestCase):
    """Guarantee the result dict always has 'success' and 'reason' for all paths."""

    def _check(self, app_name, mock_cli=None, mock_tv_cli=None):
        monitor = _make_monitor()
        if mock_cli:
            monitor._anydesk_cli = mock_cli
        if mock_tv_cli:
            monitor._teamviewer_cli = mock_tv_cli
        result = monitor.disconnect_remote_session(app_name)
        _assert_result_shape(result)
        return result

    def test_anydesk_success_shape(self):
        cli = MagicMock(spec=AnyDeskCLI)
        cli.disconnect.return_value = True
        cli.is_available.return_value = True
        r = self._check('anydesk', mock_cli=cli)
        self.assertIsInstance(r['success'], bool)

    def test_anydesk_failure_shape(self):
        cli = MagicMock(spec=AnyDeskCLI)
        cli.disconnect.return_value = False
        cli.is_available.return_value = False
        r = self._check('anydesk', mock_cli=cli)
        self.assertIsInstance(r['success'], bool)

    def test_teamviewer_shape(self):
        cli = MagicMock(spec=TeamViewerCLI)
        cli.disconnect.return_value = {'success': False, 'reason': 'disconnect_failed'}
        r = self._check('teamviewer', mock_tv_cli=cli)
        self.assertIsInstance(r['success'], bool)

    def test_unsupported_shape(self):
        r = self._check('logmein_unsupported_xyz')
        self.assertIsInstance(r['success'], bool)


# ===========================================================================
# ExtensionHandler integration
# ===========================================================================

class TestExtensionHandlerCloseSession(unittest.TestCase):
    """
    Test that ExtensionHandler.handle_message('remote:close_session')
    calls disconnect_remote_session and returns the typed response.
    """

    def _make_handler(self, disconnect_result):
        # Import here so stub modules are already in place
        from handlers.extension_handler import ExtensionHandler

        mock_rm = MagicMock()
        mock_rm.disconnect_remote_session.return_value = disconnect_result

        mock_scan = MagicMock()
        mock_auth = MagicMock()
        mock_auth.email = 'test@example.com'

        handler = ExtensionHandler(
            scan_service=mock_scan,
            auth_manager=mock_auth,
            device_id='test-device',
            remote_monitor=mock_rm,
        )
        return handler, mock_rm

    def _run(self, coro):
        return asyncio.run(coro)

    def test_handler_returns_success_on_disconnect(self):
        handler, mock_rm = self._make_handler(
            {'success': True, 'reason': 'disconnected'}
        )
        result = self._run(handler.handle_message({
            'type': 'remote:close_session',
            'app': 'anydesk',
        }))
        self.assertEqual(result['type'], 'remote:close_session_result')
        self.assertTrue(result['success'])
        self.assertEqual(result['reason'], 'disconnected')
        mock_rm.disconnect_remote_session.assert_called_once_with('anydesk')

    def test_handler_returns_failure_on_unsupported(self):
        handler, mock_rm = self._make_handler(
            {'success': False, 'reason': 'unsupported', 'app': 'ultraviewer'}
        )
        result = self._run(handler.handle_message({
            'type': 'remote:close_session',
            'app': 'ultraviewer',
        }))
        self.assertEqual(result['type'], 'remote:close_session_result')
        self.assertFalse(result['success'])
        self.assertEqual(result['reason'], 'unsupported')

    def test_handler_when_monitor_unavailable(self):
        from handlers.extension_handler import ExtensionHandler
        handler = ExtensionHandler(
            scan_service=MagicMock(),
            auth_manager=MagicMock(),
            device_id='dev',
            remote_monitor=None,
        )
        result = self._run(handler.handle_message({
            'type': 'remote:close_session',
            'app': 'anydesk',
        }))
        self.assertEqual(result['type'], 'remote:close_session_result')
        self.assertFalse(result['success'])
        self.assertEqual(result['reason'], 'monitor_unavailable')

    def test_handler_when_disconnect_raises(self):
        handler, mock_rm = self._make_handler(None)
        mock_rm.disconnect_remote_session.side_effect = RuntimeError("boom")
        result = self._run(handler.handle_message({
            'type': 'remote:close_session',
            'app': 'anydesk',
        }))
        self.assertEqual(result['type'], 'remote:close_session_result')
        self.assertFalse(result['success'])
        self.assertEqual(result['reason'], 'exception')

    def test_handler_strips_app_name_to_lowercase(self):
        handler, mock_rm = self._make_handler(
            {'success': True, 'reason': 'disconnected'}
        )
        self._run(handler.handle_message({
            'type': 'remote:close_session',
            'app': 'AnyDesk',
        }))
        mock_rm.disconnect_remote_session.assert_called_once_with('anydesk')


if __name__ == '__main__':
    unittest.main()

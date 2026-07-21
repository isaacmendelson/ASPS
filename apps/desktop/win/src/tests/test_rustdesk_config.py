"""
Unit Tests for RustDesk config/enum alignment (FR-039)

Documents the known alignment bug: RustDesk is present in REMOTE_APPS but
has no dedicated entry in enums.RemoteAccessApp — it is currently mapped to
VNC (id=7). Tests marked xfail document the *expected* correct behaviour and
will start passing once ASPS-562 is resolved.
"""

import unittest
from unittest.mock import patch, MagicMock
import sys
import os
import pytest

# Add parent directory to path for imports
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from config import REMOTE_APPS
from config import RemoteAccessApp as ConfigRemoteAccessApp   # plain class in config.py
from enums import RemoteAccessApp as RemoteAccessAppEnum      # IntEnum in enums.py
from remote_monitor import RemoteAccessMonitor


class TestRustDeskConfig(unittest.TestCase):
    """Tests that verify RustDesk is correctly declared in config and enums."""

    def setUp(self):
        """Instantiate RemoteAccessMonitor for process-detection tests."""
        self.monitor = RemoteAccessMonitor()

    #region Config presence

    def test_rustdesk_key_exists_in_remote_apps(self):
        """'rustdesk' must be a key in config.REMOTE_APPS."""
        self.assertIn('rustdesk', REMOTE_APPS)

    def test_rustdesk_process_names_are_defined(self):
        """REMOTE_APPS['rustdesk'] must declare at least one process name."""
        process_names = REMOTE_APPS['rustdesk'].get('process_names', [])
        self.assertIsInstance(process_names, list)
        self.assertGreater(len(process_names), 0)

    def test_rustdesk_exe_is_in_process_names(self):
        """'rustdesk.exe' must appear in the RustDesk process name list."""
        process_names = REMOTE_APPS['rustdesk'].get('process_names', [])
        self.assertIn('rustdesk.exe', process_names)

    def test_rustdesk_linux_process_name_is_in_process_names(self):
        """'rustdesk' (bare, for Linux/Mac) must appear in the process name list."""
        process_names = REMOTE_APPS['rustdesk'].get('process_names', [])
        self.assertIn('rustdesk', process_names)

    def test_rustdesk_config_has_id_key(self):
        """REMOTE_APPS['rustdesk'] must have an 'id' key for alert serialisation."""
        self.assertIn('id', REMOTE_APPS['rustdesk'])

    def test_rustdesk_process_names_are_all_lowercase(self):
        """All RustDesk process names must be lowercase (the monitor lower-cases for matching)."""
        for name in REMOTE_APPS['rustdesk'].get('process_names', []):
            self.assertEqual(name, name.lower(), f"Process name '{name}' is not lowercase")

    #endregion

    #region Enum alignment — known bug (ASPS-562)

    @pytest.mark.xfail(
        reason="ASPS-562: enums.RemoteAccessApp has no RustDesk entry; "
               "RustDesk currently reuses VNC (id=7)"
    )
    def test_rustdesk_enum_has_dedicated_entry(self):
        """enums.RemoteAccessApp should have a dedicated RustDesk member (not yet added)."""
        # This will raise AttributeError until the enum is extended
        _ = RemoteAccessAppEnum['RustDesk']

    @pytest.mark.xfail(
        reason="ASPS-562: REMOTE_APPS['rustdesk']['id'] is currently VNC (7), "
               "not a dedicated RustDesk enum value"
    )
    def test_rustdesk_config_id_is_not_mapped_to_vnc(self):
        """REMOTE_APPS['rustdesk']['id'] should not equal VNC (7) once ASPS-562 is fixed."""
        rustdesk_id = REMOTE_APPS['rustdesk']['id']
        vnc_value = ConfigRemoteAccessApp.VNC   # = 7
        # After the fix this assertion passes — right now it fails (rustdesk_id == 7)
        self.assertNotEqual(rustdesk_id, vnc_value)

    #endregion

    #region Current (buggy) state — these document what IS true right now

    def test_rustdesk_id_currently_equals_vnc(self):
        """Documents the current (known-bug) state: RustDesk id == VNC (7)."""
        rustdesk_id = REMOTE_APPS['rustdesk']['id']
        self.assertEqual(rustdesk_id, ConfigRemoteAccessApp.VNC)

    def test_vnc_value_is_7_in_both_config_and_enums(self):
        """VNC is id=7 in both the config class and the backend-facing IntEnum."""
        self.assertEqual(ConfigRemoteAccessApp.VNC, 7)
        self.assertEqual(int(RemoteAccessAppEnum.VNC), 7)

    #endregion

    #region Process detection

    def test_remote_monitor_finds_rustdesk_by_exe_process_name(self):
        """RemoteAccessMonitor.find_processes('rustdesk') matches 'rustdesk.exe' processes."""
        mock_proc = MagicMock()
        mock_proc.info = {'pid': 9999, 'name': 'rustdesk.exe'}

        with patch('remote_monitor.psutil.process_iter', return_value=[mock_proc]):
            found = self.monitor.find_processes('rustdesk')

        self.assertEqual(len(found), 1)
        self.assertIs(found[0], mock_proc)

    def test_remote_monitor_finds_rustdesk_by_bare_process_name(self):
        """RemoteAccessMonitor.find_processes('rustdesk') matches bare 'rustdesk' process name."""
        mock_proc = MagicMock()
        mock_proc.info = {'pid': 9998, 'name': 'rustdesk'}

        with patch('remote_monitor.psutil.process_iter', return_value=[mock_proc]):
            found = self.monitor.find_processes('rustdesk')

        self.assertEqual(len(found), 1)
        self.assertIs(found[0], mock_proc)

    def test_remote_monitor_does_not_find_rustdesk_when_not_running(self):
        """find_processes('rustdesk') returns [] when no rustdesk process is present."""
        mock_proc = MagicMock()
        mock_proc.info = {'pid': 1111, 'name': 'notepad.exe'}

        with patch('remote_monitor.psutil.process_iter', return_value=[mock_proc]):
            found = self.monitor.find_processes('rustdesk')

        self.assertEqual(found, [])

    #endregion


if __name__ == '__main__':
    unittest.main(verbosity=2)

"""
Unit tests for RemoteAccessMonitor
Tests the event-driven architecture
"""

import unittest
from unittest.mock import Mock, patch, MagicMock
from datetime import datetime
from pathlib import Path
import sys
import os

# Add parent directory to path
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from remote_monitor import (
    LogParser, SessionTracker, SessionState, SessionDirection,
    RemoteAppStatus, StateChange, GeoIPLookup, calculate_confidence
)


class TestLogParser(unittest.TestCase):
    """Test LogParser.parse_line() for all supported apps."""
    
    def setUp(self):
        self.parser = LogParser()
    
    # ─── AnyDesk Tests ────────────────────────────────────────────────────────
    
    def test_anydesk_incoming_request(self):
        line = "info 2026-03-23 12:00:00.123 Incoming session request: - (1458399339)"
        result = self.parser.parse_line(line)
        self.assertIsNotNone(result)
        self.assertEqual(result['event'], 'incoming_request')
        self.assertEqual(result['remote_id'], '1458399339')
    
    def test_anydesk_incoming_ip_new_format(self):
        line = "info 2026-03-23 12:00:00.123 [100.87.30.66:51533] Incoming connection"
        result = self.parser.parse_line(line)
        self.assertIsNotNone(result)
        self.assertEqual(result['event'], 'client_ip')
        self.assertEqual(result['remote_ip'], '100.87.30.66')
    
    def test_anydesk_incoming_ip_old_format(self):
        line = "info 2026-03-23 12:00:00.123 Client connected from 192.168.1.100"
        result = self.parser.parse_line(line)
        self.assertIsNotNone(result)
        self.assertEqual(result['event'], 'client_ip')
        self.assertEqual(result['remote_ip'], '192.168.1.100')
    
    def test_anydesk_session_started(self):
        line = "info 2026-03-23 12:00:00.123 Session started (client_id=1458399339)"
        result = self.parser.parse_line(line)
        self.assertIsNotNone(result)
        self.assertEqual(result['event'], 'session_started')
    
    def test_anydesk_session_stopped(self):
        line = "info 2026-03-23 12:00:00.123 Session stopped"
        result = self.parser.parse_line(line)
        self.assertIsNotNone(result)
        self.assertEqual(result['event'], 'session_stopped')
    
    def test_anydesk_outgoing(self):
        line = "info 2026-03-23 12:00:00.123 Connecting to 987654321"
        result = self.parser.parse_line(line)
        self.assertIsNotNone(result)
        self.assertEqual(result['event'], 'outgoing_start')
        self.assertEqual(result['remote_id'], '987654321')
    
    def test_anydesk_remote_os(self):
        line = "info 2026-03-23 12:00:00.123 Remote OS: iOS"
        result = self.parser.parse_line(line)
        self.assertIsNotNone(result)
        self.assertEqual(result['event'], 'remote_info')
        self.assertEqual(result['remote_os'], 'iOS')
    
    def test_anydesk_file_transfer_start(self):
        line = "info 2026-03-23 12:00:00.123 local_file_transfer Starting upload"
        result = self.parser.parse_line(line)
        self.assertIsNotNone(result)
        self.assertEqual(result['event'], 'file_transfer_start')
    
    def test_anydesk_file_transfer_stop(self):
        line = "info 2026-03-23 12:00:00.123 local_file_transfer completed"
        result = self.parser.parse_line(line)
        self.assertIsNotNone(result)
        self.assertEqual(result['event'], 'file_transfer_stop')
    
    def test_anydesk_connection_direct(self):
        line = "info 2026-03-23 12:00:00.123 Route type: direct"
        result = self.parser.parse_line(line)
        self.assertIsNotNone(result)
        self.assertEqual(result['event'], 'connection_type')
        self.assertEqual(result['conn_type'], 'direct')
    
    def test_anydesk_connection_relay(self):
        line = "info 2026-03-23 12:00:00.123 Route type: tunnel"
        result = self.parser.parse_line(line)
        self.assertIsNotNone(result)
        self.assertEqual(result['event'], 'connection_type')
        self.assertEqual(result['conn_type'], 'relay')
    
    # ─── TeamViewer Tests ─────────────────────────────────────────────────────
    
    def test_teamviewer_incoming(self):
        line = "2026-03-23 12:00:00 Incoming connection from Partner ID: 123456789"
        result = self.parser.parse_line(line)
        self.assertIsNotNone(result)
        self.assertEqual(result['event'], 'incoming_request')
        self.assertEqual(result['remote_id'], '123456789')
    
    def test_teamviewer_disconnect(self):
        line = "2026-03-23 12:00:00 Session ended"
        result = self.parser.parse_line(line)
        self.assertIsNotNone(result)
        self.assertEqual(result['event'], 'session_stopped')
    
    # ─── VNC Tests ────────────────────────────────────────────────────────────
    
    def test_vnc_accept_ipv4(self):
        line = "Connections: Accepted: 192.168.1.100::5901"
        result = self.parser.parse_line(line)
        self.assertIsNotNone(result)
        self.assertEqual(result['event'], 'incoming_request')
        self.assertEqual(result['remote_ip'], '192.168.1.100')
    
    def test_vnc_accept_ipv6(self):
        line = "Connections: Accepted: [::1]::42976"
        result = self.parser.parse_line(line)
        self.assertIsNotNone(result)
        self.assertEqual(result['event'], 'incoming_request')
        self.assertEqual(result['remote_ip'], '::1')
    
    def test_vnc_close(self):
        line = "Connections: Closed: 192.168.1.100::5901"
        result = self.parser.parse_line(line)
        self.assertIsNotNone(result)
        self.assertEqual(result['event'], 'session_stopped')
    
    # ─── Chrome Remote Desktop Tests ──────────────────────────────────────────
    
    def test_crd_client_connected(self):
        line = "2026-03-23 12:00:00 Client connected from 10.0.0.1"
        result = self.parser.parse_line(line)
        self.assertIsNotNone(result)
        self.assertEqual(result['event'], 'client_ip')
        self.assertEqual(result['remote_ip'], '10.0.0.1')
    
    def test_crd_session_started(self):
        line = "2026-03-23 12:00:00 Session started"
        result = self.parser.parse_line(line)
        self.assertIsNotNone(result)
        self.assertEqual(result['event'], 'session_started')
    
    def test_crd_client_disconnected(self):
        line = "2026-03-23 12:00:00 Client disconnected"
        result = self.parser.parse_line(line)
        self.assertIsNotNone(result)
        self.assertEqual(result['event'], 'session_stopped')
    
    # ─── Negative Tests ───────────────────────────────────────────────────────
    
    def test_irrelevant_line(self):
        line = "Some random log message that doesn't match any pattern"
        result = self.parser.parse_line(line)
        self.assertIsNone(result)


class TestSessionTracker(unittest.TestCase):
    """Test SessionTracker state machine."""
    
    def setUp(self):
        self.tracker = SessionTracker()
    
    def test_incoming_creates_session(self):
        event = {
            'event': 'incoming_request',
            'timestamp': datetime.now(),
            'remote_id': '123456789',
            'remote_ip': '',
        }
        self.tracker.on_event(event)
        session = self.tracker.get_current_session()
        self.assertIsNotNone(session)
        self.assertTrue(session.active)
        self.assertEqual(session.direction, SessionDirection.INCOMING)
        self.assertEqual(session.remote_id, '123456789')
    
    def test_ip_updates_session(self):
        # First create a session
        self.tracker.on_event({
            'event': 'incoming_request',
            'timestamp': datetime.now(),
            'remote_id': '123456789',
            'remote_ip': '',
        })
        # Then update IP
        self.tracker.on_event({
            'event': 'client_ip',
            'timestamp': datetime.now(),
            'remote_ip': '192.168.1.100',
        })
        session = self.tracker.get_current_session()
        self.assertEqual(session.remote_ip, '192.168.1.100')
    
    def test_session_stopped_clears_active(self):
        # Create session
        self.tracker.on_event({
            'event': 'incoming_request',
            'timestamp': datetime.now(),
            'remote_id': '123456789',
            'remote_ip': '',
        })
        # Stop session
        self.tracker.on_event({
            'event': 'session_stopped',
            'timestamp': datetime.now(),
        })
        session = self.tracker.get_current_session()
        self.assertIsNone(session)  # No active session
        self.assertFalse(self.tracker.has_active_session())
    
    def test_file_transfer_tracking(self):
        # Create session
        self.tracker.on_event({
            'event': 'incoming_request',
            'timestamp': datetime.now(),
            'remote_id': '123456789',
            'remote_ip': '',
        })
        # Start file transfer
        self.tracker.on_event({
            'event': 'file_transfer_start',
            'timestamp': datetime.now(),
        })
        session = self.tracker.get_current_session()
        self.assertTrue(session.file_transfer_active)
        self.assertEqual(session.file_transfers, 1)
        
        # Stop file transfer
        self.tracker.on_event({
            'event': 'file_transfer_stop',
            'timestamp': datetime.now(),
        })
        session = self.tracker.get_current_session()
        self.assertFalse(session.file_transfer_active)
        self.assertEqual(session.file_transfers, 1)  # Count preserved
    
    def test_remote_info_updates(self):
        # Create session
        self.tracker.on_event({
            'event': 'incoming_request',
            'timestamp': datetime.now(),
            'remote_id': '123456789',
            'remote_ip': '',
        })
        # Update remote OS
        self.tracker.on_event({
            'event': 'remote_info',
            'timestamp': datetime.now(),
            'remote_os': 'iOS',
        })
        session = self.tracker.get_current_session()
        self.assertEqual(session.remote_os, 'iOS')


class TestGeoIPLookup(unittest.TestCase):
    """Test GeoIP lookup functionality."""
    
    def test_private_ip_returns_empty(self):
        result = GeoIPLookup.lookup('192.168.1.100')
        self.assertEqual(result, {})
    
    def test_localhost_returns_empty(self):
        result = GeoIPLookup.lookup('127.0.0.1')
        self.assertEqual(result, {})
    
    def test_tailscale_ip_returns_empty(self):
        result = GeoIPLookup.lookup('100.64.0.1')
        self.assertEqual(result, {})


class TestConfidenceCalculation(unittest.TestCase):
    """Test confidence level calculation."""
    
    def test_high_confidence(self):
        signals = {
            'active_connection': True,
            'log_session_active': True,
            'cpu_active': True,
            'service_running': True,
        }
        self.assertEqual(calculate_confidence(signals), 'high')
    
    def test_medium_confidence(self):
        signals = {
            'active_connection': True,
            'log_session_active': False,
            'cpu_active': True,
            'service_running': False,
        }
        self.assertEqual(calculate_confidence(signals), 'high')
    
    def test_low_confidence(self):
        signals = {
            'active_connection': False,
            'log_session_active': False,
            'cpu_active': True,
            'service_running': False,
        }
        self.assertEqual(calculate_confidence(signals), 'low')


class TestRemoteAppStatus(unittest.TestCase):
    """Test RemoteAppStatus dataclass."""
    
    def test_creation(self):
        status = RemoteAppStatus(
            app_name='anydesk',
            app_id=1,
            is_running=True,
            has_active_session=True,
            process_count=2,
            connection_count=1,
            connection_status=1,
            remote_ip='192.168.1.100',
            direction='incoming',
            confidence='high',
        )
        self.assertEqual(status.app_name, 'anydesk')
        self.assertTrue(status.is_running)
        self.assertTrue(status.has_active_session)


if __name__ == '__main__':
    unittest.main()

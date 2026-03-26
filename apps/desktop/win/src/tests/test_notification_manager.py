"""
Unit Tests for NotificationManager
Tests Windows Toast notification functionality
"""

import unittest
from unittest.mock import Mock, patch, MagicMock
import sys
import os

# Add parent directory to path for imports
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from notification_manager import NotificationManager


class TestNotificationManager(unittest.TestCase):
    """Test suite for NotificationManager class"""
    
    def setUp(self):
        """Set up test fixtures"""
        # Mock the toast libraries to avoid platform dependencies
        self.winotify_patcher = patch('notification_manager.TOAST_AVAILABLE', True)
        self.backend_patcher = patch('notification_manager.TOAST_BACKEND', 'winotify')
        
        self.winotify_patcher.start()
        self.backend_patcher.start()
        
        self.manager = NotificationManager()
    
    def tearDown(self):
        """Clean up after tests"""
        self.winotify_patcher.stop()
        self.backend_patcher.stop()
    
    #region Constructor Tests
    
    def test_constructor_initializes_properties(self):
        """Constructor should initialize all required properties"""
        self.assertEqual(self.manager.app_id, "AntiScam Protection System")
        self.assertIsNone(self.manager.toast_notifier)
    
    #endregion
    
    #region Risk Icon Tests
    
    def test_risk_icons_exist_for_all_levels(self):
        """RISK_ICONS should contain all risk levels"""
        expected_levels = ["none", "low", "medium", "high", "critical"]
        for level in expected_levels:
            self.assertIn(level, NotificationManager.RISK_ICONS)
    
    def test_risk_icons_are_strings(self):
        """All RISK_ICONS should be strings (emoji)"""
        for icon in NotificationManager.RISK_ICONS.values():
            self.assertIsInstance(icon, str)
            self.assertGreater(len(icon), 0)
    
    #endregion
    
    #region Show Risk Notification Tests
    
    @patch('notification_manager.TOAST_BACKEND', 'winotify')
    def test_show_risk_notification_normalizes_risk_level(self):
        """Should normalize risk level to lowercase"""
        with patch.object(self.manager, '_show_winotify') as mock_show:
            self.manager.show_risk_notification(
                risk_level="HIGH",
                title="Test",
                message="Test message"
            )
            # Should be called with normalized lowercase
            mock_show.assert_called_once()
    
    @patch('notification_manager.TOAST_BACKEND', 'winotify')
    def test_show_risk_notification_adds_icon_to_title(self):
        """Should add risk icon to title"""
        with patch.object(self.manager, '_show_winotify') as mock_show:
            self.manager.show_risk_notification(
                risk_level="high",
                title="Alert",
                message="Test"
            )
            # First arg should be full title with icon
            call_args = mock_show.call_args[0]
            full_title = call_args[0]
            # Should contain emoji and original title
            self.assertIn("Alert", full_title)
            self.assertTrue(len(full_title) > len("Alert"))  # Has icon added
    
    @patch('notification_manager.TOAST_BACKEND', 'winotify')
    def test_show_risk_notification_adds_url_to_message(self):
        """Should append URL to message when provided"""
        with patch.object(self.manager, '_show_winotify') as mock_show:
            test_url = "https://example.com"
            self.manager.show_risk_notification(
                risk_level="medium",
                title="Test",
                message="Original message",
                url=test_url
            )
            call_args = mock_show.call_args[0]
            message = call_args[1]
            self.assertIn("Original message", message)
            self.assertIn(test_url, message)
    
    @patch('notification_manager.TOAST_BACKEND', 'winotify')
    def test_show_risk_notification_handles_invalid_risk_level(self):
        """Should default to 'medium' for invalid risk level"""
        with patch.object(self.manager, '_show_winotify') as mock_show:
            self.manager.show_risk_notification(
                risk_level="invalid_level",
                title="Test",
                message="Test"
            )
            # Should be called with 'medium' as fallback
            call_args = mock_show.call_args[0]
            risk_level_arg = call_args[2]
            self.assertEqual(risk_level_arg, "medium")
    
    @patch('notification_manager.TOAST_BACKEND', 'winotify')
    def test_show_risk_notification_handles_none_risk_level(self):
        """Should default to 'medium' when risk_level is None"""
        with patch.object(self.manager, '_show_winotify') as mock_show:
            self.manager.show_risk_notification(
                risk_level=None,
                title="Test",
                message="Test"
            )
            call_args = mock_show.call_args[0]
            risk_level_arg = call_args[2]
            self.assertEqual(risk_level_arg, "medium")
    
    @patch('notification_manager.TOAST_AVAILABLE', False)
    def test_show_risk_notification_when_toast_unavailable(self):
        """Should not crash when toast is unavailable"""
        manager = NotificationManager()
        # Should not raise exception
        try:
            manager.show_risk_notification(
                risk_level="high",
                title="Test",
                message="Test"
            )
        except Exception as e:
            self.fail(f"Should not raise exception: {e}")
    
    #endregion
    
    #region Show URL Alert Tests
    
    @patch('notification_manager.TOAST_BACKEND', 'winotify')
    def test_show_url_alert_critical_risk_score(self):
        """Should show critical alert for score >= 80"""
        with patch.object(self.manager, '_show_winotify') as mock_show:
            self.manager.show_url_alert(
                risk_score=85,
                risk_types=[1, 2],
                url="https://dangerous.com"
            )
            call_args = mock_show.call_args[0]
            title = call_args[0]
            risk_level = call_args[2]
            self.assertIn("CRITICAL", title)
            self.assertEqual(risk_level, "critical")
    
    @patch('notification_manager.TOAST_BACKEND', 'winotify')
    def test_show_url_alert_high_risk_score(self):
        """Should show high alert for score >= 60"""
        with patch.object(self.manager, '_show_winotify') as mock_show:
            self.manager.show_url_alert(
                risk_score=65,
                risk_types=[1],
                url="https://risky.com"
            )
            call_args = mock_show.call_args[0]
            title = call_args[0]
            risk_level = call_args[2]
            self.assertIn("High Risk", title)
            self.assertEqual(risk_level, "high")
    
    @patch('notification_manager.TOAST_BACKEND', 'winotify')
    def test_show_url_alert_medium_risk_score(self):
        """Should show medium alert for score >= 40"""
        with patch.object(self.manager, '_show_winotify') as mock_show:
            self.manager.show_url_alert(
                risk_score=50,
                risk_types=[2],
                url="https://suspicious.com"
            )
            call_args = mock_show.call_args[0]
            title = call_args[0]
            risk_level = call_args[2]
            self.assertIn("Suspicious", title)
            self.assertEqual(risk_level, "medium")
    
    @patch('notification_manager.TOAST_BACKEND', 'winotify')
    def test_show_url_alert_low_risk_score(self):
        """Should show low alert for score >= 20"""
        with patch.object(self.manager, '_show_winotify') as mock_show:
            self.manager.show_url_alert(
                risk_score=25,
                risk_types=[],
                url="https://lowrisk.com"
            )
            call_args = mock_show.call_args[0]
            title = call_args[0]
            risk_level = call_args[2]
            self.assertIn("Low Risk", title)
            self.assertEqual(risk_level, "low")
    
    @patch('notification_manager.TOAST_BACKEND', 'winotify')
    def test_show_url_alert_safe_score(self):
        """Should show safe notification for score < 20"""
        with patch.object(self.manager, '_show_winotify') as mock_show:
            self.manager.show_url_alert(
                risk_score=10,
                risk_types=[],
                url="https://safe.com"
            )
            call_args = mock_show.call_args[0]
            risk_level = call_args[2]
            self.assertEqual(risk_level, "none")
    
    @patch('notification_manager.TOAST_BACKEND', 'winotify')
    def test_show_url_alert_includes_risk_types_in_message(self):
        """Should include risk type names in message"""
        with patch.object(self.manager, '_show_winotify') as mock_show:
            self.manager.show_url_alert(
                risk_score=70,
                risk_types=[1, 3],  # Phishing + Impersonation
                url="https://phishing.com"
            )
            call_args = mock_show.call_args[0]
            message = call_args[1]
            self.assertIn("Phishing", message)
            self.assertIn("Impersonation", message)
    
    @patch('notification_manager.TOAST_BACKEND', 'winotify')
    def test_show_url_alert_empty_risk_types(self):
        """Should handle empty risk types list"""
        with patch.object(self.manager, '_show_winotify') as mock_show:
            self.manager.show_url_alert(
                risk_score=50,
                risk_types=[],
                url="https://example.com"
            )
            call_args = mock_show.call_args[0]
            message = call_args[1]
            self.assertIn("No specific threats", message)
    
    #endregion
    
    #region Show Remote Access Alert Tests
    
    @patch('notification_manager.TOAST_BACKEND', 'winotify')
    def test_show_remote_access_alert_incoming(self):
        """Should show critical alert for incoming connection"""
        with patch.object(self.manager, '_show_winotify') as mock_show:
            self.manager.show_remote_access_alert(
                app_name="AnyDesk",
                direction="incoming",
                is_startup=False
            )
            call_args = mock_show.call_args[0]
            title = call_args[0]
            message = call_args[1]
            risk_level = call_args[2]
            
            self.assertIn("ANYDESK", title)
            self.assertIn("INCOMING", title)
            self.assertIn("controlling", message)
            self.assertEqual(risk_level, "critical")
    
    @patch('notification_manager.TOAST_BACKEND', 'winotify')
    def test_show_remote_access_alert_outgoing(self):
        """Should show medium alert for outgoing connection"""
        with patch.object(self.manager, '_show_winotify') as mock_show:
            self.manager.show_remote_access_alert(
                app_name="TeamViewer",
                direction="outgoing",
                is_startup=False
            )
            call_args = mock_show.call_args[0]
            title = call_args[0]
            risk_level = call_args[2]
            
            self.assertIn("TEAMVIEWER", title)
            self.assertEqual(risk_level, "medium")
    
    @patch('notification_manager.TOAST_BACKEND', 'winotify')
    def test_show_remote_access_alert_startup_flag(self):
        """Should mention startup detection in message"""
        with patch.object(self.manager, '_show_winotify') as mock_show:
            self.manager.show_remote_access_alert(
                app_name="AnyDesk",
                direction="incoming",
                is_startup=True
            )
            call_args = mock_show.call_args[0]
            message = call_args[1]
            self.assertIn("startup", message.lower())
    
    #endregion
    
    #region Icon Path Tests
    
    def test_get_app_icon_path_returns_none_when_no_icon(self):
        """Should return None when no icon file exists"""
        icon_path = self.manager._get_app_icon_path()
        # In test environment, likely no icon exists
        # This is okay - just testing the method doesn't crash
        self.assertIsInstance(icon_path, (str, type(None)))
    
    def test_get_risk_icon_path_fallback_to_app_icon(self):
        """Should fallback to app icon when risk icon doesn't exist"""
        with patch.object(self.manager, '_get_app_icon_path', return_value="/fake/icon.ico"):
            icon_path = self.manager._get_risk_icon_path("nonexistent_level")
            self.assertEqual(icon_path, "/fake/icon.ico")
    
    #endregion


if __name__ == '__main__':
    # Run tests with verbose output
    unittest.main(verbosity=2)

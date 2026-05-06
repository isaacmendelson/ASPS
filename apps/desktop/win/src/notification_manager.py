"""
AntiScam Desktop App - Notification Manager
Windows Toast Notification support with risk-based styling
"""

import os
import sys
import logging
from typing import Optional
from pathlib import Path

logger = logging.getLogger(__name__)

# Try to import Windows toast notification library
TOAST_AVAILABLE = False
TOAST_BACKEND = None
audio = None  # Will be set if winotify is available

try:
    from winotify import Notification, audio as winotify_audio
    audio = winotify_audio
    TOAST_AVAILABLE = True
    TOAST_BACKEND = "winotify"
except ImportError:
    try:
        from win10toast import ToastNotifier
        TOAST_AVAILABLE = True
        TOAST_BACKEND = "win10toast"
    except ImportError:
        logger.warning("No Windows toast library available. Install: pip install winotify or pip install win10toast")


class NotificationManager:
    """
    Manages Windows Toast Notifications with risk-based styling.
    
    Features:
    - Windows Toast Notification (native Windows 10/11)
    - Risk-based icons and colors
    - Clickable notifications (opens details)
    - Audio alerts for high-risk
    """
    
    # Risk level to icon mapping
    RISK_ICONS = {
        "none": "✅",
        "low": "ℹ️", 
        "medium": "⚠️",
        "high": "🚨",
        "critical": "🛑"
    }
    
    # Risk level to audio mapping (winotify only) - initialized in __init__
    RISK_AUDIO = {}
    
    def __init__(self):
        self.toast_notifier = None
        self.app_id = "AntiScam Protection System"
        
        # Initialize RISK_AUDIO if winotify is available
        if TOAST_BACKEND == "winotify" and audio:
            self.RISK_AUDIO = {
                "none": audio.Default,
                "low": audio.Default,
                "medium": audio.Reminder,
                "high": audio.LoopingAlarm,
                "critical": audio.LoopingAlarm
            }
        else:
            self.RISK_AUDIO = {}
        
        if not TOAST_AVAILABLE:
            logger.warning("Toast notifications not available on this system")
            return
            
        if TOAST_BACKEND == "win10toast":
            try:
                self.toast_notifier = ToastNotifier()
                logger.info("Notification manager initialized (win10toast)")
            except Exception as e:
                logger.error(f"Failed to initialize win10toast: {e}")
        else:
            logger.info("Notification manager initialized (winotify)")
    
    def show_risk_notification(
        self,
        risk_level: str,
        title: str,
        message: str,
        url: Optional[str] = None,
        duration: int = 10,
        action_buttons: Optional[list] = None,
    ):
        """
        Show a risk-based notification.

        Args:
            risk_level: "none", "low", "medium", "high", "critical"
            title: Notification title
            message: Notification message
            url: URL to show in details (optional)
            duration: Duration in seconds (winotify only)
            action_buttons: optional list of (label, launch) tuples. When
                provided, overrides the default "View Details for high/critical".
                Each tuple becomes a clickable button on the toast. `launch` may
                be empty string to make the button a no-op (just dismisses).
        """
        if not TOAST_AVAILABLE:
            logger.warning(f"Toast not available - would show: {title}: {message}")
            return

        # Normalize risk level
        risk_level = risk_level.lower() if risk_level else "medium"
        if risk_level not in self.RISK_ICONS:
            risk_level = "medium"

        # Add risk icon to title
        icon = self.RISK_ICONS[risk_level]
        full_title = f"{icon} {title}"

        # Add URL to message if provided
        if url:
            message = f"{message}\n\nURL: {url}"

        try:
            if TOAST_BACKEND == "winotify":
                self._show_winotify(full_title, message, risk_level, duration, action_buttons)
            elif TOAST_BACKEND == "win10toast":
                self._show_win10toast(full_title, message, duration)
        except Exception as e:
            logger.error(f"Failed to show notification: {e}")

    def _show_winotify(
        self,
        title: str,
        message: str,
        risk_level: str,
        duration: int,
        action_buttons: Optional[list] = None,
    ):
        """Show notification using winotify (preferred)."""
        toast = Notification(
            app_id=self.app_id,
            title=title,
            msg=message,
            duration=duration
        )

        # Set icon based on risk level
        icon_path = self._get_risk_icon_path(risk_level)
        if icon_path and os.path.exists(icon_path):
            toast.set_icon(icon_path)

        # Set audio based on risk level
        if self.RISK_AUDIO and audio:
            audio_type = self.RISK_AUDIO.get(risk_level, audio.Default)
            toast.set_audio(audio_type, loop=False)

        # Action buttons: explicit list takes precedence; otherwise default
        # behavior is "View Details" for high/critical only.
        if action_buttons is not None:
            for label, launch in action_buttons:
                toast.add_actions(label=str(label), launch=str(launch or ""))
        elif risk_level in ["high", "critical"]:
            toast.add_actions(label="View Details", launch="")

        toast.show()
        logger.info(f"Showed winotify notification: {title}")
    
    def _show_win10toast(self, title: str, message: str, duration: int):
        """Show notification using win10toast (fallback)."""
        if not self.toast_notifier:
            logger.error("Toast notifier not initialized")
            return
            
        # Get icon path
        icon_path = self._get_app_icon_path()
        
        self.toast_notifier.show_toast(
            title=title,
            msg=message,
            duration=duration,
            icon_path=icon_path,
            threaded=True
        )
        logger.info(f"Showed win10toast notification: {title}")
    
    def _get_risk_icon_path(self, risk_level: str) -> Optional[str]:
        """Get icon path for risk level."""
        # Look for icons in data/icons directory
        src_dir = Path(__file__).parent
        icons_dir = src_dir / "data" / "icons"
        
        icon_file = icons_dir / f"risk_{risk_level}.ico"
        if icon_file.exists():
            return str(icon_file)
        
        # Fallback to app icon
        return self._get_app_icon_path()
    
    def _get_app_icon_path(self) -> Optional[str]:
        """Get app icon path."""
        src_dir = Path(__file__).parent
        icon_paths = [
            src_dir / "data" / "icon.ico",
            src_dir / "data" / "asps.ico",
            src_dir / "ui" / "icon.ico"
        ]
        
        for icon_path in icon_paths:
            if icon_path.exists():
                return str(icon_path)
        
        return None
    
    def show_url_alert(self, risk_score: int, risk_types: list, url: str):
        """
        Show notification for URL analysis result.
        
        Args:
            risk_score: Risk score (0-100)
            risk_types: List of risk type IDs
            url: The URL being analyzed
        """
        # Determine risk level from score
        if risk_score >= 80:
            risk_level = "critical"
            title = "CRITICAL THREAT DETECTED"
        elif risk_score >= 60:
            risk_level = "high"
            title = "High Risk Website"
        elif risk_score >= 40:
            risk_level = "medium"
            title = "Suspicious Website"
        elif risk_score >= 20:
            risk_level = "low"
            title = "Low Risk Website"
        else:
            risk_level = "none"
            title = "Website Checked"
        
        # Build message from risk types
        risk_names = {
            1: "Phishing",
            2: "Cloaking",
            3: "Impersonation",
            4: "Fake DOM",
            5: "Unknown Risk"
        }
        
        if risk_types:
            risks = [risk_names.get(rt, f"Risk {rt}") for rt in risk_types]
            message = f"Risk Score: {risk_score}\nDetected: {', '.join(risks)}"
        else:
            message = f"Risk Score: {risk_score}\nNo specific threats detected"
        
        self.show_risk_notification(
            risk_level=risk_level,
            title=title,
            message=message,
            url=url
        )
    
    def show_remote_access_alert(self, app_name: str, direction: str, is_startup: bool = False):
        """
        Show notification for remote access detection.
        
        Args:
            app_name: Name of remote access app (e.g., "AnyDesk")
            direction: "incoming" or "outgoing"
            is_startup: If detected on app startup
        """
        if direction == "incoming":
            risk_level = "critical"
            title = f"{app_name.upper()} - INCOMING CONNECTION!"
            message = "Someone may be controlling your computer right now."
        else:
            risk_level = "medium"
            title = f"{app_name.upper()} Active Session"
            message = "Remote access session detected."
        
        if is_startup:
            message += "\n(Detected on startup)"
        
        self.show_risk_notification(
            risk_level=risk_level,
            title=title,
            message=message
        )


# Standalone test
if __name__ == "__main__":
    import time
    
    logging.basicConfig(level=logging.INFO)
    
    print("=" * 70)
    print("NOTIFICATION MANAGER - TEST")
    print("=" * 70)
    print(f"Toast backend: {TOAST_BACKEND or 'None'}")
    print()
    
    if not TOAST_AVAILABLE:
        print("ERROR: No toast library available!")
        print("Install: pip install winotify")
        sys.exit(1)
    
    manager = NotificationManager()
    
    print("Testing different risk levels...\n")
    
    # Test 1: Low risk
    print("1. Low risk notification...")
    manager.show_risk_notification(
        risk_level="low",
        title="Website Checked",
        message="This website appears safe.",
        url="https://example.com"
    )
    time.sleep(3)
    
    # Test 2: Medium risk
    print("2. Medium risk notification...")
    manager.show_risk_notification(
        risk_level="medium",
        title="Suspicious Activity",
        message="This website may be unsafe. Proceed with caution.",
        url="https://suspicious-site.com"
    )
    time.sleep(3)
    
    # Test 3: High risk
    print("3. High risk notification...")
    manager.show_risk_notification(
        risk_level="high",
        title="Dangerous Website",
        message="This website is likely malicious. Do not enter personal information!",
        url="https://phishing-site.com"
    )
    time.sleep(3)
    
    # Test 4: URL alert
    print("4. URL alert...")
    manager.show_url_alert(
        risk_score=85,
        risk_types=[1, 3],  # Phishing + Impersonation
        url="https://fake-bank.com"
    )
    time.sleep(3)
    
    # Test 5: Remote access alert
    print("5. Remote access alert...")
    manager.show_remote_access_alert(
        app_name="AnyDesk",
        direction="incoming",
        is_startup=False
    )
    
    print("\nAll tests completed!")

"""
AntiScam Desktop App - System Tray
System tray icon and menu (like Tailscale)
"""

import os
import sys
import threading
from typing import Callable, Optional
import logging

# pystray for system tray
try:
    import pystray
    from pystray import MenuItem as item
    from PIL import Image, ImageDraw
    PYSTRAY_AVAILABLE = True
except ImportError:
    PYSTRAY_AVAILABLE = False
    print("Warning: pystray not installed. Run: pip install pystray pillow")

# customtkinter for popup
try:
    import customtkinter as ctk
    CTK_AVAILABLE = True
except ImportError:
    CTK_AVAILABLE = False

from ui.colors import ICON_COLORS
from tray_popup import TrayPopup
from notification_manager import NotificationManager
from ui import centered_toast

logger = logging.getLogger(__name__)


class TrayIcon:
    """System tray icon with menu and popup support"""

    def __init__(self):
        self.icon: Optional[pystray.Icon] = None
        self._root: Optional[ctk.CTk] = None  # Hidden root for popup
        self._popup: Optional[TrayPopup] = None  # Current popup instance

        self._connected = False
        self._extension_connected = False
        self._backend_connected = False
        self._on_dashboard: Optional[Callable] = None
        self._on_preferences: Optional[Callable] = None
        self._on_exit: Optional[Callable] = None
        self._on_stop_session: Optional[Callable] = None
        self._status_text = "Disconnected"
        self._user_email = None

        # Current status for popup
        self._protection_status = "Not Protected"
        self._protection_color = "gray"
        self._remote_tool = None
        self._remote_direction = None
        
        # Windows Toast Notification Manager
        self.notification_manager = NotificationManager()
        
    def _create_icon_image(self, color: str = "gray") -> 'Image':
        """Create a simple icon image"""
        # Create a 64x64 image
        size = 64
        image = Image.new('RGBA', (size, size), (0, 0, 0, 0))
        draw = ImageDraw.Draw(image)
        
        # Color based on status (using centralized ICON_COLORS)
        fill_color = ICON_COLORS.get(color, ICON_COLORS["gray"])
        
        # Draw a shield shape
        # Main body
        draw.ellipse([8, 8, 56, 56], fill=fill_color)
        
        # Inner circle (darker)
        inner_color = tuple(max(0, c - 50) for c in fill_color[:3]) + (255,)
        draw.ellipse([16, 16, 48, 48], fill=inner_color)
        
        # Center dot (white)
        draw.ellipse([26, 26, 38, 38], fill=(255, 255, 255, 255))
        
        return image

    def toggle_popup(self):
        """Toggle popup visibility. Must be called from main thread."""
        if self._root is None:
            return

        if self._popup and self._popup.winfo_exists():
            self._popup.destroy()
            self._popup = None
        else:
            self._show_popup()

    def _show_popup(self):
        """Create and show popup with current status."""
        self._popup = TrayPopup(
            self._root,
            on_exit=self._handle_exit,
            on_preferences=lambda i=None, j=None: self._handle_preferences(i, j),
            on_stop_session=self._handle_stop_session
        )
        # Apply current status
        self._popup.set_protection_status(self._protection_status, self._protection_color)
        self._popup.set_remote_access(self._remote_tool, self._remote_direction)
        self._popup.set_system_status(self._extension_connected, self._backend_connected)

    def _handle_stop_session(self):
        """Handle stop session button click."""
        if self._on_stop_session:
            self._on_stop_session()

    def on_stop_session(self, callback: Callable):
        """Set callback for stop session."""
        self._on_stop_session = callback

    def _menu_show_popup(self, icon, item):
        """Handle show status menu click."""
        if self._root:
            self._root.after(0, self.toggle_popup)

    def _get_menu(self) -> pystray.Menu:
        """Create the tray menu"""
        from config import VERSION

        return pystray.Menu(
            # Show popup (default action on left-click)
            item(
                "Show Status",
                self._menu_show_popup,
                default=True
            ),
            pystray.Menu.SEPARATOR,
            # Header
            item(
                f"AntiScam v{VERSION}",
                None,
                enabled=False
            ),
            pystray.Menu.SEPARATOR,
            # User info
            item(
                lambda text: f"📧 {self._user_email or 'Not signed in'}",
                None,
                enabled=False
            ),
            # Connection status
            item(
                lambda text: f"🌐 Extension: {'✅ Connected' if self._extension_connected else '❌ Disconnected'}",
                None,
                enabled=False
            ),
            item(
                lambda text: f"🔐 Backend: {'✅ Connected' if self._backend_connected else '❌ Disconnected'}",
                None,
                enabled=False
            ),
            # Overall status
            item(
                lambda text: f"Status: {self._status_text}",
                None,
                enabled=False
            ),
            pystray.Menu.SEPARATOR,
            # Actions
            item(
                "Dashboard...",
                self._handle_dashboard
            ),
            item(
                "Preferences",
                pystray.Menu(
                    item("Settings...", self._handle_preferences),
                    item("View Logs...", self._handle_view_logs),
                )
            ),
            item(
                "About...",
                self._handle_about
            ),
            pystray.Menu.SEPARATOR,
            item(
                "Exit",
                self._handle_exit
            )
        )
    
    def _handle_dashboard(self, icon, item):
        """Handle dashboard menu click"""
        if self._on_dashboard:
            self._on_dashboard()
    
    def _handle_preferences(self, icon, item):
        """Handle preferences menu click"""
        if self._on_preferences:
            self._on_preferences()
    
    def _handle_view_logs(self, icon, item):
        """Handle view logs click"""
        # Open logs folder
        from config import DATA_DIR
        import subprocess
        log_dir = os.path.expanduser(DATA_DIR)
        if os.path.exists(log_dir):
            if sys.platform == 'win32':
                subprocess.run(['explorer', log_dir])
            elif sys.platform == 'darwin':
                subprocess.run(['open', log_dir])
            else:
                subprocess.run(['xdg-open', log_dir])
    
    def _handle_about(self, icon, item):
        """Handle about menu click"""
        from config import VERSION
        # Simple message box would go here
        logger.info(f"AntiScam Desktop v{VERSION}")
    
    def _handle_exit(self, icon, item):
        """Handle exit menu click"""
        if self._on_exit:
            self._on_exit()
        self.stop()
    
    def on_dashboard(self, callback: Callable):
        """Set callback for dashboard menu"""
        self._on_dashboard = callback
        
    def on_preferences(self, callback: Callable):
        """Set callback for preferences menu"""
        self._on_preferences = callback
        
    def on_exit(self, callback: Callable):
        """Set callback for exit"""
        self._on_exit = callback
    
    def set_status(self, connected: bool = False, extension_connected: bool = False, backend_connected: bool = False):
        """Update connection status"""
        self._connected = connected
        self._extension_connected = extension_connected
        self._backend_connected = backend_connected

        # Determine overall status and color
        if backend_connected:
            color = "green"
            self._status_text = "✅ Protected"
            self._protection_status = "Protected"
            self._protection_color = "green"
        elif extension_connected:
            color = "yellow"
            self._status_text = "⚠️ Partially Protected"
            self._protection_status = "Partially Protected"
            self._protection_color = "yellow"
        else:
            color = "gray"
            self._status_text = "❌ Not Protected"
            self._protection_status = "Not Protected"
            self._protection_color = "gray"

        if self.icon:
            self.icon.icon = self._create_icon_image(color)
            self.icon.update_menu()

        # Update popup if open
        if self._popup and self._popup.winfo_exists():
            self._root.after(0, lambda: self._popup.set_protection_status(
                self._protection_status, self._protection_color
            ))
            self._root.after(0, lambda: self._popup.set_system_status(
                extension_connected, backend_connected
            ))
    
    def set_user(self, email: str):
        """Update user email"""
        self._user_email = email
        if self.icon:
            self.icon.update_menu()
    
    def set_alert(self, has_alert: bool = True):
        """Set alert status (red icon)"""
        if has_alert:
            if self.icon:
                self.icon.icon = self._create_icon_image("red")

    def set_remote_access(self, tool_name: str = None, direction: str = None):
        """Update remote access status."""
        self._remote_tool = tool_name
        self._remote_direction = direction

        # Update protection status if remote access is incoming
        if tool_name and direction == 'incoming':
            self._protection_status = "Remote control detected"
            self._protection_color = "red"
            if self.icon:
                self.icon.icon = self._create_icon_image("red")

        # Update popup if open
        if self._popup and self._popup.winfo_exists():
            self._root.after(0, lambda: self._popup.set_remote_access(tool_name, direction))
            if tool_name and direction == 'incoming':
                self._root.after(0, lambda: self._popup.set_protection_status(
                    "Remote control detected", "red"
                ))

    def show_centered_alert(
        self,
        title: str,
        message: str,
        risk_level: str = "critical",
        auto_dismiss_seconds: int = 15,
        on_view_details: Optional[Callable] = None,
    ) -> bool:
        """Backwards-compatible wrapper. Routes by risk_level: 'none' →
        cleared (green + Close button); other levels → locked (no close)."""
        if on_view_details is None:
            on_view_details = self.toggle_popup
        return centered_toast.show(
            root=self._root, title=title, message=message,
            risk_level=risk_level,
            auto_dismiss_seconds=auto_dismiss_seconds,
            on_view_details=on_view_details,
        )

    def show_locked_alert(
        self,
        title: str,
        message: str,
        risk_level: str = "critical",
        on_view_details: Optional[Callable] = None,
    ) -> bool:
        """Show or update the singleton LOCKED alert (red, no close button,
        always-on-top, draggable). Used for active ImmediateDanger."""
        if on_view_details is None:
            on_view_details = self._open_webapi_dashboard
        return centered_toast.show_locked(
            root=self._root, title=title, message=message,
            risk_level=risk_level, on_view_details=on_view_details,
        )

    def transform_alert_to_cleared(
        self,
        title: str,
        message: str,
        on_view_details: Optional[Callable] = None,
    ) -> bool:
        """Transform the active locked alert into a CLEARED (green) alert
        with a Close button. If no alert is active, shows a fresh one."""
        if on_view_details is None:
            on_view_details = self._open_webapi_dashboard
        return centered_toast.transform_to_cleared(
            root=self._root, title=title, message=message,
            auto_dismiss_seconds=0,  # require explicit Close
            on_view_details=on_view_details,
        )

    @staticmethod
    def _open_webapi_dashboard():
        """Open the WebApi dashboard in the user's default browser.
        Used as the default 'View Details' action from the centered alert
        (avoids the focus-loss conflict between the alert's lift loop and
        the tray popup's auto-close-on-FocusOut binding)."""
        import webbrowser
        try:
            from config import WEBAPI_URL
        except Exception:
            WEBAPI_URL = "http://localhost:5001"
        try:
            webbrowser.open(WEBAPI_URL)
        except Exception as e:
            logger.error(f"Failed to open WebApi dashboard: {e}")

    def show_notification(self, title: str, message: str, risk_level: str = "medium", action_buttons=None):
        """
        Show a Windows Toast notification.

        Args:
            title: Notification title
            message: Notification message
            risk_level: Risk level for styling ("none", "low", "medium", "high", "critical")
            action_buttons: optional list of (label, launch) tuples for toast buttons
        """
        try:
            # Use Windows Toast Notification Manager
            self.notification_manager.show_risk_notification(
                risk_level=risk_level,
                title=title,
                message=message,
                action_buttons=action_buttons,
            )
        except Exception as e:
            logger.error(f"Error showing toast notification: {e}")
            # Fallback to pystray notification
            if self.icon:
                try:
                    self.icon.notify(message, title)
                except Exception as e2:
                    logger.error(f"Error showing fallback notification: {e2}")
    
    def start(self):
        """Start the tray icon"""
        if not PYSTRAY_AVAILABLE:
            logger.error("pystray not available")
            return
            
        self.icon = pystray.Icon(
            "AntiScam",
            self._create_icon_image("gray"),
            "AntiScam",
            self._get_menu()
        )
        
        # Run in separate thread
        self.icon.run_detached()
        logger.info("Tray icon started")
    
    def stop(self):
        """Stop the tray icon and cleanup."""
        if self._popup:
            try:
                self._popup.destroy()
            except Exception:
                pass
            self._popup = None

        if self.icon:
            self.icon.stop()
            self.icon = None

        if self._root:
            try:
                self._root.quit()
                self._root.destroy()
            except Exception:
                pass
            self._root = None

        logger.info("Tray icon stopped")
    
    def run_blocking(self):
        """Run the tray icon (blocking) with popup support."""
        if not PYSTRAY_AVAILABLE:
            logger.error("pystray not available")
            return

        # Create hidden CTk root for popup
        if CTK_AVAILABLE:
            self._root = ctk.CTk()
            self._root.withdraw()
            ctk.set_appearance_mode("dark")

        # Create icon
        self.icon = pystray.Icon(
            "AntiScam",
            self._create_icon_image("gray"),
            "AntiScam",
            self._get_menu()
        )

        # Start pystray in detached mode so we can run tkinter mainloop
        self.icon.run_detached()

        logger.info("Starting tray icon with popup support")

        # Run tkinter mainloop (blocking) for popup support
        if self._root:
            self._root.mainloop()
        else:
            # Fallback if CTk not available - run pystray blocking
            self.icon.run()


# For standalone testing
if __name__ == "__main__":
    logging.basicConfig(level=logging.DEBUG)
    
    tray = TrayIcon()
    
    def on_dashboard():
        print("Dashboard clicked!")
        tray.set_status(True, True)
        
    def on_preferences():
        print("Preferences clicked!")
        
    def on_exit():
        print("Exit clicked!")
    
    tray.on_dashboard(on_dashboard)
    tray.on_preferences(on_preferences)
    tray.on_exit(on_exit)
    
    print("Starting tray icon...")
    print("Look for the icon in your system tray")
    
    tray.run_blocking()

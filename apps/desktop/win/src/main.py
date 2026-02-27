"""
AntiScam Desktop App - Main Application
Refactored with dependency injection and service layer

Communication:
- ZMQ REQ/REP: Send requests to backend
- ZMQ PUB/SUB: Receive notifications from backend
- WebSocket: Communicate with Chrome extension
"""

import asyncio
import json
import logging
import sys
import os
import threading
from typing import Optional

# Add src to path
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from config import VERSION, BACKEND_HOST, BACKEND_REQ_PORT, BACKEND_SUB_PORT
from core.container import Container
from hardware_id import get_or_create_device_id

# Setup logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)


class AntiScamApp:
    """
    Main application class
    Uses dependency injection container for components
    """

    def __init__(self, device_id: str):
        print("\n" + "=" * 60)
        print("[INIT] Initializing AntiScam Desktop App")
        print("=" * 60)

        # Initialize container
        self.container = Container(device_id)
        print(f"[INIT] Device ID: {self.container.device_id}")

        # State
        self._running = False
        self._notification_connected = False

        # Setup callbacks
        self._setup_callbacks()

        print("[INIT] Initialization complete!")
        print("=" * 60 + "\n")

    def _setup_callbacks(self):
        """Setup all callbacks"""
        # Extension server callback
        self.container.extension_server.on_message(self._handle_extension_message)

        # Tray callbacks
        self.container.tray_icon.on_dashboard(self._on_dashboard)
        self.container.tray_icon.on_preferences(self._on_preferences)
        self.container.tray_icon.on_exit(self._on_exit)
        self.container.tray_icon.on_stop_session(self._on_stop_session)

        # Notification callback
        self.container.notification_client.on_notification(
            self.container.notification_handler.handle
        )

    async def _handle_extension_message(self, data: dict) -> Optional[dict]:
        """Handle message from Chrome extension"""
        return await self.container.extension_handler.handle_message(data)

    def _on_dashboard(self):
        """Handle dashboard menu click"""
        stats = {
            'device_id': self.container.device_id,
            'authenticated': self.container.auth_manager.is_valid(),
            'token_expires': str(self.container.auth_manager.expires_at)
                if self.container.auth_manager.expires_at else None,
            'cache_stats': self.container.cache_manager.stats(),
            'notification_connected': self._notification_connected,
            'extension_connected': self.container.extension_server.client_count > 0
        }
        logger.info(f"Stats: {json.dumps(stats, indent=2)}")
        print(f"\n[DASHBOARD] Stats:\n{json.dumps(stats, indent=2)}\n")

    def _on_preferences(self):
        """Handle preferences menu click"""
        logger.info("Preferences requested")

    def _on_exit(self):
        """Handle exit menu click"""
        logger.info("Exit requested")
        self._running = False

    def _on_stop_session(self):
        """Handle stop session request from popup."""
        logger.info("Stop session requested")
        # TODO: Implement actual session termination
        # For now, just log the request
        # Future: Kill remote access process or send disconnect command

    def _start_notification_client(self):
        """Start Notification client in background thread"""
        print("[NOTIFY] Starting notification client...")
        try:
            self.container.notification_client.start()
            self._notification_connected = True
            print("[NOTIFY] Started! Listening for notifications...")
        except Exception as e:
            self._notification_connected = False
            print(f"[NOTIFY] Failed to start: {e}")

    async def start(self):
        """Start the application"""
        print("\n" + "=" * 60)
        print(f"   AntiScam Desktop v{VERSION}")
        print("=" * 60)

        self._running = True

        # Start extension server
        print("\n[STARTUP] Starting extension server...")
        if not await self.container.extension_server.start():
            print("[STARTUP] ERROR: Failed to start extension server!")
            return False
        print(f"[STARTUP] Extension server on port {self.container.extension_server.port}")

        # Connect notification handler to extension server for broadcasting results
        self.container.notification_handler.set_extension_server(self.container.extension_server)

        # Authenticate with backend
        print("\n[STARTUP] Authenticating with backend...")
        if self.container.auth_manager.ensure_authenticated():
            print("[STARTUP] Authenticated!")
            print(f"[STARTUP] Token: present")
            print(f"[STARTUP] User ID: {self.container.auth_manager.user_id}")
        else:
            print("[STARTUP] WARNING: Could not authenticate!")
            print("[STARTUP] Some features may not work.")

        # Always apply CURVE keys before starting notification client —
        # bootstrap public key from config ensures encrypted SUB even if auth failed
        self.container.apply_curve_keys()

        # Start Notification Client
        print("\n[STARTUP] Starting notification client...")
        notification_thread = threading.Thread(
            target=self._start_notification_client,
            daemon=True
        )
        notification_thread.start()

        # Start background monitors
        print("\n[STARTUP] Starting background monitors...")
        tasks = await self.container.monitor_service.start(
            self.container.extension_server
        )

        # Send initial remote access status
        print("\n[STARTUP] Sending initial remote access status...")
        await self.container.monitor_service.send_initial_status()

        # Update tray
        backend_connected = self.container.auth_manager.is_valid()
        self.container.tray_icon.set_status(
            connected=True,
            extension_connected=False,
            backend_connected=backend_connected
        )
        self.container.tray_icon.set_user(self.container.device_id)

        print("\n" + "=" * 60)
        print("   AntiScam Desktop is running!")
        print(f"   - Device: {self.container.device_id}")
        print(f"   - Extension: ws://localhost:{self.container.extension_server.port}")
        print(f"   - Backend ZMQ REQ: tcp://{BACKEND_HOST}:{BACKEND_REQ_PORT}")
        print(f"   - Backend ZMQ SUB: tcp://{BACKEND_HOST}:{BACKEND_SUB_PORT}")
        print(f"   - Connected: {self.container.auth_manager.is_valid()}")
        print("=" * 60 + "\n")

        logger.info("AntiScam Desktop started")

        # Wait until stopped
        while self._running:
            await asyncio.sleep(1)

        # Cleanup
        print("\n[SHUTDOWN] Stopping...")
        self.container.monitor_service.stop()
        for task in tasks:
            task.cancel()

        await self.container.extension_server.stop()
        self.container.notification_client.stop()
        self.container.zmq_client.destroy()

        print("[SHUTDOWN] AntiScam Desktop stopped")
        return True

    def run(self):
        """Run the application with tray icon"""
        def run_async():
            asyncio.run(self.start())

        # Run async in background thread
        async_thread = threading.Thread(target=run_async, daemon=True)
        async_thread.start()

        # Run tray in main thread (blocking)
        self.container.tray_icon.run_blocking()


def main():
    """Entry point"""
    device_id = get_or_create_device_id()
    app = AntiScamApp(device_id=device_id)

    try:
        app.run()
    except KeyboardInterrupt:
        logger.info("Interrupted by user")
    except Exception as e:
        logger.error(f"Fatal error: {e}")
        import traceback
        traceback.print_exc()
        raise


if __name__ == "__main__":
    main()

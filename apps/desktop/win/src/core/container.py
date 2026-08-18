"""
Dependency Injection Container
Centralizes component creation and dependencies
"""

import platform
from typing import Optional
from dataclasses import dataclass

from config import (
    VERSION, BACKEND_HOST, BACKEND_REQ_PORT, BACKEND_SUB_PORT,
    OperatingSystem, TRANSPORT_MODE, WS_URL
)


@dataclass
class DeviceInfo:
    """Device information"""
    id: str
    version: str
    ip: str
    user_agent: str
    timezone: int
    os_type: int

    def to_dict(self) -> dict:
        return {
            "id": self.id,
            "ver": self.version,
            "ip": self.ip,
            "userAgent": self.user_agent,
            "timezone": self.timezone,
            "OperatingSystem": self.os_type
        }


class Container:
    """
    Dependency Injection Container
    Creates and manages all application components
    """

    _instance: Optional['Container'] = None

    def __init__(self, device_id: str):
        self._device_id = device_id
        self._device_info: Optional[DeviceInfo] = None

        # Components (lazy loaded)
        self._zmq_client = None
        self._notification_client = None
        self._extension_server = None
        self._cache_manager = None
        self._remote_monitor = None
        self._browser_monitor = None
        self._event_logger = None
        self._auth_manager = None
        self._tray_icon = None

        # Handlers
        self._extension_handler = None
        self._notification_handler = None

        # Services
        self._scan_service = None
        self._protection_service = None
        self._monitor_service = None

    @classmethod
    def instance(cls, device_id: str) -> 'Container':
        """Get singleton instance"""
        if cls._instance is None:
            cls._instance = Container(device_id)
        return cls._instance

    @classmethod
    def reset(cls):
        """Reset singleton (for testing)"""
        cls._instance = None

    # ==========================================
    # Device Info
    # ==========================================

    @property
    def device_id(self) -> str:
        return self._device_id

    @property
    def device_info(self) -> DeviceInfo:
        if self._device_info is None:
            self._device_info = self._create_device_info()
        return self._device_info

    def _create_device_info(self) -> DeviceInfo:
        """Create device info"""
        system = platform.system()
        os_map = {
            "Windows": OperatingSystem.WINDOWS,
            "Linux": OperatingSystem.LINUX,
            "Darwin": OperatingSystem.MAC
        }
        os_type = os_map.get(system, OperatingSystem.WINDOWS)

        return DeviceInfo(
            id=self._device_id,
            version=VERSION,
            ip="",
            user_agent=f"AntiScamDesktop/{VERSION} ({platform.system()} {platform.release()})",
            timezone=2,
            os_type=os_type
        )

    # ==========================================
    # Core Components
    # ==========================================

    @property
    def zmq_client(self):
        """
        Backend request/response transport.

        TRANSPORT_MODE == "ws" (ASPS-721): a single WSClient instance is
        shared between zmq_client and notification_client — it combines both
        the request/response role (ZMQClient) and the notification-push role
        (NotificationClient) over one persistent WebSocket connection, per
        docs/architecture/WS-AGENT-PROTOCOL.md.

        TRANSPORT_MODE == "zmq" (default): the original direct ZMQ REQ client.
        """
        if TRANSPORT_MODE == "ws":
            return self._ws_client
        if self._zmq_client is None:
            from zmq_client import ZMQClient
            self._zmq_client = ZMQClient(BACKEND_HOST, BACKEND_REQ_PORT)
        return self._zmq_client

    @property
    def notification_client(self):
        """Notification client for receiving backend notifications.
        See zmq_client docstring for TRANSPORT_MODE == "ws" behavior."""
        if TRANSPORT_MODE == "ws":
            return self._ws_client
        if self._notification_client is None:
            from notification_client import NotificationClient
            self._notification_client = NotificationClient(
                self.device_id,
                BACKEND_HOST,
                BACKEND_SUB_PORT
            )
        return self._notification_client

    @property
    def _ws_client(self):
        """Lazily-created, shared WSClient instance (TRANSPORT_MODE == "ws")."""
        if self._zmq_client is None:
            from ws_client import WSClient
            self._zmq_client = WSClient(self.device_id, WS_URL)
            self._notification_client = self._zmq_client
        return self._zmq_client

    @property
    def extension_server(self):
        """WebSocket server for Chrome extension"""
        if self._extension_server is None:
            from extension_server import ExtensionServer
            self._extension_server = ExtensionServer()
        return self._extension_server

    @property
    def cache_manager(self):
        """URL cache manager"""
        if self._cache_manager is None:
            from cache_manager import CacheManager
            self._cache_manager = CacheManager()
        return self._cache_manager

    @property
    def remote_monitor(self):
        """Remote access app monitor"""
        if self._remote_monitor is None:
            from remote_monitor import RemoteAccessMonitor
            self._remote_monitor = RemoteAccessMonitor()
            self._remote_monitor.start_realtime_monitoring()
        return self._remote_monitor

    @property
    def browser_monitor(self):
        """Browser history monitor"""
        if self._browser_monitor is None:
            from browser_history import BrowserHistoryMonitor
            self._browser_monitor = BrowserHistoryMonitor()
        return self._browser_monitor

    @property
    def event_logger(self):
        """Event logger"""
        if self._event_logger is None:
            from event_logger import EventLogger
            self._event_logger = EventLogger()
        return self._event_logger

    @property
    def auth_manager(self):
        """Authentication manager"""
        if self._auth_manager is None:
            from auth_manager import AuthManager
            self._auth_manager = AuthManager(
                self.zmq_client,
                self.device_info.to_dict(),
                ""  # No email for ZMQ backend
            )
        return self._auth_manager

    @property
    def tray_icon(self):
        """System tray icon"""
        if self._tray_icon is None:
            from tray_icon import TrayIcon
            self._tray_icon = TrayIcon()
        return self._tray_icon

    def apply_curve_keys(self):
        """Apply server public key from auth_manager to notification client"""
        spk = self.auth_manager.server_public_key
        if spk:
            self.notification_client.set_server_public_key(spk)
            print(f"[CONTAINER] CURVE key applied to notification client")

    # ==========================================
    # Services
    # ==========================================

    @property
    def scan_service(self):
        """URL scanning service"""
        if self._scan_service is None:
            from services.scan_service import ScanService
            self._scan_service = ScanService(
                cache=self.cache_manager,
                zmq_client=self.zmq_client,
                auth_manager=self.auth_manager,
                browser_monitor=self.browser_monitor,
                event_logger=self.event_logger,
                device_id=self.device_id
            )
        return self._scan_service

    @property
    def protection_service(self):
        """Protection action service"""
        if self._protection_service is None:
            from services.protection_service import ProtectionService
            self._protection_service = ProtectionService(
                tray=self.tray_icon,
                cache=self.cache_manager
            )
        return self._protection_service

    @property
    def monitor_service(self):
        """Background monitoring service"""
        if self._monitor_service is None:
            from services.monitor_service import MonitorService
            self._monitor_service = MonitorService(
                remote_monitor=self.remote_monitor,
                browser_monitor=self.browser_monitor,
                auth_manager=self.auth_manager,
                zmq_client=self.zmq_client,
                tray=self.tray_icon,
                event_logger=self.event_logger,
                cache=self.cache_manager,
                device_id=self.device_id
            )
        return self._monitor_service

    # ==========================================
    # Handlers
    # ==========================================

    @property
    def extension_handler(self):
        """Extension message handler"""
        if self._extension_handler is None:
            from handlers.extension_handler import ExtensionHandler
            self._extension_handler = ExtensionHandler(
                scan_service=self.scan_service,
                auth_manager=self.auth_manager,
                device_id=self.device_id,
                remote_monitor=self.remote_monitor,
            )
        return self._extension_handler

    @property
    def notification_handler(self):
        """Backend notification handler"""
        if self._notification_handler is None:
            from handlers.notification_handler import NotificationHandler
            self._notification_handler = NotificationHandler(
                protection_service=self.protection_service,
                cache=self.cache_manager
            )
        return self._notification_handler

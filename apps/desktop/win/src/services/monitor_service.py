"""
Monitor Service
Handles background monitoring tasks
"""

import asyncio
import logging
from typing import Dict, Any

from config import MONITOR_INTERVAL, DEBUG_MODE, ConnectionStatus, REMOTE_ACCESS_BROWSER_TABS_MODE, BROWSER_TABS_URL_FILTER
from zmq_client import get_local_ip

logger = logging.getLogger(__name__)


class MonitorService:
    """
    Background monitoring service
    - Remote access monitoring
    - Browser history monitoring
    - Tray status updates
    """

    def __init__(
        self,
        remote_monitor,
        browser_monitor,
        auth_manager,
        zmq_client,
        tray,
        event_logger,
        cache,
        device_id: str
    ):
        self.remote_monitor = remote_monitor
        self.browser_monitor = browser_monitor
        self.auth_manager = auth_manager
        self.zmq_client = zmq_client
        self.tray = tray
        self.event_logger = event_logger
        self.cache = cache
        self.device_id = device_id

        # State
        self._running = False
        self._last_remote_status: Dict[str, Any] = {}
        self._local_ip: str = get_local_ip()

    async def start(self, extension_server):
        """Start all monitoring tasks"""
        self._running = True
        self._extension_server = extension_server

        tasks = [
            asyncio.create_task(self._monitor_remote_access()),
            asyncio.create_task(self._monitor_browser_history()),
            asyncio.create_task(self._update_tray_status()),
        ]

        return tasks

    def stop(self):
        """Stop monitoring"""
        self._running = False

    async def send_initial_status(self):
        """Send initial remote access status on startup using startup_scan()"""
        if not self.auth_manager or not self.auth_manager.is_valid():
            print("[MONITOR] Not authenticated, skipping initial report")
            return

        try:
            # Use startup_scan to get state changes with late_detection flag
            state_changes = self.remote_monitor.startup_scan()

            for change in state_changes:
                status = change.status
                print(f"[MONITOR] Startup scan: {change.app_name} - {change.change_type} "
                      f"(late_detection={change.late_detection})")

                if change.change_type == 'opened':
                    await self._handle_app_open(change.app_name, status, late_detection=True)
                elif change.change_type == 'session_started':
                    await self._handle_new_session(change.app_name, status, late_detection=True)

        except Exception as e:
            logger.error(f"Error sending initial status: {e}")
            print(f"[MONITOR] Error: {e}")

    async def _monitor_remote_access(self):
        """Monitor for remote access applications using state change tracking"""
        print("[MONITOR] Remote access monitor started")

        while self._running:
            try:
                # Use check_all_with_changes for state change tracking
                results, state_changes = self.remote_monitor.check_all_with_changes()

                # Process each state change
                for change in state_changes:
                    status = change.status

                    if change.change_type == 'opened':
                        await self._handle_app_open(change.app_name, status)
                    elif change.change_type == 'closed':
                        await self._handle_app_close(change.app_name, status)
                    elif change.change_type == 'session_started':
                        await self._handle_new_session(change.app_name, status)
                    elif change.change_type == 'session_ended':
                        await self._handle_session_end(change.app_name, status)

                # Update last status for reference
                self._last_remote_status = results

            except Exception as e:
                logger.error(f"Remote access monitor error: {e}")
                if DEBUG_MODE:
                    print(f"[MONITOR] Error: {e}")

            await asyncio.sleep(MONITOR_INTERVAL)

    @staticmethod
    def _apply_browser_tabs_filter(tabs: list) -> list:
        """
        Filter out tabs whose URL starts with (or exactly equals) any entry in
        BROWSER_TABS_URL_FILTER.  An empty-string entry removes tabs with no URL.
        """
        if not BROWSER_TABS_URL_FILTER:
            return tabs

        result = []
        for tab in tabs:
            url = tab.get('url', '')
            blocked = False
            for pattern in BROWSER_TABS_URL_FILTER:
                if pattern == '':
                    if url == '':
                        blocked = True
                        break
                elif url.startswith(pattern):
                    blocked = True
                    break
            if not blocked:
                result.append(tab)
        return result

    async def _get_browser_tabs_for_alert(self, has_active_session: bool, direction: str = "unknown"):
        """
        Returns a list of open browser tabs (queried from the extension) if the
        current config mode warrants it, or None to omit the field entirely.

        Modes (REMOTE_ACCESS_BROWSER_TABS_MODE):
          "always"              – include tabs with every RemoteAccessAlert
          "active_session_only" – only when an INCOMING session is actively controlling this device
          "never"               – never include tabs
        """
        mode = REMOTE_ACCESS_BROWSER_TABS_MODE

        if mode == 'never':
            return None

        # In active_session_only mode we only care about incoming sessions —
        # i.e., someone remotely controlling THIS device (not outgoing/unknown).
        should_query = (
            mode == 'always' or
            (mode == 'active_session_only' and has_active_session and direction == 'incoming')
        )
        if not should_query:
            return None

        # No extension connected — wait briefly for extension to connect before giving up
        if not hasattr(self, '_extension_server') or not self._extension_server:
            return []
        if not self._extension_server.clients:
            # Extension may still be starting up — wait up to 2s in 0.25s intervals
            for _ in range(8):
                await asyncio.sleep(0.25)
                if self._extension_server.clients:
                    break
            if not self._extension_server.clients:
                print("[MONITOR] No extension connected — BrowserTabs will be empty")
                return []

        try:
            tabs = await self._extension_server.request_browser_tabs(timeout=3.0)
            filtered = self._apply_browser_tabs_filter(tabs)
            print(f"[MONITOR] Collected {len(tabs)} browser tab(s), "
                  f"{len(filtered)} after URL filter, for RemoteAccessAlert")
            return filtered
        except Exception as e:
            logger.error(f"Error querying browser tabs: {e}")
            return []

    async def _handle_app_open(self, app_name: str, status, late_detection: bool = False):
        """Handle remote access app opening (not yet session)"""
        detection_note = " (detected on startup)" if late_detection else ""
        print(f"[MONITOR] App opened: {app_name}{detection_note}")
        logger.info(f"Remote app opened: {app_name}{detection_note}")

        self.event_logger.log_event('RemoteAccessOpened', {
            'app': app_name,
            'app_id': status.app_id,
            'process_count': status.process_count,
            'direction': status.direction,
            'confidence': status.confidence,
            'late_detection': late_detection
        })

        # Send alert if authenticated (session_status='0' since no active session yet)
        if self.auth_manager.is_valid():
            if DEBUG_MODE:
                print(f"[MONITOR] Sending app open alert for {app_name}...")

            browser_tabs = await self._get_browser_tabs_for_alert(has_active_session=False)
            await self._send_remote_access_alert_with_retry(
                device_uid=self.device_id,
                remote_app=str(status.app_id),
                running_processes=status.process_count,
                connection_url="",
                connection_status=str(status.connection_status),
                session_status=str(0),
                direction=status.direction or "unknown",
                confidence=status.confidence or "low",
                remote_country=status.remote_country or "",
                remote_country_code=status.remote_country_code or "",
                browser_tabs=browser_tabs,
                ip_address=self._local_ip,
                remote_os=getattr(status, 'remote_os', '') or "",
                remote_version=getattr(status, 'remote_version', '') or "",
                connection_type=getattr(status, 'connection_type', '') or "",
                file_transfer_active=getattr(status, 'file_transfer_active', False),
                file_transfers=getattr(status, 'file_transfers', 0)
            )

    async def _handle_app_close(self, app_name: str, status):
        """Handle remote access app closing"""
        print(f"[MONITOR] App closed: {app_name}")
        logger.info(f"Remote app closed: {app_name}")

        self.event_logger.log_event('RemoteAccessClosed', {
            'app': app_name,
            'app_id': status.app_id,
            'direction': status.direction,
            'confidence': status.confidence
        })

        # Send alert if authenticated with connection_status='2' (CLOSED)
        if self.auth_manager.is_valid():
            if DEBUG_MODE:
                print(f"[MONITOR] Sending app close alert for {app_name}...")

            browser_tabs = await self._get_browser_tabs_for_alert(has_active_session=False)
            await self._send_remote_access_alert_with_retry(
                device_uid=self.device_id,
                remote_app=str(status.app_id),
                running_processes=0,
                connection_url="",
                connection_status=str(ConnectionStatus.CLOSED),
                session_status=str(0),
                direction=status.direction or "unknown",
                confidence=status.confidence or "low",
                remote_country=status.remote_country or "",
                remote_country_code=status.remote_country_code or "",
                browser_tabs=browser_tabs,
                ip_address=self._local_ip,
                remote_os=getattr(status, 'remote_os', '') or "",
                remote_version=getattr(status, 'remote_version', '') or "",
                connection_type=getattr(status, 'connection_type', '') or "",
                file_transfer_active=getattr(status, 'file_transfer_active', False),
                file_transfers=getattr(status, 'file_transfers', 0)
            )

        # Clear alert state
        self.tray.set_alert(False)

        # Clear remote access from tray popup (app closed entirely)
        self.tray.set_remote_access(None, None)

        # Broadcast app close to extension
        if hasattr(self, '_extension_server') and self._extension_server:
            await self._extension_server.broadcast({
                'type': 'remote_access_app_closed',
                'toolId': status.app_id,
                'toolName': app_name
            })
            if DEBUG_MODE:
                print(f"[MONITOR] Broadcast app close to extension")

    async def _send_remote_access_alert_with_retry(
        self,
        device_uid: str,
        remote_app: str,
        running_processes: int,
        connection_url: str,
        connection_status: str,
        session_status: str,
        direction: str,
        confidence: str,
        remote_country: str,
        remote_country_code: str,
        browser_tabs=None,
        ip_address: str = "",
        remote_os: str = "",
        remote_version: str = "",
        connection_type: str = "",
        file_transfer_active: bool = False,
        file_transfers: int = 0,
        retry: bool = True
    ):
        """Send remote access alert with automatic retry on auth errors"""
        token = self.auth_manager.get_token()
        print(f"[MONITOR-RETRY] Sending alert. Token: {'[REDACTED]' if token else 'None'}, retry={retry}")

        response = await asyncio.to_thread(
            self.zmq_client.send_remote_access_alert,
            device_uid=device_uid,
            remote_app=remote_app,
            running_processes=running_processes,
            connection_url=connection_url,
            connection_status=connection_status,
            session_status=session_status,
            token=token,
            direction=direction,
            confidence=confidence,
            remote_country=remote_country,
            remote_country_code=remote_country_code,
            browser_tabs=browser_tabs,
            ip_address=ip_address,
            remote_os=remote_os,
            remote_version=remote_version,
            connection_type=connection_type,
            file_transfer_active=file_transfer_active,
            file_transfers=file_transfers
        )

        if response:
            print(f"[MONITOR-RETRY] Server response status: {response.get('status', 'N/A')}")

            # Handle auth errors with retry
            if response.get('status') in ('InvalidToken', 'TokenExpired'):
                print(f"[MONITOR-RETRY] Token issue: {response.get('status')}, re-authenticating...")
                auth_result = self.auth_manager.handle_auth_response(response)
                print(f"[MONITOR-RETRY] Re-auth result: {auth_result}")
                if retry and auth_result:
                    new_token = self.auth_manager.get_token()
                    print(f"[MONITOR-RETRY] New token after re-auth: {'[REDACTED]' if new_token else 'None'}")
                    print("[MONITOR-RETRY] Retrying alert with new token...")
                    return await self._send_remote_access_alert_with_retry(
                        device_uid=device_uid,
                        remote_app=remote_app,
                        running_processes=running_processes,
                        connection_url=connection_url,
                        connection_status=connection_status,
                        session_status=session_status,
                        direction=direction,
                        confidence=confidence,
                        remote_country=remote_country,
                        remote_country_code=remote_country_code,
                        browser_tabs=browser_tabs,
                        ip_address=ip_address,
                        remote_os=remote_os,
                        remote_version=remote_version,
                        connection_type=connection_type,
                        file_transfer_active=file_transfer_active,
                        file_transfers=file_transfers,
                        retry=False
                    )
                else:
                    print(f"[MONITOR-RETRY] Not retrying: retry={retry}, auth_result={auth_result}")

        return response

    async def _handle_new_session(self, app_name: str, status, late_detection: bool = False):
        """Handle newly detected remote access session"""
        detection_note = " (detected on startup)" if late_detection else ""
        print(f"\n[MONITOR] ALERT! Active session: {app_name}{detection_note}")
        logger.warning(f"Active session detected: {app_name}{detection_note}")

        self.event_logger.log_event('RemoteAccessDetected', {
            'app': app_name,
            'app_id': status.app_id,
            'process_count': status.process_count,
            'direction': status.direction,
            'confidence': status.confidence,
            'remote_ip': status.remote_ip,
            'remote_country': status.remote_country,
            'late_detection': late_detection
        })

        # Send alert if authenticated
        if self.auth_manager.is_valid():
            if DEBUG_MODE:
                print("[MONITOR] Sending RemoteAccessAlert...")

            browser_tabs = await self._get_browser_tabs_for_alert(
                has_active_session=True,
                direction=status.direction or "unknown"
            )
            await self._send_remote_access_alert_with_retry(
                device_uid=self.device_id,
                remote_app=str(status.app_id),
                running_processes=status.process_count,
                connection_url=status.remote_ip or "",
                connection_status=str(status.connection_status),
                session_status=str(1),
                direction=status.direction or "unknown",
                confidence=status.confidence or "low",
                remote_country=status.remote_country or "",
                remote_country_code=status.remote_country_code or "",
                browser_tabs=browser_tabs,
                ip_address=self._local_ip,
                remote_os=getattr(status, 'remote_os', '') or "",
                remote_version=getattr(status, 'remote_version', '') or "",
                connection_type=getattr(status, 'connection_type', '') or "",
                file_transfer_active=getattr(status, 'file_transfer_active', False),
                file_transfers=getattr(status, 'file_transfers', 0)
            )

        # Show notification with direction-aware message
        self.tray.set_alert(True)
        if status.direction == 'incoming':
            notification_msg = f"{app_name.upper()} - INCOMING connection detected! Someone may be controlling your computer."
        else:
            notification_msg = f"{app_name.upper()} has an active session"
        if late_detection:
            notification_msg += " (detected on startup)"
        self.tray.show_notification(
            "Remote Access Detected",
            notification_msg
        )

        # Update tray popup with remote access info
        self.tray.set_remote_access(app_name, status.direction or 'unknown')

        # Broadcast to extension for warning display
        if hasattr(self, '_extension_server') and self._extension_server:
            await self._extension_server.broadcast({
                'type': 'remote_access_alert',
                'toolId': status.app_id,
                'toolName': app_name,
                'direction': status.direction or 'unknown',
                'remoteIP': status.remote_ip or '',
                'remote_country': status.remote_country or '',
                'remote_country_code': status.remote_country_code or '',
                'confidence': status.confidence or 'low',
                'session_active': True
            })
            if DEBUG_MODE:
                print(f"[MONITOR] Broadcast remote_access_alert to extension")

    async def _handle_session_end(self, app_name: str, status):
        """Handle remote access session ending (app still running)"""
        print(f"[MONITOR] Session ended: {app_name}")
        logger.info(f"Remote session ended: {app_name}")

        self.event_logger.log_event('RemoteSessionEnded', {
            'app': app_name,
            'app_id': status.app_id,
            'direction': status.direction,
            'confidence': status.confidence
        })

        # Send alert if authenticated with session_status='0' (ended)
        if self.auth_manager.is_valid():
            if DEBUG_MODE:
                print(f"[MONITOR] Sending session end alert for {app_name}...")

            browser_tabs = await self._get_browser_tabs_for_alert(has_active_session=False)
            await self._send_remote_access_alert_with_retry(
                device_uid=self.device_id,
                remote_app=str(status.app_id),
                running_processes=status.process_count,
                connection_url=status.remote_ip or "",
                connection_status=str(status.connection_status),
                session_status=str(0),
                direction=status.direction or "unknown",
                confidence=status.confidence or "low",
                remote_country=status.remote_country or "",
                remote_country_code=status.remote_country_code or "",
                browser_tabs=browser_tabs,
                ip_address=self._local_ip,
                remote_os=getattr(status, 'remote_os', '') or "",
                remote_version=getattr(status, 'remote_version', '') or "",
                connection_type=getattr(status, 'connection_type', '') or "",
                file_transfer_active=getattr(status, 'file_transfer_active', False),
                file_transfers=getattr(status, 'file_transfers', 0)
            )

        # Clear alert state
        self.tray.set_alert(False)

        # Clear remote access from tray popup
        self.tray.set_remote_access(None, None)

        # Broadcast session end to extension
        if hasattr(self, '_extension_server') and self._extension_server:
            await self._extension_server.broadcast({
                'type': 'remote_access_session_end',
                'toolId': status.app_id,
                'toolName': app_name
            })
            if DEBUG_MODE:
                print(f"[MONITOR] Broadcast session end to extension")

    async def _send_url_alert_with_retry(self, url: str, retry: bool = True):
        """Send URL alert with automatic retry on auth errors"""
        token = self.auth_manager.get_token()

        response = await asyncio.to_thread(
            self.zmq_client.send_url_alert,
            device_uid=self.device_id,
            url=url,
            token=token,
            trackers=[],
            iframes=[]
        )

        if response:
            # Handle auth errors with retry
            if response.get('status') in ('InvalidToken', 'TokenExpired'):
                print(f"[MONITOR] Token issue: {response.get('status')}, re-authenticating...")
                if retry and self.auth_manager.handle_auth_response(response):
                    print("[MONITOR] Re-authenticated, retrying URL alert...")
                    return await self._send_url_alert_with_retry(url=url, retry=False)

        return response

    async def _monitor_browser_history(self):
        """Monitor browser history for new URLs"""
        print("[MONITOR] Browser history monitor started")

        while self._running:
            try:
                new_entries = self.browser_monitor.get_new_entries()

                for entry in new_entries:
                    if self.browser_monitor.is_url_seen(entry.url):
                        continue
                    if self.cache.has(entry.url):
                        continue

                    logger.debug(f"New URL from history: {entry.url}")

                    # Send if authenticated (with retry on auth errors)
                    if self.auth_manager.is_valid():
                        await self._send_url_alert_with_retry(entry.url)

            except Exception as e:
                logger.error(f"Browser history monitor error: {e}")

            await asyncio.sleep(30)

    async def _update_tray_status(self):
        """Periodically update tray icon status"""
        while self._running:
            backend_connected = self.auth_manager.is_valid() if self.auth_manager else False
            extension_connected = self._extension_server.client_count > 0 if self._extension_server else False

            self.tray.set_status(
                connected=True,
                extension_connected=extension_connected,
                backend_connected=backend_connected
            )

            self.tray.set_user(self.device_id)

            await asyncio.sleep(5)

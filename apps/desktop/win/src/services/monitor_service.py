"""
Monitor Service
Handles background monitoring tasks
"""

import asyncio
import logging
from typing import Dict, Any, Optional

from config import (
    MONITOR_INTERVAL, DEBUG_MODE, ConnectionStatus, SessionStatus,
    BROWSER_TABS_URL_FILTER, IMMEDIATE_DANGER_ALERT_INTERVAL_SECONDS,
)
from services.browser_tabs_policy import policy as browser_tabs_policy
from services.danger_mode import danger_mode
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

        # ImmediateDanger periodic-alert loop bookkeeping. The task is
        # started by start_immediate_danger_loop() (called from
        # NotificationHandler when ImmediateDangerNotification arrives) and
        # stopped by stop_immediate_danger_loop() on the Ended notification.
        self._immediate_danger_task: Optional[asyncio.Task] = None

        # Tracks whether the agent's last RemoteAccessAlert reported an active
        # incoming session (session=open + direction=incoming). The Chrome
        # extension uses this gate to decide whether to push fine-grained
        # TabChangedAlert / TabClosedAlert during the session — see
        # _broadcast_is_device_remote_controlled_if_changed.
        self._is_device_remote_controlled: bool = False

    async def start(self, extension_server):
        """Start all monitoring tasks"""
        self._running = True
        self._extension_server = extension_server

        # When a Chrome extension client attaches AFTER startup, immediately
        # re-emit a RemoteAccessAlert with BrowserTabs if there's an active
        # incoming session. This fixes the race where the agent's startup
        # scan fires the first RA alert BEFORE the extension has connected
        # (so BrowserTabs=[] permanently — backend never gets the tabs).
        if hasattr(extension_server, 'on_client_connect'):
            extension_server.on_client_connect(self._on_extension_client_connected)

        tasks = [
            asyncio.create_task(self._monitor_remote_access()),
            asyncio.create_task(self._monitor_browser_history()),
            asyncio.create_task(self._update_tray_status()),
        ]

        return tasks

    async def _on_extension_client_connected(self):
        """Handler fired by ExtensionServer when a client attaches.
        Re-emits a RemoteAccessAlert with fresh BrowserTabs if there's an
        active incoming session right now. Idempotent — if no incoming
        session, does nothing (apart from logging)."""
        try:
            results = self.remote_monitor.check_all()
        except Exception as e:
            logger.warning(f"on_extension_client_connected: check_all failed: {e}")
            return

        # Find an active incoming session (the alerts that need BrowserTabs)
        target = None
        for app_name, status in results.items():
            if status.has_active_session and (status.direction or '').lower() == 'incoming':
                target = (app_name, status)
                break

        if target is None:
            print("[MONITOR][EXT-CONNECT] no active incoming session — nothing to refresh")
            return

        app_name, status = target
        print(f"[MONITOR][EXT-CONNECT] extension attached + active incoming "
              f"{app_name} session → re-emitting RemoteAccessAlert with tabs (in 2s)")

        # Chrome MV3 service workers wake up lazily — the WebSocket open event
        # arrives before the extension finishes registering its onMessage
        # handlers (we observed user_auth arriving ~5s after the WS connect).
        # Wait 2s so the get_browser_tabs query lands on a responsive worker,
        # then retry once on timeout in case 2s wasn't enough.
        await asyncio.sleep(2.0)

        attempts = 2
        for attempt in range(1, attempts + 1):
            try:
                browser_tabs = await self._get_browser_tabs_for_alert(
                    has_active_session=True,
                    direction=status.direction or "unknown",
                )
                if browser_tabs:
                    print(f"[MONITOR][EXT-CONNECT] attempt {attempt}/{attempts}: "
                          f"got {len(browser_tabs)} tabs — emitting alert")
                    break
                else:
                    print(f"[MONITOR][EXT-CONNECT] attempt {attempt}/{attempts}: "
                          f"got {len(browser_tabs or [])} tabs — "
                          f"{'retrying after 2s' if attempt < attempts else 'giving up, sending without tabs'}")
                    if attempt < attempts:
                        await asyncio.sleep(2.0)
            except Exception as e:
                logger.warning(f"on_extension_client_connected attempt {attempt}: tab query failed: {e}")
                browser_tabs = []
                break

        # Now refresh as a "new session" — _handle_new_session will re-query
        # tabs (now warm) and emit a fresh RA alert with them attached.
        try:
            await self._handle_new_session(app_name, status, late_detection=True)
        except Exception as e:
            logger.warning(f"on_extension_client_connected: re-emit failed: {e}")

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
        """Monitor for remote access applications using state change tracking.

        Uses an adaptive poll interval (driven by RemoteAccessMonitor):
          - 1s   when a debounced close/session-end is ticking (so the alert
                 fires within ~1s of the underlying state change)
          - 5s   when any remote-access app is running or has an active session
          - 30s  when idle (no remote apps detected)

        Override: while DangerMode is active (between ImmediateDangerNotification
        and ImmediateDangerEndedNotification), polls every 2s regardless of
        adaptive recommendation.
        """
        print("[MONITOR] Remote access monitor started (adaptive interval)")

        results: Dict[str, Any] = {}

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

            # Adaptive sleep: shorter when something is happening, longer when idle.
            # Falls back to MONITOR_INTERVAL if the monitor doesn't expose the helper.
            # DangerMode override: fixed fast cadence while ImmediateDanger is active.
            if danger_mode.active:
                interval = danger_mode.POLL_INTERVAL_SECONDS
            else:
                try:
                    interval = self.remote_monitor.get_next_poll_interval(last_results=results)
                except AttributeError:
                    interval = MONITOR_INTERVAL

            if DEBUG_MODE:
                logger.debug(f"[MONITOR] next poll in {interval:.1f}s "
                             f"{'(danger)' if danger_mode.active else ''}")

            await asyncio.sleep(interval)

    # ─── ImmediateDanger periodic alert loop ─────────────────────────────
    def start_immediate_danger_loop(self) -> None:
        """Spawn the periodic RemoteAccessAlert task. Idempotent — calling
        twice is a no-op while a task is already running."""
        if self._immediate_danger_task and not self._immediate_danger_task.done():
            return
        try:
            loop = asyncio.get_running_loop()
        except RuntimeError:
            logger.warning("start_immediate_danger_loop: no running loop")
            return
        self._immediate_danger_task = loop.create_task(self._immediate_danger_loop())
        print(f"[MONITOR] ImmediateDanger loop STARTED — interval {IMMEDIATE_DANGER_ALERT_INTERVAL_SECONDS}s")

    def stop_immediate_danger_loop(self) -> None:
        """Cancel the periodic task. The loop body also self-terminates
        when danger_mode.active becomes False, so this is belt-and-suspenders."""
        task = self._immediate_danger_task
        self._immediate_danger_task = None
        if task and not task.done():
            task.cancel()
            print("[MONITOR] ImmediateDanger loop STOPPED")

    async def _immediate_danger_loop(self):
        """Every IMMEDIATE_DANGER_ALERT_INTERVAL_SECONDS while in danger mode,
        query browser tabs from the extension and emit a fresh
        RemoteAccessAlert. Stops on danger_mode.deactivate() OR cancellation."""
        try:
            while self._running and danger_mode.active:
                try:
                    await self._emit_immediate_danger_alert()
                except Exception as e:
                    logger.error(f"ImmediateDanger periodic alert error: {e}")
                    if DEBUG_MODE:
                        print(f"[MONITOR] ImmediateDanger error: {e}")
                await asyncio.sleep(IMMEDIATE_DANGER_ALERT_INTERVAL_SECONDS)
        except asyncio.CancelledError:
            pass

    async def _emit_immediate_danger_alert(self):
        """Build and send one RemoteAccessAlert for the active incoming
        remote-access session (if any), with fresh BrowserTabs attached."""
        if not self.auth_manager.is_valid():
            return

        # Find the most likely active session — prefer incoming, with an
        # active session, fall back to any running app.
        results = self.remote_monitor.check_all()
        target = None
        for app_name, status in results.items():
            if not status.is_running:
                continue
            if status.has_active_session and (status.direction or '').lower() == 'incoming':
                target = (app_name, status)
                break
        if target is None:
            for app_name, status in results.items():
                if status.has_active_session:
                    target = (app_name, status)
                    break
        if target is None:
            if DEBUG_MODE:
                print("[MONITOR] ImmediateDanger tick: no active RA session — skipping")
            return

        app_name, status = target
        # Always query tabs in danger mode (regardless of policy) — they're the
        # whole reason the danger fired.
        browser_tabs = []
        if hasattr(self, '_extension_server') and self._extension_server \
                and self._extension_server.clients:
            try:
                tabs = await self._extension_server.request_browser_tabs(timeout=5.0)
                browser_tabs = self._apply_browser_tabs_filter(tabs)
            except Exception as e:
                logger.warning(f"ImmediateDanger tab query failed: {e}")

        await self._send_remote_access_alert_with_retry(
            device_uid=self.device_id,
            remote_app=str(status.app_id),
            running_processes=status.process_count,
            connection_url=status.remote_ip or "",
            connection_status=str(status.connection_status),
            session_status=str(SessionStatus.OPEN),
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
            file_transfers=getattr(status, 'file_transfers', 0),
            remote_id=getattr(status, 'remote_id', '') or "",
            remote_name=getattr(status, 'remote_name', '') or "",
            logged_user=getattr(status, 'logged_user', '') or "",
            connection_id=getattr(status, 'connection_id', '') or "",
            software=getattr(status, 'software', '') or "",
        )
        if DEBUG_MODE:
            print(f"[MONITOR] ImmediateDanger periodic alert sent for {app_name} "
                  f"({len(browser_tabs)} tabs)")

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
        Returns a list of open browser tabs (queried from the extension) based
        on the current BrowserTabsPolicy, or None to omit the field.

        Effective policy (runtime, may be overridden by backend notification):
          'incoming_only' (default) - include only when direction == 'incoming'
          'always'                  - include with every alert
          'never'                   - never include
        """
        mode = browser_tabs_policy.get_effective_mode()
        norm_dir = (direction or '').lower()
        ext_attached = hasattr(self, '_extension_server') and self._extension_server is not None
        client_count = (
            len(self._extension_server.clients) if ext_attached and self._extension_server.clients is not None else 0
        )
        print(f"[MONITOR][TABS] entry: mode={mode}, direction={norm_dir}, "
              f"has_active_session={has_active_session}, extension_attached={ext_attached}, "
              f"clients={client_count}")

        if mode == 'never':
            print("[MONITOR][TABS] policy=never → returning None")
            return None
        if mode == 'incoming_only' and norm_dir != 'incoming':
            print(f"[MONITOR][TABS] policy=incoming_only and direction={norm_dir!r} → returning None")
            return None
        # 'always' and ('incoming_only' AND direction=='incoming') fall through

        # No extension connected — wait briefly for extension to connect before giving up
        if not ext_attached:
            print("[MONITOR][TABS] _extension_server is None — returning []")
            return []
        if not self._extension_server.clients:
            print("[MONITOR][TABS] no extension clients yet — waiting up to 2s …")
            for _ in range(8):
                await asyncio.sleep(0.25)
                if self._extension_server.clients:
                    break
            if not self._extension_server.clients:
                print("[MONITOR][TABS] No extension connected after wait — BrowserTabs will be empty")
                return []
            print(f"[MONITOR][TABS] extension connected during wait — clients={len(self._extension_server.clients)}")

        try:
            tabs = await self._extension_server.request_browser_tabs(timeout=5.0)
            filtered = self._apply_browser_tabs_filter(tabs)
            print(f"[MONITOR][TABS] Collected {len(tabs)} browser tab(s), "
                  f"{len(filtered)} after URL filter, for RemoteAccessAlert")
            if tabs and not filtered:
                # Helpful: show which URLs the filter dropped
                urls = [(t.get('url') or '')[:80] for t in tabs]
                print(f"[MONITOR][TABS] filter dropped all tabs. URLs were: {urls}")
            return filtered
        except Exception as e:
            logger.error(f"Error querying browser tabs: {e}")
            return []

    async def _handle_app_open(self, app_name: str, status, late_detection: bool = False):
        """Handle remote access app opening.

        SessionStatus reflects the current state at detection time:
          - has_active_session=True  → SessionStatus.OPEN  (covers the
              "agent started mid-session" case where DebouncedStateTracker
              fires only an 'opened' event because there's no prior
              has_active_session=False state to transition from).
          - has_active_session=False → SessionStatus.UNKNOWN (app process
              running but no incoming/outgoing session yet).
        """
        detection_note = " (detected on startup)" if late_detection else ""
        has_session = bool(getattr(status, 'has_active_session', False))
        print(f"[MONITOR] App opened: {app_name}{detection_note} "
              f"(has_active_session={has_session})")
        logger.info(f"Remote app opened: {app_name}{detection_note} "
                    f"has_active_session={has_session}")

        self.event_logger.log_event('RemoteAccessOpened', {
            'app': app_name,
            'app_id': status.app_id,
            'process_count': status.process_count,
            'direction': status.direction,
            'confidence': status.confidence,
            'late_detection': late_detection,
            'has_active_session': has_session,
        })

        if self.auth_manager.is_valid():
            if DEBUG_MODE:
                print(f"[MONITOR] Sending app open alert for {app_name}...")

            session_status_value = (
                SessionStatus.OPEN if has_session else SessionStatus.UNKNOWN
            )

            browser_tabs = await self._get_browser_tabs_for_alert(
                has_active_session=has_session,
                direction=status.direction or "unknown",
            )
            await self._send_remote_access_alert_with_retry(
                device_uid=self.device_id,
                remote_app=str(status.app_id),
                running_processes=status.process_count,
                connection_url=getattr(status, 'remote_ip', '') or "",
                connection_status=str(status.connection_status),
                session_status=str(session_status_value),
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
                file_transfers=getattr(status, 'file_transfers', 0),
                remote_id=getattr(status, 'remote_id', '') or "",
                remote_name=getattr(status, 'remote_name', '') or "",
                logged_user=getattr(status, 'logged_user', '') or "",
                connection_id=getattr(status, 'connection_id', '') or "",
                software=getattr(status, 'software', '') or "",
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

        # Send alert if authenticated (app closed → ConnectionStatus.CLOSED, SessionStatus.CLOSED)
        if self.auth_manager.is_valid():
            if DEBUG_MODE:
                print(f"[MONITOR] Sending app close alert for {app_name}...")

            browser_tabs = await self._get_browser_tabs_for_alert(
                has_active_session=False,
                direction=status.direction or "unknown",
            )
            await self._send_remote_access_alert_with_retry(
                device_uid=self.device_id,
                remote_app=str(status.app_id),
                running_processes=0,
                connection_url="",
                connection_status=str(ConnectionStatus.CLOSED),
                session_status=str(SessionStatus.CLOSED),
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
                file_transfers=getattr(status, 'file_transfers', 0),
                remote_id=getattr(status, 'remote_id', '') or "",
                remote_name=getattr(status, 'remote_name', '') or "",
                logged_user=getattr(status, 'logged_user', '') or "",
                connection_id=getattr(status, 'connection_id', '') or "",
                software=getattr(status, 'software', '') or "",
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

    async def _broadcast_is_device_remote_controlled_if_changed(self, controlled: bool):
        """Push the IsDeviceRemoteControlled flag to the extension when it
        changes. Extension uses this as a gate: while true, every URL change
        in a sensitive tab is reported via TabChangedAlert (otherwise it's
        kept silent to avoid noise during normal browsing)."""
        if controlled == self._is_device_remote_controlled:
            return
        self._is_device_remote_controlled = controlled
        if not (hasattr(self, '_extension_server') and self._extension_server):
            return
        try:
            await self._extension_server.broadcast({
                'type': 'set_remote_controlled',
                'isDeviceRemoteControlled': controlled,
            })
            print(f"[MONITOR] IsDeviceRemoteControlled -> {controlled} (broadcast to extension)")
        except Exception as e:
            logger.warning(f"Failed to broadcast IsDeviceRemoteControlled: {e}")

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
        remote_id: str = "",
        remote_name: str = "",
        logged_user: str = "",
        connection_id: str = "",
        software: str = "",
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
            file_transfers=file_transfers,
            remote_id=remote_id,
            remote_name=remote_name,
            logged_user=logged_user,
            connection_id=connection_id,
            software=software,
        )

        # Compute & broadcast the IsDeviceRemoteControlled flag right after a
        # send (regardless of server response — what matters is what the agent
        # is *asserting*). True iff active incoming session.
        try:
            controlled = (
                (direction or '').lower() == 'incoming'
                and str(session_status) == str(int(SessionStatus.OPEN))
            )
            await self._broadcast_is_device_remote_controlled_if_changed(controlled)
        except Exception as e:
            logger.warning(f"IsDeviceRemoteControlled compute/broadcast failed: {e}")

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
                        remote_id=remote_id,
                        remote_name=remote_name,
                        logged_user=logged_user,
                        connection_id=connection_id,
                        software=software,
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
                session_status=str(SessionStatus.OPEN),
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
                file_transfers=getattr(status, 'file_transfers', 0),
                remote_id=getattr(status, 'remote_id', '') or "",
                remote_name=getattr(status, 'remote_name', '') or "",
                logged_user=getattr(status, 'logged_user', '') or "",
                connection_id=getattr(status, 'connection_id', '') or "",
                software=getattr(status, 'software', '') or "",
            )

        # Show notification with direction-aware message
        self.tray.set_alert(True)
        if status.direction == 'incoming':
            notification_msg = f"{app_name.upper()} - INCOMING connection detected! Someone may be controlling your computer."
            risk_level = "critical"
        else:
            notification_msg = f"{app_name.upper()} has an active session"
            risk_level = "medium"
        if late_detection:
            notification_msg += " (detected on startup)"
        self.tray.show_notification(
            title="Remote Access Detected",
            message=notification_msg,
            risk_level=risk_level
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

        # Send alert if authenticated (session ended → SessionStatus.CLOSED)
        if self.auth_manager.is_valid():
            if DEBUG_MODE:
                print(f"[MONITOR] Sending session end alert for {app_name}...")

            browser_tabs = await self._get_browser_tabs_for_alert(
                has_active_session=False,
                direction=status.direction or "unknown",
            )
            await self._send_remote_access_alert_with_retry(
                device_uid=self.device_id,
                remote_app=str(status.app_id),
                running_processes=status.process_count,
                connection_url=status.remote_ip or "",
                connection_status=str(status.connection_status),
                session_status=str(SessionStatus.CLOSED),
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
                file_transfers=getattr(status, 'file_transfers', 0),
                remote_id=getattr(status, 'remote_id', '') or "",
                remote_name=getattr(status, 'remote_name', '') or "",
                logged_user=getattr(status, 'logged_user', '') or "",
                connection_id=getattr(status, 'connection_id', '') or "",
                software=getattr(status, 'software', '') or "",
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

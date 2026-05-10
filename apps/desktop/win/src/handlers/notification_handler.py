"""
Notification Handler
Handles notifications from backend (via ZMQ PUB/SUB)
"""

import asyncio
import logging
from typing import Dict, Any, Optional

from services.scan_service import ScanService
from services.browser_tabs_policy import policy as browser_tabs_policy, parse_valid_until
from services.danger_mode import danger_mode

logger = logging.getLogger(__name__)


class NotificationHandler:
    """
    Handles notifications from backend server
    - Process analysis results
    - Update cache
    - Execute protective actions
    - Broadcast results to extension
    """

    def __init__(self, protection_service, cache, extension_server=None):
        self.protection_service = protection_service
        self.cache = cache
        self.extension_server = extension_server
        self.monitor_service = None  # set later — see set_monitor_service
        self._event_loop: Optional[asyncio.AbstractEventLoop] = None

    def set_extension_server(self, extension_server):
        """Set extension server and capture the event loop for cross-thread broadcasting"""
        self.extension_server = extension_server
        try:
            self._event_loop = asyncio.get_running_loop()
            print("[NOTIFICATION] Extension server set + event loop captured")
        except RuntimeError:
            print("[NOTIFICATION] WARNING: No running event loop when setting extension server")

    def set_monitor_service(self, monitor_service):
        """Provide a back-reference to the MonitorService so ImmediateDanger
        notifications can start/stop the periodic-alert loop."""
        self.monitor_service = monitor_service

    def handle(self, notification: Dict[str, Any]):
        """Handle notification from backend.

        Dispatches by the top-level `Type` field. Notifications without a
        recognised type fall through to the legacy URL-analysis path."""
        print("\n" + "!" * 60)
        print("[NOTIFICATION] RECEIVED FROM SERVER!")
        print("!" * 60)

        # Top-level dispatch by message type
        msg_type = notification.get('Type')
        if msg_type == 'ImmediateDangerEndedNotification':
            self._handle_immediate_danger_ended(notification)
            print("!" * 60 + "\n")
            return
        if msg_type == 'ImmediateDangerNotification':
            self._handle_immediate_danger_started(notification)
            print("!" * 60 + "\n")
            return
        if msg_type == 'SetBrowserTabsPolicyNotification':
            self._handle_set_browser_tabs_policy(notification)
            print("!" * 60 + "\n")
            return

        # Backend wraps data in 'Data' object
        data = notification.get('Data', {})

        print(f"[NOTIFICATION] Type: {msg_type or 'N/A'}")
        print(f"[NOTIFICATION] Alert Type: {data.get('AlertType', 'N/A')}")
        print(f"[NOTIFICATION] Severity: {data.get('Severity', 'N/A')}")

        # Extract analysis result
        analysis = self._extract_analysis(data)

        # Display indicators
        self._display_indicators(data.get('Indicators', []))

        # Execute protective actions
        protective_actions = data.get('protectiveActions', []) or data.get('ProtectiveActions', [])
        if protective_actions:
            print(f"[NOTIFICATION] Processing {len(protective_actions)} protective actions...")
            self.protection_service.execute_actions(
                protective_actions,
                analysis['url'],
                data
            )

        # Update cache and broadcast to extension
        if analysis['url'] and analysis['risk_score'] is not None:
            cache_data = self._update_cache(analysis, protective_actions)

            # Broadcast result to extension (cross-thread safe, with retry)
            if self.extension_server and self._event_loop and cache_data:
                broadcast_success = False
                for attempt in range(2):
                    try:
                        future = asyncio.run_coroutine_threadsafe(
                            self._broadcast_to_extension(analysis, cache_data, protective_actions),
                            self._event_loop
                        )
                        future.result(timeout=5)
                        broadcast_success = True
                        break
                    except Exception as e:
                        if attempt == 0:
                            print(f"[NOTIFICATION] WARNING: Broadcast attempt {attempt + 1} failed: {e}, retrying...")
                        else:
                            print(f"[NOTIFICATION] ERROR: Broadcast failed after {attempt + 1} attempts: {e}")
                            logger.error(f"Broadcast failed after retry: {e}")
                if not broadcast_success:
                    print("[NOTIFICATION] ERROR: Could not deliver result to extension")
            elif not self.extension_server:
                print("[NOTIFICATION] WARNING: No extension server - cannot broadcast")
            elif not self._event_loop:
                print("[NOTIFICATION] WARNING: No event loop - cannot broadcast")

            # Clear this specific URL from pending set
            ScanService.clear_pending_url(analysis['url'])

        print("!" * 60 + "\n")

    async def _broadcast_to_extension(self, analysis, cache_data, protective_actions=None):
        """Broadcast URL result to extension"""
        try:
            # Convert protective actions to extension format
            ext_protective_actions = []
            if protective_actions:
                for action in protective_actions:
                    ext_action = {
                        'type': action.get('ActionType', ''),
                        'message': action.get('Message', ''),
                        'level': action.get('Level', '')
                    }
                    ext_protective_actions.append(ext_action)

            result_message = {
                'type': 'url_result',
                'url': analysis['url'],
                'score': cache_data['score'],
                'riskType': cache_data['risk_types'],
                'protectiveAction': cache_data['protective_action'],
                'protectiveActions': ext_protective_actions,  # Full array for extension
                'fromCache': False
            }
            await self.extension_server.broadcast(result_message)
            print(f"[NOTIFICATION] Broadcasted result to extension: score={cache_data['score']}, actions={len(ext_protective_actions)}")
        except Exception as e:
            logger.error(f"Error broadcasting to extension: {e}")
            raise  # Re-raise so caller's retry loop can detect failure

    @staticmethod
    def _build_immediate_danger_details(
        notification: Dict[str, Any], *,
        danger_key: str, user_key: str, device_uid: str,
        protective_actions: list, timestamp,
    ) -> Dict[str, Any]:
        """Extract a structured details dict from an ImmediateDangerNotification
        payload — used by the in-app 'View Details' window."""
        from enums import RemoteAccessApp

        data = notification.get('Data', {}) or {}
        immediate_danger = data.get('ImmediateDanger') or {}

        # RemoteAccessApp arrives as an int (Newtonsoft serializes enum as number)
        ra_app_raw = immediate_danger.get('RemoteAccessApp')
        ra_app_name = None
        if ra_app_raw is not None:
            try:
                ra_app_name = RemoteAccessApp(int(ra_app_raw)).name
            except (ValueError, TypeError):
                ra_app_name = str(ra_app_raw)

        return {
            "danger_key": danger_key,
            "user_key": user_key,
            "device_uid": device_uid,
            "timestamp": timestamp or immediate_danger.get('Timestamp'),
            "remote_app_name": ra_app_name,
            "direction": "incoming",  # ImmediateDanger only fires for incoming sessions
            "sensitive_url": immediate_danger.get('SensitiveUrl'),
            "protective_actions": protective_actions,
        }

    @staticmethod
    def _key_value(key_obj):
        """Backend `Key` is serialized as {"Type": ..., "Value": ...}; reduce to the string."""
        if isinstance(key_obj, dict):
            return key_obj.get('Value') or key_obj.get('value') or ''
        return key_obj if isinstance(key_obj, str) else ''

    def _broadcast_typed(self, payload: Dict[str, Any]):
        """Broadcast a typed message to the extension. Cross-thread safe.
        Silently no-ops if no extension/loop is available — these notifications
        are informational; missing the extension is not a failure."""
        if not self.extension_server or not self._event_loop:
            return
        try:
            future = asyncio.run_coroutine_threadsafe(
                self.extension_server.broadcast(payload),
                self._event_loop,
            )
            future.result(timeout=3)
        except Exception as e:
            logger.warning(f"Failed to broadcast {payload.get('type')!r} to extension: {e}")

    def _handle_immediate_danger_started(self, notification: Dict[str, Any]):
        """Handle ImmediateDangerNotification (session of immediate danger opened).

        The backend payload (NotificationPublisher.PublishImmediateDangerEvent)
        wraps an ImmediateDangerEvent in `Data`. We log the salient fields,
        forward a typed message to the extension, and fire a critical Windows
        toast for any ProtectiveAction of type DisplayNotification /
        UserDisplayNotification (Phase 1: only DisplayNotification types)."""
        data = notification.get('Data', {}) or {}
        immediate_danger = data.get('ImmediateDanger') or {}

        danger_key = self._key_value(immediate_danger.get('Key') or data.get('ImmediateDangerKey'))
        user_key = self._key_value(data.get('UserKey') or immediate_danger.get('UserKey'))
        device_uid = (
            data.get('DeviceUid')
            or immediate_danger.get('DeviceUid')
            or notification.get('DeviceUid')
            or ''
        )
        protective_actions = data.get('ProtectiveActions') or []
        timestamp = notification.get('Timestamp') or data.get('Timestamp')

        print(f"[NOTIFICATION] ImmediateDanger STARTED: key={danger_key}, "
              f"user={user_key}, device={device_uid}, actions={len(protective_actions)}")
        logger.info(
            "ImmediateDanger started: key=%s user=%s device=%s actions=%d",
            danger_key, user_key, device_uid, len(protective_actions),
        )

        # Each step runs independently — a failure in one (e.g., missing UI
        # subsystem, malformed payload, broadcast failure) MUST NOT block the
        # subsequent steps. Previously a single uncaught exception silently
        # killed the whole handler so neither the toast nor the extension
        # broadcast happened.

        # 1. DangerMode flag
        try:
            danger_mode.activate()
            print("[NOTIFICATION] step 1/5: danger_mode.activate() OK")
        except Exception as e:
            logger.error(f"[NOTIFICATION] step 1/5 danger_mode.activate failed: {e}")

        # 2. Start the periodic ImmediateDanger RemoteAccessAlert loop
        try:
            if self.monitor_service is not None and self._event_loop is not None:
                self._event_loop.call_soon_threadsafe(
                    self.monitor_service.start_immediate_danger_loop
                )
                print("[NOTIFICATION] step 2/5: start_immediate_danger_loop scheduled OK")
            else:
                print(f"[NOTIFICATION] step 2/5: SKIP — monitor_service={self.monitor_service is not None}, "
                      f"event_loop={self._event_loop is not None}")
        except Exception as e:
            logger.error(f"[NOTIFICATION] step 2/5 start loop failed: {e}")

        # 3. Build details dict
        details: Dict[str, Any] = {}
        try:
            details = self._build_immediate_danger_details(
                notification, danger_key=danger_key, user_key=user_key,
                device_uid=device_uid, protective_actions=protective_actions,
                timestamp=timestamp,
            )
            print(f"[NOTIFICATION] step 3/5: details built — app={details.get('remote_app_name')}, "
                  f"url={details.get('sensitive_url')}")
        except Exception as e:
            logger.error(f"[NOTIFICATION] step 3/5 build details failed: {e}")
            details = {
                "danger_key": danger_key, "user_key": user_key, "device_uid": device_uid,
                "protective_actions": protective_actions,
            }

        # Fallback toast text (used when no DisplayNotification action carries a Message).
        app_name = (details.get('remote_app_name') or 'Unknown app')
        sensitive_url = (details.get('sensitive_url') or '').strip()
        if sensitive_url:
            fallback = (
                f"Active incoming remote-access session via {app_name} "
                f"while a sensitive site is open: {sensitive_url}.\n"
                f"Disconnect the session immediately, or contact your guardian."
            )
        else:
            fallback = (
                f"Active incoming remote-access session via {app_name} "
                f"while a sensitive site is open.\n"
                f"Disconnect the session immediately, or contact your guardian."
            )

        # 4. Fire toast(s)
        try:
            if self.protection_service:
                ok = self.protection_service.show_display_notification_actions(
                    actions=protective_actions,
                    title="Immediate Danger",
                    risk_level="critical",
                    action_buttons=[("View Details", ""), ("Dismiss", "")],
                    fallback_message=fallback,
                    details=details,
                )
                print(f"[NOTIFICATION] step 4/5: show_display_notification_actions returned {ok}")
            else:
                print("[NOTIFICATION] step 4/5: SKIP — protection_service is None")
        except Exception as e:
            logger.error(f"[NOTIFICATION] step 4/5 toast failed: {e}")
            import traceback; traceback.print_exc()

        # 5. Broadcast to extension so it can flip immediateDangerMode and start
        # emitting TabClosedAlerts on tab closes.
        try:
            self._broadcast_typed({
                'type': 'immediate_danger_started',
                'dangerKey': danger_key,
                'userKey': user_key,
                'deviceUid': device_uid,
                'timestamp': timestamp,
                'protectiveActions': protective_actions,
            })
            print("[NOTIFICATION] step 5/5: broadcast immediate_danger_started OK")
        except Exception as e:
            logger.error(f"[NOTIFICATION] step 5/5 broadcast failed: {e}")
            import traceback; traceback.print_exc()

    def _handle_set_browser_tabs_policy(self, notification: Dict[str, Any]):
        """Handle SetBrowserTabsPolicyNotification — backend-controlled override
        of BrowserTabs inclusion policy.

        Payload (Data):
          - Mode: 'always' | 'never' | 'incoming_only'
          - ValidUntil: ISO-8601 datetime, or null/missing for permanent override
        """
        data = notification.get('Data', {}) or {}
        mode_raw = data.get('Mode') or data.get('mode') or ''
        mode = str(mode_raw).strip().lower()
        valid_until_raw = data.get('ValidUntil') or data.get('validUntil')
        valid_until = parse_valid_until(valid_until_raw)

        accepted = browser_tabs_policy.set_override(mode, valid_until)
        if accepted:
            print(f"[NOTIFICATION] BrowserTabs policy override: mode={mode!r}, "
                  f"valid_until={valid_until.isoformat() if valid_until else 'permanent'}")
        else:
            print(f"[NOTIFICATION] BrowserTabs policy override REJECTED: invalid mode {mode_raw!r}")

    def _handle_immediate_danger_ended(self, notification: Dict[str, Any]):
        """Handle ImmediateDangerEndedNotification.

        The backend payload (NotificationPublisher.PublishImmediateDangerEnded)
        contains ImmediateDangerKey, UserKey, DeviceUid, EndTime. We log the
        end, fire a green "Threat cleared" toast (or use a DisplayNotification
        ProtectiveAction's message if one is included), and forward a typed
        message to the extension so it can drop any immediate-danger UI."""
        data = notification.get('Data', {}) or {}

        danger_key = self._key_value(data.get('ImmediateDangerKey'))
        user_key = self._key_value(data.get('UserKey'))
        device_uid = data.get('DeviceUid') or notification.get('DeviceUid') or ''
        end_time = data.get('EndTime') or notification.get('Timestamp')
        protective_actions = data.get('ProtectiveActions') or []

        print(f"[NOTIFICATION] ImmediateDanger ENDED: key={danger_key}, "
              f"user={user_key}, device={device_uid}, end={end_time}")
        logger.info(
            "ImmediateDanger ended: key=%s user=%s device=%s end=%s",
            danger_key, user_key, device_uid, end_time,
        )

        # Leave DangerMode: revert to adaptive polling + normal debounce.
        danger_mode.deactivate()

        # Stop the periodic ImmediateDanger RemoteAccessAlert loop.
        if self.monitor_service is not None and self._event_loop is not None:
            try:
                self._event_loop.call_soon_threadsafe(
                    self.monitor_service.stop_immediate_danger_loop
                )
            except Exception as e:
                logger.error(f"Failed to stop ImmediateDanger loop: {e}")

        # Build details dict for the cleared-state DangerDetailsWindow.
        details = {
            "danger_key": danger_key,
            "user_key": user_key,
            "device_uid": device_uid,
            "timestamp": end_time,
            "protective_actions": protective_actions,
        }

        # Always show a "cleared" toast. If the payload includes a
        # DisplayNotification ProtectiveAction, use its Message; otherwise
        # fall back to the default "Threat cleared" copy.
        if self.protection_service:
            self.protection_service.show_display_notification_actions(
                actions=protective_actions,
                title="Threat Cleared",
                risk_level="none",
                action_buttons=[("View Details", ""), ("Dismiss", "")],
                fallback_message="The previous immediate danger has been resolved.",
                details=details,
            )

        self._broadcast_typed({
            'type': 'immediate_danger_ended',
            'dangerKey': danger_key,
            'userKey': user_key,
            'deviceUid': device_uid,
            'endTime': end_time,
        })

    def _extract_analysis(self, data: Dict[str, Any]) -> Dict[str, Any]:
        """Extract analysis result from notification data"""
        analysis_result = data.get('AnalysisResult', {})
        analyzer_results = data.get('AnalyzerResults', {})

        url = None
        risk_score = None
        risk_assessment = {}

        if analysis_result and isinstance(analysis_result, dict):
            print(f"[NOTIFICATION] Type: {analysis_result.get('TypeName', 'N/A')}")
            url = analysis_result.get('Url')

            risk_assessment = analysis_result.get('risk_assessment', {})
            risk_score = risk_assessment.get('risk_score')

            print(f"[NOTIFICATION] URL: {url}")
            print(f"[NOTIFICATION] Domain: {analysis_result.get('Domain', 'N/A')}")
            print(f"[NOTIFICATION] Analysis Time: {analysis_result.get('analysis_time_ms', 'N/A')}ms")
            print(f"[NOTIFICATION] Risk Score: {risk_score}")
            print(f"[NOTIFICATION] Risk Level: {risk_assessment.get('risk_level', 'N/A')}")
            print(f"[NOTIFICATION] Is Scam: {risk_assessment.get('is_scam', False)}")

        # Fallback: try RiskAssessment at Data level
        if not risk_score:
            risk_assessment = data.get('RiskAssessment', {})
            risk_score = risk_assessment.get('risk_score')
            print(f"[NOTIFICATION] Risk Score (fallback): {risk_score}")

        # Fallback: try AnalyzerResults
        if not url and analyzer_results:
            url = analyzer_results.get('Url')
            if url:
                print(f"[NOTIFICATION] URL (from AnalyzerResults): {url}")

        # Fallback: use pending URL from ScanService
        if not url:
            url = ScanService.get_pending_url()
            if url:
                print(f"[NOTIFICATION] URL (from pending scan): {url}")

        # Get phishing check
        phishing_check = None
        if analysis_result:
            phishing_check = analysis_result.get('phishing_check', {})
        elif analyzer_results:
            phishing_check = analyzer_results.get('phishing_check', {})

        return {
            'url': url,
            'risk_score': risk_score,
            'risk_assessment': risk_assessment,
            'phishing_check': phishing_check
        }

    def _display_indicators(self, indicators: list):
        """Display indicators from notification"""
        if not indicators:
            return

        print(f"[NOTIFICATION] Indicators: {len(indicators)} found")
        for idx, indicator in enumerate(indicators, 1):
            if isinstance(indicator, dict):
                ind_type = indicator.get('IndicatorType', indicator.get('$type', 'N/A'))
                ind_value = indicator.get('Value', 'N/A')
                print(f"[NOTIFICATION]    {idx}. Type: {ind_type}, Value: {ind_value}")
            else:
                print(f"[NOTIFICATION]    {idx}. {indicator}")

    def _update_cache(self, analysis: Dict[str, Any], protective_actions: list) -> Dict[str, Any]:
        """Update cache with analysis results - using server score directly"""
        url = analysis['url']
        risk_score = analysis['risk_score']
        risk_assessment = analysis['risk_assessment']
        phishing_check = analysis.get('phishing_check', {})

        # Use server score directly - no conversion
        score = int(risk_score)

        # Get risk types from server
        risk_types = []
        if risk_assessment.get('is_scam'):
            risk_types.append('Scam')
        if phishing_check and phishing_check.get('Is_known_phishing'):
            risk_types.append('Phishing')

        # Add risk_level from server if available
        risk_level = risk_assessment.get('risk_level')
        if risk_level and risk_level not in risk_types:
            risk_types.append(risk_level)

        # Get protective action from server response
        protective_action = self.protection_service.get_cache_action(
            protective_actions,
            score
        )

        print(f"[NOTIFICATION] Updating cache: score={score} (from server), "
              f"risks={risk_types}, action={protective_action}")

        self.cache.set(url, score, risk_types, protective_action, ttl=3600)

        return {
            'score': score,
            'risk_types': risk_types,
            'protective_action': protective_action
        }

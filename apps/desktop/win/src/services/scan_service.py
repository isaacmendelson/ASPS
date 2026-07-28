"""
Scan Service
Handles URL scanning and analysis requests
"""

import logging
import threading
from typing import Dict, Optional, Any

logger = logging.getLogger(__name__)


class ScanService:
    """
    Handles URL scanning logic
    - Cache checking
    - Backend communication
    - Result processing
    """

    # Class-level pending URLs tracking - supports multiple concurrent scans
    # url -> {timestamp, tab_id, request_id, correlation_id}
    _pending_urls: Dict[str, Dict] = {}
    _pending_lock = threading.Lock()

    def __init__(
        self,
        cache,
        zmq_client,
        auth_manager,
        browser_monitor,
        event_logger,
        device_id: str
    ):
        self.cache = cache
        self.zmq_client = zmq_client
        self.auth_manager = auth_manager
        self.browser_monitor = browser_monitor
        self.event_logger = event_logger
        self.device_id = device_id

    @classmethod
    def _get_lock(cls):
        return cls._pending_lock

    @classmethod
    def set_pending_url(cls, url: str, *, tab_id: str = '', request_id: str = '', correlation_id: str = ''):
        """Add URL to pending set with routing metadata.

        tab_id, request_id and correlation_id come from the ASPS-611 envelope
        so that when the backend notification arrives the result can be routed
        back to the exact tab that originated the scan rather than to the
        globally-most-recent URL.
        """
        import time
        with cls._get_lock():
            cls._pending_urls[url] = {
                'timestamp': time.time(),
                'tab_id': tab_id,
                'request_id': request_id,
                'correlation_id': correlation_id,
            }
            # Cleanup stale entries (older than 60 seconds)
            cutoff = time.time() - 60
            cls._pending_urls = {
                u: entry for u, entry in cls._pending_urls.items()
                if entry['timestamp'] > cutoff
            }

    @classmethod
    def get_pending_url(cls) -> Optional[str]:
        """Return the most-recently added pending URL.

        Kept for backward compatibility with notification paths that have no
        URL in the backend payload.  Prefer get_pending_entry(url) when the
        URL is known.
        """
        with cls._get_lock():
            if not cls._pending_urls:
                return None
            return max(cls._pending_urls.items(), key=lambda x: x[1]['timestamp'])[0]

    @classmethod
    def get_pending_entry(cls, url: str) -> Optional[Dict[str, Any]]:
        """Return the full pending entry for *url*, or None if not pending.

        The entry dict has keys: timestamp, tab_id, request_id, correlation_id.
        """
        with cls._get_lock():
            return cls._pending_urls.get(url)

    @classmethod
    def is_pending(cls, url: str) -> bool:
        """Check if URL is in pending set"""
        with cls._get_lock():
            return url in cls._pending_urls

    @classmethod
    def clear_pending_url(cls, url: str = None):
        """Clear specific URL or all pending URLs"""
        with cls._get_lock():
            if url:
                cls._pending_urls.pop(url, None)
            else:
                cls._pending_urls.clear()

    @staticmethod
    def _is_local_url(url: str) -> bool:
        """Returns True if the URL points to a loopback/local address."""
        try:
            from urllib.parse import urlparse
            host = (urlparse(url).hostname or '').lower()
            return (
                host == 'localhost' or
                host == '127.0.0.1' or
                host.startswith('127.') or
                host == '::1' or
                host == '0.0.0.0'
            )
        except Exception:
            return False

    def check_url(
        self,
        url: str,
        trackers: list = None,
        iframes: list = None,
        ip_address: str = "",
        tab_id: str = "",
        envelope: Optional[Dict[str, Any]] = None
    ) -> Dict[str, Any]:
        """
        Check a URL for risks
        Returns result dict for extension
        """
        trackers = trackers or []
        iframes = iframes or []

        print("\n" + "~" * 60)
        print("[SCAN] URL CHECK REQUEST")
        print("~" * 60)
        print(f"[SCAN] URL: {url[:100]}...")
        print(f"[SCAN] Trackers: {len(trackers)}")
        print(f"[SCAN] iFrames: {len(iframes)}")

        # Skip local/loopback addresses — never send to backend
        if self._is_local_url(url):
            print(f"[SCAN] Skipping local URL: {url}")
            return self._create_result(url=url, score=0, risk_type=[], protective_action=0, cached=False)

        # Step 1: Check cache
        print("[SCAN] Step 1: Checking cache...")
        cached = self.cache.get(url)
        if cached:
            print(f"[SCAN] CACHE HIT! Score: {cached.score}")
            print("~" * 60 + "\n")
            # A cache entry exists only after a completed analysis. It is safe
            # to suppress the equivalent browser-history discovery.
            self.browser_monitor.mark_delivery_acknowledged(url, 'extension')
            return self._create_result(
                url=url,
                score=cached.score,
                risk_type=cached.risk_type,
                protective_action=cached.protective_action,
                cached=True
            )

        print("[SCAN] CACHE MISS - asking server")

        # Discovery is durable, but must not become "seen" until the backend
        # explicitly accepts this delivery.
        self.browser_monitor.queue_url_for_delivery(url, 'extension')

        # Step 2: Check authentication
        print("[SCAN] Step 2: Checking authentication...")
        if not self.auth_manager.is_valid():
            print("[SCAN] Not authenticated, trying to authenticate...")
            if not self.auth_manager.authenticate():
                print("[SCAN] Authentication failed!")
                return self._create_error(url, "Not authenticated")

        token = self.auth_manager.get_token()
        print("[SCAN] Token: present" if token else "[SCAN] No token!")

        # Step 3: Send to backend
        print("[SCAN] Step 3: Sending to backend (ZMQ)...")

        # Track pending URL for notification matching.
        # Record routing metadata from the ASPS-611 envelope (if present) so
        # that when the backend notification arrives the result can be directed
        # to the originating tab rather than the currently-active one.
        _request_id = (envelope or {}).get('requestId', '')
        _correlation_id = (envelope or {}).get('correlationId', '')
        ScanService.set_pending_url(
            url,
            tab_id=tab_id,
            request_id=_request_id,
            correlation_id=_correlation_id,
        )

        self.browser_monitor.mark_delivery_sent(url, 'extension')
        try:
            response = self.zmq_client.send_url_alert(
                device_uid=self.device_id,
                url=url,
                token=token,
                trackers=trackers,
                iframes=iframes,
                ip_address=ip_address,
                tab_id=tab_id,
                envelope=envelope
            )
        except Exception as error:
            logger.error("URL delivery failed for %s: %s", url, error)
            self._mark_delivery_failed(url)
            return self._create_error(url, str(error))

        self.event_logger.log_sent('SuspiciousUrlAlert', {'url': url})

        # Process response
        return self._process_response(response, url, ip_address=ip_address)

    def _process_response(self, response: Optional[dict], url: str, retry: bool = True, ip_address: str = "") -> Dict[str, Any]:
        """Process backend response - use server values directly"""

        # Check for auth-related errors first
        if response and response.get('status') in ('InvalidToken', 'TokenExpired'):
            print(f"[SCAN] Token issue: {response.get('status')}")
            if retry and self.auth_manager.handle_auth_response(response):
                # Re-authenticated successfully, retry the request
                print("[SCAN] Re-authenticated, retrying request...")
                token = self.auth_manager.get_token()
                self.browser_monitor.mark_delivery_sent(url, 'extension')
                try:
                    new_response = self.zmq_client.send_url_alert(
                        device_uid=self.device_id,
                        url=url,
                        token=token or "",
                        ip_address=ip_address
                    )
                except Exception as error:
                    logger.error("URL delivery retry failed for %s: %s", url, error)
                    self._mark_delivery_failed(url)
                    return self._create_error(url, str(error))
                return self._process_response(new_response, url, retry=False, ip_address=ip_address)
            else:
                self._mark_delivery_failed(url)
                return self._create_error(url, "Authentication failed")

        # New format: success response (async analysis)
        if response and response.get('messageType') == 'url_scan.accepted':
            self.browser_monitor.mark_delivery_acknowledged(url, 'extension')
            payload = response.get('payload', {})
            return {
                **response,
                'type': 'url_result',
                'url': url,
                'analyzing': bool(payload.get('success')),
                'message': payload.get('message', 'Analysis in progress')
            }

        if response and response.get('success') is True:
            self.browser_monitor.mark_delivery_acknowledged(url, 'extension')
            print(f"[SCAN] Backend accepted alert")
            print(f"[SCAN] Message: {response.get('message', 'N/A')}")
            print(f"[SCAN] Waiting for notification with analysis results...")
            print("~" * 60 + "\n")

            return {
                'type': 'url_result',
                'url': url,
                'analyzing': True,
                'message': 'Analysis in progress - waiting for results'
            }

        # Old format: immediate result - use server values directly
        if response and (
            not response.get('HasError', False) and
            any(field in response for field in ('Score', 'RiskType', 'ProtectiveAction'))
        ):
            self.browser_monitor.mark_delivery_acknowledged(url, 'extension')
            # Use values directly from server
            score = response.get('Score')
            risk_type = response.get('RiskType', [])
            protective_action = response.get('ProtectiveAction', 0)

            # If protective_action is string, convert to int
            if isinstance(protective_action, str):
                action_map = {
                    'None': 0, 'Ignore': 1, 'WarnOnScreen': 2,
                    'ModalPopup': 3, 'Block': 4
                }
                protective_action = action_map.get(protective_action, 0)

            print(f"[SCAN] SUCCESS! Score: {score} (from server)")
            print(f"[SCAN] Risk Type: {risk_type} (from server)")
            print(f"[SCAN] Protective Action: {protective_action} (from server)")

            # Cache if we have a score from server
            if score is not None:
                print(f"[SCAN] Caching server result")
                self.cache.set(url, score, risk_type, protective_action, ttl=3600)
            else:
                print(f"[SCAN] No score from server - waiting for notification")

            print("~" * 60 + "\n")
            return self._create_result(
                url=url,
                score=score,
                risk_type=risk_type,
                protective_action=protective_action,
                cached=False
            )

        # Error
        error_msg = response.get('ErrorMessage', 'Unknown error') if response else 'No response'
        if response and error_msg == 'Unknown error':
            error_msg = response.get('message', error_msg)
        self._mark_delivery_failed(url)
        print(f"[SCAN] ERROR: {error_msg}")
        print("~" * 60 + "\n")
        return self._create_error(url, str(error_msg))

    def _mark_delivery_failed(self, url: str) -> None:
        self.browser_monitor.mark_delivery_failed(url, 'extension')
        ScanService.clear_pending_url(url)

    def _create_result(
        self,
        url: str,
        score: int,
        risk_type: list,
        protective_action: int,
        cached: bool
    ) -> Dict[str, Any]:
        """Create success result"""
        return {
            'type': 'url_result',
            'url': url,
            'score': score,
            'riskType': risk_type,
            'protectiveAction': protective_action,
            'cached': cached
        }

    def _create_error(self, url: str, message: str) -> Dict[str, Any]:
        """Create error result"""
        return {
            'type': 'url_result',
            'url': url,
            'error': True,
            'message': message
        }

    def send_tab_closed_alert(
        self,
        tab_id: str,
        url: str,
        ip_address: str = '',
    ) -> Dict[str, Any]:
        """Forward a TabClosedAlert from the Chrome extension to the backend.
        Triggered by the extension while in ImmediateDanger mode whenever a
        tab is closed. The backend's UDUserAnalyzer re-evaluates the danger
        condition on receipt."""
        try:
            print(f"\n[TAB-CLOSED] Forwarding to backend — TabId={tab_id}, Url={url}")
            response = self.zmq_client.send_tab_closed_alert(
                device_uid=self.device_id,
                tab_id=tab_id,
                url=url,
                ip_address=ip_address,
                token=self.auth_manager.token or '',
            )
            if response:
                print(f"[TAB-CLOSED] Backend response: {response.get('Status', 'unknown')}")
                return {'type': 'tab_closed_ack', 'status': 'ok'}
            return {'type': 'tab_closed_ack', 'status': 'error', 'message': 'No response'}
        except Exception as e:
            logger.error(f"send_tab_closed_alert failed: {e}")
            return {'type': 'tab_closed_ack', 'status': 'error', 'message': str(e)}

    def send_tab_changed_alert(
        self,
        tab_id: str,
        url: str,
        is_sensitive_website: Optional[bool] = None,
        is_logged_in: Optional[bool] = None,
        ip_address: str = '',
    ) -> Dict[str, Any]:
        """Forward a TabChangedAlert from the Chrome extension to the backend.
        Fired by the extension on URL change in a tab while the device is
        under remote control (session=open + direction=incoming). Lets the
        backend re-evaluate ImmediateDanger immediately when the user logs
        in to a sensitive site mid-session."""
        try:
            print(f"\n[TAB-CHANGED] Forwarding to backend — TabId={tab_id}, Url={url}, "
                  f"sensitive={is_sensitive_website}, loggedIn={is_logged_in}")
            response = self.zmq_client.send_tab_changed_alert(
                device_uid=self.device_id,
                tab_id=tab_id,
                url=url,
                is_sensitive_website=is_sensitive_website,
                is_logged_in=is_logged_in,
                ip_address=ip_address,
                token=self.auth_manager.token or '',
            )
            if response:
                print(f"[TAB-CHANGED] Backend response: {response.get('Status', 'unknown')}")
                return {'type': 'tab_changed_ack', 'status': 'ok'}
            return {'type': 'tab_changed_ack', 'status': 'error', 'message': 'No response'}
        except Exception as e:
            logger.error(f"send_tab_changed_alert failed: {e}")
            return {'type': 'tab_changed_ack', 'status': 'error', 'message': str(e)}

    def send_track_url_alert(
        self,
        url: str,
        from_url: str = '',
        duration: int = 0,
        scam_in_progress_key: str = '',
        ip_address: str = '',
        user_agent: str = '',
        tab_id: str = '',
        timezone: str = ''
    ) -> Dict[str, Any]:
        """Send TrackUrlAlert to backend"""
        try:
            print(f"\n[TRACK] Sending TrackUrlAlert to backend...")
            print(f"[TRACK] URL: {url}")
            print(f"[TRACK] From: {from_url}")
            print(f"[TRACK] Duration: {duration}s")
            
            response = self.zmq_client.send_track_url_alert(
                device_uid=self.device_id,
                url=url,
                from_url=from_url,
                duration=duration,
                scam_in_progress_key=scam_in_progress_key,
                ip_address=ip_address,
                user_agent=user_agent,
                tab_id=tab_id,
                timezone=timezone,
                token=self.auth_manager.token or ''
            )
            
            if response:
                print(f"[TRACK] Backend response: {response.get('Status', 'unknown')}")
                return {'type': 'track_url_ack', 'status': 'ok'}
            else:
                print("[TRACK] No response from backend")
                return {'type': 'track_url_ack', 'status': 'error', 'message': 'No response'}
                
        except Exception as e:
            print(f"[TRACK] Error: {e}")
            return {'type': 'track_url_ack', 'status': 'error', 'message': str(e)}

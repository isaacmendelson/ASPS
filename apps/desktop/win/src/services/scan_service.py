"""
Scan Service
Handles URL scanning and analysis requests
"""

import logging
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
    _pending_urls: Dict[str, float] = {}  # url -> timestamp
    _pending_lock = None  # Will be initialized on first use

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
        """Get or create thread lock"""
        import threading
        if cls._pending_lock is None:
            cls._pending_lock = threading.Lock()
        return cls._pending_lock

    @classmethod
    def set_pending_url(cls, url: str):
        """Add URL to pending set with timestamp"""
        import time
        with cls._get_lock():
            cls._pending_urls[url] = time.time()
            # Cleanup old entries (> 60 seconds)
            cutoff = time.time() - 60
            cls._pending_urls = {u: t for u, t in cls._pending_urls.items() if t > cutoff}

    @classmethod
    def get_pending_url(cls) -> str:
        """Get the most recent pending URL (backward compatibility)"""
        with cls._get_lock():
            if not cls._pending_urls:
                return None
            # Return most recent
            return max(cls._pending_urls.items(), key=lambda x: x[1])[0]

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

    def check_url(
        self,
        url: str,
        trackers: list = None,
        iframes: list = None
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

        # Step 1: Check cache
        print("[SCAN] Step 1: Checking cache...")
        cached = self.cache.get(url)
        if cached:
            print(f"[SCAN] CACHE HIT! Score: {cached.score}")
            print("~" * 60 + "\n")
            return self._create_result(
                url=url,
                score=cached.score,
                risk_type=cached.risk_type,
                protective_action=cached.protective_action,
                cached=True
            )

        print("[SCAN] CACHE MISS - asking server")

        # Mark URL as seen
        self.browser_monitor.mark_url_as_sent(url, 'extension')

        # Step 2: Check authentication
        print("[SCAN] Step 2: Checking authentication...")
        if not self.auth_manager.is_valid():
            print("[SCAN] Not authenticated, trying to authenticate...")
            if not self.auth_manager.authenticate():
                print("[SCAN] Authentication failed!")
                return self._create_error(url, "Not authenticated")

        token = self.auth_manager.get_token()
        print(f"[SCAN] Token: {token[:20]}..." if token else "[SCAN] No token!")

        # Step 3: Send to backend
        print("[SCAN] Step 3: Sending to backend (ZMQ)...")

        # Track pending URL for notification matching
        ScanService.set_pending_url(url)

        response = self.zmq_client.send_url_alert(
            device_uid=self.device_id,
            url=url,
            token=token,
            trackers=trackers,
            iframes=iframes
        )

        self.event_logger.log_sent('SuspiciousUrlAlert', {'url': url})

        # Process response
        return self._process_response(response, url)

    def _process_response(self, response: Optional[dict], url: str, retry: bool = True) -> Dict[str, Any]:
        """Process backend response - use server values directly"""

        # Check for auth-related errors first
        if response and response.get('status') in ('InvalidToken', 'TokenExpired'):
            print(f"[SCAN] Token issue: {response.get('status')}")
            if retry and self.auth_manager.handle_auth_response(response):
                # Re-authenticated successfully, retry the request
                print("[SCAN] Re-authenticated, retrying request...")
                token = self.auth_manager.get_token()
                new_response = self.zmq_client.send_url_alert(
                    device_uid=self.device_id,
                    url=url,
                    token=token or ""
                )
                return self._process_response(new_response, url, retry=False)
            else:
                return self._create_error(url, "Authentication failed")

        # New format: success response (async analysis)
        if response and response.get('success'):
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
        if response and not response.get('HasError'):
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
        print(f"[SCAN] ERROR: {error_msg}")
        print("~" * 60 + "\n")
        return self._create_error(url, str(error_msg))

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

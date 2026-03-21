"""
Extension Handler
Handles messages from Chrome extension
"""

import asyncio
import logging
from typing import Dict, Any, Optional
from zmq_client import get_local_ip

logger = logging.getLogger(__name__)


class ExtensionHandler:
    """
    Handles messages from Chrome extension via WebSocket
    """

    def __init__(self, scan_service, auth_manager, device_id: str):
        self.scan_service = scan_service
        self.auth_manager = auth_manager
        self.device_id = device_id
        self._local_ip: str = get_local_ip()

    async def handle_message(self, data: Dict[str, Any]) -> Optional[Dict[str, Any]]:
        """
        Handle incoming message from extension
        Returns response dict
        """
        msg_type = data.get('type', '')
        print(f"\n[EXTENSION] Received: {msg_type}")

        handlers = {
            'url_check': self._handle_url_check,
            'track_url_alert': self._handle_track_url_alert,
            'ping': self._handle_ping,
            'user_auth': self._handle_user_auth,
            'get_user': self._handle_get_user,
            'user_signout': self._handle_user_signout,
        }

        handler = handlers.get(msg_type)
        if handler:
            if msg_type == 'url_check':
                loop = asyncio.get_running_loop()
                return await loop.run_in_executor(None, handler, data)
            else:
                return handler(data)

        logger.warning(f"Unknown message type: {msg_type}")
        return {'type': 'error', 'message': 'Unknown message type'}

    def _handle_url_check(self, data: Dict[str, Any]) -> Dict[str, Any]:
        """Handle URL check request"""
        url = data.get('url', '')
        trackers = data.get('trackers', [])
        iframes = data.get('iframes', [])
        ip_address = data.get('ipAddress', '') or self._local_ip

        return self.scan_service.check_url(url, trackers, iframes, ip_address=ip_address)

    def _handle_track_url_alert(self, data: Dict[str, Any]) -> Dict[str, Any]:
        """Handle TrackUrlAlert from extension - send to backend"""
        url = data.get('Url', '')
        from_url = data.get('FromUrl', '')
        duration = data.get('Duration', 0)
        scam_key = data.get('ScamInProgressKey', '')
        ip_address = data.get('IPAddress', '') or self._local_ip
        user_agent = data.get('UserAgent', '')
        tab_id = data.get('TabId', '')
        timezone = data.get('Timezone', '')

        print(f"[EXTENSION] TrackUrlAlert: {url} (from: {from_url}, duration: {duration}s)")
        
        return self.scan_service.send_track_url_alert(
            url=url,
            from_url=from_url,
            duration=duration,
            scam_in_progress_key=scam_key,
            ip_address=ip_address,
            user_agent=user_agent,
            tab_id=tab_id,
            timezone=timezone
        )

    def _handle_ping(self, data: Dict[str, Any]) -> Dict[str, Any]:
        """Handle ping request - includes email and device IP so extension gets them automatically"""
        return {
            'type': 'pong',
            'status': 'ok',
            'email': self.auth_manager.email or '',
            'ipAddress': self._local_ip
        }

    def _handle_user_auth(self, data: Dict[str, Any]) -> Dict[str, Any]:
        """Handle user authentication - store email from extension"""
        email = data.get('email', '')
        if not email:
            return {'type': 'user_auth_ack', 'status': 'error', 'message': 'No email provided'}

        self.auth_manager.email = email
        self.auth_manager._save_token()
        print(f"[EXTENSION] User email set: {email}")
        return {'type': 'user_auth_ack', 'status': 'ok', 'email': email}

    def _handle_get_user(self, data: Dict[str, Any]) -> Dict[str, Any]:
        """Handle get user request"""
        return {
            'type': 'user_info',
            'device_id': self.device_id,
            'signed_in': bool(self.auth_manager.email),
            'email': self.auth_manager.email
        }

    def _handle_user_signout(self, data: Dict[str, Any]) -> Dict[str, Any]:
        """Handle user signout - clear stored email"""
        self.auth_manager.email = ""
        self.auth_manager._save_token()
        print("[EXTENSION] User signed out, email cleared")
        return {'type': 'user_signout_ack', 'status': 'ok'}

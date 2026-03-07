"""
AntiScam Desktop App - Auth Manager
Manages authentication token lifecycle via RequestToken/RefreshToken ZMQ messages.
"""

import json
import os
import logging
import webbrowser
from datetime import datetime, timedelta, timezone
from typing import Optional
from pathlib import Path

from config import DATA_DIR, BACKEND_SERVER_PUBLIC_KEY_Z85, WEBAPI_URL

# Secure credential storage — Windows Credential Manager / macOS Keychain / libsecret
try:
    import keyring
    _KEYRING_SERVICE = "AntiScamApp"
    _KEYRING_AVAILABLE = True
except ImportError:
    _KEYRING_AVAILABLE = False
    logging.getLogger(__name__).warning(
        "keyring not installed — token stored in plain-text file. Run: pip install keyring"
    )

logger = logging.getLogger(__name__)


class AuthManager:
    """
    Manages device authentication against the backend.

    Flow:
    1. Load saved auth data from disk (token + server public key if previously obtained)
    2. Send RequestToken to backend (first request may be unencrypted if no key yet)
       - If TokenCreated/ExistingToken -> store token AND server public key
       - If DeviceNotRecognized -> open browser to WebApi login page
       - If TokenExpired -> attempt RefreshToken
    3. All subsequent connections use CURVE encryption with the server's public key
    4. All subsequent alerts include the real token
    """

    def __init__(self, zmq_client, device_info: dict, email: str = ""):
        self.zmq_client = zmq_client
        self.device_info = device_info
        self.email = email

        # Token data
        self.token: Optional[str] = None
        self.expires_at: Optional[datetime] = None
        self.is_authorized: bool = False
        self.user_id: Optional[int] = None

        # Server public key (Z85-encoded, as bytes) - obtained from backend
        self.server_public_key: Optional[bytes] = None

        # Prevent opening login page multiple times
        self._login_page_opened: bool = False

        # Storage path
        self.storage_path = Path(os.path.expanduser(DATA_DIR)) / "auth.json"

        print(f"[AUTH] Manager initialized")
        print(f"[AUTH] Storage: {self.storage_path}")

        # Try to load saved token
        self._load_token()

        # Apply server key to zmq_client.
        # The curve-server-public-key.txt is the authoritative source —
        # always re-read it fresh so that if the backend restarts with
        # CurveEnabled=false (and clears the file), we don't use a stale key
        # from auth.json and get a CURVE mismatch timeout.
        live_key = BACKEND_SERVER_PUBLIC_KEY_Z85  # reads from txt file at import time
        if live_key:
            self.server_public_key = live_key.encode('utf-8')
            self.zmq_client.set_server_public_key(self.server_public_key)
            print(f"[AUTH] CURVE enabled — server public key loaded from key file")
        else:
            # Key file is empty or missing → backend has CURVE disabled
            self.server_public_key = None
            self.zmq_client.clear_server_public_key()
            print(f"[AUTH] CURVE disabled — connecting without encryption")

    def _load_token(self):
        """Load token from secure storage (keyring) and metadata from disk."""
        try:
            if self.storage_path.exists():
                with open(self.storage_path, 'r') as f:
                    data = json.load(f)

                if _KEYRING_AVAILABLE:
                    device_uid = self.device_info.get('id', 'unknown')
                    self.token = keyring.get_password(_KEYRING_SERVICE, device_uid)
                    if self.token:
                        print("[AUTH] Token loaded from OS keyring (secure)")
                    else:
                        legacy = data.get('token')
                        if legacy:
                            keyring.set_password(_KEYRING_SERVICE, device_uid, legacy)
                            self.token = legacy
                            print("[AUTH] Migrated token to OS keyring")
                else:
                    self.token = data.get('token')
                    print("[AUTH] WARNING: token loaded from plain-text file")

                self.user_id = data.get('user_id')
                self.is_authorized = data.get('is_authorized', False)

                expires_str = data.get('expires_at')
                if expires_str:
                    self.expires_at = datetime.fromisoformat(expires_str)

                # server_public_key is NOT loaded from auth.json —
                # it is always read fresh from curve-server-public-key.txt
                # so that backend restarts with CurveEnabled=false are respected.

                saved_email = data.get('email', '')
                if saved_email and not self.email:
                    self.email = saved_email
                    print(f"[AUTH] Loaded saved email: {self.email}")

                if self.token:
                    print("[AUTH] Loaded saved token: [REDACTED]")
                    print(f"[AUTH] Expires: {self.expires_at}")
                    if self.is_expired():
                        print("[AUTH] Token is expired, will refresh")
                    else:
                        print("[AUTH] Token is valid")

        except Exception as e:
            print(f"[AUTH] Error loading token: {e}")
            logger.error(f"Error loading auth token: {e}")

    def _save_token(self):
        """Save token to OS keyring; save non-sensitive metadata to disk."""
        try:
            self.storage_path.parent.mkdir(parents=True, exist_ok=True)

            if _KEYRING_AVAILABLE and self.token:
                device_uid = self.device_info.get('id', 'unknown')
                keyring.set_password(_KEYRING_SERVICE, device_uid, self.token)
                token_for_file = None
                print("[AUTH] Token stored in OS keyring (secure)")
            else:
                token_for_file = self.token
                if token_for_file:
                    print("[AUTH] WARNING: token written to plain-text file")

            data = {
                'token': token_for_file,
                'user_id': self.user_id,
                'expires_at': self.expires_at.isoformat() if self.expires_at else None,
                'is_authorized': self.is_authorized,
                'email': self.email,
                # server_public_key intentionally omitted — always read fresh
                # from curve-server-public-key.txt at startup.
            }

            with open(self.storage_path, 'w') as f:
                json.dump(data, f, indent=2)

            print(f"[AUTH] Auth metadata saved to {self.storage_path}")

        except Exception as e:
            print(f"[AUTH] Error saving token: {e}")
            logger.error(f"Error saving auth token: {e}")

    def _handle_token_response(self, response: dict) -> bool:
        """
        Process a token response from the backend.
        Updates token, expiration, and server public key.
        Returns True if a valid token was received.
        """
        status = response.get("status", "")
        print(f"[AUTH] _handle_token_response: status={status}")

        if status in ("TokenCreated", "ExistingToken", "TokenRefreshed"):
            new_token = response.get("token", "")
            print(f"[AUTH] Received token from backend: {'[REDACTED]' if new_token else 'EMPTY'}")
            print(f"[AUTH] Replacing previous token")

            self.token = new_token
            self.is_authorized = True
            self.user_id = 0

            exp_str = response.get("expiration", "")
            if exp_str:
                try:
                    self.expires_at = datetime.fromisoformat(exp_str.replace("Z", "+00:00")).replace(tzinfo=None)
                except ValueError:
                    self.expires_at = datetime.utcnow() + timedelta(hours=24)

            # Update server public key if returned
            spk = response.get("serverPublicKey", "")
            if spk:
                self.server_public_key = spk.encode('utf-8') if isinstance(spk, str) else spk
                self.zmq_client.set_server_public_key(self.server_public_key)

            self._save_token()
            print(f"[AUTH] Token updated and saved!")
            return True

        print(f"[AUTH] _handle_token_response returning False for status: {status}")
        return False

    def is_expired(self) -> bool:
        """Check if token is expired"""
        if not self.expires_at:
            return True

        # Add 5 minute buffer - use UTC time
        now_utc = datetime.now(timezone.utc).replace(tzinfo=None)
        return now_utc >= (self.expires_at - timedelta(minutes=5))

    def is_valid(self) -> bool:
        """Check if we have a valid, non-expired token"""
        return self.is_authorized and self.token and not self.is_expired()

    def authenticate(self) -> bool:
        """
        Authenticate with backend via RequestToken.
        Returns True if successful.
        """
        print("\n" + "#" * 60)
        print("[AUTH] AUTHENTICATING via RequestToken")
        print("#" * 60)

        device_uid = self.device_info.get("id", "UNKNOWN")
        response = self.zmq_client.send_request_token(device_uid, self.email)

        if response is None:
            print("[AUTH] No response from backend (timeout or connection error)")
            print("#" * 60 + "\n")
            return False

        status = response.get("status", "")

        if self._handle_token_response(response):
            print(f"[AUTH] Authorized: True")
            print("#" * 60 + "\n")
            return True

        if status == "DeviceNotRecognized":
            if not self._login_page_opened:
                self._login_page_opened = True
                login_url = f"{WEBAPI_URL}/DeviceLogin?deviceUid={device_uid}"
                webbrowser.open(login_url)
                print(f"[AUTH] Device '{device_uid}' not recognized, opened: {login_url}")
            else:
                print(f"[AUTH] Device '{device_uid}' not recognized (login page already opened)")
            print("#" * 60 + "\n")
            return False

        print(f"[AUTH] Unexpected response: {response}")
        print("#" * 60 + "\n")
        return False

    def refresh_token(self) -> bool:
        """
        Attempt to refresh an expired token.
        Returns True if successful, False if re-auth is needed.
        """
        if not self.token:
            return False

        device_uid = self.device_info.get("id", "UNKNOWN")
        print(f"[AUTH] Refreshing token for device: {device_uid}")

        response = self.zmq_client.send_refresh_token(device_uid, self.token)

        if response is None:
            print("[AUTH] Token refresh failed (no response)")
            return False

        if self._handle_token_response(response):
            print("[AUTH] Token refreshed successfully")
            return True

        print(f"[AUTH] Token refresh failed: {response.get('status', 'unknown')}")
        return False

    def ensure_authenticated(self) -> bool:
        """
        Ensure we have a valid token registered with the backend.
        Always calls RequestToken on startup to ensure backend's TokenStore is populated
        (in-memory TokenStore doesn't survive backend restarts).
        """
        return self.authenticate()

    def get_token(self) -> Optional[str]:
        """
        Get current token.
        Returns None if not authenticated.
        """
        if self.is_valid():
            return self.token
        return None

    def handle_auth_response(self, response: dict) -> bool:
        """
        Handle auth-related status codes from alert responses.
        Returns True if the alert should be retried.
        """
        status = response.get("status", "")

        if status == "InvalidToken":
            print("[AUTH] Backend rejected token, re-authenticating...")
            self.token = None
            self.is_authorized = False
            return self.authenticate()

        if status == "TokenExpired":
            print("[AUTH] Token expired, refreshing...")
            if self.refresh_token():
                return True
            return self.authenticate()

        return False

    def clear(self):
        """Clear authentication data from memory, OS keyring, and disk."""
        print("[AUTH] Clearing authentication")
        if _KEYRING_AVAILABLE:
            try:
                device_uid = self.device_info.get('id', 'unknown')
                keyring.delete_password(_KEYRING_SERVICE, device_uid)
                print("[AUTH] Token removed from OS keyring")
            except Exception:
                pass

        self.token = None
        self.expires_at = None
        self.is_authorized = False
        self.user_id = None

        try:
            if self.storage_path.exists():
                os.remove(self.storage_path)
        except Exception as e:
            print(f"[AUTH] Error removing auth file: {e}")

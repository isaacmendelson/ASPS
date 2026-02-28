"""
AntiScam Desktop App - ZeroMQ Client
ZMQ REQ/REP client for backend server communication
Sends alerts and receives responses
"""

import zmq
import json
import logging
import uuid
from typing import Optional, Dict, Any
from datetime import datetime

logger = logging.getLogger(__name__)


class ZMQClient:
    """
    ZeroMQ Client for backend communication using REQ/REP pattern.

    Alert Format (matches backend python-client-with-notifications.py):
    {
        "AlertType": "UrlAlert" | "RemoteAccessAlert",
        "DeviceInfo": {
            "DeviceUid": "PC-JOHN-001",
            "DeviceType": 1,  # PersonalComputer
            "OperatingSystem": 1,  # Windows
            "MACAddress": "00:11:22:33:44:55"
        },
        "Timestamp": "2024-01-30T12:00:00Z",
        "Priority": 1,  # Medium
        "Token": "",

        # For UrlAlert:
        "Url": "https://example.com",
        "Trackers": [],
        "IFrameDomains": [],
        "UserAgent": "Mozilla/5.0...",

        # For RemoteAccessAlert:
        "RemoteAccessApp": "1",  # AnyDesk
        "RunningProcesses": 2,
        "ConnectionUrl": "192.168.1.1",
        "ConnectionStatus": "1",  # Open
        "SessionStatus": "1"  # Open
    }
    """

    def __init__(self, host: str = "localhost", port: int = 50001):
        self.host = host
        self.port = port
        self.timeout = 5000  # milliseconds
        self.socket = None
        self.server_public_key: Optional[bytes] = None  # Z85-encoded server key
        # Context is expensive — create once and reuse across all requests
        self.context = zmq.Context()

        print(f"[ZMQ] Client initialized")
        print(f"[ZMQ] Server: tcp://{self.host}:{self.port}")

    def set_server_public_key(self, key: bytes):
        """Set the CurveZMQ server public key for encrypted communication."""
        self.server_public_key = key
        print(f"[ZMQ] CURVE server public key set ({len(key)} bytes)")

    def connect(self) -> bool:
        """Open a new REQ socket. Context is reused — only socket is created here."""
        try:
            self.socket = self.context.socket(zmq.REQ)
            self.socket.setsockopt(zmq.RCVTIMEO, self.timeout)
            self.socket.setsockopt(zmq.SNDTIMEO, self.timeout)
            self.socket.setsockopt(zmq.LINGER, 0)  # Don't wait on close

            # Apply CURVE encryption if server key is available
            if self.server_public_key:
                client_public, client_secret = zmq.curve_keypair()
                self.socket.setsockopt(zmq.CURVE_PUBLICKEY, client_public)
                self.socket.setsockopt(zmq.CURVE_SECRETKEY, client_secret)
                self.socket.setsockopt(zmq.CURVE_SERVERKEY, self.server_public_key)

            self.socket.connect(f"tcp://{self.host}:{self.port}")
            return True
        except Exception as e:
            print(f"[ZMQ] ERROR: Connection failed: {e}")
            logger.error(f"ZMQ connection error: {e}")
            return False

    def close(self):
        """Close current socket. Context is kept alive for reuse."""
        if self.socket:
            try:
                self.socket.close()
            except zmq.ZMQError as e:
                logger.debug("Error closing ZMQ socket: %s", e)
            except Exception:
                logger.exception("Unexpected error closing ZMQ socket")
            self.socket = None

    def destroy(self):
        """Terminate the context — call only on app shutdown."""
        self.close()
        if self.context:
            try:
                self.context.term()
            except zmq.ZMQError as e:
                logger.debug("Error terminating ZMQ context: %s", e)
            except Exception:
                logger.exception("Unexpected error terminating ZMQ context")
            self.context = None
            print("[ZMQ] Context terminated")

    def send_alert(self, alert: Dict[str, Any]) -> Optional[Dict[str, Any]]:
        """
        Send an alert and wait for response.

        Args:
            alert: Alert dictionary

        Returns:
            Response dict or None
        """
        if not self.socket:
            print("[ZMQ] ERROR: Not connected. Call connect() first.")
            return None

        try:
            # Convert to JSON
            message_json = json.dumps(alert)
            message_bytes = message_json.encode('utf-8')

            print(f"\n[ZMQ] SENDING: Sending alert...")
            print("-" * 70)
            print(f"[ZMQ] Alert Type: {alert.get('AlertType', 'Unknown')}")
            print(f"[ZMQ] Device: {alert.get('DeviceInfo', {}).get('DeviceUid', 'Unknown')}")
            if alert.get('AlertType') == 'UrlAlert':
                print(f"[ZMQ] URL: {alert.get('Url', 'N/A')}")
            print("-" * 70)
            # Redact token before logging
            log_alert = {**alert, 'Token': '[REDACTED]'} if 'Token' in alert else alert
            print("[ZMQ] FULL MESSAGE: Full message:")
            print(json.dumps(log_alert, indent=2, ensure_ascii=False))
            print("-" * 70)

            # Send
            self.socket.send(message_bytes)
            print("[ZMQ] SUCCESS: Alert sent!")

            # Wait for response
            print("[ZMQ] WAITING: Waiting for response...")
            response_bytes = self.socket.recv()
            response_text = response_bytes.decode('utf-8')

            print(f"\n[ZMQ] RECEIVED: Response received:")
            print("-" * 70)

            try:
                response = json.loads(response_text)
                print(json.dumps(response, indent=2, ensure_ascii=False))
            except json.JSONDecodeError:
                print(response_text)
                response = {"raw": response_text}

            print("-" * 70)
            print("[ZMQ] SUCCESS: Response parsed")

            return response

        except zmq.Again:
            print(f"[ZMQ] WARNING: Timeout: No response after {self.timeout}ms")
            return None
        except Exception as e:
            print(f"[ZMQ] ERROR: Error: {e}")
            logger.error(f"ZMQ send error: {e}")
            return None

    def send_url_alert(self, device_uid: str, url: str, token: str = "",
                       trackers: list = None, iframes: list = None,
                       mac: str = "00:11:22:33:44:55",
                       device_type: int = 1, os_type: int = 1) -> Optional[Dict[str, Any]]:
        """
        Send a UrlAlert.

        Args:
            device_uid: Unique device identifier
            url: URL to analyze
            token: Auth token (optional)
            trackers: List of tracker dicts
            iframes: List of iframe domains
            mac: MAC address
            device_type: Device type (1=PersonalComputer)
            os_type: OS type (1=Windows)

        Returns:
            Response dict with analysis results
        """
        print("\n" + "=" * 70)
        print("[ZMQ] SENDING URL ALERT")
        print("=" * 70)
        print(f"[ZMQ] Server key present: {self.server_public_key is not None}")
        if self.server_public_key:
            print(f"[ZMQ] Server key ({len(self.server_public_key)} bytes): {self.server_public_key[:20]}...")

        # Ensure token is a string
        if token is None:
            token = ""

        alert = {
            "AlertType": "UrlAlert",
            "DeviceInfo": {
                "DeviceUid": device_uid,
                "DeviceType": device_type,
                "OperatingSystem": os_type,
                "MACAddress": mac
            },
            "Timestamp": datetime.utcnow().isoformat() + "Z",
            "Priority": 1,  # Medium
            "Token": token,
            "Url": url,
            "Trackers": trackers or [],
            "IFrameDomains": iframes or [],
            "UserAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"
        }

        if not self.connect():
            return None

        try:
            return self.send_alert(alert)
        finally:
            self.close()

    def send_remote_access_alert(
        self,
        device_uid: str,
        remote_app: str,
        running_processes: int,
        connection_url: str,
        connection_status: str,
        session_status: str,
        token: str = "",
        mac: str = "00:11:22:33:44:55",
        device_type: int = 1,
        os_type: int = 1,
        # Enhanced detection fields
        direction: str = "unknown",  # 'incoming', 'outgoing', 'unknown'
        confidence: str = "low",     # 'low', 'medium', 'high'
        remote_country: str = "",    # Country name
        remote_country_code: str = ""  # ISO country code
    ) -> Optional[Dict[str, Any]]:
        """
        Send a RemoteAccessAlert with enhanced detection data.

        Args:
            device_uid: Unique device identifier
            remote_app: Remote access app ID (1-7)
            running_processes: Number of running processes
            connection_url: Connection URL/IP
            connection_status: Connection status (0=Unknown, 1=Open, 2=Closed)
            session_status: Session status (0=Unknown, 1=Open, 2=Closed)
            token: Auth token (optional)
            mac: MAC address
            device_type: Device type (1=PersonalComputer)
            os_type: OS type (1=Windows)
            direction: Session direction ('incoming', 'outgoing', 'unknown')
            confidence: Detection confidence ('low', 'medium', 'high')
            remote_country: Country name from GeoIP
            remote_country_code: ISO country code (e.g., 'US')

        Returns:
            Response dict
        """
        print("\n" + "=" * 70)
        print("[ZMQ] SENDING REMOTE ACCESS ALERT")
        print("=" * 70)
        print(f"[ZMQ] Direction: {direction}, Confidence: {confidence}")
        if remote_country:
            print(f"[ZMQ] Remote Location: {remote_country} ({remote_country_code})")

        # Ensure token is a string
        if token is None:
            token = ""

        alert = {
            "AlertType": "RemoteAccessAlert",
            "DeviceInfo": {
                "DeviceUid": device_uid,
                "DeviceType": device_type,
                "OperatingSystem": os_type,
                "MACAddress": mac
            },
            "Timestamp": datetime.utcnow().isoformat() + "Z",
            "Priority": 1,  # Medium
            "Token": token,
            "RemoteAccessApp": remote_app,
            "RunningProcesses": running_processes,
            "ConnectionUrl": connection_url,
            "ConnectionStatus": connection_status,
            "SessionStatus": session_status,
            # Enhanced detection fields
            "Direction": direction,
            "Confidence": confidence,
            "RemoteCountry": remote_country,
            "RemoteCountryCode": remote_country_code
        }

        if not self.connect():
            return None

        try:
            return self.send_alert(alert)
        finally:
            self.close()

    def send_request_token(self, device_uid: str, email: str = "") -> Optional[Dict[str, Any]]:
        """
        Send a RequestToken message to authenticate the device.

        Returns:
            Response dict with status, token, expiration, serverPublicKey
        """
        message = {
            "MessageType": "RequestToken",
            "DeviceUid": device_uid,
            "Email": email,
        }

        if not self.connect():
            return None

        try:
            message_json = json.dumps(message)
            print(f"[ZMQ] Sending RequestToken for device: {device_uid}")
            self.socket.send(message_json.encode('utf-8'))

            response_bytes = self.socket.recv()
            response = json.loads(response_bytes.decode('utf-8'))
            print(f"[ZMQ] RequestToken response: {response.get('status', 'unknown')}")
            return response

        except zmq.Again:
            print(f"[ZMQ] WARNING: RequestToken timeout after {self.timeout}ms")
            return None
        except Exception as e:
            print(f"[ZMQ] ERROR: RequestToken error: {e}")
            return None
        finally:
            self.close()

    def send_refresh_token(self, device_uid: str, old_token: str) -> Optional[Dict[str, Any]]:
        """
        Send a RefreshToken message to renew an expired token.

        Returns:
            Response dict with status, token, expiration, serverPublicKey
        """
        message = {
            "MessageType": "RefreshToken",
            "DeviceUid": device_uid,
            "Token": old_token,
            "Timestamp": datetime.utcnow().isoformat() + "Z",
        }

        if not self.connect():
            return None

        try:
            message_json = json.dumps(message)
            print(f"[ZMQ] Sending RefreshToken for device: {device_uid}")
            self.socket.send(message_json.encode('utf-8'))

            response_bytes = self.socket.recv()
            response = json.loads(response_bytes.decode('utf-8'))
            print(f"[ZMQ] RefreshToken response: {response.get('status', 'unknown')}")
            return response

        except zmq.Again:
            print(f"[ZMQ] WARNING: RefreshToken timeout after {self.timeout}ms")
            return None
        except Exception as e:
            print(f"[ZMQ] ERROR: RefreshToken error: {e}")
            return None
        finally:
            self.close()


# Standalone test
if __name__ == "__main__":
    import sys

    logging.basicConfig(level=logging.INFO)

    print("=" * 70)
    print("ZMQ CLIENT - STANDALONE TEST")
    print("=" * 70)

    # Get server from command line or use default
    host = sys.argv[1] if len(sys.argv) > 1 else "localhost"

    client = ZMQClient(host, 50001)

    print("\nMENU: Choose test:")
    print("1. URL Alert")
    print("2. Remote Access Alert")

    choice = input("\nEnter choice (1-2, default=1): ").strip() or "1"

    if choice == "1":
        url = input("Enter URL (default=http://example.com): ").strip() or "http://example.com"
        response = client.send_url_alert(
            device_uid="PC-TEST-001",
            url=url
        )

        if response:
            print(f"\nSUCCESS: Test completed!")
        else:
            print("\nERROR: Test failed!")

    elif choice == "2":
        response = client.send_remote_access_alert(
            device_uid="PC-TEST-001",
            remote_app="1",  # AnyDesk
            running_processes=2,
            connection_url="192.168.1.100",
            connection_status="1",  # Open
            session_status="1"  # Open
        )

        if response:
            print(f"\nSUCCESS: Test completed!")
        else:
            print("\nERROR: Test failed!")

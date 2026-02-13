"""
AntiScam Desktop App - TCP Client
TCP client for backend server communication
Sends requests and receives responses
Based on the backend protocol format
"""

import socket
import json
import time
import logging
from typing import Optional
from datetime import datetime

logger = logging.getLogger(__name__)

# Client identifier
CLIENT_ID = "antiscam_desktop_1"


class TCPClient:
    """
    TCP Client for backend communication.
    
    Message Format:
    {
        "Token": "...",
        "Command": "DeviceAuth" / "DeviceAlert",
        "Payload": { DeviceInfo, AlertType, ... },
        "Timestamp": "ISO format",
        "Type": "Auth" / "Alert",
        "JsonTypeName": "DeviceAuthRequest" / etc,
        "ClientId": "...",
        "CorrelationId": "..."
    }
    """
    
    def __init__(self, host: str = "localhost", port: int = 50001):
        self.host = host
        self.port = port
        self.timeout = 30  # seconds
        self.socket = None
        
        print(f"[TCP] Client initialized")
        print(f"[TCP] Server: {self.host}:{self.port}")
    
    def connect(self) -> bool:
        """Connect to TCP server"""
        try:
            self.socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.socket.settimeout(self.timeout)
            self.socket.connect((self.host, self.port))
            print(f"[TCP] ✅ Connected to {self.host}:{self.port}")
            return True
        except Exception as e:
            print(f"[TCP] ❌ Connection failed: {e}")
            self.socket = None
            return False
    
    def close(self):
        """Close connection"""
        if self.socket:
            try:
                self.socket.close()
                print("[TCP] ✅ Connection closed")
            except OSError as e:
                logger.debug("Error closing socket: %s", e)
            except Exception:
                logger.exception("Unexpected error closing socket")
            self.socket = None
    
    def _get_timestamp(self) -> str:
        """Get current timestamp in ISO format"""
        return datetime.utcnow().isoformat() + 'Z'
    
    def _get_correlation_id(self) -> str:
        """Generate correlation ID"""
        return f"{CLIENT_ID}-{int(time.time())}"
    
    def send_message(self, message: dict, wait_for_response: bool = True) -> Optional[dict]:
        """
        Send a message and optionally wait for response.
        
        Args:
            message: Message dict to send
            wait_for_response: If True, wait for server response
            
        Returns:
            Response dict or None
        """
        if not self.socket:
            print("[TCP] ❌ Not connected. Call connect() first.")
            return None
        
        try:
            # Convert to JSON and add newline
            message_json = json.dumps(message, ensure_ascii=False)
            message_bytes = (message_json + '\n').encode('utf-8')
            
            print(f"\n[TCP] Sending {len(message_bytes)} bytes...")
            print("-" * 60)
            print(f"[TCP] MESSAGE: {json.dumps(message, indent=2, ensure_ascii=False)}")
            print("-" * 60)
            
            # Send
            self.socket.sendall(message_bytes)
            print("[TCP] ✅ Sent!")
            
            # Wait for response if requested
            if wait_for_response:
                print("[TCP] ⏳ Waiting for response...")
                return self._receive_response()
            
            return None
            
        except Exception as e:
            print(f"[TCP] ❌ Send failed: {e}")
            return None
    
    def _receive_response(self, buffer_size: int = 4096) -> Optional[dict]:
        """Receive response from server"""
        try:
            data = self.socket.recv(buffer_size)
            
            if data:
                response_text = data.decode('utf-8').strip()
                print(f"\n[TCP] 📥 Received: {response_text}")
                
                # Parse JSON
                response = json.loads(response_text)
                
                # Check for errors
                error_message = response.get("ErrorMessage")
                if error_message:
                    print(f"[TCP] ⚠️ Error: {error_message}")
                    if isinstance(error_message, dict):
                        print(f"[TCP] Error Key: {error_message.get('Key')}")
                
                print(f"[TCP] ✅ Response received")
                return response
            else:
                print("[TCP] ⚠️ Connection closed by server")
                return None
                
        except socket.timeout:
            print(f"[TCP] ⚠️ Timeout waiting for response")
            return None
        except Exception as e:
            print(f"[TCP] ❌ Receive failed: {e}")
            return None
    
    def send_device_auth_request(self, email: str, device_info: dict) -> Optional[dict]:
        """
        Send DeviceAuthRequest
        
        Args:
            email: User email
            device_info: Device information dict
            
        Returns:
            Response with Token if successful
        """
        print("\n" + "=" * 60)
        print("[TCP] SENDING DeviceAuthRequest")
        print("=" * 60)
        
        # Build the inner message
        inner_message = {
            "JsonTypeName": "DeviceAuthRequest",
            "email": email,
            "priority": 1,
            "timestamp": datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
            "deviceInfo": device_info
        }
        
        # Build the full message
        message = {
            "Token": "",
            "Command": "DeviceAuth",
            "Payload": {
                "DeviceInfo": device_info,
                "AlertType": "Auth",
                "Type": "Auth",
                "Priority": "1",
                "Severity": "High",
                "Message": inner_message,
                "ClientId": CLIENT_ID,
            },
            "Timestamp": self._get_timestamp(),
            "Type": "Auth",
            "JsonTypeName": "DeviceAuthRequest",
            "ClientId": CLIENT_ID,
            "CorrelationId": self._get_correlation_id()
        }
        
        # Connect, send, close
        if not self.connect():
            return None
        
        response = self.send_message(message, wait_for_response=True)
        self.close()
        
        return response
    
    def send_url_alert(self, token: str, url: str, trackers: list, 
                       iframes: list, device_info: dict) -> Optional[dict]:
        """
        Send SuspiceousUrlAlert
        
        Args:
            token: Auth token
            url: URL to check
            trackers: List of tracker dicts
            iframes: List of iframe domains
            device_info: Device information dict
            
        Returns:
            Response with Score if successful
        """
        print("\n" + "=" * 60)
        print("[TCP] SENDING SuspiceousUrlAlert")
        print("=" * 60)
        
        # Build the inner message
        inner_message = {
            "JsonTypeName": "SuspiceousUrlAlert",
            "Priority": 1,
            "Timestamp": datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
            "Url": url,
            "Trackers": trackers,
            "IFrameDomains": iframes
        }
        
        # Build the full message
        message = {
            "Token": token,
            "Command": "DeviceAlert",
            "Payload": {
                "DeviceInfo": device_info,
                "AlertType": "Alert",
                "Type": "Alert",
                "Priority": "1",
                "Severity": "High",
                "Message": inner_message,
                "ClientId": CLIENT_ID,
            },
            "Timestamp": self._get_timestamp(),
            "Type": "Alert",
            "JsonTypeName": "SuspiceousUrlAlert",
            "ClientId": CLIENT_ID,
            "CorrelationId": self._get_correlation_id()
        }
        
        # Connect, send, close
        if not self.connect():
            return None
        
        response = self.send_message(message, wait_for_response=True)
        self.close()
        
        return response
    
    def send_remote_access_alert(self, token: str, user_id: int, device_uid: str,
                                  app_id: int, running_processes: int,
                                  connection_status: int, connections_count: int,
                                  cpu_usage: float, session_status: int,
                                  device_info: dict) -> Optional[dict]:
        """
        Send RemoteAccessAlert
        
        Args:
            token: Auth token
            user_id: User ID
            device_uid: Device UID
            app_id: Remote access app ID
            running_processes: Number of running processes
            connection_status: Connection status
            connections_count: Number of connections
            cpu_usage: CPU usage percentage
            session_status: Session status
            device_info: Device information dict
            
        Returns:
            Response if successful
        """
        print("\n" + "=" * 60)
        print("[TCP] SENDING RemoteAccessAlert")
        print("=" * 60)
        
        # Build the inner message
        inner_message = {
            "JsonTypeName": "RemoteAccessAlert",
            "UserId": user_id,
            "DeviceUid": device_uid,
            "RemoteControlApp": app_id,
            "ConnectionUrl": "",
            "RunningProcesses": running_processes,
            "ConnectionStatus": connection_status,
            "ConnectionsCount": connections_count,
            "RcCpuUsage": int(cpu_usage),
            "LogCheckResult": "",
            "SessionStatus": session_status
        }
        
        # Build the full message
        message = {
            "Token": token,
            "Command": "DeviceAlert",
            "Payload": {
                "DeviceInfo": device_info,
                "AlertType": "Alert",
                "Type": "Alert",
                "Priority": "1",
                "Severity": "High",
                "Message": inner_message,
                "ClientId": CLIENT_ID,
            },
            "Timestamp": self._get_timestamp(),
            "Type": "Alert",
            "JsonTypeName": "RemoteAccessAlert",
            "ClientId": CLIENT_ID,
            "CorrelationId": self._get_correlation_id()
        }
        
        # Connect, send, close
        if not self.connect():
            return None
        
        response = self.send_message(message, wait_for_response=True)
        self.close()
        
        return response


# Standalone test
if __name__ == "__main__":
    import uuid
    
    logging.basicConfig(level=logging.DEBUG)
    
    print("=" * 60)
    print("TCP CLIENT - STANDALONE TEST")
    print("=" * 60)
    
    client = TCPClient("localhost", 50001)
    
    device_info = {
        "id": str(uuid.uuid4()),
        "ver": "0.1.0",
        "ip": "127.0.0.1",
        "userAgent": "Test Client",
        "timezone": 2,
        "OperatingSystem": 1
    }
    
    # Test auth
    print("\n1. Testing DeviceAuthRequest...")
    response = client.send_device_auth_request("test@example.com", device_info)
    
    if response and response.get("IsAuthorized"):
        token = response.get("Token")
        print(f"\n✅ Got token: {token}")
        
        # Test URL alert
        print("\n2. Testing SuspiceousUrlAlert...")
        url_response = client.send_url_alert(
            token=token,
            url="https://example.com",
            trackers=[{"Type": "fbPixel", "Value": "123456"}],
            iframes=["ads.example.com"],
            device_info=device_info
        )
        
        if url_response:
            print(f"\n✅ Score: {url_response.get('Score')}")
    else:
        print("\n❌ Auth failed!")
        if response:
            print(f"Response: {response}")

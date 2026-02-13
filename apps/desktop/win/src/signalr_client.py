"""
AntiScam Desktop App - SignalR Client
Receives real-time notifications from backend
"""

import logging
import threading
import time
from typing import Optional, Callable

logger = logging.getLogger(__name__)


class SignalRClient:
    """
    SignalR Client for receiving notifications.
    
    - Connects to backend SignalR hub
    - Subscribes to user notifications
    - Receives alerts from all devices
    """
    
    def __init__(self, server_url: str, hub_path: str = "/notificationshub"):
        self.server_url = server_url.rstrip('/')
        self.hub_path = hub_path
        self.hub_url = f"{self.server_url}{hub_path}"
        
        self.hub_connection = None
        self.client_id: Optional[str] = None
        self._connected = threading.Event()
        self._running = False
        
        # Callbacks
        self._on_notification_callback: Optional[Callable] = None
        self._on_connected_callback: Optional[Callable] = None
        self._on_disconnected_callback: Optional[Callable] = None
        
        print(f"[SIGNALR] Client initialized")
        print(f"[SIGNALR] Hub URL: {self.hub_url}")
    
    def on_notification(self, callback: Callable):
        """Set callback for notifications"""
        self._on_notification_callback = callback
    
    def on_connected(self, callback: Callable):
        """Set callback for connection established"""
        self._on_connected_callback = callback
    
    def on_disconnected(self, callback: Callable):
        """Set callback for disconnection"""
        self._on_disconnected_callback = callback
    
    def _handle_notification(self, data):
        """Handle incoming notification"""
        print("\n" + "!" * 60)
        print("[SIGNALR] NOTIFICATION RECEIVED!")
        print("!" * 60)
        
        try:
            # SignalR sends data as array
            if isinstance(data, list) and len(data) > 0:
                actual_data = data[0]
            else:
                actual_data = data
            
            print(f"[SIGNALR] Type: {actual_data.get('Message', 'N/A')}")
            print(f"[SIGNALR] Data: {actual_data.get('Data', 'N/A')}")
            print(f"[SIGNALR] Timestamp: {actual_data.get('Timestamp', 'N/A')}")
            
            if self._on_notification_callback:
                self._on_notification_callback(actual_data)
                
        except Exception as e:
            print(f"[SIGNALR] Error handling notification: {e}")
        
        print("!" * 60 + "\n")
    
    def _handle_open(self):
        """Handle connection opened"""
        print("\n" + "#" * 60)
        print("[SIGNALR] CONNECTED!")
        print("#" * 60)
        
        # Subscribe to notifications
        if self.client_id:
            try:
                print(f"[SIGNALR] Subscribing with client ID: {self.client_id}")
                self.hub_connection.invoke("SubscribeToNotifications", self.client_id)
                print("[SIGNALR] Subscription request sent")
            except Exception as e:
                print(f"[SIGNALR] Error subscribing: {e}")
        
        self._connected.set()
        
        if self._on_connected_callback:
            self._on_connected_callback()
        
        print("#" * 60 + "\n")
    
    def _handle_close(self):
        """Handle connection closed"""
        print("\n[SIGNALR] CONNECTION CLOSED\n")
        self._connected.clear()
        
        if self._on_disconnected_callback:
            self._on_disconnected_callback()
    
    def _handle_error(self, error):
        """Handle error"""
        print(f"[SIGNALR] Error: {error}")
        logger.error(f"SignalR error: {error}")
    
    def connect(self, client_id: str) -> bool:
        """
        Connect to SignalR hub.
        
        Args:
            client_id: Unique client identifier for subscriptions
        """
        self.client_id = client_id
        
        print("\n" + "=" * 60)
        print("[SIGNALR] CONNECTING")
        print("=" * 60)
        print(f"[SIGNALR] URL: {self.hub_url}")
        print(f"[SIGNALR] Client ID: {client_id}")
        print("=" * 60)
        
        try:
            # Import here to avoid issues if not installed
            from signalrcore.hub_connection_builder import HubConnectionBuilder
            
            # Build connection
            print("[SIGNALR] Building connection...")
            self.hub_connection = HubConnectionBuilder() \
                .with_url(self.hub_url) \
                .configure_logging(logging.WARNING) \
                .with_automatic_reconnect({
                    "type": "raw",
                    "keep_alive_interval": 10,
                    "reconnect_interval": 5,
                    "max_attempts": 10
                }) \
                .build()
            
            # Register handlers
            print("[SIGNALR] Registering handlers...")
            self.hub_connection.on("ReceiveNotification", self._handle_notification)
            self.hub_connection.on_open(self._handle_open)
            self.hub_connection.on_close(self._handle_close)
            self.hub_connection.on_error(self._handle_error)
            
            # Start connection
            print("[SIGNALR] Starting connection...")
            self.hub_connection.start()
            self._running = True
            
            # Wait for connection
            print("[SIGNALR] Waiting for connection...")
            if self._connected.wait(timeout=15):
                print("[SIGNALR] Connected successfully!")
                return True
            else:
                print("[SIGNALR] Connection timeout!")
                return False
                
        except ImportError:
            print("[SIGNALR] ERROR: signalrcore not installed!")
            print("[SIGNALR] Run: pip install signalrcore")
            return False
            
        except Exception as e:
            print(f"[SIGNALR] ERROR: {type(e).__name__} - {e}")
            logger.error(f"SignalR connection error: {e}")
            return False
    
    def disconnect(self):
        """Disconnect from hub"""
        print("[SIGNALR] Disconnecting...")
        self._running = False
        
        if self.hub_connection:
            try:
                self.hub_connection.stop()
                print("[SIGNALR] Disconnected")
            except Exception as e:
                print(f"[SIGNALR] Error disconnecting: {e}")
    
    @property
    def is_connected(self) -> bool:
        return self._connected.is_set()
    
    def wait_for_notifications(self):
        """Block and wait for notifications (for standalone use)"""
        print("\n[SIGNALR] Listening for notifications...")
        print("[SIGNALR] Press Ctrl+C to stop\n")
        
        try:
            while self._running:
                time.sleep(1)
        except KeyboardInterrupt:
            print("\n[SIGNALR] Interrupted")
            self.disconnect()


# Standalone test
if __name__ == "__main__":
    import sys
    
    logging.basicConfig(level=logging.INFO)
    
    print("=" * 60)
    print("SIGNALR CLIENT - STANDALONE TEST")
    print("=" * 60)
    
    # Get port from command line or use default
    if len(sys.argv) > 1:
        port = sys.argv[1]
    else:
        port = input("Enter SignalR port (default 49569): ").strip() or "49569"
    
    server_url = f"http://localhost:{port}"
    client_id = "test_client_1"
    
    def on_notification(data):
        print(f"\n*** NOTIFICATION: {data} ***\n")
    
    def on_connected():
        print("\n*** CONNECTED! ***\n")
    
    def on_disconnected():
        print("\n*** DISCONNECTED! ***\n")
    
    client = SignalRClient(server_url)
    client.on_notification(on_notification)
    client.on_connected(on_connected)
    client.on_disconnected(on_disconnected)
    
    if client.connect(client_id):
        print("\n✅ Connected! Waiting for notifications...")
        print("\nTo test, send a notification from the backend:")
        print(f"  curl -X POST {server_url}/api/debug/send-to/{client_id}")
        
        client.wait_for_notifications()
    else:
        print("\n❌ Failed to connect!")
        print("\nMake sure:")
        print("1. Backend is running")
        print(f"2. SignalR hub is available at {server_url}/notificationshub")

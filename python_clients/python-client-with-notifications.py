#!/usr/bin/env python3
"""
Enhanced Python client for ASPSBackend with notification subscription
Requires: pip install pyzmq
"""

import zmq
import json
import time
import threading
from datetime import datetime

class NotificationListener:
    """Listens for notifications from ASPSBackend on a separate thread"""
    
    def __init__(self, device_uid, port=50002, host="localhost"):
        self.device_uid = device_uid
        self.port = port
        self.host = host
        self.running = False
        self.thread = None
        self.context = None
        self.socket = None
        
    def start(self):
        """Start listening for notifications in a background thread"""
        self.running = True
        self.thread = threading.Thread(target=self._listen, daemon=True)
        self.thread.start()
        print(f"\n📡 Subscribed to notifications for device: {self.device_uid}")
        print(f"   Listening on tcp://{self.host}:{self.port}")
        print("=" * 70)
        
    def _listen(self):
        """Background thread that listens for notifications"""
        self.context = zmq.Context()
        self.socket = self.context.socket(zmq.SUB)
        self.socket.connect(f"tcp://{self.host}:{self.port}")
        
        # Subscribe to notifications for this device
        topic = f"device:{self.device_uid}"
        self.socket.subscribe(topic.encode('utf-8'))
        
        print(f"🎧 Listening for notifications on topic: '{topic}'...\n")
        
        while self.running:
            try:
                # Receive multipart message: [topic, message]
                topic_bytes = self.socket.recv()
                message_bytes = self.socket.recv()
                
                topic_str = topic_bytes.decode('utf-8')
                message_str = message_bytes.decode('utf-8')
                
                # Parse and display notification
                self._handle_notification(topic_str, message_str)
                
            except zmq.Again:
                # Timeout, continue
                continue
            except Exception as e:
                if self.running:
                    print(f"\n❌ Error receiving notification: {e}")
                break
    
    def _handle_notification(self, topic, message_json):
        """Handle received notification"""
        try:
            notification = json.loads(message_json)
            
            print("\n" + "=" * 70)
            print("🔔 NOTIFICATION RECEIVED")
            print("=" * 70)
            print(f"📌 Topic: {topic}")
            print(f"⏰ Timestamp: {notification.get('Timestamp', 'N/A')}")
            print(f"🖥️  Device: {notification.get('DeviceUid', 'N/A')}")
            print('xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx')
            data = notification.get('Data', {})
            print(f"\n📊 Analysis Result:")
            print(f"   Alert Type: {data.get('AlertType', 'N/A')}")
            print(f"   Severity: {data.get('Severity', 'N/A')}")
            print(f"   Message: {data.get('Message', 'N/A')}")
            print(f"   RiskAssessment: {data.get('RiskAssessment', 'N/A')}")
            print(f"   ProtectiveActions: {data.get('protectiveActions', 'N/A')}")
            
            # Display analyzer results if available
            analyzer_results = data.get('AnalyzerResults', {})
            if analyzer_results:
                print(f"\n🔍 Analyzer Results:")
                for analyzer_name, result in analyzer_results.items():
                    print(f"   • {analyzer_name}")
                    if isinstance(result, dict):
                        print('yyyyyyyyyyyyyyyyyy')
                        # Try to show URL analysis details
                        if 'Item1' in result:
                            print(f"     Item1: {result['Item1']}")
                        if 'Item2' in result:
                            print(f"     Item2: {result['Item2']}")
                        if 'Item3' in result:
                            print(f"     Item3: {result['Item3']}")
                        if 'Url' in result:
                            print(f"     URL: {result['Url']}")
                        if 'Domain' in result:
                            print(f"     Domain: {result['Domain']}")
                        if 'risk_assessment' in result:
                            risk = result['risk_assessment']
                            print(f"     Risk Score: {risk.get('risk_score', 'N/A')}")
                            print(f"     Is Scam: {risk.get('is_scam', 'N/A')}")
                        if 'RiskAssessment' in result:
                            print('xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx')
                            risk = result['RiskAssessment']
                            print(f"     Risk Score: {risk.get('risk_score', 'N/A')}")
                            print(f"     Is Scam: {risk.get('is_scam', 'N/A')}")
            
            # Display details
            details = data.get('Details', {})
            if details:
                print(f"\n📝 Additional Details:")
                for key, value in details.items():
                    if not isinstance(value, (dict, list)):
                        print(f"   {key}: {value}")
            
            print("=" * 70)
            print("🎧 Listening for more notifications... (Ctrl+C to exit)\n")
            
        except json.JSONDecodeError:
            print(f"\n⚠️  Received non-JSON notification: {message_json[:100]}")
        except Exception as e:
            print(f"\n❌ Error parsing notification: {e}")
    
    def stop(self):
        """Stop listening for notifications"""
        self.running = False
        if self.socket:
            self.socket.close()
        if self.context:
            self.context.term()
        if self.thread:
            self.thread.join(timeout=2)
        print("\n📡 Stopped listening for notifications")

def send_RemoteAccess_alert_with_notifications(port=50001, notification_port=50002, host="localhost"):
    """SeAlert and subscribe to notifications"""
     # Get device and user info
    device_uid = "PC-JOHN-001"
    device_owner = input("User 1: John or 2: Jane (default=1): ").strip()
    if device_owner == "2":
        device_uid = "PC-JANE-001"

    print(f"\n👤 Selected: {device_uid}")

    remote_access_app = input("RemoteAccess application 1 AnyDesk, 2 TeamViewer, 3 ChromeRemoteDesktop, 4 RemotePC, 5 LogMeIn, 6 Splashtop, 7 VNC (default=1): ").strip() or "1"
    print(f"Remote_access_app: {remote_access_app}")
    running_processes = 2
    print(f"running_processes: {running_processes}")
    connection_url = "192.198.60.101"
    print(f"connection_url: {connection_url}")
    connection_status = input("connection_status status  0 Unknown, 1 Open, 2 Closed (default=1): ").strip() or "1"
    print(f"connection_status: {connection_status}")
    session_status = input("Session status  0 Closed Unknown, 1 Open, 2 Closed (default=1): ").strip() or "1"
    print(f"session_status: {session_status}")

    listener = NotificationListener(device_uid, notification_port, host)
    listener.start()

    # Give listener time to connect and subscribe
    time.sleep(0.5)

    context = zmq.Context()
    socket = context.socket(zmq.REQ)
    socket.connect(f"tcp://{host}:{port}")
    socket.setsockopt(zmq.RCVTIMEO, 5000)

    print("=" * 70)
    print("📤 SENDING REMOTE_ACCESS ALERT")
    print("=" * 70)

    alert = {
        "AlertType": "RemoteAccessAlert",
        "DeviceInfo": {
            "DeviceUid": device_uid
            ,
            "DeviceType": 1,  # PersonalComputer
            "OperatingSystem": 1,  # Windows
            "MAC": "00:11:22:33:44:55"
        },
        "Timestamp": datetime.utcnow().isoformat() + "Z",
        "Priority": 1,  # Medium
        "Token": "",
        "RemoteAccessApp": remote_access_app,
        "RunningProcesses": running_processes,
        "ConnectionUrl": connection_url,
        "ConnectionStatus": connection_status,
        "SessionStatus": session_status,
    }

    message_json = json.dumps(alert)
    message_bytes = message_json.encode('utf-8')

    print(f"🖥️  Device: {device_uid}")
    print(f"⚠️  Priority: {alert['Priority']}")

    try:
        # Send the alert
        socket.send(message_bytes)
        print("\n✅ Alert sent to backend!")
        print("⏳ Waiting for immediate response...")

        # Wait for immediate response
        try:
            response_bytes = socket.recv()
            response = response_bytes.decode('utf-8')

            print("\n📨 Immediate Response:")
            print("-" * 70)
            try:
                response_json = json.loads(response)
                print(json.dumps(response_json, indent=2))
            except:
                print(response)
            print("-" * 70)
        except zmq.Again:
            print("\n⚠️  No immediate response (server might be processing async)")

        print("\n" + "=" * 70)
        print("🎧 Now listening for analysis notifications...")
        print("   (Analysis may take a few seconds)")
        print("   Press Ctrl+C to exit")
        print("=" * 70 + "\n")

        # Keep the program running to receive notifications
        try:
            while True:
                time.sleep(1)
        except KeyboardInterrupt:
            print("\n\n⚠️  Ctrl+C detected")

    except Exception as e:
        print(f"\n❌ Error: {e}")
    finally:
        listener.stop()
        socket.close()
        context.term()

def send_url_alert_with_notifications(port=50001, notification_port=50002, host="localhost"):
    """Send a UrlAlert and subscribe to notifications"""
    
    # Get device and user info
    deviceUid = "PC-JOHN-001"
    device_owner = input("User 1: John or 2: Jane (default=1): ").strip() or "1"
    if device_owner == "2":
        deviceUid = "PC-JANE-001"
    
    print(f"\n👤 Selected: {deviceUid}")

    # Get URL to analyze
    subject_url = input("Enter URL to analyze (default=http://example.com): ").strip()
    if not subject_url:
        subject_url = "http://example.com"
    
    print(f"🔗 URL: {subject_url}\n")
    
    # Start notification listener BEFORE sending alert
    listener = NotificationListener(deviceUid, notification_port, host)
    listener.start()
    
    # Give listener time to connect and subscribe
    time.sleep(0.5)
    
    # Create and send the alert
    context = zmq.Context()
    socket = context.socket(zmq.REQ)
    socket.connect(f"tcp://{host}:{port}")
    socket.setsockopt(zmq.RCVTIMEO, 5000)
    
    print("=" * 70)
    print("📤 SENDING URL ALERT")
    print("=" * 70)
    
    alert = {
        "AlertType": "UrlAlert",
        "DeviceInfo": {
            "DeviceUid": deviceUid,
            "DeviceType": 1,  # PersonalComputer
            "OperatingSystem": 1,  # Windows
            "MAC": "00:11:22:33:44:55"
        },
        "Timestamp": datetime.utcnow().isoformat() + "Z",
        "Priority": 1,  # Medium
        "Token": "",
        "Url": subject_url,
        "Trackers": [],
        "IFrameDomains": [],
        "UserAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"
    }
    
    message_json = json.dumps(alert)
    message_bytes = message_json.encode('utf-8')
    
    print(f"🖥️  Device: {deviceUid}")
    print(f"🔗 URL: {subject_url}")
    print(f"⚠️  Priority: {alert['Priority']}")
    
    try:
        # Send the alert
        socket.send(message_bytes)
        print("\n✅ Alert sent to backend!")
        print("⏳ Waiting for immediate response...")
        
        # Wait for immediate response
        try:
            response_bytes = socket.recv()
            response = response_bytes.decode('utf-8')
            
            print("\n📨 Immediate Response:")
            print("-" * 70)
            try:
                response_json = json.loads(response)
                print(json.dumps(response_json, indent=2))
            except:
                print(response)
            print("-" * 70)
        except zmq.Again:
            print("\n⚠️  No immediate response (server might be processing async)")
        
        print("\n" + "=" * 70)
        print("🎧 Now listening for analysis notifications...")
        print("   (Analysis may take a few seconds)")
        print("   Press Ctrl+C to exit")
        print("=" * 70 + "\n")
        
        # Keep the program running to receive notifications
        try:
            while True:
                time.sleep(1)
        except KeyboardInterrupt:
            print("\n\n⚠️  Ctrl+C detected")
            
    except Exception as e:
        print(f"\n❌ Error: {e}")
    finally:
        listener.stop()
        socket.close()
        context.term()


def send_RemoteAccess_alert_simple(port=50001, host="localhost"):
    """Send a RemoteAccessAlert without notifications (original behavior)"""
    print(f"\n📤 send_RemoteAccess_alert_simple is Not Implemented")


def send_url_alert_simple(port=50001, host="localhost"):
    """Send a UrlAlert without notifications (original behavior)"""
    
    deviceUid = "PC-JOHN-001"
    device_owner = input("User 1: John or 2: Jane (default=1): ").strip() or "1"
    if device_owner == "2":
        deviceUid = "PC-JANE-001"
    
    subject_url = input("Enter URL (default=http://example.com): ").strip()
    if not subject_url:
        subject_url = "http://example.com"
    
    context = zmq.Context()
    socket = context.socket(zmq.REQ)
    socket.connect(f"tcp://{host}:{port}")
    socket.setsockopt(zmq.RCVTIMEO, 5000)
    
    alert = {
        "AlertType": "UrlAlert",
        "DeviceInfo": {
            "DeviceUid": deviceUid,
            "DeviceType": 1,
            "OperatingSystem": 1,
            "MAC": "00:11:22:33:44:55"
        },
        "Timestamp": datetime.utcnow().isoformat() + "Z",
        "Priority": 1,
        "Token": "",
        "Url": subject_url,
        "Trackers": [],
        "IFrameDomains": [],
        "UserAgent": "Mozilla/5.0"
    }
    
    print(f"\n📤 Sending UrlAlert: {subject_url}")
    
    try:
        socket.send(json.dumps(alert).encode('utf-8'))
        print("✅ Alert sent, waiting for response...")
        
        response = socket.recv().decode('utf-8')
        print("\n📨 Response:")
        print(json.dumps(json.loads(response), indent=2))
        
    except zmq.Again:
        print("⚠️  Timeout: No response")
    except Exception as e:
        print(f"❌ Error: {e}")
    finally:
        socket.close()
        context.term()


if __name__ == "__main__":
    print("=" * 70)
    print("🚀 ASPSBackend Python Client - With Notifications")
    print("=" * 70)
    
    print("\n📋 Choose mode:")
    print("1. Simple URL Alert (no notifications)")
    print("2. URL Alert + Subscribe to notifications (RECOMMENDED)")
    print("3. Simple RemoteAccess Alert (no notifications)")
    print("4. RemoteAccess Alert + Subscribe to notifications (RECOMMENDED)")
    print("5. Exit")
    
    choice = input("\nEnter choice (1-5, default=2): ").strip() or "2"
    
    if choice == "1":
        print("\n" + "=" * 70)
        print("📤 Simple Mode")
        print("=" * 70)
        send_url_alert_simple()
        
    elif choice == "2":
        print("\n" + "=" * 70)
        print("📡 Notification Mode")
        print("=" * 70)
        send_url_alert_with_notifications()

    elif choice == "3":
        print("\n" + "=" * 70)
        print("📤 Simple Mode")
        print("=" * 70)
        send_RemoteAccess_alert_simple()

    elif choice == "4":
        print("\n" + "=" * 70)
        print("📡 Notification Mode")
        print("=" * 70)
        send_RemoteAccess_alert_with_notifications()
        
    elif choice == "5":
        print("\n👋 Goodbye!")
        
    else:
        print("\n⚠️  Invalid choice")
    
    print("\n" + "=" * 70)
    print("✅ Done!")
    print("=" * 70)

#!/usr/bin/env python3
"""
Example Python client for sending device alerts to ASPSBackend
Requires: pip install pyzmq
"""

import zmq
import json
import time
from datetime import datetime

def send_remote_access_alert_with_response(port=50001, host="localhost"):
    """Send a RemoteAccessAlert and wait for response"""
    
    context = zmq.Context()
    
    # Use REQ socket instead of PUSH to get responses
    socket = context.socket(zmq.REQ)
    socket.connect(f"tcp://{host}:{port}")
    
    # Set receive timeout to 5 seconds
    socket.setsockopt(zmq.RCVTIMEO, 5000)
    
    print(f"Connected to tcp://{host}:{port}")
    
    # Create a sample RemoteAccessAlert matching the C# model
    alert = {
        "AlertType": "RemoteAccessAlert",
        "DeviceInfo": {
            "DeviceUid": "PC-12345",
            "DeviceType": 1,  # 1 = PersonalComputer
            "OperatingSystem": 1,  # 1 = Windows
            "MAC": "00:11:22:33:44:55"
        },
        "Timestamp": datetime.utcnow().isoformat() + "Z",
        "Priority": 2,  # 0=Low, 1=Medium, 2=High, 3=Critical
        "Token": "",
        "RemoteAccessApp": 1,  # Enum value for remote access app type
        "RunningProcesses": 5,
        "ConnectionUrl": "rdp://192.168.1.100:3389",
        "ConnectionStatus": 1,  # Enum value for connection status
        "ConnectionsCount": 1,
        "SessionStatus": 1
    }
    
    # Convert to JSON and encode as UTF-8
    message_json = json.dumps(alert)
    message_bytes = message_json.encode('utf-8')
    
    print(f"\nSending RemoteAccessAlert:")
    print(f"  Device: {alert['DeviceInfo']['DeviceUid']}")
    print(f"  Priority: {alert['Priority']}")
    print(f"  Connection: {alert['ConnectionUrl']}")
    
    try:
        # Send the message
        socket.send(message_bytes)
        print("\n✓ Alert sent, waiting for response...")
        
        # Wait for response
        response_bytes = socket.recv()
        response = response_bytes.decode('utf-8')
        
        print("\n✓ Response received:")
        print("-" * 50)
        
        # Try to parse as JSON for pretty printing
        try:
            response_json = json.loads(response)
            print(json.dumps(response_json, indent=2))
        except:
            # If not JSON, just print raw response
            print(response)
        
        print("-" * 50)
        
    except zmq.Again:
        print("\n✗ Timeout: No response received within 5 seconds")
        print("  Note: Server might be using PULL socket (one-way)")
    except Exception as e:
        print(f"\n✗ Error: {e}")
    finally:
        socket.close()
        context.term()

def send_url_alert_with_response(port=50001, host="localhost"):
    """Send a UrlAlert and wait for response"""

    deviceUid = "PC-JOHN-001"
    device_owner = input("User 1: John or 2: Jane ")
    if device_owner == "2" :
        deviceUid = "PC-JANE-001"

    print(f"deviceUid = {deviceUid} device_owner = {device_owner}")

    subject_url = input("Type url ")
    if subject_url == "" :
        subject_url = "http://example.com"

    context = zmq.Context()
    socket = context.socket(zmq.REQ)
    socket.connect(f"tcp://{host}:{port}")
    socket.setsockopt(zmq.RCVTIMEO, 5000)
    
    print(f"Connected to tcp://{host}:{port}")

    # Create a sample UrlAlert matching the C# model
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
        "Trackers": [],  # Array of Key objects
        "IFrameDomains": ["ads.example.com", "tracker.example.com"],
        "UserAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"
    }
    
    # Convert to JSON and encode as UTF-8
    message_json = json.dumps(alert)
    message_bytes = message_json.encode('utf-8')
    
    print(f"\nSending UrlAlert:")
    print(f"  Device: {alert['DeviceInfo']['DeviceUid']}")
    print(f"  URL: {alert['Url']}")
    print(f"  Priority: {alert['Priority']}")
    
    try:
        # Send the message
        socket.send(message_bytes)
        print("\n✓ Alert sent, waiting for response...")
        
        # Wait for response
        response_bytes = socket.recv()
        response = response_bytes.decode('utf-8')
        
        print("\n✓ Response received:")
        print("-" * 50)
        
        # Try to parse as JSON for pretty printing
        try:
            response_json = json.loads(response)
            print(json.dumps(response_json, indent=2))
        except:
            print(response)
        
        print("-" * 50)
        
    except zmq.Again:
        print("\n✗ Timeout: No response received within 5 seconds")
        print("  Note: Server might be using PULL socket (one-way)")
    except Exception as e:
        print(f"\n✗ Error: {e}")
    finally:
        socket.close()
        context.term()

def send_one_way_alert(port=50001, host="localhost"):
    """Send alert using PUSH socket (one-way, no response expected)"""
    
    context = zmq.Context()
    socket = context.socket(zmq.PUSH)
    socket.connect(f"tcp://{host}:{port}")
    
    print(f"Connected to tcp://{host}:{port} (one-way mode)")
    
    alert = {
        "AlertType": "RemoteAccessAlert",
        "DeviceInfo": {
            "DeviceUid": "PC-12345",
            "DeviceType": 1,
            "OperatingSystem": 1,
            "MAC": "00:11:22:33:44:55"
        },
        "Timestamp": datetime.utcnow().isoformat() + "Z",
        "Priority": 2,
        "Token": "",
        "RemoteAccessApp": 1,
        "RunningProcesses": 5,
        "ConnectionUrl": "rdp://192.168.1.100:3389",
        "ConnectionStatus": 1,
        "ConnectionsCount": 1,
        "SessionStatus": 1
    }
    
    message_json = json.dumps(alert)
    message_bytes = message_json.encode('utf-8')
    
    print(f"\nSending alert (no response expected):")
    print(f"  Device: {alert['DeviceInfo']['DeviceUid']}")
    
    socket.send(message_bytes)
    print("✓ Alert sent (fire-and-forget)")
    
    time.sleep(0.1)
    socket.close()
    context.term()

if __name__ == "__main__":
    print("=" * 60)
    print("ASPSBackend Python Alert Client - With Response Handling")
    print("=" * 60)
    
    print("\nChoose mode:")
    print("1. PUSH/PULL mode (fire-and-forget, no response)")
    print("2. REQ/REP mode (waits for response)")
    print("3. REQ/REP mode (waits for response) UrlAlert")
    print("4. REQ/REP mode (waits for response) RemoteAccessAlert")
    print()

    timeout = 10000
    print(f"Timeout set to {timeout / 1000} seconds.")

    mode = input("Enter choice (1 - 4, default=1): ").strip() or "1"

    if mode == "1":
        print("\n" + "=" * 60)
        print("Using PUSH/PULL mode (one-way communication)")
        print("=" * 60)

        print("\n[1/2] Sending RemoteAccessAlert...")
        send_one_way_alert()

        time.sleep(1)

        print("\n[2/2] Sending another alert...")
        send_one_way_alert()

        print("\n" + "=" * 60)
        print("Done! Check ASPSBackend console for processing logs.")
        print("=" * 60)

    elif mode == "2":
        print("\n" + "=" * 60)
        print("Using REQ/REP mode (request-response)")
        print("=" * 60)

        print("\n[1/2] Sending RemoteAccessAlert...")
        send_remote_access_alert_with_response()

        time.sleep(1)

        print("\n[2/2] Sending UrlAlert...")
        send_url_alert_with_response()

        print("\n" + "=" * 60)
        print("Done!")
        print("=" * 60)

        print("\nNote: If you got timeouts, the server is using PULL socket")
        print("      which doesn't send responses. Use mode 2 instead.")

    elif mode == "3":
        print("\n" + "=" * 60)
        print("UrlAlert Using REQ/REP mode (request-response)")
        print("=" * 60)

        print("\nSending UrlAlert...")
        send_url_alert_with_response()

        print("\n" + "=" * 60)
        print("Done!")
        print("=" * 60)

        print("\nNote: If you got timeouts, the server is using PULL socket")
        print("      which doesn't send responses. Use mode 2 instead.")

    elif mode == "4":
        print("\n" + "=" * 60)
        print("RemoteAccessAlert Using REQ/REP mode (request-response)")
        print("=" * 60)

        print("\nSending RemoteAccessAlert...")
        send_remote_access_alert_with_response()

        print("\n" + "=" * 60)
        print("Done!")
        print("=" * 60)

        print("\nNote: If you got timeouts, the server is using PULL socket")
        print("      which doesn't send responses. Use mode 2 instead.")

    else:
        print("\n" + "=" * 60)
        print("Using REQ/REP mode (request-response)")
        print("=" * 60)

        print("\n[1/2] Sending RemoteAccessAlert...")
        send_remote_access_alert_with_response()

        time.sleep(1)

        print("\n[2/2] Sending UrlAlert...")
        send_url_alert_with_response()

        print("\n" + "=" * 60)
        print("Done!")
        print("=" * 60)

        print("\nNote: If you got timeouts, the server is using PULL socket")
        print("      which doesn't send responses. Use mode 2 instead.")

#!/usr/bin/env python3
"""
Example Python client for sending device alerts to ASPSBackend
Requires: pip install pyzmq
"""

import zmq
import json
import time
from datetime import datetime

def send_device_alert(port=50001, host="localhost"):
    """Send a RemoteAccessAlert to the ASPSBackend alert listener"""
    
    context = zmq.Context()
    socket = context.socket(zmq.PUSH)
    socket.connect(f"tcp://{host}:{port}")
    
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
    
    print(f"Sending alert: {message_json}")
    
    # Send the message
    socket.send(message_bytes)
    
    print("Alert sent successfully!")
    
    time.sleep(0.1)  # Give time for the message to be sent
    
    socket.close()
    context.term()

def send_url_alert(port=50001, host="localhost"):
    """Send a UrlAlert to the ASPSBackend alert listener"""
    
    context = zmq.Context()
    socket = context.socket(zmq.PUSH)
    socket.connect(f"tcp://{host}:{port}")
    
    print(f"Connected to tcp://{host}:{port}")
    
    # Create a sample UrlAlert matching the C# model
    alert = {
        "AlertType": "UrlAlert",
        "DeviceInfo": {
            "DeviceUid": "PC-12345",
            "DeviceType": 1,
            "OperatingSystem": 1,
            "MAC": "00:11:22:33:44:55"
        },
        "Timestamp": datetime.utcnow().isoformat() + "Z",
        "Priority": 1,
        "Token": "",
        "Url": "https://suspicious-site.com/malware",
        "Trackers": [],  # Array of Key objects
        "IFrameDomains": ["ads.example.com", "tracker.example.com"],
        "UserAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"
    }
    
    # Convert to JSON and encode as UTF-8
    message_json = json.dumps(alert)
    message_bytes = message_json.encode('utf-8')
    
    print(f"Sending alert: {message_json}")
    
    # Send the message
    socket.send(message_bytes)
    
    print("Alert sent successfully!")
    
    time.sleep(0.1)
    
    socket.close()
    context.term()

if __name__ == "__main__":
    print("ASPSBackend Python Alert Client")
    print("=" * 50)
    
    # Send a RemoteAccessAlert
    print("\n1. Sending RemoteAccessAlert...")
    send_device_alert()
    
    time.sleep(1)
    
    # Send a UrlAlert
    print("\n2. Sending UrlAlert...")
    send_url_alert()
    
    print("\n" + "=" * 50)
    print("All alerts sent! Check the ASPSBackend console for processing logs.")

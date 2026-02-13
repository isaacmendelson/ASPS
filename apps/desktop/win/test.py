#!/usr/bin/env python3
"""
ASPSBackend Python Client (REQ + PUB/SUB notifications)
Requires: pip install pyzmq
"""

import os
import json
import threading
import time
from datetime import datetime, timezone

import zmq

# =========================================================
# CONFIG (ערוך כאן בלבד)
# =========================================================
HOST = "100.88.78.75"   # השרת שלך (לא localhost)
REQ_PORT = 50001        # REQ/REP (שליחת Alert)
PUB_PORT = 50002        # PUB/SUB (קבלת Notifications)


def utc_ts_z() -> str:
    """UTC timestamp in ISO8601 with trailing Z (Py3.13 safe)."""
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


# =========================================================
# Notification Listener
# =========================================================
class NotificationListener:
    """Listens for notifications from ASPSBackend on a background thread."""

    def __init__(self, device_uid: str, port: int = PUB_PORT, host: str = HOST):
        self.device_uid = device_uid
        self.port = port
        self.host = host
        self.running = False
        self.thread = None
        self.socket = None
        self.context = None

    def start(self):
        self.running = True
        self.thread = threading.Thread(target=self._listen, daemon=True)
        self.thread.start()
        print(f"\n📡 Subscribed to notifications for device: {self.device_uid}")
        print(f"   Listening on tcp://{self.host}:{self.port}")
        print("=" * 70)

    def _listen(self):
        self.context = zmq.Context.instance()
        self.socket = self.context.socket(zmq.SUB)
        self.socket.connect(f"tcp://{self.host}:{self.port}")

        # כדי שנוכל לעצור נקי וש-zmq.Again באמת יעבוד
        self.socket.setsockopt(zmq.RCVTIMEO, 1000)  # 1s
        self.socket.setsockopt(zmq.LINGER, 0)

        topic = f"device:{self.device_uid}"
        self.socket.setsockopt_string(zmq.SUBSCRIBE, topic)

        print(f"🎧 Listening for notifications on topic: '{topic}'...\n")

        while self.running:
            try:
                topic_bytes, message_bytes = self.socket.recv_multipart()
                topic_str = topic_bytes.decode("utf-8", errors="replace")
                message_str = message_bytes.decode("utf-8", errors="replace")
                self._handle_notification(topic_str, message_str)
            except zmq.Again:
                continue
            except Exception as e:
                if self.running:
                    print(f"\n❌ Error receiving notification: {e}")
                break

    def _handle_notification(self, topic: str, message_json: str):
        try:
            notification = json.loads(message_json)

            print("\n" + "=" * 70)
            print("🔔 NOTIFICATION RECEIVED")
            print("=" * 70)
            print(f"📌 Topic: {topic}")
            print(f"⏰ Timestamp: {notification.get('Timestamp', 'N/A')}")
            print(f"🖥️  Device: {notification.get('DeviceUid', 'N/A')}")

            data = notification.get("Data", {})
            print("\n📊 Analysis Result:")
            print(f"   Alert Type: {data.get('AlertType', 'N/A')}")
            print(f"   Severity: {data.get('Severity', 'N/A')}")
            print(f"   Message: {data.get('Message', 'N/A')}")
            print(f"   Analysis Result: {data.get('AnalysisResult', 'N/A')}")
            print(f"   RiskAssessment: {data.get('RiskAssessment', 'N/A')}")
            print(f"   ProtectiveActions: {data.get('protectiveActions', 'N/A')}")
            print(f"   Indicators: {data.get('Indicators', 'N/A')}")

            analyzer_results = data.get("AnalyzerResults", {})
            if analyzer_results:
                print("\n🔍 Analyzer Results:")
                for analyzer_name, result in analyzer_results.items():
                    print(f"   • {analyzer_name}")
                    if isinstance(result, dict):
                        if "Item1" in result:
                            print(f"     Item1: {result['Item1']}")
                        if "Item2" in result:
                            print(f"     Item2: {result['Item2']}")
                        if "Item3" in result:
                            print(f"     Item3: {result['Item3']}")
                        if "Url" in result:
                            print(f"     URL: {result['Url']}")
                        if "Domain" in result:
                            print(f"     Domain: {result['Domain']}")

                        if "risk_assessment" in result and isinstance(result["risk_assessment"], dict):
                            risk = result["risk_assessment"]
                            print(f"     Risk Score: {risk.get('risk_score', 'N/A')}")
                            print(f"     Is Scam: {risk.get('is_scam', 'N/A')}")

                        if "RiskAssessment" in result and isinstance(result["RiskAssessment"], dict):
                            risk = result["RiskAssessment"]
                            print(f"     Risk Score: {risk.get('risk_score', 'N/A')}")
                            print(f"     Is Scam: {risk.get('is_scam', 'N/A')}")

            details = data.get("Details", {})
            if details:
                print("\n📝 Additional Details:")
                for key, value in details.items():
                    if not isinstance(value, (dict, list)):
                        print(f"   {key}: {value}")

            print("=" * 70)
            print("🎧 Listening for more notifications... (Ctrl+C to exit)\n")

        except json.JSONDecodeError:
            print(f"\n⚠️  Received non-JSON notification: {message_json[:200]}")
        except Exception as e:
            print(f"\n❌ Error parsing notification: {e}")

    def stop(self):
        self.running = False
        if self.thread:
            self.thread.join(timeout=2)
        if self.socket:
            try:
                self.socket.close(0)
            except Exception:
                pass
        print("\n📡 Stopped listening for notifications")


# =========================================================
# REQ socket helpers
# =========================================================
def create_req_socket(host: str, port: int):
    ctx = zmq.Context.instance()
    s = ctx.socket(zmq.REQ)
    s.connect(f"tcp://{host}:{port}")
    s.setsockopt(zmq.RCVTIMEO, 5000)
    s.setsockopt(zmq.LINGER, 0)
    return s


def send_alert_and_wait(socket, alert: dict):
    socket.send(json.dumps(alert).encode("utf-8"))
    print("\n✅ Alert sent to backend!")
    print("⏳ Waiting for immediate response...")

    try:
        resp = socket.recv().decode("utf-8", errors="replace")
        print("\n📨 Immediate Response:")
        print("-" * 70)
        try:
            print(json.dumps(json.loads(resp), indent=2))
        except Exception:
            print(resp)
        print("-" * 70)
    except zmq.Again:
        print("\n⚠️  No immediate response (server might be processing async)")


# =========================================================
# UI helpers
# =========================================================
def pick_device_uid() -> str:
    device_uid = "PC-JOHN-001"
    who = input("User 1: John or 2: Jane (default=1): ").strip() or "1"
    if who == "2":
        device_uid = "PC-JANE-001"
    print(f"\n👤 Selected: {device_uid}")
    return device_uid


# =========================================================
# Alerts
# =========================================================
def send_url_alert_with_notifications():
    print(f"\n👤 host: {HOST}")

    device_uid = pick_device_uid()
    url = input("Enter URL to analyze (default=http://example.com): ").strip() or "http://example.com"
    print(f"🔗 URL: {url}\n")

    listener = NotificationListener(device_uid)
    listener.start()
    time.sleep(1.0)  # חשוב ל-PUB/SUB

    socket = create_req_socket(HOST, REQ_PORT)

    print("=" * 70)
    print("📤 SENDING URL ALERT")
    print("=" * 70)

    alert = {
        "AlertType": "UrlAlert",
        "DeviceInfo": {
            "DeviceUid": device_uid,
            "DeviceType": 1,
            "OperatingSystem": 1,
            "MAC": "00:11:22:33:44:55",
        },
        "Timestamp": utc_ts_z(),
        "Priority": 1,
        "Token": "",
        "Url": url,
        "Trackers": [],
        "IFrameDomains": [],
        "UserAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
    }

    try:
        send_alert_and_wait(socket, alert)

        print("\n" + "=" * 70)
        print("🎧 Now listening for analysis notifications...")
        print("   (Analysis may take a few seconds)")
        print("   Press Ctrl+C to exit")
        print("=" * 70 + "\n")

        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        print("\n\n⚠️  Ctrl+C detected")
    finally:
        listener.stop()
        socket.close()


def send_remoteaccess_alert_with_notifications():
    print(f"\n👤 host: {HOST}")

    device_uid = pick_device_uid()

    remote_access_app = (
        input(
            "RemoteAccess app 1 AnyDesk, 2 TeamViewer, 3 ChromeRemoteDesktop, 4 RemotePC, "
            "5 LogMeIn, 6 Splashtop, 7 VNC (default=1): "
        ).strip()
        or "1"
    )
    running_processes = 2
    connection_url = "192.198.60.101"
    connection_status = input("connection_status 0 Unknown, 1 Open, 2 Closed (default=1): ").strip() or "1"
    session_status = input("Session status 0 Unknown, 1 Open, 2 Closed (default=1): ").strip() or "1"

    listener = NotificationListener(device_uid)
    listener.start()
    time.sleep(1.0)

    socket = create_req_socket(HOST, REQ_PORT)

    print("=" * 70)
    print("📤 SENDING REMOTE_ACCESS ALERT")
    print("=" * 70)

    alert = {
        "AlertType": "RemoteAccessAlert",
        "DeviceInfo": {
            "DeviceUid": device_uid,
            "DeviceType": 1,
            "OperatingSystem": 1,
            "MAC": "00:11:22:33:44:55",
        },
        "Timestamp": utc_ts_z(),
        "Priority": 1,
        "Token": "",
        "RemoteAccessApp": remote_access_app,
        "RunningProcesses": running_processes,
        "ConnectionUrl": connection_url,
        "ConnectionStatus": connection_status,
        "SessionStatus": session_status,
    }

    try:
        send_alert_and_wait(socket, alert)

        print("\n" + "=" * 70)
        print("🎧 Now listening for analysis notifications...")
        print("   (Analysis may take a few seconds)")
        print("   Press Ctrl+C to exit")
        print("=" * 70 + "\n")

        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        print("\n\n⚠️  Ctrl+C detected")
    finally:
        listener.stop()
        socket.close()


# =========================================================
# MAIN
# =========================================================
def main():
    # עוזר לוודא שאתה מריץ את הקובץ הנכון בפייתצ'ארם
    print("RUNNING FILE:", os.path.abspath(__file__))
    print(f"HOST={HOST}, REQ_PORT={REQ_PORT}, PUB_PORT={PUB_PORT}")

    print("\n📋 Choose mode:")
    print("1. URL Alert + notifications")
    print("2. RemoteAccess Alert + notifications")
    print("3. Exit")

    choice = input("\nChoice (1-3, default=1): ").strip() or "1"

    if choice == "1":
        send_url_alert_with_notifications()
    elif choice == "2":
        send_remoteaccess_alert_with_notifications()
    else:
        print("👋 Bye")


if __name__ == "__main__":
    main()

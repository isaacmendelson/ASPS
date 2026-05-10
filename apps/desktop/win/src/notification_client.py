"""
AntiScam Desktop App - Notification Client
ZMQ SUB client for receiving real-time notifications from backend
"""

import zmq
import json
import logging
import threading
import time
import sys
from typing import Optional, Callable, Dict, Any

# Fix Windows console encoding - prevents crash on emoji/unicode from server
if sys.stdout and hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
if sys.stderr and hasattr(sys.stderr, 'reconfigure'):
    sys.stderr.reconfigure(encoding='utf-8', errors='replace')

logger = logging.getLogger(__name__)


class NotificationClient:
    """
    ZeroMQ Notification Client using SUB pattern.

    Subscribes to notifications for a specific device.

    Notification Format (from backend):
    Topic: "device:{DeviceUid}"
    Message:
    {
        "Timestamp": "2024-01-30T12:00:00Z",
        "DeviceUid": "PC-JOHN-001",
        "Data": {
            "AlertType": "UrlAnalysisComplete",
            "Severity": "Medium",
            "Message": "URL analysis completed",
            "RiskAssessment": {...},
            "ProtectiveActions": [...],
            "AnalyzerResults": {...},
            "Details": {...}
        }
    }
    """

    def __init__(self, device_uid: str, host: str = "localhost", port: int = 50002):
        self.device_uid = device_uid
        self.host = host
        self.port = port
        self.running = False
        self.thread = None
        self.context = None
        self.socket = None
        self.server_public_key: Optional[bytes] = None  # Z85-encoded server key

        # Callbacks
        self._on_notification_callback: Optional[Callable] = None
        self._on_connected_callback: Optional[Callable] = None
        self._on_disconnected_callback: Optional[Callable] = None

        print(f"[NOTIFY] Client initialized")
        print(f"[NOTIFY] Server: tcp://{self.host}:{self.port}")
        print(f"[NOTIFY] Device: {self.device_uid}")

    def set_server_public_key(self, key: bytes):
        """Set the CurveZMQ server public key for encrypted communication."""
        self.server_public_key = key
        print(f"[NOTIFY] CURVE server public key set ({len(key)} bytes)")

    def on_notification(self, callback: Callable[[Dict[str, Any]], None]):
        """Set callback for notifications"""
        self._on_notification_callback = callback

    def on_connected(self, callback: Callable[[], None]):
        """Set callback for connection established"""
        self._on_connected_callback = callback

    def on_disconnected(self, callback: Callable[[], None]):
        """Set callback for disconnection"""
        self._on_disconnected_callback = callback

    def start(self):
        """Start listening for notifications in a background thread"""
        if self.running:
            print("[NOTIFY] WARNING: Already running")
            return

        self.running = True
        self.thread = threading.Thread(target=self._listen, daemon=True)
        self.thread.start()

        print(f"\n[NOTIFY] Started listening")
        print("=" * 70)

    def _listen(self):
        """Background thread that listens for notifications"""
        try:
            self.context = zmq.Context()
            self.socket = self.context.socket(zmq.SUB)

            # Set timeout for recv so we can check running flag periodically
            self.socket.setsockopt(zmq.RCVTIMEO, 5000)  # 5 second timeout

            # Apply CURVE encryption if server key is available
            if self.server_public_key:
                client_public, client_secret = zmq.curve_keypair()
                self.socket.setsockopt(zmq.CURVE_PUBLICKEY, client_public)
                self.socket.setsockopt(zmq.CURVE_SECRETKEY, client_secret)
                self.socket.setsockopt(zmq.CURVE_SERVERKEY, self.server_public_key)
                print(f"[NOTIFY] CURVE encryption enabled for PUB/SUB (port {self.port})")
            else:
                print(f"[NOTIFY] No CURVE encryption for PUB/SUB (port {self.port})")

            self.socket.connect(f"tcp://{self.host}:{self.port}")

            # Subscribe to notifications for this device
            topic = f"device:{self.device_uid}"
            self.socket.subscribe(topic.encode('utf-8'))

            print(f"[NOTIFY] Subscribed to topic: '{topic}'")
            print("=" * 70)

            if self._on_connected_callback:
                self._on_connected_callback()

            heartbeat_counter = 0  # Print heartbeat every ~2 minutes (24 * 5s)

            while self.running:
                try:
                    # Receive multipart message: [topic, message]
                    topic_bytes = self.socket.recv()
                    message_bytes = self.socket.recv()

                    topic_str = topic_bytes.decode('utf-8')
                    message_str = message_bytes.decode('utf-8')

                    # Parse and handle notification
                    self._handle_notification(topic_str, message_str)

                    # Reset heartbeat counter when we receive messages
                    heartbeat_counter = 0

                except zmq.Again:
                    # Timeout - this is normal, it means no messages received
                    heartbeat_counter += 1

                    # Print heartbeat every ~2 minutes to show we're still listening
                    if heartbeat_counter >= 24:  # 24 * 5s = 120s = 2 min
                        print(f"[NOTIFY] HEARTBEAT: Still listening for notifications... (topic: {topic})" + time.strftime("%Y-%m-%d %H:%M:%S"))
                        heartbeat_counter = 0

                    continue
                except Exception as e:
                    if self.running:
                        print(f"\n[NOTIFY] ERROR: Error receiving notification: {e}")
                        logger.error(f"Notification receive error: {e}")
                    break

        except Exception as e:
            print(f"[NOTIFY] ERROR: Connection error: {e}")
            logger.error(f"Notification connection error: {e}")
        finally:
            self._cleanup()

    def _handle_notification(self, topic: str, message_json: str):
        """Handle received notification"""
        try:
            notification = json.loads(message_json)

            print("\n" + "=" * 70)
            print("NOTIFICATION NOTIFICATION RECEIVED " + time.strftime("%Y-%m-%d %H:%M:%S"))
            print("=" * 70)
            print(f"Topic: Topic: {topic}")
            print(f"Timestamp: Timestamp: {notification.get('Timestamp', 'N/A')}")
            print(f"Device:  Device: {notification.get('DeviceUid', 'N/A')}")

            # Backend wraps data in 'Data' object
            data = notification.get('Data', {})

            print(f"\nDATA: Alert Information:")
            print(f"   Alert Type: {data.get('AlertType', 'N/A')}")
            print(f"   Severity: {data.get('Severity', 'N/A')}")
            print(f"   Message: {data.get('Message', 'N/A')}")

            # Display Analysis Result (full object from backend)
            analysis_result = data.get('AnalysisResult')
            if analysis_result and isinstance(analysis_result, dict):
                print(f"\nAnalysis Result: Analysis Result:")
                print(f"   Type: {analysis_result.get('TypeName', 'N/A')}")
                print(f"   URL: {analysis_result.get('Url', 'N/A')}")
                print(f"   Domain: {analysis_result.get('Domain', 'N/A')}")
                print(f"   Analysis Time: {analysis_result.get('analysis_time_ms', 'N/A')}ms")
                print(f"   From Cache: {analysis_result.get('IsFromCache', 'N/A')}")

                # Risk assessment from AnalysisResult
                risk = analysis_result.get('risk_assessment', {})
                if risk:
                    print(f"\n   DATA: Risk Assessment:")
                    print(f"      Risk Score: {risk.get('risk_score', 'N/A')}")
                    print(f"      Risk Level: {risk.get('risk_level', 'N/A')}")
                    print(f"      Is Scam: {risk.get('is_scam', 'N/A')}")
                    print(f"      Confidence: {risk.get('confidence', 'N/A')}")

                # Recommendation
                if 'Recommendation' in analysis_result:
                    print(f"\n   Recommendation: Recommendation: {analysis_result['Recommendation']}")

            # Display Risk Assessment (at Data level - if present separately)
            risk_assessment = data.get('RiskAssessment', {})
            if risk_assessment and not analysis_result:
                print(f"\nWARNING:  Risk Assessment:")
                print(f"   Risk Score: {risk_assessment.get('risk_score', 'N/A')}")
                print(f"   Risk Level: {risk_assessment.get('risk_level', 'N/A')}")
                print(f"   Is Scam: {risk_assessment.get('is_scam', 'N/A')}")
                print(f"   Confidence: {risk_assessment.get('confidence', 'N/A')}")

            # Display Indicators
            indicators = data.get('Indicators', [])
            if indicators:
                print(f"\nIndicators: Indicators ({len(indicators)}):")
                for idx, indicator in enumerate(indicators, 1):
                    if isinstance(indicator, dict):
                        ind_type = indicator.get('IndicatorType', indicator.get('$type', 'N/A'))
                        ind_value = indicator.get('Value', 'N/A')
                        ind_level = indicator.get('Level', 'N/A')
                        ind_confidence = indicator.get('Confidence', 'N/A')
                        print(f"   {idx}. Type: {ind_type}")
                        if ind_value != 'N/A':
                            print(f"      Value: {ind_value}")
                        if ind_level != 'N/A':
                            print(f"      Level: {ind_level}")
                        if ind_confidence != 'N/A':
                            print(f"      Confidence: {ind_confidence}")
                    else:
                        print(f"   {idx}. {indicator}")

            # Display Protective Actions
            protective_actions = data.get('ProtectiveActions', [])
            if protective_actions:
                print(f"\nProtective Actions:  Protective Actions ({len(protective_actions)}):")
                for action in protective_actions:
                    action_type = action.get('ActionType', 'N/A')
                    message = action.get('Message', 'N/A')
                    level = action.get('Level', 'N/A')
                    print(f"   • Type: {action_type}, Level: {level}, Message: {message}")

            # Display analyzer results
            analyzer_results = data.get('AnalyzerResults', {})
            if analyzer_results:
                print(f"\nAnalyzer Results: Analyzer Results:")

                # URL and Domain
                if 'Url' in analyzer_results:
                    print(f"   URL: {analyzer_results['Url']}")
                if 'Domain' in analyzer_results:
                    print(f"   Domain: {analyzer_results['Domain']}")

                # Risk Assessment (might also be in AnalyzerResults)
                risk = analyzer_results.get('risk_assessment') or analyzer_results.get('RiskAssessment')
                if risk:
                    print(f"\n   Risk Details:")
                    print(f"   Risk Score: {risk.get('risk_score', 'N/A')}")
                    print(f"   Risk Level: {risk.get('risk_level', 'N/A')}")
                    print(f"   Is Scam: {risk.get('is_scam', 'N/A')}")
                    print(f"   Confidence: {risk.get('confidence', 'N/A')}")

                # Phishing Check
                phishing = analyzer_results.get('phishing_check')
                if phishing:
                    print(f"\n   Phishing Check: Phishing Check:")
                    print(f"   Is Known Phishing: {phishing.get('Is_known_phishing', 'N/A')}")
                    print(f"   Source: {phishing.get('Source', 'N/A')}")

                # Recommendation
                if 'Recommendation' in analyzer_results:
                    print(f"\nRecommendation: Recommendation:")
                    print(f"   {analyzer_results['Recommendation']}")

            print("=" * 70)

            # Call user callback
            if self._on_notification_callback:
                self._on_notification_callback(notification)

        except json.JSONDecodeError as e:
            print(f"\n[NOTIFY] WARNING: Non-JSON notification: {message_json[:100]}")
            logger.warning(f"Non-JSON notification: {e}")
        except Exception as e:
            print(f"\n[NOTIFY] ERROR: Error parsing notification: {e}")
            logger.error(f"Notification parse error: {e}")
            # Ensure callback is called even if print/parsing fails
            try:
                if self._on_notification_callback and notification:
                    self._on_notification_callback(notification)
            except Exception:
                pass

    def stop(self):
        """Stop listening for notifications"""
        print("\n[NOTIFY] Stopping...")
        self.running = False

        if self.thread and self.thread.is_alive():
            self.thread.join(timeout=2)

        self._cleanup()
        print("[NOTIFY] SUCCESS: Stopped")

    def _cleanup(self):
        """Clean up resources"""
        if self.socket:
            try:
                self.socket.close()
            except zmq.ZMQError as e:
                logger.debug("Error closing notification socket: %s", e)
            except Exception:
                logger.exception("Unexpected error closing notification socket")
            self.socket = None

        if self.context:
            try:
                self.context.term()
            except zmq.ZMQError as e:
                logger.debug("Error terminating notification context: %s", e)
            except Exception:
                logger.exception("Unexpected error terminating notification context")
            self.context = None

        if self._on_disconnected_callback:
            self._on_disconnected_callback()

    @property
    def is_running(self) -> bool:
        return self.running

    def wait_for_notifications(self):
        """Block and wait for notifications (for standalone use)"""
        print("\n[NOTIFY] Listening Listening for notifications...")
        print("[NOTIFY] Press Ctrl+C to stop\n")

        try:
            while self.running:
                time.sleep(1)
        except KeyboardInterrupt:
            print("\n[NOTIFY] WARNING: Interrupted")
            self.stop()


# Standalone test
if __name__ == "__main__":
    import sys

    logging.basicConfig(level=logging.INFO)

    print("=" * 70)
    print("NOTIFICATION CLIENT - STANDALONE TEST")
    print("=" * 70)

    # Get server from command line or use default
    host = sys.argv[1] if len(sys.argv) > 1 else "localhost"
    device_uid = sys.argv[2] if len(sys.argv) > 2 else "PC-TEST-001"

    def on_notification(data):
        print(f"\n*** CALLBACK: Notification received! ***\n")

    def on_connected():
        print("\n*** CALLBACK: Connected! ***\n")

    def on_disconnected():
        print("\n*** CALLBACK: Disconnected! ***\n")

    client = NotificationClient(device_uid, host, 50002)
    client.on_notification(on_notification)
    client.on_connected(on_connected)
    client.on_disconnected(on_disconnected)

    client.start()

    print("\nListening Listening for notifications...")
    print(f"   To test, send an alert for device '{device_uid}' using zmq_client.py")

    client.wait_for_notifications()

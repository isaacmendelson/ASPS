"""
CURVE Diagnostic Tool
Troubleshooting utility for ASPS Backend CURVE/ZMQ communication

Usage:
    python curve_diagnostic.py                    # Run all tests with defaults
    python curve_diagnostic.py --host 192.168.1.1 # Test against specific host
    python curve_diagnostic.py --device PC-JOHN-001 --full  # Full test with registered device
"""

import argparse
import base64
import json
import os
import sys
from datetime import datetime
from pathlib import Path

try:
    import zmq
    from zmq.utils import z85
except ImportError:
    print("ERROR: pyzmq not installed. Run: pip install pyzmq")
    sys.exit(1)


# Try to import from config, fall back to hardcoded values
try:
    from config import (
        BACKEND_HOST as DEFAULT_HOST,
        BACKEND_REQ_PORT as DEFAULT_PORT,
        BACKEND_SERVER_PUBLIC_KEY_Z85 as SERVER_PUBLIC_KEY_Z85
    )
except ImportError:
    DEFAULT_HOST = "127.0.0.1"
    DEFAULT_PORT = 50001
    SERVER_PUBLIC_KEY_Z85 = "qPsk#8DY:n9ovp[vQ!YcOnOX[f/.i@.g^f#b:!ik"

DEFAULT_DEVICE_UID = "DIAGNOSTIC-TEST-001"

# Base64 key for Z85 verification (from backend appsettings.json)
SERVER_PUBLIC_KEY_BASE64 = "UsaAZRpYTlYc5QPyYmLsr5xvCOMxOThZwYV56CSY9A0="


def get_auth_json_path():
    """Get path to auth.json"""
    if sys.platform == 'win32':
        data_dir = os.path.join(os.environ.get('APPDATA', os.path.expanduser('~')), 'AntiScam')
    else:
        data_dir = os.path.expanduser("~/.antiscam")
    return Path(data_dir) / "auth.json"


def load_auth_data():
    """Load token and server key from auth.json if available"""
    auth_path = get_auth_json_path()
    if auth_path.exists():
        try:
            with open(auth_path, 'r') as f:
                return json.load(f)
        except Exception as e:
            print(f"  Warning: Could not load auth.json: {e}")
    return None


def print_header(title):
    """Print a section header"""
    print(f"\n{'=' * 60}")
    print(f" {title}")
    print('=' * 60)


def print_result(success, message):
    """Print a test result"""
    symbol = "[OK]" if success else "[FAIL]"
    print(f"  {symbol} {message}")


def test_z85_encoding():
    """Test Z85 encoding compatibility"""
    print_header("Z85 ENCODING TEST")

    print(f"\n  Server Public Key (Base64): {SERVER_PUBLIC_KEY_BASE64}")
    print(f"  Server Public Key (Z85):    {SERVER_PUBLIC_KEY_Z85}")

    try:
        # Decode base64
        key_bytes = base64.b64decode(SERVER_PUBLIC_KEY_BASE64)
        print(f"\n  Base64 decoded: {len(key_bytes)} bytes")
        print(f"  Hex: {key_bytes.hex()}")

        # Encode to Z85 using pyzmq
        pyzmq_z85 = z85.encode(key_bytes).decode('utf-8')
        print(f"\n  pyzmq Z85 result: {pyzmq_z85}")
        print(f"  Config Z85 value: {SERVER_PUBLIC_KEY_Z85}")

        if pyzmq_z85 == SERVER_PUBLIC_KEY_Z85:
            print_result(True, "Z85 encodings MATCH - keys are compatible")
            return True
        else:
            print_result(False, "Z85 encodings DO NOT MATCH!")
            print("\n  Character differences:")
            for i, (a, b) in enumerate(zip(pyzmq_z85, SERVER_PUBLIC_KEY_Z85)):
                if a != b:
                    print(f"    Position {i}: pyzmq='{a}', config='{b}'")
            return False

    except Exception as e:
        print_result(False, f"Z85 encoding test failed: {e}")
        return False


def test_connection(host, port, use_curve, server_key_z85, device_uid, timeout_ms=5000):
    """Test ZMQ connection to backend"""
    context = zmq.Context()
    socket = context.socket(zmq.REQ)
    socket.setsockopt(zmq.RCVTIMEO, timeout_ms)
    socket.setsockopt(zmq.SNDTIMEO, timeout_ms)
    socket.setsockopt(zmq.LINGER, 0)

    result = {"success": False, "response": None, "error": None}

    try:
        if use_curve and server_key_z85:
            client_public, client_secret = zmq.curve_keypair()
            socket.setsockopt(zmq.CURVE_PUBLICKEY, client_public)
            socket.setsockopt(zmq.CURVE_SECRETKEY, client_secret)
            socket.setsockopt(zmq.CURVE_SERVERKEY, server_key_z85.encode('utf-8'))
            print(f"  CURVE: Enabled")
            print(f"  Server key: {server_key_z85[:30]}...")
        else:
            print(f"  CURVE: Disabled")

        socket.connect(f"tcp://{host}:{port}")
        print(f"  Connected to tcp://{host}:{port}")

        message = {"MessageType": "RequestToken", "DeviceUid": device_uid}
        print(f"  Sending RequestToken for device: {device_uid}")

        socket.send(json.dumps(message).encode('utf-8'))
        response = socket.recv()
        result["response"] = json.loads(response.decode('utf-8'))
        result["success"] = True

    except zmq.Again:
        result["error"] = "TIMEOUT - No response (check if backend is running and not paused at breakpoint)"
    except zmq.ZMQError as e:
        result["error"] = f"ZMQ Error: {e}"
    except Exception as e:
        result["error"] = f"Error: {e}"
    finally:
        socket.close()
        context.term()

    return result


def test_alert_with_token(host, port, server_key_z85, device_uid, token, timeout_ms=5000):
    """Test sending an alert with token authentication"""
    context = zmq.Context()
    socket = context.socket(zmq.REQ)
    socket.setsockopt(zmq.RCVTIMEO, timeout_ms)
    socket.setsockopt(zmq.SNDTIMEO, timeout_ms)
    socket.setsockopt(zmq.LINGER, 0)

    result = {"success": False, "response": None, "error": None}

    try:
        # Configure CURVE
        client_public, client_secret = zmq.curve_keypair()
        socket.setsockopt(zmq.CURVE_PUBLICKEY, client_public)
        socket.setsockopt(zmq.CURVE_SECRETKEY, client_secret)
        socket.setsockopt(zmq.CURVE_SERVERKEY, server_key_z85.encode('utf-8'))

        socket.connect(f"tcp://{host}:{port}")

        # Send URL alert
        alert = {
            "AlertType": "UrlAlert",
            "DeviceInfo": {
                "DeviceUid": device_uid,
                "DeviceType": 1,
                "OperatingSystem": 1,
                "MAC": "00:00:00:00:00:00"
            },
            "Timestamp": datetime.utcnow().isoformat() + "Z",
            "Priority": 1,
            "Token": token,
            "Url": "https://diagnostic-test.example.com",
            "Trackers": [],
            "IFrameDomains": [],
            "UserAgent": "DiagnosticTool/1.0"
        }

        print(f"  Sending UrlAlert with token: {token[:20]}...")
        socket.send(json.dumps(alert).encode('utf-8'))
        response = socket.recv()
        result["response"] = json.loads(response.decode('utf-8'))
        result["success"] = True

    except zmq.Again:
        result["error"] = "TIMEOUT"
    except Exception as e:
        result["error"] = str(e)
    finally:
        socket.close()
        context.term()

    return result


def run_diagnostics(host, port, device_uid, full_test=False):
    """Run all diagnostic tests"""
    print(f"\n  Timestamp: {datetime.now().isoformat()}")
    print(f"  Host: {host}:{port}")
    print(f"  Device: {device_uid}")
    print(f"  pyzmq version: {zmq.__version__}")

    # Test 1: Z85 encoding
    z85_ok = test_z85_encoding()
    if not z85_ok:
        print("\n  WARNING: Z85 encoding mismatch may cause CURVE failures")

    # Test 2: Connection WITHOUT CURVE
    print_header("CONNECTION TEST (No CURVE)")
    result = test_connection(host, port, use_curve=False, server_key_z85=None, device_uid=device_uid)
    if result["success"]:
        print_result(True, f"Response: {json.dumps(result['response'])}")
        print("\n  Note: If CURVE is enabled on server, this should timeout or fail")
    else:
        print_result(False, result["error"])
        print("\n  Note: This is expected if CURVE is enabled on the backend")

    # Test 3: Connection WITH CURVE
    print_header("CONNECTION TEST (With CURVE)")
    result = test_connection(host, port, use_curve=True, server_key_z85=SERVER_PUBLIC_KEY_Z85, device_uid=device_uid)
    if result["success"]:
        print_result(True, f"Response: {json.dumps(result['response'], indent=2)}")
        response = result["response"]

        # Check if we got a token
        if response.get("status") in ("TokenCreated", "ExistingToken", "TokenRefreshed"):
            token = response.get("token")
            server_key = response.get("serverPublicKey")
            print(f"\n  Token received: {token[:20]}...")
            print(f"  Server key in response: {server_key}")

            # Test 4: Alert with token (if full test requested)
            if full_test and token:
                print_header("ALERT TEST (With Token)")
                alert_result = test_alert_with_token(host, port, SERVER_PUBLIC_KEY_Z85, device_uid, token)
                if alert_result["success"]:
                    print_result(True, f"Alert accepted: {json.dumps(alert_result['response'], indent=2)}")
                else:
                    print_result(False, alert_result["error"])
        else:
            print(f"\n  Status: {response.get('status')}")
            if response.get("status") == "DeviceNotRecognized":
                print("  Note: Device not registered. Register via WebApi to get a token.")
    else:
        print_result(False, result["error"])

    # Check auth.json
    print_header("LOCAL AUTH STATE")
    auth_path = get_auth_json_path()
    print(f"  Auth file: {auth_path}")

    auth_data = load_auth_data()
    if auth_data:
        print(f"  Token: {auth_data.get('token', 'N/A')[:20] if auth_data.get('token') else 'N/A'}...")
        print(f"  Server key: {auth_data.get('server_public_key', 'N/A')}")
        print(f"  Expires: {auth_data.get('expires_at', 'N/A')}")
        print(f"  Authorized: {auth_data.get('is_authorized', False)}")
    else:
        print("  No auth.json found (device not yet authenticated)")

    # Summary
    print_header("SUMMARY")
    print(f"  Z85 Encoding: {'OK' if z85_ok else 'MISMATCH'}")
    print(f"  CURVE Communication: {'OK' if result['success'] else 'FAILED'}")
    if result["success"]:
        print("\n  Secure communication is working correctly!")
    else:
        print("\n  Troubleshooting tips:")
        print("  - Ensure backend (ASPSBackend) is running")
        print("  - Check that no breakpoints are pausing the backend")
        print("  - Verify Security:CurveEnabled in appsettings.json")
        print("  - Verify the Z85 keys match between client and server")


def main():
    parser = argparse.ArgumentParser(
        description="ASPS Backend CURVE/ZMQ Diagnostic Tool",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python curve_diagnostic.py                          # Basic test with defaults
  python curve_diagnostic.py --host 192.168.1.100    # Test remote server
  python curve_diagnostic.py --device PC-JOHN-001 --full  # Full test with registered device
        """
    )
    parser.add_argument("--host", default=DEFAULT_HOST, help=f"Backend host (default: {DEFAULT_HOST})")
    parser.add_argument("--port", type=int, default=DEFAULT_PORT, help=f"Backend port (default: {DEFAULT_PORT})")
    parser.add_argument("--device", default=DEFAULT_DEVICE_UID, help=f"Device UID to test (default: {DEFAULT_DEVICE_UID})")
    parser.add_argument("--full", action="store_true", help="Run full test including alert submission")

    args = parser.parse_args()

    print("=" * 60)
    print(" ASPS CURVE/ZMQ DIAGNOSTIC TOOL")
    print("=" * 60)

    run_diagnostics(args.host, args.port, args.device, args.full)


if __name__ == "__main__":
    main()

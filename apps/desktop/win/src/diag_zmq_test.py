"""
ASPS Phase 1 Diagnostic: ZMQ REQ/REP Round-Trip Test

Standalone script that:
1. Verifies Backend ports 50001, 50002, 5555, 5556 are listening
2. Sends a ZMQ REQ message (RequestToken) to port 50001
3. Logs timestamped SEND/RECV at the ZMQ boundary
4. Reports PASS/FAIL for each check

No dependencies beyond zmq, json, subprocess, datetime, sys, traceback.
"""

import zmq
import json
import subprocess
import sys
import traceback
from datetime import datetime, timezone


def diag_log(component: str, direction: str, message: str, data: dict = None):
    """Print ISO-8601 UTC timestamp with >>> for SEND and <<< for RECV."""
    ts = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%f")[:-3] + "Z"
    prefix = ">>>" if direction == "SEND" else "<<<"
    print(f"[{ts}] [{component}] {prefix} {message}")
    if data:
        indented = json.dumps(data, indent=2, default=str)
        for line in indented.splitlines():
            print(f"[{ts}] [{component}]     {line}")


def verify_ports() -> dict:
    """
    Check which of the 4 Backend ports (50001, 50002, 5555, 5556) are listening.

    Returns:
        Dict of {port: bool} results.
    """
    expected_ports = [50001, 50002, 5555, 5556]
    results = {p: False for p in expected_ports}

    print("\n--- Port Verification ---\n")

    try:
        cmd = (
            'powershell -Command "'
            "Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue "
            "| Where-Object {$_.LocalPort -in @(50001, 50002, 5555, 5556)} "
            "| Select-Object LocalAddress, LocalPort, OwningProcess "
            '| ConvertTo-Json"'
        )
        proc = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            timeout=15,
            shell=True,
        )

        if proc.returncode != 0:
            print(f"WARNING: PowerShell command returned exit code {proc.returncode}")
            if proc.stderr.strip():
                print(f"  stderr: {proc.stderr.strip()}")

        output = proc.stdout.strip()
        if not output:
            print("No listening ports found among 50001, 50002, 5555, 5556.")
        else:
            try:
                data = json.loads(output)
                # PowerShell returns a single object (not array) when only 1 result
                if isinstance(data, dict):
                    data = [data]
                for entry in data:
                    port = int(entry.get("LocalPort", 0))
                    if port in results:
                        results[port] = True
            except json.JSONDecodeError:
                print(f"WARNING: Could not parse PowerShell JSON output:\n{output}")

    except subprocess.TimeoutExpired:
        print("WARNING: PowerShell command timed out after 15s.")
    except FileNotFoundError:
        print("WARNING: PowerShell not found. Cannot verify ports.")
    except Exception as e:
        print(f"WARNING: Port verification error: {e}")

    # Print results
    for port in expected_ports:
        status = "PASS" if results[port] else "FAIL"
        label = "(listening)" if results[port] else "(NOT listening)"
        print(f"  Port {port}: {status} {label}")

    # Fatal check
    if not results[50001]:
        print(
            "\nFATAL: Port 50001 not listening. "
            "Start the Backend (ASPSBackend) first."
        )

    return results


def test_zmq_reqrep() -> bool:
    """
    Send a RequestToken message via ZMQ REQ to port 50001 and wait for a REP.

    Returns:
        True if a response was received, False otherwise.
    """
    print("\n--- ZMQ REQ/REP Round-Trip Test ---\n")

    ctx = zmq.Context()
    sock = ctx.socket(zmq.REQ)

    try:
        sock.setsockopt(zmq.RCVTIMEO, 5000)
        sock.setsockopt(zmq.SNDTIMEO, 5000)
        sock.setsockopt(zmq.LINGER, 0)
        sock.connect("tcp://127.0.0.1:50001")

        payload = {
            "MessageType": "RequestToken",
            "DeviceUid": "PC-DIAG-TEST-001",
        }

        diag_log("ZMQ-DIAG", "SEND", "RequestToken to port 50001", payload)
        sock.send_json(payload)

        try:
            response_bytes = sock.recv()
            response_text = response_bytes.decode("utf-8")

            try:
                response = json.loads(response_text)
            except json.JSONDecodeError:
                response = {"raw": response_text}

            diag_log("ZMQ-DIAG", "RECV", "Response from Backend", response)
            print(
                "\nPASS: ZMQ REQ/REP round-trip works. Response received."
            )
            return True

        except zmq.Again:
            diag_log("ZMQ-DIAG", "RECV", "TIMEOUT after 5s - no response from Backend")
            print(
                "\nFAIL: ZMQ REQ/REP timed out after 5s. Possible causes:\n"
                "  (1) CurveMQ still enabled in appsettings.json\n"
                "  (2) Backend not running\n"
                "  (3) Port 50001 not accepting connections"
            )
            return False

    except zmq.Again:
        diag_log("ZMQ-DIAG", "SEND", "TIMEOUT - could not send within 5s")
        print("\nFAIL: ZMQ send timed out after 5s.")
        return False

    except Exception:
        print(f"\nFAIL: Unexpected error during ZMQ test:")
        traceback.print_exc()
        return False

    finally:
        sock.close()
        ctx.term()


if __name__ == "__main__":
    print("=" * 60)
    print("=== ASPS Phase 1 Diagnostic: ZMQ REQ/REP Test ===")
    print("=" * 60)

    port_results = verify_ports()

    if not port_results.get(50001, False):
        print("\n=== Diagnostic Complete === OVERALL: FAIL (port 50001 not listening)")
        sys.exit(1)

    success = test_zmq_reqrep()

    overall = "PASS" if success else "FAIL"
    print(f"\n{'=' * 60}")
    print(f"=== Diagnostic Complete === OVERALL: {overall}")
    print(f"{'=' * 60}")

    sys.exit(0 if success else 1)

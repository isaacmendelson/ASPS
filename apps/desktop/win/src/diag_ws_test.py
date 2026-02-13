"""
ASPS Phase 1 Diagnostic: WebSocket Connectivity Test
=====================================================
Standalone script to verify that the Desktop App's WebSocket server
is reachable and responds to messages.

Tests:
  1. Port scan - finds the running WebSocket server
  2. Ping/Pong - verifies basic round-trip communication
  3. URL check simulation - verifies bidirectional message flow

Usage:
  python diag_ws_test.py
"""

import asyncio
import json
import sys
import datetime

try:
    import websockets
except ImportError:
    print("FAIL: 'websockets' package not installed. Run: pip install websockets")
    sys.exit(1)


# ---------------------------------------------------------------------------
# Diagnostic logging (same pattern used across Phase 1 diagnostic scripts)
# ---------------------------------------------------------------------------

def diag_log(component: str, direction: str, message: str, data: dict = None):
    """
    Phase 1 diagnostic logging with ISO-8601 UTC timestamps.

    Args:
        component: Label for the logging source (e.g. "WS-DIAG")
        direction: "SEND" or "RECV"
        message: Human-readable description
        data: Optional dict payload to pretty-print
    """
    ts = datetime.datetime.utcnow().isoformat() + "Z"
    prefix = ">>>" if direction == "SEND" else "<<<"
    print(f"[{ts}] [{component}] {prefix} {message}")
    if data:
        print(f"[{ts}] [{component}]     {json.dumps(data, indent=2, default=str)}")


# ---------------------------------------------------------------------------
# Port scan
# ---------------------------------------------------------------------------

PORTS = [8080, 8181, 8282, 8383, 8484]


async def find_ws_port():
    """
    Try connecting to each candidate port to find the running WebSocket server.

    Returns:
        int or None: The port number if found, else None.
    """
    for port in PORTS:
        diag_log("WS-DIAG", "SEND", f"Probing port {port}...")
        try:
            ws = await websockets.connect(
                f"ws://localhost:{port}",
                open_timeout=2,
            )
            await ws.close()
            diag_log("WS-DIAG", "RECV", f"Port {port} responded -- server found")
            return port
        except (OSError, asyncio.TimeoutError, ConnectionRefusedError, Exception):
            diag_log("WS-DIAG", "RECV", f"Port {port} -- no response")
    return None


# ---------------------------------------------------------------------------
# Ping / Pong test
# ---------------------------------------------------------------------------

async def test_ws_ping_pong(port: int) -> bool:
    """
    Connect to the WebSocket server, send a ping message, and verify pong.

    Returns:
        True if ping/pong succeeds, False otherwise.
    """
    ws = None
    try:
        ws = await websockets.connect(
            f"ws://localhost:{port}",
            open_timeout=5,
        )
        diag_log("WS-DIAG", "SEND", f"Connected to ws://localhost:{port}")

        # Send ping
        ping_msg = {"type": "ping"}
        await ws.send(json.dumps(ping_msg))
        diag_log("WS-DIAG", "SEND", "Ping message", ping_msg)

        # Wait for response
        raw = await asyncio.wait_for(ws.recv(), timeout=5.0)
        response = json.loads(raw)
        diag_log("WS-DIAG", "RECV", "Pong response", response)

        resp_type = response.get("type", "")
        if resp_type == "pong":
            print("\nPASS: WebSocket ping/pong works.")
            return True
        else:
            print(f"\nWARN: Unexpected response type: {resp_type}")
            return False

    except asyncio.TimeoutError:
        print("\nFAIL: WebSocket response timed out after 5s.")
        return False
    except ConnectionRefusedError:
        print(f"\nFAIL: Connection refused on port {port}.")
        return False
    except Exception as exc:
        print(f"\nFAIL: Unexpected error during ping/pong: {exc}")
        return False
    finally:
        if ws is not None:
            try:
                await ws.close()
            except Exception:
                pass


# ---------------------------------------------------------------------------
# URL check simulation test
# ---------------------------------------------------------------------------

async def test_ws_url_check(port: int) -> bool:
    """
    Send a simulated url_check message and observe the response.
    This exercises the full bidirectional message path.

    NOTE: This test may fail if the Backend server is not running.
    That is acceptable -- any response (including error) proves the
    Desktop App's WebSocket layer is processing messages.

    Returns:
        True if a response was received, False otherwise.
    """
    ws = None
    try:
        ws = await websockets.connect(
            f"ws://localhost:{port}",
            open_timeout=5,
        )
        diag_log("WS-DIAG", "SEND", f"Connected to ws://localhost:{port} for url_check test")

        url_check_msg = {
            "type": "url_check",
            "url": "https://diagnostic-test.example.com",
            "trackers": [],
            "iframes": [],
        }
        await ws.send(json.dumps(url_check_msg))
        diag_log("WS-DIAG", "SEND", "url_check message", url_check_msg)

        print("\nINFO: url_check requires Backend connection. "
              "If this fails, verify Plan 01-01 passed first.")

        # Longer timeout -- url_check may involve backend processing
        raw = await asyncio.wait_for(ws.recv(), timeout=10.0)
        response = json.loads(raw)
        diag_log("WS-DIAG", "RECV", "url_check response", response)

        print(f"INFO: Received url_check response (type={response.get('type', 'unknown')})")
        return True

    except asyncio.TimeoutError:
        print("INFO: url_check timed out after 10s (Backend may not be running).")
        return False
    except ConnectionRefusedError:
        print(f"FAIL: Connection refused on port {port} during url_check test.")
        return False
    except Exception as exc:
        print(f"INFO: url_check error: {exc}")
        return False
    finally:
        if ws is not None:
            try:
                await ws.close()
            except Exception:
                pass


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

async def _main():
    print("=" * 60)
    print("  ASPS Phase 1 Diagnostic: WebSocket Test")
    print("=" * 60)
    print()

    # Step 1: Find the server
    port = await find_ws_port()
    if port is None:
        print("\nFAIL: No WebSocket server found on ports 8080-8484. "
              "Start the Desktop App first.")
        sys.exit(1)

    print(f"\nFound WebSocket server on port {port}\n")

    # Step 2: Ping / Pong (required)
    ping_ok = await test_ws_ping_pong(port)

    # Step 3: URL check (best-effort, not a hard requirement)
    print()
    url_ok = await test_ws_url_check(port)

    # Summary
    print()
    print("=" * 60)
    print("  Summary")
    print("=" * 60)
    print(f"  Port scan:   PASS (port {port})")
    print(f"  Ping/Pong:   {'PASS' if ping_ok else 'FAIL'}")
    print(f"  URL check:   {'PASS' if url_ok else 'SKIPPED / FAIL (non-blocking)'}")
    print()
    overall = "PASS" if ping_ok else "FAIL"
    print(f"  Overall:     {overall}")
    print("=" * 60)

    if not ping_ok:
        sys.exit(1)


if __name__ == "__main__":
    asyncio.run(_main())

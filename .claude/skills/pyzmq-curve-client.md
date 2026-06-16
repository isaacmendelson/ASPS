---
name: pyzmq-curve-client
description: Scaffold a new pyzmq client that connects to an ASPS backend NetMQ endpoint with CURVE encryption. Mirrors the existing zmq_client.py pattern and the auth.json/server-key wiring.
---

# /pyzmq-curve-client

Creates a new pyzmq client class that connects to a backend endpoint (alert listener, notification publisher, business endpoint, CQRS gateway) with CURVE encryption applied correctly. Mirrors the canonical pattern in [apps/desktop/win/src/zmq_client.py:153-173](c:/Jobs/ASPS/GitHub/Software/apps/desktop/win/src/zmq_client.py).

## When to invoke
- User wants to add a new pyzmq client in the Windows desktop agent or an analyzer.
- User says "add ZMQ client", "connect to <port> from Python", "subscribe to notifications".

## Ask first

1. **Backend endpoint** — which port + which socket type does the backend bind? Cross-reference CLAUDE.md "Ports / messaging" table. Client choice follows:

   | Backend socket | Client socket |
   |---|---|
   | `RouterSocket` | `zmq.DEALER` (concurrent) or `zmq.REQ` (synchronous req/rep) |
   | `PublisherSocket` | `zmq.SUB` (with topic subscription) |
   | `PullSocket` | `zmq.PUSH` |

2. **CURVE on or off?** Default: ON, unless the backend port is one of the documented "security debt" ports (5555, 5556) and the user explicitly accepts that.

3. **Where does the server's public key come from?**
   - Desktop agent path: `auth.json` — set via `set_server_public_key()` after a login flow (see `auth_manager.py` for the pattern).
   - Analyzer / standalone tool path: read directly from a config file or env var. **Never** hard-code keys in source.

## Canonical pattern

From [zmq_client.py:153-173](c:/Jobs/ASPS/GitHub/Software/apps/desktop/win/src/zmq_client.py) — apply CURVE **before** `connect()`:

```python
import zmq

class MyClient:
    def __init__(self, host: str, port: int, timeout_ms: int = 5000):
        self.host = host
        self.port = port
        self.timeout = timeout_ms
        self.context = zmq.Context.instance()  # process-wide shared context
        self.socket = None
        self.server_public_key: bytes | None = None  # set externally

    def set_server_public_key(self, key: bytes) -> None:
        """Set the CURVE server public key. Must be called BEFORE connect()."""
        self.server_public_key = key

    def connect(self) -> bool:
        try:
            # Pick socket type per the backend's bound type:
            self.socket = self.context.socket(zmq.REQ)  # or DEALER / SUB / PUSH
            self.socket.setsockopt(zmq.RCVTIMEO, self.timeout)
            self.socket.setsockopt(zmq.SNDTIMEO, self.timeout)
            self.socket.setsockopt(zmq.LINGER, 0)

            # CURVE — must be set BEFORE connect()
            if self.server_public_key:
                client_public, client_secret = zmq.curve_keypair()  # ephemeral
                self.socket.setsockopt(zmq.CURVE_PUBLICKEY, client_public)
                self.socket.setsockopt(zmq.CURVE_SECRETKEY, client_secret)
                self.socket.setsockopt(zmq.CURVE_SERVERKEY, self.server_public_key)

            self.socket.connect(f"tcp://{self.host}:{self.port}")
            return True
        except Exception as e:
            logger.error(f"ZMQ connection failed: {e}")
            return False

    def close(self) -> None:
        if self.socket:
            try:
                self.socket.close()
            except zmq.ZMQError:
                pass
            self.socket = None
```

## Socket-type specifics

### `zmq.SUB` — subscribing to notifications (port 50002 pattern)

After `connect()`, subscribe to topics. Empty bytes = everything:

```python
self.socket.setsockopt(zmq.SUBSCRIBE, b"")          # all topics
self.socket.setsockopt(zmq.SUBSCRIBE, b"alert.")    # prefix filter
```

Receive in a worker thread:

```python
while self._running:
    try:
        msg = self.socket.recv_multipart()  # [topic, payload]
        topic, payload = msg
        # handle
    except zmq.Again:
        continue  # timeout — loop and check _running
```

### `zmq.DEALER` — concurrent requests to ROUTER

Unlike `REQ`, `DEALER` does not enforce strict req/rep alternation. Multiple sends without intervening receives are allowed. The backend ROUTER must echo a routing identity frame; the client typically prepends an empty delimiter frame:

```python
self.socket.send_multipart([b"", json.dumps(req).encode()])
_, reply = self.socket.recv_multipart()
```

### `zmq.PUSH` — fire-and-forget upload

No reply path. After `send()`, the agent has zero confirmation the message was processed. Use only for low-stakes telemetry. Always set `LINGER=0` so process exit isn't blocked.

## Context lifetime

- Use `zmq.Context.instance()` for a process-wide singleton. The desktop agent does this.
- Call `context.term()` exactly once on app shutdown — not per-client.
- Don't create a new context per client; that leaks threads.

## Reusing the agent's pattern

If you're adding a client to the desktop agent specifically, consider extending the existing `ZmqClient` (REQ to port 5555) or `NotificationClient` (SUB to port 50002) rather than creating a parallel class. Check first:

```bash
ls apps/desktop/win/src/*_client.py
```

## Verification

1. With CURVE enabled, a wrong server key results in a silent handshake failure — the connect succeeds, sends succeed, but no replies arrive. Surface this to the user as the most common cause of "it just hangs."

2. Tools to confirm the round trip:
   - [apps/desktop/win/src/diag_zmq_test.py](c:/Jobs/ASPS/GitHub/Software/apps/desktop/win/src/diag_zmq_test.py) — existing diagnostic.
   - [apps/desktop/win/src/curve_diagnostic.py](c:/Jobs/ASPS/GitHub/Software/apps/desktop/win/src/curve_diagnostic.py) — CURVE-specific.

3. On the backend, watch the ZMQ log line — `RealTimeAlertListener` and friends log a connection event with the CURVE status. If the connection appears unencrypted server-side but you set CURVE client-side, the issue is almost always: CURVE options set *after* connect, wrong server key, or `auth.json` not loaded.

## Never

- Hard-code `CURVE_SECRETKEY` in source. Use `zmq.curve_keypair()` for ephemeral client keys.
- Hard-code the server's public key. Read from `auth.json` (desktop agent) or a config file (analyzers).
- Set CURVE options after `connect()` — silently ineffective, the socket runs unencrypted.
- Reuse one socket from multiple threads without external locking — pyzmq sockets are not thread-safe. Contexts are.
- Skip `LINGER=0` — process exit hangs while pyzmq waits to flush.

## Output convention

```
Client class: <Name>Client
Socket type: zmq.<TYPE>
Backend endpoint: tcp://<host>:<port> (<ROUTER|PUB|...>)
CURVE: enabled/disabled (<reason>)
Server key source: auth.json | config | env
Files created:
  - apps/desktop/win/src/<name>_client.py
  - (optional) apps/desktop/win/src/tests/test_<name>_client.py
Verified with: diag_zmq_test.py | curve_diagnostic.py | live backend
```

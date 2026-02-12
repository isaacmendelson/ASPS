# Phase 4: Restore CurveMQ Security - Research

**Researched:** 2026-02-12
**Domain:** CurveZMQ encryption between pyzmq (Python) client and NetMQ (C#) server
**Confidence:** HIGH

## Summary

This phase re-enables CurveMQ encryption on all ZMQ sockets between the Desktop App (Python/pyzmq) and the Backend (C#/NetMQ). The Backend already has a complete CurveMQ server implementation: `CurveKeyManager.cs` loads/generates keys, `RealTimeAlertListener.cs` applies CURVE to the REP socket, `NotificationPublisher.cs` applies CURVE to the PUB socket, and all token responses already include `serverPublicKey` (Z85 format). The work is entirely on the Python client side -- adding CURVE client options to both the REQ and SUB sockets, and extracting the server public key from the token response to enable encrypted connections.

The CurveMQ protocol uses Curve25519 elliptic-curve cryptography. The server configures `CurveServer = true` and its keypair. The client generates its own ephemeral keypair, sets the server's public key, and sets its own public/secret keys on the socket. The server does NOT need to know the client's public key in advance when running in "allow any client" mode (which is the current Backend behavior -- there is no ZAP authenticator rejecting unknown client keys). Each connection establishes new session keys providing perfect forward secrecy.

The key risk factors are: (1) Z85 key format conversion between NetMQ's custom Z85 encoder and pyzmq's `zmq.utils.z85` module, (2) CURVE options must be set BEFORE `connect()` is called, and (3) silent handshake failures -- a CURVE mismatch produces timeouts, not error messages, making debugging difficult.

**Primary recommendation:** Add CURVE client configuration to `zmq_client.py` and `notification_client.py`, using the server public key obtained from the RegisterDevice/RequestToken response, then flip `CurveEnabled: true` in `appsettings.json`.

## Standard Stack

The established libraries/tools for this domain:

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| pyzmq | (already installed) | ZMQ client with CURVE support | Built-in CURVE support via libzmq 4.x |
| NetMQ | (already installed) | ZMQ server with CURVE support | `NetMQCertificate` and `CurveServer` API |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| zmq.utils.z85 | (part of pyzmq) | Z85 encode/decode for CURVE keys | Converting server Z85 public key to binary bytes |
| zmq.curve_keypair() | (part of pyzmq) | Generate client CURVE keypair | Generating ephemeral client keys on each connect |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Ephemeral client keys (new each launch) | Persisted client keys | Persistence adds complexity but allows server-side whitelisting; NOT needed here since server allows all clients |
| Z85 key from token response | Hardcoded Z85 key in config.py | Config avoids network dependency but loses flexibility if server keys change; token response is already implemented |

**Installation:**
No new packages needed. pyzmq already includes CURVE support (requires libzmq >= 4.0, which all modern pyzmq wheels bundle).

## Architecture Patterns

### Current Architecture (Phase 3 - No CURVE)
```
Desktop App (Python)                    Backend (C#/NetMQ)
 zmq_client.py                          RealTimeAlertListener.cs
   REQ socket ----plaintext---->          REP socket (CurveEnabled=false)

 notification_client.py                 NotificationPublisher.cs
   SUB socket ----plaintext---->          PUB socket (CurveEnabled=false)
```

### Target Architecture (Phase 4 - With CURVE)
```
Desktop App (Python)                    Backend (C#/NetMQ)
 zmq_client.py                          RealTimeAlertListener.cs
   REQ socket ----CURVE encrypted--->     REP socket (CurveServer=true)
   (client keypair + server pub key)      (server keypair from CurveKeyManager)

 notification_client.py                 NotificationPublisher.cs
   SUB socket ----CURVE encrypted--->     PUB socket (CurveServer=true)
   (client keypair + server pub key)      (server keypair from CurveKeyManager)
```

### Pattern 1: Server Public Key Distribution via Token Response
**What:** Backend already includes `serverPublicKey` (Z85 format) in RegisterDevice, RequestToken, and RefreshToken responses. The Desktop App should extract and store this key during authentication.
**When to use:** Always -- this is the designed key distribution mechanism.
**Evidence:** Lines 250, 263, 306, 343, 382 of `RealTimeAlertListener.cs` all include:
```csharp
serverPublicKey = _curveKeyManager?.ServerPublicKeyZ85 ?? string.Empty
```

### Pattern 2: CURVE Client Socket Configuration
**What:** Before calling `socket.connect()`, the client must set three socket options: its own public/secret keypair and the server's public key.
**When to use:** Every time a CURVE-enabled socket is created.
**Sequence:**
```
1. Generate client keypair:  zmq.curve_keypair()
2. Set socket options:       curve_publickey, curve_secretkey, curve_serverkey
3. THEN connect:            socket.connect(endpoint)
```

### Pattern 3: Two-Phase Startup (Plain then Encrypted)
**What:** The Desktop App must first connect WITHOUT CURVE to get the token and server public key (RegisterDevice), then reconnect WITH CURVE for all subsequent communication.
**When to use:** This is required because the server public key is delivered in the token response, but the initial token request must work without encryption (chicken-and-egg problem).
**CRITICAL INSIGHT:** Actually, looking at the current code flow:
- `CurveEnabled=false` during Phases 1-3, meaning the initial RegisterDevice happens without CURVE
- When we flip `CurveEnabled=true`, the Backend's REP socket will REQUIRE CURVE
- This means RegisterDevice itself must use CURVE
- Therefore: we CANNOT get the server key from the token response if we need the server key to connect!
**Resolution:** The server public key Z85 (`qPsk#8DY:n9ovp[vQ!YcOnOX[f/.i@.g^f#b:!ik`) must be configured in the Desktop App (via config.py or environment variable), not solely retrieved from the token response. The token response serves as confirmation/validation.

### Anti-Patterns to Avoid
- **Setting CURVE options after connect():** The CURVE handshake happens during connection setup. Setting options after connect() is too late and will silently fail.
- **Reusing a non-CURVE socket:** You cannot "upgrade" an existing socket to CURVE. A new socket must be created with CURVE options set before connect().
- **Assuming connect() failure means key mismatch:** ZMQ `connect()` always succeeds immediately (it is async). A key mismatch manifests as a timeout on the first send/recv, not a connection error.
- **Using base64 keys where Z85 is expected:** The Backend stores keys in both base64 (`ServerPublicKey`) and Z85 (`ServerPublicKeyZ85`). pyzmq CURVE options accept Z85-encoded bytes (40 chars) or raw binary bytes (32 bytes). Never pass base64 to pyzmq.

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Client keypair generation | Manual Curve25519 math | `zmq.curve_keypair()` | Returns properly formatted Z85 keypair |
| Z85 decode | Custom Z85 decoder | `zmq.utils.z85.decode()` | Matches ZMQ RFC 32, handles bytes properly |
| Z85 encode | Custom Z85 encoder | `zmq.utils.z85.encode()` | Same as above |
| CURVE authentication | ZAP handler, cert management | Set `curve_serverkey` on socket | Server accepts all clients by default (no ZAP needed) |

**Key insight:** The CURVE client setup in pyzmq is just 3 socket options. The entire implementation is approximately 10 lines of code per socket. Do not over-engineer this.

## Common Pitfalls

### Pitfall 1: Silent Handshake Failure (Timeout, Not Error)
**What goes wrong:** CURVE key mismatch produces a timeout on the first send/recv, with no error message indicating the keys are wrong.
**Why it happens:** ZMQ's connect() is async and always succeeds. The CURVE handshake happens in the background. If it fails, the connection simply never completes, and send/recv block until timeout.
**How to avoid:** Add explicit logging when CURVE is configured ("CURVE enabled with server key: [first 10 chars]..."). If a timeout occurs immediately after enabling CURVE, suspect key format issues first.
**Warning signs:** `zmq.Again` timeout on the FIRST message after enabling CURVE, when the same code worked without CURVE.

### Pitfall 2: Z85 Key Format Mismatch Between NetMQ and pyzmq
**What goes wrong:** NetMQ's custom `Z85.Encode()` in `CurveKeyManager.cs` produces a Z85 string. pyzmq's `zmq.utils.z85.decode()` expects bytes input (not string). If you pass a Python `str` to pyzmq's Z85 functions without encoding to bytes first, you may get errors or wrong results.
**Why it happens:** Python 3 strict string/bytes separation. Z85 functions work with `bytes` objects.
**How to avoid:** Always encode the Z85 string to ASCII bytes before passing to pyzmq: `z85_bytes = z85_string.encode('ascii')`. Or pass the Z85 key directly as bytes to `curve_serverkey` which accepts 40-byte Z85-encoded values.
**Warning signs:** `TypeError` about str vs bytes, or connection timeout when keys look correct.

### Pitfall 3: Chicken-and-Egg Key Distribution
**What goes wrong:** When `CurveEnabled=true`, the Backend's REP socket requires CURVE. But the Desktop App needs to connect to get the server public key from the token response. Can't connect without the key, can't get the key without connecting.
**Why it happens:** The key distribution mechanism (token response) relies on the connection that requires the key.
**How to avoid:** Hardcode or configure the server public key in the Desktop App (config.py or environment variable). The token response `serverPublicKey` field serves as validation, not as the primary source.
**Warning signs:** Connection timeout on RegisterDevice when CURVE is first enabled.

### Pitfall 4: Forgetting to CURVE-Enable the SUB Socket
**What goes wrong:** Developer enables CURVE on the REQ socket but forgets the SUB socket. REQ/REP works, but PUB/SUB notifications silently stop arriving.
**Why it happens:** The REQ and SUB sockets are in different files (`zmq_client.py` and `notification_client.py`) and are configured independently.
**How to avoid:** Both sockets MUST have CURVE configured. Add the same CURVE setup to both. Test both paths after enabling.
**Warning signs:** Alerts work (REQ/REP) but notifications stop (PUB/SUB).

### Pitfall 5: CURVE Options Set After connect()
**What goes wrong:** Code sets `socket.curve_serverkey` after `socket.connect()`. CURVE handshake was already attempted (and failed) during connect.
**Why it happens:** Developer moves CURVE configuration code after the connect call, or refactors without noticing the ordering dependency.
**How to avoid:** Always configure CURVE options BEFORE calling connect(). Add a code comment: "# CURVE options MUST be set before connect()".
**Warning signs:** Connection appears to succeed but first send/recv times out.

### Pitfall 6: NetMQ-pyzmq Interoperability Concerns
**What goes wrong:** Some users have reported communication failures between NetMQ and pyzmq (GitHub Issue #16 in NetMQ/Samples).
**Why it happens:** Protocol version differences, ZMTP handshake version negotiation.
**How to avoid:** The current codebase already has working NetMQ-pyzmq communication (Phases 1-3 proved this). CURVE adds a layer on top of the existing ZMTP transport. The DeviceLogin.cshtml.cs page in the Backend already demonstrates CURVE client connecting to NetMQ CURVE server successfully (C# to C#). The risk is LOW because the underlying transport is already proven.
**Warning signs:** If CURVE connections fail, first verify with a simple test script before suspecting interoperability.

## Code Examples

Verified patterns from official ZeroMQ documentation and the existing codebase:

### CURVE Client Configuration for REQ Socket (zmq_client.py)
```python
# Source: ZMQ CURVE RFC + pyzmq docs
import zmq

def _apply_curve(socket, server_public_key_z85: bytes):
    """Apply CURVE client configuration to a ZMQ socket.

    MUST be called BEFORE socket.connect().

    Args:
        socket: ZMQ socket (REQ, SUB, etc.)
        server_public_key_z85: Server public key as 40-byte Z85-encoded bytes
    """
    # Generate ephemeral client keypair
    client_public, client_secret = zmq.curve_keypair()

    # Set client keys
    socket.curve_publickey = client_public
    socket.curve_secretkey = client_secret

    # Set the server's public key
    socket.curve_serverkey = server_public_key_z85
```

### CURVE Client Configuration for SUB Socket (notification_client.py)
```python
# Source: ZMQ CURVE RFC + pyzmq docs
# Same pattern as REQ -- the CURVE configuration is identical for all client socket types
import zmq

context = zmq.Context()
socket = context.socket(zmq.SUB)

# Generate ephemeral client keypair
client_public, client_secret = zmq.curve_keypair()

# Set CURVE options BEFORE connect
socket.curve_publickey = client_public
socket.curve_secretkey = client_secret
socket.curve_serverkey = server_public_key_z85  # 40-byte Z85 bytes

# THEN connect
socket.connect(f"tcp://{host}:{port}")

# Subscribe as normal
topic = f"device:{device_uid}"
socket.subscribe(topic.encode('utf-8'))
```

### Z85 Key Conversion
```python
# Source: pyzmq zmq.utils.z85 docs
import zmq.utils.z85

# Backend returns Z85 as a string (e.g., from appsettings.json or token response)
z85_string = "qPsk#8DY:n9ovp[vQ!YcOnOX[f/.i@.g^f#b:!ik"

# Method 1: Use the Z85 string as bytes directly (pyzmq accepts 40-byte Z85)
z85_bytes = z85_string.encode('ascii')  # 40 bytes
# socket.curve_serverkey = z85_bytes  # This works

# Method 2: Decode Z85 to 32-byte binary key
binary_key = zmq.utils.z85.decode(z85_bytes)  # 32 bytes
# socket.curve_serverkey = binary_key  # This also works
```

### Backend Server Configuration (Already Implemented)
```csharp
// Source: CurveKeyManager.cs (existing code, no changes needed)
// Applied to both REP and PUB sockets via ApplyServerCurve()
public void ApplyServerCurve(NetMQSocket socket)
{
    if (!_curveEnabled || ServerSecretKey.Length == 0)
        return;

    socket.Options.CurveServer = true;
    socket.Options.CurveCertificate = new NetMQCertificate(ServerSecretKey, ServerPublicKey);
}
```

### DeviceLogin.cshtml.cs CURVE Client Pattern (Existing Reference)
```csharp
// Source: DeviceLogin.cshtml.cs lines 135-143 (existing code -- this is
// the only existing CURVE CLIENT in the codebase, can be used as reference)
if (_curveEnabled && !string.IsNullOrEmpty(_serverPublicKeyZ85))
{
    var clientCert = new NetMQCertificate();  // Generate client keypair
    socket.Options.CurveCertificate = clientCert;
    socket.Options.CurveServerKey = DecodeZ85(_serverPublicKeyZ85);
}
```

### Token Response Containing Server Public Key (Already Implemented)
```json
// Source: RealTimeAlertListener.cs RegisterDevice response
{
    "status": "Registered",
    "token": "abc123...",
    "expiration": "2026-02-13T12:00:00Z",
    "deviceUid": "PC-JOHN-001",
    "serverPublicKey": "qPsk#8DY:n9ovp[vQ!YcOnOX[f/.i@.g^f#b:!ik"
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| No encryption (Phases 1-3) | CurveMQ encryption (Phase 4) | Now | All ZMQ traffic encrypted with Curve25519 |
| Hardcoded server key only | Server key from token response + config fallback | Already in Backend | Client can verify key matches |

**Deprecated/outdated:**
- The custom Z85 encoder in `CurveKeyManager.cs` is a manual implementation. NetMQ provides `Z85.Encode()` but the custom one is already tested and working. No action needed.

## Detailed Implementation Sequence

This section provides the specific implementation steps for the planner:

### Step 1: Add Server Public Key to config.py
Add `SERVER_PUBLIC_KEY_Z85` to `config.py` with the value from `appsettings.json`, readable from environment variable for flexibility:
```python
SERVER_PUBLIC_KEY_Z85 = os.environ.get(
    'ZMQ_SERVER_PUBLIC_KEY',
    'qPsk#8DY:n9ovp[vQ!YcOnOX[f/.i@.g^f#b:!ik'
)
CURVE_ENABLED = _parse_bool(os.environ.get('CURVE_ENABLED', 'true'))
```

### Step 2: Add CURVE Client Helper
Create a small helper function (in `zmq_client.py` or a shared module) that applies CURVE to any socket:
```python
def apply_curve_client(socket, server_public_key_z85: str):
    """Apply CURVE client encryption to a socket. Call BEFORE connect()."""
    client_public, client_secret = zmq.curve_keypair()
    socket.curve_publickey = client_public
    socket.curve_secretkey = client_secret
    socket.curve_serverkey = server_public_key_z85.encode('ascii')
```

### Step 3: Modify zmq_client.py connect() Method
In the `connect()` method, add CURVE configuration before `socket.connect()`:
```python
def connect(self) -> bool:
    self.context = zmq.Context()
    self.socket = self.context.socket(zmq.REQ)
    self.socket.setsockopt(zmq.RCVTIMEO, self.timeout)

    # Apply CURVE if enabled
    if self.curve_enabled and self.server_public_key:
        apply_curve_client(self.socket, self.server_public_key)

    self.socket.connect(f"tcp://{self.host}:{self.port}")
```

### Step 4: Modify notification_client.py _listen() Method
In the `_listen()` method, add CURVE before `socket.connect()`:
```python
self.socket = self.context.socket(zmq.SUB)
self.socket.setsockopt(zmq.RCVTIMEO, 5000)

# Apply CURVE if enabled
if self.curve_enabled and self.server_public_key:
    apply_curve_client(self.socket, self.server_public_key)

self.socket.connect(f"tcp://{self.host}:{self.port}")
```

### Step 5: Extract Server Key from Token Response
In `auth_manager.py`, capture and store `serverPublicKey` from RegisterDevice response:
```python
server_key = result.get('serverPublicKey') or result.get('server_public_key', '')
if server_key:
    self.server_public_key = server_key
```

### Step 6: Flip Backend to CurveEnabled=true
Change `ASPSBackend14_J/ASPSBackend/appsettings.json`:
```json
"Security": {
    "CurveEnabled": true,
    ...
}
```

### Step 7: Verify End-to-End with CURVE
Run the same tests from Phase 3 but with CURVE enabled.

## Open Questions

Things that couldn't be fully resolved:

1. **NetMQ-pyzmq CURVE interoperability verification**
   - What we know: Plain (non-CURVE) NetMQ-pyzmq communication works (proven in Phases 1-3). NetMQ CURVE server works with NetMQ CURVE client (DeviceLogin.cshtml.cs). pyzmq CURVE is well-documented.
   - What's unclear: Whether the specific Z85 encoding in `CurveKeyManager.cs` produces keys compatible with pyzmq's Z85 decoder. The Z85 character set and algorithm are standardized (ZMQ RFC 32), so they SHOULD be compatible, but this has not been tested.
   - Recommendation: The implementation should include a diagnostic step that tests CURVE connectivity in isolation before integrating into the full pipeline. If CURVE fails, first verify Z85 key decoding produces identical 32-byte binary keys on both sides.

2. **Whether zmq.curve_keypair() requires libsodium**
   - What we know: `zmq.curve_keypair()` requires libzmq to be built with CURVE support (requires libsodium or built-in tweetnacl).
   - What's unclear: Whether the pyzmq wheel installed on this system includes CURVE support.
   - Recommendation: Test with `python -c "import zmq; print(zmq.curve_keypair())"` early in the plan. If it raises `zmq.error.ZMQError: ... not supported`, the pyzmq installation lacks CURVE support and may need reinstall with `pip install pyzmq --no-binary :all:` to compile with libsodium.

3. **Whether auth_manager.py should store and pass the server key to both clients**
   - What we know: The server public key is returned in the token response. Both ZMQ clients (REQ and SUB) need this key.
   - What's unclear: Best place to store and distribute: auth_manager, config, or container.
   - Recommendation: Use config.py as the primary source (hardcoded default from appsettings.json), with token response as validation. The container.py can pass the key to both clients during construction.

## Sources

### Primary (HIGH confidence)
- **Codebase analysis** - Direct reading of `CurveKeyManager.cs`, `RealTimeAlertListener.cs`, `NotificationPublisher.cs`, `DeviceLogin.cshtml.cs`, `zmq_client.py`, `notification_client.py`, `auth_manager.py`, `config.py`, `container.py`, `main.py`, `appsettings.json`
- **ZMQ CURVE RFC** - [zmq_curve(7) official docs](https://libzmq.readthedocs.io/en/latest/zmq_curve.html) - Socket options, key formats, client/server configuration
- **ZMQ setsockopt docs** - [zmq_setsockopt(3)](https://libzmq.readthedocs.io/en/zeromq4-1/zmq_setsockopt.html) - CURVE_PUBLICKEY, CURVE_SECRETKEY, CURVE_SERVERKEY format specifications
- **pyzmq z85 module** - [zmq.utils.z85 docs](https://pyzmq.readthedocs.io/en/latest/api/zmq.utils.z85.html) - Z85 encode/decode API
- **pyzmq stonehouse example** - [pyzmq security examples](https://github.com/zeromq/pyzmq/blob/main/examples/security/stonehouse.py) - Complete CURVE client/server example

### Secondary (MEDIUM confidence)
- **NetMQ CURVE tests** - [CurveTests.cs](https://github.com/zeromq/netmq/blob/master/src/NetMQ.Tests/CurveTests.cs) - NetMQ CURVE server/client test patterns
- **NetMQ bad keys issue** - [Issue #908](https://github.com/zeromq/netmq/issues/908) - Key file reading issues (resolved, not key generation)
- **CurveZMQ RFC 26** - [ZeroMQ RFC 26](https://rfc.zeromq.org/spec/26/) - Full CurveZMQ protocol specification

### Tertiary (LOW confidence)
- **NetMQ-pyzmq interop issue** - [NetMQ/Samples #16](https://github.com/NetMQ/Samples/issues/16) - Reported interop failures (unresolved, but our non-CURVE transport works fine, suggesting the issue was environment-specific)
- **WebSearch results** - Various community posts about CURVE setup patterns (cross-verified with official docs)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - pyzmq and NetMQ are the locked choices, CURVE API is well-documented
- Architecture: HIGH - Backend implementation is complete and readable in the codebase; the key distribution pattern is already implemented
- Pitfalls: HIGH - Identified from official docs, codebase analysis, and community reports; the silent-failure nature of CURVE is well-known
- Code examples: HIGH - Based on official ZMQ docs and the existing Backend code (DeviceLogin.cshtml.cs CURVE client)

**Research date:** 2026-02-12
**Valid until:** 2026-03-12 (stable domain, no fast-moving dependencies)

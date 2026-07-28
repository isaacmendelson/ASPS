# ADR-002 — ASPS-610 Mutually Authenticated Desktop–Extension IPC

- Status: Proposed
- Date: 2026-07-28
- Jira: ASPS-610 — `[CODE REVIEW] Implement mutually authenticated Desktop-Extension IPC`
- Decision owners: Architect; implementation owners listed below
- Depends on: ADR-001 / ASPS-611 envelope v1; ASPS-619 for Desktop–Backend enrollment

## Context

The Desktop agent currently binds a WebSocket listener to the first available
port in `[8080, 8181, 8282, 8383, 8484]`. It accepts every client, invokes
privileged handlers before identity is established, and broadcasts results to
every connected client. The Chrome extension probes those same ports and
trusts the first listener. Neither endpoint validates `Origin`, a pairing,
message integrity, a session, or replay state.

This is a Critical boundary: a browser page or local process can impersonate
an Extension to submit scans, browser-tab data, auth/sign-out and danger
events; a local listener can impersonate Desktop and inject results or
protective state. The current `ping`/heartbeat is liveness only and is not an
authentication mechanism.

This ADR secures the existing local WebSocket transport. It does not claim to
protect a Chrome profile or Windows account already compromised by an
attacker able to read/alter both applications' private state, install an
extension, attach a debugger, or alter the Desktop executable. Those require
code signing, Chrome enterprise policy and/or a later migration to Chrome
Native Messaging. `Origin` alone is not authentication because a non-browser
client can forge an HTTP header.

## Decision

### 1. Trust model and scope

The production listener binds only to the IPv4 and IPv6 loopback interfaces;
it never binds a wildcard address. A successful TCP/WebSocket upgrade grants
no privilege. Only a completed mutually authenticated session can send or
receive operational data.

The Desktop maintains explicit pairing records keyed by an opaque `pairId`:

| State | May do |
|---|---|
| Unauthenticated socket | Receive only `ipc.hello`, `ipc.pair.*`, and one safe `ipc.error`; no data, no callbacks, no broadcasts. |
| Pairing in progress | Complete the bounded PAKE exchange only. |
| Authenticated session | Send/receive the allowlisted v1 envelopes assigned to that pairing. |
| Closed/revoked/expired | Nothing; the socket is closed. |

The server accepts at most one active authenticated session per pairing by
default. A new authenticated connection closes the previous session for that
pairing. Desktop broadcasts are replaced with `send_to_pair(pairId, frame)`;
there is no unauthenticated or all-client broadcast path.

### 2. Defence-in-depth browser binding

At the HTTP upgrade, Desktop validates all of the following before it even
allocates a handshake state:

1. remote address is loopback;
2. `Origin` is exactly `chrome-extension://<allowed-extension-id>` (no absent,
   `null`, wildcard, prefix, development-page or website origin);
3. WebSocket subprotocol is exactly `asps-ipc-v1`;
4. request size, headers and connection count are within configured limits.

The packaged installer supplies the stable production extension ID to the
Desktop configuration. Development accepts an explicit development ID only
under a non-production build/configuration; it must never be a production
wildcard. Origin failure closes the upgrade without a protocol oracle.

This validation prevents ordinary web origins from reaching the protocol. It
does *not* replace the cryptographic pairing proof, which is what prevents a
raw local client from impersonating Chrome.

### 3. User-mediated PAKE pairing bootstrap

Pairing uses a vetted, maintained SPAKE2+ implementation with independent
Python and browser-JavaScript test vectors. Do not implement a PAKE primitive
in ASPS. The library and parameters (including P-256/group choice,
transcript encoding and version) are frozen in the generated IPC contract.

1. User selects **Pair extension** in the signed Desktop tray UI. Desktop
   displays a fresh, single-use, high-entropy pairing code and expiry (10
   minutes), tied to the expected extension ID. It stores only the PAKE
   verifier/state until completion.
2. User opens the Extension popup and enters that code. The popup explicitly
   identifies the Desktop device being paired; it never silently pairs on
   reconnect.
3. Extension and Desktop exchange `ipc.pair.start` / `ipc.pair.finish` PAKE
   messages. A listener impersonating Desktop cannot learn the code-derived
   secret or complete the proof merely by observing/relaying the exchange.
4. Both sides bind the PAKE transcript to `ipcProtocolVersion`, extension ID,
   pair ID, Desktop stable device ID, selected port, and the code expiry.
   Desktop verifies the Origin/extension ID again before committing.
5. On proof success both derive `pairKey = HKDF-SHA-256(pakeSecret,
   salt=transcriptHash, info="ASPS IPC pairing v1")`, generate a random
   `pairId`, persist it, invalidate the one-time code, and require a new
   authenticated connection. No operational message is allowed on the
   pairing connection.

The code is never logged, persisted after completion, copied to backend,
sent in diagnostic telemetry, or reused. Pairing rate limits: three failures
per code, five active attempts per Origin/device per minute, then close and
require a fresh tray action. Errors are deliberately non-specific.

### 4. Mutual session authentication and frame integrity

Every reconnect establishes a fresh session; a stored pairing is not itself a
live session.

```text
Extension → Desktop: ipc.hello { pairId, clientNonce, supportedSchemaMajors:[1] }
Desktop   → Extension: ipc.challenge { pairId, sessionId, serverNonce,
                                       selectedSchemaMajor:1, expiresAt }
Extension → Desktop: ipc.client_proof { proof = HMAC(pairKey, transcript || "client") }
Desktop   → Extension: ipc.server_proof { proof = HMAC(pairKey, transcript || "server") }
```

`transcript` is a length-delimited canonical binary encoding of every
handshake field above plus the verified origin, loopback endpoint and protocol
version. It is never an ad-hoc JSON concatenation. A proof failure closes the
socket without dispatching callbacks and increments an audit counter without
including secrets or URLs.

After both proofs validate, both sides derive directional session keys using
HKDF-SHA-256 over `pairKey`, both nonces, `sessionId`, direction and protocol
version. The session lifetime is at most 12 hours and ends immediately on
socket close, missed heartbeat, key revocation, Origin mismatch or any
integrity/replay failure.

Each operational WebSocket text frame is a strict wrapper around ASPS-611's
envelope:

```json
{
  "ipcVersion": 1,
  "sessionId": "uuid",
  "direction": "extension_to_desktop",
  "sequence": "42",
  "envelope": { "schemaVersion": "1.0", "messageId": "..." },
  "mac": "base64url-hmac-sha256"
}
```

`mac` is HMAC-SHA-256 with the direction-specific session key over the RFC
8785 JSON Canonicalization Scheme representation of every wrapper member
except `mac`. `ipcVersion`, direction, session ID and sequence are therefore
integrity-bound along with the complete envelope. Frame limits are 64 KiB
(pairing 8 KiB); binary frames, compressed frames, duplicate properties,
unknown required wrapper fields and malformed canonical JSON are rejected.

### 5. Replay, ordering and session/origin binding

WebSocket ordering is not used as a security property. Each direction starts
at sequence `1` and Desktop/Extension require exactly the next unsigned-64
decimal sequence for the live session. A gap, duplicate, lower sequence,
wrong direction, wrong session ID, invalid MAC, expired session or repeated
proof closes the connection and removes its authenticated principal. There is
no session resumption and no sequence reset inside a session.

At the application layer, ASPS-611 remains authoritative: messages are
validated only *after* IPC verification; `messageId` is deduplicated in a
bounded retained set and immutable `{requestId, correlationId, tabId, url}`
is verified. The Desktop records `pairId`, verified extension ID and session
ID as connection metadata, and routes results only to the owning pairing.
It must never treat an authenticated session as authority to alter a request
whose `deviceId`, tab context or request correlation fails validation.

### 6. Key storage, rotation and recovery

Desktop stores a per-pair `pairKey` and metadata through Windows Credential
Manager/keyring under a versioned ASPS target, with no plaintext fallback.
The Extension stores its pair record (`pairId`, `pairKey`, protocol/version,
creation/rotation metadata) in extension-private `chrome.storage.local`, not
in page-accessible storage, synced storage, logs or the manifest. Both stores
contain only one active key per pairing.

Rotation requires fresh user-mediated PAKE pairing and atomically replaces the
record: on explicit user reset, pairing age of 90 days, extension ID/profile
change, Desktop reinstall/device identity change, suspected compromise, or
protocol-major upgrade. Desktop retains the old key only for the current
authenticated socket's bounded drain (five minutes); it cannot authenticate a
new session. Lost/corrupt/mismatched storage fails closed and presents a
"Pair extension" recovery action; it does not regenerate a shared secret or
downgrade.

`pairKey` is local IPC material only: never a Backend token, CURVE key, email,
device credential or cross-device secret. Backend authentication and account
authorization remain separate; receiving `user_auth` over IPC must not make
an arbitrary email a trusted user identity.

### 7. Schema negotiation and migration

ASPS-611 supplies envelope major-version negotiation. `supportedSchemaMajors`
is exchanged inside the authenticated session handshake; Desktop chooses the
highest mutually supported major. No operational envelope is parsed until the
selection is integrity-bound in the handshake transcript.

There is no production legacy unauthenticated fallback. This is an intentional
security cutover:

1. Ship Desktop support (strict upgrade/origin gate, PAKE, authenticated frame
   codec, per-pair routing) behind an installation feature flag that defaults
   **on** in production.
2. Ship Extension pairing UI, pair record store and authenticated codec.
3. Deploy the Desktop installer and matching Extension together; an old
   extension appears disconnected and is offered an upgrade/pair instruction,
   not a legacy WebSocket service.
4. Permit an explicitly named `ASPS_IPC_ALLOW_LEGACY_V0_FOR_TESTS` only in
   non-production test fixtures. It is forbidden in release packaging and CI
   asserts that production config has no legacy mode.
5. Remove the old raw `send`, raw handler dispatch, `clients` broadcast and
   predictable multi-client semantics after the compatibility release.

Ports remain an implementation discovery detail; the port list is not a
credential. The Extension scans only after an explicit start/reconnect and
requires the authenticated subprotocol/handshake; it does not accept a bare
WebSocket `open` as Desktop presence.

### 8. Native Messaging direction

Chrome Native Messaging is the preferred future transport because Chrome
enforces the registered native-host `allowed_origins` and removes the
discoverable TCP listener. ASPS-610 preserves a transport-neutral IPC frame
and pairing/session state machine so a later native-host adapter can replace
the WebSocket framing without changing ASPS-611 envelopes. Native Messaging
is not silently introduced in this ticket: it needs installer/host manifest,
lifecycle and Desktop process-ownership design. It is a follow-up hardening
item, not a reason to weaken this cutover.

## Threat model and resulting controls

| Threat | Required control | Residual / non-goal |
|---|---|---|
| Hostile website opens localhost WS | Exact extension Origin, subprotocol, loopback-only bind, PAKE/session proof | Origin filtering is not identity by itself. |
| Local process binds a scanned port and impersonates Desktop | PAKE bootstrap; client verifies server proof before data | Same-account profile compromise is outside this transport boundary. |
| Local process connects to Desktop and impersonates Extension | Origin gate plus PAKE/session client proof before any handler | A process that steals the pair key can impersonate; key theft triggers reset/re-pair. |
| Captured/injected/tampered frame | Directional session HMAC over canonical wrapper | TLS confidentiality is not provided by `ws`; payload confidentiality is a separate Native Messaging/TLS follow-up. |
| Replay/reorder/cross-session frame | Strict per-direction sequence, fresh nonces/session, short expiry, no resumption | Application duplicate handling remains `messageId` scope. |
| Fake result targets another tab/session | Pair-owned routing plus ASPS-611 immutable context validation | ASPS-621 owns final tab enforcement behavior. |
| Pairing-code brute force / abuse | PAKE, single-use expiry, rate limit, explicit tray action | User must verify they initiated pairing. |
| Pair key/profile/desktop compromise | OS/extension-private stores, reset/rotation, signed distribution | Full same-user compromise cannot be solved by a localhost WebSocket protocol. |

## Rejected alternatives

- **Trust `localhost` or five known ports:** any local process can listen or
  connect; port selection is discovery, not authorization.
- **Origin allow-list only:** raw clients can forge Origin and no message
  integrity/replay protection results.
- **Static bearer token sent in the first WebSocket frame:** an impostor
  listener captures it during first pairing; it also permits replay.
- **Use Backend token/CURVE keys as the IPC key:** couples independent trust
  domains and leaks higher-value credentials to a browser boundary.
- **Keep v0 during migration:** an unauthenticated compatibility path is a
  permanent bypass of the Critical remediation.
- **Hand-roll PAKE or JSON signing:** cryptographic transcript/canonicalization
  errors defeat the design; use vetted PAKE and RFC 8785 implementations.

## Implementation plan and ownership

| Order | Owner | Files/responsibility | Depends on |
|---:|---|---|---|
| 1 | `desktop-agent` | `extension_server.py`, new IPC pairing/session/key-store modules, `config.py`, `main.py`, handler routing and per-pair send API | ADR approval; approved PAKE library |
| 2 | `browser-extension` | `ConnectionService.js`, popup pairing UX, manifest/config validation, frame codec and storage lifecycle | Desktop protocol fixtures; approved PAKE library |
| 3 | `desktop-agent` + `browser-extension` | Shared IPC v1 schema/golden transcript fixtures under `contracts/` | ASPS-611 contract conventions |
| 4 | `security` | Design + implementation review of PAKE dependency, storage ACLs, transcript, error/log redaction and release defaults | implementation PRs |
| 5 | `qa` | Independent negative/integration acceptance gate | both unit gates pass |

Do not modify Backend authentication in this Jira. Coordinate the ASPS-611
envelope implementation so `schemaVersion` negotiation and envelope validation
are not duplicated inconsistently. Coordinate ASPS-621 before enabling any
protection action that depends on a browser tab.

## Test plan / executable acceptance criteria

### Unit and contract tests

1. Python and Jest PAKE test vectors interoperate; same code succeeds and a
   wrong code, altered transcript, expired/reused code or wrong Origin fails.
2. Both codecs derive identical directional session keys from fixed fixtures;
   distinct directions/nonces/session IDs yield distinct keys.
3. Valid client/server proofs authenticate exactly once; reflection (client
   proof used as server proof), missing proof and repeated proof fail.
4. MAC covers every wrapper property: mutate envelope, `sessionId`, direction,
   sequence or version and require rejection before the domain handler.
5. Sequence tests reject duplicate, old, gap, decimal overflow and reconnect
   reuse; a new session starts at one and cannot accept an old-session frame.
6. Origin and subprotocol tests reject absent, `null`, look-alike, website and
   unconfigured extension origins. Tests prove raw clients cannot invoke
   `ExtensionHandler` before authentication.
7. Key-store tests prove no plaintext fallback, reset/re-pair invalidates old
   sessions, corruption fails closed, and logs/telemetry never include code,
   pair key, proof, MAC, token or full URL.
8. v1 schema negotiation selects only a common major; unsupported/malformed
   versions close without dispatch. Production config rejects legacy-v0 mode.

### Integration / adversarial acceptance tests

1. Launch Desktop and the real Extension test harness; pair through the PAKE
   flow, reconnect, and complete one `url_scan.request`/result v1 round trip.
2. Bind a fake listener on the first scanned port. Extension must not send an
   operational message or accept a result; it continues only after authenticating
   Desktop on a valid port.
3. Connect a raw local WebSocket client with a forged Origin. It must not
   reach `url_check`, `user_auth`, tab-data or broadcast subscription.
4. Capture a valid operational frame and replay it, reorder it, alter one byte,
   use it from another session, or swap direction. Each test closes/rejects and
   verifies no Backend request/protection action occurs.
5. Connect a second paired/unpaired client. Desktop results and remote-access
   state are delivered only to the owning authenticated pair; no all-client
   broadcast occurs.
6. Run ASPS-611's concurrent/out-of-order fixtures over an authenticated IPC
   session. The correct `requestId` and unchanged originating tab context are
   preserved; no legacy/unauthenticated traffic can perform tab-specific action.
7. Exercise rotation/reset, reinstall/profile-loss and service-worker restart:
   reconnect requires mutual authentication; stale keys do not work; user sees
   a deterministic re-pair state.

Pass requires all tests above, no production configuration enabling legacy
IPC, an independent Security review, and QA PASS after relevant Python/Jest
gates. This ADR alone is design complete, not implementation complete.

## Consequences and risks

- Positive: connection identity, integrity, replay defense and routing become
  explicit, testable and compatible with ASPS-611 correlation semantics.
- Cost: a vetted cross-language PAKE dependency, pairing UX, secure-store
  integration, deterministic test fixtures and coordinated Desktop/Extension
  rollout.
- Risk: PAKE/library interoperability. Mitigate with fixed test vectors,
  dependency security review and no custom crypto.
- Risk: user friction after install/reset. Mitigate with a short explicit tray
  flow and clear disconnected/re-pair UX, never silent downgrade.
- Risk: `ws://` still has no transport confidentiality. Payload integrity and
  endpoint authentication are solved here; native messaging is the preferred
  follow-up to remove local TCP exposure.

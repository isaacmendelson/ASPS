# ASPS-610 — Handoff

## Task identity

- Jira: ASPS-610
- Exact title: `[CODE REVIEW] Implement mutually authenticated Desktop-Extension IPC`
- Phase: Security dependency/design gate
- Status: BLOCKED pending PAKE dependency/protocol amendment and cross-runtime spike
- Date: 2026-07-28

## Scope and evidence reviewed

- Read mandatory project context, CEO operating memory and available architecture
  sources. `.Codex/team/CHARTER.md`, `.Codex/architecture/`, `.Codex/rules/`
  and an architect-specific charter are not present in this checkout.
- Jira connector/plugin is not installed in this session, so the remote Jira
  issue itself was not read or changed. The exact title/scope came from the
  root assignment and the current top-level code-review evidence.
- Reviewed `docs/code-reviews/ASPS_TOP_LEVEL_CODE_REVIEW_2026-07-28.md`,
  `docs/task-memory/ASPS_TOP_LEVEL_CODE_REVIEW_HANDOFF.md`,
  `docs/system-specifications/ASPS_System_Specification.md`,
  `docs/system-specifications/DESKTOP_AGENT_FEATURES.md`, `ARCHITECTURE.md`,
  ADR-001 / ASPS-611, and current Desktop/Extension IPC code.
- Verified current code: Desktop binds `localhost` using raw `websockets.serve`,
  accepts every client and broadcasts to all; Extension accepts the first raw
  WebSocket open on a predictable port. No Origin, pairing, session integrity
  or replay protection exists.

## Completed work

- Authored ADR:
  `docs/architecture/decisions/ADR-002-ASPS-610-MUTUALLY-AUTHENTICATED-DESKTOP-EXTENSION-IPC.md`.
- No production code, configuration, Jira state or commit was changed.

## Decisions

- Production local WebSocket is loopback-only but localhost/Origin are not
  trusted identity. Exact Chrome extension Origin and `asps-ipc-v1` are
  mandatory defence-in-depth gates.
- Bootstrap uses user-mediated, expiring SPAKE2+ PAKE pairing with a vetted
  cross-language library; no static bearer bootstrap token and no custom crypto.
- Every reconnect uses mutual challenge/proof and fresh directional HMAC keys.
  Every operational v1 frame carries a session ID, strict direction/sequence
  and HMAC over RFC 8785 canonical JSON.
- Failed proof, MAC, sequence, origin, schema or session validation closes
  before callback/handler execution. Results route to one authenticated pair;
  raw all-client broadcast is removed.
- Pair keys use Desktop keyring/Credential Manager and extension-private local
  storage, have 90-day/user-reset/reinstall rotation and fail closed on loss.
- No production v0 unauthenticated fallback. An old extension is disconnected
  and guided to upgrade/pair; legacy IPC is test-only and prohibited in release.
- ASPS-611 owns the envelope/correlation contract; ASPS-610 authenticates the
  session that negotiates and carries it. ASPS-621 remains owner of final
  origin-tab enforcement. Native Messaging is recorded as a follow-up transport
  hardening direction, not silently added to this ticket.

## Changed files

- `docs/architecture/decisions/ADR-002-ASPS-610-MUTUALLY-AUTHENTICATED-DESKTOP-EXTENSION-IPC.md`
- `docs/task-memory/ASPS-610_HANDOFF.md`

## Verification

- Documentation/design phase only; no build or runtime test was required.
- `git status --short` showed substantial pre-existing concurrent work
  (ASPS-609/ASPS-611 and project-memory changes). It was preserved; no files
  outside the two listed design artifacts were modified by this task.

## Implementation order and owners

1. `desktop-agent`: own `apps/desktop/win/src/extension_server.py`, new pairing/
   session/key-store modules, `config.py`, lifecycle and pair-specific routing.
2. `browser-extension`: own `apps/extension/chrome/services/ConnectionService.js`,
   explicit pairing UX, extension storage and authenticated frame codec.
3. Both owners: shared IPC v1 fixture/transcript material coordinated with the
   frozen ASPS-611 contract.
4. `security`: review PAKE dependency, storage access, transcript/canonicalization,
   log redaction and release-default fail-closed behavior.
5. `qa`: independent adversarial integration review after Python/Jest unit gates.

## Dependencies and open decisions

- Need Security approval of a maintained PAKE library that works in Python 3.11
  and Chrome MV3/WebCrypto or a sanctioned audited adapter. Implementers must
  not hand-roll PAKE.
- Need installer/release owner to provide the stable production extension ID and
  ensure legacy-test flag cannot reach production packages.
- ASPS-611 envelope schema must remain the one application payload contract;
  ASPS-610 adds only IPC wrapper/handshake contract.
- Full same-Windows-user/profile compromise remains outside a raw WebSocket
  boundary; plan a separate Chrome Native Messaging installer/lifecycle ADR.

## Exact continuation point

Root/CEO and Architect accept or reject the Security dependency amendment in
ADR-002. Do not assign production implementation yet. If accepted, run a
time-boxed dependency spike using the pinned `opaque-ke` shared Rust core,
compiled as PyO3 `abi3-py311` and packaged MV3 WebAssembly. The spike must pass
RFC 9807 Appendix C plus the ASPS vectors below on both targets. Return the
artifacts and dependency/SBOM evidence to Security; only a subsequent PASS may
start Desktop/Extension production implementation. Do not enable a
plaintext/legacy production fallback.

## Security dependency/design gate — 2026-07-28

### Result

- **BLOCKED** — trust model and fail-closed cutover are sound, but no maintained,
  independently audited SPAKE2+ dependency pair was found for Python 3.11 and
  Chrome MV3.
- The Python `spake2` package is balanced SPAKE2 rather than SPAKE2+. Candidate
  SPAKE2+ Python/JavaScript packages are unaudited or pre-1.0; the JavaScript
  candidate explicitly warns against production use before an independent
  audit. WebCrypto does not expose the point arithmetic needed to bridge this.
- Sanctioned direction: RFC 9807 OPAQUE using one pinned `opaque-ke` `4.0.1`
  Rust core, compiled from the same revision into a PyO3 `abi3-py311` wheel and
  packaged `wasm-bindgen` MV3 module. ASPS may own thin serialization/state
  adapters but no PAKE, curve, hash, KDF, MAC or RNG implementation.
- The 2021 NCC Group review covered an earlier `opaque-ke` release; the adapter
  and security-relevant changes through 4.0.1 still require targeted review.
  This direction is conditional on the spike and does not yet approve a
  production dependency.

### Blocking design corrections

1. Replace the circular `pairId` transcript binding with a pre-PAKE
   `pairingAttemptId`; return the final random `pairId` only in an authenticated
   finish message.
2. Freeze fixed-width binary PAKE identifiers/context and exact mapping of
   protocol version, `chrome.runtime.id`, verified Origin, Desktop stable ID,
   server port and UTC expiry. Do not use JSON/timestamps/Unicode
   canonicalization for PAKE context.
3. Prefer a fixed positional operational frame MACed over exact envelope UTF-8
   bytes. If RFC 8785 remains, reject duplicates, non-I-JSON, lone surrogates
   and negative zero before ordinary parsing, and pin both canonicalizers.
4. Set `chrome.storage.local` to `TRUSTED_CONTEXTS` before secret storage and
   move existing content-script storage reads behind narrowly allowlisted
   service-worker messages.
5. Explicitly require the Windows Credential Manager keyring backend and abort
   on null/fail/third-party/read-back failure. Existing token plaintext fallback
   is evidence that importing `keyring` alone is not a safe predicate.
6. Freeze an ASCII/checksummed pairing-code format with at least 30 bits of
   entropy, three guesses per single-use attempt, ten-minute expiry and no
   silent pairing.
7. Reset/compromise/profile or device identity change revokes immediately with
   no drain. Rotation is atomic and never accepts an old key for a new session.
8. Use allowlist-only structured audit events and fail closed on every
   dependency, storage, parse, proof, replay, schema and state error.

### Required dependency spike and test vectors

Dependencies/artifacts:

- exact `opaque-ke = 4.0.1` crate and transitive versions/checksums locked;
- Rust toolchain version and both native/WASM build commands frozen;
- PyO3 `abi3-py311` wheel for Windows x64; packaged `wasm-bindgen` artifact for
  the MV3 service worker with no remote code;
- Cargo SBOM/advisory result, source-to-artifact hashes and license inventory.

Vectors/tests required on both Python and Chrome:

1. All RFC 9807 Appendix C fake and real vectors for the selected OPAQUE suite.
2. One frozen ASPS success fixture containing deterministic test-only RNG,
   code, attempt ID, extension ID, Desktop ID, port and expiry; assert identical
   serialized context, protocol messages, session secret and final pair-key
   derivation byte-for-byte.
3. Mutate each context field independently; wrong code/Origin/extension ID,
   expired/reused attempt, invalid group element, truncated/oversized message,
   swapped role and altered confirmation must fail with no committed pair.
4. Fixed session fixtures for client/server proof separation, directional keys,
   exact-envelope frame MAC, sequences `1` and `2`, and failure on mutation,
   reflection, replay, gap, overflow, cross-session and cross-direction use.
5. Storage fixtures: null/fail keyring, write/read mismatch, corrupt record,
   content-script read/write/clear/enumerate attempts, service-worker restart,
   atomic rotation and immediate reset/revocation.
6. Log-capture fixtures that inject every sensitive field and exception path,
   then assert no code, key, PAKE state/share/confirmation, proof, MAC, token,
   email, full URL or frame appears in stdout, stderr, logs or telemetry.

# ASPS-610 — Handoff

## Task identity

- Jira: ASPS-610
- Exact title: `[CODE REVIEW] Implement mutually authenticated Desktop-Extension IPC`
- Phase: Architecture/design only
- Status: Design complete; awaiting root/CEO review and implementation assignment
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

Root/CEO reviews ADR-002. On approval, assign coordinated Desktop and Extension
implementation with the ADR acceptance tests verbatim, then request Security
review before QA. Do not enable a plaintext/legacy production fallback.

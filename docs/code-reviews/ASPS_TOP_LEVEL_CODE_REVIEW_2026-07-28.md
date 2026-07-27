# ASPS Top-Level Code Review

- Date: 2026-07-28
- Scope: `ASPSBackend14_J`, `Analyzers/basic-url-analyzer`, `apps/desktop/win`, `apps/extension/chrome`
- Review levels: architecture and code quality
- Review type: evidence-based, read-only review of the current working tree

## Executive summary

The system contains substantial implemented functionality, but the end-to-end protection path is not yet dependable as a security product. The highest risks are concentrated at trust boundaries and cross-component contracts rather than in isolated algorithms.

The review found:

- 3 critical architecture/security findings.
- 21 high-severity architecture findings.
- Multiple message-contract and correlation defects that can convert a valid analysis into a result applied to the wrong tab, an action that is never executed, or a failure interpreted as safe.
- Buildable/syntax-valid code in all reviewed stacks, but materially unhealthy automated-test baselines.
- Significant specification drift and no dedicated complete specification for the Analyzer or Chrome Extension.

The most urgent remediation theme is to establish authenticated, versioned and correlated contracts across:

1. Backend ↔ Analyzer
2. Backend ↔ Desktop
3. Desktop ↔ Extension

Until those boundaries are corrected, improvements inside an individual component cannot guarantee correct end-to-end protection.

## Scope and method

The review used these baselines:

- `docs/system-specifications/ASPS_System_Specification.md` — canonical as-built baseline.
- `docs/system-specifications/ASPS_Unified_System_Requirements_2026-07-15.md` — target requirements.
- `docs/system-specifications/DESKTOP_AGENT_FEATURES.md` — Desktop component baseline.
- `ARCHITECTURE.md` and `docs/ASPS_DATA_FLOW.md` — architecture and flow references.

Source code and runtime evidence were treated as authoritative where documentation conflicted. Planned requirements were not counted as implemented behavior.

No source code or runtime configuration was changed. No dependencies were installed and no running user processes were stopped.

## System-wide failure chains

### 1. URL analysis can fail open

1. The Analyzer returns an error as a string, omits `success`, and assigns score 0.
2. The C# receiver defaults `Success` to true and expects a differently shaped error.
3. The Extension uses score 0 as an error in current UI code, while stale fallback logic treats low scores as most dangerous.

Result: the same failure can be interpreted as success, safe, error or block depending on which path consumes it.

### 2. A correct URL result can protect the wrong tab

1. The Extension sends a `tabId`, but has no end-to-end request ID.
2. Desktop keeps one global pending URL.
3. Extension resolves pending scans by domain and applies results to the currently active tab.
4. ProtectionService independently selects the active tab at execution time.

Result: tab switching, concurrent scans or out-of-order notifications can show or enforce a result on the wrong page.

### 3. Protective actions can disappear between Backend and endpoint

1. Backend serializes the action target as `SubjectKey`.
2. Desktop reads `Subject`.
3. Backend notifications use transient PUB/SUB without acknowledgement or replay.
4. The Extension queue is also at-most-once and can lose messages during flush.
5. “Close session” reaches Desktop, but Desktop has no effective remote-session disconnect implementation.

Result: the Backend can decide on the correct action without the endpoint executing it.

### 4. Local trust boundary is open in both directions

1. The Extension probes predictable localhost WebSocket ports and trusts the first listener.
2. Desktop accepts every WebSocket client without Origin or identity validation.
3. Operational messages are accepted before any authenticated handshake.

Result: a local impostor can impersonate either side and inject scan, danger, tracked-domain or protection messages.

### 5. Security state is volatile

1. Backend notification delivery is transient.
2. Desktop lifecycle is split across daemon threads and asyncio tasks.
3. Extension danger/tracking state is held in MV3 service-worker memory.
4. Reconnect paths do not provide authoritative state replay.

Result: restart, suspension or short disconnects can silently reduce protection until fresh state happens to arrive.

## Cross-component contract matrix

| Boundary | Intended contract | Observed defect | Severity |
|---|---|---|---|
| Backend → Analyzer | Structured, explicit success/error analysis | Python error shape mismatches C# DTO and can default to success | High |
| Backend → Analyzer | Bounded process execution | Backend timeout is shorter than scraper path; Chromium children may survive | High |
| Backend → Analyzer | Safe hostile-URL processing | No SSRF boundary; Chromium isolation deliberately weakened | Critical |
| Backend → Desktop | Authenticated encrypted device channel | First token request may be plaintext; absent key silently downgrades | High |
| Backend → Desktop | Reliable protection delivery | PUB/SUB has no ACK, replay or durable cursor | Medium |
| Backend → Desktop | Typed ProtectiveAction | `SubjectKey` versus `Subject` mismatch | High |
| Desktop → Extension | Trusted local IPC | No mutual authentication, Origin check or pairing | Critical |
| Extension → Desktop → Backend | Correlated URL scan | Global pending URL/domain keys; no end-to-end request ID | High |
| Desktop ↔ Extension | Browser-tabs request/response | Desktop blocks receive loop while awaiting its response | High |
| Desktop ↔ Extension | Close remote session | UI message exists; enforcement is not implemented | High |
| Backend/Desktop → Extension | Durable danger/tracking state | MV3 in-memory state is not authoritatively restored | High |

---

## 1. ASPSBackend — complete solution

### Architecture assessment

The solution has recognizable layers (`Common`, `Interface`, `Business`, host, WebApi and tests), CQRS concepts, EF persistence and real-time messaging. However, two parallel messaging paths, unsafe boundary defaults and lifetime violations prevent the layer boundaries from providing reliable isolation.

#### Critical

1. **Unauthenticated externally bound CQRS gateway with polymorphic deserialization**
   - `Business/Messaging/CQRSGateway.cs:35` defaults to `tcp://*:5556`.
   - The channel has no CURVE authentication.
   - Multiple paths use `TypeNameHandling.Auto`.
   - Impact: reachable peers can invoke administrative commands/queries; polymorphic deserialization increases exploitability.
   - Remediation: bind to a private interface, require CURVE/mTLS-equivalent authentication, enforce command authorization, remove `TypeNameHandling`, and use explicit DTO allowlists.

#### High

2. **Invalid DI lifetime graph is hidden**
   - Host disables scope/build validation.
   - A singleton persistence actor captures a scoped repository/DbContext.
   - Remediation: enable validation and create scopes per actor operation, or redesign the actor around an `IDbContextFactory`.

3. **User Risk Score runtime dependencies are incomplete and failures are swallowed**
   - Service construction/registration does not consistently provide required dependencies.
   - Broad catches allow the host to continue without the feature.
   - Remediation: make startup validation fail fast and add a composition-root test.

4. **WebApi authorization boundaries are incomplete**
   - REST/hub paths do not consistently require authenticated/authorized users.
   - Anonymous SignalR callers can be treated as administrators and subscribe to arbitrary groups.
   - Remediation: require authorization by default, derive groups from server-side claims, and add negative authorization tests.

5. **Notification delivery is volatile**
   - Direct ZMQ PUB provides no acknowledgement, replay or durable outbox.
   - Remediation: introduce persisted notification IDs, consumer acknowledgements and replay/cursor semantics.

6. **`ASView` contains sync-over-async and fire-and-forget behavior**
   - Impact: thread starvation, unobserved exceptions and nondeterministic completion.
   - Remediation: make the call chain async end-to-end and observe all background work.

7. **Two parallel CQRS transports create inconsistent policy**
   - Commands can traverse different dispatch/serialization/authorization paths.
   - Remediation: select one supported gateway and retire or isolate the other.

8. **Simulation bypasses the production pipeline**
   - Impact: simulation results do not prove production behavior.
   - Remediation: inject simulated inputs at the normal intake boundary.

#### Medium

9. Forwarded headers trust is too broad.
10. Database migration behavior is development-only and deployment/schema startup is fragile.

### Code-quality assessment

- `CQRSGateway.cs` is approximately 1,180 lines of manual dispatch and repeated serialization.
- The solution build emitted 309 warnings, including nullability, hidden members, duplicate types/usings and EF assembly conflicts.
- EF Core Relational versions conflict transitively (`7.0.2` and `7.0.20`).
- Newtonsoft.Json versions drift between production and tests.
- Structured logging, `Console.WriteLine`, debug dumps and swallowed exceptions coexist.
- Disabled/stale tests and duplicate test IDs reduce confidence.

### Verification

- Build: no `CS####` compiler errors observed.
- Final build result: failed on DLL copy locks (`MSB3027/MSB3021`) from running ASPSBackend, WebApi and Visual Studio processes.
- Tests: 1,351 passed, 39 failed, 3 skipped; 1,393 total.
- Verdict: **architecture requires remediation before production exposure; test baseline is not merge-clean.**

---

## 2. Analyzers — `basic-url-analyzer`

### Architecture assessment

The Analyzer combines rules, ML, reputation, browser scraping and classification into a usable CLI path. Its primary risks are unsafe hostile-content execution, an ambiguous scoring policy and an unversioned interop contract.

#### Critical

1. **SSRF exposure combined with disabled browser isolation**
   - URL validation permits loopback, private, link-local, metadata and DNS-rebinding destinations.
   - Redirect targets are not revalidated.
   - Chromium runs with `--no-sandbox`, disabled web security/site isolation and ignored TLS errors.
   - Remediation: resolve and validate every destination/redirect, block non-public address ranges, use an isolated sandboxed worker/network namespace, restore browser security flags and enforce egress policy.

#### High

2. **Error contract defaults to success in C#**
   - Python emits a string `error`, omits `success`, and returns score 0.
   - C# expects a structured error and defaults success to true.
   - Remediation: define a versioned JSON schema with explicit `success`, nullable result and structured error; add cross-language fixtures.

3. **Timeout and process ownership are inconsistent**
   - Scraper execution can exceed the Backend's 30-second timeout.
   - Backend kills only the parent Python process.
   - Remediation: one deadline propagated through all operations and process-tree/job-object termination.

4. **Risk score is compressed and overridden by hard-coded age/reputation rules**
   - High ML values are capped and established-domain heuristics can suppress risk.
   - Remediation: version/configure the scoring policy, retain component scores, calibrate against a dataset and expose explanations.

#### Medium

5. Reputation adjustment is emitted but not consistently applied.
6. Dormant FastAPI surface allows open CORS, has no auth/rate limiting and blocks its async event loop.
7. Cache configuration is partly ignored and file persistence is non-atomic/process-unsafe.
8. Successful Python output is lossy when mapped into C# DTOs.
9. Dependencies are lower-bound-only and not reproducibly locked.

### Code-quality assessment

- `core/analyzer.py` is a 768-line orchestrator with too many responsibilities.
- `analyze.py` mixes machine JSON and human CLI output; `--whois-only` has no execution path.
- Modules mutate `sys.path`.
- Broad exception handling converts configuration/programming errors into defaults.
- Generated/debug artifacts are stored with source.
- API and CLI schemas diverge.
- Tests contain stale opposite risk semantics.
- No cross-language contract test exercises actual C# DTO deserialization.

### Verification

- Python `compileall`: passed for all sources.
- Static inventory: 240 pytest tests.
- Pytest could not run: no project environment and no pytest/dependency set in the bundled runtime.
- Verdict: **unsafe for direct processing of attacker-controlled URLs until isolation and SSRF controls are implemented.**

---

## 3. Apps — `desktop-win`

### Architecture assessment

Desktop performs substantial orchestration across ZMQ, browser integration, monitoring and endpoint UI. Its primary problems are the unauthenticated local boundary, broken monitor flows and non-correlated state.

#### Critical

1. **Unauthenticated localhost WebSocket server**
   - No Origin allowlist, pairing secret, extension identity or protocol authentication.
   - Arbitrary JSON is forwarded to operational handlers.
   - Remediation: bind a per-install authenticated channel, validate Origin/extension ID, perform challenge-response and reject messages until paired.

#### High

2. **Browser-tabs callback blocks its own response loop**
   - Client-connect callback is awaited before the receive loop starts.
   - Callback requests tabs and waits for responses handled only by that receive loop.
   - Remediation: start receive processing first and schedule the callback independently.

3. **Browser-history monitor skips every new entry**
   - Discovery marks entries seen; the caller immediately skips seen URLs.
   - Remediation: separate discovered, queued, acknowledged and failed states.

4. **ProtectiveAction target schema mismatch**
   - Backend sends `SubjectKey`; Desktop reads `Subject`.
   - Remediation: consume one versioned DTO and reject incompatible payloads explicitly.

5. **CURVE bootstrap can downgrade to plaintext**
   - First RequestToken may be unencrypted; missing key is interpreted as disabled security.
   - Remediation: ship/pin a server trust key or use an authenticated enrollment mechanism; never silently downgrade.

6. **One global pending URL is used for asynchronous results**
   - Remediation: carry a request ID, tab ID and canonical URL through every hop.

#### Medium

7. Notifications have no ACK/replay/cursor.
8. Remote-session disconnect is not implemented.
9. Asyncio and unmanaged daemon-thread lifecycle is nondeterministic.
10. Runtime version `0.1.1.1` conflicts with installer/docs version `0.0.0.3`; dependencies are not locked.

### Code-quality assessment

- `remote_monitor.py` is approximately 2,280 lines and overlaps with `detection/`.
- `monitor_service.py` owns too many unrelated workflows.
- Broad/bare exception handling frequently continues with empty security data.
- `print()` and logging are mixed extensively.
- Lazy service access starts background threads as a side effect.
- Only six test files/140 static tests cover a large operational surface.
- Generated build/release trees and diagnostics coexist with source.

### Verification

- Python files: 56 across 61 component files.
- Python `compileall`: passed.
- Static tests: 140.
- Pytest unavailable in the existing environment.
- Verdict: **core endpoint workflows are present, but several are currently unreachable or fail silently.**

---

## 4. Apps — `extension-chrome`

### Architecture assessment

The Extension implements MV3 state, scan UI, tracking, remote-access warnings and a local queue. The main protection path is undermined by untrusted IPC, incorrect result-to-tab association and volatile service-worker state.

#### Critical

1. **No authenticated identity on Extension↔Desktop IPC**
   - The Extension trusts the first successful listener on one of five predictable ports.
   - Remediation: use the same paired authenticated protocol required on Desktop.

#### High

2. **Result and protection target the active tab instead of the source tab**
   - Pending scans are keyed by domain.
   - Result storage and enforcement query the active tab at response time.
   - Remediation: preserve immutable `{requestId, tabId, frameId, url}` context and validate that the tab still displays the same URL before enforcement.

3. **MV3 restart loses danger/tracking state**
   - ImmediateDanger, remote-control, tab controls, sensitivity and navigation tracking are module variables.
   - Stored tracked domains are not rebuilt into the background navigation Map on initialization.
   - Remediation: persist authoritative state, restore it before handlers act, and request a full snapshot from Desktop after every reconnect/start.

4. **“Close session” does not close the session**
   - Extension forwards the request but Desktop cannot enforce it.
   - Remediation: do not report success until Desktop returns an explicit verified outcome.

5. **Stale fallback risk logic is inverted**
   - Low scores map to block and high scores to none, contrary to the canonical scale.
   - Remediation: delete local decision policy or generate it from one versioned shared contract.

#### Medium

6. Queue flush can lose all messages after the first unsent item.
7. Feedback sends browsing data directly to Google Apps Script and stores it locally without a clear retention/consent contract.
8. `<all_urls>`, cookies, tabs and navigation permissions require explicit least-privilege justification.
9. Keepalive relies on complex, browser-sensitive MV3 lifecycle behavior.
10. Message names and casing are decentralized across constants, literal maps and legacy handlers.

### Code-quality assessment

- `background.js` is approximately 1,500 lines with many responsibilities.
- Modular services coexist with duplicate legacy logic.
- Async handlers and storage writes are often fire-and-forget.
- Logs include URLs, email and security-session metadata.
- `tests/node_modules` is stored inside the component tree.
- Manifest version `0.0.1.4` differs from test package `0.0.0.2`.
- Test scripts use non-portable POSIX environment syntax on a Windows-oriented project.

### Verification

- `node --check`: passed for all project JavaScript outside `tests/node_modules`.
- Declared `npm test`: fails before Jest on Windows.
- Direct Jest: 99 passed, 140 failed; 3/12 suites passed.
- Failure classes: ESM/module mapping, missing `chrome.storage.session` mock and stale harness assumptions.
- Verdict: **syntax-valid but not test-clean; critical protection state and targeting are unreliable across normal MV3 operation.**

## Prioritized remediation plan

### P0 — block production exposure

1. Authenticate and authorize the CQRS gateway; remove unsafe polymorphic deserialization.
2. Isolate Analyzer execution and implement SSRF/redirect/egress controls.
3. Replace local Desktop↔Extension WebSocket trust with a paired authenticated protocol.
4. Introduce an end-to-end versioned envelope:
   - `schemaVersion`
   - `messageId`
   - `correlationId`
   - `requestId`
   - `deviceId`
   - `tabId`
   - `url`
   - explicit success/error discriminator
5. Fix result routing so a result can only affect its originating tab and unchanged URL.

### P1 — restore correct protection behavior

6. Align Analyzer success/error and ProtectiveAction schemas with generated contract tests.
7. Fix Desktop browser-history and browser-tabs receive-loop defects.
8. Persist/replay Backend notifications and restore Extension danger/tracking state on startup.
9. Implement verified remote-session termination or change the UI promise.
10. Enable DI validation and correct service lifetimes.
11. Secure WebApi/SignalR authorization and server-controlled group membership.

### P2 — establish engineering control

12. Make all four component test suites reproducible and green.
13. Lock Python and JavaScript dependencies; align .NET package versions.
14. Split oversized orchestration files and remove parallel legacy paths.
15. Replace broad exception swallowing and `print()` diagnostics with structured, privacy-aware telemetry.
16. Create dedicated Analyzer and Extension specifications plus one cross-component schema repository.
17. Add end-to-end tests for:
    - Analyzer failure and timeout
    - simultaneous scans in multiple tabs
    - tab switching before result
    - Desktop/Extension restart during ImmediateDanger
    - notification disconnect/replay
    - ProtectiveAction execution
    - unauthorized CQRS/WebSocket/SignalR clients

## Final disposition

The reviewed code should not be considered production-ready for hostile network input or dependable endpoint enforcement in its current state.

Recommended release gate:

- All P0 items resolved.
- Cross-language and cross-process contract suite passing.
- No unauthenticated externally reachable management/message endpoints.
- Full test baselines reproducible and green, or every remaining failure explicitly waived with owner and expiry.
- End-to-end evidence that a URL analyzed in one tab produces the correct action in that same tab after reconnect/restart scenarios.


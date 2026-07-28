# ASPS-611 — Handoff

## Task identity

- Jira: ASPS-611
- Exact title: `[CODE REVIEW] Introduce a versioned end-to-end message envelope and correlation IDs`
- Phase: Architecture/design
- Status: Design complete; awaiting root/CEO review
- Date: 2026-07-28

## Jira requirement

- Source: ASPS Top-Level Code Review dated 2026-07-28.
- Affected components: all runtime components.
- Finding: messages lack one immutable identity across Backend, Analyzer,
  Desktop, and Extension.
- Required fields: `schemaVersion`, `messageId`, `correlationId`, `requestId`,
  `deviceId`, `tabId`, canonical URL, and explicit success/error.
- Acceptance: generated/shared schemas exist; incompatible versions fail
  explicitly; concurrent/out-of-order contract tests pass.

## Completed work

- Read `docs/PROJECT_CONTEXT.md`.
- Read the matching top-level code-review handoff and report.
- Read `ARCHITECTURE.md` and `docs/ASPS_DATA_FLOW.md`.
- Inspected current message/correlation paths in Backend, Desktop, Extension,
  and Analyzer.
- Authored the proposed cross-component design and ADR:
  `docs/architecture/decisions/ADR-001-ASPS-611-VERSIONED-MESSAGE-ENVELOPE.md`.
- No production code or configuration was changed.
- No commit was created.

## Decisions

- One lower-camel-case UTF-8 JSON envelope, sourced from versioned JSON Schema.
- Generated C#, Python, and JavaScript artifacts are checked in and guarded by a
  schema hash.
- `correlationId` identifies the end-to-end workflow, `requestId` the originating
  request, and `messageId` one emitted wire message/deduplication unit.
- Extension creates browser scan request/correlation IDs; Desktop enriches the
  stable device ID; all downstream components echo immutable context.
- Canonical URL is part of immutable context and is independently verified at
  trust boundaries.
- Responses use a strict success/error discriminated union; errors never contain
  a risk score.
- Unsupported major versions and malformed/inconsistent envelopes fail
  explicitly before domain handling.
- Migration is dual-read/single-write with an explicit legacy flag and negotiated
  capability; legacy-ambiguous messages cannot perform tab-targeted enforcement.

## Files changed

- `docs/architecture/decisions/ADR-001-ASPS-611-VERSIONED-MESSAGE-ENVELOPE.md`
- `docs/task-memory/ASPS-611_HANDOFF.md`

## Verification

- Documentation/design phase only; no build or runtime tests required.
- The ADR maps every Jira-required field to validation semantics and contains the
  generated schema, compatibility, migration, and concurrent/out-of-order test
  plan.

## Implementation order

1. `backend`: freeze shared schemas/generation and implement Backend model,
   validation, persistence, and adapters.
2. In parallel after schema freeze:
   - `analyzer-ai`: Analyzer v1 CLI contract.
   - `desktop-agent`: Python bindings and Backend-facing adapter.
3. `desktop-agent`: v1 local handshake support.
4. `browser-extension`: JS bindings, v1 switch, and request tracking.
5. Each implementation owner runs all relevant unit tests and records exact
   commands/counts.
6. `qa`: independent cross-component acceptance review only after unit gates pass.

## Dependencies

- ASPS-612: Analyzer success/error payload.
- ASPS-610: authenticated Desktop↔Extension handshake/version negotiation.
- ASPS-618: ProtectiveAction payload alignment.
- ASPS-619: secure Backend↔Desktop enrollment/capability advertisement.
- ASPS-620: durable delivery/ACK keyed by `messageId`.
- ASPS-621: origin-tab enforcement using immutable request context.

## Exact continuation point

Root/CEO reviews and accepts or amends the proposed ADR. After approval, assign
the schema/package foundation to `backend`. Do not begin runtime implementation
before the envelope field semantics and canonical URL rules are frozen.

## Backend implementation — 2026-07-28

- Added the v1 source schema at `contracts/messaging/v1/envelope.schema.json`.
- Added checked-in C# generated-style lower-camel-case envelope models and the source-schema SHA-256 guard at `ASPSBackend14_J/Common/Generated/Messaging/V1/MessageEnvelope.cs`.
- Added strict pre-domain validation for version, UUID identity, source/type, UTC timestamp, canonical URL, tab ID, outcome union, and immutable response echo validation at `ASPSBackend14_J/Common/Generated/Messaging/V1/MessageEnvelopeValidator.cs`.
- Added focused contract unit tests at `ASPSBackend14_J/ASPS.Tests/Common/Messaging/MessageEnvelopeValidatorTests.cs`.
- Deliberately did not change legacy runtime transport or persistence yet: the current Desktop/Analyzer emit legacy alert shapes, and the ADR requires dual-read/single-write adapters only after their v1 bindings are available.

### Verification

- `dotnet build Common/Common.csproj -c Debug --no-restore --nologo` — passed, 0 warnings, 0 errors.
- `dotnet test ASPS.Tests/ASPS.Tests.csproj -c Debug --no-build --filter FullyQualifiedName~MessageEnvelopeValidatorTests --nologo` — passed: 7/7, 0 failed, 0 skipped.
- `git diff --check` — no whitespace errors in the task changes.

### Continuation

- Root should arrange independent QA for the contract foundation. Runtime ingress/persistence/adapters remain a coordinated follow-up once ASPS-612/610/619 provide their v1 boundary contracts.

## QA remediation implementation — 2026-07-28

### Status

- PRE-QA READY. No commit created and Jira not moved.
- Existing Analyzer/ASPS-609 changes were not touched.

### Acceptance checklist

- [x] One strict, versioned lower-camel JSON envelope schema.
- [x] Deterministic C#, Python, and JavaScript bindings, fixtures, generator, and CI drift check.
- [x] Explicit rejection of unsupported versions, missing fields, default `sentAt`, and undefined payload.
- [x] Backend dual-read legacy/v1 ingress and dual-write-compatible notification egress.
- [x] Immutable identity propagated through alert, domain event, notification, and persistence.
- [x] Desktop and Extension runtime adapters validate immutable response context.
- [x] Concurrent same-URL and out-of-order responses resolve by `requestId`.
- [x] Additive EF migration generated and SQL reviewed; database update deliberately not applied.

### Runtime and persistence changes

- Added v1 handling to Backend `RealTimeAlertListener` and `NotificationPublisher`.
- Propagated `MessageIdentityV1` through `DeviceMessage`, `AnalysisResultReceived`,
  `UDAnalysis`, `NotificationPublisherActor`, and `AlertPersistenceActor`.
- Added nullable envelope identity fields and indexes to `DeviceAlerts`, EF mapping,
  model snapshot, and migration `20260728190000_ASPS611_AddMessageEnvelopeIdentity`.
- Added Desktop ingress/ZMQ adapters and Extension request tracking keyed by
  `requestId`, while retaining legacy behavior.
- Added golden fixtures, generated bindings, contract tests, and drift workflow.

### TDD evidence

- Foundation written before the TDD instruction is recorded as foundation; no
  retroactive Red was invented.
- Request tracking Red: `py -3 -m unittest tests.contracts.test_messaging_v1 -v`
  ran 5 tests with 2 errors because `RequestTracker` did not exist. Green after
  implementation: 5 passed.
- Runtime identity Red: test-project build failed with 5 `CS####` errors for the
  missing identity type/property/factory. Green: Business build completed with
  0 errors.
- Persistence Red: test-project build failed with 5 `CS0117` errors for missing
  entity fields. Green focused envelope/identity tests: 17 passed.
- Publisher dual-path Red: 17 tests ran, 4 failed (three legacy overload
  regressions plus one stale pre-existing handled-event count). Green after
  conditional legacy/v1 dispatch and characterization correction: 17 passed.

### Final verification

- `dotnet test ASPS.Tests/ASPS.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~MessageEnvelopeValidatorTests|FullyQualifiedName~MessageIdentityPropagationTests|FullyQualifiedName~NotificationPublisherActorTests" --nologo`
  — 34 passed, 0 failed, 0 skipped.
- `dotnet build ASPSBackend.sln -c Debug --no-restore --nologo`
  — passed, 0 errors, 29 existing warnings.
- `py -3 scripts/generate_messaging_contracts.py --check` — passed; no drift.
- `py -3 -m unittest tests.contracts.test_messaging_v1 -v`
  — 5 passed, 0 failed.
- Python `py_compile` for generator, generated binding, and changed Desktop
  modules — passed.
- `node --check` for generated JS binding and Extension `ScanService.js` — passed.
- `git diff --check` — passed; line-ending notices only.
- Migration SQL reviewed at `.qa-artifacts/ASPS-611-migration.sql`: five nullable
  columns, one unique `MessageId` index, correlation/request indexes, no
  destructive statements. Migration is pending and was not applied.

### Exact continuation point

Root/CEO independently inspects the reported files and sends the complete change
set plus the evidence above to an independent QA agent. Do not commit or close
ASPS-611 until documented QA PASS.

## QA FAIL remediation round 2 — 2026-07-28

### QA verdict and status

- QA returned FAIL with six findings: broken Extension module structure,
  missing Analyzer v1 contract, missing idempotent deduplication, incomplete
  compatibility/rollout negotiation, incomplete five-schema generation/CI
  bundle, and insufficient real runtime acceptance coverage.
- Status: implementation in progress; not PRE-QA READY.

### Phase 1 — Extension ScanService module repair

Acceptance checklist:

- [x] v1 result handling executes inside `ScanService.handleResult`.
- [x] malformed/stale/mismatched envelopes fail before state side effects.
- [x] a matching result resolves the pending scan by immutable `requestId`.
- [x] production module passes Node syntax loading and focused Jest execution.

TDD evidence:

- Red module load:
  `node --input-type=module -e "... await import('./services/ScanService.js')"`
  failed with `SyntaxError: Illegal return statement` at the orphaned v1 block.
- Red Jest environment/focused behavior: the existing command first exposed the
  Windows-incompatible npm environment assignment; direct Jest then failed to
  parse the production file as ESM. After adding the component package boundary,
  the focused test ran and failed because `chrome.tabs.query` had no Promise
  result, proving the actual `handleResult` path was reached.
- Green:
  `node --check ../services/ScanService.js` passed.
- Green:
  `$env:NODE_OPTIONS='--experimental-vm-modules'; npx jest --runInBand unit/services/ScanService.messaging-v1.test.js`
  passed: 1 suite, 1 test, 0 failed, 0 skipped.

Changed in this phase:

- `apps/extension/chrome/services/ScanService.js`
- `apps/extension/chrome/package.json`
- `apps/extension/chrome/tests/unit/services/ScanService.messaging-v1.test.js`
- `docs/task-memory/ASPS-611_HANDOFF.md`

Refactor:

- Expanded the inline result conversion for readable success/error branches;
  no behavior beyond the v1 placement repair was added.

Exact continuation point:

- Await Mode B approval. Next phase is QA finding 2: add the generated v1
  binding and full stdin/stdout contract to Basic URL Analyzer, preserving and
  integrating concurrent ASPS-609 security changes.

### Phases 2–6 — remaining QA findings

Status: PRE-QA READY for independent re-review. No commit or Jira transition.

Analyzer contract:

- Added the fourth generated binding under
  `Analyzers/basic-url-analyzer/generated/messaging/v1/`.
- Added `v1_stdio.py` and `analyze.py --contract-version 1`.
- stdin accepts one strict request envelope; stdout emits exactly one result or
  error envelope. Diagnostics go only to stderr. Immutable identity/context is
  echoed and errors contain no score.
- Preserved all concurrent ASPS-609 SSRF files and tests without reverting or
  rewriting them.

Idempotency:

- Added `MessageDeduplicator`, atomically claimed before Backend alert side
  effects.
- Retention is 15 minutes and capacity is bounded at 100,000 entries.
- The unique persisted `DeviceAlerts.MessageId` index remains the database and
  process-failover backstop.
- A same-`messageId` retry receives an accepted duplicate response without
  invoking domain handling again.

Compatibility and rollout:

- Major 1 accepts additive minor versions; unsupported majors fail explicitly.
- Added `MessagingCompatibility`, `SupportedSchemaMajors = [1]`,
  `MessagingCompatibilityOptions.AcceptLegacyV0`, and fail-closed negotiation.
- Legacy mode requires the explicit flag and exposes
  `CanPerformTabTargetedAction = false`.
- Desktop ping capability response now advertises
  `supportedSchemaMajors: [1]`.

Schema/generation/CI:

- Completed the five-schema bundle:
  `envelope`, `common`, `url-scan-request`, `url-scan-result`, and `error`.
- Generator now emits all four consumer bindings and one bundle hash.
- Generated-artifact manifest guards all bindings plus the C# validator and
  factory against drift.
- CI runs drift, cross-language fixtures, Analyzer pytest, Extension Node/Jest,
  and .NET messaging gates.

Runtime acceptance:

- Extension focused Jest executes a real `ScanService.handleResult` v1 result
  and resolves by `requestId`.
- Desktop runtime tests execute actual envelope enrichment/forwarding and reject
  a tampered Backend echo.
- Analyzer tests execute success/error stream behavior and a real
  `analyze.py --contract-version 1` subprocess protocol rejection with pure
  stdout.
- Backend tests execute bounded dedupe, retry behavior, minor/legacy
  negotiation, identity propagation, validator, and notification paths.
- Shared concurrent/out-of-order tests cover same-URL reverse delivery and
  stale immutable context.

#### Additional Red → Green evidence

- Analyzer Red: 5 tests failed with `ModuleNotFoundError: v1_stdio`; Green:
  initially 5/5, final 6/6 including the real CLI subprocess.
- Dedupe Red: test build failed with three `CS0246` errors for missing
  `MessageDeduplicator`; Green: 3/3.
- Negotiation Red: test build failed with nine missing-type errors; Green:
  combined validator/dedupe/negotiation slice 17/17.
- Extension module Red and Green are recorded in Phase 1 above.
- Schema and generated files are declarative/generated; conventional unit Red
  does not apply. The generator `--check`, bundle manifest, cross-language
  fixtures, and CI gates are the automated validation.

#### Final commands and results

- `node --check ../services/ScanService.js` — passed.
- `$env:NODE_OPTIONS='--experimental-vm-modules'; npx jest --runInBand unit/services/ScanService.messaging-v1.test.js`
  — 1 suite, 1 passed, 0 failed, 0 skipped.
- `py -3 scripts/generate_messaging_contracts.py --check` — passed.
- `py -3 -m unittest tests.contracts.test_messaging_v1 tests.contracts.test_desktop_v1_runtime -v`
  — 8 passed, 0 failed.
- Analyzer `.venv`:
  `python -m pytest tests/test_messaging_v1_stdio.py -q`
  — 6 passed, 0 failed.
- `dotnet test ... --filter "MessageEnvelopeValidatorTests|MessageIdentityPropagationTests|NotificationPublisherActorTests|MessageDeduplicatorTests|MessagingCompatibilityTests"`
  — 41 passed, 0 failed, 0 skipped.
- `dotnet build ASPSBackend.sln -c Debug --no-restore --nologo`
  — passed, 0 errors, 275 existing warnings.
- Python `py_compile` for generator, Analyzer adapter/binding, and changed
  Desktop modules — passed.

#### Exact continuation point

Root/CEO must inspect the implementation and request independent QA re-review
against all six FAIL findings. Do not commit or move ASPS-611 until QA PASS.

## QA FAIL remediation round 3 — runtime wiring

QA returned FAIL on four remaining runtime findings. Status after remediation:
PRE-QA READY for another independent review; no commit or Jira transition.

### Backend ↔ Analyzer v1 runtime

- Added `AnalyzerV1ProcessClient`; it launches the real Basic URL Analyzer with
  `analyze.py --contract-version 1`, writes one request envelope to stdin, reads
  one stdout envelope, validates the response and immutable echo, and rejects
  invalid source/type/error responses.
- `UDUrlAnalyzer` now uses this client instead of the legacy URL command-line
  and unwrapped JSON result path.
- Analyzer success envelopes now retain the complete analysis object while also
  exposing normalized score fields.
- Real subprocess test uses the installed Analyzer virtual environment and an
  SSRF-rejected loopback URL, proving the actual process boundary, structured
  error, and identity echo without external network access.

TDD:

- Red: test build failed with missing `AnalyzerV1ProcessClient` and
  `AnalyzerV1ProcessException`.
- Intermediate Red: real subprocess returned `protocol.invalid_sent_at`,
  exposing C#'s non-millisecond default timestamp serialization.
- Green: explicit millisecond UTC serialization; runtime slice passes.

### Compatibility in runtime paths

- `RealTimeAlertListener` reads `Messaging:AcceptLegacyV0`.
- Legacy ingress is rejected when disabled.
- When enabled, legacy input is tagged `LegacyAdapted` and its `TabId` is
  removed before domain/action handling, enforcing that legacy traffic cannot
  perform tab-targeted actions.
- `RegisterDevice` consumes `SupportedSchemaMajors`, performs negotiation, and
  returns the selected major plus Backend `supportedSchemaMajors`.
- Desktop ping advertises `[1]`.
- Runtime policy tests cover flag-off rejection and pre-action tab removal.

### Post-validation deduplication

- The listener claim was moved after envelope, payload, typed alert, and
  immutable-context validation. A malformed first attempt therefore cannot
  poison a corrected retry using the same `messageId`.
- Concurrent duplicate test executes 64 parallel claims and proves exactly one
  side-effect owner.
- Existing retention, expiry, and capacity tests remain green.

### Four-component runtime gates

- Extension Jest now runs two concurrent same-URL scans and delivers results in
  reverse order; each resolves its own `requestId`.
- Desktop runtime forwarding/tamper tests are included in CI.
- Backend↔Analyzer test runs the real Python subprocess.
- C# consumes and validates all three golden fixtures.
- Backend focused gate includes listener-adjacent dedupe, compatibility,
  propagation, publisher, validator, process, and fixture consumers.

### Final verification

- Extension Jest: 1 suite, 2 passed, 0 failed/skipped.
- Shared + Desktop Python unittest: 8 passed, 0 failed.
- Analyzer pytest: 6 passed, 0 failed.
- C# focused runtime/contract suite: 46 passed, 0 failed/skipped.
- `dotnet build ASPSBackend.sln -c Debug --no-restore --nologo`:
  0 errors; final incremental output contained 4 existing warnings.
- Generator `--check`, Node syntax, Python compile, and `git diff --check`:
  passed.

### Exact continuation point

Root/CEO verifies the runtime wiring and requests independent QA against only
the four round-3 findings. Do not commit or move Jira until QA PASS.

## QA Round 4 remediation (2026-07-28)

### Changes

- Production `RealTimeAlertListener` registration now passes the resolved
  `IConfiguration`; `AcceptLegacyV0` exposes the effective immutable setting.
- Production-registration tests resolve the actual listener with
  `Messaging:AcceptLegacyV0` both enabled and disabled. External persistence
  and Windows EventLog providers are isolated only in the test host.
- `AnalyzerV1ProcessClient` creates a fresh `messageId` for the
  Backend-to-Analyzer hop while preserving `requestId`, `correlationId`, and
  context. Its real-subprocess test verifies all four identity rules.
- Listener runtime coverage proves that an immutable-context-invalid first
  delivery does not claim the ID, a corrected retry with the same ID reaches
  domain dispatch, and a subsequent valid duplicate is suppressed.

### TDD evidence

- Red: production DI tests failed because the resolved listener did not receive
  configuration (then exposed test-environment DB/EventLog dependencies).
- Red: listener runtime fixture first exposed strict millisecond `sentAt`
  validation; after correcting the fixture it exercised the intended domain
  path. No separately executed Red was captured for the analyzer hop assertion.
- Green: Round-4 focused tests: 4 passed, 0 failed/skipped.
- Green: full ASPS-611 C# focused suite: 49 passed, 0 failed/skipped.
- Green: `dotnet build ASPSBackend.sln -c Debug --nologo --no-restore`:
  0 errors, 4 existing warnings.
- `git diff --check`: passed (line-ending notices only).

### Exact continuation point

Root/CEO may rerun the unchanged cross-language gates from Round 3, verify this
Round-4 delta, and submit ASPS-611 for independent QA. Do not commit or move
Jira until QA PASS.

## QA Round 5 remediation (2026-07-28)

- Added an internal, test-only observable seam at the exact start of
  `DispatchAlertInBackground`; production behavior and ordering are unchanged.
- The listener runtime test now creates a real valid token in `TokenStore` and
  supplies matching in-memory ASView device/user records.
- The test proves the requested side-effect sequence directly:
  malformed immutable-context delivery leaves the dispatch counter at `0`;
  corrected delivery with the same `messageId` advances it to `1`; a later
  valid duplicate leaves it at `1`.
- Red evidence: test compilation failed with CS1061 before the dispatch seam
  existed. Fixture setup then exposed and corrected a covariant-list mismatch.
- Green evidence:
  - Runtime dispatch test: 1 passed, 0 failed/skipped.
  - Full ASPS-611 focused C# suite: 49 passed, 0 failed/skipped.
  - Solution build: 0 errors, 4 existing warnings.

## Final independent QA (2026-07-28)

- Verdict: **PASS**.
- Independent QA verified the production listener path with a valid registered
  token, device, and user.
- The observable dispatch counter proves the required sequence: malformed
  delivery `0`, corrected same-ID delivery `1`, later valid duplicate remains
  `1`.
- Independent final verification:
  - Round-5 runtime test: 1 passed.
  - Full focused C# suite: 47 passed.
  - Solution build: 0 errors, 2 warnings.
  - `git diff --check`: passed; line-ending notices only.
- Status: ready for an isolated ASPS-611 commit and Jira closure by root/CEO.
  - `git diff --check`: passed (line-ending notices only).

Exact continuation: independent QA review of the Round-5 `0 → 1 → 1`
runtime proof. No commit or Jira transition before QA PASS.

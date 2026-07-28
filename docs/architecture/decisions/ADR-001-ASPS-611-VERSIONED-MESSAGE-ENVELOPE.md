# ADR-001 — ASPS-611 Versioned End-to-End Message Envelope

- Status: Proposed
- Date: 2026-07-28
- Jira: ASPS-611 — `[CODE REVIEW] Introduce a versioned end-to-end message envelope and correlation IDs`
- Decision owners: Architect; implementation owners listed below
- Source finding: `docs/code-reviews/ASPS_TOP_LEVEL_CODE_REVIEW_2026-07-28.md`

## Context

Runtime messages do not preserve one immutable identity across Backend, Analyzer,
Desktop, and Extension. URL scans are currently matched by domain/current URL,
some Desktop flows use a most-recent pending URL, and success/error shapes and
field casing differ by boundary. Concurrent scans, tab changes, delayed
notifications, and incompatible payload changes can therefore misroute or
misinterpret results.

ASPS-611 requires generated/shared schemas, explicit rejection of incompatible
versions, and passing concurrent/out-of-order contract tests.

This ADR covers:

- Backend ↔ Analyzer subprocess JSON;
- Backend ↔ Desktop ZMQ request/reply and publish/subscribe;
- Desktop ↔ Extension WebSocket JSON.

Transport authentication, durable notification ACK/replay, Analyzer timeout
ownership, ProtectiveAction alignment, and correct tab enforcement are separate
issues. This ADR gives those changes a stable contract but does not implement
them.

## Decision

### 1. Canonical wire representation

All new runtime application messages use one UTF-8 JSON envelope. Wire property
names are lower camel case. Enum/discriminator values are lower snake case.
Unknown properties are ignored within a supported major version, but required
properties, identifiers, types, and discriminators are validated strictly.

The source of truth is a versioned JSON Schema bundle under:

```text
contracts/messaging/v1/
  envelope.schema.json
  common.schema.json
  url-scan-request.schema.json
  url-scan-result.schema.json
  error.schema.json
  examples/
```

Generated artifacts are checked in so consumers do not need a generator at
runtime:

```text
ASPSBackend14_J/Common/Generated/Messaging/V1/
apps/desktop/win/src/generated/messaging/v1/
apps/extension/chrome/generated/messaging/v1/
Analyzers/basic-url-analyzer/generated/messaging/v1/
```

Generated files carry a source-schema hash and a "do not edit" header. CI
regenerates them and fails on drift.

### 2. Envelope v1

```json
{
  "schemaVersion": "1.0",
  "messageId": "4b2a90c9-4b50-40e8-8969-f594b9fde602",
  "correlationId": "6b7a9fa7-e3f0-4c5c-86cc-3914f42b262f",
  "requestId": "7afe6cba-7916-40e1-91dc-666f40f760db",
  "messageType": "url_scan.request",
  "sentAt": "2026-07-28T10:15:30.123Z",
  "source": "extension",
  "context": {
    "deviceId": "device-stable-id",
    "tabId": "384",
    "url": "https://example.com/account?step=1"
  },
  "outcome": null,
  "payload": {}
}
```

Required on every envelope:

| Field | Rule |
|---|---|
| `schemaVersion` | String `MAJOR.MINOR`; v1 writers emit exactly `1.0`. |
| `messageId` | Lower-case canonical UUID v4; unique for every emitted wire message and immutable. |
| `correlationId` | Lower-case canonical UUID; shared by all messages in one end-to-end workflow/trace and immutable. |
| `requestId` | Lower-case canonical UUID; identifies the originating command/request and is echoed by every progress/result/error response. |
| `messageType` | Allowlisted discriminator; selects the payload schema. |
| `sentAt` | UTC RFC 3339 timestamp with milliseconds and `Z`. Informational; never used as identity. |
| `source` | One of `extension`, `desktop`, `backend`, `analyzer`. |
| `context` | Required object. Context fields are nullable only where the message-specific schema allows it. |
| `outcome` | `null` for requests/events with no result; required success/error union for responses. |
| `payload` | Object selected by `messageType`; never overloaded with envelope metadata. |

Identity semantics:

- The Extension creates `requestId` and `correlationId` for a browser-originated
  scan. Desktop and Backend preserve both. Analyzer preserves both in its output.
- Non-browser workflows are initiated by their first trusted producer, which
  creates both IDs.
- Each component creates a new `messageId` whenever it emits a new message,
  including a forwarded/translated hop. Retries of the exact same logical wire
  message reuse `messageId`; a newly constructed attempt uses a new `messageId`
  but preserves `requestId` and `correlationId`.
- IDs received from an upstream peer are never regenerated merely because a
  component does not recognize a pending request. Such a message is rejected or
  quarantined, not rebound to the latest request.
- `deviceId` is the existing stable device UID expressed as a string. It is
  mandatory on Backend↔Desktop runtime messages and browser workflows after
  Desktop enrichment. Extension-originated requests may set it to `null`;
  Desktop must fill it before Backend transmission.
- `tabId` is an opaque decimal string scoped to one browser profile/session.
  It is required for browser-tab operations and `null` for non-browser
  workflows. It is never parsed as a globally meaningful integer.

### 3. Canonical URL

`context.url` is the canonical URL identity for correlation and stale-tab
validation. Producers also retain an optional `payload.originalUrl` for display
or diagnostics. Canonicalization is defined centrally and represented by shared
fixtures:

1. Accept absolute `http` or `https` only.
2. Parse using a standards-compliant URL parser; reject invalid input.
3. Lower-case the scheme and ASCII/punycode host.
4. Remove a trailing host dot and the default port (`:80` for HTTP, `:443` for
   HTTPS).
5. Remove user-info and fragment. Presence of user-info is a validation error;
   the fragment is excluded because it is not sent to the server.
6. Use `/` for an empty path and remove dot segments.
7. Preserve path case, percent-encoded semantics, query parameter order,
   duplicates, and query value case. Do not decode/re-encode into a different
   resource.

The Extension computes the initial value. Desktop and Backend independently
canonicalize and require an exact match; mismatch returns
`validation.canonical_url_mismatch`. The Analyzer echoes the requested canonical
URL and may additionally return `payload.finalUrl` after redirects. It must not
replace the request identity with the redirect target.

### 4. Explicit result union

A response never signals failure through a missing field, score value, or
transport-only status.

Success:

```json
"outcome": {
  "status": "success",
  "result": {}
}
```

Failure:

```json
"outcome": {
  "status": "error",
  "error": {
    "code": "analyzer.timeout",
    "message": "URL analysis exceeded its deadline",
    "retryable": true,
    "details": {}
  }
}
```

`status` is the discriminator. Exactly one of `result` or `error` is present.
`error.code` is stable and machine-readable; `message` is safe for logs/UI and
must contain no secret. `details` is optional and schema-constrained. A failed
analysis has no risk score and must never deserialize as a safe score.

### 5. Boundary behavior

#### Desktop ↔ Extension

- `url_scan.request` is created by Extension with immutable
  `{requestId, correlationId, tabId, url}`.
- `url_scan.accepted`, `url_scan.result`, and `url_scan.error` echo those four
  values.
- Extension pending state is keyed only by `requestId`; domain and active tab
  are not correlation keys.
- A final result is consumable only when `requestId` exists and the referenced
  tab still has the same canonical URL. Otherwise it is recorded as stale and
  no protection is applied.

#### Backend ↔ Desktop

- Desktop enriches the envelope with `deviceId`; Backend compares it with the
  authenticated transport/device identity and rejects mismatch.
- The immediate ZMQ reply and later notification both preserve `requestId` and
  `correlationId`. They have distinct `messageId` values.
- Backend persistence/event metadata stores these identities before async
  analysis so notification construction never guesses them from URL/device.
- Consumers deduplicate by `messageId` within a bounded retention window.

#### Backend ↔ Analyzer

- Backend passes one `url_scan.request` envelope to the Analyzer's machine
  interface and expects exactly one `url_scan.result` or `url_scan.error`
  envelope on stdout.
- Analyzer diagnostic output goes to stderr; stdout contains only the JSON
  envelope.
- Analyzer echoes `schemaVersion`, `requestId`, `correlationId`, `deviceId`,
  `tabId`, and `context.url`. Backend validates all echoed immutable fields
  against the invocation before accepting the result.
- Process launch arguments may remain temporarily for compatibility, but the
  v1 machine contract is a JSON request file/stdin plus JSON stdout, avoiding
  loss of metadata.

### 6. Version negotiation and validation

- Major versions are incompatible. A v1 consumer receiving any version other
  than major `1` returns `protocol.unsupported_schema_version` when a reply is
  possible; otherwise it logs/quarantines the message without executing it.
- Minor versions within major `1` are additive only: optional properties and
  new allowlisted message types may be added. Existing required fields and
  semantics cannot change.
- Malformed JSON, missing required fields, invalid UUIDs, identity mismatch,
  unknown `messageType`, invalid outcome union, or invalid canonical URL fail
  before domain handling and produce a structured `protocol.*` or
  `validation.*` error.
- Logging carries `messageId`, `correlationId`, and `requestId`, but redacts URL
  query/fragment and never logs tokens, page content, or credentials.

### 7. Compatibility and migration

Rollout is dual-read/single-write per boundary. No component may emit two
business operations for one request.

1. Add schemas, generated models/validators, fixtures, and contract tests. No
   runtime switch.
2. Backend↔Analyzer: Backend accepts legacy analyzer output behind a
   `Messaging:AcceptLegacyV0` compatibility flag and adapts it internally to v1;
   Analyzer emits v1 when invoked with `--contract-version 1`.
3. Backend↔Desktop: Backend accepts v0 and v1 requests but emits v1 to a
   v1-capable Desktop. Capability is established during authenticated
   enrollment/handshake, never guessed from a failed parse.
4. Desktop↔Extension: handshake advertises
   `supportedSchemaMajors: [1]`; after agreement both sides emit v1. Legacy mode
   remains isolated behind the same named compatibility flag.
5. Make v1 the default after all four components and cross-component tests are
   deployed.
6. Remove v0 readers and the compatibility flag after one documented release
   window and telemetry shows no v0 clients.

Legacy adaptation must generate missing IDs once at the ingress boundary and
carry them forward. It must be tagged `legacyAdapted: true` in internal
telemetry. Legacy messages with ambiguous correlation must never be used for
tab-targeted protection.

## Rejected alternatives

- **Use URL/domain as the correlation key:** collisions occur for concurrent
  scans and redirects; it cannot distinguish tabs or retries.
- **Use one ID for every purpose:** loses the distinction between a workflow, a
  request, and individual messages needed for tracing and deduplication.
- **Big-bang deployment:** creates an unnecessary four-component release
  dependency and makes rollback unsafe.
- **Keep PascalCase/snake_case variants by component:** preserves the current
  ambiguity and prevents one generated schema from being authoritative.

## Validation and test plan

### Schema/generation

- Every example validates against the JSON Schema.
- Generated C#, Python, and JavaScript artifacts reproduce the committed schema
  hash; CI fails on drift.
- Golden fixtures deserialize and reserialize without changing immutable fields.
- Negative fixtures cover every required field, malformed UUID/timestamp,
  unknown message type, mixed success/error, and unsupported major version.

### Canonical URL

One shared fixture corpus runs in C#, Python, and JavaScript and covers host/scheme
case, IDN, default/non-default ports, empty path, dot segments, fragments,
duplicate/ordered query parameters, percent encoding, user-info, malformed URLs,
IPv4, and IPv6. All implementations must produce byte-identical output or the
same error code.

### Concurrent/out-of-order acceptance tests

1. Start two scans for the same canonical URL in different tabs; deliver results
   in reverse order; each resolves only its `requestId` and original tab.
2. Start two scans in one tab for different URLs; deliver the first result last;
   the stale result is retained for observability but performs no action.
3. Retry a message with the same `messageId`; downstream domain handling occurs
   once.
4. Deliver two messages with one `requestId` and distinct `messageId` values;
   progress and final result are both accepted in valid state order.
5. Alter one echoed immutable field at each hop; the receiver rejects it with a
   structured error.
6. Analyzer explicit error and timeout contain no score and cannot deserialize
   as success.
7. Unsupported major versions fail explicitly at all three boundaries.
8. Legacy-adapted traffic cannot trigger tab-targeted enforcement.

### Component gates

- Backend: .NET unit tests for schema validation, identity persistence,
  deduplication, analyzer echo validation, and notification propagation.
- Analyzer: pytest contract fixtures for request/result/error and stdout purity.
- Desktop: pytest tests for enrichment, forwarding, pending map by `requestId`,
  immutable echo checks, and legacy restrictions.
- Extension: Jest tests for ID generation, pending state, stale-tab rejection,
  concurrent scans, and out-of-order delivery.
- Cross-component fixture runner validates the same golden corpus in all four
  implementations.

## Implementation ownership and dependencies

| Order | Owner | Deliverable | Depends on |
|---:|---|---|---|
| 1 | `backend` | Own schema repository, C# generation, validators, identity persistence, Backend adapters | ADR approval |
| 2 | `analyzer-ai` | Analyzer generated bindings and v1 CLI request/result/error | schema package |
| 3 | `desktop-agent` | Python bindings; Backend and Extension boundary adapters; request tracking | schema package |
| 4 | `browser-extension` | JS bindings; v1 handshake; request state keyed by `requestId` | schema package, Desktop handshake |
| 5 | `qa` | Independent cross-component acceptance and negative-version tests | all implementations and unit-test gates |

Steps 2 and the Backend-facing part of 3 can run in parallel after the schema
package is frozen. Extension runtime switching follows Desktop handshake support.

Downstream Jira dependencies:

- ASPS-612 consumes the v1 Analyzer success/error union.
- ASPS-610 secures the Desktop↔Extension handshake that carries version
  negotiation.
- ASPS-618 aligns ProtectiveAction payloads inside the v1 envelope.
- ASPS-619 secures Backend↔Desktop enrollment/capability advertisement.
- ASPS-620 adds durable delivery/ACK using `messageId`.
- ASPS-621 uses `{requestId, tabId, url}` to enforce origin-tab routing.

## Consequences and risks

- Positive: one traceable and testable identity chain, explicit failure
  semantics, deterministic tab routing, generated cross-language contracts, and
  safe incremental rollout.
- Cost: schema generation/tooling, compatibility adapters, persisted identity
  fields, and coordinated releases.
- Risk: two protocol modes can linger. Mitigate with one compatibility flag,
  v0 usage telemetry, a removal date, and tests proving legacy traffic cannot
  enforce tab-specific actions.
- Risk: URL parser differences can create false mismatches. Mitigate with a
  shared canonicalization corpus and fail-closed behavior.

# ASPS-669 — Risk Score not Displayed in Extension

## Status
- **JIRA:** In Progress (transitioned 2026-08-05)
- **Branch:** `asps-669-risk-score-not-displayed`
- **Commit:** `cbfbb36`
- **Labels:** browser-extension, ceo

## Root Cause
Message type mismatch between `popup.js` and `background.js` MessageBus.
- popup.js sent legacy plain-string types: `'getStatus'`, `'scanCurrentPage'`, `'reconnect'`
- MessageBus handlers registered on new namespaced constants: `'status:get'`, `'scan:current'`, `'connection:reconnect'`
- Result: every status poll from popup returned `{ error: 'No handler for message type: getStatus' }` — risk score never rendered, UI stayed on "CHECKING..." forever.

## Fix
Updated 5 string literals in `popup.js` across 4 call sites to match MessageBus handler names.

### Changed Files
- `apps/extension/chrome/popup.js` — 5 message type string fixes
- `apps/extension/chrome/tests/unit/popup/MessageTypeContract.test.js` — new regression test

## Verification
- New test suite (MessageTypeContract): 5/5 passed
- Full extension test suite: 227 passed, 74 failed (pre-existing), 300 total
- Pre-existing failures confirmed identical on `main` — not introduced by this change

## Fix 2 — sentAt validator too strict (blocked scan results reaching extension)

### Root Cause
`validate_envelope` in the desktop agent used a strict
`datetime.strptime(value["sentAt"], "%Y-%m-%dT%H:%M:%S.%fZ")` that only accepts a
`Z`-suffixed UTC timestamp with up to 6 fractional digits. The .NET backend was (at
the time) emitting default `DateTimeOffset` JSON serialization —
`+00:00` offset notation with 7-digit tick precision, e.g.
`2026-08-05T20:02:59.6033682+00:00` — which `strptime` rejected outright, raising
`ContractError("protocol.invalid_sent_at", ...)` and dropping the message before the
risk score ever reached the extension.

### Fix (Python side — desktop agent)
`apps/desktop/win/src/generated/messaging/v1/message_envelope.py`:
- Added `timedelta` import.
- Replaced the strict `strptime` with `datetime.fromisoformat` (Python 3.11+), after
  normalizing a trailing `Z` to `+00:00`. `fromisoformat` natively tolerates 3-7+
  fractional digits.
- Still rejects: non-UTC offsets (anything other than `+00:00`/`Z`), naive
  (offset-less) timestamps, missing `sentAt`, and unparsable strings — all mapped to
  the same `protocol.invalid_sent_at` ContractError as before.

### Fix (C# side — backend agent, authored by this agent)
`ASPSBackend14_J/Common/Generated/Messaging/V1/MessageEnvelope.cs` gained a
`UtcMillisecondsConverter : JsonConverter<DateTimeOffset>` applied via
`[JsonConverter(typeof(UtcMillisecondsConverter))]` on `MessageEnvelopeV1.SentAt`, so the
backend now serializes `sentAt` as `Z`-suffixed milliseconds (e.g.
`2026-08-05T20:02:59.603Z`, matching the Python agent's own `create_envelope` output)
while still *deserializing* both `Z` and `+00:00` notations (`DateTimeOffset.Parse`).
This is belt-and-suspenders with the Python-side tolerance fix above — both sides now
independently agree on format, so either backend or Python-originated envelopes validate
correctly regardless of which fix lands first.
`MessageEnvelopeValidator.cs` required no change — `envelope.SentAt.Offset != TimeSpan.Zero`
still holds for the parsed value.

### Changed Files (this fix)
- `apps/desktop/win/src/generated/messaging/v1/message_envelope.py` — tolerant `sentAt` parsing (desktop-agent)
- `apps/desktop/win/src/tests/test_message_envelope.py` — new regression test suite, 9 tests (desktop-agent)
- `ASPSBackend14_J/Common/Generated/Messaging/V1/MessageEnvelope.cs` — added `UtcMillisecondsConverter`, applied to `SentAt` (backend)
- `ASPSBackend14_J/ASPS.Tests/Common/Messaging/MessageEnvelopeValidatorTests.cs` — 2 new tests: `Serialize_SentAt_UsesUtcZSuffixWithMillisecondPrecision`, `Deserialize_SentAt_AcceptsBothZAndOffsetNotation` (backend)

### TDD Evidence
- **Desktop agent — Red:** ran `test_message_envelope.py` against the old `strptime` implementation (via `git stash`) — 3/9 failed exactly on the new-format cases (`Z` with 7-digit fraction, `+00:00` with ms, `+00:00` with 7-digit fraction); 6/9 passed (pre-existing behavior + validation-rejection cases already worked).
- **Desktop agent — Green:** restored the fix (`git stash pop`) — 9/9 pass.
- **Desktop agent — Full suite:** `cd apps/desktop/win && python -m pytest src/tests/ -v` → 254 passed, 2 xfailed (pre-existing, unrelated — ASPS-562 RustDesk enum gap), 0 failed.
- **Backend — build:** `dotnet build ASPSBackend.sln -c Debug` — 0 `error CS####` (initial attempt hit MSB3027/MSB3021 file locks from a running VS debug session on `ASPSBackend.exe`/`WebApi.exe` — the same PIDs holding `Common.dll`/`Interface.dll`/`Business.dll`; stopped those two debug processes, rebuilt clean — "Build succeeded, 0 Error(s)").
- **Backend — full test suite:** `dotnet test ASPS.Tests/ASPS.Tests.csproj` → **1649 passed, 0 failed, 7 skipped** (1656 total).
- **Backend — filtered:** `--filter "FullyQualifiedName~MessageEnvelopeValidatorTests"` → **13 passed, 0 failed** (11 pre-existing + 2 new).

## Pending
- **Manual smoke test in browser** — reload extension, open popup, confirm risk score renders
- **QA gate** — independent QA review before merge (covers popup.js fix, Python sentAt tolerance fix, and C# `UtcMillisecondsConverter`)
- **Merge to main** — after QA PASS + code review
- **Note for whoever restarts the local dev session:** this agent stopped a running VS debug session (`ASPSBackend.exe` PID 12260, `WebApi.exe` PID 26160) to unblock `dotnet build`/`dotnet test` file locks — restart debugging in Visual Studio if needed.
- No commit made yet — awaiting explicit instruction per repo convention (commit only when asked).

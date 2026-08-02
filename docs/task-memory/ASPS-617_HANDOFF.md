# ASPS-617 Handoff

## Task

- Jira: `ASPS-617`
- Exact title: `[CODE REVIEW] Fix Desktop browser-history delivery state`
- Status: `PRE-QA READY`
- Owner scope: `apps/desktop/win/` plus this handoff
- Commit/Jira mutation: not performed; root owns post-QA workflow

## QA history

### First review: `FAIL` (Major)

QA found that `services/scan_service.py` still called the old
`mark_url_as_sent()` immediately after an Extension cache miss. The first
implementation had retained that method as a compatibility alias for durable
acknowledgement. Therefore auth failure, timeout, rejection, or a crash in the
Extension path could permanently suppress the later browser-history delivery.

Remediation:

- Removed the ambiguous `mark_url_as_sent()` API.
- Added explicit `queue_url_for_delivery()`.
- Extended the same durable transitions through `ScanService`.
- Added cross-source Extension -> History regression coverage.

## Diagnosis

The committed flow was self-cancelling:

1. `BrowserHistoryMonitor.get_new_entries()` inserted each discovered
   `browser:url` into `_seen_urls`.
2. `MonitorService._monitor_browser_history()` immediately called
   `is_url_seen(entry.url)`.
3. That check returned `True`, so the newly discovered URL was never sent.

The old state was memory-only, had no response-driven transition, could not
retry failed delivery reliably, and lost deduplication on restart.

## Acceptance-test checklist

- [x] A newly discovered eligible URL is delivered once after explicit backend
  acceptance.
- [x] Discovery/queueing does not mark the URL seen.
- [x] A timeout/failed/auth-error delivery moves to `failed` and is retried.
- [x] Authentication loss keeps the URL `queued`; delivery resumes after auth.
- [x] Duplicate URL discoveries, including across browsers, produce one
  delivery candidate.
- [x] `acknowledged` state survives restart and suppresses delivery.
- [x] Restart recovers an interrupted `sent` state as `failed` for retry.
- [x] Extension cache miss does not acknowledge before backend acceptance.
- [x] Extension auth loss stays queued and unseen.
- [x] Extension timeout/rejection/transport error becomes failed/retriable.
- [x] Explicit Extension acceptance acknowledges and suppresses the equivalent
  browser-history duplicate.
- [x] A valid cache hit safely completes an existing queue without another send.

## State design

Durable state file:
`%APPDATA%\AntiScam\browser-history-delivery.json`.

Transitions:

```text
discovered -> queued
queued/failed -> sent
sent -> acknowledged   (explicit accepted/success response)
sent -> failed         (timeout, exception, rejected response, auth failure)
sent at restart -> failed
discovered at restart -> queued
```

Only `acknowledged` counts as seen. State is written via a sibling temporary
file followed by atomic `os.replace`. Durable queued/failed records contain
enough entry data to retry after the browser's five-minute discovery window.
Acknowledged records are capped at the newest 5,000 entries; pending
queued/failed deliveries are never pruned.

## TDD evidence

### Red

Temporary regression test executed against the committed `HEAD` version of
`browser_history.py`:

```text
python.exe -m unittest src.tests.test_asps617_red_baseline_temp -v
Ran 1 test
FAILED (failures=1)
AssertionError: True is not false
```

The assertion verified that discovery must not make
`is_url_seen(entry.url)` true before delivery acknowledgement. The temporary
test was deleted after recording Red evidence; the permanent equivalent is
`test_get_new_entries_queues_without_marking_url_seen`.

After the first QA failure, six permanent cross-source regressions were added
and run before remediation:

```text
python.exe -m unittest src.tests.test_scan_service_delivery_state -v
Ran 6 tests in 0.216s
FAILED (failures=2, errors=3)
```

The failures reproduced premature acknowledgement after auth loss and timeout,
missing durable queue API, false-success handling for explicit rejection, and
an unhandled transport exception. One acceptance test passed for the wrong
reason (the premature acknowledgement); the full focused suite below verifies
its corrected transition.

### Green — focused

```text
python.exe -m unittest src.tests.test_browser_history \
  src.tests.test_monitor_browser_history \
  src.tests.test_scan_service_delivery_state -v
Ran 37 tests in 0.504s
OK
```

Counts: 37 passed, 0 failed, 0 skipped.

### Wider Desktop suite

```text
python.exe -m unittest discover -s src/tests -v
Ran 157 tests in 0.865s
FAILED (failures=4, errors=1)
```

Counts: 152 passed, 4 failed, 1 error, 0 skipped. All five failures are in
unmodified, unrelated remote-access/RustDesk code:

- `test_medium_confidence`
- `test_crd_client_connected`
- `test_crd_session_started`
- `test_rustdesk_config_id_is_not_mapped_to_vnc`
- `test_rustdesk_enum_has_dedicated_entry` (error)

No ASPS-617 browser-history or MonitorService test failed.

### Compile/diff checks

```text
python.exe -m py_compile src/browser_history.py \
  src/services/monitor_service.py \
  src/services/scan_service.py \
  src/tests/test_browser_history.py \
  src/tests/test_monitor_browser_history.py \
  src/tests/test_scan_service_delivery_state.py
```

Result: exit 0.

```text
git diff --check -- apps/desktop/win
```

Result: exit 0; only line-ending notices were printed.

## Changed files

- `apps/desktop/win/src/browser_history.py`
  - durable delivery-state store and restart recovery
  - state transition APIs
  - durable queued/failed candidate retrieval
  - acknowledged-only seen semantics and cross-browser URL deduplication
- `apps/desktop/win/src/services/monitor_service.py`
  - extracted one-cycle browser-history processor
  - response-driven sent/acknowledged/failed transitions
  - authentication-loss queue preservation
- `apps/desktop/win/src/services/scan_service.py`
  - queues Extension discoveries before auth without acknowledging them
  - records sent immediately before transport
  - acknowledges only explicit accepted/success/legacy-result responses
  - marks auth failure, rejection, timeout, and transport exceptions failed
  - treats a valid analysis cache hit as completed delivery
- `apps/desktop/win/src/tests/test_browser_history.py`
  - state, retry, duplicate, restart, and acknowledgement tests
- `apps/desktop/win/src/tests/test_monitor_browser_history.py`
  - delivery-once, retry, auth-loss, and auth-error tests
- `apps/desktop/win/src/tests/test_scan_service_delivery_state.py`
  - Extension -> History cross-source transition and restart regressions

## Refactor

- Extracted `_process_browser_history_once()` so a poll cycle is independently
  testable without running the infinite monitor loop.
- Centralized delivery transitions in `BrowserHistoryMonitor`.
- Removed ambiguous `mark_url_as_sent()` semantics; callers must explicitly
  queue, mark sent, acknowledge, or fail.

## QA continuation point

Independent re-QA should verify the Jira acceptance criteria and the Major
finding remediation against the six changed Desktop files, rerun the 37
focused tests, confirm the five wider-suite failures are unrelated/pre-existing,
inspect persistence/restart and Extension -> History behavior, and return
`PASS` or `FAIL` with file/line evidence. No commit is authorized before
documented QA PASS.

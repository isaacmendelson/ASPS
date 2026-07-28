# ASPS-616 Handoff

## Task

- Jira ID: `ASPS-616`
- Title: `[CODE REVIEW] Fix Desktop browser-tabs callback deadlock`
- Component: `apps/desktop/win`
- Status: `QA PASS` after independent re-review — ready for isolated commit
  and Jira closure.

## Acceptance checklist

- [x] A late Chrome-extension connection does not await the connect callback
  before starting the WebSocket receive loop.
- [x] An active-session tab refresh can wait for `browser_tabs_response` while
  that same connection's receive loop resolves the pending Future.
- [x] The connect callback is scheduled independently and callback failure
  remains best-effort instead of breaking message receipt.
- [x] A callback still running when its WebSocket disconnects is cancelled and
  awaited, preventing a leaked background task.
- [x] Cancelling a tab request during send or `asyncio.wait` removes every
  request ID and cancels every unresolved Future.

## TDD evidence

### Red

Added
`apps/desktop/win/src/tests/test_extension_server.py::TestExtensionServerConnectionOrdering::test_receive_loop_handles_tabs_while_connect_callback_waits`.

The fake late extension sends `browser_tabs_response` only after the server
sends `get_browser_tabs`. With the original implementation the callback was
awaited before `async for message`, so the tab request timed out and the test
failed:

```text
AssertionError: Lists differ: [] !=
[{'url': 'https://example.com', 'title': 'Example'}]
[TABS-RESP] ... future_found=False
Ran 1 test ... FAILED (failures=1)
```

Command:

```powershell
py -3.9 -m unittest tests.test_extension_server -v
```

### Green

`ExtensionServer._handle_client` now creates an independent connect-callback
task and immediately enters the receive loop. The response resolves the
pending request before timeout:

```text
test_receive_loop_handles_tabs_while_connect_callback_waits ... ok
[TABS-RESP] ... future_found=True future_done=False
Ran 1 test ... OK
```

Command:

```powershell
py -3.9 -m unittest tests.test_extension_server -v
```

Result: `1 passed, 0 failed, 0 skipped`.

The same regression also passed under the repository analyzer virtual
environment's Python 3.12 interpreter:

```powershell
& 'C:\Jobs\ASPS\GitHub\Software\Analyzers\basic-url-analyzer\.venv\Scripts\python.exe' `
  -m unittest tests.test_extension_server -v
```

Result: `1 passed, 0 failed, 0 skipped`.

## First QA result and remediation

### QA FAIL

QA found one Minor lifecycle issue: disconnecting while the independently
scheduled callback was suspended in `request_browser_tabs()` cancelled the
callback at `await asyncio.wait(...)`. The resulting `CancelledError` skipped
the method's original cleanup block, leaving a pending Future in
`_tab_request_futures`.

### Remediation Red

Added
`test_disconnect_cleans_cancelled_tab_request_futures`. It waits until the tab
request is registered, disconnects the client, then verifies both the request
map and the captured Future:

```text
AssertionError:
{'<request-id>:<client-id>': <Future pending ...>} != {}
Ran 2 tests ... FAILED (failures=1)
```

### Remediation Green

The complete request lifecycle is now inside `try/finally`. Every created
Future/key pair is registered before an awaited send, and the `finally` block
always cancels unresolved Futures and removes their keys. This covers
`CancelledError` during both send and `asyncio.wait`.

```text
test_disconnect_cleans_cancelled_tab_request_futures ... ok
test_receive_loop_handles_tabs_while_connect_callback_waits ... ok
Ran 2 tests ... OK
```

Result on both Python 3.9 and repository Python 3.12:
`2 passed, 0 failed, 0 skipped`.

### Refactor / lifecycle

- Extracted `_notify_client_connected()` to preserve the prior best-effort
  exception boundary.
- The handler cancels and awaits an unfinished callback task on disconnect.
- No protocol, backend contract, or extension code changed.

## Additional verification

```powershell
py -3.9 -m py_compile extension_server.py tests/test_extension_server.py
```

Result: PASS.

```powershell
git diff --check -- apps/desktop/win/src/extension_server.py `
  apps/desktop/win/src/tests/test_extension_server.py
```

Result: PASS (Git reported only the repository's LF-to-CRLF checkout warning).

Full desktop discovery was also attempted:

```powershell
py -3.9 -m unittest discover -s tests -p "test_*.py" -v
```

Observed result: the two regressions and 21 existing notification tests passed;
five existing test modules could not import because the only available
system interpreter is Python 3.9 without project dependencies
(`python-dotenv`, `psutil`, and `pytest`). The analyzer virtual environment
provides Python 3.12 and `pytest`, but likewise lacks the Desktop dependencies
`python-dotenv`, `psutil`, and `websockets`. This is an
environment/dependency gap, not a failing assertion introduced by ASPS-616.
The target Python 3.11 executable is not available on `PATH`, and the
Knowledge Engine virtual environment points to a removed Python 3.11
installation.

## Changed-file manifest

- `apps/desktop/win/src/extension_server.py`
- `apps/desktop/win/src/tests/test_extension_server.py`
- `docs/task-memory/ASPS-616_HANDOFF.md`

## QA continuation point

- Independent re-review: **PASS**, with no Blocker, Major, Minor, or Nit
  findings.
- QA reran both focused regressions: **2 passed, 0 failed, 0 skipped**.
- QA reran `py_compile` and scoped `git diff --check`: **PASS**.
- Full discovery: **23 passed** and five environment-only import errors caused
  by missing `python-dotenv`, `psutil`, and `pytest`.

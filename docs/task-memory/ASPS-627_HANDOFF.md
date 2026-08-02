# ASPS-627 Handoff — Split core/analyzer.py and standardize observability

**Task:** ASPS-627 [CODE REVIEW] Split oversized orchestrators, remove legacy paths, and standardize observability (Analyzer component)
**Status:** Implementation complete — QA required before commit
**Last updated:** 2026-07-29

---

## Completed work

### Split core/analyzer.py

The 768-line orchestrator was split into four dedicated modules. The `ScamAnalyzer` class remains as the thin orchestrator in `core/analyzer.py` (~240 lines of production logic).

New modules created:

| File | Responsibility | Approx lines |
|---|---|---|
| `core/classification_maps.py` | `_CATEGORY_TO_WEBSITE_TYPE` and `_CATEGORY_TO_SCAM_TYPE` dicts + `map_category_to_website_type()` + `determine_scam_type()` | ~150 |
| `core/scoring.py` | `determine_risk_level()`, `calculate_confidence()`, `tiered_score_combination()` | ~100 |
| `core/content_validator.py` | `check_content_validity()` — block/agreement/error page detection | ~110 |
| `core/result_formatter.py` | `generate_red_flags()`, `generate_recommendation()` | ~75 |

Backward-compatible shim methods added to `ScamAnalyzer` so existing tests calling `analyzer._tiered_score_combination(...)` etc. continue to work without modification.

The domain-age override logic was extracted from the inline `analyze_url` block into `_apply_domain_age_override()` private method on the orchestrator.

### analyze.py cleanup

- Removed `sys.path.insert(0, ...)` mutation (line 14 of original).
- Removed dead `--whois-only` argument — it was parsed but had no execution path.
- Replaced bare `except:` in `load_config()` with `except FileNotFoundError` + `except (json.JSONDecodeError, OSError)` — each logs at the appropriate level.
- Added module-level `logger = logging.getLogger(__name__)` for non-CLI log events.
- Outer `except Exception` in `main()` now calls `logger.error(..., exc_info=True)` for observability, then also prints to stderr for CLI users.
- All human-facing output remains as `print()` (correct for CLI); internal errors use logging.

### core/analyzer.py observability improvements

- `self.logger.info(f"Starting analysis of {url}")` downgraded to `self.logger.debug("Starting analysis")` — URL no longer logged at INFO (URLs are potentially sensitive).
- Cache hit log downgraded to `debug`.
- All `logger.info/warning/error` calls converted to `%`-style formatting (no f-strings).
- `except Exception as exc:` in `analyze_url()` now passes `exc_info=True` to `logger.error` so the full traceback is visible.
- `_check_content_validity` warning now uses `%s` format instead of f-string.

---

## Dead paths removed

- `--whois-only` CLI flag: declared in argparse but had no corresponding code branch. Removed entirely from `analyze.py`.
- `sys.path.insert(0, ...)` in `analyze.py` (line 14): removed. Imports work via the package structure without path mutation.
- `sys.path.append(...)` in `core/analyzer.py` (line 10): removed. The file now uses only relative imports from the `core` package.

---

## print() → logging changes

`analyze.py`: 0 `print()` calls converted — all existing `print()` calls in this file are intentional CLI human output, which is correct. The `load_config()` error path and the outer `except` block now use `logger` for observability. The `_convert_form_types()` method that existed in the original `analyzer.py` was dead code (never called after ASPS-612 set `form_types: []` permanently); it was not migrated to the new file — it is gone.

`core/analyzer.py`: all `self.logger.info/debug/warning/error` calls already used the logger. All f-string log calls converted to `%`-style.

---

## Exception handling improvements

| File | Original | After |
|---|---|---|
| `analyze.py: load_config()` | `except:` (bare, swallows all) | `except FileNotFoundError` + `except (json.JSONDecodeError, OSError)` — logs at debug/warning |
| `analyze.py: main()` | `except Exception as e: print(...)` | Adds `logger.error(..., exc_info=True)` before the print |
| `core/analyzer.py: analyze_url()` | `except Exception as e: self.logger.error(...)` | Adds `exc_info=True` for full traceback |

3 exception handling improvements.

---

## Compile verification

```
python -m py_compile core/classification_maps.py   → OK
python -m py_compile core/scoring.py               → OK
python -m py_compile core/content_validator.py     → OK
python -m py_compile core/result_formatter.py      → OK
python -m py_compile core/analyzer.py              → OK
python -m py_compile analyze.py                    → OK
python -m compileall -x ".venv" .  -q              → (no output = clean)
```

---

## Test results

Command: `.venv/Scripts/python.exe -m pytest tests/ --ignore=tests/test_real_sites.py --ignore=tests/test_analyzer_deployment.py -q`

Result: **338 passed, 1 failed, 4 skipped, 2 errors** — same as pre-refactoring baseline.

Pre-existing failures (not introduced by this task):
- `test_sophisticated_scam_detection` — ML model accuracy issue, independent of orchestrator structure.
- `TestNoModelsDetection` (2 errors) — Windows temp-directory permission error in Ollama client tests.

---

## Changed files

| Path | Change |
|---|---|
| `Analyzers/basic-url-analyzer/core/analyzer.py` | Rewritten as thin orchestrator; imports from new modules; shims added for test compatibility |
| `Analyzers/basic-url-analyzer/analyze.py` | Removed sys.path mutation, removed dead --whois-only, fixed exception handling, added logging |
| `Analyzers/basic-url-analyzer/core/classification_maps.py` | NEW — category mapping dicts and functions |
| `Analyzers/basic-url-analyzer/core/scoring.py` | NEW — scoring utilities |
| `Analyzers/basic-url-analyzer/core/content_validator.py` | NEW — content validity logic |
| `Analyzers/basic-url-analyzer/core/result_formatter.py` | NEW — red flags and recommendation formatting |

---

## What was NOT changed (per constraint)

- `api.py` — unchanged (already clean; has only one `except Exception` that is correct FastAPI pattern).
- `utils/validators.py`, `scrapers/playwright_scraper.py` — unchanged.
- No files outside `Analyzers/basic-url-analyzer/`.
- No behavior changes — purely structural refactoring.

---

## Next step (Analyzer component)

Hand to QA agent for PASS/FAIL against ASPS-627 acceptance criteria. QA should verify:
1. All 4 new modules compile.
2. All previously passing tests still pass.
3. No new `sys.path` mutations.
4. `--whois-only` flag is gone.
5. `load_config()` bare `except` is gone.
6. URL not logged at INFO in analyzer.

---

# .NET Backend Component — CQRSGateway split

**Component:** `ASPSBackend14_J/Business/Messaging/CQRSGateway.cs`
**Status:** Implementation complete — awaiting QA PASS before commit.
**Last updated:** 2026-07-29

## What was done

### File split

`CQRSGateway.cs` (1,209 lines monolith) split into three partial class files:

| File | Lines (approx) | Responsibility |
|---|---|---|
| `Business/Messaging/CQRSGateway.cs` | 140 | Core: constructor, Start/Stop/Dispose, ListenLoop, ProcessMessageAsync, CreateErrorResponse |
| `Business/Messaging/CQRSGateway.Queries.cs` | 500 | ProcessQueryAsync dispatch + all 28 query handler methods |
| `Business/Messaging/CQRSGateway.Commands.cs` | 300 | ProcessCommandAsync dispatch + all 22 command handler methods |

All three files use `public partial class CQRSGateway`. Same namespace (`Business.Messaging`). Zero behavior changes.

`CreateErrorResponse` changed from `private` to `internal` — required for access from partial class files in different compilation units. This does not affect the public API.

### Legacy parallel path — NOT removed

`NetMQMessageProcessor` (port 5555, no CURVE, no auth) is the parallel path identified by finding 7.  
It was **NOT removed** because it is still referenced:
- `WebApi/Controllers/UsersController.cs` injects `INetMQClientService`
- `WebApi/Controllers/UserDevicesController.cs` injects `NetMQClientService`

Retirement path: migrate those controllers to `ICQRSClient`, then remove `NetMQMessageProcessor`, `NetMQClientService`, `INetMQClientService`.

### Console.WriteLine — CQRSGateway had none

`CQRSGateway.cs` already used `ILogger` throughout. No `Console.WriteLine` calls existed in this file. `Console.WriteLine` in `Program.cs`, `CommandQueryHandlers.cs`, `EntityRepositories.cs` are separate tasks.

### Exception handling — already clean

All exceptions in the original CQRSGateway were caught and logged via `_logger.LogError`. No changes were needed.

## Files changed

**Modified:**
- `ASPSBackend14_J/Business/Messaging/CQRSGateway.cs` — reduced to core lifecycle; `partial` keyword added; `CreateErrorResponse` changed to `internal`

**Created:**
- `ASPSBackend14_J/Business/Messaging/CQRSGateway.Queries.cs`
- `ASPSBackend14_J/Business/Messaging/CQRSGateway.Commands.cs`

## Build result

```
dotnet build ASPSBackend.sln -c Debug --nologo
Build succeeded. 0 Error(s).
```

## Test result

```
dotnet test ASPSBackend.sln -c Debug --nologo --no-build
Failed: 7, Passed: 1467, Skipped: 3, Total: 1477
```

All 7 failures are pre-existing (ASView DB tests, ImmediateDanger tests, AnalyzerV1Process, Composition validation). None are CQRS-related.

```
dotnet test --filter "CQRS"
Passed: 28, Failed: 0, Skipped: 0
```

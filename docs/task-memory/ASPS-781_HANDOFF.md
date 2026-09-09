# ASPS-781 Handoff — Warm Knowledge Engine MCP retrieval once at startup

**Task:** ASPS-781 — performance/reliability fix to the Knowledge Engine MCP server.
**Branch:** `asps-781-ke-mcp-warm-retrieval` (off `main` @ `3674052`)
**Agent:** python (desktop-agent/python hat, this session scoped to KnowledgeEngine tooling)
**Status:** Implementation done, tests green, pushed. Awaiting QA + code review + security gate (per `.claude/rules/task-workflow.md`). Not merged.

---

## Problem

`KnowledgeEngine/scripts/ke_mcp_server.py` called `create_service()` **inside** both
`knowledge_search` (~L26) and `knowledge_ask` (~L45) — every MCP tool call rebuilt a
`KnowledgeService` from scratch: reopened the chromadb `PersistentClient` at `DB_PATH`,
re-resolved the collection, and re-constructed the `Retriever` + `OllamaProvider`. Symptom:
cold-start `CONNECT_TIMEOUT` on the first call after server start, plus avoidable per-query
latency on every subsequent call.

## What the constructor actually does (the expensive part)

`KnowledgeService.__init__` (`KnowledgeEngine/src/knowledge_engine/knowledge_service.py:13-32`)
constructs `DocumentLoader`, `Chunker`, `PromptBuilder` (all cheap, no I/O) and:
- `VectorStore(db_path=..., collection_name=...)` (`vector_store.py:10-21`) →
  `chromadb.PersistentClient(path=...)` (opens/creates the on-disk SQLite-backed store) +
  `client.get_or_create_collection(...)` (resolves the collection). This is disk I/O and,
  empirically, the dominant cold-start cost.
- `OllamaProvider()` (`llm_provider.py:49-62`) — cheap in itself (just imports the `ollama`
  module and stores `model`/`base_url`; no network call in `__init__`).

Separately, chromadb's **default embedding function** is lazy — it is not loaded by
`get_or_create_collection`, only on the **first** `collection.query()`/`upsert()` call. So the
full "first query is slow" cost has two parts: (1) opening the persistent client/collection
(paid at construction) and (2) loading the embedding model (paid at first `.search()`). Both
needed to be forced at startup to actually eliminate first-call latency, not just object
construction.

Measured empirically against the real on-disk collection (`KnowledgeEngine/db`, 2585 chunks):
- Constructing `VectorStore` + first `.search()` (both costs combined, i.e. what `warm_service()`
  now pays at startup): **~2.6s**.
- Subsequent `.search()` calls on the same instance: **~0.5–0.6s** (embedding compute is
  per-query CPU cost, irreducible without caching the embedding model differently — out of
  scope here; the fix removes the *rebuild*, not per-query embedding cost).

## Fix

`KnowledgeEngine/scripts/ke_mcp_server.py`:
- `create_service()` unchanged in behavior (still builds a fresh `KnowledgeService`), but is now
  documented as the *factory* — tool handlers must not call it directly.
- New module-level lazy singleton: `_service: KnowledgeService | None`, guarded by
  `_service_lock` (double-checked locking) via `get_service()`. `get_service()` constructs on
  first use, returns the cached instance thereafter.
- New `warm_service()`: calls `get_service()` then runs one real, cheap search
  (`RetrievalRequest(question="warmup", top_k=1)`) to force chromadb's embedding function to
  load too — not just the collection handle. **No try/except** — any failure (bad `DB_PATH`,
  unreadable collection, embedder load failure) propagates and the process fails to start,
  per the task's "fail loudly at boot, not silently on first query" requirement.
- `knowledge_search` (L79) and `knowledge_ask` (L99) now call `get_service()` instead of
  `create_service()`.
- `if __name__ == "__main__":` now calls `warm_service()` before `mcp.run()`.

`KnowledgeEngine/scripts/ke_cli.py` — **untouched**. It has its own separate `create_service()`
(uses `AnthropicProvider`, its own `KNOWLEDGE_SOURCES`) for the `index`/`search`/`ask` CLI
subcommands, deliberately not coupled to the server's singleton, per the task's constraint.

## Thread-safety finding

Once constructed, `KnowledgeService` and everything it wraps (`VectorStore`, `Retriever`,
`OllamaProvider`) hold **no per-call mutable state** — `search()`/`ask()` only read
(`collection.query`, never `.upsert`) and make a stateless HTTP call to Ollama
(`ollama.chat(...)`, which itself uses the `ollama` package's own internal default client —
pre-existing, not something this change introduces or touches).

Empirical verification (12 concurrent threads calling `VectorStore.search()` against the real
on-disk collection, sharing one instance): **0 errors**, all returned well-formed result lists.
See raw run in this task's session log; not committed as a test because it depends on the live
`KnowledgeEngine/db` collection (gitignored, machine-specific) rather than a mockable seam.

**Conclusion:** the shared singleton is safe for concurrent reads. The lock in `get_service()`
guards **only** the lazy construction (so a burst of concurrent first-callers can't race into
building two `KnowledgeService` instances); it is released immediately after construction and
does **not** wrap `search()`/`ask()` — queries are not serialized. This matches the task's
instruction not to add a global lock that serializes all queries unless actually required.

## TDD

KnowledgeEngine had no test harness (`tests/` held only an empty `__init__.py`; no pytest config;
pytest not installed in `.venv`). Used stdlib `unittest` only — no new dependency — so tests run
directly with the KE venv python.

**Pre-existing namespace-collision landmine found and fixed as part of building the harness:**
the original `KnowledgeEngine/tests/knowledge_engine/__init__.py` stub directory shares its name
with the real `src/knowledge_engine` package. Under `python -m unittest discover`, the discoverer
imports every package it walks (including this stub) and registers it in `sys.modules['knowledge_engine']`
*before* any test module's own `sys.path` manipulation runs — so any test that does
`from knowledge_engine.models import ...` (or any module, like `ke_mcp_server.py`, that imports
`knowledge_engine.*`) silently binds to the **empty stub**, not the real package, and fails with
`ModuleNotFoundError: No module named 'knowledge_engine.models'`. This is a stdlib import-caching
behavior, not fixable via `sys.path` ordering. Fix: nested all test packages one level deeper
under `tests/unit/` (`tests/unit/knowledge_engine/`, `tests/unit/scripts/`) so the colliding name
is no longer a top-level dotted-import segment. Verified this resolves discovery (Red evidence
below was captured *after* this reorg, using the exact documented discover command).

- **Red:** `KnowledgeEngine/tests/unit/scripts/test_ke_mcp_server.py`, run via
  `KnowledgeEngine/.venv/Scripts/python.exe -m unittest discover -s KnowledgeEngine/tests -v`
  against the pre-fix `ke_mcp_server.py` (`create_service()` called directly in both tools, no
  `get_service`/`warm_service`): **7 tests, 3 failures + 4 errors** — 3 failures asserting
  `create_service` called once but was called twice (one per tool invocation); 4 errors
  `AttributeError: module 'ke_mcp_server' has no attribute 'get_service'/'warm_service'`.
- **Green:** after the fix, same command: **Ran 7 tests in 0.016s — OK**.
- **Refactor:** none needed beyond the initial implementation; kept green.

### What's covered vs. deferred

Covered (mocked `create_service`, no live chroma/Ollama needed):
- `knowledge_search`/`knowledge_ask` construct the service exactly once across multiple calls.
- Both tools share the same singleton instance (`get_service()` is idempotent, `is` identity
  checked).
- `warm_service()` constructs + runs one real search through the (mocked) service.
- `warm_service()` failure — both a `create_service()` exception and a `service.search()`
  exception — propagates (`assertRaises`), is not caught/swallowed.

Deferred (needs a live chromadb + Ollama, not exercised in the committed unit tests):
- Actual chromadb collection-open latency / embedder-load timing — measured manually instead
  (see "What the constructor actually does" above), not asserted as a unit test since it's an
  environment-dependent timing, not a behavior.
- Concurrency — verified empirically against the live on-disk collection (see Thread-safety
  finding above), not committed as an automated test (would require the live, gitignored
  `KnowledgeEngine/db`, making the test non-portable/flaky in CI without a fixture DB — flagging
  as a good follow-up if KE gets a proper fixture chroma collection).

## Test commands + results

```
cd C:\Jobs\ASPS\GitHub\Software
KnowledgeEngine\.venv\Scripts\python.exe -m unittest discover -s KnowledgeEngine\tests -v
```
Result (post-fix): `Ran 7 tests in 0.016s` — `OK` (0 failed, 0 errors, 0 skipped).

```
KnowledgeEngine\.venv\Scripts\python.exe -m py_compile KnowledgeEngine\scripts\ke_mcp_server.py KnowledgeEngine\tests\unit\scripts\test_ke_mcp_server.py
```
Result: no output, exit 0 (compiles clean).

### Functional smoke (live chromadb, mocked Ollama)

Ran an ad-hoc smoke script (not committed — scratchpad only) against the real on-disk
`KnowledgeEngine/db` collection (2585 chunks). No local Ollama daemon was running in this
environment (`curl http://localhost:11434/api/tags` → connection refused), so `knowledge_ask`'s
LLM call was monkeypatched on the already-constructed singleton's `llm_provider.generate`;
`knowledge_search` used the live collection unmocked.

```
[warm_service] ok, elapsed=2.586s
[knowledge_search #1] elapsed=0.618s len=1924
[knowledge_search #2] elapsed=0.472s len=1955
[create_service call count after warm+2 searches] 0 (expect 0 -- singleton already warmed)
[identity check] knowledge_search used the same KnowledgeService instance as warm_service()
[knowledge_ask #1] 'MOCKED ANSWER (no local Ollama daemon in this env)\n\nSOURCES:'
[knowledge_ask #2] 'MOCKED ANSWER (no local Ollama daemon in this env)\n\nSOURCES:'

SMOKE PASS: single KnowledgeService instance reused across warm + search + ask.
```

## Files changed

| File | Change |
|---|---|
| `KnowledgeEngine/scripts/ke_mcp_server.py` | Added `get_service()` singleton (lock-guarded lazy construction) + `warm_service()`; `knowledge_search`/`knowledge_ask` now call `get_service()`; `__main__` calls `warm_service()` before `mcp.run()`. |
| `KnowledgeEngine/tests/unit/scripts/test_ke_mcp_server.py` | New — singleton-reuse + warm/fail-loudly unit tests (stdlib `unittest`). |
| `KnowledgeEngine/tests/unit/scripts/__init__.py` | New — package marker. |
| `KnowledgeEngine/tests/unit/__init__.py` | New — package marker (namespace-collision fix container). |
| `KnowledgeEngine/tests/unit/knowledge_engine/__init__.py` | Moved from `KnowledgeEngine/tests/knowledge_engine/__init__.py` — collision-avoidance reorg (see TDD section). |
| `KnowledgeEngine/tests/__init__.py` | New — package marker for `tests/` itself (needed for clean `unittest discover`). |
| `KnowledgeEngine/scripts/ke_cli.py` | **Not touched** — confirmed still has its own independent `create_service()`. |

## Not in scope / not touched

- `ke_cli.py` ingest/index path — explicitly unaffected per task instructions.
- No retrieval-behavior change — same `search`/`ask` logic, same chunking/embedding, same
  chromadb queries. Pure construction lifecycle change.
- No `version.py` bump — KnowledgeEngine has no `version.py` (that convention is desktop-agent
  specific, `apps/desktop/win/src/version.py`); not applicable to this MCP tooling change.

## Spec/doc flags (not edited — CEO/Architect/TechWriter own spec edits per `task-workflow.md`)

- [`.claude/tools/knowledge-engine.md`](../../.claude/tools/knowledge-engine.md) — describes the
  MCP server's two tools and lifecycle at a high level (owner: knowledge-manager). Worth a line
  noting the server now warms the retrieval stack (chroma collection + embedder) once at startup
  and reuses one `KnowledgeService` for the process lifetime, so the previously-observed
  first-call `CONNECT_TIMEOUT` after a fresh server start should no longer occur.

## Pre-QA gate status

- [x] Build/import succeeds (`py_compile` clean).
- [x] Task tests exist and pass (7/7, stdlib `unittest`).
- [x] No uncommitted changes (after this handoff commit).
- [x] Latest `main` merged — branch was cut from `main` @ `3674052`, which was still
      `origin/main` HEAD at push time (`git fetch origin main` showed no new commits); no merge
      needed.
- [x] Tests re-run after confirming main freshness — same 7/7 pass.
- [ ] Pushed to remote — done by the implementing agent as the final step of this session.
- [x] Handoff updated (this file).
- [x] Spec documents reviewed — flagged `.claude/tools/knowledge-engine.md` above; not edited
      (implementer does not edit specs per `task-workflow.md`).

## Continuation point

Branch `asps-781-ke-mcp-warm-retrieval` pushed to remote. **Not merged.** Next: CEO/orchestrator
runs QA review, code review, and the mandatory security gate (see
`.claude/rules/task-workflow.md` — full review depth applies: this touches a running server's
startup/concurrency behavior). No PR opened per instructions ("Do NOT open a PR/merge — CEO runs
QA + code review + security gate").

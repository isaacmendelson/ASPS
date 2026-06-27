# Tool: Knowledge Engine

A local RAG-based knowledge system that indexes ASPS knowledge and answers questions with cited sources.

- **Location:** `C:\AI\Projects\KnowledgeEngine`
- **Provider:** Anthropic (`AnthropicProvider`) — requires `ANTHROPIC_API_KEY` in the environment.
- **Vector DB:** `C:\AI\Projects\KnowledgeEngine\db` (persisted; rebuilt with `--reset`).

## What it indexes

The repository is the single source of truth. Indexed paths (`run_knowledge_engine.py`):
- `C:\Jobs\ASPS\GitHub\Software\docs` — design docs, specs, audits
- `C:\Jobs\ASPS\GitHub\Software\.claude` — agents, workflows, rules, AIducation, ADRs

Supported file types: `.txt`, `.md`, `.docx`, `.pdf` (other files in those paths — e.g. `.json`, `.html` — are skipped).

> The standalone `KnowledgeEngine\documents` path is **disabled** — knowledge lives in the repo, not a separate copy. Last build: **654 chunks**.

## How to run

The script is an **interactive REPL** with no `--query` flag. Drive it headless via stdin,
and always force UTF-8 (the result-preview print crashes on box-drawing chars under Windows cp1252):

```bash
# Ask a question (headless)
printf 'YOUR QUESTION\nquit\n' | \
  PYTHONIOENCODING=utf-8 C:/AI/Projects/KnowledgeEngine/.venv/Scripts/python.exe \
  C:/AI/Projects/KnowledgeEngine/scripts/run_knowledge_engine.py

# Rebuild the index after changing docs/.claude content
printf 'quit\n' | PYTHONIOENCODING=utf-8 .venv/Scripts/python.exe scripts/run_knowledge_engine.py --reset
```

Each query returns retrieval debug (top-k chunks + scores), an LLM answer, and a sources list.

## When to use it

When an agent needs project knowledge, organizational rules, architecture decisions, or prior
lessons. Example questions:
- What is the CEO agent responsible for?
- What workflow is used for feature development?
- Describe the ASPS fraud protection system and its user layer.
- What rules apply to backend development?

## Caveats

- **UTF-8 required** — run with `PYTHONIOENCODING=utf-8` or the debug print can crash (`UnicodeEncodeError │`). Cosmetic bug in the tool's print loop, not the query.
- **Rebuild after edits** — the DB is static; new/changed docs are not reflected until `--reset`.
- **External path** — outside the ASPS repo; add `C:\AI\Projects\KnowledgeEngine` as a Claude Code working dir (`/add-dir`) to avoid permission prompts.

## Future integration

The Knowledge Engine will expose an API or **MCP server** so agents can query it directly
instead of shelling out. This is the integration point referenced by
[`../aiducation/learning-engine/README.md`](../aiducation/learning-engine/README.md) (Principle 7).
Owner: **knowledge-manager**.

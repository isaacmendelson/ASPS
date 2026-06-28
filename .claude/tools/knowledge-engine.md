# Tool: Knowledge Engine

A local RAG-based knowledge system that indexes ASPS knowledge and answers questions with cited sources.

- **Location:** `C:\AI\Projects\KnowledgeEngine`
- **Provider:** Anthropic (`AnthropicProvider`) — requires `ANTHROPIC_API_KEY` in the environment.
- **Vector DB:** `C:\AI\Projects\KnowledgeEngine\db` (persisted; rebuilt with `--reset`).

## What it indexes

The repository is the single source of truth. Indexed paths (`ke_cli.py`):
- `C:\Jobs\ASPS\GitHub\Software\docs` — design docs, specs, audits
- `C:\Jobs\ASPS\GitHub\Software\.claude` — agents, workflows, rules, AIducation, ADRs

Supported file types: `.txt`, `.md`, `.docx`, `.pdf` (other files in those paths — e.g. `.json`, `.html` — are skipped).

> The standalone `KnowledgeEngine\documents` path is **disabled** — knowledge lives in the repo, not a separate copy. Last build: **690 chunks**.

## How to use it — CLI

Use the Knowledge Engine CLI when project knowledge, agent definitions, workflows, rules, ADRs,
or AIducation knowledge are needed. The CLI (`ke_cli.py`) has three subcommands:
`ask` (LLM answer + cited sources), `search` (raw retrieval, no LLM), `index --reset` (rebuild).

```bash
cd C:\AI\Projects\KnowledgeEngine

# Primary: ask a question — LLM answer with cited sources
.venv\Scripts\python.exe scripts\ke_cli.py ask "QUESTION" --sources

# Raw retrieval only — top-k chunks + scores, no LLM call
.venv\Scripts\python.exe scripts\ke_cli.py search "QUESTION" --top-k 5

# Rebuild the index after docs/.claude content changes
.venv\Scripts\python.exe scripts\ke_cli.py index --reset
```

## When to use it

When an agent needs project knowledge, organizational rules, architecture decisions, or prior
lessons. Example questions:
- What is the CEO agent responsible for?
- What workflow is used for feature development?
- Describe the ASPS fraud protection system and its user layer.
- What rules apply to backend development?

## Caveats

- **CLI only, with Bash** — agents without a Bash tool (e.g. architect, product, qa) cannot call it directly until the MCP server exists (see below).
- **Rebuild after edits** — the DB is static; new/changed docs are not reflected until `index --reset`.
- **External path** — outside the ASPS repo; add `C:\AI\Projects\KnowledgeEngine` as a Claude Code working dir (`/add-dir`) to avoid permission prompts.
- **UTF-8 fallback** — if you hit `UnicodeEncodeError` on Windows, prefix the command with `PYTHONIOENCODING=utf-8`.

## Future integration

The Knowledge Engine will expose an API or **MCP server** so agents can query it directly
instead of shelling out. This is the integration point referenced by
[`../aiducation/learning-engine/README.md`](../aiducation/learning-engine/README.md) (Principle 7).
Owner: **knowledge-manager**.

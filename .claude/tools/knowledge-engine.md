# Tool: Knowledge Engine

A local RAG-based knowledge system that indexes ASPS knowledge and answers questions with cited sources.

- **Location:** `C:\Jobs\ASPS\GitHub\Software\KnowledgeEngine` (in-repo). The standalone `C:\AI\Projects\KnowledgeEngine` is deprecated.
- **Provider:** Anthropic (`AnthropicProvider`) — requires `ANTHROPIC_API_KEY` (loaded from `KnowledgeEngine\.env`, gitignored).
- **Vector DB:** `KnowledgeEngine\db` (gitignored; rebuilt with `index --reset`).

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
cd C:\Jobs\ASPS\GitHub\Software\KnowledgeEngine

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

- **Two access paths** — the **MCP server** (below; works for every agent) or the **CLI** (needs a Bash tool; agents like architect/product/qa have none).
- **Rebuild after edits** — the DB is static; new/changed docs are not reflected until `index --reset`.
- **Fresh clone** — `.venv/` is gitignored; recreate with `python -m venv .venv && .venv\Scripts\python.exe -m pip install -r requirements.txt`, and create `.env` from `.env.example`.
- **UTF-8 fallback** — the CLI's console print can hit `UnicodeEncodeError` on Windows; prefix with `PYTHONIOENCODING=utf-8`. (The MCP server is unaffected — it returns strings, not console prints.)

## MCP integration

An MCP server (`KnowledgeEngine/scripts/ke_mcp_server.py`, FastMCP over stdio) is registered in
the repo-root [`.mcp.json`](../../.mcp.json), exposing two tools: **`knowledge_ask`** and
**`knowledge_search`**. After a Claude Code restart + project MCP approval, **every** agent can
query the Knowledge Engine directly — no Bash/CLI shell-out needed. This closes the integration
point referenced by [`../aiducation/learning-engine/README.md`](../aiducation/learning-engine/README.md) (Principle 7).
Owner: **knowledge-manager**.

# Memory — ASPS AI Operating System

Project-local, durable working memory for the AI OS: state that must survive across sessions but belongs to the **repository**, not to any single user.

Examples of what lives here (once defined):
- Active initiatives and their status
- Cross-session decisions not yet promoted to an ADR
- Open questions and parking-lot items

> NOTE: This is distinct from per-user Claude Code memory and from per-role
> memory under `.claude/hats/<role>/`. Keep durable facts here; keep ephemeral
> conversation state out.

> TODO: Define the memory file format and index convention before first use.

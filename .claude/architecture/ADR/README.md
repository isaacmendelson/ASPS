# Architecture Decision Records (ADR)

Architecture decisions for ASPS are stored here as **Architecture Decision Records** —
one Markdown file per significant decision.

An ADR captures a single decision: its **context**, the **decision** itself, and its
**consequences**. ADRs are immutable once accepted — to change a decision, write a new
ADR that supersedes the old one (and mark the old one `Superseded`).

## Why ADRs
- The *reasoning* behind a choice survives, not just the choice.
- New contributors (human or agent) can read the history instead of re-deriving it.
- Reversals are explicit and traceable.

## Conventions
- **File name:** `NNNN-short-title.md` — zero-padded sequential number (e.g. `0001-use-netmq-curve.md`).
- **Status:** `Proposed` → `Accepted` → `Superseded` / `Deprecated`.
- **Template:** copy [0000-template.md](0000-template.md) to start a new record.

## Index
| ADR | Title | Status |
|---|---|---|
| 0000 | Template | — |

> TODO: Add ADRs here as decisions are made or reconstructed.

# AIducation — Learning Engine

The learning backbone of the AI OS: the mechanism that ingests completed-task signals,
stores structured knowledge, and feeds it back to agents (retrieval, role-training updates).

> **Principle 7:** the **Knowledge Engine currently under development** becomes part of this
> AI Operating System and is integrated here. This folder is the integration point —
> **reference** that engine, do not duplicate it.

## Responsibilities (once integrated)
- Ingest learning triggers from the `knowledge-update` workflow.
- Persist lessons/principles/prompts against the AIducation schemas.
- Serve retrieval to agents (the right knowledge at the right moment).
- Track which lessons changed which role-training.

## Open items
- **TODO (blocking):** record the in-development Knowledge Engine's location/interface here,
  then link it. Owner: Knowledge Manager.
- TODO: define the ingest + retrieval contract.

## See also
- Owner: [../../agents/knowledge-manager.md](../../agents/knowledge-manager.md)
- Workflow: [../../workflows/knowledge-update.md](../../workflows/knowledge-update.md)
- Schemas: [../schemas/](../schemas/)

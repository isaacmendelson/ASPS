# ASPS-770 — Freeze the MASDP Project-Config Contract — Handoff

**JIRA:** ASPS-770 (story 1/8 of epic ASPS-769, MASDP/SPS separation). Epic authority: [`ADR-006-MASDP-SPS-SEPARATION.md`](../architecture/decisions/ADR-006-MASDP-SPS-SEPARATION.md).
**Agent:** devops
**Branch:** `asps-770-project-config-contract`
**Status (this session):** Implementation complete, pre-QA gate steps 1–2 done, verification evidence recorded below. **Not yet pushed / no PR opened** — CEO runs QA + code review + security gate per this story's own instructions ("Do NOT open a PR/merge — CEO runs QA + code review + security gate and merges").

## What this story is

Design-only / config artifact. Freezes ADR-006's "Project-config contract"
(decision 1) into a concrete, JSON-Schema-validatable shape:
`{ name, github_repo, jira_project, working_dir, domain_context, secrets_ref }`.
No code extraction, no repo creation, no runtime wiring — those are stories
771+ in the same epic. Does NOT re-open any ADR-006 decision.

## Files created (all new, nothing else touched)

| File | Purpose |
|---|---|
| [`docs/architecture/contracts/projects.schema.json`](../architecture/contracts/projects.schema.json) | JSON Schema (2020-12) for `projects.json`. Top-level `{schema_version, projects[]}`; each record requires exactly the 6 ADR-006 fields, `additionalProperties: false`. |
| [`docs/architecture/contracts/projects.example.json`](../architecture/contracts/projects.example.json) | Valid example: `masdp` (tenant #0) + `sps` (project #1). |
| [`docs/architecture/contracts/projects.invalid.example.json`](../architecture/contracts/projects.invalid.example.json) | Deliberately malformed negative-test fixture only — never a template. |
| [`docs/architecture/contracts/validate_projects.py`](../architecture/contracts/validate_projects.py) | Validation check script (positive must pass, negative must fail; exit 0 only if both behave as expected). |
| [`docs/architecture/contracts/SECRETS-REF-BUNDLE-LAYOUT.md`](../architecture/contracts/SECRETS-REF-BUNDLE-LAYOUT.md) | `secrets_ref` → on-host bundle directory layout/naming spec, generalizing `SECRETS_DIR`. No secret values. |
| [`docs/architecture/contracts/PROJECT-CONFIG-CONTRACT.md`](../architecture/contracts/PROJECT-CONFIG-CONTRACT.md) | The tying-together contract doc — principle, path-choice rationale, field-by-field example walkthrough, verification evidence, explicit out-of-scope list. |

## Path choice

`docs/architecture/contracts/` (not repo-root `contracts/masdp/`) — colocated
with the ADR-006 decision that mandates it, under the already-KE-indexed
`docs/` tree, parallel to the existing `decisions/`/`messaging/`
subdirectories. Explicitly **staging** — the canonical copy moves into the
MASDP repo in ASPS-771/772; the schema's own description field says so.
Full rationale in `PROJECT-CONFIG-CONTRACT.md` "Path choice, and why".

## Marked TBD (for later stories — not resolved here)

- `masdp.github_repo` — literal placeholder `TBD-ASPS-771/masdp`; real
  owner/repo decided in **ASPS-771**.
- `masdp.jira_project` — literal placeholder `TBD`; **gap flagged**: ADR-006's
  extraction order does not currently assign "create MASDP's own JIRA
  project + pick its key" to a numbered step. CEO/Architect should assign
  this to a story (likely alongside ASPS-771).

Everything else in the example uses real, currently-true values (SPS's
actual repo/JIRA key/working dir today) or a deliberate, documented
*convention* (MASDP's `working_dir`/`domain_context` under a future
projects-root layout, `secrets_ref` bundle names) that this story is
entitled to define now, per its own scope.

## Verification (TDD rule item 9 — declarative config, not unit Red/Green)

Documented in full, with exact command and captured output, in
`PROJECT-CONFIG-CONTRACT.md` "Verification" section. Summary:

```
python docs/architecture/contracts/validate_projects.py
```
- **Positive:** `projects.example.json` → 0 validation errors. PASS.
- **Negative:** `projects.invalid.example.json` → 6 validation errors across
  `additionalProperties`, `required` (×2), `type`, and `pattern` (×2)
  mechanisms. PASS (fails as required).
- Exit code `0` (script's own combined pass/fail gate).
- Verified against the repo's global `python` (jsonschema 4.25.1) and cross-
  checked `KnowledgeEngine/.venv/Scripts/python.exe` (jsonschema 4.26.0) has
  `jsonschema` available too, per `CLAUDE.md`'s Python-interpreter note.

No conventional unit TDD applies here — see `PROJECT-CONFIG-CONTRACT.md`
"Verification" for the explicit justification (CLAUDE.md TDD rule item 9).

## Pre-QA gate status

1. ✅ Branch `asps-770-project-config-contract` created off latest `main` (was clean, up to date with origin/main at session start).
2. ✅ Schema-validation check run — evidence above / in `PROJECT-CONFIG-CONTRACT.md`.
3. ⬜ Commit — next step (this session, immediately after this handoff write).
4. ⬜ Merge latest `main` (should be a no-op — nothing else landed on `main` since branch creation) + push branch.
5. ⬜ CEO runs QA + code review + security gate (per this story's explicit instruction — no PR opened by this agent).

## Continuation point

1. Commit with header `ASPS-770 Freeze the MASDP project-config contract + registry/secrets_ref shape` (body + `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`).
2. `git fetch && git merge origin/main` (expect no-op/clean), re-run `validate_projects.py` if `main` moved.
3. `git push -u origin asps-770-project-config-contract`.
4. Hand back to CEO for QA + code review + security gate. Security gate here is realistically a **"no security impact" determination** candidate (doc/schema/example-data only, no code, no secrets, no infra, no auth/permission-model change) — but that determination must still be made explicitly by the reviewer, not silently skipped, per `task-workflow.md`'s security-gate section.
5. No JIRA transition performed by this agent in this session — CEO/whoever owns the JIRA workflow should set ASPS-770 label `devops`, transition to appropriate state, and propagate epic ASPS-769 to In Progress if this is its first child work (per `task-workflow.md` "Parent issue status propagation") — not yet checked/done in this session (no JIRA tool call made).

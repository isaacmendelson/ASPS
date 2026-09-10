# MASDP Project-Config Contract — ASPS-770

Status: **locked** — this freezes ADR-006's "Project-config contract" and
decision 1 into a concrete, validatable shape. Story 1/8 of the MASDP/SPS
separation epic (ASPS-769). Design-only / config artifact; does not extract
any code or create any repo.

Authority: [`ADR-006-MASDP-SPS-SEPARATION.md`](../decisions/ADR-006-MASDP-SPS-SEPARATION.md)
(esp. "Project-config contract" and locked decisions 1, 3, 8). This document
does not re-open any ADR-006 decision — it formalizes decision 1.

## The principle

> **MASDP carries the generic "how"; each project carries its own "what".
> MASDP is also tenant #0** — it manages itself as a project (dogfooding),
> the same registry entry shape as every other managed project, no special
> case in the schema.

Concretely: MASDP (the platform — `.claude/` operating system, Telegram CEO
bot, Knowledge Engine, `deploy/vps`) is stack-agnostic and process-generic.
Everything project-specific — which repo, which JIRA project, where the
clone lives, where its domain instructions/role-pack live, which credential
bundle to load — is a **data record**, not something hardcoded into MASDP
itself (breaking coupling point C1 from the
[boundary inventory](../MASDP-SPS-BOUNDARY-INVENTORY.md)).

## The artifacts this story produces

| Artifact | Path | Purpose |
|---|---|---|
| Registry schema | [`projects.schema.json`](projects.schema.json) | JSON Schema (2020-12) for `projects.json` — the six-field record ADR-006 decision 1 locked: `{ name, github_repo, jira_project, working_dir, domain_context, secrets_ref }`. |
| Documented example (valid) | [`projects.example.json`](projects.example.json) | Two records: MASDP as tenant #0, SPS as project #1. Field-by-field walkthrough below. |
| Negative fixture (invalid) | [`projects.invalid.example.json`](projects.invalid.example.json) | Deliberately malformed — used only to prove the schema actually rejects bad input. Never a template. |
| Validation check | [`validate_projects.py`](validate_projects.py) | Runs both fixtures against the schema; positive must pass, negative must fail. See "Verification" below. |
| Secrets bundle layout | [`SECRETS-REF-BUNDLE-LAYOUT.md`](SECRETS-REF-BUNDLE-LAYOUT.md) | How `secrets_ref` resolves to an on-host directory of per-project credential files, outside every clone. No secret values. |

## Path choice, and why

Placed under `docs/architecture/contracts/` rather than repo-root
`contracts/masdp/`:

- `docs/architecture/` already holds the design authority for this work
  (ADR-006, the boundary inventory) — colocating the frozen contract next to
  the decision that mandates it keeps one place to look, and this directory
  is already indexed by the Knowledge Engine (`docs/` is a KE source root —
  see `docs/PROJECT_CONTEXT.md` §6).
- A `contracts/` subdirectory (parallel to the existing `decisions/` and
  `messaging/` subdirectories under `docs/architecture/`) signals "frozen,
  validatable interface" distinctly from prose ADRs/specs, without
  inventing a new top-level repo directory for a single story's output.
- This is explicitly **staging**, not final placement: per ADR-006
  extraction order and this story's own instructions, the canonical copy of
  `projects.json` + its schema moves into the **MASDP repo** once it exists
  (ASPS-771/772). Nothing under `docs/architecture/contracts/` should be
  treated as the live/authoritative registry after that migration — this
  location is "author here now, relocate later," not a second permanent
  home. The schema's own `description` field says the same thing so it
  survives being read out of context.

## The example, field by field

See [`projects.example.json`](projects.example.json) for the full JSON. Two
records:

### `masdp` (tenant #0)

| Field | Value | Status |
|---|---|---|
| `name` | `masdp` | Real — the project slug MASDP uses for itself. |
| `github_repo` | `TBD-ASPS-771/masdp` | **TBD.** The `TBD-ASPS-771` segment is a deliberate, unmistakable placeholder (not a real GitHub org) — the actual MASDP repo owner/name is decided in **ASPS-771** per this story's own instructions. Kept schema-valid (`owner/repo` shape) so the example still validates while the placeholder is visually obvious on read. |
| `jira_project` | `TBD` | **TBD.** ADR-006's extraction order (§ "Extraction order") does not currently assign "create MASDP's own JIRA project + pick its key" to a numbered step — flagged here as a gap for the CEO/Architect to assign to a story (likely alongside ASPS-771's repo creation, since both are "give MASDP its own external-service identity"). `TBD` itself is a valid `jira_project` value under this schema (3 uppercase letters) precisely so this gap is visible in a validating example rather than hidden behind a schema-breaking sentinel. |
| `working_dir` | `./masdp` | Convention, not a fact-in-waiting — relative-to-projects-root layout ADR-006 implies once the bot is multi-tenant (extraction-order step 6), independent of what the eventual repo is named. |
| `domain_context` | `./masdp/CLAUDE.md` | Convention (relative to `working_dir`), same reasoning as above — MASDP's own operating-charter entry point once split (ADR-006 decision 6/extraction-order step 3). |
| `secrets_ref` | `masdp` | Real — decided now, as part of this contract: the bundle subdirectory name for MASDP's own credentials (see the layout doc). Not contingent on the repo name. |

### `sps` (project #1)

| Field | Value | Status |
|---|---|---|
| `name` | `sps` | Real. |
| `github_repo` | `isaacmendelson/ASPS` | Real — today's actual repo. |
| `jira_project` | `ASPS` | Real — today's actual JIRA project key. |
| `working_dir` | `C:\Jobs\ASPS\GitHub\Software` | Real — today's actual clone path on the current dev box (the pre-split state; ADR-006 extraction-order step 7, "Register SPS as project #1," is what will eventually re-home this under a MASDP-managed projects root — that step is not yet done, so the current real path is the correct "real value where known" per this story's instructions). |
| `domain_context` | `C:\Jobs\ASPS\GitHub\Software\CLAUDE.md` | Real — today's actual entry point. |
| `secrets_ref` | `sps` | Real — decided now: the bundle subdirectory name for SPS's credentials, generalizing the existing single-tenant `SECRETS_DIR` (today `SECRETS_DIR` *is* SPS's bundle with no subdirectory, since there's only one project — see the layout doc). |

## Verification (declarative config — TDD rule item 9)

This is a JSON Schema + example data, not executable application logic —
per `CLAUDE.md`'s TDD rule item 9, "generated code, documentation-only
changes, and purely declarative configuration may use validation or
contract checks instead of unit-level Red/Green... document why and use the
strongest automated verification available." Conventional Red/Green does
not apply here: there is no production code path to drive through a
failing test first — the schema *is* the specification, and validating real
and deliberately-invalid data against it *is* the strongest available
automated check for a JSON Schema artifact.

**Command:**
```bash
python docs/architecture/contracts/validate_projects.py
```
(Any Python with `jsonschema` installed works — verified against both the
repo's global `python` and `KnowledgeEngine/.venv/Scripts/python.exe`.)

**Evidence — positive (valid example passes):**
```
[POSITIVE] projects.example.json against projects.schema.json
  PASS — 0 validation errors (expected).
```

**Evidence — negative (malformed fixture fails, with concrete errors):**
```
[NEGATIVE] projects.invalid.example.json against projects.schema.json
  PASS — validation correctly FAILED with 6 error(s):
    - []: Additional properties are not allowed ('_comment_for_humans' was unexpected)
    - ['projects', 1]: 'domain_context' is a required property
    - ['projects', 1]: 'secrets_ref' is a required property
    - ['projects', 1, 'github_repo']: 12345 is not of type 'string'
    - ['projects', 1, 'jira_project']: 'asps-not-uppercase' does not match '^[A-Z][A-Z0-9]{1,9}$'
    - ['projects', 1, 'working_dir']: 'relative/without/leading-dot-or-drive' does not match '^(?:[A-Za-z]:[\\\\/]|/|\\./).+'

RESULT: PASS (positive passes, negative fails as expected)
```
(`RESULT: PASS` here means "the check behaved correctly" — i.e. valid data
validated cleanly AND malformed data was rejected — not that the malformed
data validated. Exit code `0` in both runs, captured together via the
script's own combined summary; see `validate_projects.py`'s docstring for
the exact pass/fail semantics.)

The negative fixture exercises three distinct schema mechanisms in one run:
`additionalProperties: false` (top-level), `required` (missing
`domain_context`/`secrets_ref`), `type` (numeric `github_repo`), and
`pattern` (bad `jira_project` casing, bad `working_dir` shape) — so a
future accidental schema-weakening (e.g. dropping a `required` entry) has a
concrete regression check to fail against, not just an eyeball review.

## What is explicitly out of scope here (no scope creep)

- No MASDP repo is created (ASPS-771).
- No code is extracted from this repo (ASPS-772+).
- No runtime code reads `projects.json` yet — the Telegram bot's `WORKING_DIR`
  coupling (C1) is unchanged; this contract is the shape that future work
  maps onto (extraction-order step 6).
- No secret bundle is created on any host — [`SECRETS-REF-BUNDLE-LAYOUT.md`](SECRETS-REF-BUNDLE-LAYOUT.md)
  is layout/naming only.
- The MASDP JIRA-project-key gap noted above is flagged, not resolved, here.

# AGENTS.md — ASPS Project Instructions

This file is auto-loaded by Codex at session start. It is the entry point to the **hat-based working system** for this project.

---

## At session start

1. Read **`docs/PROJECT_CONTEXT.md` completely** — the mandatory shared context for ASPS, including the product, components, specification index, Knowledge Engine, external resources, source-of-truth order, and task-memory workflow. Applies to every role, every task, and every session.
2. Identify the current Codex task and read its matching **`docs/task-memory/<TASK_NAME>_HANDOFF.md`** when one exists. If the match is ambiguous, ask the user instead of guessing or overwriting another task's memory.
3. Read **`.Codex/team/CHARTER.md`** — the team-wide behavioral charter (ethics, thinking, priorities, conduct). Applies to every role, every session.
4. Read **`.Codex/hats/ceo/INDEX.md`** — that file lists what to read next as the CEO (the default hat).
5. Then read each file the INDEX points to, in order.
6. Only after that, address the user's first message.

The CEO is always the default. Other roles (CTO, Backend, Frontend, Python, Mobile, QA, Security) are spawned as sub-agents when needed — their role charter is `.Codex/agents/<role>.md` and their accumulated memory is `.Codex/hats/<role>/`. Every role reads `docs/PROJECT_CONTEXT.md`, its relevant task handoff, and `.Codex/team/CHARTER.md`.

---

## Stack quick reference

| Layer | Stack |
|---|---|
| Backend | .NET 8 + EF Core 7 (→ 8 planned) + MySQL via Pomelo |
| Messaging | NetMQ 4.0.1.13 with CURVE encryption |
| Admin UI | Razor Pages + Keycloak SSO |
| Desktop agent | Python 3.11 + pyzmq + websockets |
| Browser extension | Chrome MV3 (vanilla JS, no framework) |
| Mobile (planned) | Android (Kotlin), iOS (Swift) |
| Database | MySQL 8 |

## Ports / messaging

| Port | Purpose | Protocol |
|---|---|---|
| 50001 | Real-time alert listener (device → backend) | NetMQ ROUTER + CURVE |
| 50002 | Notification publisher (backend → admin) | NetMQ PUB + CURVE |
| 5555 | NetMQ business endpoint | NetMQ — bound to `*:` (security debt) |
| 5556 | CQRS gateway (WebApi ↔ Backend) | NetMQ — bound to `*:` (security debt) |
| 5001 | WebApi HTTP | Razor Pages |
| 5002 | WebApi HTTPS | Razor Pages |
| 3306 | MySQL | exposed in docker-compose (security debt) |
| 8080–8484 | Local WebSocket between extension and Python agent (scans `[8080, 8181, 8282, 8383, 8484]`) | `ws://` (security debt) |
| 8180 | Local Keycloak (dev only — OIDC for WebApi + ASPSBackend) | `http://` |

## Repo layout

```
c:\Jobs\ASPS\GitHub\Software\
├── ASPSBackend14_J\              ← .NET solution
│   ├── ASPSBackend\             ← Backend host service (NetMQ + EF)
│   ├── WebApi\                  ← Admin UI + REST + Keycloak
│   ├── Business\                ← Domain logic, CQRS, repositories
│   ├── Common\                  ← Entities, DTOs, value objects, enums
│   ├── Interface\               ← Repository contracts
│   └── ASPS.Tests\              ← Unit / integration tests
├── apps\
│   ├── extension\chrome\        ← Browser extension (MV3)
│   └── desktop\win\             ← Python desktop agent
├── Analyzers\                   ← Python analyzer microservices
├── docs\                        ← ARCHITECTURE.md, ASPS_DATA_FLOW.md, PRODUCT.md
│   └── security-audits\         ← Daily audit reports
├── .Codex\
│   └── hats\                    ← Per-role memory (this system)
└── _inbox\                      ← gitignored, for staging imports
```

---

## Working style — universal rules

### GSD — Get Shit Done
- Don't talk, do
- Don't apologize, fix
- Don't explain why-not, explain how-yes
- Tests > assumptions
- Done = built, tested, behaving in real use

### Mode B — phased execution
For any multi-phase task: execute one phase → stop → wait for "מאשר" / "תמשיך" before next.

### TDD — mandatory for implementation agents
All agents that change production code must use Test-Driven Development:

1. **Translate acceptance first:** before production-code changes, turn the Jira
   acceptance criteria, security invariants, and failure modes into an explicit
   test checklist. For cross-component work, include contract and end-to-end
   scenarios, not only unit tests.
2. **Red:** add or modify the smallest relevant automated test and run it. Record
   evidence that it fails for the intended missing behavior or regression.
3. **Green:** make the smallest production-code change that makes that test pass.
4. **Refactor:** improve structure without changing behavior, keeping the relevant
   test suite green.
5. Repeat Red → Green → Refactor in small slices. Do not implement an entire Jira
   issue and add tests afterward.
6. Security work must begin with negative tests for bypass, unauthenticated input,
   tampering, replay, malformed data, unauthorized actions, and fail-open behavior
   whenever they apply.
7. Bug fixes must begin with a regression test that reproduces the bug. Legacy
   behavior that is not yet testable must first receive a characterization test or
   a documented test seam.
8. Never weaken, delete, skip, or rewrite a valid failing test merely to obtain
   Green unless the requirement itself changed and the CEO/root explicitly
   confirms that change.
9. Generated code, documentation-only changes, and purely declarative configuration
   may use validation or contract checks instead of unit-level Red/Green. The agent
   must document why conventional TDD does not apply and use the strongest
   automated verification available.
10. The implementation handoff must include the acceptance-test checklist, Red
    evidence, Green evidence, refactoring performed, and exact final test commands
    with passed/failed/skipped counts. Missing Red evidence requires an explicit
    justification and CEO/root approval before QA.

### Persistent active handoff
- Keep one cross-session handoff file per Codex task under `docs/task-memory/`.
- Name it `docs/task-memory/<TASK_NAME>_HANDOFF.md`, using a stable filesystem-safe form of the task name.
- Never use one shared `ACTIVE_HANDOFF.md` for multiple tasks; parallel sessions must not overwrite each other's memory.
- At the start of a new chat or session, identify the current task and read its matching handoff before continuing work.
- If no unambiguous matching handoff exists, list the relevant candidates and ask the user which task to resume instead of guessing or overwriting a file.
- If the Codex task is renamed, rename its handoff file in the same phase and update internal title, task-name metadata, and references to the old handoff path.
- Update the task handoff at the end of every significant phase and before every planned session ending.
- The handoff must record the task name and identifier when available, completed work, changed files, verification results, decisions, uncompleted work, and the exact continuation point for the next agent.
- Keep one canonical handoff per task and update it in place instead of creating a new version per session.
- `docs/task-memory/` is used because the Knowledge Engine indexes `docs/`; `.codex/` is not currently an indexed knowledge source.
- An unexpected application or session failure can occur before the final update, so phase-end updates are mandatory.

### QA gate before merge
Non-trivial code changes require PASS from QA agent before commit. See `.Codex/hats/ceo/operating_principles.md`.

For Jira-backed implementation work, the following completion workflow is mandatory:

1. The implementing agent reports completion to the CEO/root agent with the Jira issue ID and exact title, changed files, implementation summary, and verification results.
2. Before requesting QA, the implementing agent must run all unit tests relevant to the changed code. The handoff to QA must include the exact test commands, passed/failed/skipped counts, and the final result.
3. The implementing agent must not hand work to QA while a relevant unit test is failing. A failure may be treated as pre-existing only when the agent documents it, reproduces it without the task changes or provides equivalent baseline evidence, and demonstrates that the task did not introduce or worsen it.
4. If the component has no relevant unit tests or the test environment cannot run, the implementing agent must document the gap and run the strongest available alternative verification. The CEO/root agent decides whether the evidence is sufficient to enter QA; absence of tests is not an automatic pass.
5. After the pre-QA test gate passes, the implementing agent must request an independent QA-agent review against the Jira acceptance criteria and the original requirement.
6. If QA returns `FAIL`, the issue returns to implementation and must be reviewed again after the fixes. The implementing agent must rerun the relevant unit tests before every QA resubmission. A previous PASS does not cover later material changes.
7. The CEO/root agent independently verifies the reported files, test evidence, and the final QA result.
8. A Jira issue must not be treated as complete or moved to `Done`, and its code must not be committed, until the CEO/root agent has confirmed a documented `QA PASS`.
9. When a commit is requested or otherwise authorized, the CEO/root agent owns the commit and its message. The commit message must contain:
   - the Jira issue ID;
   - the exact Jira issue title;
   - a concise description of the implementation included in the commit.
10. Preferred commit-message format:

   ```text
   <JIRA-ID> <Exact Jira issue title>

   <Concise description of the implemented changes and relevant verification>
   ```

11. The CEO/root agent records the commit hash and QA PASS evidence in the Jira issue before moving it to `Done`.

### Trust-but-verify
When a sub-agent reports work done — open the actual files and confirm before relaying to user.

### No silent side-fixes
If I notice another bug while doing X — mention it, ask whether to address, don't bundle silently.

### Destructive operations — confirm first
`rm -rf`, `git reset --hard`, `git push --force`, `DROP TABLE`, etc. — always confirm.

---

## Communication style

- **Hebrew default** when user writes Hebrew; mix English for technical terms.
- **Short.** Tables and bullets > paragraphs.
- **Markdown links** for file refs: `[file.cs:42](file.cs#L42)`. Never bare backticks alone for clickability.
- **No preambles**, no flattery, no "let me know if you need anything else".
- **Direct openers**: "התיקון:", "הבעיה:", "ההצעה:", "ממצאים:".

Full style guide: `.Codex/hats/ceo/communication.md`.

---

## Build & migrations

```bash
# Build all
cd ASPSBackend14_J && dotnet build ASPSBackend.sln -c Debug --nologo

# Single project
dotnet build Business/Business.csproj -c Debug --nologo

# Add migration
dotnet ef migrations add <Name> --project Business --startup-project ASPSBackend

# Apply migrations
dotnet ef database update --project Business --startup-project ASPSBackend

# DB CLI
"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe" -h 127.0.0.1 -P 3306 -uroot -pzappa22 ASPSBackend2DB -e "SELECT ..."
```

When build output shows `MSB3027` / `MSB3021` (file lock) — compilation succeeded; only DLL copy failed because a process holds the file. Look for `error CS####` to find real errors.

---

## Hat system — quick map

| Hat | When to wear | Where its memory lives |
|---|---|---|
| **CEO** (me, default) | Always at session start; coordination, simple tasks, user dialog | `.Codex/hats/ceo/` |
| **CTO** (sub-agent) | Architecture decisions, cross-cutting design, spec breakdown | `.Codex/hats/cto/` (TBD) |
| **Backend programmer** (sub-agent) | C# / EF / NetMQ / DI / migrations | `.Codex/hats/backend/` (TBD) |
| **Frontend programmer** (sub-agent) | Razor pages / CSS / JS / extension UI | `.Codex/hats/frontend/` (TBD) |
| **Python programmer** (sub-agent) | Desktop agent / analyzers | `.Codex/hats/python/` (TBD) |
| **Mobile programmer** (sub-agent) | Android / iOS — when those start | `.Codex/hats/mobile/` (TBD) |
| **QA** (sub-agent) | Verify code before merge — mandatory for non-trivial | `.Codex/hats/qa/` (TBD) |

Routing rules: `.Codex/hats/ceo/delegation.md`.

---

## Paths I never touch without explicit user approval

- `aspsbackend2db_*.sql` (DB seed dump)
- Anything under `.git/` (other than committing when asked)
- `_inbox/` (user's staging area)
- Production secrets in any `appsettings*.json` (read OK, write/commit not OK without rotation plan)
- Force operations on `main`/`master` branch

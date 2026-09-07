# Task Workflow — Branching, QA, Merge, and JIRA

Binding workflow for all development tasks. Every agent that changes code — developers, QA, DevOps, CISO — must follow this process.

---

## Branch per task

- Every development task or bug fix runs on a **dedicated branch**, never directly on `main`.
- Branch naming: `<JIRA-ID>-<task-title>` — spaces replaced with hyphens, lowercase.
  - Example: `ASPS-627-split-orchestrators-observability`
- The agent that receives the task creates the branch before writing any code.

## Multi-agent work on one story

When multiple agents work on the same feature or story:

- The **orchestrator (CEO)** decides a single branch name at the story level.
- The orchestrator assigns one agent to create the branch.
- All other agents on that story receive the branch name and work on it.
- Agents coordinate via the orchestrator to avoid conflicts.

## Development flow

```
1. Orchestrator assigns task → agent receives branch name (or creates it)
2. Agent works on the branch
3. Agent completes pre-QA gate (see below)
4. Agent notifies orchestrator "ready for QA" with summary
5. Orchestrator launches QA agent to review on the branch
6. QA PASS + main unchanged → agent opens Merge Request (PR)
7. Agent transitions JIRA to "In Review" (transition ID 31)
8. Orchestrator does code review (or delegates — see below)
9. Orchestrator runs the SECURITY GATE (see below) — mandatory before merge
   ├─ Code review approved AND security PASS → merge to main → JIRA "Done" (41)
   └─ Not approved / security FAIL → return to agent → JIRA "In Progress" (21)
```

## Pre-QA gate — mandatory before requesting QA

The implementing agent must complete **all** of the following before notifying the orchestrator that work is ready for QA:

1. **Build succeeds** — the full component builds without errors.
2. **Task tests exist and pass** — all unit tests written for this task pass.
3. **All component tests pass** — run the full test suite for the component (`.NET`: `dotnet test`; Python: `pytest`; JS: `jest`). All tests must pass. A failure may be treated as pre-existing only when the agent documents it, reproduces it without the task changes, and demonstrates that the task did not introduce or worsen it.
4. **No uncommitted changes** — all work is committed to the task branch. No loose files.
5. **Merge latest main** — pull the latest `main` from the remote, merge it into the task branch, and verify the build still succeeds.
6. **Re-run all tests** — after the merge, run the full test suite again. All tests must pass.
7. **Push to remote** — push the task branch to GitHub (or the configured remote).
8. **Notify orchestrator** — report "ready for QA" to the orchestrator with: JIRA issue ID, changed files, implementation summary, test commands with pass/fail/skip counts.
9. **Specification documents updated** — see [Specification update rule](#specification-update-rule) below. The agent must review and update any affected spec/design documents before requesting QA.

**Merge conflict escalation:** if merging `main` into the branch causes conflicts, the agent attempts to resolve them. If resolution is not straightforward, the agent escalates to the orchestrator with the conflict details.

**Handoff update:** the agent must update `docs/task-memory/<TASK>_HANDOFF.md` as part of the pre-QA process — status, changed files, test results.

## Post-QA and merge request

After QA returns **PASS**:

1. **Check main freshness** — if `main` has advanced since the last merge, repeat steps 5–7 of the pre-QA gate (merge, test, push). Then re-request QA only if the merge introduced non-trivial changes.
2. **Open Merge Request (PR)** — reviewer = orchestrator (CEO).
3. **Transition JIRA to In Review** (transition ID 31).

After QA returns **FAIL**:

1. The issue returns to the implementing agent with the QA findings.
2. The agent fixes, re-runs the full pre-QA gate, and re-requests QA.

## Code review

- The **orchestrator (CEO)** is the default reviewer.
- The orchestrator may delegate review to another agent (e.g., architect, or a peer developer agent).
- The reviewer must follow the code review guide in [review-standards.md](review-standards.md).
- Approved → proceed to the **security gate** (below), then merge.
- Not approved → orchestrator returns to agent with findings and transitions JIRA to In Progress.

## Security gate — mandatory before every merge to `main`

**No change reaches `main` without a security review PASS.** The orchestrator runs the **security** agent against the branch (using the security-review guide in [review-standards.md](review-standards.md) and [security-rules.md](security-rules.md)) after code review and before merge. This is a third required gate alongside QA and code review — it is not optional and not left to case-by-case judgment.

- **Verdict rule:** any **Blocker or Major** security finding → the merge is **blocked**; return to the agent (JIRA → In Progress) to remediate, then re-run the full gate (security + QA re-check as needed). Minor/Nit → may merge with the findings tracked as follow-ups.
- **Depth scales with the change, but the gate never does.** For changes touching code, configuration, infrastructure, dependencies, authentication/authorization, secrets, network exposure, data handling, or the permission model — a **full security review** is required. For genuinely security-inert changes (documentation-only, comments, non-executable content), the orchestrator (or security agent) records an explicit **"no security impact"** determination in the PR/handoff — that determination is itself the gate being satisfied, and must be a deliberate call, never a silent skip.
- **Independence:** the security reviewer verifies independently against `security-rules.md`; a developer agent's self-assessment does not satisfy the gate.
- **Evidence:** the security verdict (PASS / findings table with severity + exploit path + `file:line` + remediation, or the "no security impact" note) is recorded before the merge and mirrored into the task handoff. For infrastructure/box changes, capture the audit under `docs/security-audits/`.
- **Applies to all mergers**, including the orchestrator's own changes and hotfixes. A time-critical hotfix may merge on an expedited security review, but never with no security review at all.

## Commit message format

```
<JIRA-ID> <Exact Jira issue title>

<Concise description of the implemented changes and relevant verification>
```

## Responsibilities

| Role | Responsibility |
|---|---|
| **Orchestrator (CEO)** | Assigns tasks, decides branch names for multi-agent stories, does code review, runs the security gate, approves/rejects merges, transitions JIRA to Done |
| **Developer agent** | Creates branch, implements, runs tests, requests QA, opens PR after QA PASS, transitions JIRA to In Review |
| **QA agent** | Reviews on the branch, returns PASS/FAIL with evidence |
| **Security agent (CISO)** | Runs the mandatory pre-merge security gate; returns PASS or findings (severity + exploit path + file:line + remediation). Reviews, does not fix. |
| **DevOps / CISO** | Same branching rules when changing code or infrastructure |

## JIRA transitions

| Transition | ID | Who triggers |
|---|---|---|
| To Do → In Progress | 21 | Orchestrator (on assignment) or implementing agent (on start) |
| In Progress → In Review | 31 | Developer agent (after QA PASS + PR opened) |
| In Review → Done | 41 | Orchestrator (after code review approval + merge) |
| In Review → In Progress | 21 | Orchestrator (if code review fails) |

## Agent labels on JIRA issues

Every agent that works on a JIRA issue must add its agent name to the issue's **Labels** field before starting work. This makes it clear which agent handled each issue.

- When an agent receives a task, it sets the label to its own role name (e.g., `devops`, `backend`, `security`).
- If multiple agents work on the same issue, all agent names appear as labels.
- The orchestrator sets labels when creating sub-tasks if the assignee agent is known.

## Parent issue status propagation

When a JIRA issue has child issues (epic → stories, story → sub-tasks):

- **The parent issue must transition to In Progress when work begins on its first child issue** — not when the user asks. This applies to epics, stories with sub-tasks, and any parent-child JIRA hierarchy.
- The orchestrator checks the parent's status before delegating child work. If the parent is still To Do, transition it to In Progress first.
- The parent moves to Done only when all child issues are Done (or explicitly closed).

## Handoff sync with JIRA

Every JIRA status change must be mirrored in the task's handoff file (`docs/task-memory/<TASK_NAME>_HANDOFF.md`):

- When transitioning a task in JIRA, update the handoff file in the same action.
- The handoff must include the current JIRA status and the date of the last status change.
- When a task moves to Done, the handoff receives a final update with the Done status, date, commit hash, and QA evidence.

## Specification update rule

Updating specification and design documents is an **integral part of completing every task** — not a separate activity.

### Who may edit specification documents

Only **CEO**, **Architect**, or **TechWriter** may edit specification documents. Implementing agents (backend, desktop-agent, browser-extension, analyzer-ai, devops) **do not edit specs** — they report which specs may be affected.

**Scope:** this rule covers system specifications, ICDs, SRS, protocol docs, data flow docs, and design documents under `docs/system-specifications/`, `docs/architecture/`, and `docs/ASPS_DATA_FLOW.md`. Infrastructure/deployment docs (`docs/cloud/`) remain owned by DevOps.

### Workflow

```
Implementation done
  ├─ QA (verifies code)              ── in parallel
  └─ TechWriter (reviews specs)
        └─ Architect (approves spec changes)
Then: merge
```

1. **Implementing agent** — includes in its hand-off a list of spec documents that may be affected by the change, and why. Does NOT edit them.
2. **CEO/orchestrator** — spawns TechWriter in parallel with QA.
3. **TechWriter** — reviews the affected specs against the implementation. Drafts updates. If a bug fix touches a feature **not yet documented** in the specs, adds the feature (undocumented features are spec debt; a bug fix is the trigger to pay it).
4. **Architect** — reviews and approves TechWriter's spec changes. This also keeps the Architect informed of system evolution.
5. **QA** — verifies that the TechWriter was engaged and spec updates were made where needed. A missing spec review is a **Minor** finding.

### Specification document index

| Document type | Location | When affected |
|---|---|---|
| System Specification | `docs/system-specifications/ASPS_System_Specification.md` | Contracts, interfaces, protocols, enums |
| Desktop Agent Features | `docs/system-specifications/DESKTOP_AGENT_FEATURES.md` | Desktop agent behavior, enums, config |
| System Overview | `docs/system-specifications/ASPS_System_Overview.md` | Architecture, communication flows |
| Data Flow | `docs/ASPS_DATA_FLOW.md` | Alert types, data paths, processing pipeline |
| Messaging Spec | `docs/architecture/messaging/` | Message formats, envelope, serialization |
| WebSocket Protocol | `docs/architecture/WS-AGENT-PROTOCOL.md` | WebSocket transport, message types |
| ADRs | `docs/architecture/decisions/` | Architecture decisions |

## Rules

- No commits directly to `main` — all work goes through branches + PR.
- No merge without QA PASS, orchestrator code review, **and a security gate PASS** (see "Security gate" above). All three gates are mandatory for every merge to `main`.
- Branch is deleted after successful merge.

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
   ├─ Approved → merge to main → JIRA to "Done" (transition ID 41)
   └─ Not approved → return to agent → JIRA to "In Progress" (transition ID 21)
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
- Approved → orchestrator merges to `main` and transitions JIRA to Done.
- Not approved → orchestrator returns to agent with findings and transitions JIRA to In Progress.

## Commit message format

```
<JIRA-ID> <Exact Jira issue title>

<Concise description of the implemented changes and relevant verification>
```

## Responsibilities

| Role | Responsibility |
|---|---|
| **Orchestrator (CEO)** | Assigns tasks, decides branch names for multi-agent stories, does code review, approves/rejects merges, transitions JIRA to Done |
| **Developer agent** | Creates branch, implements, runs tests, requests QA, opens PR after QA PASS, transitions JIRA to In Review |
| **QA agent** | Reviews on the branch, returns PASS/FAIL with evidence |
| **DevOps / CISO** | Same branching rules when changing code or infrastructure |

## JIRA transitions

| Transition | ID | Who triggers |
|---|---|---|
| To Do → In Progress | 21 | Orchestrator (on assignment) |
| In Progress → In Review | 31 | Developer agent (after QA PASS + PR opened) |
| In Review → Done | 41 | Orchestrator (after code review approval + merge) |
| In Review → In Progress | 21 | Orchestrator (if code review fails) |

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

## Rules

- No commits directly to `main` — all work goes through branches + PR.
- No merge without QA PASS and orchestrator code review.
- Branch is deleted after successful merge.

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
3. Agent runs tests, verifies work
4. Agent requests QA review (on the branch)
5. QA PASS → agent opens Merge Request (PR), reviewer = orchestrator (CEO)
6. Agent transitions JIRA to "In Review" (transition ID 31)
7. Orchestrator does code review
   ├─ Approved → merge to main → JIRA to "Done" (transition ID 41)
   └─ Not approved → return to agent → JIRA to "In Progress" (transition ID 21)
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

## Handoff sync with JIRA

Every JIRA status change must be mirrored in the task's handoff file (`docs/task-memory/<TASK_NAME>_HANDOFF.md`):

- When transitioning a task in JIRA, update the handoff file in the same action.
- The handoff must include the current JIRA status and the date of the last status change.
- When a task moves to Done, the handoff receives a final update with the Done status, date, commit hash, and QA evidence.

## Rules

- No commits directly to `main` — all work goes through branches + PR.
- No merge without QA PASS and orchestrator code review.
- Branch is deleted after successful merge.

---
name: feedback-jira-auto-sync
description: JIRA must mirror real-time work status — update BEFORE agents start AND AFTER each phase completes, never batch
metadata:
  type: feedback
  originSessionId: 429ee8b3-85e1-42ff-b1b0-f7d623110a6e
  modified: 2026-09-09T08:41:31.166Z
---

JIRA must reflect real-time work status at all times. Two directions:

## Before work starts
1. **Create sub-tasks** in JIRA under the parent issue for each agent/work-stream
2. **Set all tasks to In Progress** — the parent issue AND the sub-tasks
3. **Don't mark Done prematurely** — a task is Done only when the full requirement is met

## After each phase completes
4. **Transition immediately** — when a deploy succeeds, a fix is committed, or a sub-task is complete, update JIRA in the same action. Don't batch updates for later.
5. **Add a comment** with: what was done, changed files, commit hash, test results
6. **Close children before parent** — only transition parent to Done when ALL sub-tasks are Done

**Why:** On 2026-08-24, multiple tasks were completed but JIRA still showed them as To Do / In Progress. Isaac had to ask "למה המשימות לא מעודכנות?" — the board didn't match reality. **RECURRED 2026-09-09** (ASPS-763/768 sat at To Do while their work was in flight / done) *with this memory already present* — proof that a remember-at-the-right-instant rule is NOT enough under multi-parallel-gate load. The gap is application, not knowledge. Fix = a self-healing reconciliation that does not depend on remembering at the spawn moment.

**Words must match the board too (2026-09-09):** Isaac caught me calling "ASPS-763 complete" while the ticket was In Progress with open child subtasks (778/779 hardening). Do NOT say "complete"/"done"/"🎉 project complete" for a ticket that is In Progress or has open children — that contradicts the status I maintain and is the same board-vs-reality mismatch as a missed transition. Precise language: distinguish "the CORE/primary goal is delivered and live" from "the ticket is Done." A parent is only "Done" when its children are Done; until then it is "core delivered, In Progress, N follow-ups open."

**Self-healing reconciliation (the load-bearing habit):** at EVERY status checkpoint — especially when answering "who's working on what" / "מי עובד על מה?" or before any status report — run a quick JIRA-vs-reality pass over the active epic's tree: every ticket with a live agent or an open branch must be **In Progress**; every merged one must be **Done**; a parent with any active child must be **In Progress**. Fix drift on the spot. Because the operator asks for status often, this catches drift within one checkpoint regardless of whether the start-time transition was remembered.

**How to apply:**
- At the start of any delegated work: create JIRA sub-tasks, transition parent + sub-tasks to In Progress
- After each significant completion (commit pushed, image deployed, PR merged): transition the relevant JIRA issue immediately — same tool-call batch, not "I'll do it later"
- At agent completion: transition sub-task to Done (or In Review if QA needed)
- Never close a parent issue while any child is still open
- Use issue type "Subtask" (id: 10010) with `parent: { key: "ASPS-XXX" }`
- Transition IDs: 11=To Do, 21=In Progress, 31=In Review, 41=Done

Related: [[reference-jira]], [[reference-access-keys]]

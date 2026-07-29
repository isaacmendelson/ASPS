# CEO Operating Principles

`AGENTS.md` (Codex) / `CLAUDE.md` (Claude Code) is the primary process
authority. This memory summarizes durable CEO behavior and must not weaken
newer rules there.

## Quality gates

- Non-trivial code requires relevant unit tests before QA.
- Implementers report exact commands and pass/fail/skip counts.
- Relevant failing tests block QA unless proven pre-existing with baseline
  evidence and no regression.
- Independent QA is mandatory before commit for non-trivial changes.
- `QA FAIL` returns the task to implementation, tests, and QA again.
- A previous QA PASS does not cover later material changes.
- CEO/root verifies the actual diff, test evidence, and QA verdict.
- No Jira `Done` or authorized commit before documented QA PASS.

## Trust-but-verify

- Agent summaries describe intent; inspect actual files and evidence.
- Verify remembered file/function claims against the current repository.
- Recheck memory older than roughly 30 days before relying on current status.
- Never claim an external service was checked unless a tool returned data.

## Active-agent monitoring

- While delegated ASPS work is active, check every 10 minutes which agents are
  running, completed, waiting, or blocked.
- Present the user with a compact Hebrew status table every 10-minute checkpoint,
  including Jira issue, agent/label, current phase, new evidence, and blocker.
- A status check must not pause productive agents. Request a non-blocking
  checkpoint when an agent has produced no visible progress.
- Immediately route completed implementation to independent QA, return QA
  failures to implementation, and keep filling safe execution slots while
  preserving one slot for QA.
- Verify Jira status when an issue changes phase; do not describe an issue as
  closed until Jira confirms `Done`.

## Safety and scope

- Confirm destructive or hard-to-recover actions.
- Do not bundle unrelated side fixes; report and route them separately.
- Preserve unrelated working-tree changes.
- Never expose or persist tokens, passwords, keys, cookies, or connection
  strings in memory, docs, logs, commits, or responses.

## Build and runtime evidence

- Run the strongest proportionate build/test verification.
- In .NET, distinguish compiler errors from `MSB3027/MSB3021` file-copy locks.
- If UI/runtime verification is unavailable, state that limitation precisely.
- Absence of tests is a gap, not an automatic pass.

## Git and Jira

- Commit only when requested or authorized by the active workflow.
- CEO/root owns the final commit after QA PASS.
- Commit messages for Jira work contain exact Jira ID, exact title, and a
  concise implementation/verification description.
- Record commit hash and QA evidence in Jira before moving to `Done`.

## Memory

- Update durable learning inline rather than waiting for session end.
- Use `docs/task-memory/` for task-specific state.
- Use this role memory only for durable CEO behavior and verified decisions.

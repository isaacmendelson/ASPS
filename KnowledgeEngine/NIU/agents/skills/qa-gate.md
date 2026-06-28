---
name: qa-gate
description: Run the pre-merge QA verification on a non-trivial change. Spawns the qa sub-agent with the change scope and acceptance criteria, then reports PASS / FAIL with evidence.
---

# /qa-gate

The pre-merge verification step that's mandatory per `operating_principles.md` → "QA Gate Before Merge". This skill formalizes the SendMessage-to-QA flow into one invocation so it's not skipped.

## When to invoke
- A non-trivial change is ready to commit but hasn't been verified independently.
- User says "QA this", "run the QA gate", "verify before commit", "pre-merge check".

## When to SKIP

Per `operating_principles.md`, these are trivial and **do not** require QA:
- Typo fix
- Comment / log message change
- Single-line variable rename
- Documentation-only edit

These are **non-trivial** and **must** pass QA before commit:
- Logic change in any handler / service / repository
- New file (any size)
- Schema / migration change
- Dependency add or upgrade
- Multi-file refactor
- Anything in security-sensitive paths (auth, crypto, deserialization, secrets)

If the user invokes this skill for a trivial change, surface the gap: "this looks trivial — running QA anyway, or skip?"

## Ask first

1. **Scope** — what files were changed? (Default: `git diff --stat HEAD` against the last commit.)
2. **Acceptance criteria** — what must be true for the change to pass? At minimum:
   - Build is clean (0 `error CS####`; `MSB3027`/`MSB3021` file locks are OK).
   - Existing tests still pass.
   - The change does what the user asked for, verifiable somehow.
3. **What else should QA verify?** Anything specific to this change — a new migration applies cleanly, a new tool definition is reachable, a new admin page renders, etc.
4. **Branch + base** — current branch and the commit/branch the change builds on. QA needs to know what it's diffing against.

If the user can't state acceptance criteria, QA can't grade — push back.

## Flow

1. Collect the change scope:
   ```bash
   git status
   git diff --stat HEAD
   git log --oneline -5
   ```
2. Identify the right QA target. The repo has a `qa` sub-agent at [.claude/agents/qa.md](c:/Jobs/ASPS/GitHub/Software/.claude/agents/qa.md). Spawn it via the `Agent` tool with `subagent_type: "qa"`.
3. Brief the QA agent with:
   - The files changed (paths).
   - The acceptance criteria from the user.
   - Any context the agent wouldn't otherwise have (e.g. "this depends on a Keycloak realm that exists on dev only").
   - **Do not tell QA what to find.** Brief on what changed and what success means; let QA find issues independently.
4. Wait for QA's response.
5. **Trust-but-verify** (per `operating_principles.md`): if QA reports PASS, spot-check the most load-bearing file. If QA reports FAIL, read the cited evidence before relaying.
6. Report to the user:
   - **PASS** → "QA PASSED on <files>. <one-line confirmation>. Safe to commit."
   - **FAIL** → "QA FAILED. <findings>. <recommended next step>." Do **not** commit.

## What QA will check (depending on stack)

For C# / Backend changes:
- `dotnet build` clean (only real failures are `error CS####`).
- Migrations: generated SQL is sane; no destructive changes without explicit intent.
- CQRS: handler is registered in DI + routed in `CQRSGateway`.
- Tests: `ASPS.Tests` still passes for the touched project.

For Python (desktop agent / analyzers):
- Module imports without error.
- Existing tests in `apps/desktop/win/src/tests/` still pass.
- No hard-coded secrets / keys.

For Razor / WebApi:
- Build clean.
- Page renders (or: explicit note that UI rendering can't be verified without running the app).
- Nav menu updated if Index page.

For Chrome extension:
- `manifest.json` valid JSON.
- Permissions match what's actually used.
- Existing Jest tests pass.

Across all stacks:
- **No silent side-fixes** — every change is intentional.
- **No leaked secrets** — appsettings, .env, tokens.
- **No backwards-compat hacks** — `_unused` renames, `// removed X` comments.

## Output convention

```
QA Gate: <branch>
Scope: <N> files (<list>)
Acceptance criteria:
  1. ...
  2. ...

Build: PASS / FAIL <details>
Tests: PASS / FAIL <details>
Acceptance: PASS / FAIL <per-criterion>

Result: PASS — safe to commit
       OR
Result: FAIL — <findings + recommended action>
```

## Never

- Skip QA because "it's just a small change" when the change touches a non-trivial path (auth, crypto, schema, secrets). Trivial-vs-non-trivial is about *risk*, not lines of code.
- Commit between FAIL and a re-run. Either fix and re-QA, or revert.
- Relay a QA PASS without spot-checking the most load-bearing file. Sub-agent reports describe intent; verify reality.
- Run QA against uncommitted *and* unsaved files. Save first, then QA.

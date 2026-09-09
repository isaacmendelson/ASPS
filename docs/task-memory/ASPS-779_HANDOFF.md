# ASPS-779 — Sandbox denyRead / env-denylist hardening — Handoff

**Task:** ASPS-779 — subtask of ASPS-763 (bubblewrap sandbox hardening story), parent epic ASPS-738.
**Branch:** `asps-779-sandbox-denyread-env-hardening`
**Status:** All three merge gates passed (Code review PASS, QA PASS, Security PASS). Ready for orchestrator PR + merge.
**JIRA:** In Progress → **In Review** on PR open → **Done** on merge (see JIRA sync section below).

---

## What this task does

Follow-up to the ASPS-768 security gate (which auto-allowed sandboxed `Bash` for the Telegram CEO bot). Three parts, all in `apps/telegram-ceo`:

### Part 1 — `denyRead` hardening (`src/agent.ts`, `buildSandboxSettings`)
`filesystem.denyRead` gains four more home-dir paths: `~/.ssh`, `~/.gitconfig`, `~/.aws`, `~/.gnupg`. Before this, these existed only as `Read`/`Edit` tool-level guards (`SECRET_PATH_PATTERNS` in `src/security.ts`) — a sandboxed `Bash` command (auto-allowed since ASPS-768) could still read them straight off disk, since the tool-level guard never sees `Bash` command arguments. Same class of gap ASPS-766 already closed for `~/.claude`/`~/.npmrc`.

- `~/.ssh`, `~/.aws`, `~/.gnupg` mirror directory entries already in `SECRET_PATH_PATTERNS` (`src/security.ts:513-515`).
- `~/.gitconfig` is **not** in `SECRET_PATH_PATTERNS` — it's added here as an additional credential store (can hold `credential.helper store` plaintext creds), a deliberate over-inclusion beyond that set, not a mirror of an existing entry.
- Defense-in-depth, not a live vuln today (bot pushes over HTTPS with a stored `credential.helper`, none of these paths exist on the box yet) — closes the gap before any future SSH-deploy-key switch would make it live.

**Glob decision (deliberately excluded):** `*.pem`/`*.key` (also in `SECRET_PATH_PATTERNS`) are **not** added to `denyRead`. Verified against `node_modules/@anthropic-ai/claude-agent-sdk/sdk.d.ts` and the zod schema in `sdk.mjs`: `filesystem.denyRead` is typed `string[]` with no documented glob-matching (the SDK's only documented picomatch glob support is the unrelated CLAUDE.md `excludePatterns` option). The sandbox is bwrap/mount-based — binds concrete paths, not glob expressions. A `*.pem` entry would be a no-op or fail confusingly, which is worse than no entry (reads as "covered" while doing nothing at runtime). **Residual gap, accepted and logged, not silently dropped:** a sandboxed `Bash` command reading a `*.pem`/`*.key` file outside the concrete denied directories (e.g. a stray key dropped directly under the repo clone) is not stopped by `denyRead` — still covered only by the tool-level guard for `Read`/`Edit`/`Write`/... tool calls, not for arbitrary Bash. **Tracked as ASPS-780** — re-check `sdk.d.ts` for real glob support if/when revisited.

### Part 2 — Boot-time env self-check (`src/agent.ts` + `src/index.ts`)
`assertSandboxEnvDenylistComplete()` — new exported pure function in `agent.ts`, called once at boot from `index.ts` before the bot starts serving turns.

- **Problem it closes:** `SANDBOX_DENIED_ENV_VARS` (the list of env vars stripped from sandboxed `Bash`'s environment) is hand-maintained. A future secret env var could be wired into the process without anyone remembering to add its name to the list, silently reopening the "sandboxed Bash reads a token from `process.env`" gap.
- **Design:** name-shape heuristic only — scans `process.env` **keys**, never values. Flags any name (case-insensitive) containing `_TOKEN`, `_KEY`, `_SECRET`, or `_PASSWORD` (deliberately "contains" not "ends with", to safe-over-include rather than risk a false negative), plus a small explicit `KNOWN_BARE_SECRET_ENV_NAMES` fallback (`ANTHROPIC_API_KEY`, `TELEGRAM_BOT_TOKEN`) for names without one of those substrings.
- **Fail-closed:** throws (process exits via `index.ts`'s catch → `process.exit(1)`) if any flagged name is missing from `SANDBOX_DENIED_ENV_VARS`. Deliberately a boot-time assertion, not a runtime warning — a missing entry means the sandbox's core guarantee no longer holds.
- **No value leakage:** only variable NAMES ever appear in the thrown message / logged by `index.ts`'s `console.error`, never a value.
- Pure function, takes `env: NodeJS.ProcessEnv = process.env` as a parameter — unit-testable with a fake env object.

### Part 3 — `socat` install (`deploy/vps/06-sandbox.sh` + `docs/cloud/VPS_TELEGRAM_HARDENING.md`)
The SDK's sandbox network proxy (enforces `network.allowedDomains` egress control for sandboxed Bash) shells out to `socat` independently of `bwrap` (separate `socatPath` option in the SDK schema, `sdk.mjs`). It had been installed by hand on the live box, outside provisioning — a fresh box built from the script alone would have been missing it. Script now installs + verifies `socat` on PATH as its own idempotent step (new step 2/5; steps renumbered 1/5→5/5, was 1/4→4/4). Doc updated to match.

---

## Changed files

| File | Change |
|---|---|
| `apps/telegram-ceo/src/agent.ts` | `denyRead` +4 paths; new `SECRET_ENV_NAME_SUBSTRINGS`, `KNOWN_BARE_SECRET_ENV_NAMES`, `looksLikeSecretEnvName`, `assertSandboxEnvDenylistComplete` |
| `apps/telegram-ceo/src/index.ts` | Boot-time call to `assertSandboxEnvDenylistComplete()`, fail-closed with `process.exit(1)` |
| `apps/telegram-ceo/src/__tests__/agent.test.ts` | New tests for `denyRead` additions + `assertSandboxEnvDenylistComplete` |
| `deploy/vps/06-sandbox.sh` | New `socat` install+verify step; step numbering 1/4→1/5 through 4/4→5/5 |
| `docs/cloud/VPS_TELEGRAM_HARDENING.md` | Documents the `socat` dependency |

### Closeout changes (this session — Nit fix + handoff, no functional change)

| File | Change |
|---|---|
| `apps/telegram-ceo/src/agent.ts` | Reworded the `denyRead` block comment (~L333-348): corrected the inaccurate claim that all 4 added paths are "already listed in `SECRET_PATH_PATTERNS`" — `~/.gitconfig` is not in that list; it's an additional credential store added beyond it. `~/.ssh`/`~/.aws`/`~/.gnupg` do mirror `SECRET_PATH_PATTERNS`'s home-dir entries. Comment text only, `denyRead` array/logic unchanged. |
| `apps/telegram-ceo/src/__tests__/agent.test.ts` | Matching reword of the test description string (~L1010) for the same accuracy fix. Test body/assertions unchanged. |
| `docs/task-memory/ASPS-779_HANDOFF.md` | This file — created per `task-workflow.md`. |

---

## Test / verify results

- `cd apps/telegram-ceo && npm run build` — clean, no errors (both before and after the closeout reword).
- `npx vitest run` — **359 passed / 0 failed / 0 skipped**, both before and after the closeout reword (comment/description-only change, no assertion changes).
- `bash -n deploy/vps/06-sandbox.sh` — OK.
- Line endings: LF (per `feedback_docker_sh_line_endings` convention — verified, not a `.sh` line-ending regression).
- No secret values in any changed file (names only, per the env self-check design).

## Gate outcomes

| Gate | Verdict |
|---|---|
| Code review (orchestrator) | PASS — 1 Nit raised (comment/description parity vs. `SECRET_PATH_PATTERNS`), fixed this session |
| QA | PASS |
| Security | PASS — residual `*.pem`/`*.key` chained/obfuscated Bash-read gap noted as accepted, tracked separately (see below) |

## Tracked findings

1. **Nit — fixed this session.** `denyRead` block comment (`agent.ts` ~L333-348) and its matching test description (`agent.test.ts` ~L1010) inaccurately stated all four added paths were "already listed in `SECRET_PATH_PATTERNS`" / "the rest of `SECRET_PATH_PATTERNS`'s home-dir entries." `~/.gitconfig` is not in `SECRET_PATH_PATTERNS` (verified `src/security.ts:505-516`) — it's an addition beyond that set. Reworded both to state this accurately; no logic change.
2. **ASPS-780 (new, tracked, not fixed here).** Residual gap: a sandboxed `Bash` command reading a `*.pem`/`*.key` file via a chained or obfuscated path outside the concrete `denyRead` directories (e.g. a stray key file dropped directly under the repo clone, or reached via command chaining) is not stopped by `denyRead` — the SDK's `filesystem.denyRead` has no glob support (verified against `sdk.d.ts`/`sdk.mjs`), so `*.pem`/`*.key` patterns can't be expressed there. Only the tool-level guard (`SECRET_PATH_PATTERNS` via `Read`/`Edit`/`Write`/...) covers those extensions, and it doesn't see Bash command arguments. Accepted risk, logged in the `agent.ts` block comment, filed as ASPS-780 for whoever revisits this if the SDK ever adds real glob support to `denyRead`.

## Continuation point

Branch has been pushed to `origin/asps-779-sandbox-denyread-env-hardening` with the Nit fix + this handoff. **No PR opened, no merge performed** — per instructions, the CEO/orchestrator handles PR creation, code review sign-off recording, and merge to `main`. Next action: orchestrator opens PR, confirms JIRA transition In Progress → In Review (transition 31), then after merge → Done (transition 41), and files ASPS-780 in JIRA if not already created as a tracked follow-up ticket.

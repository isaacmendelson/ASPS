# ASPS-763 — Privilege-Separate the Telegram CEO Bot's Agent Tool Execution

**Task:** ASPS-763 (Story, under epic ASPS-738). Sandbox / privilege-separate the bot's
tool execution so code the agent runs cannot reach the bot's secrets or git-push
credential — which **unblocks ASPS-762** (relax the approval model / "gate only
dangerous ops").
**Status:** In Progress. **Bot: STOPPED** (stays off until ASPS-763 is complete + deployed).
**Design of record:** [ADR-005](../architecture/decisions/ADR-005-ASPS-763-AGENT-TOOL-EXECUTION-PRIVILEGE-SEPARATION.md).
**Last updated:** 2026-09-08.

---

## Why this exists (the ASPS-762 saga)

The operator asked to "cancel the approvals" (the bot flooded them with Telegram
approval taps and its bridge kept breaking — duplicate/zombie processes, `AbortError:
Stream closed`; the bot was stopped to end the flood). ASPS-762 relaxed the model to
auto-allow dev work. Its security gate **FAILED twice**:

1. **Env exfil (closed):** the Bash tool child inherited the bot's secret env. Fixed
   with the SDK `Options.env` scrub (`buildToolChildEnv`) removing
   `GITHUB_TOKEN`/`JIRA_API_TOKEN`/`TELEGRAM_BOT_TOKEN`/`ANTHROPIC_API_KEY`.
2. **Code-exec exfil (the wall → this ticket):** auto-allowing test-running
   (`npm test`/`jest`/`pytest`) + `Write` is unapproved **arbitrary code execution**.
   The agent runs as OS user `aspsbot`, which OWNS `/home/aspsbot/secrets/*` and the
   git credential-store. So `Write evil.test.js` + `jest` → the executed code can
   `fs.readFileSync` a secret or `git push HEAD:main` via the ambient credential —
   bypassing the git-push approval gate and the tool path-guard (which only constrains
   the Edit/Read *tools*, not code they spawn). Env-scrub can't close this.

**Root constraint:** the agent shares the OS user with the secrets + git creds.
**Operator decision (2026-09-08): B — privilege-separate the agent** (chosen over
"relax-less"). Restore JIRA/PR write capability too (via a gated path).

---

## The design (ADR-005)

Two parts, both using SDK primitives (no second daemon / user / socket):

1. **Sandbox every Bash exec** via the SDK's built-in `sandbox` option (bubblewrap on
   Linux): `filesystem.denyRead` the secrets dir, `allowWrite` the repo clone,
   `credentials.files/envVars` deny the git cred + tokens. Do **not** set
   `autoAllowBashIfSandboxed` — `canUseTool` stays the sole authority and auto-allows
   Bash (after the destructive hard-deny) because the sandbox bounds the blast radius.
2. **In-process gated MCP server** (`createSdkMcpServer({name:'ceo-privileged'})`) for
   the privileged ops — `git_push`, `jira_transition`/`jira_comment`/`jira_update_issue`,
   `github_create_pr`/`github_comment`. Handlers run in the bot's own Node process
   (holds creds, unsandboxed); NO auto-allow wildcard → each call is Telegram-gated.
   Restores write capability, safely.

SDK facts (from `apps/telegram-ceo/node_modules/@anthropic-ai/claude-agent-sdk/sdk.d.ts`):
no per-Bash uid/shell-wrapper hook; `spawnClaudeCodeProcess` is whole-CLI granularity;
the `sandbox` option is the per-exec isolation primitive; `createSdkMcpServer` tools run
in-process. See ADR-005 for the full table + rejected alternatives.

---

## Implementation plan (5 sub-tasks of ASPS-763)

| Sub-task | Story | Owner | Status |
|---|---|---|---|
| **ASPS-764** | VPS bubblewrap enablement (install + AppArmor userns + RestrictNamespaces) | devops | ✅ built + applied live; **in gate (PR #52)** |
| **ASPS-765** | Enable the SDK `sandbox` in `buildOptions` (agent.ts) | backend | queued (next) |
| **ASPS-766** | In-process `ceo-privileged` MCP (JIRA/GitHub writes) | backend | queued |
| **ASPS-767** | `git_push` gated tool + block ambient push | backend | queued |
| **ASPS-768** | Auto-allow sandboxed Bash → **closes ASPS-762** | backend | queued |

Sequencing: 764→765 before 768; 766/767 before 768. **768 is the only story that
relaxes an approval and merges only after 764–767 are verified on the box.**

---

## Completed so far

### ASPS-764 — VPS bubblewrap enablement (DONE, in gate)
- Branch `asps-764-vps-bubblewrap`, PR #52. Applied to the live box (bot left stopped).
- **AppArmor:** authored a **scoped** `/etc/apparmor.d/bwrap` profile (`flags=(unconfined)`
  + `userns,`, names only `/usr/bin/bwrap`) — mirrors the box's existing
  `/etc/apparmor.d/lxc-usernsexec`. Did **NOT** set the global
  `kernel.apparmor_restrict_unprivileged_userns=0` sysctl (least-privilege choice —
  this was the flagged security-gate decision point).
- **systemd:** `telegram-ceo.service` `RestrictNamespaces=yes` → `user pid mnt` (via a
  drop-in override; net/uts/ipc/cgroup still denied). `systemd-analyze security` = 6.6 MEDIUM.
- **Validated live as `aspsbot`** (4/4): secret `cat` → ENOENT (masked); ambient
  git-push credential unreachable (`git credential fill` → no cred; direct `cat` → ENOENT;
  same commands succeed OUTSIDE the sandbox — real); `getent hosts github.com` → works
  (egress kept); `node -e`/`npm --version` in the writable repo bind → works.
- Files: `deploy/vps/06-sandbox.sh` (new, idempotent), `deploy/vps/telegram-ceo.service`,
  `deploy/vps/README.md`, `docs/cloud/VPS_TELEGRAM_HARDENING.md` §7.
- Flagged (not fixed): `deploy/vps/README.md` still has a stale "NOT YET EXECUTED / no
  VPS exists" banner (pre-existing drift) — decide on a follow-up fix.

---

## Continuation point (exact next steps)

1. **Finish the ASPS-764 gate** (security + QA running on PR #52; CI trivially green — no
   tested component touched) → admin-merge #52 → ASPS-764 Done.
2. **ASPS-765:** in `apps/telegram-ceo/src/agent.ts` `buildOptions`, add the `sandbox`
   block (`enabled:true, failIfUnavailable:true, bwrapPath, filesystem.denyRead:[SECRETS_DIR],
   allowWrite:[WORKING_DIR], credentials.files/envVars deny`). Bash still gated. TDD
   negative test: sandboxed secret read fails. Gate → merge. (Box already bwrap-ready from 764.)
3. **ASPS-766:** `src/privileged-mcp.ts` via `createSdkMcpServer` (jira/github writes),
   no wildcard → per-call approval. Gate → merge.
4. **ASPS-767:** add `git_push` tool + verify ambient Bash push has no cred in-sandbox.
   Gate → merge.
5. **ASPS-768:** auto-allow sandboxed Bash in `createCanUseTool` (closes ASPS-762). Merge
   ONLY after 764–767 verified on the box. Then finalize/merge ASPS-762 (#51) on top.
6. **Deploy the bot from main**, restart `telegram-ceo`, verify end-to-end (sandboxed
   test-run auto-allows; secret read / push blocked; JIRA/PR writes gated). Bot back.

---

## Key facts for the next agent

- **Box:** `168.231.111.91`, user `aspsbot`. WORKING_DIR = `/home/aspsbot/ASPS`. Secrets
  in `/home/aspsbot/secrets/` (700; ACCESS_KEYS.env, telegram-ceo.env, github-credentials).
  git push via `credential.helper=store --file=/home/aspsbot/secrets/github-credentials`.
- **SSH access artifacts** are in the session scratchpad (`id_deploy` key, `known_hosts`,
  `aspsbot_pass`) — NOT in the repo. bubblewrap 0.9.0 is installed; the scoped AppArmor
  profile + RestrictNamespaces drop-in are already applied to the box.
- **Mandatory merge gates:** QA + CEO code review + security — every merge to main
  (operator directive). Merges to main are admin-bypass (branch protection needs 1
  approval the author can't self-give) — **ask the operator per-merge**.
- **main state:** ASPS-749/750/752/753/754 merged (baseline green; bot AskUserQuestion fix
  in; read-only git allowlist; approval timeout 600s). ASPS-762 (#51) NOT merged (blocked).
- **Related open:** ASPS-762 (blocked by this), ASPS-755–759 (follow-ups: extension test
  rewrites, isLocalUrl hardening + `.gitignore *.key`), ASPS-745 (secret relocation —
  largely satisfied on the box), plus flagged: bot tokens were passed on the MCP process
  argv (rotate), and ASPS-761 (bridge stability / zombie processes).

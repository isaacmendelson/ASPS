# VPS + Telegram CEO Bot Migration — HANDOFF

**Task name:** VPS_TELEGRAM_MIGRATION
**Owner hat:** CEO (orchestrator)
**Created:** 2026-08-25
**Status:** IN PROGRESS — user approved scope (D1–D4) on 2026-09-06. JIRA epic + stories created. Phase 4 (bot→Agent SDK) code complete on branch, pushed, ready for QA. Phases 0–3,5–7 gated on user provisioning the VPS.

## JIRA
| Item | Key | Status |
|---|---|---|
| Epic — VPS + Telegram CEO agent migration | ASPS-738 | In Progress |
| Phase 0 — Provisioning & prerequisites | ASPS-739 | To Do (user action) |
| Phase 1 — VPS baseline hardening | ASPS-740 | To Do (gated on VPS) |
| Phase 2 — Runtime toolchain | ASPS-741 | To Do (gated on VPS) |
| Phase 3 — Clone repo & wire secrets | ASPS-742 | To Do (gated on VPS) |
| Phase 4 — Migrate bot to Claude Agent SDK | ASPS-743 | **In Progress — ready for QA** — branch `asps-743-migrate-telegram-bot-to-claude-agent-sdk`, pushed |
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

### Phase 2 — Runtime toolchain — owner: devops
- Node.js 20 LTS (nvm or NodeSource), `git`, `ripgrep`, `build-essential`.
- Claude Code CLI (for `setup-token` + as SDK dependency): `npm i -g @anthropic-ai/claude-code`.
- Per D4: .NET 8 SDK, Python 3.11 + venv, Docker Engine + compose plugin (add `aspsbot` to `docker` group — note this is a privilege boundary; security to review).
- **Deliverable:** `dotnet --version`, `node -v`, `python3 --version`, `docker ps` all green.

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
- `apps/telegram-ceo/src/context.ts` — rewritten: no longer hand-reads `CLAUDE.md`/hat files from disk (the SDK's `settingSources: ["project"]` does this natively, and the agent self-onboards via CLAUDE.md's own "at session start" instructions, same as an interactive Claude Code session). Now only exports `TELEGRAM_SYSTEM_PROMPT_APPEND`, a short Telegram-transport-specific addendum (char limit, Markdown flavor, no live terminal on the other end).
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
5. **`context.ts` scope reduced:** no longer hand-loads `CLAUDE.md` + hat files (`.claude/hats/ceo/*`, `.claude/team/CHARTER.md`) — `settingSources: ["project"]` loads `CLAUDE.md` (and `.mcp.json`) automatically, and the agent follows CLAUDE.md's own "read PROJECT_CONTEXT.md → CHARTER.md → hats/ceo/INDEX.md" instructions itself, the same way an interactive Claude Code session self-onboards. `context.ts` now only holds a short Telegram-transport addendum. This is a deliberate behavior improvement (agent self-orients exactly like Claude Code, instead of being fed a hand-curated file subset) — flagging in case QA/architect wants to confirm this matches intent.
6. **MCP wiring:** used `settingSources: ["project"]` (auto-loads `.mcp.json` → `knowledge-engine` server) + `allowedTools: ["mcp__knowledge-engine__knowledge_search", "mcp__knowledge-engine__knowledge_ask"]` so those specific MCP tool calls don't stall on a permission prompt. Did not pass an explicit `mcpServers` option — relying on the project-settings auto-load per the task's own instruction.
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

**Next steps for CEO/QA:**
1. QA review on branch `asps-743-migrate-telegram-bot-to-claude-agent-sdk` against this handoff's acceptance checklist.
2. Code review (CEO or delegate) per `.claude/rules/review-standards.md`.
3. On PASS: open PR, merge, JIRA ASPS-743 → Done.
4. **Phases 0–3, 5–7** remain BLOCKED on the user provisioning the Hostinger VPS (Phase 0, ASPS-739). When the VPS + secrets exist, start Phase 1 (baseline hardening) via devops+security before any app work.
5. Do **not** touch the running Azure backend as part of this task.

## 7. JIRA
See the JIRA table at the top of this handoff (epic ASPS-738 + stories ASPS-739…746).

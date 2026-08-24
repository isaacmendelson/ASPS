# VPS + Telegram CEO Bot Migration — HANDOFF

**Task name:** VPS_TELEGRAM_MIGRATION
**Owner hat:** CEO (orchestrator)
**Created:** 2026-08-25
**Status:** PLANNING (no execution yet — awaiting user approval to start Phase 0)

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
Awaiting user approval of scope defaults (D1–D4) and Phase 0 provisioning. On approval:
start Phase 1 (baseline hardening) via devops+security before any app work. Do **not**
touch the running Azure backend as part of this task.

## 7. JIRA
Not yet created. On approval, open an epic (e.g. "ASPS VPS + Telegram CEO agent") with
one story per phase; label with handling agent per `.claude/rules/task-workflow.md`.

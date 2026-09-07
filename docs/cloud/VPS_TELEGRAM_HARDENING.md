# VPS Telegram CEO Bot — Hardening Log

**Box:** `168.231.111.91` (Hostinger "2nd VPS", `srv1618511`, Ubuntu 24.04.4 LTS, 2 vCPU / 8 GB)
**Role:** hosts the ASPS Telegram CEO bot (`@Zappa_desktop_bot`) as a 24/7 systemd service + a clone of the ASPS repo for the agent to work on. ASPS backend stays on Azure (D1). A pre-existing `openclaw` (localhost:18789) + Docker run on the box — **preserved, untouched**.
**Owner:** CEO orchestration. **Last updated:** 2026-09-07.

This log records every hardening applied to the live box, why, and what remains. It complements the security audit (`docs/security-audits/2026-09-07-vps-telegram.md`) and the migration handoff (`docs/task-memory/VPS_TELEGRAM_MIGRATION_HANDOFF.md`). The reusable pieces are codified in `deploy/vps/` (so a future provision inherits them); this doc is the record of what is true on THIS box.

---

## 1. Access model

- **SSH: key-only.** `aspsbot` (non-root, sudo) authenticates with an ed25519 key. **Password auth and root SSH are DISABLED.** `AllowUsers aspsbot`, `MaxAuthTries 3`.
- **sudo:** `aspsbot` uses an interactive password (not passwordless).
- **Access artifacts** (private key + sudo password) are held by the operator locally at `C:\Jobs\ASPS\aspsbot_vps_key` + `C:\Jobs\ASPS\VPS-ACCESS-README.txt` — **never in the git repo.**
- **Emergency recovery:** Hostinger hpanel → this VPS → Browser console → log in as `root` (root password in the "Hostinger Logins" doc). Root console login is intentionally preserved (`LOCK_ROOT=false`) so a broken key/sudo is always recoverable. Used once on 2026-09-07 to recover the SSH-lockout incident (see §4).

## 2. Phase 1 — baseline hardening (executed + verified)

| Control | State |
|---|---|
| OS patches | `apt full-upgrade` applied; `unattended-upgrades` enabled |
| Non-root user | `aspsbot` created, sudo group, key installed |
| SSH | key-only, root off, `AllowUsers aspsbot`, `MaxAuthTries 3`, X11/agent/TCP forwarding off, `PermitEmptyPasswords no` — verified via `sshd -T` |
| Firewall | UFW default-deny incoming / allow outgoing; **only `22/tcp`** allowed (the bot uses outbound long-poll — no inbound app port) |
| Brute-force | `fail2ban` sshd jail (see §3 M4 for the journalmatch fix) |
| Swap | 2 GB swapfile, `vm.swappiness=10` |
| Time/host | `Asia/Jerusalem`, hostname `srv1618511.hstgr.cloud` |
| Secrets dir | `/home/aspsbot/secrets` (700), **outside** the repo clone |
| Reboot survival | `ssh.service` enabled + `RuntimeDirectory=sshd` (auto-creates `/run/sshd` at boot); `ssh.socket` disabled; all services (`telegram-ceo`, `fail2ban`, `ufw`) enabled at boot. Verified non-invasively. |

## 3. Phase 6 — security-audit hardening (2026-09-07)

Audit verdict was FAIL (5 Majors, **no Blocker**; intrusion check clean — only Internet botnet scans, none succeeded). Applied on the box + folded into `deploy/vps/` templates (PR #44):

| # | Sev | Fix applied |
|---|---|---|
| **M4** | Major | **fail2ban was not actually banning** — its default journalmatch didn't match this box's sshd journal identity, so `Total failed: 0` despite real auth failures. Added `journalmatch = _COMM=sshd` to `jail.local`; verified matches now register. |
| **M5** | Major | **Secret files were writable by the service.** Dropped `SECRETS_DIR` from the service's `ReadWritePaths` → now **read-only** (env is injected by PID 1 before the sandbox; `git credential-store` only needs to *read* the cred file, which still works). An injected/approved agent can no longer overwrite its own token files. |
| **m1** | Minor | Removed leftover UFW `80/tcp` + `443/tcp` allow rules (no listener — pre-existing box drift). |
| **m3** | Minor | Service `UMask=0077` (was systemd default 0022 → world-readable files). |
| **M1** | Major | **GitHub token re-scoped + rotated.** Replaced the box's GitHub PAT with a **fine-grained token scoped to `isaacmendelson/ASPS` only, Contents + Pull-requests read/write, NO admin** — verified: write works (ref create 201), admin endpoints denied (hooks/secrets 403), real push from the box succeeded. Updated `ACCESS_KEYS.env` + `github-credentials` (600) and restarted the service. |

### Bot permission model (from Phase 4, context for the audit)
Deny-by-default: `Read`/`Grep`/`Glob` + 2 read-only knowledge-engine MCP tools auto-allowed (path-guarded, secret paths hard-denied); **every state-changing tool (Write/Edit/Bash/git push) requires a Telegram approve/deny from the authorized user.** `settingSources: []` isolation makes `canUseTool` the sole authority. Tuned (ASPS-747) to prefer the auto-allowed read tools so routine reads don't prompt.

## 4. Live incident (2026-09-07) — SSH lockout during Phase 1, recovered

`01-harden.sh` step 5 unconditionally disabled `ssh.socket` and switched to `ssh.service` for **any** SSH port incl. the default 22. The `systemctl restart ssh.service` failed and left sshd without `/run/sshd` (privsep dir) → every connection reset at `kex_exchange_identification` → **SSH lockout.** The script's own fail-safe held: it aborted **before** UFW, and `LOCK_ROOT=false` kept root console access. Recovered via the Hostinger browser console (`mkdir -p /run/sshd` + clean `systemctl restart ssh.service`). **Root cause + fix** (port 22 keeps socket activation; non-22 creates `/run/sshd` before restart) merged in PR #42. The box's end-state (`ssh.service` on 22, hardened) is correct and reboot-safe.

## 5. Secrets model

All bot secrets live in `/home/aspsbot/secrets/` (600, **outside** the repo clone `/home/aspsbot/ASPS`), injected into the service via two `EnvironmentFile=`:
- `telegram-ceo.env` — `TELEGRAM_BOT_TOKEN`, `CLAUDE_CODE_OAUTH_TOKEN` (subscription, verified), `AUTHORIZED_USERS`, `WORKING_DIR`, model/turn caps.
- `ACCESS_KEYS.env` — GitHub agent PAT (scoped, see M1) + JIRA creds, for the agent's own `gh`/`git`/JIRA use.
- `github-credentials` — the scoped PAT for `git push` (github.com-scoped credential helper).

The Phase-4 path guard hard-denies the agent reading any `*.env`/`ACCESS_KEYS*`/key file, and they sit outside the agent's `cwd` — belt and suspenders. Tokens are also present as process env vars, so the agent's approval-gated Bash could echo them if a human approved such a command (accepted residual — every Bash call is shown in full to the approver).

## 6. Deferred / pending

**Deferred systemd-sandbox tightening** (need an on-box smoke test — can break node's V8 JIT / git / the docker client; documented in `deploy/vps/telegram-ceo.service`):
`PrivateDevices=yes`, `SystemCallFilter=@system-service` (+ native arch + `SystemCallErrorNumber=EPERM`), capability drops, `RestrictAddressFamilies=AF_UNIX AF_INET AF_INET6`, `ProtectProc=invisible`. Kept OFF on purpose: `MemoryDenyWriteExecute` (V8 JIT needs W+X), `ProtectHome` (would hide `~/.gitconfig`, breaking push; `ProtectSystem=strict` already read-onlys the FS except the RW paths).

**Resolved:**
- **M2 — `main` branch protection** ✅ **DONE (2026-09-07)** — enabled in the GitHub UI: require a PR + 1 approval before merging, dismiss stale approvals, no force-push, no deletion, administrators can bypass (owner unblocked) but the agent's non-admin scoped token cannot merge to `main` without human review. Verified `main protected: true`.
- **M3 — `docker` group = root-equivalent** ✅ **DECIDED: accept as documented debt (option A), 2026-09-07.** `aspsbot` stays in the `docker` group so the agent can `docker compose` the ASPS stack per D4. Mitigations relied on: every Bash/`docker` call is Telegram-approval-gated and shown in full to the operator; single-user box; backend is on Azure so local Docker use is occasional. Revisit rootless Docker / a socket proxy if the exposure becomes unacceptable.

**Still pending (lower priority, tracked):**
- **Egress** unrestricted (UFW allow-outgoing) — low ROI to restrict (CDN IP ranges); FQDN proxy if pursued.
- **Token rotation cadence** — set a schedule for the 4 on-box tokens (GitHub scoped PAT, JIRA, `CLAUDE_CODE_OAUTH_TOKEN`, Telegram).
- **Retire the old GitHub PAT** from the "Hostinger Logins" doc (the pre-swap one) in GitHub settings, once confirmed it isn't used elsewhere.

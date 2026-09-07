# VPS Telegram CEO Bot — Operations Runbook

**What this is:** the day-to-day operations guide for the ASPS Telegram CEO bot running on the Hostinger VPS. For the security/hardening record see [`VPS_TELEGRAM_HARDENING.md`](VPS_TELEGRAM_HARDENING.md); for the migration history see [`../task-memory/VPS_TELEGRAM_MIGRATION_HANDOFF.md`](../task-memory/VPS_TELEGRAM_MIGRATION_HANDOFF.md).

**Last updated:** 2026-09-07.

---

## 1. At a glance

| | |
|---|---|
| Box | `168.231.111.91` (Hostinger, `srv1618511`, Ubuntu 24.04.4 LTS, 2 vCPU / 8 GB) |
| Bot | Telegram `@Zappa_desktop_bot` — the ASPS CEO agent on `@anthropic-ai/claude-agent-sdk` |
| Service | `telegram-ceo.service` (systemd, `User=aspsbot`, `Restart=always`, enabled at boot) |
| Repo clone | `/home/aspsbot/ASPS` (tracks GitHub `main`) |
| Bot source | `apps/telegram-ceo/` (built to `dist/`) |
| Secrets | `/home/aspsbot/secrets/` (600, outside the clone) |
| Auth to Claude | `CLAUDE_CODE_OAUTH_TOKEN` (subscription) |
| Also on box (do not touch) | `openclaw` (localhost:18789) + Docker |

## 2. Connecting

```bash
ssh -i C:\Jobs\ASPS\aspsbot_vps_key aspsbot@168.231.111.91
```
- Key + sudo password: `C:\Jobs\ASPS\VPS-ACCESS-README.txt` (keep private, not in git).
- Password SSH and root SSH are disabled — the key is the only SSH path.
- `sudo <cmd>` prompts for the sudo password.

## 3. Everyday operations

```bash
# status / health
sudo systemctl status telegram-ceo
systemctl is-active telegram-ceo

# live logs
journalctl -u telegram-ceo -f
# recent logs
journalctl -u telegram-ceo -n 100 --no-pager

# restart / stop / start
sudo systemctl restart telegram-ceo
sudo systemctl stop telegram-ceo
sudo systemctl start telegram-ceo
```

**The bot only responds to the authorized Telegram user id(s)** in `AUTHORIZED_USERS` (currently one). Anyone else is silently ignored.

## 4. How the approval model works (for the operator)

The agent runs **deny-by-default**:
- **No prompt:** reading files / searching (`Read`/`Grep`/`Glob`) and the knowledge-engine lookups — auto-allowed, confined to the repo tree, secret files hard-denied.
- **Approve/Deny prompt in Telegram:** every state-changing action — `Write`/`Edit`, any `Bash` command, `git push`, etc. The prompt shows the **full** command/target; tap ✅ Approve or ❌ Deny. A prompt times out to Deny after ~60s.
- Truly destructive Bash (`rm -rf`, `git reset --hard`, `DROP TABLE`, …) is **hard-denied** — never even offered for approval.

If approvals feel too frequent or too rare, the policy lives in `apps/telegram-ceo/src/` (`agent.ts` `createCanUseTool`, `context.ts` system prompt) — change, rebuild, redeploy (§5).

## 5. Updating the bot (deploy a new version)

After merging bot changes to GitHub `main`:
```bash
ssh -i C:\Jobs\ASPS\aspsbot_vps_key aspsbot@168.231.111.91
cd ~/ASPS && git pull --ff-only origin main
cd apps/telegram-ceo && npm ci && npm run build   # npm ci only if deps changed; else npm run build
sudo systemctl restart telegram-ceo
journalctl -u telegram-ceo -n 20 --no-pager        # confirm "Bot started" + no errors
```
The service reads `CLAUDE.md`/`.mcp.json` from the clone on every turn, but the compiled bot code + the system prompt are in `dist/` — a code/prompt change needs the rebuild + restart above.

## 6. Rotating a secret

Secrets are in `/home/aspsbot/secrets/` (600). To change one:
```bash
# edit the value (keep the file 600, keep it OUT of the repo clone)
nano ~/secrets/telegram-ceo.env      # or ACCESS_KEYS.env
chmod 600 ~/secrets/telegram-ceo.env
sudo systemctl restart telegram-ceo  # EnvironmentFile is read at start
```
- **GitHub PAT** also lives in `~/secrets/github-credentials` (`https://<user>:<pat>@github.com`) — update both it and `GITHUB_TOKEN` in `ACCESS_KEYS.env`.
- Use a **fine-grained** GitHub PAT: `isaacmendelson/ASPS` only, **Contents + Pull requests: read/write, no admin**.
- After rotating, retire the old token in GitHub settings.

## 7. Recovery — SSH is down / locked out

Root console access is intentionally preserved:
1. Hostinger **hpanel** → this VPS → **Browser terminal / Console** → log in as **`root`** (root password in the "Hostinger Logins" doc).
2. Common fix (sshd not listening):
   ```bash
   mkdir -p /run/sshd; chmod 0755 /run/sshd
   systemctl restart ssh.service
   ss -tlnp | grep ':22'      # confirm sshd is listening
   ```
   ⚠️ Do **not** `pkill sshd` from a console that is itself SSH-based — it kills your session mid-command.
3. Then reconnect over SSH with the key.

## 8. Firewall / fail2ban

```bash
sudo ufw status verbose                 # only 22/tcp should be open
sudo fail2ban-client status sshd        # bans / failures (journalmatch = _COMM=sshd)
```

## 9. Reboot

`ssh.service`, `telegram-ceo`, `fail2ban`, `ufw` are all enabled at boot; `/run/sshd` is auto-created via `RuntimeDirectory=sshd`. The box comes back with SSH on 22 and the bot running. `openclaw` + Docker restart on their own. (A real reboot test has not been forced — all preconditions verified non-invasively.)

## 10. Provisioning a fresh box from scratch

The full chain lives in [`../../deploy/vps/`](../../deploy/vps): `01-harden.sh` → `02-toolchain.sh` → `03-clone.sh` → (fill `~/secrets/*.env`) → `05-service.sh`. Fill `deploy/vps/config.env` from `config.env.example` first. See `deploy/vps/README.md` for the exact order, the lockout-safety notes, and the config values.

## 11. Escalation / known limits

- **`docker` group = root-equivalent** (accepted debt): a `docker run -v /:/host …` can escape the service sandbox — mitigated because every Bash/docker call is approval-gated and shown in full. Do not approve a docker command that mounts host paths unless you mean it.
- **Deferred hardening** (tracked, not applied): `SystemCallFilter`/`PrivateDevices`/capability drops on the service (need an on-box smoke test), egress restriction, token-rotation cadence. See `VPS_TELEGRAM_HARDENING.md` §6.
- Bot secrets are also in the process env → an approved Bash command could print them; approve shell commands deliberately.

# VPS Telegram CEO Bot — Hardening Log

**Box:** `168.231.111.91` (Hostinger "2nd VPS", `srv1618511`, Ubuntu 24.04.4 LTS, 2 vCPU / 8 GB)
**Role:** hosts the ASPS Telegram CEO bot (`@Zappa_desktop_bot`) as a 24/7 systemd service + a clone of the ASPS repo for the agent to work on. ASPS backend stays on Azure (D1). A pre-existing `openclaw` (localhost:18789) + Docker run on the box — **preserved, untouched**.
**Owner:** CEO orchestration. **Last updated:** 2026-09-08.

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
- **M2 — `main` branch protection** ✅ **DONE (2026-09-07)** — enabled in the GitHub UI: require a PR + 1 approval before merging, dismiss stale approvals, no force-push, no deletion, `enforce_admins=false` (owner can bypass so a solo dev isn't blocked). Verified `main protected: true`. **Caveat (found 2026-09-07, see below):** because the box's GitHub token belongs to the repo **owner** and `enforce_admins=false`, that token **bypasses** branch protection on a direct push — so M2 alone does NOT independently gate the bot from `main`. The bot IS gated in practice by the Telegram approval on every `git push` (a Bash call). See the "branch-protection bypass" item below.
- **M3 — `docker` group = root-equivalent** ✅ **DECIDED: accept as documented debt (option A), 2026-09-07.** `aspsbot` stays in the `docker` group so the agent can `docker compose` the ASPS stack per D4. Mitigations relied on: every Bash/`docker` call is Telegram-approval-gated and shown in full to the operator; single-user box; backend is on Azure so local Docker use is occasional. Revisit rootless Docker / a socket proxy if the exposure becomes unacceptable.

**Still pending (lower priority, tracked):**
- **Branch-protection bypass — the bot pushes with the OWNER's token, so M2 doesn't independently gate it.** With `enforce_admins=false`, any token belonging to the repo owner (isaacmendelson) bypasses `main` branch protection on direct push — and the box's scoped GitHub token is the owner's. **What actually gates the bot from `main` today:** every `git push` is a Bash command, so it is Telegram-approval-gated (deny-by-default model) — the operator sees and approves each push in real time. That is the load-bearing control; M2 is defense-in-depth for PR flow / force-push / deletion. **Decision (2026-09-07): accept option C** — rely on the Telegram approval as the agent-gating control; M2 stays as defense-in-depth. **Follow-up (option A, deferred):** to make M2 independently gate the bot, run the box under a **separate non-admin GitHub "machine user"** (a collaborator with Write, not Admin, not the owner) — then that account is bound by branch protection (can't bypass), the owner still bypasses, git history cleanly attributes bot vs. human, and a leaked box token has a smaller blast radius (non-admin, can't touch settings). Worth doing if the bot becomes more autonomous / the box less trusted, or for belt-and-suspenders; not urgent while every push is Telegram-approved.
- **Egress** unrestricted (UFW allow-outgoing) — low ROI to restrict (CDN IP ranges); FQDN proxy if pursued.
- **Token rotation cadence** — set a schedule for the 4 on-box tokens (GitHub scoped PAT, JIRA, `CLAUDE_CODE_OAUTH_TOKEN`, Telegram).
- **Retire the old GitHub PAT** from the "Hostinger Logins" doc (the pre-swap one) in GitHub settings, once confirmed it isn't used elsewhere.

## 7. Bubblewrap sandbox enablement (ASPS-764, sub-task of ASPS-763, ADR-005) — 2026-09-08

**Goal:** make `/usr/bin/bwrap` (bubblewrap) reliably usable by `aspsbot`
so the Claude Agent SDK's built-in Linux sandbox (`sandbox.enabled`) can
contain every Bash execution the bot's agent makes, per ADR-005. This is
Story 1 of ADR-005's implementation plan — OS/box enablement only. **No bot
behavior changed**: the bot's own `sandbox.enabled` option is not yet turned
on (that's ASPS-763-2), and `telegram-ceo.service` was left **stopped**
throughout (it was already stopped before this work started, blocked on
later ASPS-763 stories) — only a `systemctl daemon-reload` was run against
it, never a start/restart.

### What was applied to the live box (`168.231.111.91`)

| # | Change | Where |
|---|---|---|
| 1 | Installed `bubblewrap` 0.9.0-1ubuntu0.1 (already present from a prior manual probe; script is idempotent regardless). | `dpkg` |
| 2 | Installed `/etc/apparmor.d/bwrap` — a scoped AppArmor profile naming `/usr/bin/bwrap` (`flags=(unconfined)` + `userns,`), loaded with `apparmor_parser -r`. | `/etc/apparmor.d/bwrap` (root:root, 0644) |
| 3 | Added `RestrictNamespaces=` (reset) + `RestrictNamespaces=user pid mnt` to the unit's drop-in override. | `/etc/systemd/system/telegram-ceo.service.d/override.conf` |
| 4 | `systemctl daemon-reload` (no start/restart). | — |

`kernel.apparmor_restrict_unprivileged_userns` was **left untouched at `1`**
(the Ubuntu 24.04 distro default) — the sysctl fallback was never applied.
`kernel.unprivileged_userns_clone` was already `1` (unchanged).

### AppArmor resolution: scoped profile chosen over the global sysctl fallback

**Problem:** Ubuntu 24.04 mediates unprivileged user-namespace creation
with AppArmor. An unconfined process (the default — no profile attached)
gets `setting up uid map: Permission denied` when calling
`unshare(CLONE_NEWUSER)` unless `kernel.apparmor_restrict_unprivileged_userns=0`
(global opt-out) **or** the process is confined by a named AppArmor profile
that includes a `userns,` rule.

**Chosen fix — scoped profile (preferred, per the task spec):** a dedicated
`/etc/apparmor.d/bwrap` profile that names only `/usr/bin/bwrap`:

```
abi <abi/4.0>,
include <tunables/global>

profile bwrap /usr/bin/bwrap flags=(unconfined) {
  userns,

  include if exists <local/bwrap>
}
```

This is **not a bespoke workaround** — it is the exact pattern Ubuntu itself
already ships on this box for the identical restriction:
`/etc/apparmor.d/lxc-usernsexec` (`profile lxc-usernsexec /usr/bin/lxc-usernsexec
flags=(unconfined) { userns, ... }`), confirmed present and loaded
(`aa-status` lists it) before this change. `bwrap` performs its own
sandboxing via the namespaces/seccomp it sets up *inside* the userns it
creates — this profile does not attempt to additionally mediate bwrap's
filesystem/network access (that would be redundant and is not what the
restriction is for); it only supplies the named label the kernel's
userns-creation check requires, plus the one extra permission
(`userns,`) an unconfined process would already have had implicitly before
Ubuntu 24.04 introduced this restriction.

**Why not the global sysctl fallback
(`kernel.apparmor_restrict_unprivileged_userns=0`):** it would remove the
restriction for **every** unconfined process on the box, not just bwrap —
any other unprivileged process (now or added later) could then also create
user namespaces freely. The scoped profile achieves the same outcome for
the one binary that actually needs it, with a materially smaller blast
radius. The scoped fix worked and was fully sufficient — **the sysctl
fallback was never applied.**

**Security-gate flag:** this AppArmor decision (profile vs. sysctl) is
explicitly the security-gate discussion point called out in the task spec.
Verdict applied: **scoped profile, not the global sysctl.** Residual risk:
`flags=(unconfined)` on the `bwrap` profile means AppArmor does not mediate
what bwrap itself does with the `userns,` grant beyond gating its creation
— containment for what runs *inside* the sandbox is bwrap's own
namespace/mount/seccomp configuration (the SDK's `sandbox.filesystem.
denyRead`/`credentials.*` options, validated below), not AppArmor. This
mirrors exactly how `lxc-usernsexec` is already trusted on this box.

### systemd `RestrictNamespaces=` scoping

`telegram-ceo.service` denied all namespace types (`RestrictNamespaces=yes`).
Relaxed to an explicit allow-list, **not** `no`:

```
RestrictNamespaces=user pid mnt
```

- `user` — bwrap's unprivileged uid/gid map.
- `mnt` — the ro-bind + tmpfs-mask filesystem view bwrap constructs.
- `pid` — isolates the sandboxed process tree from the rest of the host,
  which the SDK's sandbox also expects.
- `net`, `uts`, `ipc`, `cgroup` remain **denied**. Network egress for the
  sandboxed command is kept via the **inherited** net namespace (no
  `--unshare-net` in the bwrap invocation) — the unit does not need
  permission to *create* a new net namespace for that.

Applied on the box via the existing `override.conf` drop-in pattern
(`/etc/systemd/system/telegram-ceo.service.d/override.conf`, same file that
already carries the `ReadWritePaths`/`UMask` fixes from the Phase 6 audit)
rather than rewriting the live base unit file, since the base unit on this
box already has other, unrelated drift from the repo template (see the
Phase 6 section above) that is out of scope for this task. The **repo
template** (`deploy/vps/telegram-ceo.service`) has the scoped value baked
directly into its `[Service]` section (no drop-in needed there — a fresh
re-provision gets it from `05-service.sh` directly).

`systemctl show telegram-ceo -p RestrictNamespaces` confirms
`RestrictNamespaces=mnt pid user` is the effective value after
`daemon-reload`. `systemd-analyze security telegram-ceo.service` shows the
expected single line item flip (`RestrictNamespaces=~mnt` now scored,
+0.1 exposure) with `net`/`uts`/`ipc`/`cgroup` still scored `✓` (denied) —
overall exposure **6.6 MEDIUM**, consistent with the pre-existing baseline
plus this one documented, reasoned relaxation (no other hardening
directives were touched).

### Acceptance evidence — validated as `aspsbot` on the live box

Exact command (unshares user+mount+pid, ro-binds `/`, binds the repo clone
read-write, tmpfs-masks the secrets directory, keeps network — no
`--unshare-net`):

```bash
bwrap \
  --ro-bind / / \
  --bind /home/aspsbot/ASPS /home/aspsbot/ASPS \
  --tmpfs /home/aspsbot/secrets \
  --proc /proc --dev /dev \
  --unshare-user --unshare-pid --unshare-uts --unshare-ipc \
  --die-with-parent --chdir /home/aspsbot/ASPS \
  bash -c '<checks below>'
```

| # | Check | Command | Result |
|---|---|---|---|
| 1 | Secret unreachable | `cat /home/aspsbot/secrets/ACCESS_KEYS.env` | **FAILS** — `No such file or directory` (tmpfs mask hides the real directory contents entirely; exit 1). |
| 2 | Ambient push credential unreachable | `git credential fill` for `host=github.com` (also direct `cat` of `github-credentials`) | **FAILS inside the sandbox** — `fatal: could not read Username for 'https://github.com': No such device or address`; direct `cat` → `No such file or directory`. Confirmed as a real mask, not a coincidence: the **same commands run outside the sandbox** on the same box succeed (`git credential fill` returns a real `username=isaacmendelson` + password). Note: plain `git -C /home/aspsbot/ASPS ls-remote origin` still succeeds inside the sandbox — expected and benign, since `ASPS` is a **public** GitHub repo and anonymous read access needs no credential at all; the credential itself (the thing that matters — the push capability) is what's verified unreachable above. |
| 3 | Network egress kept | `getent hosts github.com` | **WORKS** — resolves `140.82.121.3 github.com`. |
| 4 | Dev command in writable repo bind | `node -e "console.log(1)"`, `npm --version` | **WORKS** — prints `1`, `npm --version` → `11.19.0`. |

### Files changed (this task)

| File | Change |
|---|---|
| `deploy/vps/06-sandbox.sh` | New — installs bubblewrap + the AppArmor profile, idempotent, includes an on-box smoke test as `aspsbot`. |
| `deploy/vps/telegram-ceo.service` | `RestrictNamespaces=yes` → `RestrictNamespaces=user pid mnt`, with rationale comment. |
| `deploy/vps/README.md` | New run-order row, files table row, and "Bubblewrap sandbox enablement" section. |
| `docs/cloud/VPS_TELEGRAM_HARDENING.md` | This section. |
| Live box | `/etc/apparmor.d/bwrap` (new file); `/etc/systemd/system/telegram-ceo.service.d/override.conf` (appended `RestrictNamespaces=` reset + scoped value); `systemctl daemon-reload` run. `telegram-ceo.service` remains **stopped**, **enabled** (unchanged from before this task). |

### Residual risk / follow-ups

- Sandboxed Bash (once ASPS-763-2 turns `sandbox.enabled` on) retains
  network egress by design — an injected agent could exfiltrate repo
  *source* (already public on GitHub); secrets/creds are unreachable. This
  matches ADR-005's accepted residual; ASPS-763-6 (optional egress
  allowlist) narrows it further if pursued.
- The `bwrap` AppArmor profile uses `flags=(unconfined)` — AppArmor does not
  additionally mediate bwrap's own behavior beyond gating userns creation.
  This is the same trust level already extended to `lxc-usernsexec` on this
  box; flagged for the security gate rather than silently accepted.
- `RestrictNamespaces=user pid mnt` is a real (small, documented) widening
  of the unit's sandbox versus the previous deny-all — the containment
  intent shifts to bwrap's own per-exec namespace/filesystem/credential
  controls (ASPS-763-2), which is the design ADR-005 already commits to.
- This story only proves the OS-level capability. The **service's own** use
  of bwrap (via the SDK's `sandbox` option, under the unit's full sandbox —
  `ProtectSystem=strict`, `NoNewPrivileges=yes`, etc., together) is not yet
  live-tested end-to-end because the bot is intentionally not started; that
  end-to-end verification is part of ASPS-763-2's acceptance, not this
  task's.

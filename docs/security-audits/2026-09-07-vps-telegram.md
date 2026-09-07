# Security Audit — Telegram CEO Bot VPS (Phase 6 / ASPS-745)

**Date:** 2026-09-07
**Scope:** Live Hostinger VPS `168.231.111.91` (Ubuntu 24.04.4) running the Telegram CEO bot (`telegram-ceo.service`) + pre-existing `openclaw` (localhost) + Docker. Backend stays on Azure.
**Method:** Read-only host snapshot (`vps_security_snapshot.txt`) + repo configs (`deploy/vps/telegram-ceo.service`, `01-harden.sh`, `README.md`) + bot permission model (`apps/telegram-ceo/src/security.ts`, already PASSed).
**Reviewer:** Security agent. **Review only — CEO orchestrates remediation.**

---

## Verdict

**FAIL** (2 Major host/GitHub items need the USER; 3 further Majors fixable on-box). No Blocker: nothing is remotely exploitable in the current state — SSH is fully hardened, only port 22 exposes a service, secrets are `600`, no foothold in the auth logs. The Majors are latent blast-radius and an ineffective control (fail2ban), not an open door.

## Confirmed-good posture (positive findings)

- **SSH** — port 22, `PermitRootLogin no`, `PasswordAuthentication no`, `KbdInteractiveAuthentication`/empty-passwords off, `MaxAuthTries 3`, `X11Forwarding no`, `AllowTcpForwarding no`, `AllowUsers aspsbot`. No weak setting. Key-only.
- **Exposure** — only `sshd` on `0.0.0.0:22` is internet-reachable. `systemd-resolve` (53) and **openclaw (18789) are localhost-only** on both v4 and v6 — confirmed contained.
- **Secrets** — `SECRETS_DIR` `700`, all four secret files `600`, owned `aspsbot`, **outside** the clone; no secret found inside `/home/aspsbot/ASPS`.
- **Patches** — 0 pending, unattended-upgrades enabled. No world-writable files. Cron clean (only stock `docker-*-prune`/`e2scrub`/`sysstat`).
- **Intrusion check — clean.** All auth failures are Internet background botnet scans (`william`/`admin`/`fnrsv`/`root` from 159.223.218.194, 207.175.227.249, 109.186.197.28) — every one `Invalid user` or closed pre-auth; none for `aspsbot`/`root`, none succeeded. The `Bind to port 22 ... Address already in use` lines (Sep 07 01:03) are the self-inflicted socket-activation lockout episode, not an attack. No foothold indicators.

---

## Findings (most severe first)

| # | Severity | Location | Concrete risk / exploit | Remediation | Debt call |
|---|---|---|---|---|---|
| M1 | **Major** | GitHub PAT in `SECRETS_DIR/github-credentials` + `ACCESS_KEYS.env` (`GITHUB_TOKEN`) | If the token is **admin/classic-scoped** (unverified — snapshot correctly does not print it), a host compromise or a single prompt-injected approved `git`/`curl` turn grants full control of `isaacmendelson/ASPS`: rewrite/delete history, change repo settings, **disable branch protection (defeats M2)**, add collaborators, read Actions secrets. | **USER:** verify the live token is the intended fine-grained PAT scoped to `isaacmendelson/ASPS`, **Contents: read/write only** (no admin, no workflow, no org). If broader, **re-scope and rotate now**. Rotate regardless. | **Fix now — needs USER.** Blast radius is the whole repo; not acceptable as silent debt. |
| M2 | **Major** | GitHub repo setting (server-side) + agent `git push` capability | `main` is **not branch-protected**. The agent can `git push` to `main` after one Telegram approval; a rubber-stamped/social-engineered approval — or M1's admin PAT — puts arbitrary code on `main` with no server-side gate. Telegram approval is the *only* control. | **USER:** enable branch protection on `main` (require PR + review, block force-push/deletion, no bypass for the PAT identity). | **Fix now — needs USER.** ASPS-745 items (b)+(c); rate Major, not "later". |
| M3 | **Major** | `telegram-ceo.service` `SupplementaryGroups=docker` + `aspsbot` in `docker` group (`docker:x:988:aspsbot`) | **The systemd sandbox is porous.** `docker` group = root-equivalent: an injected agent runs `docker run -v /home/aspsbot/secrets:/x ...` (or `-v /:/host`) to read every secret, write any file, escalate to host root — **bypassing `ProtectSystem=strict`, `ReadWritePaths`, the SECRETS_DIR app-guard, and every directive in this audit.** This caps the value of all other hardening. | Reduce with **rootless Docker**, or a **socket-proxy** exposing only the compose verbs D4 needs, or drop `docker` from the *service* and gate it behind a separate friction path. At minimum document that "sandboxed" != "contained" while this holds. | **Accepted debt (documented) — revisit.** Known (ASPS-745 item e). It is the ceiling on every other control; escalate for a rootless-Docker decision. |
| M4 | **Major** | fail2ban `sshd` jail vs. Ubuntu 24.04 `ssh.socket` activation | Jail shows **`Total failed: 0` / `Banned: 0`** despite repeated auth failures in the journal. Socket-activated sshd runs as transient `ssh@<n>-...service` units, but the journalmatch is `_SYSTEMD_UNIT=sshd.service + _COMM=sshd` — so fail2ban **never sees** the per-connection failures and **never bans** brute-force. Protection is believed-active but **effectively off**. | Broaden the match, e.g. jail.local `[sshd]` -> `journalmatch = _COMM=sshd` (drop the unit constraint), `fail2ban-client restart`, confirm `Total failed` rises. Fold into `01-harden.sh` step 7. | **Fix now.** A control reported working that isn't. On-box config, no user cost. |
| M5 | **Major** | `telegram-ceo.service` `ReadWritePaths=... @SECRETS_DIR@` (live: `/home/aspsbot/secrets`) | Systemd injects env from `EnvironmentFile=` as **PID 1, before** the sandbox — the process **needs no FS access to `SECRETS_DIR`**. Granting **read+write** lets an injected agent read/copy raw token files at a known path, or **overwrite `github-credentials`** with attacker creds. Only the app-layer `SECRET_PATH_PATTERNS` guard stands in the way. | Remove `@SECRETS_DIR@` from `ReadWritePaths` (strict makes it read-only), and add **`InaccessiblePaths=@SECRETS_DIR@`** so the process cannot even read files it already has as env vars. Zero functional loss. | **Fix now.** ASPS-745 item (f). Removes a disclosure/overwrite class (except via M3). |
| m1 | Minor | UFW: `80/tcp` + `443/tcp` `ALLOW IN Anywhere` (v4+v6) | **Nothing listens on 80/443** (leftover from a prior setup). No live exploit, but open-firewall-to-nothing is attack surface + drift: any future/compromised process binding 80/443 becomes instantly Internet-reachable with no further firewall change. Bot is outbound-only. | `ufw delete allow 80/tcp` + `443/tcp` (both families). Re-add only for an intentional public listener (prefer scoped source). | Fix now. Trivial surface reduction. |
| m2 | Minor | `ubuntu` uid=1000 login shell + `/etc/sudoers.d/90-cloud-init-users: ubuntu ALL=(ALL) NOPASSWD:ALL` | Unused cloud-init account with a login shell and **passwordless root**. `AllowUsers aspsbot` blocks SSH (low remote risk), but it is a latent local-priv-esc amplifier: code exec as `ubuntu` -> instant root, no password. Dead account = needless surface. | `passwd -l ubuntu` + remove login shell, or delete the account; at minimum remove the `NOPASSWD:ALL` drop-in. Keep `aspsbot`'s password-prompted sudo. | Fix now (or explicitly accept, mirroring the `LOCK_ROOT` console-rescue rationale). |
| m3 | Minor | `telegram-ceo.service` — `systemd-analyze`: `UMask=` world-readable | Service-created files default to world-readable (`0022`). Low impact on a single-user box, but any secret-ish artifact the agent writes (logs, caches, tmp) is group/other-readable. | Add `UMask=0077`. Free. | Fix now. |
| m4 | Minor | `telegram-ceo.service` — no `SystemCallFilter` (`~@resources`, `~@swap` flagged; largest exposure contributor) | Full syscall surface to the kernel from a network-facing, injection-reachable Node process. Deferred pre-live (crash-closed risk) — **the box now exists and it is testable.** | Add `SystemCallFilter=@system-service` + `SystemCallArchitectures=native` + `SystemCallErrorNumber=EPERM`; run one real `docker compose` + `git push` + `dotnet` turn, watch for `EPERM`, iterate with `systemd-analyze security`. `@system-service` is Node-compatible by design. | Fix now (with a live smoke test). Deferral condition now met. |
| m5 | Minor | `telegram-ceo.service` — `PrivateDevices` not set | Full `/dev` visible. The docker **client** needs only `/run/docker.sock` (a `connect()`, unaffected), not `/dev/*`; `null|zero|random|urandom|tty` remain under `PrivateDevices`. | Enable `PrivateDevices=yes`; confirm `docker compose` + build still work. | Fix now (same smoke test as m4). Previously "unverified against a live box" — now verifiable. |
| m6 | Minor | UFW `default allow outgoing` — no egress restriction | Exfil surface: an injected agent can POST secrets anywhere outbound. **But** low mitigation ROI — legit destinations (Telegram/Anthropic/GitHub/JIRA) sit behind large shared CDN ranges, so an IP allowlist is fragile and co-tenant-bypassable. Real control is approval-gating + M5. | Document as accepted; if pursued, prefer an **egress HTTP proxy with an FQDN allowlist** over `IPAddressAllow=`. Do not add a brittle IP allowlist for false assurance. | Accept as documented debt (ASPS-745 item a). |
| m7 | Minor | `telegram-ceo.service` two `EnvironmentFile=` -> `GITHUB_TOKEN`/`JIRA_API_TOKEN` in process env inherited by every subprocess | Every Bash-tool subprocess (and its descendants) sees raw tokens in env; an injected-but-plausible approved command can embed token exfil. Approval-gated (residual, not open), but exposure is broad. | Consider dropping the **bare `GITHUB_TOKEN`** env var and relying on the github-scoped credential-helper for `git push`, keeping only `JIRA_*` in env (JIRA has no helper equivalent). | Accept as documented debt (ASPS-745 item d) — optionally narrow. |
| n1 | Nit | `telegram-ceo.service` residual cheap hardening | Not vulnerabilities; free exposure reduction. `CapabilityBoundingSet=` empty (Node/git/docker-client need no caps under `NoNewPrivileges`), `AmbientCapabilities=`, `RestrictAddressFamilies=AF_UNIX AF_INET AF_INET6`, `ProtectProc=invisible`, `ProcSubset=pid`. | Add the above; each is a graceful no-op if wrong. | Optional. |
| n2 | Nit | Token rotation posture | No rotation schedule for four long-lived secrets on an Internet-facing box that already had a console-recovery incident. | Establish a rotation cadence + record last-rotated. | Optional but recommended alongside M1. |

---

## Sandbox posture — directives correctly NOT enabled (confirmed)

- **`MemoryDenyWriteExecute=yes`** — **keep OFF.** Node's V8 JIT requires W+X pages; MDWE would crash-loop the bot. systemd's own docs list JIT as the known-incompatible case. Correct call.
- **`ProtectHome=yes`** — leaving OFF is acceptable. `ProtectSystem=strict` already makes the whole FS read-only except the two `ReadWritePaths`, and read-only != invisible, so `~/.gitconfig` (credential-helper config, identity, `safe.directory`) stays readable. `ProtectHome=yes` would make it *inaccessible* and break `git push` for no confinement `strict` doesn't already give. (Even `ProtectHome=read-only` adds nothing over `strict` here.)

Everything *worth* enabling now (m3 UMask, m4 SystemCallFilter, m5 PrivateDevices, n1 caps/address-families) is graceful-degradation or a quick live smoke-test away — the pre-live "guess-closed against a host that doesn't exist" rationale no longer applies now that `168.231.111.91` is live.

---

## Credential blast radius (host-compromise model)

If the host is compromised — or the agent is prompt-injected into an approved Bash turn, and note **M3 makes host-root reachable via docker regardless of the sandbox** — the on-box tokens grant:

| Token | Grants | Least-privilege / rotation |
|---|---|---|
| `GITHUB_TOKEN` (PAT) | If admin/classic: full control of `isaacmendelson/ASPS` — history, settings, branch protection, Actions secrets, collaborators. If fine-grained repo `Contents:rw`: push code only. | Verify + force to fine-grained `Contents:read/write` on the one repo (M1). Prefer a GitHub App installation token (short-lived) over a long-lived PAT. Rotate now. |
| `JIRA_API_TOKEN` + `JIRA_EMAIL` | Read/write all JIRA issues visible to that Atlassian account. | Scope to the ASPS project if the token type allows; rotate now. |
| `CLAUDE_CODE_OAUTH_TOKEN` | Use Isaac's Claude subscription — quota abuse, billing, any data the session can reach. | Rotate now; treat as a credential, not config. |
| `TELEGRAM_BOT_TOKEN` | Impersonate the bot: read messages sent to it and **send messages to the authorized CEO user** (phish the approver into rubber-stamping — compounds M2). | Rotate now; consider a dedicated bot identity. |

**Recommendation:** rotate all four now (Internet-facing box, prior console-recovery incident, tokens historically colocated with a repo-root `ACCESS_KEYS.env`), then set a rotation cadence and enforce least privilege per row.

---

## Items requiring the USER (cost/decision)

1. **M1** — verify + (if broader than repo `Contents:rw`) re-scope and rotate the GitHub PAT.
2. **M2** — enable `main` branch protection on GitHub (server-side).
3. **M3** — decide rootless Docker / socket-proxy vs. accepting root-equivalent `docker` group for the service.
4. **Rotation** — rotate all four tokens; agree a cadence.

## On-box fixes (no user cost, CEO can task an implementer)

- **M4** fail2ban journalmatch; **M5** drop `SECRETS_DIR` from `ReadWritePaths` + `InaccessiblePaths`; **m1** close UFW 80/443; **m2** neutralize the `ubuntu` NOPASSWD account; **m3/m4/m5/n1** systemd `UMask`/`SystemCallFilter`/`PrivateDevices`/caps (with one live smoke test). Fold host fixes back into `deploy/vps/01-harden.sh` and `telegram-ceo.service` so they survive a re-provision.

---
*Filed under `docs/security-audits/`. Review only — remediation is the CEO's to orchestrate. Update `NEEDS_ATTENTION.md` if the Majors are accepted rather than fixed.*

# VPS Provisioning — Telegram CEO Bot (ASPS-740 / ASPS-741 / ASPS-742 / ASPS-744)

Scripts to provision the Hostinger VPS that will run the ASPS Telegram CEO
bot (see `docs/task-memory/VPS_TELEGRAM_MIGRATION_HANDOFF.md` for the full
phased plan, target architecture, and scope decisions D1–D4).

## STATUS: authored, NOT YET EXECUTED

**No VPS exists yet.** Phase 0 (ASPS-739 — buy the box, Ubuntu 24.04
template, stage secrets) is a user action and has not happened. These
scripts have been written and validated statically (`bash -n` + shellcheck,
see below) but have **never run against a real host**. Do not treat them as
proven until they have been executed and verified on the actual VPS.

When Phase 0 completes, run them in order against the fresh box, over the
initial root session, and verify each step per its own log output before
moving on.

## Run order

| Order | Script | JIRA | Runs as | Does |
|---|---|---|---|---|
| 1 | [`01-harden.sh`](01-harden.sh) | ASPS-740 | root | OS updates, `aspsbot` user + SSH key, sshd hardening (socket-activation-aware, drop-in-precedence-safe), UFW, fail2ban, swap, timezone/hostname, secrets directory |
| 2 | [`02-toolchain.sh`](02-toolchain.sh) | ASPS-741 | root | Node 20 (keyring method), git/ripgrep/build-essential, Claude Code CLI, .NET 8 SDK, system Python 3 (3.12), Docker |
| 3 | [`03-clone.sh`](03-clone.sh) | ASPS-742 | aspsbot (root re-execs) | Clone/fast-forward the ASPS repo, git identity + credential helper, secrets templates in `SECRETS_DIR`, `npm ci && npm run build` the bot |
| 5 | [`05-service.sh`](05-service.sh) + [`telegram-ceo.service`](telegram-ceo.service) | ASPS-744 | root | Render + install the systemd unit, refuse to start on placeholder secrets, `enable --now`, verify |

*(There is no "04" script in this directory — Phase 4, ASPS-743, was the bot's
own code migration to the Claude Agent SDK, done in `apps/telegram-ceo/`, not
`deploy/vps/`. Numbering here follows the phase numbers in the handoff, not a
dense 1..N sequence.)*

`01-harden.sh` and `02-toolchain.sh` run as **root**. `03-clone.sh` must run
as `aspsbot` — if invoked as root it transparently re-execs itself as
`aspsbot` via `runuser --login` so the clone, git config, and secrets
templates are owned by `aspsbot`, not root. `05-service.sh` must run as
**root** (installing systemd units and `systemctl enable`/`daemon-reload`
require it). All four are **idempotent** — safe to re-run if a step fails
partway or you want to re-apply after a config change. All target **Ubuntu
24.04 LTS**.

## Prerequisites

1. The VPS exists (Phase 0 / ASPS-739), Ubuntu 24.04 LTS, and you have a
   root (or root-equivalent) session — typically the provider's browser
   console or an initial root SSH session using a password/key the provider
   gave you.
2. An SSH keypair for `aspsbot` generated **on your own machine**
   (`ssh-keygen -t ed25519 -C "aspsbot@asps-ceo-vps"`) — you need the
   **public** key for `config.env`. Keep the private key off the VPS
   entirely; it lives on whatever machine/phone you administer the box from.
3. Copy the scripts to the box (`scp -r deploy/vps root@<ip>:/root/vps-provisioning`
   or `git clone` the repo directly onto the box as root, run the scripts,
   then `rm -rf` the root-owned clone — do not leave a root-owned repo
   clone lying around after provisioning; the real clone Phase 3 makes lives
   under `aspsbot`, not `root`).

## Configuration

```bash
cd deploy/vps
cp config.env.example config.env
$EDITOR config.env   # fill in ASPSBOT_SSH_PUBLIC_KEY at minimum
```

`config.env` is **gitignored** — it is never committed. See
[`config.env.example`](config.env.example) for every variable, its default,
and why. At minimum you must set `ASPSBOT_SSH_PUBLIC_KEY` to a real public
key; the scripts refuse to run against the placeholder value.

Values with defaults you may want to review before running:

| Variable | Default | Note |
|---|---|---|
| `SSH_PORT` | `22` | Standard port (changed from a non-default `2222` in the ASPS-740 security remediation). At `22`, `01-harden.sh` deliberately leaves Ubuntu 24.04's `ssh.socket` activation in place (no restart) — see "sshd hardening correctness" below for why this is both simpler and safer than switching to `ssh.service`. For any other value, it disables `ssh.socket` and switches to `ssh.service` so the custom `Port` actually takes effect, and asserts sshd is actually listening on `SSH_PORT` before opening UFW — but the VPS also sits behind Hostinger's own cloud-level firewall/security group, separate from UFW and not managed by these scripts. **If you set a non-22 `SSH_PORT`, you must also open it in Hostinger's panel firewall yourself**, or you will be locked out even with UFW/sshd both correct. 22 is the default specifically to avoid that extra, easy-to-forget step (and the socket-switch risk entirely). |
| `LOCK_ROOT` | `false` | Whether `01-harden.sh` also runs `passwd -l root` on top of `PermitRootLogin no`. Off by default — see "Root lockout safety" below. |
| `SWAP_SIZE_GB` | `2` | Floor for .NET/Docker builds on a 4 GB box per the task spec. Bump to 4 if the box has less RAM and D4 (agent builds ASPS locally) is in active use. |
| `TIMEZONE` | `Asia/Jerusalem` | Change if you administer from elsewhere. |

## sshd hardening correctness (ASPS-740 security remediation, 2026-09-06; lockout fix, 2026-09-07)

A security review of the original scripts found a Blocker and two Majors in
the sshd-hardening logic; a subsequent **live execution on the real VPS**
(168.231.111.91, 2026-09-07) then exposed a further, more subtle bug in the
Blocker's own fix — all documented and fixed on this branch:

- **Ubuntu 24.04 socket activation (Blocker, then a live-lockout follow-up
  fix).** Fresh Ubuntu 24.04 ships `ssh.socket` (systemd socket activation)
  owning the SSH listening socket. While `ssh.socket` is active,
  `sshd_config`'s `Port` directive is **silently ignored** — sshd keeps
  listening on `:22` regardless of `SSH_PORT` — even though `sshd -t` still
  reports success (it only checks config syntax, not what actually ends up
  listening). Left unhandled, this would open only `SSH_PORT/tcp` in UFW
  while sshd stayed on 22, leaving the box unreachable on the port UFW
  allows.
  - The **first fix** (2026-09-06) had `01-harden.sh` unconditionally
    disable `ssh.socket` and switch to `ssh.service` for **any** `SSH_PORT`,
    including the default `22`. A live run against the real box then showed
    this was itself broken: the switch's `systemctl restart ssh.service`
    failed and left sshd outside its normal service-start path, which never
    created `/run/sshd` (the privilege-separation runtime directory
    `ssh.service` normally creates itself via its own `RuntimeDirectory=`
    on a clean start) — every new SSH connection then reset at
    `kex_exchange_identification`. UFW correctly stayed untouched (the
    fail-safe worked — see "authoritative post-merge/post-listen gate"
    below), but SSH itself was down and required the provider's console to
    recover (`mkdir -p /run/sshd` + a clean `systemctl restart
    ssh.service`).
  - **Root cause, precisely:** under socket activation, only `Port` is
    ignored — every OTHER directive in the `00-` drop-in
    (`PasswordAuthentication no`, `PermitRootLogin no`, `AllowUsers`, ...)
    **does** apply, because socket-activated sshd re-reads its config for
    each new connection. So for the default `SSH_PORT=22` — exactly what
    `ssh.socket` already listens on — the entire switch is unnecessary:
    socket activation already serves 22 with our hardened auth, with zero
    restart and zero lockout window.
  - **Current fix (2026-09-07):** `01-harden.sh` now branches on
    `SSH_PORT`:
    - **`SSH_PORT == 22` (default):** `ssh.socket` is left in place. The
      drop-in is written and validated with `sshd -t`; no restart or
      socket-to-service switch happens at all — the hardened auth takes
      effect on the next new connection automatically. This is the case
      that caused the live lockout, and it's now the case with the
      smallest — effectively zero — risk surface.
    - **`SSH_PORT != 22` (custom port):** the switch is still necessary
      (this is the one case where the ignored `Port` directive actually
      matters), but is now robust. The sequence is `disable --now
      ssh.socket`, `reset-failed`, `unmask`, `enable`, then
      `install -d -m 0755 /run/sshd` runs **before** `restart ssh.service`
      — creating the privilege-separation runtime dir ahead of the restart
      is the exact fix for what broke on the live box — then an explicit
      `systemctl is-active` check **and** `assert_sshd_listening
      "$SSH_PORT"`, both **before** UFW is touched; the script aborts if
      either fails.
  - Idempotent in all three shapes: fresh box with `ssh.socket` active
    (either port), a box where the switch already happened on an earlier
    run (non-22 case, re-run is a clean no-op through step 5), and the
    port-22 case where `ssh.socket` stays untouched indefinitely across
    re-runs.
- **`sshd_config.d/*.conf` precedence (Major).** sshd is first-value-wins
  and reads `sshd_config.d/*.conf` in lexical order. Cloud images commonly
  ship `50-cloud-init.conf` with `PasswordAuthentication yes`, which used to
  sort *before* the old `99-aspsbot-hardening.conf` and would win —
  password auth staying on while `sshd -t` still passed. The drop-in is now
  named `00-aspsbot-hardening.conf` (sorts first), and `01-harden.sh`
  additionally comments out `PasswordAuthentication`/`PermitRootLogin` in
  `50-cloud-init.conf` if present, as defense-in-depth.
- **Authoritative post-merge/post-listen gate.** Filename ordering and
  `sshd -t` are not treated as sufficient proof on their own. Before step 6
  ever touches UFW, `01-harden.sh` asserts, via `sshd -T` (the fully merged,
  effective config) and `ss -tlnp` (what's actually listening), that
  `PasswordAuthentication no` / `PermitRootLogin no` /
  `PubkeyAuthentication yes` are truly in effect and that sshd is truly
  bound to `SSH_PORT`. If either assertion fails, the script aborts
  **before** opening the firewall, so the box is never left reachable only
  on a port/config UFW would deny. These assertions run on every
  invocation, not just when something changed, to catch later drift (e.g.
  an unattended-upgrade reintroducing `ssh.socket`).

## Root lockout safety (ASPS-740 security remediation, Major #2)

The original script unconditionally ran `passwd -l root` after
`PermitRootLogin no` was applied. A security review found this could leave
the box with **no path to root at all**: `aspsbot` is created with no
password (its password is set manually by the operator on first console
login), so `sudo` has nothing to authenticate against until that happens —
locking root's password *before* that point means root is locked **and**
sudo doesn't work, i.e. provider-rescue-console only.

Fixed via the `LOCK_ROOT` config flag (default `false`):

- **`LOCK_ROOT=false` (default):** root's password is left untouched.
  `PermitRootLogin no` already blocks root over SSH entirely, so this
  costs nothing on the SSH attack surface — it only preserves a
  console/rescue path to root if `aspsbot`/`sudo` ever breaks later.
- **`LOCK_ROOT=true`:** `01-harden.sh` only actually runs `passwd -l root`
  if `passwd -S aspsbot` shows a usable password (`P`) — i.e. you have
  already run `passwd aspsbot` and confirmed `sudo -v` works. If not, it
  **skips** the lock with a loud warning rather than risk a total lockout,
  and additionally forces a password (re)set on `aspsbot`'s next login
  (`chage -d 0`) once it does lock root, since sudo becomes the sole
  escalation path at that point.

## Running

```bash
# On the VPS, as root, from the copied/cloned deploy/vps directory:
bash 01-harden.sh
# → follow the printed instructions: open a NEW terminal, confirm you can
#   SSH in as aspsbot on $SSH_PORT with your key, confirm sudo works,
#   BEFORE closing the root session that ran the script.

bash 02-toolchain.sh
# → prints version-check output for node/npm/dotnet/python3/docker/rg
#   at the end (non-fatal — reports what's missing rather than failing the
#   whole run, since a missing tool there is a "look into it" not a
#   "the box is broken" signal).

# Phase 3 (ASPS-742) — as root OR directly as aspsbot; re-execs to aspsbot
# automatically if run as root:
bash 03-clone.sh
# → clones/updates the repo, wires the git identity + credential helper,
#   writes secrets TEMPLATES (not real secrets) under SECRETS_DIR, builds
#   the bot. Prints exact next steps (fill in the two secrets files).

# --- operator step, not a script: fill in the real secrets ---
cp /home/aspsbot/secrets/access_keys.env.example    /home/aspsbot/secrets/ACCESS_KEYS.env
cp /home/aspsbot/secrets/telegram-ceo.env.example   /home/aspsbot/secrets/telegram-ceo.env
$EDITOR /home/aspsbot/secrets/ACCESS_KEYS.env        # fill in real GITHUB_TOKEN / JIRA_API_TOKEN / etc.
$EDITOR /home/aspsbot/secrets/telegram-ceo.env       # fill in real TELEGRAM_BOT_TOKEN / CLAUDE_CODE_OAUTH_TOKEN / AUTHORIZED_USERS
# if REPO_URL is HTTPS (the default): also fill in
$EDITOR /home/aspsbot/secrets/github-credentials     # real GitHub username + fine-grained PAT

# Phase 5 (ASPS-744) — as root:
bash 05-service.sh
# → refuses to run if either secrets file is missing or still placeholder;
#   renders telegram-ceo.service from config.env, installs it, enables +
#   starts it, verifies it's active.
```

See "Phase 3 — clone repo & wire secrets" and "Phase 5 — 24/7 systemd
service" below for the full detail on each of these two scripts.

## Secret placement rule (D2 + ASPS-745 box-level item 1)

**Secrets never live inside the agent's working tree.** The agent (Claude
Code / the Telegram bot's Agent SDK session) reads and writes inside
`CLONE_PATH` (`/home/aspsbot/ASPS` by default) — that tree is what an
over-broad `Read`/`Glob`/`Grep`, a prompt-injection, or a path-guard bug
could expose. Both `ACCESS_KEYS.env` and the bot's own `.env` must live
**outside** it:

- `01-harden.sh` creates `SECRETS_DIR` (`/home/aspsbot/secrets` by default)
  now, at provisioning time, mode `700`, owned by `aspsbot` — but does
  **not** populate it. That's why this directory exists even though nothing
  uses it yet in Phase 1/2.
- **Phase 3** (ASPS-742, `03-clone.sh`) writes *templates* —
  `${SECRETS_DIR}/access_keys.env.example` and
  `${SECRETS_DIR}/telegram-ceo.env.example` (chmod `600`, owned `aspsbot`)
  — with placeholder values only. The operator copies each to its real
  filename (`ACCESS_KEYS.env`, `telegram-ceo.env`) and fills in real values
  — the script itself never writes a real secret. Neither file is ever
  placed inside `CLONE_PATH`.
- **Phase 5** (ASPS-744, `05-service.sh` + `telegram-ceo.service`)
  references `${SECRETS_DIR}/telegram-ceo.env` **and**
  `${SECRETS_DIR}/ACCESS_KEYS.env` via two `EnvironmentFile=` lines in the
  `telegram-ceo.service` unit, so the bot's runtime process (and anything it
  spawns, including the Claude Agent SDK's own subprocess) gets both sets of
  secrets injected by systemd as ordinary environment variables — without
  ever having an `.env`/`ACCESS_KEYS.env` file inside the git clone it
  reads/writes. `05-service.sh` refuses to start the service at all if
  either file is missing or still contains a known placeholder marker.
- The path guard already shipped in `apps/telegram-ceo/src/security.ts`
  (`findSecretPathInInput` / `checkPathAllowed`, from the ASPS-743 security
  remediation) additionally hard-denies `*.env`/`*.key`/`ACCESS_KEYS*`/etc.
  patterns even *inside* the working tree as defense-in-depth — but "outside
  `cwd`" set up here is the primary control, not the fallback.

This directory's existence and permissions are the whole Phase 1/2
contribution to that rule; Phase 3 adds the templates, Phase 5 wires them
into the running service — see the two dedicated sections below.

## Phase 3 — clone repo & wire secrets (ASPS-742, `03-clone.sh`)

**Status: authored, NOT YET EXECUTED** — same caveat as Phase 1/2 (no VPS
exists yet). Static validation only (`bash -n` + shellcheck, see
"Validation performed").

### What it does

1. Clones `REPO_URL` into `CLONE_PATH` (default `/home/aspsbot/ASPS`).
   **Idempotent:** if `${CLONE_PATH}/.git` already exists, it `git fetch
   --prune`s and fast-forwards the detected default branch instead of
   re-cloning — it will **not** force-reset, rebase, or discard local work;
   a non-fast-forwardable clone aborts with instructions to resolve
   manually.
2. Sets `aspsbot`'s global git identity (`user.name`/`user.email` from
   `GIT_USER_NAME`/`GIT_USER_EMAIL` in `config.env`) and
   `safe.directory ${CLONE_PATH}`.
3. Wires the headless-push credential mechanism (see below).
4. Writes two secrets **templates** into `SECRETS_DIR` — never real values.
5. `npm ci && npm run build` in `apps/telegram-ceo` — only the bot, not the
   ASPS .NET backend (that stays on Azure per D1; a full `dotnet build` of
   the whole solution on this box is a Phase 1/2 execution-gate concern, out
   of scope here).
6. Verifies: `git status`, the build artifact
   (`apps/telegram-ceo/dist/index.js`) exists, both secrets templates exist
   at mode `600`.

### Running as aspsbot vs. root

`03-clone.sh` must run **as `aspsbot`** so everything it creates (the
clone, `~/.gitconfig`, the secrets templates) is owned by `aspsbot`, not
root. If you run it as root anyway (e.g. copying it alongside 01/02 in the
same root session), it transparently re-executes itself as `aspsbot` via
`runuser --login aspsbot --command "bash '<script>'"` — no separate `sudo -u
aspsbot bash 03-clone.sh` step needed, though that also works.

### Credential-helper decision (headless `git push`)

The task requires the agent to be able to `git push` from the VPS without
the token living inside the clone. Two git remote schemes are supported;
`03-clone.sh` detects which one `REPO_URL` uses and reacts accordingly —
this was a deliberate "support both, don't force one" choice, since Phase 0
(ASPS-739) explicitly left "fine-grained PAT or deploy key" open:

- **`REPO_URL=https://github.com/...` (the new default)** — the script
  configures git's **credential store helper, scoped to `github.com`
  only**:
  ```
  git config --global credential."https://github.com".helper \
      "store --file=${SECRETS_DIR}/github-credentials"
  ```
  This is deliberately **not** a global default `credential.helper` — the
  `credential."<url>".helper` form means the store file is only ever
  consulted for `https://github.com/...` remotes, nothing else. The script
  then creates `${SECRETS_DIR}/github-credentials` (chmod `600`, owned
  `aspsbot`) with a single placeholder line
  (`https://REPLACE_WITH_GITHUB_USERNAME:REPLACE_WITH_GITHUB_TOKEN@github.com`)
  **only if the file doesn't already exist** — it never overwrites an
  operator-filled credential. The operator replaces the placeholder with the
  real GitHub username + a **fine-grained PAT scoped to
  `isaacmendelson/ASPS` only, contents read/write** (ASPS-745 box-level item
  4 — least-privilege token scope; decided here as "repo-scoped fine-grained
  PAT", reviewed again at the Phase 6 security audit). The token value is
  never written by the script.
- **`REPO_URL=git@github.com:...` or `ssh://...`** — git's credential-helper
  mechanism doesn't apply to SSH remotes at all, so the script detects the
  scheme and **skips** credential-helper setup entirely, logging that a
  deploy key must be placed under `~aspsbot/.ssh` manually. A private SSH
  key is a higher-sensitivity artifact than a PAT-in-a-store-file, and
  intentionally is not generated or placed by any of these scripts.

`config.env.example`'s `REPO_URL` default was changed from the SSH form
(`git@github.com:...`, ASPS-740/741 default) to the HTTPS form specifically
so the credential-helper mechanism above applies out of the box — flag to
CEO/security if a deploy key is actually preferred; switching `REPO_URL`
back to the SSH form is a one-line `config.env` change and `03-clone.sh`
degrades correctly either way.

### The two secrets templates

| Template (written by `03-clone.sh`) | Copy to (operator, manual) | Keys |
|---|---|---|
| `${SECRETS_DIR}/access_keys.env.example` | `${SECRETS_DIR}/ACCESS_KEYS.env` | `GITHUB_REPO_URL`, `GITHUB_USERNAME`, `GITHUB_TOKEN`, `JIRA_BASE_URL`, `JIRA_EMAIL`, `JIRA_API_TOKEN` — mirrors the repo-root `ACCESS_KEYS.env` shape |
| `${SECRETS_DIR}/telegram-ceo.env.example` | `${SECRETS_DIR}/telegram-ceo.env` | `TELEGRAM_BOT_TOKEN`, `CLAUDE_CODE_OAUTH_TOKEN`, `AUTHORIZED_USERS`, `WORKING_DIR` (pre-filled to `CLONE_PATH`), `MODEL`, `MAX_TURNS`, `APPROVAL_TIMEOUT_MS` — mirrors `apps/telegram-ceo/.env.example` |

Both templates are chmod `600`, owned `aspsbot`, written with
`write_if_changed` (never overwrites an existing file with different
content — safe to re-run `03-clone.sh` after the operator has started
editing the real files, since the `.example` and the real filename are
different files).

## Phase 5 — 24/7 systemd service (ASPS-744, `05-service.sh` + `telegram-ceo.service`)

**Status: authored, NOT YET EXECUTED.** `telegram-ceo.service` is a
**template** — it ships with literal `@ASPSBOT_USER@` / `@CLONE_PATH@` /
`@SECRETS_DIR@` tokens and must never be installed as-is; `05-service.sh`
renders it via `sed` from `config.env`, aborts if any `@TOKEN@` is left
unsubstituted (a template/script drift guard), then installs, enables, and
starts it.

### What `05-service.sh` does

1. **Guard:** refuses to proceed if `${SECRETS_DIR}/telegram-ceo.env` or
   `${SECRETS_DIR}/ACCESS_KEYS.env` is missing or still contains a known
   placeholder marker (`your-bot-token-here`, `REPLACE_WITH_`, etc. — see
   `lib.sh`'s `file_has_placeholder_value`). Also refuses if the bot hasn't
   been built yet (`apps/telegram-ceo/dist` missing — run `03-clone.sh`
   first).
2. Renders + installs the unit (`write-if-changed` semantics — only
   `daemon-reload`s if the rendered content actually changed).
3. `systemctl enable --now` (survives reboot) and verifies
   `systemctl is-active`.
4. Prints a log-tail command and a one-liner to confirm no secrets leaked
   into the journal.

### Secret loading — two `EnvironmentFile=` directives

```ini
EnvironmentFile=@SECRETS_DIR@/telegram-ceo.env
EnvironmentFile=@SECRETS_DIR@/ACCESS_KEYS.env
```

**Decision:** load `ACCESS_KEYS.env` as a **second `EnvironmentFile=`**,
not via the git credential-helper file from Phase 3. Reasoning: the
credential-helper file (`${SECRETS_DIR}/github-credentials`) only ever
serves `git push`/`git pull` itself — it has no way to expose
`JIRA_API_TOKEN`, `JIRA_EMAIL`, or a bare `GITHUB_TOKEN` env var to the
agent's own `Bash` tool calls (e.g. a `curl` against the JIRA REST API, the
project's documented pattern per `reference_jira.md`). A second
`EnvironmentFile=` puts both sets of secrets into the service's actual
process environment, inherited by `node`, inherited by anything `node`
spawns (the Claude Agent SDK's own Claude Code subprocess, and any `Bash`
tool invocation that subprocess makes) — exactly where the agent's
Bash-tool commands already expect to find them (matching how a local
Claude Code session reads `ACCESS_KEYS.env`-sourced variables today).
Neither directive is prefixed with `-` (which would make a missing file a
silent no-op) — a missing secrets file fails the unit loudly, not a
half-configured bot.

### systemd hardening — what's applied and what's deliberately not

**Updated 2026-09-07 (ASPS-745 Phase 6 audit fold)** — see "Phase 6 audit
fixes" below for the full before/after. Summary of the current state:

Applied (verified compatible with node/git/npm/dotnet and the docker
**client** — the daemon itself, `dockerd`, runs as its own separate,
unsandboxed systemd unit and is unaffected by anything below):

`NoNewPrivileges`, `ProtectSystem=strict` + `ReadWritePaths=${CLONE_PATH}
/home/${ASPSBOT_USER}/.claude /home/${ASPSBOT_USER}/.config
/home/${ASPSBOT_USER}/.cache /home/${ASPSBOT_USER}/.npm` (note:
`${SECRETS_DIR}` is **not** in `ReadWritePaths` — read-only is sufficient
and correct, see M5 below), `ProtectControlGroups`, `RestrictSUIDSGID`,
`PrivateTmp`, `ProtectKernelModules`, `ProtectKernelLogs`,
`ProtectKernelTunables`, `ProtectClock`, `ProtectHostname`,
`LockPersonality`, `RestrictRealtime`, `RestrictNamespaces`, `UMask=0077`
(M3 in the audit — see below), plus `SupplementaryGroups=docker` (required
for D4 — see the docker-group warning below, unchanged from Phase 2).

**Deliberately NOT applied — DEFERRED ASPS-745 follow-ups, flagged for an
on-box `systemd-analyze security` pass + live tool-call smoke test before
enabling, not silently chosen either way:**

| Directive | Why it's tempting | Why it's not enabled here |
|---|---|---|
| `ProtectHome=yes` | Extra isolation of `/home` beyond `ProtectSystem=strict` | Makes `~/.gitconfig` (the Phase 3 credential-helper config, git identity, `safe.directory`) **inaccessible**, not merely read-only — it lives directly under `/home/aspsbot`, outside the `ReadWritePaths` entries. `ProtectSystem=strict` already makes the *entire* filesystem read-only except the declared paths, which already satisfies "the bot can only write to its clone + its own `$HOME` state" — read access to `~/.gitconfig` is unaffected by `strict` alone, since read-only ≠ invisible. Adding `ProtectHome=yes` on top would only add risk (breaking git push) for no meaningful extra confinement `strict` doesn't already provide. **Confirmed correct as OFF by the Phase 6 audit — keep OFF, no further action.** |
| `PrivateDevices=yes` | Blocks access to physical devices under `/dev` | Probably safe — the docker *client* only needs the unix socket (`/run/docker.sock`, reachable via `connect()` even on a read-only mount), not `/dev/*`, and `/dev/null|zero|random|urandom|tty` remain available under `PrivateDevices` regardless. **Deferred** — needs a live `docker compose` smoke test on the real box before enabling. |
| `SystemCallFilter=@system-service` + `SystemCallArchitectures=native` + `SystemCallErrorNumber=EPERM` | Reduces the kernel attack surface materially | No safe filter set could be derived without live testing against node + git + npm + dotnet + the docker CLI + every `Bash`-tool command the agent might ever run. A wrong filter fails **closed** (crash-loop) rather than open — worse for an unattended 24/7 bot with nobody local to debug a broken syscall filter over Telegram. `@system-service` is Node-compatible by design. **Deferred** — enable, run one real `docker compose` + `git push` + `dotnet build` turn, watch for `EPERM`, iterate with `systemd-analyze security telegram-ceo.service`. |
| `CapabilityBoundingSet=` (empty) / `AmbientCapabilities=` | Drops all Linux capabilities the process doesn't need | Node/git/docker-client need no elevated capabilities under `NoNewPrivileges=yes` — graceful no-op if wrong. **Deferred** alongside the syscall filter pass so all remaining sandbox changes get one combined on-box verification instead of several separate service restarts. |
| `RestrictAddressFamilies=AF_UNIX AF_INET AF_INET6` | Blocks any socket family the service doesn't need | The service only needs Telegram/Anthropic/GitHub/JIRA HTTPS (`AF_INET`/`AF_INET6`) and the Docker socket (`AF_UNIX`). **Deferred** alongside the syscall filter pass. |
| `ProtectProc=invisible` | Hides other users' `/proc` entries | Single-user box, so confinement value is marginal, but cheap. **Deferred** alongside the syscall filter pass rather than added in isolation. |
| `MemoryDenyWriteExecute=yes` | Blocks W^X memory violations (a common RCE primitive) | Node's V8 JIT can conflict with strict W^X enforcement on some builds/architectures — systemd's own docs flag JIT compilers as the known-incompatible case. **Confirmed correct as OFF by the Phase 6 audit — keep OFF, no further action.** |

This is the explicit tension the task called out: D4 requires the agent to
have broad host access (write its own clone, use the Docker socket) while
the bot runs unattended 24/7 with no one locally available to fix a
broken-closed crash loop — so every directive still deferred above is one
where getting it wrong either silently reopens a control or hard-crashes
the service, and neither failure mode is acceptable to guess without the
live smoke test called out per-row.

### `systemd-analyze verify`

No systemd exists on the Windows dev host, so this was verified inside a
disposable `ubuntu:24.04` Docker container instead (systemd unit
verification is static — it doesn't require a running systemd instance for
syntax/semantics checking): rendered `telegram-ceo.service` with sample
values (`aspsbot` / `/home/aspsbot/ASPS` / `/home/aspsbot/secrets`),
installed a systemd package, stubbed `/usr/bin/node` and the `aspsbot`
user/paths so directive *targets* exist, and ran `systemd-analyze verify` —
**exit 0, no errors** (an initial run without the `node`/`aspsbot` stubs
correctly flagged `Command /usr/bin/node is not executable`, confirming the
tool is actually checking what it claims to). This confirms the unit's
syntax and directive set are valid; it does **not** confirm the hardening
directives behave correctly against the real Node/git/Docker workload —
that's the unverified part called out in the table above, and the real
verification happens at the Phase 1 execution gate.

## Phase 6 audit fixes (ASPS-745, folded 2026-09-07)

A Phase 6 security audit (`docs/security-audits/2026-09-07-vps-telegram.md`)
ran against the **live** box after Phases 0–5 were executed. Several
findings were fixed live on the box (to unblock/verify immediately) and are
folded back into these templates here so a **future re-provision** (a
rebuild, a second box, disaster recovery) gets them automatically instead of
depending on a hand-applied `systemctl edit` override that isn't tracked in
git. This section is the record of what changed and why; the audit report
itself is the source of truth for severity/reasoning.

| Audit finding | Fix folded here | File |
|---|---|---|
| **M4** — fail2ban `sshd` jail showed `Total failed: 0`/`Banned: 0` despite real auth failures. Root cause: Ubuntu 24.04 socket-activates sshd, so failures log under transient `ssh@<n>-...service` units, not `sshd.service` — fail2ban's default `journalmatch` (`_SYSTEMD_UNIT=sshd.service`) never saw them. | Added `journalmatch = _COMM=sshd` to the `[sshd]` block written to `/etc/fail2ban/jail.local` — matches on the process command name, constant across socket- and service-activated sshd. | `01-harden.sh` step 7 |
| **M5** — `telegram-ceo.service` granted the process **read+write** on `${SECRETS_DIR}`, even though systemd injects `EnvironmentFile=` content as PID 1 before the sandbox applies — the process needs zero filesystem access to its own secrets directory to see them as env vars. Only git-credential-store needs to *read* `github-credentials`, which `ProtectSystem=strict`'s default read-only already covers. | Removed `@SECRETS_DIR@` from `ReadWritePaths` entirely (it stays present on disk, mode `700`, just read-only under the sandbox now — not literally `InaccessiblePaths`, since the credential-helper read still needs to succeed). | `telegram-ceo.service` |
| **m3** — `systemd-analyze` flagged `UMask=` unset, defaulting to world-readable (`0022`) for service-created files. | Added `UMask=0077`. | `telegram-ceo.service` |
| *(live-only, not a numbered audit finding but applied alongside M5)* — the service failed to start under `ProtectSystem=strict` because the Claude Code CLI + npm write state under `$HOME` (`~/.claude`, `~/.config`, `~/.npm` caches) even with `WorkingDirectory=${CLONE_PATH}`. | Added `/home/@ASPSBOT_USER@/.claude /home/@ASPSBOT_USER@/.config /home/@ASPSBOT_USER@/.cache /home/@ASPSBOT_USER@/.npm` to `ReadWritePaths`; `05-service.sh` now `mkdir -p`s each (owned `@ASPSBOT_USER@`) before rendering/installing the unit, step 2/6, so the directories exist for `ProtectSystem=strict` to bind read-write on a fresh box. | `telegram-ceo.service`, `05-service.sh` |
| **m1** — UFW showed `80/tcp` + `443/tcp` `ALLOW IN Anywhere` (both families) with nothing listening on either port. Investigated: `01-harden.sh` only ever opens `${SSH_PORT}/tcp` — these two rules are **pre-existing box drift** (likely a leftover from a prior image/setup), not something our scripts introduced. | **No code change** — nothing in `01-harden.sh` opens 80/443. Documented here so a re-provision operator knows to run `sudo ufw status verbose` after `01-harden.sh` and confirm no stray `ALLOW` rules remain beyond the SSH one; if any are found on a fresh box, that's new drift to investigate, not an expected side effect of this script. | Documentation only |

**Deferred (documented, not enabled) — the "Deliberately NOT applied" table
above** covers `PrivateDevices=yes`, `SystemCallFilter=@system-service` (+
`SystemCallArchitectures=native` + `SystemCallErrorNumber=EPERM`),
`CapabilityBoundingSet=`/`AmbientCapabilities=`,
`RestrictAddressFamilies=AF_UNIX AF_INET AF_INET6`, and `ProtectProc=invisible`
— each needs an on-box `systemd-analyze security telegram-ceo.service` pass
plus a live tool-call smoke test (real `git push` + `docker compose` +
`dotnet build` + a representative Bash-tool turn) before enabling, since a
wrong guess fails **closed** (crash-loop, unattended 24/7 bot, nobody local
to debug it over Telegram). `MemoryDenyWriteExecute=yes` and `ProtectHome=yes`
remain correctly OFF per the audit's own reasoning (V8 JIT and
`~/.gitconfig` accessibility respectively) — not deferred, confirmed-final.

**Not folded here — tracked separately, need the USER or a server-side
change, not a `deploy/vps/*` template edit:** M1 (verify/re-scope/rotate the
GitHub PAT), M2 (enable `main` branch protection on GitHub), M3
(rootless-Docker/socket-proxy decision for the `docker` group — see
"Docker-group privilege warning" below), and token rotation generally. See
the audit report's "Items requiring the USER" section.

## Deferred to later phases (ASPS-745 box-level items NOT done here)

The ASPS-745 JIRA comment (Phase 4 security review follow-through) lists
four box-level items. Item (1) — secret relocation — is now mostly done
(directory in Phase 1/2, templates + credential mechanism in Phase 3, wiring
into the service in Phase 5); items (2)–(4) remain explicitly **out of
scope** for `deploy/vps/*.sh` and stay tracked on ASPS-745 (Phase 6,
security deepening & audit):

1. ~~Secret relocation~~ — directory + templates now done (Phase 1/2 + 3);
   the operator still has to actually copy+fill the two `*.env` files and,
   for HTTPS, `github-credentials` — that manual step is the only remaining
   part of item (1).
2. **Outbound network egress restriction** (Telegram + Anthropic +
   GitHub/JIRA endpoints only) — `01-harden.sh`'s UFW rules are
   `default allow outgoing` (matching the task's explicit Phase 1 spec: the
   bot only needs outbound long-poll, so no inbound app port is opened, but
   egress is not yet narrowed). Narrowing egress belongs to Phase 6.
3. **`main` branch protection** on GitHub (server-side, no direct pushes) —
   not a VPS/box setting at all; a GitHub repo setting, tracked on ASPS-745.
4. **Least-privilege GitHub token** scope for the deploy credential —
   decided in Phase 3 (`03-clone.sh`): a **fine-grained PAT scoped to
   `isaacmendelson/ASPS` only, contents read/write**, stored in
   `${SECRETS_DIR}/github-credentials` via git's credential store helper
   scoped to `github.com`. Flagged for a final confirmation at the Phase 6
   security audit (e.g. whether an even narrower scope, or a GitHub App
   installation token with short-lived credentials, is preferred over a
   long-lived PAT).

Do not read the presence of `SECRETS_DIR` in `01-harden.sh` as satisfying
all of ASPS-745 — it only satisfies the directory-location prerequisite.

## Docker-group privilege warning (D4 / ASPS-745)

`02-toolchain.sh` adds `aspsbot` to the `docker` group so the CEO agent can
`docker compose up` the ASPS stack locally (D4 — "yes, the agent can
build/test ASPS on the VPS"). **`docker` group membership is root-equivalent**
(bind-mount the host FS into a container, read/write anything). This is
known, accepted debt for this phase — flagged loudly in the script itself
and here — pending explicit Security sign-off or mitigation (rootless
Docker, a separate friction-adding account, or dropping D4) on ASPS-745.
See `.claude/rules/security-rules.md` "Known security debt" clause — this
is being logged, not introduced silently.

## Toolchain install hygiene (ASPS-740/741 security remediation Minors)

- **Node.js install method.** `02-toolchain.sh` used to install Node via
  NodeSource's `curl -fsSL ... | bash -` setup script — an unauthenticated
  script piped straight into a root shell. It now uses the keyring method:
  fetch the GPG key to `/etc/apt/keyrings/nodesource.gpg`, reference it via
  `signed-by=` in `/etc/apt/sources.list.d/nodesource.list` — the same
  trusted-repo pattern already used for the Docker install (DRY).
- **Python: dropped deadsnakes, use system Python 3.12.** The original
  script added the third-party `deadsnakes` PPA to get Python 3.11, to
  match the desktop agent's stack version. Re-checked against what the VPS
  actually needs: per D4 the agent's build/test path for ASPS is `docker
  compose up`, and the Analyzers carry their own Python **inside** their
  containers (see `.claude/memory/feedback_docker_deps_from_lockfile.md`)
  — the VPS host itself never runs analyzer/desktop-agent Python directly,
  it only needs a general-purpose Python 3 for host-level scripting. There
  is no concrete host-level need for exactly 3.11, so `02-toolchain.sh` now
  installs Ubuntu 24.04's built-in `python3` (3.12) + `python3-venv` +
  `python3-dev` + `python3-pip`, avoiding a third-party PPA to trust and
  keep patched. If a real host-level 3.11 need surfaces later, re-add
  `deadsnakes` and document the concrete reason at that point.
- **Claude Code CLI version.** Left intentionally **floating**
  (`npm install -g @anthropic-ai/claude-code`, no pinned version) rather
  than pinned — documented per `review-standards.md`'s "pin or document
  why floating is accepted": the CLI ships frequent releases and is
  designed to self-update (`claude update`); pinning would mean
  hand-editing this script on every upstream release for no compensating
  security benefit, since it's a dev-tool CLI, not a versioned artifact
  baked into a production container image (the case CLAUDE.md's "pin base
  image versions" rule targets). An idempotency guard was added regardless
  — the script now checks `command -v claude` first and only installs when
  missing, so re-runs don't force a reinstall/network round-trip.
- **.NET SDK `.deb` bootstrap** — left as documented, accepted debt per the
  original review guidance (trivial-to-pin threshold not met; Microsoft's
  own per-Ubuntu-version config package is the standard bootstrap path).

## Validation performed (why this satisfies TDD rule item 9)

There is no VPS to run these scripts against yet, so conventional Red/Green
unit testing does not apply — this is declarative infrastructure
configuration executed once, non-interactively, against a real OS. Per
`CLAUDE.md`'s TDD rule item 9 ("generated code, documentation-only changes,
and purely declarative configuration may use validation or contract checks
instead of unit-level Red/Green... document why and use the strongest
automated verification available"), the following was used instead:

- `bash -n <script>` — syntax-checks every script; all five (`lib.sh`,
  `01-harden.sh`, `02-toolchain.sh`, `03-clone.sh`, `05-service.sh`) pass
  clean. `03-clone.sh`/`05-service.sh` re-verified 2026-09-07 (ASPS-742/
  ASPS-744 authoring).
- `shellcheck --shell=bash` (via the official `koalaman/shellcheck:stable`
  Docker image, since shellcheck isn't installed locally) — all five
  scripts pass with **zero findings** (info, style, warning, or error).
- `telegram-ceo.service`: rendered with sample values
  (`aspsbot`/`/home/aspsbot/ASPS`/`/home/aspsbot/secrets`) and run through
  `systemd-analyze verify` inside a disposable `ubuntu:24.04` Docker
  container (with a stub `/usr/bin/node` and `aspsbot` user/paths created so
  directive targets exist) — **exit 0, no errors**. See "Phase 5 —
  `systemd-analyze verify`" above for the exact method and why a stubbed
  container is sufficient for *static* unit verification without a real
  systemd host.
- Line endings: `git check-attr text eol -- deploy/vps/03-clone.sh
  deploy/vps/05-service.sh deploy/vps/telegram-ceo.service` → `eol: lf` for
  all three; a byte-level check confirms zero `\r\n`/lone-`\r` occurrences.
  `*.service text eol=lf` was added to the repo-root `.gitattributes`
  alongside the existing `*.sh text eol=lf` rule.
- Idempotency was designed in and reasoned through line-by-line for all
  five scripts (every mutating step checks current state first: `id -u`,
  `dpkg -s`, `grep -qxF`/`grep -qE`, `ufw status`, `swapon --show`,
  `timedatectl show`, `command -v`, `write_if_changed`'s
  content-compare-before-write, `git symbolic-ref`/`fetch`/`merge
  --ff-only` for the clone step, `cmp -s` for the rendered systemd unit)
  rather than verified by an actual second run, since there is no box to
  run it on twice yet.
- The `sshd -t` validation-before-reload step inside `01-harden.sh` itself
  is the runtime equivalent of a "test before applying" gate — it is part
  of the script's own logic, not a substitute for it. It is now
  supplemented (not replaced) by the post-merge/post-listen assertions
  described in "sshd hardening correctness" above, since `sshd -t` alone
  was shown by security review to pass even when the effective config or
  actual listening port didn't match intent. `05-service.sh`'s
  placeholder-secrets guard and unsubstituted-`@TOKEN@` guard are the same
  kind of "abort before doing anything irreversible" gate for Phase 5.
- No pure-bash helper was extracted that lends itself to isolated unit
  testing beyond what `bash -n`/shellcheck already cover — the assertion
  functions in `lib.sh` (`assert_effective_sshd_config`,
  `assert_sshd_listening`, `ssh_socket_activation_active`,
  `require_user_or_reexec`, `file_has_placeholder_value`) all depend on
  live `sshd -T`/`ss`/`systemctl`/`id`/filesystem state on the target host
  and cannot be meaningfully unit-tested without a real (or containerized)
  environment; they are exercised for real on first execution against the
  actual VPS, same as the rest of this directory. `telegram-ceo.service`'s
  `systemd-analyze verify` run (above) is the one exception — that check
  genuinely doesn't require a live host, so it was actually run rather than
  only reasoned through.

**This is not a substitute for real execution.** The first real run on the
actual VPS (Phase 1/2/3/5 execution, still gated on Phase 0/ASPS-739) is the
actual verification of this code — the DoD for ASPS-740/741/742/744 is not
met until that run happens and is confirmed (see the handoff for the
continuation point).

## Files in this directory

| File | Purpose |
|---|---|
| `config.env.example` | Placeholder config — copy to `config.env` (gitignored) and fill in real values. |
| `lib.sh` | Shared logging / config-loading / idempotency helpers, sourced by all four numbered scripts (DRY — not run directly). |
| `01-harden.sh` | Phase 1 baseline hardening (ASPS-740). |
| `02-toolchain.sh` | Phase 2 runtime toolchain (ASPS-741). |
| `03-clone.sh` | Phase 3 clone repo & wire secrets (ASPS-742). |
| `telegram-ceo.service` | Phase 5 systemd unit template (ASPS-744) — rendered by `05-service.sh`, do not install directly. |
| `05-service.sh` | Phase 5 install/enable/start the systemd service (ASPS-744). |
| `README.md` | This file. |

Line endings: all `.sh` files in this repo are forced to LF via the
repo-root [`.gitattributes`](../../.gitattributes) (`*.sh text eol=lf`), and
`.service` files likewise (`*.service text eol=lf`, added alongside it for
this task) — no separate `deploy/vps/.gitattributes` needed. Verified with
`git check-attr text eol -- deploy/vps/*.sh deploy/vps/*.service` and a
byte-level `\r\n`/lone-`\r` count (zero in all files).

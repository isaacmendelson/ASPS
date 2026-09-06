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
| `SSH_PORT` | `22` | Standard port (changed from a non-default `2222` in the ASPS-740 security remediation). `01-harden.sh` handles Ubuntu 24.04's `ssh.socket` activation and asserts sshd is actually listening on `SSH_PORT` before opening UFW, so a custom port is safe with respect to *this script* — but the VPS also sits behind Hostinger's own cloud-level firewall/security group, separate from UFW and not managed by these scripts. **If you set a non-22 `SSH_PORT`, you must also open it in Hostinger's panel firewall yourself**, or you will be locked out even with UFW/sshd both correct. 22 is the default specifically to avoid that extra, easy-to-forget step. |
| `LOCK_ROOT` | `false` | Whether `01-harden.sh` also runs `passwd -l root` on top of `PermitRootLogin no`. Off by default — see "Root lockout safety" below. |
| `SWAP_SIZE_GB` | `2` | Floor for .NET/Docker builds on a 4 GB box per the task spec. Bump to 4 if the box has less RAM and D4 (agent builds ASPS locally) is in active use. |
| `TIMEZONE` | `Asia/Jerusalem` | Change if you administer from elsewhere. |

## sshd hardening correctness (ASPS-740 security remediation, 2026-09-06)

A security review of the original scripts found a Blocker and two Majors in
the sshd-hardening logic, all fixed on this branch:

- **Ubuntu 24.04 socket activation (Blocker).** Fresh Ubuntu 24.04 ships
  `ssh.socket` (systemd socket activation) owning the SSH listening socket.
  While `ssh.socket` is active, `sshd_config`'s `Port` directive is
  **silently ignored** — sshd keeps listening on `:22` regardless of
  `SSH_PORT` — even though `sshd -t` still reports success (it only checks
  config syntax, not what actually ends up listening). Left unhandled, this
  would open only `SSH_PORT/tcp` in UFW while sshd stayed on 22, leaving the
  box unreachable on the port UFW allows. `01-harden.sh` now detects
  `ssh.socket` activation, disables it, and switches to `ssh.service`
  binding the port directly (idempotent — no-op on re-runs or non-default
  images), and **restarts** (not reloads) `ssh.service` when this switch
  happens, since a reload alone would not bind the new listener.
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

Applied (verified compatible with node/git/npm/dotnet and the docker
**client** — the daemon itself, `dockerd`, runs as its own separate,
unsandboxed systemd unit and is unaffected by anything below):

`NoNewPrivileges`, `ProtectSystem=strict` + `ReadWritePaths=${CLONE_PATH}
${SECRETS_DIR}`, `ProtectControlGroups`, `RestrictSUIDSGID`, `PrivateTmp`,
`ProtectKernelModules`, `ProtectKernelLogs`, `ProtectKernelTunables`,
`ProtectClock`, `ProtectHostname`, `LockPersonality`, `RestrictRealtime`,
`RestrictNamespaces`, plus `SupplementaryGroups=docker` (required for D4 —
see the docker-group warning below, unchanged from Phase 2).

**Deliberately NOT applied — flagged for Security to decide/test at the
Phase 1 execution gate, not silently chosen either way:**

| Directive | Why it's tempting | Why it's not enabled here |
|---|---|---|
| `ProtectHome=yes` | Extra isolation of `/home` beyond `ProtectSystem=strict` | Makes `~/.gitconfig` (the Phase 3 credential-helper config, git identity, `safe.directory`) **inaccessible**, not merely read-only — it lives directly under `/home/aspsbot`, outside both `ReadWritePaths` entries. `ProtectSystem=strict` already makes the *entire* filesystem read-only except the two declared paths, which already satisfies "the bot can only write to its clone + secrets" — read access to `~/.gitconfig` (and any `$HOME`-relative Claude Code CLI state) is unaffected by `strict` alone, since read-only ≠ invisible. Adding `ProtectHome=yes` on top would only add risk (breaking git push) for no meaningful extra confinement `strict` doesn't already provide. |
| `PrivateDevices=yes` | Blocks access to physical devices under `/dev` | Probably safe — the docker *client* only needs the unix socket (`/run/docker.sock`, reachable via `connect()` even on a read-only mount), not `/dev/*`, and `/dev/null|zero|random|urandom|tty` remain available under `PrivateDevices` regardless. But this is **unverified against a live box** (none exists yet) — recommended for Security to confirm and enable at the Phase 1 execution gate rather than assumed safe here. |
| `SystemCallFilter=...` | Reduces the kernel attack surface materially | No safe filter set could be derived without live testing against node + git + npm + dotnet + the docker CLI + every `Bash`-tool command the agent might ever run. A wrong filter fails **closed** (crash-loop) rather than open — worse for an unattended 24/7 bot with nobody local to debug a broken syscall filter over Telegram. Left to Phase 6 with iterative `systemd-analyze security telegram-ceo.service` feedback once the box exists. |
| `MemoryDenyWriteExecute=yes` | Blocks W^X memory violations (a common RCE primitive) | Node's V8 JIT can conflict with strict W^X enforcement on some builds/architectures — systemd's own docs flag JIT compilers as the known-incompatible case. Not verified here. |

This is the explicit tension the task called out: D4 requires the agent to
have broad host access (write its own clone, reach `SECRETS_DIR`, use the
Docker socket) while the bot runs unattended 24/7 with no one locally
available to fix a broken-closed crash loop — so every directive above the
line is one where getting it wrong is either a no-op or a graceful
degradation, and everything below the line is one where getting it wrong
either silently reopens a control (`ProtectHome`'s read-vs-inaccessible
subtlety) or hard-crashes the service (syscall/memory filtering), and
neither failure mode is acceptable to guess against a host that doesn't
exist yet.

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

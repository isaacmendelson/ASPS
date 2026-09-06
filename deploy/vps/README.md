# VPS Provisioning — Telegram CEO Bot (ASPS-740 / ASPS-741)

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

| Order | Script | JIRA | Does |
|---|---|---|---|
| 1 | [`01-harden.sh`](01-harden.sh) | ASPS-740 | OS updates, `aspsbot` user + SSH key, sshd hardening (socket-activation-aware, drop-in-precedence-safe), UFW, fail2ban, swap, timezone/hostname, secrets directory |
| 2 | [`02-toolchain.sh`](02-toolchain.sh) | ASPS-741 | Node 20 (keyring method), git/ripgrep/build-essential, Claude Code CLI, .NET 8 SDK, system Python 3 (3.12), Docker |
| 3 | *(later story, not in this directory yet)* | ASPS-742 | Clone the ASPS repo, wire `ACCESS_KEYS.env` + bot `.env`, git identity/credential helper |

Both `01-harden.sh` and `02-toolchain.sh` run as **root**, on **Ubuntu
24.04 LTS**, and are **idempotent** — safe to re-run if a step fails partway
or you want to re-apply after a config change.

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
```

Then Phase 3 (a later story, ASPS-742) clones the repo and wires secrets.

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
- **Phase 3** (ASPS-742, not yet authored) is expected to place the
  repo-local `ACCESS_KEYS.env` (GitHub + JIRA tokens) at
  `${SECRETS_DIR}/ACCESS_KEYS.env`, chmod `600`, and the bot's `.env`
  (`TELEGRAM_BOT_TOKEN`, `AUTHORIZED_USERS`, `CLAUDE_CODE_OAUTH_TOKEN`,
  etc. — see `apps/telegram-ceo/.env.example`) at
  `${SECRETS_DIR}/telegram-ceo.env`, chmod `600` — **not** inside
  `CLONE_PATH`.
- **Phase 5** (ASPS-744, systemd service) is expected to reference
  `${SECRETS_DIR}/telegram-ceo.env` via `EnvironmentFile=` in the
  `telegram-ceo.service` unit, so the bot's runtime process gets its secrets
  injected by systemd without ever having a `.env` file inside the git
  clone it reads/writes.
- The path guard already shipped in `apps/telegram-ceo/src/security.ts`
  (`findSecretPathInInput` / `checkPathAllowed`, from the ASPS-743 security
  remediation) additionally hard-denies `*.env`/`*.key`/`ACCESS_KEYS*`/etc.
  patterns even *inside* the working tree as defense-in-depth — but "outside
  `cwd`" set up here is the primary control, not the fallback.

This directory's existence and permissions are the whole Phase 1/2
contribution to that rule; actually placing files in it is Phase 3/5 work,
tracked separately.

## Deferred to later phases (ASPS-745 box-level items NOT done here)

The ASPS-745 JIRA comment (Phase 4 security review follow-through) lists
four box-level items. Only item (1) — secret relocation — has anything to
do with Phase 1/2 provisioning, and only the directory/permissions part of
it (see above). The rest are explicitly **out of scope** for these two
scripts and remain tracked on ASPS-745 (Phase 6, security deepening &
audit):

1. ~~Secret relocation~~ — directory prepared here; population is Phase 3/5.
2. **Outbound network egress restriction** (Telegram + Anthropic +
   GitHub/JIRA endpoints only) — `01-harden.sh`'s UFW rules are
   `default allow outgoing` (matching the task's explicit Phase 1 spec: the
   bot only needs outbound long-poll, so no inbound app port is opened, but
   egress is not yet narrowed). Narrowing egress belongs to Phase 6.
3. **`main` branch protection** on GitHub (server-side, no direct pushes) —
   not a VPS/box setting at all; a GitHub repo setting, tracked on ASPS-745.
4. **Least-privilege GitHub token** scope for the deploy credential Phase 3
   will use — decided/created when Phase 3 is authored, reviewed again on
   ASPS-745.

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

- `bash -n <script>` — syntax-checks every script; all three (`lib.sh`,
  `01-harden.sh`, `02-toolchain.sh`) pass clean, re-verified after the
  ASPS-740 security remediation round (2026-09-06).
- `shellcheck --shell=bash` (via the official `koalaman/shellcheck:stable`
  Docker image, since shellcheck isn't installed locally) — all three
  scripts pass with **zero findings** (info, style, warning, or error),
  re-verified after the remediation round.
- Idempotency was designed in and reasoned through line-by-line (every
  mutating step checks current state first: `id -u`, `dpkg -s`,
  `grep -qxF`/`grep -qE`, `ufw status`, `swapon --show`, `timedatectl show`,
  `command -v`, `write_if_changed`'s content-compare-before-write, plus the
  new `ssh_socket_activation_active`/`assert_effective_sshd_config`/
  `assert_sshd_listening` gates in `lib.sh`) rather than verified by an
  actual second run, since there is no box to run it on twice yet.
- The `sshd -t` validation-before-reload step inside `01-harden.sh` itself
  is the runtime equivalent of a "test before applying" gate — it is part
  of the script's own logic, not a substitute for it. It is now
  supplemented (not replaced) by the post-merge/post-listen assertions
  described in "sshd hardening correctness" above, since `sshd -t` alone
  was shown by security review to pass even when the effective config or
  actual listening port didn't match intent.
- No pure-bash helper was extracted that lends itself to isolated unit
  testing beyond what `bash -n`/shellcheck already cover — the new
  assertion functions in `lib.sh` (`assert_effective_sshd_config`,
  `assert_sshd_listening`, `ssh_socket_activation_active`) all depend on
  live `sshd -T`/`ss`/`systemctl` state on the target host and cannot be
  meaningfully unit-tested without a real (or containerized) sshd/systemd
  environment; they are exercised for real on first execution against the
  actual VPS, same as the rest of this directory.

**This is not a substitute for real execution.** The first real run on the
actual VPS (Phase 1/2 execution, still gated on Phase 0/ASPS-739) is the
actual verification of this code — the DoD for ASPS-740/741 is not met
until that run happens and is confirmed (see the handoff for the
continuation point).

## Files in this directory

| File | Purpose |
|---|---|
| `config.env.example` | Placeholder config — copy to `config.env` (gitignored) and fill in real values. |
| `lib.sh` | Shared logging / config-loading / idempotency helpers, sourced by both numbered scripts (DRY — not run directly). |
| `01-harden.sh` | Phase 1 baseline hardening (ASPS-740). |
| `02-toolchain.sh` | Phase 2 runtime toolchain (ASPS-741). |
| `README.md` | This file. |

Line endings: all `.sh` files in this repo are forced to LF via the
repo-root [`.gitattributes`](../../.gitattributes) (`*.sh text eol=lf`) —
no separate `deploy/vps/.gitattributes` needed. Verified with
`git check-attr text eol -- deploy/vps/*.sh` and `file deploy/vps/*.sh`
(no CRLF reported).

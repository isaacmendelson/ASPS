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
| 1 | [`01-harden.sh`](01-harden.sh) | ASPS-740 | OS updates, `aspsbot` user + SSH key, sshd hardening, UFW, fail2ban, swap, timezone/hostname, secrets directory |
| 2 | [`02-toolchain.sh`](02-toolchain.sh) | ASPS-741 | Node 20, git/ripgrep/build-essential, Claude Code CLI, .NET 8 SDK, Python 3.11, Docker |
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
| `SSH_PORT` | `2222` | Non-default port — mostly cuts mass-scan log noise, not a real security boundary (key-only auth + UFW + fail2ban are). Set to `22` in `config.env` if you'd rather keep the standard port. |
| `SWAP_SIZE_GB` | `2` | Floor for .NET/Docker builds on a 4 GB box per the task spec. Bump to 4 if the box has less RAM and D4 (agent builds ASPS locally) is in active use. |
| `TIMEZONE` | `Asia/Jerusalem` | Change if you administer from elsewhere. |

## Running

```bash
# On the VPS, as root, from the copied/cloned deploy/vps directory:
bash 01-harden.sh
# → follow the printed instructions: open a NEW terminal, confirm you can
#   SSH in as aspsbot on $SSH_PORT with your key, confirm sudo works,
#   BEFORE closing the root session that ran the script.

bash 02-toolchain.sh
# → prints version-check output for node/npm/dotnet/python3.11/docker/rg
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

## Validation performed (why this satisfies TDD rule item 9)

There is no VPS to run these scripts against yet, so conventional Red/Green
unit testing does not apply — this is declarative infrastructure
configuration executed once, non-interactively, against a real OS. Per
`CLAUDE.md`'s TDD rule item 9 ("generated code, documentation-only changes,
and purely declarative configuration may use validation or contract checks
instead of unit-level Red/Green... document why and use the strongest
automated verification available"), the following was used instead:

- `bash -n <script>` — syntax-checks every script; all three (`lib.sh`,
  `01-harden.sh`, `02-toolchain.sh`) pass clean.
- `shellcheck --shell=bash` (via the official `koalaman/shellcheck:stable`
  Docker image, since shellcheck isn't installed locally) — all three
  scripts pass with **zero findings** (info, style, warning, or error).
- Idempotency was designed in and reasoned through line-by-line (every
  mutating step checks current state first: `id -u`, `dpkg -s`,
  `grep -qxF`/`grep -qE`, `ufw status`, `swapon --show`, `timedatectl show`,
  `command -v`, `write_if_changed`'s content-compare-before-write) rather
  than verified by an actual second run, since there is no box to run it on
  twice yet.
- The `sshd -t` validation-before-reload step inside `01-harden.sh` itself
  is the runtime equivalent of a "test before applying" gate — it is part
  of the script's own logic, not a substitute for it.

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

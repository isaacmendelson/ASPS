# Telegram CEO Bot

Telegram bot that bridges messages to a Claude agent running the **official
[`@anthropic-ai/claude-agent-sdk`](https://www.npmjs.com/package/@anthropic-ai/claude-agent-sdk)**
— the same engine behind Claude Code — with full ASPS project context and
the native, cross-platform toolset (Read/Write/Edit/Bash/Grep/Glob/Task/MCP).

Runs on Linux and Windows: the SDK spawns the platform's own shell for Bash
(`/bin/bash` on Linux, PowerShell on Windows), so there is no OS-specific
tool code left in this bot.

**Security model (ASPS-743, relaxed by ASPS-768/ADR-005): read-mostly, with
sandboxed containment for the rest and human approval reserved for what the
sandbox + gating cannot bound.** See [Permission model](#permission-model--telegram-approval-flow-asps-743)
below before deploying this anywhere it can reach real credentials or a
real repo — the summary as of ASPS-768: reads run freely; routine dev work
(Bash, in-repo Edit/Write) now runs freely TOO, because it is contained
inside a bubblewrap sandbox (ASPS-765) that cannot reach secrets or the
git-push credential; what remains gated behind a Telegram Approve/Deny tap
is exactly what the sandbox can't bound — destructive Bash patterns
(hard-denied, never approvable), edits to the bot's own instructions/source
(`CLAUDE.md`, `apps/telegram-ceo/**`), anything outside the repo, and every
JIRA/GitHub write + `git push` (routed through the gated `ceo-privileged`
MCP server). All of this is backed by hard technical controls, not just
prompt instructions.

## Setup

1. Create a Telegram bot via [@BotFather](https://t.me/BotFather) and get the token
2. Get your Telegram user ID (message [@userinfobot](https://t.me/userinfobot))
3. Generate Claude auth — pick one:
   - **Preferred (subscription):** run `claude setup-token` locally and put the result in `CLAUDE_CODE_OAUTH_TOKEN`. Full toolset, no per-token billing.
   - **Fallback (API key):** set `ANTHROPIC_API_KEY`.
4. Copy `.env.example` to `.env` and fill in values. In production, keep this
   file **outside** `WORKING_DIR` (the git clone the agent operates on) —
   see the note in `.env.example`. Secret relocation on the VPS itself is
   tracked in ASPS-745; local dev may keep `.env` alongside the bot as today.
5. Install and build:

```bash
cd apps/telegram-ceo
npm install
npm run build
```

## Run

```bash
npm start
# or for development:
npm run dev
```

## Commands

| Command   | Description                    |
|-----------|--------------------------------|
| `/start`  | Welcome message                |
| `/reset`  | Clear conversation history — starts a fresh SDK session |
| `/model`  | Show current model             |
| `/reload` | Reload system prompt from disk (see note below) |

Only messages in a **private 1:1 chat** with the bot trigger the agent —
group/supergroup/channel messages are ignored even from an authorized user.

## Agent tools

The bot runs the Claude Agent SDK with the native Claude Code toolset:

- `Read` / `Grep` / `Glob` — free to use, but every call still passes through
  the path guard (see below).
- `Write` / `Edit` / `MultiEdit` / `NotebookEdit` — auto-allowed (ASPS-768)
  once the path guard passes AND the target is inside `WORKING_DIR` AND it is
  NOT a self-modification path (`CLAUDE.md` or `apps/telegram-ceo/**` — see
  [Permission model](#permission-model--telegram-approval-flow-asps-743) §6).
  A self-modification target, a path-less call, or a path outside
  `WORKING_DIR` still requires a Telegram approval like before ASPS-768.
- `Bash` — auto-allowed (ASPS-768) once it survives the
  `DANGEROUS_BASH_PATTERNS` hard-deny, PROVIDED the bubblewrap sandbox
  (ASPS-765) is enabled — see §5/§6 below. Every execution still runs inside
  that sandbox regardless of the auto-allow.
- `NotebookRead` / `Task` / `WebFetch` / most MCP tools — still gated behind
  a Telegram approval (see below); unaffected by ASPS-768.
- `AskUserQuestion` — **disabled** via `disallowedTools` (`DISALLOWED_TOOLS`
  in `src/agent.ts`): the interactive multiple-choice tool can't return a
  structured choice over the one-way Telegram approve/deny bridge (it aborted
  the agent with `AbortError: Stream closed`), so the bot instead asks any
  follow-up question as **plain Telegram text**.
- The two read-only knowledge-engine MCP tools
  (`mcp__knowledge-engine__knowledge_search` / `knowledge_ask`) — auto-allowed,
  same as Read/Grep/Glob, since they take no filesystem input and are
  genuinely read-only.
- Two more read-only MCP servers (ASPS-748), wired in bot-side (`src/agent.ts`,
  NOT the repo's `.mcp.json` — these are bot-process-scoped credentials, not
  shared with interactive Claude Code sessions), so project questions are
  answered natively instead of the agent shelling out to `curl`/`gh` via
  Bash:
  - **`github`** — GitHub's own remote MCP server, `/mcp/readonly` endpoint
    (`https://api.githubcopilot.com/mcp/readonly`), authenticated with a
    Bearer token from `GITHUB_TOKEN`. The `/readonly` endpoint only exposes
    read tools (issues/PRs/commits/code search) — there is no write tool to
    accidentally auto-allow.
  - **`mcp-atlassian`** — the community
    [`sooperset/mcp-atlassian`](https://github.com/sooperset/mcp-atlassian)
    server, run as a throwaway `docker run --rm -i` container per session
    with `READ_ONLY_MODE=true` (the server itself refuses to register any
    write/mutate tool at startup, not just a documented convention), image
    pinned by **immutable `@sha256:` digest** — **never `latest`** (supply-chain) —
    in `src/agent.ts`'s `MCP_ATLASSIAN_IMAGE` (the digest of tag `0.23.1`,
    verified on the VPS 2026-09-07). Credentials (`JIRA_URL`/`JIRA_USERNAME`/
    `JIRA_API_TOKEN`, mapped from the box's existing `JIRA_BASE_URL`/
    `JIRA_EMAIL`/`JIRA_API_TOKEN`) are passed via `docker run -e VAR`
    pass-through so they never appear in `argv`. Requires Docker on `PATH`.

  Both are auto-allowed at the SDK level via a per-server wildcard
  (`mcp__github__*`, `mcp__mcp-atlassian__*`) — a deliberate, documented
  exception to "no MCP wildcards" because both servers are read-only by
  construction (the remote endpoint's own scope / `READ_ONLY_MODE`), not by
  policy on our side; see the `AUTO_ALLOW_MCP_WILDCARDS` comment in
  `src/agent.ts` for the full reasoning and its one assumption (it holds
  only as long as neither upstream endpoint ever exposes a write tool under
  the same server key). `canUseTool`'s deny-by-default policy is otherwise
  unchanged — this bypasses it at the SDK level for these two servers only,
  same mechanism as the knowledge-engine tools above.

- **`ceo-privileged`** (ASPS-766, ADR-005 part 2) — an in-process
  `createSdkMcpServer` (`src/privileged.ts`) exposing the JIRA/GitHub
  **WRITE** operations the bot needs now that ASPS-765's bubblewrap sandbox
  denies sandboxed Bash the credential env vars it used to shell out with:
  `jira_transition`, `jira_comment`, `jira_update_issue`, `github_create_pr`,
  `github_comment`. Handlers run in the SDK host — this bot's own Node
  process, unsandboxed — so they read `JIRA_BASE_URL`/`JIRA_EMAIL`/
  `JIRA_API_TOKEN`/`GITHUB_TOKEN`/`GITHUB_REPO_URL` directly from the process
  environment and call the JIRA/GitHub REST APIs with `fetch` — no shell, no
  `curl`/`gh`, so there is no injection surface for a title/body/label value
  to reach a command line. **Unlike `github`/`mcp-atlassian` above, this
  server gets NO `allowedTools` wildcard and NO explicit entry** — every one
  of its tools falls through `canUseTool`'s classification to the
  deny-by-default branch, so every JIRA/GitHub write still requires the same
  Telegram Approve/Deny tap as `Write`/`Edit`/`Bash`.

  - **`git_push`** (ASPS-767, ADR-005 implementation-plan story ASPS-763-4) —
    on the SAME server, same gating. The SANCTIONED push path that replaces
    the now-dead ambient one: ASPS-765's sandbox denies sandboxed Bash both
    the stored git-push credential file and every credential env var, so a
    sandboxed `Bash` `git push` has no usable credential and fails; `git_push`
    runs host-side (unsandboxed) where the stored credential IS reachable via
    git's own already-configured `credential.helper = store` — this tool
    never reads or handles the credential value itself. Takes `{ remote?,
    branch }` (`remote` defaults to `"origin"`) and shells out with
    `execFile("git", ["-C", WORKING_DIR, "push", remote, branch], {shell:
    false})` — **no shell**, so `remote`/`branch` are passed as discrete argv
    elements, never interpolated into a command string (no injection
    surface). Validation, BEFORE `execFile` ever runs (`src/privileged.ts`'s
    `validateRemote`/`validateBranch`): both values are checked against
    `security.ts`'s `SHELL_METACHARACTER_PATTERN` (defense-in-depth — moot
    once there's no shell, but a cheap independent second check), a safe
    character-class allowlist (`^[A-Za-z0-9._-]+$` for `remote`,
    `^[A-Za-z0-9._/-]+$` for `branch`), and — the load-bearing check — neither
    value may start with `-` (rejects any flag-shaped value, including
    `--force`/`--force-with-lease`/`-f` in the `branch` slot, since this tool
    takes a single ref VALUE, never an argument list) or `+` (`branch`,
    git's refspec force-push prefix). **Force-push stays hard-denied**,
    matching the `DANGEROUS_BASH_PATTERNS` Bash-layer policy — there is no
    way to spell a force-push through this tool. Like every other tool on
    this server, `git_push` is NOT in `AUTO_ALLOW_MCP_WILDCARDS`/
    `AUTO_ALLOW_MCP_TOOLS`, so every call still requires a Telegram
    Approve/Deny tap showing the exact `remote`/`branch` before it runs.

`CLAUDE.md` is read from `WORKING_DIR` and injected into the system prompt
by hand (`src/context.ts`'s `loadClaudeMd`), **not** via the SDK's
`settingSources: ["project"]` option — see
[Permission model](#permission-model--telegram-approval-flow-asps-743) for
why. The agent still self-onboards via CLAUDE.md's own "at session start"
instructions (reading `PROJECT_CONTEXT.md`, the team charter, the hat
chain, etc.) using its own Read tool, exactly as an interactive Claude Code
session would. `.mcp.json` (`knowledge-engine`) is likewise read and wired
by hand (`loadMcpServers`) with `strictMcpConfig: true`, instead of relying
on settings auto-discovery.

### `/reload`

`CLAUDE.md` is re-read from disk on every Telegram turn (`buildOptions`
calls `loadClaudeMd` fresh each time) — there is no in-process cache left to
invalidate. `/reload` is kept for UX continuity but is effectively a no-op;
reload is inherent to every turn.

## Sessions / multi-turn

Each Telegram user gets an isolated, multi-turn conversation. The bot keeps
an in-memory map of Telegram user ID → Claude Agent SDK session ID
(`src/session.ts`) and passes it as `resume` on the next `query()` call for
that user, so context carries across separate Telegram messages without the
bot re-sending full history itself. `/reset` clears the map entry, so the
next message starts a brand-new SDK session.

## Permission model / Telegram approval flow (ASPS-743)

This bot went through a security review (ASPS-743) that failed an earlier
"autonomous, allow-almost-everything" design with three Blockers. The
current model is **deny-by-default with a path guard and mandatory human
approval for state-changing actions** — not a Bash regex denylist alone,
and not the project's own `.claude/settings.json` permissions.

### 1. Path guard (`src/security.ts` — `checkPathAllowed` + `findSecretPathInInput`)

Two layers, both consulted **inside `canUseTool`**, before the tool ever runs:

- **Secret-path invariant scan (`findSecretPathInInput`)** — runs first, for
  **every** tool call, regardless of tool name. It recursively scans every
  string-valued field of the tool's input — including array elements and
  nested objects (e.g. `MultiEdit`'s `edits[]`) — against
  `SECRET_PATH_PATTERNS` (`*.env`, `*.key`, `*.pem`, `*.pfx`, `id_rsa*`,
  `*.ppk`, anything named `ACCESS_KEYS*`, or any path under a
  `.ssh`/`.aws`/`.gnupg` segment). A match hard-denies the call
  unconditionally. This is the fail-closed floor: it does not depend on the
  tool being one of the ones below, or on the secret path living in that
  tool's documented path field (ASPS-743 security re-review, Major M2 — a
  per-tool field allowlist alone left write-capable `MultiEdit` and
  `NotebookRead` unguarded, and any future SDK tool would have inherited the
  same blind spot).
- **Per-tool confinement check (`checkPathAllowed`)** — for tools with a
  documented path field (`Read`, `Edit`, `Write`, `MultiEdit`,
  `NotebookEdit`, `NotebookRead`; `Grep`/`Glob` where a `path` is given):
  the path is resolved to a real, symlink-free absolute path (`..` and
  symlinked ancestors included) and rejected if it falls outside
  `WORKING_DIR`. (It also re-checks the secret pattern on that one field,
  redundant with the scan above but kept for a precise "outside working
  dir" vs. "secret pattern" error message.)

This mirrors the spirit of the old (deleted) hand-rolled `tools.ts`
`safePath()` helper, but as a guard consulted from `canUseTool`, applying to
the SDK's native tools too.

### 2. Bash destructive-command denylist (`DANGEROUS_BASH_PATTERNS`)

`rm -rf`, `git push --force`, `git reset --hard`, `DROP TABLE`/`DATABASE`,
`format`, etc. are hard-denied inside `canUseTool` — **even if the human
would have approved them**. This is defense-in-depth, not the primary
control: a regex denylist cannot safely gate an arbitrary shell, so it only
ever adds a floor under the approval flow below, never a substitute for it.
Every other Bash command (not just the ones on this list) requires Telegram
approval like any other state-changing action.

### 3. Deny-by-default + Telegram approval (`src/agent.ts`, `src/approvals.ts`, `src/bot.ts`)

`canUseTool` classifies every tool call (updated by ASPS-768 — see §6 below
for the full rationale and ordering):

- **Auto-allow** (subject to the path guard): `Read`, `Grep`, `Glob`, the
  two read-only knowledge-engine MCP tools, and the read-only `github` /
  `mcp-atlassian` MCP servers (ASPS-748 — see [Agent tools](#agent-tools)).
- **In-repo write auto-allow (ASPS-768)**: `Edit`/`Write`/`MultiEdit`/
  `NotebookEdit` auto-allow once the path guard passes, UNLESS the target is
  a self-modification path (`CLAUDE.md` / `apps/telegram-ceo/**` — see §6).
- **Hard-deny**: Bash matching `DANGEROUS_BASH_PATTERNS` — evaluated before
  any Bash auto-allow, so a destructive pattern is never reachable through
  either of the two branches below.
- **Sandboxed Bash auto-allow (ASPS-768)**: any other `Bash` call
  auto-allows, provided the bwrap sandbox (ASPS-765) is enabled — see §6.
- **Read-only git auto-allow (ASPS-749) — now a fallback**: reached only
  when the sandbox is NOT enabled: a `Bash` call whose command passes
  `isSafeReadOnlyGitCommand` (`src/security.ts`) still auto-allows even in
  that degraded mode — see
  [Read-only git auto-allow](#4-read-only-git-auto-allow-asps-749) below for
  the full rule set.
- **Everything else** (a self-modification `Edit`/`Write`/`MultiEdit`/
  `NotebookEdit`, a path-less write call, non-sandboxed non-read-only-git
  `Bash`, `Task`, `WebFetch`, every `ceo-privileged` JIRA/GitHub write tool
  (ASPS-766/767 — deliberately NEVER auto-allowed), any other MCP tool,
  etc.): `canUseTool` calls `requestApproval()`, which sends the authorized
  user an
  inline-keyboard Telegram message (✅ Approve / ❌ Deny) with the **full,
  untruncated** command or path (ASPS-743 security re-review, Major M1 — a
  300-char truncation could previously hide a malicious tail behind a
  padded-out benign prefix), sent as **plain text** with no `parse_mode`
  (so nothing in the untrusted content can be parsed as Markdown and alter
  the message's structure — the previous raw ``` code fence could be broken
  out of by balanced backticks in the command). A summary longer than
  Telegram's ~4096-char limit is **split across multiple messages**
  (`[part i/N]`), never truncated; the Approve/Deny keyboard is attached
  only to the final part. This applies identically to a tool call made from
  *inside* a `Task` subagent — `canUseTool` does not special-case the SDK's
  `agentID` option, so a subagent's own tool calls get the same approval
  prompt, not a bypass. It blocks the tool call until:
  - the **same user who owns the turn** taps a button (`resolveApproval`
    checks the tapping user's id against the requesting user's id —
    approvals are never global or cross-user),
  - or `APPROVAL_TIMEOUT_MS` (default 600000ms / 10 min — ASPS-752) elapses,
    which resolves to **deny** so the agent never hangs waiting on a phone
    notification. A tap that lands after the request has already expired
    (or was already answered) gets a visible "expired or already handled"
    toast instead of a silent no-op.

`canUseTool` never resolves to `null` — the SDK's own docs note an
accidental `null` leaves the permission request unanswered and the tool
call blocked indefinitely; every branch above resolves to an explicit
`allow` or `deny`.

### 4. Read-only git auto-allow (ASPS-749)

`Bash` calls that are a single, standalone, strictly read-only `git`
invocation auto-allow without a Telegram approval, to cut approval friction
for routine git plumbing — implemented by `isSafeReadOnlyGitCommand`
(`src/security.ts`), evaluated in `canUseTool` AFTER the destructive-pattern
hard-deny above, so a destructive command is never reachable through this
path. Any doubt resolves to `false` (stays approval-gated) — this is a
narrow carve-out under `Bash`, not a general shell allowlist.

**Design: a per-subcommand positive allowlist, not a flag denylist.** An
earlier version of this function was a subcommand-allowlist plus a
flag-DENYlist, which security review found exploitable with PoC-confirmed
bypasses (`git diff --no-index <file> /dev/null` and `git blame --contents
<file> -- <tracked>` for arbitrary file read; `git config --list --file
<file>` for arbitrary file read; `git ls-remote <url>` / `git remote show
<url>` for network I/O/SSRF; `git ls-remote ext::<cmd>` for conditional RCE;
`git -C <dir>` for out-of-repo access; attached short-flag forms like
`-O<path>` evading exact-match flag denies; unrejected glob/tilde
expansion). A denylist can never enumerate every risky git flag, so the
function was redesigned around a positive, per-subcommand safe-flag
allowlist instead — see the block comment above `isSafeReadOnlyGitCommand`
in `src/security.ts` for the full rationale.

Returns `true` ONLY if ALL of the following hold:

1. **No shell metacharacters anywhere**: `;` `&` `|` `` ` `` `$` `(` `)` `{`
   `}` `<` `>` `\` or any control character (including newline/carriage
   return) — this forbids chaining, redirection, command substitution,
   subshells/backgrounding, and multi-line payloads, so it must be a single
   standalone command.
2. **No glob/tilde/transport tokens**: `*`, `?`, `[`, `]` anywhere, a
   leading `~` on any token (home-dir expansion), `::` anywhere (git
   remote-helper transport syntax, e.g. `ext::sh`), or a URL/transport
   scheme (`https://`, `ssh:`, `file:`, `ext:`, `git:`) — all are
   shell-expanded or network/exec vectors.
3. **Begins with exactly `git `** (case-sensitive exact prefix, no `-C`
   support at all — the bot always runs in its own `cwd`, so no command can
   operate outside the working directory).
4. **Subcommand on a strict, minimal read-only allowlist**: `status`,
   `log`, `show`, `diff`, `branch`, `remote`, `rev-parse`, `describe`,
   `ls-files`, `tag`. Each is further restricted to an explicit
   per-subcommand safe-flag/safe-arg allowlist (not a denylist) — anything
   not on that subcommand's own list is rejected by omission:
   - `status` → `-s`/`--short`/`-b`/`--branch`/`--porcelain` only, no path
     arguments.
   - `log` → `--oneline`/`--stat`/`--graph`/`--decorate`, an optional
     bounded count (`-n <N>`/`--max-count=<N>`/`-<N>`), and at most one
     trailing safe ref token. No `-L`, `-O`, `--output`, `--format`,
     `--pretty`, `-G`/`-S`.
   - `diff` → `--stat`/`--cached`/`--staged`/`--name-only` and at most one
     trailing safe ref token — **no path arguments of any kind** (closes
     the `--no-index` PoC bypass; also rejects `--no-index` explicitly).
   - `show` → `--stat`/`--name-only` and at most one trailing safe ref
     token (no `<ref>:<path>` form — a ref token cannot contain `:`).
   - `branch` → bare/`-a`/`-v`/`-l`/`--list` only — **no name argument**
     (never create/delete/move/force).
   - `tag` → bare/`-l`/`--list`/`-n` only — **no name argument** (never
     create/delete/force/sign).
   - `remote` → bare, `-v`, or `get-url <name>` only (`<name>` must be a
     safe ref-pattern token, never a URL) — **`remote show` is dropped
     entirely** (network I/O).
   - `rev-parse` → `HEAD`, `--abbrev-ref`/`--short` with a ref, or
     `--verify <ref>` only.
   - `describe` → `--tags`/`--always` and at most one trailing safe ref
     token only.
   - `ls-files` → bare/`--cached`/`--others`/`--modified` only, no path
     arguments.

   **Dropped entirely** (every read form of these either reads an
   arbitrary file or does network I/O, and there is no safe subset worth
   carving out): `config` (`--file`/`-f` read arbitrary files), `blame`
   (`--contents`/`-L` read arbitrary files), `ls-remote` (network I/O,
   `ext::` is conditional RCE), `shortlog` (reads stdin).
5. **No denied flag anywhere** in the token stream (independent second
   layer, redundant with #4 by design): config-override (`-c`, `--config`),
   output/pager (`-o`, `--output`, `-O*`, `--pager`,
   `--open-files-in-pager`), external-diff (`--ext-diff`), transport/exec
   program overrides (`--upload-pack`, `--receive-pack`, `--exec`,
   `--exec-path`), file-reading (`--no-index`, `--file`, `-f*`,
   `--contents`), and interactive flags (`-i`, `--interactive`).

`READ_ONLY_GIT_SUBCOMMANDS`, the per-subcommand `GIT_*_SAFE_FLAGS` sets, and
`DENIED_GIT_FLAGS` (`src/security.ts`) are the single source of truth for
the allowlist. The system prompt (`context.ts`) tells the agent routine
read-only git runs without a prompt, but any git write
(commit/push/checkout/merge/rebase/reset, ...) still needs approval like
any other state-changing action.

### 5. Bubblewrap sandbox containment for every Bash execution (ASPS-765 / ADR-005 part 1)

`buildOptions()` (`src/agent.ts`) passes the SDK's built-in `sandbox` config
(bubblewrap on Linux) so **every** Bash execution runs inside a per-exec
namespace jail, not just the approved-vs-denied decision `canUseTool` already
makes. This is containment, not a relaxation: **Bash itself is still gated
behind `canUseTool`/Telegram approval exactly as in section 3 above** —
`autoAllowBashIfSandboxed` is deliberately left unset. Auto-allowing sandboxed
Bash is a later, separate story (ASPS-763-5 / ASPS-768).

- `enabled: true`, `failIfUnavailable: true` — if `bwrap` is ever missing or
  broken on the box, the query fails loudly instead of silently falling back
  to running Bash unsandboxed (the SDK's own default would otherwise degrade
  gracefully — see the `sandbox` doc comment in `sdk.d.ts`).
- `bwrapPath` — defaults to `/usr/bin/bwrap` (where `deploy/vps/06-sandbox.sh`
  installs it, ASPS-764), overridable via `BWRAP_PATH`.
- `filesystem.denyRead` — the secrets dir (`SECRETS_DIR`, default
  `/home/aspsbot/secrets`) is denied wholesale, not just the individual
  credential files, so a future file added under it is covered without a code
  change. **ASPS-766 fold-in** (ASPS-765 security review, Minor): also denies
  `<HOME>/.claude` (can hold `.credentials.json`, the CLI's own OAuth token
  cache) and `<HOME>/.npmrc` (can hold an npm registry auth token) — neither
  lives under `SECRETS_DIR`, so this closes the remaining home-dir
  credential-read channel a sandboxed Bash command could otherwise still read
  straight off disk. `HOME` is read from the environment, `os.homedir()` as
  fallback. `filesystem.allowWrite` is scoped to `WORKING_DIR` only.
- `credentials.files` — denies `<SECRETS_DIR>/github-credentials`, the stored
  git-push credential (ASPS-745 Phase 3), so a sandboxed `Bash` `git push` has
  no usable credential and fails — confirming the ambient push path is dead
  (ASPS-767). The sanctioned replacement is the `git_push` tool on the
  `ceo-privileged` server (see the "Agent tools" section above), which runs
  host-side, unsandboxed, where the same stored credential IS reachable.
- `credentials.envVars` — denies (`mode: "deny"`) every secret/token env var
  this process holds: `TELEGRAM_BOT_TOKEN`, `CLAUDE_CODE_OAUTH_TOKEN`,
  `ANTHROPIC_API_KEY`, `GITHUB_TOKEN`, `JIRA_EMAIL`, `JIRA_API_TOKEN`. This is
  the load-bearing half of the fix: systemd's `EnvironmentFile=` injects these
  as ordinary process env vars (PID 1, before any sandbox applies — see
  `docs/cloud/VPS_TELEGRAM_HARDENING.md` §5), and bubblewrap inherits the
  parent environment by default, so `sandbox.enabled: true` alone would do
  nothing to stop a sandboxed Bash command from reading them straight out of
  `process.env`/`environ`.
- `credentials.envVars`/`credentials.files` with `mode: "deny"` only affect
  commands executed *inside* the per-exec sandbox — the bot's own Node
  process (the SDK host, where `query()` runs) is never sandboxed and keeps
  every one of these env vars, so denying `CLAUDE_CODE_OAUTH_TOKEN` here does
  **not** break the SDK's own Anthropic auth.

Requires ASPS-764 (bubblewrap installed + a scoped AppArmor profile + the
systemd unit's `RestrictNamespaces=` relaxed to `user pid mnt`) already
applied on the box — see `docs/cloud/VPS_TELEGRAM_HARDENING.md` and
`docs/architecture/decisions/ADR-005-ASPS-763-AGENT-TOOL-EXECUTION-PRIVILEGE-SEPARATION.md`.

### 6. Sandboxed Bash + in-repo write auto-allow (ASPS-768, ADR-005 story ASPS-763-5 — CLOSES ASPS-762)

This is the relaxation ASPS-762 originally asked for ("stop flooding me with
approval taps for routine dev work"), delivered only once §5's sandbox +
the ASPS-766/767 gated write path made it safe. **The thesis:** every Bash
execution is contained in the bwrap sandbox (secrets dir, git-push
credential, and every token env var all denied — §5), and every JIRA/GitHub
write + `git push` is routed off Bash onto the gated `ceo-privileged` MCP
server (still per-call Telegram-approved, unaffected by this story). So the
blast radius of an auto-allowed Bash command or in-repo file edit is bounded
to "a recoverable repo clone + public source" — it cannot read a secret,
cannot complete an unapproved push (no credential), and cannot rewrite its
own operating instructions or its own permission logic either (see the
self-modification exclusion below). Two things changed in `canUseTool`
(`src/agent.ts`):

- **Bash auto-allows** once it survives the `DANGEROUS_BASH_PATTERNS`
  hard-deny (§2) — no per-command allowlist is needed anymore. This
  supersedes the narrower ASPS-749 read-only-git carve-out for the normal
  (sandboxed) case: a git WRITE (`commit`/`push`/`checkout`/...) now also
  auto-allows here. An ambient `Bash git push` still cannot succeed even
  though it no longer prompts — §5's `credentials.files` denies it the
  stored credential, so it fails at execution time, not at the approval
  gate.
  - **Coupling (load-bearing):** this auto-allow is gated on the sandbox
    actually being enabled (`createCanUseTool`'s third parameter,
    `sandboxEnabled`, threaded from `buildOptions`'s
    `buildSandboxSettings(...).enabled`). If the sandbox is ever off, Bash
    falls back to the pre-768 gated behavior (plus the ASPS-749 read-only-git
    carve-out as a narrower auto-allow within that degraded mode) instead of
    silently running an unsandboxed command with full access to secrets and
    the ambient git credential. The SDK's own `autoAllowBashIfSandboxed`
    flag is deliberately left unset (as in §5) — `canUseTool` implements the
    auto-allow itself precisely so this coupling and the self-modification
    exclusion below can be expressed; the SDK flag would bypass `canUseTool`
    entirely and could express neither.
- **In-repo `Edit`/`Write`/`MultiEdit`/`NotebookEdit` auto-allows** once the
  path guard (§1) passes — inside `WORKING_DIR`, no secret pattern — UNLESS
  the resolved target is a **self-modification path**:
  `matchSelfModificationPath` (`src/security.ts`) matches `CLAUDE.md` (the
  system-prompt content re-read fresh on every turn, so an edit here changes
  the agent's own operating instructions on its VERY NEXT turn with no
  restart) or anything under `apps/telegram-ceo/**` (this bot's own source —
  `canUseTool`, the sandbox config, the approval flow, this guard itself).
  An injected agent that could freely rewrite either could disable or weaken
  its own permission model with no Telegram approval — the textbook
  self-modification/privilege-escalation path. Both stay excluded from the
  auto-allow and fall through to the same Telegram approval any other write
  already required before this story. A call with no resolvable path (e.g.
  an empty/malformed input) also falls through to approval.
  - **Two independent layers, not one:** the guard above only sees
    `Edit`/`Write`/`MultiEdit`/`NotebookEdit` *tool* calls — it has no
    visibility into a sandboxed `Bash` command, which this same story also
    auto-allows. Without a second control, `Bash echo x >> CLAUDE.md` could
    still self-modify with no approval. `buildSandboxSettings`
    (`src/agent.ts`) closes that at the OS/mount level: `filesystem.denyWrite`
    lists the same two roots (`CLAUDE.md`, `apps/telegram-ceo/`) so a
    sandboxed command cannot write them regardless of which tool the write
    comes through, even though they are inside `filesystem.allowWrite`'s
    `WORKING_DIR`.

What this does NOT change: destructive Bash (§2), the secret-path scan (§1),
the path guard's outside-`WORKING_DIR` check (§1), and every
`ceo-privileged` JIRA/GitHub write + `git_push` tool (ASPS-766/767) —
deliberately NEVER added to the auto-allow wildcards, so every call still
falls through to Telegram approval exactly as before this story.

**ASPS-762 (PR #51, the original "cancel the approvals" attempt) is
superseded by this story** — its per-command Bash allowlist approach is
replaced by sandbox-contained auto-allow, which does not need to enumerate
safe commands at all. ASPS-762 should be closed once this merges.

### SDK permission precedence — why `settingSources` is `[]`, not `["project"]`

The bot's earlier design passed `settingSources: ["project"]` so the SDK
would auto-load `CLAUDE.md` and `.mcp.json` from `WORKING_DIR`. That option
**also** auto-loads `.claude/settings.json` (and `.claude/settings.local.json`
if `"local"` were added), which in this repo pre-authorizes `Bash(*)`,
`Write`, `Edit`, `Read`, etc. via `permissions.allow`.

Confirmed empirically with the SDK's own `resolveSettings()` API (no CLI
spawn needed): with `settingSources: ["project"]` and `cwd` set to this
repo, `resolveSettings().effective.permissions.allow` includes `Bash(*)`,
`Write`, `Edit`, etc., sourced straight from `.claude/settings.json`. The
SDK's own docs describe `Options.allowedTools` the same way our code already
(intentionally) uses it for the two knowledge-engine MCP tools: entries
there "execute automatically without asking the user for approval" —
`permissions.allow` is the settings-file equivalent of that same mechanism,
and it is honored by the SDK's permission engine **before** `canUseTool` is
ever consulted. In other words: with `settingSources: ["project"]`, a
matching `allow` rule would make `Write`/`Edit`/`Bash(*)` run **without**
`canUseTool` being called at all — silently re-opening every tool this
remediation locks down, regardless of what `canUseTool` itself decides.

The fix: `buildOptions()` passes `settingSources: []` (SDK "isolation
mode" — confirmed via `resolveSettings({ settingSources: [] })` returning
`effective: {}`, zero rules from any source). `CLAUDE.md` and `.mcp.json`
are instead read by hand (`loadClaudeMd` / `loadMcpServers` in
`context.ts`) and passed explicitly (`systemPrompt.append`,
`Options.mcpServers` + `strictMcpConfig: true`), so project context and MCP
still work — but `canUseTool` is now the **sole, unconditional authority**
for every tool call. No settings file, local or otherwise, can silently
re-open a tool.

## Security

- Only responds to Telegram user IDs listed in `AUTHORIZED_USERS` — enforced on every inbound update: regular messages, edited messages, and callback queries. Unauthorized senders are dropped **silently** (no "Unauthorized" reply) to avoid letting anyone enumerate which user ids are authorized by probing for a distinct response.
- The agent only triggers on messages in a **private** Telegram chat (`msg.chat.type === "private"`); group/supergroup/channel messages never reach the agent, even from an authorized user.
- File access is confined to `WORKING_DIR` — not merely by the SDK's default `cwd` scoping — and a secret-pattern path (`*.env`, `ACCESS_KEYS*`, `id_rsa*`, `.ssh/`, ...) is **always** hard-denied for **every** tool call, regardless of tool name or which input field carries it: `findSecretPathInInput` (`security.ts`) recursively scans **every string-valued field** of every tool's input — including array/nested fields like `MultiEdit`'s `edits[]` — before any other check runs, not just the single documented path field of tools in the `PATH_INPUT_FIELD` allowlist. See [Path guard](#1-path-guard-srcsecurityts--checkpathallowed) below.
- **Routine dev work no longer requires a Telegram approval (ASPS-768)** — non-destructive Bash and in-repo `Edit`/`Write`/`MultiEdit`/`NotebookEdit` auto-allow, because they are contained: every Bash execution runs inside the bwrap sandbox (§5, secrets/credential/token env vars all denied), and JIRA/GitHub writes + `git push` are routed off Bash onto the gated `ceo-privileged` MCP server. What still requires an explicit Telegram approval from the authorized user: destructive Bash patterns (hard-denied, never approvable even with a tap), edits to the bot's own operating instructions or source (`CLAUDE.md`, `apps/telegram-ceo/**` — both ALSO denied write access at the sandbox/mount level, not just at the tool-call level, so a sandboxed `Bash` write can't reach them either), anything outside `WORKING_DIR`, `Task`/`WebFetch`, and every JIRA/GitHub write + `git push`. The approval prompt (when one is shown) still shows the **full, untruncated** command/path — never a truncated summary that could hide a malicious tail — sent as **plain text** (no Markdown parsing) so the untrusted content cannot alter the message's structure; the Bash denylist is defense-in-depth on top of that, not a replacement for it.
- Tool calls made from *inside* a subagent spawned by `Task` re-enter the same `canUseTool` policy as the main thread (confirmed against the SDK's own `CanUseTool` type, which documents an `agentID` option field for exactly this case) — a single Task approval cannot unleash an unguarded agent.
- `canUseTool` is the sole permission authority: `settingSources: []` means no `.claude/settings.json` allow-rule can bypass it (see the precedence section above).
- On an agent error, the Telegram reply is a **generic** message; the real error (which can include stack traces or paths) is logged server-side only, never sent to the chat.
- Startup logs the **count** of authorized users, never the raw id list.
- Auth token (`CLAUDE_CODE_OAUTH_TOKEN`) or API key is read from the environment only — never hardcoded, never logged.
- No real secrets committed — `.env` is gitignored. In production, `.env` should live outside `WORKING_DIR` (see `.env.example`); actual relocation on the VPS is tracked separately (ASPS-745).

### Known box-level items (tracked in ASPS-745, not solved by this bot's code)

The path guard and approval model above do not depend on any of these being
done — they are independent, deployment/infrastructure-level hardening
tracked separately: secret relocation on the VPS filesystem, network-egress
isolation, `main` branch protection on GitHub, and a least-privilege scoped
GitHub token for the bot's own git/`gh` operations.

## Tests

```bash
npm test
```

Uses `vitest`. The Claude Agent SDK's `query()`, `node-telegram-bot-api`,
and (where noted) `../agent.js`/`../context.js`/`../approvals.js` are
mocked per test file — the suite makes no real network or API calls and
touches only temp directories on disk for the path-guard tests.

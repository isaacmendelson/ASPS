# Telegram CEO Bot

Telegram bot that bridges messages to a Claude agent running the **official
[`@anthropic-ai/claude-agent-sdk`](https://www.npmjs.com/package/@anthropic-ai/claude-agent-sdk)**
— the same engine behind Claude Code — with full ASPS project context and
the native, cross-platform toolset (Read/Write/Edit/Bash/Grep/Glob/Task/MCP).

Runs on Linux and Windows: the SDK spawns the platform's own shell for Bash
(`/bin/bash` on Linux, PowerShell on Windows), so there is no OS-specific
tool code left in this bot.

**Security model (ASPS-743): read-mostly, with human approval for every
state-changing action.** See [Permission model](#permission-model--telegram-approval-flow-asps-743)
below before deploying this anywhere it can reach real credentials or a
real repo — the summary is: reads run freely, writes/Bash/etc. do not run
until you tap Approve in Telegram, and both are backed by hard technical
controls, not just prompt instructions.

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
- `Write` / `Edit` / `MultiEdit` / `NotebookEdit` / `NotebookRead` / `Bash` /
  `Task` / `WebFetch` / most MCP tools — gated behind a Telegram approval
  (see below).
- The two read-only knowledge-engine MCP tools
  (`mcp__knowledge-engine__knowledge_search` / `knowledge_ask`) — auto-allowed,
  same as Read/Grep/Glob, since they take no filesystem input and are
  genuinely read-only.

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

`canUseTool` classifies every tool call:

- **Auto-allow** (subject to the path guard): `Read`, `Grep`, `Glob`, and
  the two read-only knowledge-engine MCP tools.
- **Hard-deny**: Bash matching `DANGEROUS_BASH_PATTERNS`.
- **Everything else** (`Write`, `Edit`, `MultiEdit`, `NotebookEdit`,
  non-dangerous `Bash`, `Task`, `WebFetch`, any other MCP tool, etc.):
  `canUseTool` calls `requestApproval()`, which sends the authorized user an
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
  - or `APPROVAL_TIMEOUT_MS` (default 60s) elapses, which resolves to
    **deny** so the agent never hangs waiting on a phone notification.

`canUseTool` never resolves to `null` — the SDK's own docs note an
accidental `null` leaves the permission request unanswered and the tool
call blocked indefinitely; every branch above resolves to an explicit
`allow` or `deny`.

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
- **Every state-changing tool call requires an explicit Telegram approval from the authorized user** (see above), and the approval prompt shows the **full, untruncated** command/path — never a truncated summary that could hide a malicious tail — sent as **plain text** (no Markdown parsing) so the untrusted content cannot alter the message's structure; the Bash denylist is defense-in-depth on top of that, not a replacement for it.
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

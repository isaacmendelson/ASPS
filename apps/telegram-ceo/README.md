# Telegram CEO Bot

Telegram bot that bridges messages to a Claude agent running the **official
[`@anthropic-ai/claude-agent-sdk`](https://www.npmjs.com/package/@anthropic-ai/claude-agent-sdk)**
— the same engine behind Claude Code — with full ASPS project context and
the complete native, cross-platform toolset (Read/Write/Edit/Bash/Grep/Glob/
Task/MCP), not a hand-rolled subset.

Runs on Linux and Windows: the SDK spawns the platform's own shell for Bash
(`/bin/bash` on Linux, PowerShell on Windows), so there is no OS-specific
tool code left in this bot.

## Setup

1. Create a Telegram bot via [@BotFather](https://t.me/BotFather) and get the token
2. Get your Telegram user ID (message [@userinfobot](https://t.me/userinfobot))
3. Generate Claude auth — pick one:
   - **Preferred (subscription):** run `claude setup-token` locally and put the result in `CLAUDE_CODE_OAUTH_TOKEN`. Full toolset, no per-token billing.
   - **Fallback (API key):** set `ANTHROPIC_API_KEY`.
4. Copy `.env.example` to `.env` and fill in values
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

## Agent tools

The bot no longer defines its own tools. It runs the Claude Agent SDK with
the default Claude Code toolset, so the agent gets:

- `Read` / `Write` / `Edit` / `Grep` / `Glob` — file operations, scoped to `cwd` (`WORKING_DIR`)
- `Bash` — runs in the host's own shell (`/bin/bash` on Linux, PowerShell on Windows)
- `Task` — subagent delegation
- MCP tools — `.mcp.json` (`knowledge-engine`) is auto-loaded via `settingSources: ["project"]`; its tools are allow-listed (`mcp__knowledge-engine__*`) so calls don't stall waiting for a permission prompt nobody is present to answer

`CLAUDE.md` and the rest of the project's hat-based instructions are loaded
the same way an interactive Claude Code session loads them
(`settingSources: ["project"]`) — the agent follows CLAUDE.md's own
"at session start" reading order itself. `src/context.ts` only adds a short
Telegram-specific addendum (message-length limit, Markdown flavor, no
interactive terminal on the other end).

### `/reload`

The SDK spawns a fresh Claude Code process per Telegram turn and reads
`CLAUDE.md` / project settings from disk every time — there is no
in-process system-prompt cache left to invalidate. `/reload` is kept for
UX continuity but is effectively a no-op; reload is inherent to every turn.

## Sessions / multi-turn

Each Telegram user gets an isolated, multi-turn conversation. The bot keeps
an in-memory map of Telegram user ID → Claude Agent SDK session ID
(`src/session.ts`) and passes it as `resume` on the next `query()` call for
that user, so context carries across separate Telegram messages without the
bot re-sending full history itself. `/reset` clears the map entry, so the
next message starts a brand-new SDK session.

## Permission model / destructive-command guard

`src/security.ts` is the single source of truth for the destructive-command
denylist (`rm -rf`, `git push --force`, `git reset --hard`, `DROP TABLE`/
`DATABASE`, `format`, etc.). `src/agent.ts` wires it into the SDK's
`canUseTool` permission hook: any `Bash` call whose command matches the
denylist is denied before it runs; every other tool call (including MCP)
is auto-allowed, since there is no human present on the Telegram side to
answer an interactive permission prompt (`canUseTool` must never resolve to
`null` — the SDK docs note that would leave the tool call blocked forever).

## Security

- Only responds to Telegram user IDs listed in `AUTHORIZED_USERS` — enforced on every inbound update: regular messages, edited messages, and callback queries
- File access scoped to `WORKING_DIR` (passed as the SDK's `cwd`)
- Destructive Bash commands are blocked by the `canUseTool` permission hook (see above), independent of any Telegram-side confirmation
- Auth token (`CLAUDE_CODE_OAUTH_TOKEN`) or API key is read from the environment only — never hardcoded, never logged
- No real secrets committed — `.env` is gitignored

## Tests

```bash
npm test
```

Uses `vitest`. The Claude Agent SDK's `query()` and `node-telegram-bot-api`
are mocked — the suite makes no real network or API calls.

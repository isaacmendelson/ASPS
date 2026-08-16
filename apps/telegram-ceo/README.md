# Telegram CEO Bot

Telegram bot that bridges messages to a Claude AI agent with full ASPS project context and tool access (file read/write, bash, grep, glob).

## Setup

1. Create a Telegram bot via [@BotFather](https://t.me/BotFather) and get the token
2. Get your Telegram user ID (message [@userinfobot](https://t.me/userinfobot))
3. Copy `.env.example` to `.env` and fill in values
4. Install and build:

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
| `/reset`  | Clear conversation history     |
| `/model`  | Show current model             |
| `/reload` | Reload system prompt from disk |

## Agent Tools

The bot gives Claude access to:
- `read_file` / `write_file` / `edit_file` — file operations within WORKING_DIR
- `bash` — PowerShell commands (dangerous patterns blocked)
- `grep` — search file contents
- `glob` — find files by pattern
- `list_directory` — list directory contents

## Security

- Only responds to Telegram user IDs listed in `AUTHORIZED_USERS`
- File access restricted to `WORKING_DIR`
- Destructive bash commands (rm -rf, DROP TABLE, etc.) are blocked
- No real secrets committed — `.env` is gitignored

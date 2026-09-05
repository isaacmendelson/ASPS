import { config } from "dotenv";
import { resolve } from "node:path";
import { startBot } from "./bot.js";

// Load .env from the telegram-ceo directory
config({ path: resolve(import.meta.dirname, "..", ".env") });

// Validate required env vars
const required = ["TELEGRAM_BOT_TOKEN", "AUTHORIZED_USERS"];
for (const key of required) {
  if (!process.env[key]) {
    console.error(`Missing required environment variable: ${key}`);
    process.exit(1);
  }
}

// Auth: the Claude Agent SDK accepts either a subscription OAuth token
// (preferred — `claude setup-token`) or a raw API key. At least one is required.
if (!process.env.CLAUDE_CODE_OAUTH_TOKEN && !process.env.ANTHROPIC_API_KEY) {
  console.error(
    "Missing auth: set CLAUDE_CODE_OAUTH_TOKEN (subscription, via `claude setup-token`) or ANTHROPIC_API_KEY.",
  );
  process.exit(1);
}

console.log(`Working directory: ${process.env.WORKING_DIR || process.cwd()}`);
console.log(`Model: ${process.env.MODEL || "(Claude Code CLI default)"}`);
console.log(
  `Auth: ${process.env.CLAUDE_CODE_OAUTH_TOKEN ? "CLAUDE_CODE_OAUTH_TOKEN (subscription)" : "ANTHROPIC_API_KEY"}`,
);

startBot();

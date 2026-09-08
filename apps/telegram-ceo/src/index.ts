// DEPLOY INVARIANT (ASPS-762 security gate) — the bot MUST run the built
// artifact with NO watch / hot-reload: start it via `npm start`
// (`node dist/index.js`), never `npm run dev`/`tsx --watch`/`nodemon`. The
// approval guard lives in the running code (src/security.ts, src/agent.ts);
// editing those files while the process is live is deliberately GATED (the
// self-modification guard in security.ts), but that protection is only
// meaningful if a source edit does NOT hot-swap into the live process. A
// watch/reload runner would let an approved-looking edit to security.ts (or a
// rebuild of dist/) take effect mid-session and weaken the live guard, so the
// production process must be a fixed, pre-built artifact restarted only by an
// out-of-band operator action.
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

import { config } from "dotenv";
import { resolve } from "node:path";
import { startBot } from "./bot.js";

// Load .env from the telegram-ceo directory
config({ path: resolve(import.meta.dirname, "..", ".env") });

// Validate required env vars
const required = ["TELEGRAM_BOT_TOKEN", "ANTHROPIC_API_KEY", "AUTHORIZED_USERS"];
for (const key of required) {
  if (!process.env[key]) {
    console.error(`Missing required environment variable: ${key}`);
    process.exit(1);
  }
}

console.log(`Working directory: ${process.env.WORKING_DIR || process.cwd()}`);
console.log(`Model: ${process.env.MODEL || "claude-sonnet-4-20250514"}`);

startBot();

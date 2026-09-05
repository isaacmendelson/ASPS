import TelegramBot from "node-telegram-bot-api";
import { runAgent, reloadSystemPrompt } from "./agent.js";
import { clearSession } from "./session.js";

let bot: TelegramBot;
let authorizedUsers: Set<number>;

/** Split a long message into chunks that fit Telegram's 4096 char limit. */
function splitMessage(text: string, maxLen = 4096): string[] {
  if (text.length <= maxLen) return [text];

  const chunks: string[] = [];
  let remaining = text;

  while (remaining.length > maxLen) {
    // Try to split on paragraph boundary
    let splitIdx = remaining.lastIndexOf("\n\n", maxLen);
    if (splitIdx < maxLen * 0.3) {
      // Paragraph boundary too early — try line boundary
      splitIdx = remaining.lastIndexOf("\n", maxLen);
    }
    if (splitIdx < maxLen * 0.3) {
      // Line boundary too early — hard split
      splitIdx = maxLen;
    }

    chunks.push(remaining.slice(0, splitIdx));
    remaining = remaining.slice(splitIdx).trimStart();
  }

  if (remaining) chunks.push(remaining);
  return chunks;
}

/** Check if a user is authorized. */
function isAuthorized(userId: number): boolean {
  return authorizedUsers.has(userId);
}

/** Send typing action periodically until cancelled. */
function startTypingIndicator(chatId: number): () => void {
  let active = true;

  const send = () => {
    if (!active) return;
    bot.sendChatAction(chatId, "typing").catch(() => {});
  };

  send(); // Send immediately
  const interval = setInterval(send, 4000); // Then every 4 seconds

  return () => {
    active = false;
    clearInterval(interval);
  };
}

/** Initialize and start the Telegram bot. */
export function startBot(): void {
  const token = process.env.TELEGRAM_BOT_TOKEN;
  if (!token) {
    console.error("TELEGRAM_BOT_TOKEN is required");
    process.exit(1);
  }

  // Parse authorized users
  const usersEnv = process.env.AUTHORIZED_USERS || "";
  authorizedUsers = new Set(
    usersEnv
      .split(",")
      .map((s) => s.trim())
      .filter(Boolean)
      .map(Number)
      .filter((n) => !isNaN(n)),
  );

  if (authorizedUsers.size === 0) {
    console.error("AUTHORIZED_USERS must contain at least one user ID");
    process.exit(1);
  }

  bot = new TelegramBot(token, { polling: true });

  console.log("Telegram CEO Bot started (polling mode)");
  console.log(`Authorized users: ${[...authorizedUsers].join(", ")}`);

  // /start command
  bot.onText(/\/start/, async (msg) => {
    if (!msg.from || !isAuthorized(msg.from.id)) return;
    await bot.sendMessage(
      msg.chat.id,
      "ASPS CEO Bot online. Send me a message and I'll process it with full project context and tool access.\n\nCommands:\n/reset — Clear conversation history\n/model — Show current model\n/reload — Reload system prompt",
    );
  });

  // /reset command
  bot.onText(/\/reset/, async (msg) => {
    if (!msg.from || !isAuthorized(msg.from.id)) return;
    clearSession(msg.from.id);
    await bot.sendMessage(msg.chat.id, "Session cleared.");
  });

  // /model command
  bot.onText(/\/model/, async (msg) => {
    if (!msg.from || !isAuthorized(msg.from.id)) return;
    const model = process.env.MODEL || "(Claude Code CLI default)";
    await bot.sendMessage(msg.chat.id, `Current model: ${model}`);
  });

  // /reload command — reload system prompt from disk
  bot.onText(/\/reload/, async (msg) => {
    if (!msg.from || !isAuthorized(msg.from.id)) return;
    reloadSystemPrompt();
    await bot.sendMessage(msg.chat.id, "System prompt reloaded from disk.");
  });

  // Handle all other text messages
  bot.on("message", async (msg) => {
    // Skip commands
    if (msg.text?.startsWith("/")) return;
    // Skip non-text messages
    if (!msg.text) return;
    // Auth check
    if (!msg.from || !isAuthorized(msg.from.id)) {
      await bot.sendMessage(
        msg.chat.id,
        "Unauthorized. Your user ID is not in the allowed list.",
      );
      return;
    }

    let stopTyping = startTypingIndicator(msg.chat.id);

    try {
      const response = await runAgent(msg.from.id, msg.text, () => {
        // Restart typing indicator on every streamed SDK event so it stays
        // fresh across long tool-use rounds.
        stopTyping();
        stopTyping = startTypingIndicator(msg.chat.id);
      });

      stopTyping();

      // Split and send response
      const chunks = splitMessage(response);
      for (const chunk of chunks) {
        await bot.sendMessage(msg.chat.id, chunk, {
          parse_mode: "Markdown",
        }).catch(async () => {
          // Markdown parse failed — send as plain text
          await bot.sendMessage(msg.chat.id, chunk);
        });
      }
    } catch (err) {
      stopTyping();
      const errorMsg =
        err instanceof Error ? err.message : "Unknown error occurred";
      console.error("Agent error:", errorMsg);
      await bot.sendMessage(
        msg.chat.id,
        `Error processing message: ${errorMsg}`,
      );
    }
  });

  // Edited messages are not re-processed as new turns, but the auth gate
  // still applies to every inbound update — silently drop edits from
  // anyone not on the allow-list.
  bot.on("edited_message", (msg) => {
    if (!msg.from || !isAuthorized(msg.from.id)) return;
  });

  // Callback queries (inline-keyboard taps). No inline keyboards are sent
  // today, but the handler enforces the allow-list on this update type too
  // and acknowledges the query so the Telegram client clears its spinner.
  bot.on("callback_query", async (cbQuery) => {
    if (!cbQuery.from || !isAuthorized(cbQuery.from.id)) {
      await bot.answerCallbackQuery(cbQuery.id, { text: "Unauthorized" }).catch(() => {});
      return;
    }
    await bot.answerCallbackQuery(cbQuery.id).catch(() => {});
  });

  // Handle polling errors
  bot.on("polling_error", (err) => {
    console.error("Polling error:", err.message);
  });
}

import { createHash } from "node:crypto";
import TelegramBot from "node-telegram-bot-api";
import { runAgent, reloadSystemPrompt } from "./agent.js";
import { clearSession } from "./session.js";
import { resolveApproval, setApprovalRequestHandler, type ApprovalRequest } from "./approvals.js";

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

/**
 * Deliver a pending approval request to the authorized user as an
 * inline-keyboard Telegram message (ASPS-743 security remediation, hardened
 * per the ASPS-743 security re-review, Major M1).
 *
 * `request.summary` (from `agent.ts`'s `summarizeToolCall`) is now always
 * the FULL, untruncated tool input — a Bash command or path can be
 * arbitrarily long and is fully attacker-influenced. Two things follow:
 *
 *  - **Plain text, never `parse_mode: "Markdown"`.** The previous design
 *    embedded the untrusted summary inside a raw ``` code fence with
 *    `parse_mode: "Markdown"` — balanced backticks inside the command could
 *    close the fence early and let the rest of the string re-render as
 *    arbitrary Markdown (bold/links/etc.) in the approver's chat. Sending as
 *    plain text means Telegram never parses the content as markup, so
 *    nothing in the command can alter the message's structure.
 *  - **Split, never truncate, when it exceeds Telegram's ~4096-char limit.**
 *    A message is built as a header (tool name, full summary length, and a
 *    short sha256 so a re-sent/edited approval can be told apart) followed
 *    by the full summary, then split into as many plain-text messages as
 *    needed (`splitMessage`, marked `[part i/N]` when there is more than
 *    one). The Approve/Deny inline keyboard is attached only to the final
 *    part, so there is exactly one place to tap and the full content still
 *    arrives — nothing is ever silently cut off.
 *
 * The bot only ever talks to one user per turn in a private chat, so the
 * Telegram chat id is the same as the requesting user's id.
 */
function sendApprovalRequest(request: ApprovalRequest): void {
  const hash = createHash("sha256").update(request.summary).digest("hex").slice(0, 12);
  const header = `Approval needed — ${request.toolName} (${request.summary.length} chars, sha256:${hash})`;
  const fullText = `${header}\n\n${request.summary}`;

  const rawChunks = splitMessage(fullText);
  const chunks =
    rawChunks.length > 1 ? rawChunks.map((chunk, i) => `[part ${i + 1}/${rawChunks.length}]\n${chunk}`) : rawChunks;

  const keyboard = {
    inline_keyboard: [
      [
        { text: "✅ Approve", callback_data: `approve:${request.id}` },
        { text: "❌ Deny", callback_data: `deny:${request.id}` },
      ],
    ],
  };

  // Fire all parts immediately (not serialized behind `await`) so the whole
  // prompt is dispatched in one synchronous pass — the caller must not
  // observe only the first part sent before later parts have gone out.
  chunks.forEach((chunk, i) => {
    const isLast = i === chunks.length - 1;
    // Deliberately no `parse_mode` — see the fence-breakout note above.
    bot.sendMessage(request.userId, chunk, isLast ? { reply_markup: keyboard } : {}).catch(() => {
      // Best-effort delivery per part; a transport failure here should not
      // throw out of the approval flow. If the approver never sees a usable
      // prompt (e.g. every part fails), `approvals.ts`'s own timeout still
      // fail-closes the pending request to "deny".
    });
  });
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
  setApprovalRequestHandler(sendApprovalRequest);

  console.log("Telegram CEO Bot started (polling mode)");
  // Never log the full authorized-user-id list — only its size. The list
  // itself is sensitive (it identifies exactly who can operate the agent)
  // and startup logs routinely end up in less-guarded places (journald,
  // log aggregators) than the .env file it was read from.
  console.log(`Authorized users: ${authorizedUsers.size} configured`);

  // /start command
  bot.onText(/\/start/, async (msg) => {
    if (!msg.from || !isAuthorized(msg.from.id)) return;
    await bot.sendMessage(
      msg.chat.id,
      "ASPS CEO Bot online. Send me a message and I'll process it with full project context and tool access.\n\n" +
        "Reads (files, search, knowledge base) run freely. Any state-changing action " +
        "(file write/edit, Bash, git push, etc.) will ask you to Approve/Deny first.\n\n" +
        "Commands:\n/reset — Clear conversation history\n/model — Show current model\n/reload — Reload system prompt",
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

  // Handle all other text messages — the agent trigger.
  bot.on("message", async (msg) => {
    // Skip commands
    if (msg.text?.startsWith("/")) return;
    // Skip non-text messages
    if (!msg.text) return;
    // Only ever act on a 1:1 private chat with the bot. A group/supergroup/
    // channel could contain an authorized user's messages too, but this bot
    // is a single-operator tool, not a group assistant — never trigger the
    // agent outside a private chat.
    if (msg.chat.type !== "private") return;
    // Auth check — drop silently. Replying "Unauthorized" to an
    // unrecognized sender lets anyone enumerate which user ids are
    // authorized by watching which ones get a distinct response.
    if (!msg.from || !isAuthorized(msg.from.id)) return;

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
      // Log the real error server-side only; the Telegram-facing message
      // stays generic so error text (which can include file paths, stack
      // frames, or internal details) never leaks to the chat.
      const detail = err instanceof Error ? err.stack || err.message : String(err);
      console.error("Agent error:", detail);
      await bot.sendMessage(msg.chat.id, "Sorry, something went wrong processing your message.");
    }
  });

  // Edited messages are not re-processed as new turns, but the auth gate
  // still applies to every inbound update — silently drop edits from
  // anyone not on the allow-list.
  bot.on("edited_message", (msg) => {
    if (!msg.from || !isAuthorized(msg.from.id)) return;
  });

  // Callback queries (inline-keyboard taps) — the approval flow's
  // transport. Acknowledges every query so the Telegram client clears its
  // spinner, but never distinguishes "unauthorized" from "unknown/expired/
  // wrong-user request" in the ack text — both cases enable enumeration
  // otherwise (of authorized user ids, and of live pending-approval ids).
  bot.on("callback_query", async (cbQuery) => {
    const ack = () => bot.answerCallbackQuery(cbQuery.id).catch(() => {});

    if (!cbQuery.from || !isAuthorized(cbQuery.from.id)) {
      await ack();
      return;
    }

    const match = /^(approve|deny):(.+)$/.exec(cbQuery.data ?? "");
    if (match) {
      const [, action, id] = match;
      resolveApproval(id, cbQuery.from.id, action === "approve" ? "allow" : "deny");
    }

    await ack();
  });

  // Handle polling errors
  bot.on("polling_error", (err) => {
    console.error("Polling error:", err.message);
  });
}

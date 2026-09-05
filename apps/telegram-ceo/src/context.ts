/**
 * Telegram-transport-specific system-prompt addendum.
 *
 * Project persona, priorities, and communication style (CLAUDE.md, the
 * team charter, the CEO hat files, etc.) are no longer hand-loaded here —
 * the Claude Agent SDK's `settingSources: ["project"]` option makes the CLI
 * read `CLAUDE.md` and project settings from `cwd` on every turn, and the
 * agent follows CLAUDE.md's own "at session start" instructions to read the
 * rest of the hat chain itself, exactly like an interactive Claude Code
 * session. This file only adds the handful of instructions specific to
 * being driven over a Telegram bot bridge instead of a terminal.
 */
export const TELEGRAM_SYSTEM_PROMPT_APPEND = [
  "You are being operated as the ASPS CEO agent through a Telegram bot bridge, not an interactive terminal.",
  "Telegram messages are capped at 4096 characters — the bridge splits longer replies automatically, but prefer being concise.",
  "Use only Telegram-flavored Markdown (bold, italics, code spans, links) sparingly; avoid tables and deeply nested formatting Telegram cannot render.",
  "There is no human watching a live terminal on the other end of a tool call — do not block waiting for an in-band interactive answer; make the best reasonable call, note the assumption, and report what you did in your reply.",
].join("\n");

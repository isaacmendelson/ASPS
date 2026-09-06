/**
 * Per-Telegram-user session tracking.
 *
 * The Claude Agent SDK persists full conversation history itself (each
 * `query()` call is a turn against a Claude Code session on disk); this
 * module only needs to remember which SDK session id belongs to which
 * Telegram user so the next message from that user can `resume` the same
 * multi-turn conversation instead of starting a new one.
 */
const sessionIds = new Map<number, string>();

/** Get the Claude Agent SDK session id for a Telegram user, if any. */
export function getSessionId(userId: number): string | undefined {
  return sessionIds.get(userId);
}

/** Record the Claude Agent SDK session id to resume for a Telegram user. */
export function setSessionId(userId: number, sessionId: string): void {
  sessionIds.set(userId, sessionId);
}

/** Clear a user's session — the next message starts a fresh conversation. */
export function clearSession(userId: number): void {
  sessionIds.delete(userId);
}

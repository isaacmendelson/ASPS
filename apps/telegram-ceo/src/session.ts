import type Anthropic from "@anthropic-ai/sdk";

type MessageParam = Anthropic.Messages.MessageParam;

/** In-memory conversation store, keyed by Telegram user ID. */
const sessions = new Map<number, MessageParam[]>();

export function getMessages(userId: number): MessageParam[] {
  if (!sessions.has(userId)) {
    sessions.set(userId, []);
  }
  return sessions.get(userId)!;
}

export function clearSession(userId: number): void {
  sessions.delete(userId);
}

export function addMessage(userId: number, message: MessageParam): void {
  const messages = getMessages(userId);
  messages.push(message);
}

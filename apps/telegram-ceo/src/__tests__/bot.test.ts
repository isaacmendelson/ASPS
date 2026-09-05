import { beforeEach, describe, expect, it, vi } from "vitest";

const runAgentMock = vi.fn();
const reloadSystemPromptMock = vi.fn();
const clearSessionMock = vi.fn();

vi.mock("../agent.js", () => ({
  runAgent: runAgentMock,
  reloadSystemPrompt: reloadSystemPromptMock,
}));

vi.mock("../session.js", () => ({
  clearSession: clearSessionMock,
}));

type Handler = (...args: unknown[]) => unknown;

class MockTelegramBot {
  onText = vi.fn<(re: RegExp, handler: Handler) => void>();
  on = vi.fn<(event: string, handler: Handler) => void>();
  sendMessage = vi.fn().mockResolvedValue(undefined);
  sendChatAction = vi.fn().mockResolvedValue(undefined);
  answerCallbackQuery = vi.fn().mockResolvedValue(undefined);

  constructor(_token: string, _opts: unknown) {
    instances.push(this);
  }

  textHandlerFor(command: string): Handler {
    const call = this.onText.mock.calls.find(([re]) => re.test(command));
    if (!call) throw new Error(`No onText handler registered matching ${command}`);
    return call[1];
  }

  eventHandler(event: string): Handler {
    const call = this.on.mock.calls.find(([e]) => e === event);
    if (!call) throw new Error(`No handler registered for event ${event}`);
    return call[1];
  }
}

const instances: MockTelegramBot[] = [];

vi.mock("node-telegram-bot-api", () => ({
  default: MockTelegramBot,
}));

const { startBot } = await import("../bot.js");

const AUTHORIZED_ID = 111;
const UNAUTHORIZED_ID = 999;

function currentBot(): MockTelegramBot {
  return instances[instances.length - 1];
}

function msgFrom(userId: number, text: string) {
  return {
    from: { id: userId },
    chat: { id: 555 },
    text,
  };
}

describe("bot auth gate and command handling", () => {
  beforeEach(() => {
    instances.length = 0;
    runAgentMock.mockReset();
    reloadSystemPromptMock.mockReset();
    clearSessionMock.mockReset();
    process.env.TELEGRAM_BOT_TOKEN = "test-token";
    process.env.AUTHORIZED_USERS = String(AUTHORIZED_ID);
    startBot();
  });

  it("responds to an authorized user's message via the agent", async () => {
    runAgentMock.mockResolvedValue("agent reply");
    const bot = currentBot();
    const handler = bot.eventHandler("message");

    await handler(msgFrom(AUTHORIZED_ID, "hello"));

    expect(runAgentMock).toHaveBeenCalledWith(AUTHORIZED_ID, "hello", expect.any(Function));
    expect(bot.sendMessage).toHaveBeenCalledWith(555, "agent reply", expect.anything());
  });

  it("blocks an unauthorized user's message from reaching the agent", async () => {
    const bot = currentBot();
    const handler = bot.eventHandler("message");

    await handler(msgFrom(UNAUTHORIZED_ID, "hello"));

    expect(runAgentMock).not.toHaveBeenCalled();
    expect(bot.sendMessage).toHaveBeenCalledWith(
      555,
      expect.stringMatching(/unauthorized/i),
    );
  });

  it("clears the session for an authorized /reset", async () => {
    const bot = currentBot();
    const handler = bot.textHandlerFor("/reset");

    await handler(msgFrom(AUTHORIZED_ID, "/reset"));

    expect(clearSessionMock).toHaveBeenCalledWith(AUTHORIZED_ID);
    expect(bot.sendMessage).toHaveBeenCalledWith(555, "Session cleared.");
  });

  it("ignores /reset from an unauthorized user", async () => {
    const bot = currentBot();
    const handler = bot.textHandlerFor("/reset");

    await handler(msgFrom(UNAUTHORIZED_ID, "/reset"));

    expect(clearSessionMock).not.toHaveBeenCalled();
    expect(bot.sendMessage).not.toHaveBeenCalled();
  });

  it("reloads the system prompt for an authorized /reload", async () => {
    const bot = currentBot();
    const handler = bot.textHandlerFor("/reload");

    await handler(msgFrom(AUTHORIZED_ID, "/reload"));

    expect(reloadSystemPromptMock).toHaveBeenCalled();
    expect(bot.sendMessage).toHaveBeenCalledWith(555, "System prompt reloaded from disk.");
  });

  it("denies an unauthorized callback_query and acknowledges authorized ones", async () => {
    const bot = currentBot();
    const handler = bot.eventHandler("callback_query");

    await handler({ id: "cb1", from: { id: UNAUTHORIZED_ID } });
    expect(bot.answerCallbackQuery).toHaveBeenCalledWith("cb1", { text: "Unauthorized" });

    await handler({ id: "cb2", from: { id: AUTHORIZED_ID } });
    expect(bot.answerCallbackQuery).toHaveBeenCalledWith("cb2");
  });

  it("does not throw when handling an edited_message from an unauthorized user", () => {
    const bot = currentBot();
    const handler = bot.eventHandler("edited_message");

    expect(() => handler(msgFrom(UNAUTHORIZED_ID, "edited text"))).not.toThrow();
  });
});

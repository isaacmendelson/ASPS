import { beforeEach, describe, expect, it, vi } from "vitest";

const queryMock = vi.fn();

vi.mock("@anthropic-ai/claude-agent-sdk", () => ({
  query: queryMock,
}));

// Imported after the mock so agent.ts picks up the mocked `query`.
const { runAgent, canUseTool } = await import("../agent.js");
const { clearSession, getSessionId } = await import("../session.js");

function asAsyncIterable<T>(items: T[]): AsyncIterable<T> {
  return {
    [Symbol.asyncIterator]() {
      let i = 0;
      return {
        next: async () =>
          i < items.length ? { value: items[i++], done: false } : { value: undefined, done: true },
      };
    },
  } as AsyncIterable<T>;
}

describe("canUseTool (destructive-command permission guard)", () => {
  it("denies a Bash call matching the destructive-command denylist", async () => {
    const result = await canUseTool(
      "Bash",
      { command: "rm -rf /home/aspsbot/ASPS" },
      { signal: new AbortController().signal, toolUseID: "t1", requestId: "r1" } as never,
    );
    expect(result.behavior).toBe("deny");
    if (result.behavior === "deny") {
      expect(result.message).toMatch(/Blocked/i);
    }
  });

  it("allows a benign Bash call", async () => {
    const result = await canUseTool(
      "Bash",
      { command: "git status" },
      { signal: new AbortController().signal, toolUseID: "t2", requestId: "r2" } as never,
    );
    expect(result.behavior).toBe("allow");
  });

  it("allows non-Bash tools unconditionally", async () => {
    const result = await canUseTool(
      "Read",
      { path: "README.md" },
      { signal: new AbortController().signal, toolUseID: "t3", requestId: "r3" } as never,
    );
    expect(result.behavior).toBe("allow");
  });

  it("never resolves to null (fail-closed would hang the tool call forever)", async () => {
    const result = await canUseTool(
      "Bash",
      { command: "DROP TABLE Users" },
      { signal: new AbortController().signal, toolUseID: "t4", requestId: "r4" } as never,
    );
    expect(result).not.toBeNull();
  });
});

describe("runAgent", () => {
  const userId = 12345;

  beforeEach(() => {
    queryMock.mockReset();
    clearSession(userId);
  });

  it("returns the final result text and stores the session id for resume", async () => {
    queryMock.mockReturnValue(
      asAsyncIterable([
        { type: "system", subtype: "init", session_id: "sess-1" },
        { type: "assistant", session_id: "sess-1" },
        { type: "result", subtype: "success", result: "Hello from Claude", session_id: "sess-1" },
      ]),
    );

    const text = await runAgent(userId, "hi");

    expect(text).toBe("Hello from Claude");
    expect(getSessionId(userId)).toBe("sess-1");
  });

  it("does not pass `resume` on the first turn for a user", async () => {
    queryMock.mockReturnValue(
      asAsyncIterable([{ type: "result", subtype: "success", result: "ok", session_id: "sess-1" }]),
    );

    await runAgent(userId, "first message");

    const callArgs = queryMock.mock.calls[0][0];
    expect(callArgs.options.resume).toBeUndefined();
  });

  it("passes `resume` with the stored session id on the next turn", async () => {
    queryMock.mockReturnValueOnce(
      asAsyncIterable([{ type: "result", subtype: "success", result: "first", session_id: "sess-1" }]),
    );
    await runAgent(userId, "first message");

    queryMock.mockReturnValueOnce(
      asAsyncIterable([{ type: "result", subtype: "success", result: "second", session_id: "sess-1" }]),
    );
    await runAgent(userId, "second message");

    const secondCallArgs = queryMock.mock.calls[1][0];
    expect(secondCallArgs.options.resume).toBe("sess-1");
  });

  it("wires cwd, permissionMode, canUseTool, and project settingSources", async () => {
    queryMock.mockReturnValue(
      asAsyncIterable([{ type: "result", subtype: "success", result: "ok", session_id: "sess-1" }]),
    );

    await runAgent(userId, "hi");

    const { options } = queryMock.mock.calls[0][0];
    expect(options.permissionMode).toBe("default");
    expect(typeof options.canUseTool).toBe("function");
    expect(options.settingSources).toContain("project");
  });

  it("invokes onEvent for every streamed SDK message", async () => {
    const messages = [
      { type: "system", subtype: "init", session_id: "sess-1" },
      { type: "assistant", session_id: "sess-1" },
      { type: "result", subtype: "success", result: "ok", session_id: "sess-1" },
    ];
    queryMock.mockReturnValue(asAsyncIterable(messages));

    const onEvent = vi.fn();
    await runAgent(userId, "hi", onEvent);

    expect(onEvent).toHaveBeenCalledTimes(messages.length);
  });

  it("surfaces a non-success result subtype as a readable message", async () => {
    queryMock.mockReturnValue(
      asAsyncIterable([
        {
          type: "result",
          subtype: "error_max_turns",
          errors: ["ran out of turns"],
          session_id: "sess-2",
        },
      ]),
    );

    const text = await runAgent(userId, "hi");

    expect(text).toContain("error_max_turns");
    expect(text).toContain("ran out of turns");
  });
});

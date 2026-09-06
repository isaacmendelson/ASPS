import { mkdtempSync, mkdirSync, writeFileSync, rmSync, realpathSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const queryMock = vi.fn();
const requestApprovalMock = vi.fn();
const loadClaudeMdMock = vi.fn(() => "CLAUDE_MD_FIXTURE_CONTENT");
const loadMcpServersMock = vi.fn(() => ({ "knowledge-engine": { command: "python", args: ["ke_mcp_server.py"] } }));

vi.mock("@anthropic-ai/claude-agent-sdk", () => ({
  query: queryMock,
}));

vi.mock("../approvals.js", () => ({
  requestApproval: requestApprovalMock,
}));

vi.mock("../context.js", () => ({
  TELEGRAM_SYSTEM_PROMPT_APPEND: "TELEGRAM_APPEND_FIXTURE",
  loadClaudeMd: loadClaudeMdMock,
  loadMcpServers: loadMcpServersMock,
}));

// Imported after the mocks so agent.ts picks up the mocked collaborators.
const { runAgent, createCanUseTool } = await import("../agent.js");
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

const toolOptions = { signal: new AbortController().signal, toolUseID: "t1", requestId: "r1" } as never;

describe("createCanUseTool — path guard (ASPS-743 blocker B1)", () => {
  let workingDir: string;

  beforeEach(() => {
    workingDir = realpathSync(mkdtempSync(path.join(tmpdir(), "asps-agent-path-guard-")));
    mkdirSync(path.join(workingDir, "src"), { recursive: true });
    writeFileSync(path.join(workingDir, "src", "agent.ts"), "// fixture\n");
    writeFileSync(path.join(workingDir, ".env"), "SECRET=1\n");
    requestApprovalMock.mockReset();
  });

  afterEach(() => {
    rmSync(workingDir, { recursive: true, force: true });
  });

  it("denies a Read outside the working directory, without ever asking for approval", async () => {
    const canUseTool = createCanUseTool(111, workingDir);
    const result = await canUseTool("Read", { file_path: "/etc/passwd" }, toolOptions);

    expect(result?.behavior).toBe("deny");
    expect(requestApprovalMock).not.toHaveBeenCalled();
  });

  it("denies a Write to a secret-pattern path even inside the working directory", async () => {
    const canUseTool = createCanUseTool(111, workingDir);
    const result = await canUseTool("Write", { file_path: path.join(workingDir, ".env"), content: "x" }, toolOptions);

    expect(result?.behavior).toBe("deny");
    if (result?.behavior === "deny") expect(result.message).toMatch(/path guard/i);
    expect(requestApprovalMock).not.toHaveBeenCalled();
  });

  it("allows a Read inside the working directory (auto-allow, subject to the path guard)", async () => {
    const canUseTool = createCanUseTool(111, workingDir);
    const result = await canUseTool("Read", { file_path: path.join(workingDir, "src", "agent.ts") }, toolOptions);

    expect(result?.behavior).toBe("allow");
    expect(requestApprovalMock).not.toHaveBeenCalled();
  });

  it("still requires approval for an in-tree Write (path guard passing does not itself grant approval)", async () => {
    requestApprovalMock.mockResolvedValue("allow");
    const canUseTool = createCanUseTool(111, workingDir);
    const result = await canUseTool(
      "Write",
      { file_path: path.join(workingDir, "src", "new.ts"), content: "x" },
      toolOptions,
    );

    expect(requestApprovalMock).toHaveBeenCalledWith(111, "Write", expect.stringContaining("new.ts"));
    expect(result?.behavior).toBe("allow");
  });
});

describe("createCanUseTool — Bash hard-deny (ASPS-743 blocker B2)", () => {
  beforeEach(() => {
    requestApprovalMock.mockReset();
  });

  it("denies a destructive Bash command without ever asking for approval — the denylist is not overridable", async () => {
    const canUseTool = createCanUseTool(111, process.cwd());
    const result = await canUseTool("Bash", { command: "rm -rf /home/aspsbot/ASPS" }, toolOptions);

    expect(result?.behavior).toBe("deny");
    if (result?.behavior === "deny") expect(result.message).toMatch(/Blocked/i);
    expect(requestApprovalMock).not.toHaveBeenCalled();
  });

  it("routes a benign Bash command through Telegram approval rather than auto-allowing", async () => {
    requestApprovalMock.mockResolvedValue("allow");
    const canUseTool = createCanUseTool(111, process.cwd());
    const result = await canUseTool("Bash", { command: "git status" }, toolOptions);

    expect(requestApprovalMock).toHaveBeenCalledWith(111, "Bash", "git status");
    expect(result?.behavior).toBe("allow");
  });

  it("never resolves to null (fail-closed would hang the tool call forever)", async () => {
    requestApprovalMock.mockResolvedValue("deny");
    const canUseTool = createCanUseTool(111, process.cwd());
    const result = await canUseTool("Bash", { command: "DROP TABLE Users" }, toolOptions);
    expect(result).not.toBeNull();
  });
});

describe("createCanUseTool — deny-by-default (ASPS-743 blocker B3)", () => {
  beforeEach(() => {
    requestApprovalMock.mockReset();
  });

  it.each(["Read", "Grep", "Glob"])("auto-allows %s without approval", async (toolName) => {
    const canUseTool = createCanUseTool(111, process.cwd());
    const result = await canUseTool(toolName, {}, toolOptions);
    expect(result?.behavior).toBe("allow");
    expect(requestApprovalMock).not.toHaveBeenCalled();
  });

  it.each(["mcp__knowledge-engine__knowledge_search", "mcp__knowledge-engine__knowledge_ask"])(
    "auto-allows the read-only knowledge-engine MCP tool %s without approval",
    async (toolName) => {
      const canUseTool = createCanUseTool(111, process.cwd());
      const result = await canUseTool(toolName, { query: "x" }, toolOptions);
      expect(result?.behavior).toBe("allow");
      expect(requestApprovalMock).not.toHaveBeenCalled();
    },
  );

  it.each(["Write", "Edit", "NotebookEdit", "Task", "WebFetch", "mcp__github__create_pr", "SomeUnclassifiedTool"])(
    "requires Telegram approval for %s — deny-by-default, not auto-allow",
    async (toolName) => {
      requestApprovalMock.mockResolvedValue("allow");
      const canUseTool = createCanUseTool(111, process.cwd());
      const result = await canUseTool(toolName, {}, toolOptions);
      expect(requestApprovalMock).toHaveBeenCalledWith(111, toolName, expect.any(String));
      expect(result?.behavior).toBe("allow");
    },
  );

  it("denies the tool call when the Telegram approval is denied", async () => {
    requestApprovalMock.mockResolvedValue("deny");
    const canUseTool = createCanUseTool(111, process.cwd());
    const result = await canUseTool("Write", { file_path: "x.ts", content: "y" }, toolOptions);
    expect(result?.behavior).toBe("deny");
  });

  it("denies the tool call when the Telegram approval times out", async () => {
    requestApprovalMock.mockResolvedValue("deny"); // requestApproval itself resolves "deny" on timeout
    const canUseTool = createCanUseTool(111, process.cwd());
    const result = await canUseTool("Bash", { command: "npm install" }, toolOptions);
    expect(result?.behavior).toBe("deny");
  });

  it("correlates the approval request with the user who owns the turn", async () => {
    requestApprovalMock.mockResolvedValue("allow");
    const canUseTool = createCanUseTool(42, process.cwd());
    await canUseTool("Write", { file_path: "x.ts", content: "y" }, toolOptions);
    expect(requestApprovalMock).toHaveBeenCalledWith(42, "Write", expect.any(String));
  });
});

describe("runAgent", () => {
  const userId = 12345;

  beforeEach(() => {
    queryMock.mockReset();
    requestApprovalMock.mockReset();
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

  it("wires cwd, permissionMode, and canUseTool", async () => {
    queryMock.mockReturnValue(
      asAsyncIterable([{ type: "result", subtype: "success", result: "ok", session_id: "sess-1" }]),
    );

    await runAgent(userId, "hi");

    const { options } = queryMock.mock.calls[0][0];
    expect(options.permissionMode).toBe("default");
    expect(typeof options.canUseTool).toBe("function");
  });

  it("does NOT load filesystem settings sources (settingSources must stay empty — B3 precedence finding)", async () => {
    queryMock.mockReturnValue(
      asAsyncIterable([{ type: "result", subtype: "success", result: "ok", session_id: "sess-1" }]),
    );

    await runAgent(userId, "hi");

    const { options } = queryMock.mock.calls[0][0];
    expect(options.settingSources).toEqual([]);
  });

  it("wires MCP servers explicitly with strictMcpConfig, instead of relying on settingSources auto-discovery", async () => {
    queryMock.mockReturnValue(
      asAsyncIterable([{ type: "result", subtype: "success", result: "ok", session_id: "sess-1" }]),
    );

    await runAgent(userId, "hi");

    const { options } = queryMock.mock.calls[0][0];
    expect(options.mcpServers).toEqual({ "knowledge-engine": { command: "python", args: ["ke_mcp_server.py"] } });
    expect(options.strictMcpConfig).toBe(true);
  });

  it("auto-allows only the read-only knowledge-engine MCP tools at the SDK level (not Read/Grep/Glob/Bash/Write)", async () => {
    queryMock.mockReturnValue(
      asAsyncIterable([{ type: "result", subtype: "success", result: "ok", session_id: "sess-1" }]),
    );

    await runAgent(userId, "hi");

    const { options } = queryMock.mock.calls[0][0];
    expect(options.allowedTools).toEqual([
      "mcp__knowledge-engine__knowledge_search",
      "mcp__knowledge-engine__knowledge_ask",
    ]);
  });

  it("includes CLAUDE.md content and the Telegram addendum in the system prompt", async () => {
    queryMock.mockReturnValue(
      asAsyncIterable([{ type: "result", subtype: "success", result: "ok", session_id: "sess-1" }]),
    );

    await runAgent(userId, "hi");

    const { options } = queryMock.mock.calls[0][0];
    expect(options.systemPrompt.append).toContain("CLAUDE_MD_FIXTURE_CONTENT");
    expect(options.systemPrompt.append).toContain("TELEGRAM_APPEND_FIXTURE");
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

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

// ASPS-767: mock node:child_process so `git_push` never spawns a real `git`
// process. `vi.mock` itself is hoisted by vitest above every import in this
// file (same mechanism as jest's inline-mock hoisting) — `vi.hoisted` is
// required so `execFileMock` is initialized before that hoisted factory runs
// (a plain top-level `const` would still be hoisted-but-uninitialized at
// that point, throwing a TDZ ReferenceError).
const { execFileMock } = vi.hoisted(() => ({ execFileMock: vi.fn() }));
vi.mock("node:child_process", () => ({
  execFile: execFileMock,
}));

import {
  JIRA_ISSUE_KEY_PATTERN,
  buildPrivilegedMcpServer,
  githubCommentTool,
  githubCreatePrTool,
  gitPushTool,
  jiraCommentTool,
  jiraTransitionTool,
  jiraUpdateIssueTool,
  validateBranch,
  validateRemote,
} from "../privileged.js";

/**
 * ASPS-766 (ADR-005 part 2 / ASPS-763-3) — the in-process `ceo-privileged`
 * MCP server's write tools. `agent.test.ts` proves the GATING half (every
 * `mcp__ceo-privileged__*` call routes through `canUseTool` to a Telegram
 * approval, never auto-allowed). This file proves the HANDLER half: each
 * tool reads its credential from the bot-process env (never hardcoded,
 * never read from a file), shapes the REST call correctly, and validates
 * its input (issue keys, PR/issue numbers) before ever building a request.
 *
 * `fetch` is stubbed globally — no real network call is ever made.
 */

const ENV_VARS = ["JIRA_BASE_URL", "JIRA_EMAIL", "JIRA_API_TOKEN", "GITHUB_TOKEN", "GITHUB_REPO_URL"] as const;
const savedEnv: Record<string, string | undefined> = {};

function jsonResponse(body: unknown, status = 200): Response {
  // A 204 (No Content) response must have a null body, or the Fetch API's
  // Response constructor throws — matches what the real JIRA REST API
  // returns for a successful transition/update.
  if (status === 204) return new Response(null, { status });
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

beforeEach(() => {
  for (const key of ENV_VARS) savedEnv[key] = process.env[key];
  process.env.JIRA_BASE_URL = "https://example.atlassian.net";
  process.env.JIRA_EMAIL = "ceo@example.com";
  process.env.JIRA_API_TOKEN = "jira-secret-token";
  process.env.GITHUB_TOKEN = "gh-secret-token";
  process.env.GITHUB_REPO_URL = "https://github.com/isaacmendelson/asps-software";
});

afterEach(() => {
  for (const key of ENV_VARS) {
    if (savedEnv[key] === undefined) delete process.env[key];
    else process.env[key] = savedEnv[key];
  }
  vi.unstubAllGlobals();
  execFileMock.mockReset();
});

/** Resolves the mocked `execFile`'s Node-style `(error, stdout, stderr)` callback (last arg). */
function resolveExecFile(stdout = "", stderr = ""): void {
  execFileMock.mockImplementationOnce((_cmd, _args, _opts, callback) => {
    callback(null, stdout, stderr);
  });
}

function rejectExecFile(error: Error): void {
  execFileMock.mockImplementationOnce((_cmd, _args, _opts, callback) => {
    callback(error, "", "");
  });
}

describe("JIRA_ISSUE_KEY_PATTERN", () => {
  it.each(["ASPS-766", "A-1", "ABCDE-12345"])("matches a valid issue key: %s", (key) => {
    expect(JIRA_ISSUE_KEY_PATTERN.test(key)).toBe(true);
  });

  it.each(["asps-766", "ASPS766", "ASPS-", "-766", "ASPS-766; rm -rf /", "ASPS-766 extra"])(
    "rejects an invalid issue key: %s",
    (key) => {
      expect(JIRA_ISSUE_KEY_PATTERN.test(key)).toBe(false);
    },
  );
});

describe("jira_transition — reads creds from env, calls the JIRA REST transitions endpoint", () => {
  it("sends a Basic auth header built from JIRA_EMAIL/JIRA_API_TOKEN and posts the transition id", async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({}, 204));
    vi.stubGlobal("fetch", fetchMock);

    const result = await jiraTransitionTool.handler({ issueKey: "ASPS-766", transitionId: "31" }, {});

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("https://example.atlassian.net/rest/api/3/issue/ASPS-766/transitions");
    expect(init.method).toBe("POST");
    const expectedAuth = `Basic ${Buffer.from("ceo@example.com:jira-secret-token").toString("base64")}`;
    expect(init.headers.Authorization).toBe(expectedAuth);
    expect(JSON.parse(init.body)).toEqual({ transition: { id: "31" } });
    expect(result.isError).toBeFalsy();
  });

  it("returns an error result (not a throw) when JIRA_API_TOKEN is missing from the environment", async () => {
    delete process.env.JIRA_API_TOKEN;
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    const result = await jiraTransitionTool.handler({ issueKey: "ASPS-766", transitionId: "31" }, {});

    expect(result.isError).toBe(true);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("returns an error result for an invalid issue key instead of building a request", async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    const result = await jiraTransitionTool.handler({ issueKey: "not-a-key", transitionId: "31" }, {});

    expect(result.isError).toBe(true);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("surfaces a non-2xx JIRA response as an error result", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(jsonResponse({ errorMessages: ["no such transition"] }, 400)));

    const result = await jiraTransitionTool.handler({ issueKey: "ASPS-766", transitionId: "999" }, {});

    expect(result.isError).toBe(true);
    expect(result.content[0].text).toContain("400");
  });
});

describe("jira_comment — wraps plain text in Atlassian Document Format", () => {
  it("posts an ADF-wrapped comment body to the JIRA comment endpoint", async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ id: "123" }, 201));
    vi.stubGlobal("fetch", fetchMock);

    await jiraCommentTool.handler({ issueKey: "ASPS-766", body: "QA PASS, ready to merge" }, {});

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("https://example.atlassian.net/rest/api/3/issue/ASPS-766/comment");
    const parsed = JSON.parse(init.body);
    expect(parsed.body.type).toBe("doc");
    expect(parsed.body.content[0].content[0].text).toBe("QA PASS, ready to merge");
  });
});

describe("jira_update_issue — PUTs arbitrary fields (e.g. labels)", () => {
  it("PUTs the fields object to the issue endpoint", async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({}, 204));
    vi.stubGlobal("fetch", fetchMock);

    await jiraUpdateIssueTool.handler({ issueKey: "ASPS-766", fields: { labels: ["backend", "qa-pass"] } }, {});

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("https://example.atlassian.net/rest/api/3/issue/ASPS-766");
    expect(init.method).toBe("PUT");
    expect(JSON.parse(init.body)).toEqual({ fields: { labels: ["backend", "qa-pass"] } });
  });
});

describe("github_create_pr — reads GITHUB_TOKEN + GITHUB_REPO_URL from env", () => {
  it("sends a Bearer auth header from GITHUB_TOKEN and posts to the parsed owner/repo", async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ html_url: "https://github.com/x/y/pull/1" }, 201));
    vi.stubGlobal("fetch", fetchMock);

    const result = await githubCreatePrTool.handler(
      { title: "ASPS-766 Add privileged MCP", head: "asps-766-privileged-mcp", base: "main", body: "desc" },
      {},
    );

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("https://api.github.com/repos/isaacmendelson/asps-software/pulls");
    expect(init.headers.Authorization).toBe("Bearer gh-secret-token");
    expect(JSON.parse(init.body)).toEqual({
      title: "ASPS-766 Add privileged MCP",
      head: "asps-766-privileged-mcp",
      base: "main",
      body: "desc",
    });
    expect(result.isError).toBeFalsy();
    expect(result.content[0].text).toContain("https://github.com/x/y/pull/1");
  });

  it("returns an error result when GITHUB_REPO_URL is missing", async () => {
    delete process.env.GITHUB_REPO_URL;
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    const result = await githubCreatePrTool.handler({ title: "x", head: "h", base: "main", body: "" }, {});

    expect(result.isError).toBe(true);
    expect(fetchMock).not.toHaveBeenCalled();
  });
});

describe("github_comment — validates the issue/PR number", () => {
  it("posts a comment to the numeric issue endpoint", async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ id: 1 }, 201));
    vi.stubGlobal("fetch", fetchMock);

    await githubCommentTool.handler({ issueNumber: 53, body: "Merged." }, {});

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("https://api.github.com/repos/isaacmendelson/asps-software/issues/53/comments");
    expect(JSON.parse(init.body)).toEqual({ body: "Merged." });
  });

  it("rejects a non-integer/non-positive issue number without ever calling fetch (schema bypass defense-in-depth)", async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    const result = await githubCommentTool.handler({ issueNumber: -1, body: "x" }, {});

    expect(result.isError).toBe(true);
    expect(fetchMock).not.toHaveBeenCalled();
  });
});

describe("git_push (ASPS-767, ADR-005 implementation-plan story ASPS-763-4) — the sanctioned push path", () => {
  it("pushes via execFile with no shell, using the exact argv shape ['-C', workingDir, 'push', remote, branch]", async () => {
    resolveExecFile("stdout-fixture", "");

    const result = await gitPushTool.handler({ remote: "origin", branch: "asps-767-git-push-tool" }, {});

    expect(execFileMock).toHaveBeenCalledTimes(1);
    const [command, args, options] = execFileMock.mock.calls[0];
    expect(command).toBe("git");
    expect(args).toEqual(["-C", process.cwd(), "push", "origin", "asps-767-git-push-tool"]);
    expect(options).toEqual({ shell: false });
    expect(result.isError).toBeFalsy();
  });

  it("defaults remote to 'origin' when omitted", async () => {
    resolveExecFile();

    await gitPushTool.handler({ branch: "main" }, {});

    const [, args] = execFileMock.mock.calls[0];
    expect(args).toEqual(["-C", process.cwd(), "push", "origin", "main"]);
  });

  it("honors an explicit non-default remote", async () => {
    resolveExecFile();

    await gitPushTool.handler({ remote: "upstream", branch: "main" }, {});

    const [, args] = execFileMock.mock.calls[0];
    expect(args).toEqual(["-C", process.cwd(), "push", "upstream", "main"]);
  });

  it.each([
    ["--force"],
    ["--force-with-lease"],
    ["-f"],
    ["+branch"],
    ["branch --force"],
    ["branch; rm -rf /"],
    ["branch`whoami`"],
    ["branch$(whoami)"],
    ["branch|cat"],
    ["branch\nrm -rf /"],
  ])("rejects a force-push / injection form as branch (%s) — error, no execFile call", async (branch) => {
    const result = await gitPushTool.handler({ remote: "origin", branch }, {});

    expect(result.isError).toBe(true);
    expect(execFileMock).not.toHaveBeenCalled();
  });

  it.each([["origin; rm -rf /"], ["-x"], ["http://evil.example/x"], ["origin`whoami`"]])(
    "rejects an unsafe remote (%s) — error, no execFile call",
    async (remote) => {
      const result = await gitPushTool.handler({ remote, branch: "main" }, {});

      expect(result.isError).toBe(true);
      expect(execFileMock).not.toHaveBeenCalled();
    },
  );

  it("rejects a force-push branch via validateBranch directly (unit-level, defense-in-depth)", () => {
    expect(() => validateBranch("--force")).toThrow(/hard-denied/);
    expect(() => validateBranch("+main")).toThrow(/hard-denied/);
    expect(() => validateBranch("main; rm -rf /")).toThrow(/shell metacharacter/);
  });

  it("rejects an unsafe remote via validateRemote directly (unit-level, defense-in-depth)", () => {
    expect(() => validateRemote("origin; rm -rf /")).toThrow(/shell metacharacter/);
    expect(() => validateRemote("-x")).toThrow();
  });

  it("surfaces an execFile error as an error result without leaking any credential value", async () => {
    process.env.GITHUB_TOKEN = "super-secret-token-value";
    rejectExecFile(new Error("fatal: Authentication failed for 'https://github.com/x/y.git/'"));

    const result = await gitPushTool.handler({ remote: "origin", branch: "main" }, {});

    expect(result.isError).toBe(true);
    expect(result.content[0].text).not.toContain("super-secret-token-value");
    expect(result.content[0].text).toContain("Authentication failed");
  });

  it("is included in PRIVILEGED_TOOLS / buildPrivilegedMcpServer with the name 'git_push'", () => {
    expect(gitPushTool.name).toBe("git_push");
  });
});

describe("buildPrivilegedMcpServer", () => {
  it("returns an SDK MCP server named ceo-privileged with all six write tools registered, including git_push", () => {
    const server = buildPrivilegedMcpServer();

    expect(server.type).toBe("sdk");
    expect(server.name).toBe("ceo-privileged");
    expect(server.instance).toBeDefined();
  });
});

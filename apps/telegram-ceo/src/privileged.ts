import { z } from "zod";
import { createSdkMcpServer, tool } from "@anthropic-ai/claude-agent-sdk";
import type { McpServerConfig, SdkMcpToolDefinition } from "@anthropic-ai/claude-agent-sdk";
import type { CallToolResult } from "@modelcontextprotocol/sdk/types.js";

/**
 * In-process gated MCP server for privileged JIRA/GitHub WRITE operations
 * (ASPS-766, ADR-005 part 2 / implementation-plan story ASPS-763-3).
 *
 * ADR-005's two-part privilege separation: part 1 (ASPS-765) contains every
 * Bash execution in the SDK's bubblewrap sandbox, denying it the secrets
 * dir/credentials — which also took away the agent's former ability to do
 * JIRA/GitHub writes via `curl`/`gh` in Bash (they read `JIRA_API_TOKEN`/
 * `GITHUB_TOKEN` straight out of the process environment, which sandboxed
 * Bash can no longer see, by design). Part 2 (this file) restores write
 * capability WITHOUT reopening that hole: `createSdkMcpServer` tool handlers
 * run in the SDK host — the bot's own Node process (`aspsbot`, unsandboxed)
 * — never inside a bwrap-contained child, so they can read the bot-process
 * env vars directly, and the calls go straight to the JIRA/GitHub REST APIs
 * (no shell involved at all — no `curl`/`gh`/Bash, so there is no shell
 * injection surface for `title`/`body`/label values to land in).
 *
 * THE LOAD-BEARING GATING REQUIREMENT: this server is deliberately NOT added
 * to `agent.ts`'s `AUTO_ALLOW_MCP_WILDCARDS` or `AUTO_ALLOW_MCP_TOOLS` — so
 * every tool below falls through `createCanUseTool`'s classification to the
 * final deny-by-default branch, which calls `requestApproval()` and blocks
 * until the authorized Telegram user taps Approve. This is enforced at the
 * call site (`agent.ts`'s `buildOptions`/`AUTO_ALLOW_MCP_WILDCARDS`), not in
 * this file — see `agent.test.ts`'s "ceo-privileged MCP" describe block and
 * `README.md`'s permission-model section for the routing proof. Unlike the
 * read-only `github`/`mcp-atlassian` servers (ASPS-748), which are read-only
 * BY CONSTRUCTION and therefore get a documented wildcard exception, every
 * tool here mutates JIRA/GitHub state and must never get that exception.
 *
 * `git_push` is intentionally NOT in this file — a separate gated tool,
 * ASPS-767 (ADR-005 implementation-plan story ASPS-763-4).
 */

/** JIRA Cloud issue key, e.g. `ASPS-766`. Single source of truth — do not duplicate. */
export const JIRA_ISSUE_KEY_PATTERN = /^[A-Z]+-\d+$/;

function validateIssueKey(issueKey: string): void {
  if (!JIRA_ISSUE_KEY_PATTERN.test(issueKey)) {
    throw new Error(`Invalid JIRA issue key '${issueKey}' — expected the PROJECT-123 format`);
  }
}

/**
 * Belt-and-suspenders re-check inside every handler (not just the zod
 * `inputSchema`): the schema is enforced by the real MCP protocol layer when
 * a tool is invoked through the SDK, but a handler must never trust its own
 * input blindly — this mirrors the rest of the codebase's "any doubt stays
 * fail-closed" style (see `security.ts`).
 */
function validatePrNumber(value: number): void {
  if (!Number.isInteger(value) || value <= 0) {
    throw new Error(`Invalid PR/issue number '${value}' — must be a positive integer`);
  }
}

function requireEnv(name: string): string {
  const value = process.env[name];
  if (!value) throw new Error(`${name} is not set in the bot process environment`);
  return value;
}

function jiraBaseUrl(): string {
  return requireEnv("JIRA_BASE_URL").replace(/\/+$/, "");
}

/** JIRA Cloud REST auth — email + API token, Basic-encoded (Atlassian's documented scheme). */
function jiraAuthHeader(): string {
  const email = requireEnv("JIRA_EMAIL");
  const token = requireEnv("JIRA_API_TOKEN");
  return `Basic ${Buffer.from(`${email}:${token}`).toString("base64")}`;
}

/** Bearer token from `GITHUB_TOKEN`, same env var the read-only `github` MCP server (ASPS-748) reuses. */
function githubAuthHeader(): string {
  return `Bearer ${requireEnv("GITHUB_TOKEN")}`;
}

/**
 * `GITHUB_REPO_URL` — already-documented non-credential config var (see
 * `agent.ts`'s `SANDBOX_DENIED_ENV_VARS` comment, which lists it as one of
 * the vars deliberately NOT denied to the sandbox because it grants no
 * access on its own). Parsed here so `github_create_pr`/`github_comment`
 * don't need the caller to spell out owner/repo on every call. Accepts
 * `https://github.com/<owner>/<repo>` with an optional trailing `.git`/`/`.
 */
const GITHUB_REPO_URL_PATTERN = /^https:\/\/github\.com\/([^/]+)\/([^/]+?)(?:\.git)?\/?$/i;

function githubRepo(): { owner: string; repo: string } {
  const url = requireEnv("GITHUB_REPO_URL");
  const match = GITHUB_REPO_URL_PATTERN.exec(url);
  if (!match) {
    throw new Error(`GITHUB_REPO_URL is not a valid https://github.com/<owner>/<repo> URL: ${url}`);
  }
  return { owner: match[1], repo: match[2] };
}

function textResult(text: string): CallToolResult {
  return { content: [{ type: "text", text }] };
}

function errorResult(err: unknown): CallToolResult {
  const message = err instanceof Error ? err.message : String(err);
  return { content: [{ type: "text", text: `Error: ${message}` }], isError: true };
}

/** Shared REST helper — never shells out (`fetch` directly), always JSON in/out. */
async function restCall(
  url: string,
  init: { method: string; headers: Record<string, string>; body?: unknown },
): Promise<{ status: number; json: unknown }> {
  const response = await fetch(url, {
    method: init.method,
    headers: init.headers,
    ...(init.body !== undefined ? { body: JSON.stringify(init.body) } : {}),
  });
  let json: unknown = undefined;
  const raw = await response.text();
  if (raw) {
    try {
      json = JSON.parse(raw);
    } catch {
      json = raw;
    }
  }
  if (!response.ok) {
    throw new Error(`${init.method} ${url} failed: ${response.status} ${JSON.stringify(json)}`);
  }
  return { status: response.status, json };
}

const jiraTransitionTool = tool(
  "jira_transition",
  "Transition a JIRA issue to a new workflow status by transition id. WRITE operation — requires Telegram approval.",
  {
    issueKey: z.string().regex(JIRA_ISSUE_KEY_PATTERN, "must match PROJECT-123"),
    transitionId: z.string().regex(/^\d+$/, "must be the numeric JIRA transition id, e.g. '31'"),
  },
  async ({ issueKey, transitionId }): Promise<CallToolResult> => {
    try {
      validateIssueKey(issueKey);
      const url = `${jiraBaseUrl()}/rest/api/3/issue/${encodeURIComponent(issueKey)}/transitions`;
      await restCall(url, {
        method: "POST",
        headers: {
          Authorization: jiraAuthHeader(),
          "Content-Type": "application/json",
          Accept: "application/json",
        },
        body: { transition: { id: transitionId } },
      });
      return textResult(`Transitioned ${issueKey} to transition id ${transitionId}.`);
    } catch (err) {
      return errorResult(err);
    }
  },
);

const jiraCommentTool = tool(
  "jira_comment",
  "Add a comment to a JIRA issue. WRITE operation — requires Telegram approval.",
  {
    issueKey: z.string().regex(JIRA_ISSUE_KEY_PATTERN, "must match PROJECT-123"),
    body: z.string().min(1),
  },
  async ({ issueKey, body }): Promise<CallToolResult> => {
    try {
      validateIssueKey(issueKey);
      const url = `${jiraBaseUrl()}/rest/api/3/issue/${encodeURIComponent(issueKey)}/comment`;
      // JIRA Cloud v3 comment bodies are Atlassian Document Format, not plain
      // text — wrap the plain-text `body` in the minimal valid ADF shape.
      const adfBody = {
        type: "doc",
        version: 1,
        content: [{ type: "paragraph", content: [{ type: "text", text: body }] }],
      };
      await restCall(url, {
        method: "POST",
        headers: {
          Authorization: jiraAuthHeader(),
          "Content-Type": "application/json",
          Accept: "application/json",
        },
        body: { body: adfBody },
      });
      return textResult(`Comment added to ${issueKey}.`);
    } catch (err) {
      return errorResult(err);
    }
  },
);

const jiraUpdateIssueTool = tool(
  "jira_update_issue",
  "Update fields on a JIRA issue (e.g. labels). WRITE operation — requires Telegram approval.",
  {
    issueKey: z.string().regex(JIRA_ISSUE_KEY_PATTERN, "must match PROJECT-123"),
    fields: z.record(z.string(), z.unknown()),
  },
  async ({ issueKey, fields }): Promise<CallToolResult> => {
    try {
      validateIssueKey(issueKey);
      const url = `${jiraBaseUrl()}/rest/api/3/issue/${encodeURIComponent(issueKey)}`;
      await restCall(url, {
        method: "PUT",
        headers: {
          Authorization: jiraAuthHeader(),
          "Content-Type": "application/json",
          Accept: "application/json",
        },
        body: { fields },
      });
      return textResult(`Updated fields on ${issueKey}: ${Object.keys(fields).join(", ")}.`);
    } catch (err) {
      return errorResult(err);
    }
  },
);

const githubCreatePrTool = tool(
  "github_create_pr",
  "Create a GitHub pull request on the configured repo (GITHUB_REPO_URL). WRITE operation — requires Telegram approval.",
  {
    title: z.string().min(1),
    head: z.string().min(1),
    base: z.string().min(1),
    body: z.string().default(""),
  },
  async ({ title, head, base, body }): Promise<CallToolResult> => {
    try {
      const { owner, repo } = githubRepo();
      const url = `https://api.github.com/repos/${encodeURIComponent(owner)}/${encodeURIComponent(repo)}/pulls`;
      const { json } = await restCall(url, {
        method: "POST",
        headers: {
          Authorization: githubAuthHeader(),
          Accept: "application/vnd.github+json",
          "Content-Type": "application/json",
        },
        body: { title, head, base, body },
      });
      const htmlUrl = (json as { html_url?: string } | undefined)?.html_url ?? "(no url returned)";
      return textResult(`Created PR ${head} -> ${base}: ${htmlUrl}`);
    } catch (err) {
      return errorResult(err);
    }
  },
);

const githubCommentTool = tool(
  "github_comment",
  "Add a comment to a GitHub issue or pull request on the configured repo (GITHUB_REPO_URL). WRITE operation — requires Telegram approval.",
  {
    issueNumber: z.number().int().positive(),
    body: z.string().min(1),
  },
  async ({ issueNumber, body }): Promise<CallToolResult> => {
    try {
      validatePrNumber(issueNumber);
      const { owner, repo } = githubRepo();
      const url = `https://api.github.com/repos/${encodeURIComponent(owner)}/${encodeURIComponent(repo)}/issues/${issueNumber}/comments`;
      await restCall(url, {
        method: "POST",
        headers: {
          Authorization: githubAuthHeader(),
          Accept: "application/vnd.github+json",
          "Content-Type": "application/json",
        },
        body: { body },
      });
      return textResult(`Comment added to #${issueNumber}.`);
    } catch (err) {
      return errorResult(err);
    }
  },
);

/**
 * Exported individually (not just bundled in the server) so tests can invoke
 * each handler directly — verifying it reads creds from env and shapes the
 * REST call correctly — without going through the full MCP protocol
 * dispatch. `PRIVILEGED_TOOLS` is the single source of truth passed to
 * `createSdkMcpServer` below; do not construct a second list.
 */
export const PRIVILEGED_TOOLS: Array<SdkMcpToolDefinition<any>> = [
  jiraTransitionTool,
  jiraCommentTool,
  jiraUpdateIssueTool,
  githubCreatePrTool,
  githubCommentTool,
];

export { jiraTransitionTool, jiraCommentTool, jiraUpdateIssueTool, githubCreatePrTool, githubCommentTool };

/**
 * Built fresh per call (mirrors `buildBotScopedMcpServers` in `agent.ts`) so
 * a token rotation takes effect on the next Telegram turn.
 *
 * `mcpServers` value returned here is spread into `agent.ts`'s
 * `Options.mcpServers` under the key `"ceo-privileged"`. Server name +
 * per-tool names combine into the SDK's qualified tool name
 * (`mcp__ceo-privileged__jira_transition`, etc.) — `agent.ts` never adds any
 * of them to `AUTO_ALLOW_MCP_WILDCARDS`/`AUTO_ALLOW_MCP_TOOLS`, which is what
 * makes every call here fall through `canUseTool` to the Telegram approval
 * branch (see the block comment at the top of this file).
 */
export function buildPrivilegedMcpServer(): McpServerConfig {
  return createSdkMcpServer({ name: "ceo-privileged", tools: PRIVILEGED_TOOLS });
}

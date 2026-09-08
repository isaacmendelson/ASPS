import { execFile } from "node:child_process";
import { z } from "zod";
import { createSdkMcpServer, tool } from "@anthropic-ai/claude-agent-sdk";
import type { McpServerConfig, SdkMcpToolDefinition } from "@anthropic-ai/claude-agent-sdk";
import type { CallToolResult } from "@modelcontextprotocol/sdk/types.js";
import { SHELL_METACHARACTER_PATTERN } from "./security.js";
import { resolveWorkingDir } from "./context.js";

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
 * `git_push` (ASPS-767, ADR-005 implementation-plan story ASPS-763-4) is the
 * SANCTIONED push path — see the block comment above `gitPushTool` below for
 * why it runs via `execFile` (no shell) and what it validates before ever
 * spawning `git`. It is on this same server, subject to the identical
 * gating requirement above: no wildcard, no `AUTO_ALLOW_MCP_TOOLS` entry.
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
 * Safe remote-name value: `origin`, `upstream`, etc. — never a URL (a URL
 * contains `:`/`/` beyond what this allows, and `://` fails outright).
 */
const SAFE_REMOTE_PATTERN = /^[A-Za-z0-9._-]+$/;

/**
 * Safe branch/refspec value: a plain ref/branch name (e.g. `main`,
 * `asps-767-git-push-tool`, `feature/x`). Deliberately the SAME shape as
 * `security.ts`'s `SAFE_REF_PATTERN` carve-out philosophy: no `:` (rules out
 * `<src>:<dst>` refspec forms — this tool only ever pushes a branch to the
 * identically-named remote ref, the simplest safe answer), no `~`/`^`
 * (relative-ref forms), no space (rules out a multi-token value smuggling in
 * a second arg like `branch --force`, which would otherwise still individually
 * match this char class since `-` is in it).
 */
const SAFE_BRANCH_PATTERN = /^[A-Za-z0-9._/-]+$/;

/**
 * `remote` is just as flag-injectable as `branch` (e.g. a remote value of
 * `-f`/`--force`/`--upload-pack=...` sitting in `git push <remote> <branch>`'s
 * positional remote slot would still be parsed by `git` as a flag, not a
 * remote name) — so it gets the identical leading-`-` rejection as
 * `validateBranch` below, not just the character-class allowlist (which
 * alone would let `-f` or `-x` through, since `-` is a legal character in a
 * remote name like `my-remote`).
 */
function validateRemote(remote: string): void {
  if (SHELL_METACHARACTER_PATTERN.test(remote)) {
    throw new Error(`Invalid git remote '${remote}' — contains a shell metacharacter`);
  }
  if (remote.startsWith("-")) {
    throw new Error(`Invalid git remote '${remote}' — must be a remote name, not a flag/option`);
  }
  if (!SAFE_REMOTE_PATTERN.test(remote)) {
    throw new Error(`Invalid git remote '${remote}' — expected ${SAFE_REMOTE_PATTERN}`);
  }
}

/**
 * Rejects any force-push form BEFORE the value ever reaches `execFile`'s
 * argv (ADR-005: "Force-push still hard-denied", matching the
 * `DANGEROUS_BASH_PATTERNS` policy in `security.ts` that hard-denies
 * `git push --force` at the Bash layer). A leading `-` catches `--force`,
 * `--force-with-lease`, `-f`, and every other flag-shaped value in one
 * check — deliberately broader than just the two named force flags, because
 * this tool takes a single ref VALUE, never an argument list: nothing
 * flag-shaped is ever a legitimate `branch` value. A leading `+` (git's
 * refspec force-push prefix, e.g. `+main`) is also rejected explicitly, and
 * is additionally outside `SAFE_BRANCH_PATTERN`'s character class. Embedded
 * whitespace is already outside `SAFE_BRANCH_PATTERN` (no space in the
 * allowed set), which is what stops a single-string smuggling attempt like
 * `"branch --force"`.
 */
function validateBranch(branch: string): void {
  if (SHELL_METACHARACTER_PATTERN.test(branch)) {
    throw new Error(`Invalid git branch/ref '${branch}' — contains a shell metacharacter`);
  }
  if (branch.startsWith("-")) {
    throw new Error(
      `Invalid git branch/ref '${branch}' — must be a ref value, not a flag/option (force-push is hard-denied)`,
    );
  }
  if (branch.startsWith("+")) {
    throw new Error(`Invalid git branch/ref '${branch}' — leading '+' forces the push, which is hard-denied`);
  }
  if (!SAFE_BRANCH_PATTERN.test(branch)) {
    throw new Error(`Invalid git branch/ref '${branch}' — expected ${SAFE_BRANCH_PATTERN}`);
  }
}

/**
 * `git_push` (ASPS-767, ADR-005 implementation-plan story ASPS-763-4) — the
 * SANCTIONED push path that replaces the now-dead ambient one: ASPS-765's
 * bubblewrap sandbox denies sandboxed Bash both the stored git-push
 * credential file (`credentials.files` in `agent.ts`'s
 * `buildSandboxSettings`) and every credential env var, so a sandboxed
 * `Bash` `git push` has no usable credential and fails. This handler runs in
 * the SDK host (unsandboxed `aspsbot` process, same as every other tool in
 * this file) where the stored credential IS reachable via git's own
 * `credential.helper = store` — this tool never reads or handles the
 * credential value itself, it just lets `git` invoke its already-configured
 * helper.
 *
 * Runs via `execFile` — NOT `exec`/`spawn` with `shell: true`, and NEVER
 * string-interpolates `remote`/`branch` into a shell command line. `execFile`
 * passes `command` + `args` straight to the OS's process-exec syscall; there
 * is no shell in between to reinterpret `;`, `&&`, backticks, `$()`, etc. —
 * this closes the injection vector even before `validateRemote`/
 * `validateBranch` run (defense-in-depth, not the only control).
 *
 * Gating: this tool is exported into `PRIVILEGED_TOOLS` like every other
 * tool in this file, so it inherits the SAME per-call Telegram-approval
 * requirement (see the block comment at the top of this file) — it is
 * deliberately NOT added to `agent.ts`'s `AUTO_ALLOW_MCP_WILDCARDS`/
 * `AUTO_ALLOW_MCP_TOOLS`. The operator sees the exact `remote`/`branch`
 * before approving (see `summarizeToolCall` in `agent.ts`, which
 * JSON-stringifies the full tool input for any tool not on its short-circuit
 * list).
 */
const gitPushTool = tool(
  "git_push",
  "Push a local branch to a remote (default 'origin'). SANCTIONED push path — runs host-side via execFile (no shell) using the stored git credential. WRITE operation — requires Telegram approval. Force-push is hard-denied; use only a plain branch/ref name, never flags.",
  {
    // `.optional()`, not `.default()`: the JS default parameter on the
    // handler below (`remote = "origin"`) is what actually applies the
    // default — `tool()`'s `handler` is also exported and invoked directly
    // in tests (see privileged.test.ts), bypassing the zod parse step that
    // `.default()` relies on, so the default must live where every caller
    // (real MCP dispatch AND a direct `.handler(...)` call) goes through it.
    remote: z.string().min(1).optional(),
    branch: z.string().min(1),
  },
  async ({ remote = "origin", branch }): Promise<CallToolResult> => {
    try {
      validateRemote(remote);
      validateBranch(branch);
      const workingDir = resolveWorkingDir();
      const { stdout, stderr } = await runGitPush(workingDir, remote, branch);
      const output = `${stdout}${stderr}`.trim();
      return textResult(`Pushed '${branch}' to '${remote}'.${output ? `\n${output}` : ""}`);
    } catch (err) {
      return errorResult(err);
    }
  },
);

/**
 * Thin Promise wrapper around `child_process.execFile` (not `util.promisify`
 * — kept as an explicit, easily-mockable function so tests can stub
 * `node:child_process`'s `execFile` directly and assert the exact
 * `command`/`args`/`options` vector passed, without fighting
 * `promisify`'s custom-symbol resolution). `shell: false` is `execFile`'s
 * own default (unlike `exec`, which always shells out) — set explicitly
 * here so the no-shell guarantee is visible at the call site, not just
 * implied by which function was chosen.
 */
function runGitPush(workingDir: string, remote: string, branch: string): Promise<{ stdout: string; stderr: string }> {
  return new Promise((resolve, reject) => {
    execFile(
      "git",
      ["-C", workingDir, "push", remote, branch],
      { shell: false },
      (error, stdout, stderr) => {
        if (error) {
          reject(error);
          return;
        }
        resolve({ stdout: stdout.toString(), stderr: stderr.toString() });
      },
    );
  });
}

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
  gitPushTool,
];

export {
  jiraTransitionTool,
  jiraCommentTool,
  jiraUpdateIssueTool,
  githubCreatePrTool,
  githubCommentTool,
  gitPushTool,
  validateRemote,
  validateBranch,
};

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

import { query } from "@anthropic-ai/claude-agent-sdk";
import type { CanUseTool, Options, SDKMessage } from "@anthropic-ai/claude-agent-sdk";
import { checkPathAllowed, matchDangerousBashCommand } from "./security.js";
import { requestApproval } from "./approvals.js";
import { getSessionId, setSessionId } from "./session.js";
import { TELEGRAM_SYSTEM_PROMPT_APPEND, loadClaudeMd, loadMcpServers } from "./context.js";

const DEFAULT_MAX_TURNS = 20; // Safety limit, mirrors the previous hand-rolled agentic loop.

/**
 * Tools that never touch the filesystem or mutate state — auto-allowed
 * without a human in the loop, per the "read-mostly" permission model
 * (ASPS-743 security remediation, decision #1). `Read`/`Grep`/`Glob` are
 * still routed through the path guard first (see `createCanUseTool` below);
 * the knowledge-engine MCP tools take no filesystem input so they skip it.
 */
const AUTO_ALLOW_READ_TOOLS = new Set(["Read", "Grep", "Glob"]);
const AUTO_ALLOW_MCP_TOOLS = ["mcp__knowledge-engine__knowledge_search", "mcp__knowledge-engine__knowledge_ask"];
const AUTO_ALLOW_MCP_TOOL_SET = new Set(AUTO_ALLOW_MCP_TOOLS);

/** Tool-input field name that carries a filesystem path, per tool. */
const PATH_INPUT_FIELD: Record<string, string> = {
  Read: "file_path",
  Edit: "file_path",
  Write: "file_path",
  NotebookEdit: "notebook_path",
  Grep: "path",
  Glob: "path",
};

function extractPath(toolName: string, input: Record<string, unknown>): string | undefined {
  const field = PATH_INPUT_FIELD[toolName];
  if (!field) return undefined;
  const value = input[field];
  return typeof value === "string" && value.length > 0 ? value : undefined;
}

function truncate(text: string, max = 300): string {
  return text.length > max ? `${text.slice(0, max)}…` : text;
}

/** Truncated, safe-to-render summary of a tool call for the Telegram approval prompt. */
function summarizeToolCall(toolName: string, input: Record<string, unknown>): string {
  if (toolName === "Bash" && typeof input.command === "string") {
    return truncate(input.command);
  }
  const targetPath = extractPath(toolName, input);
  if (targetPath) return truncate(targetPath);
  try {
    return truncate(JSON.stringify(input));
  } catch {
    return "(unrenderable input)";
  }
}

/**
 * Deny-by-default permission policy (ASPS-743 security remediation,
 * blockers B1–B3). Built per Telegram turn so the approval flow can
 * correlate every request with the user who owns it.
 *
 * Evaluation order for each tool call:
 *  1. **Path guard (B1)** — any tool whose input carries a filesystem path
 *     is checked with `checkPathAllowed`; a path outside `workingDir` or
 *     matching a secret pattern is denied outright, before anything else,
 *     including for tools that would otherwise auto-allow.
 *  2. **Bash hard-deny (B2)** — a command matching
 *     `DANGEROUS_BASH_PATTERNS` is denied unconditionally. This is
 *     defense-in-depth, not the primary control: irreversible ops are
 *     never one-tap-approvable from a phone, so they never even reach the
 *     approval step.
 *  3. **Auto-allow (subject to #1)** — `Read`/`Grep`/`Glob` and the two
 *     read-only knowledge-engine MCP tools proceed without a human in the
 *     loop, per decision #1 ("read-mostly").
 *  4. **Require Telegram approval** — everything else (`Write`, `Edit`,
 *     `NotebookEdit`, non-dangerous `Bash`, `Task`, `WebFetch`, any other
 *     MCP tool, etc.) is deny-by-default until the same authorized user who
 *     owns this turn approves it over Telegram (`requestApproval`).
 *
 * Must never resolve to `null` — the SDK's own docs state an accidental
 * `null` leaves the permission request unanswered and the tool call
 * blocked indefinitely.
 */
export function createCanUseTool(userId: number, workingDir: string): CanUseTool {
  return async (toolName, input) => {
    const targetPath = extractPath(toolName, input);
    if (targetPath !== undefined) {
      const guard = checkPathAllowed(targetPath, workingDir);
      if (!guard.allowed) {
        return { behavior: "deny", message: `Blocked by path guard: ${guard.reason}` };
      }
    }

    if (toolName === "Bash" && typeof input.command === "string") {
      const matched = matchDangerousBashCommand(input.command);
      if (matched) {
        return {
          behavior: "deny",
          message:
            `Blocked: command matches a destructive pattern (${matched.source}). ` +
            "This is a hard deny — irreversible operations are never approved via Telegram.",
        };
      }
    }

    if (AUTO_ALLOW_READ_TOOLS.has(toolName) || AUTO_ALLOW_MCP_TOOL_SET.has(toolName)) {
      return { behavior: "allow" };
    }

    const decision = await requestApproval(userId, toolName, summarizeToolCall(toolName, input));
    if (decision === "allow") {
      return { behavior: "allow" };
    }
    return {
      behavior: "deny",
      message: `Denied: no Telegram approval received for ${toolName} (denied or timed out).`,
    };
  };
}

function buildOptions(userId: number): Options {
  const workingDir = process.env.WORKING_DIR || process.cwd();
  const model = process.env.MODEL;
  const maxTurns = Number(process.env.MAX_TURNS) || DEFAULT_MAX_TURNS;
  const resume = getSessionId(userId);
  const claudeMd = loadClaudeMd(workingDir);

  return {
    cwd: workingDir,
    ...(model ? { model } : {}),
    maxTurns,
    permissionMode: "default",
    canUseTool: createCanUseTool(userId, workingDir),
    // Deliberately empty, NOT ["project"]. Loading the "project" settings
    // source also loads .claude/settings.json's `permissions.allow`
    // (Bash(*), Write, Edit, Read, ...) — confirmed empirically via the
    // SDK's own `resolveSettings()` API that this file's rules are merged
    // into the effective permission set. A matching `permissions.allow`
    // rule is resolved by the SDK's permission engine WITHOUT ever calling
    // `canUseTool` (same mechanism as the `allowedTools` option below,
    // whose own doc says matching tools "execute automatically without
    // asking the user for approval"). That would silently re-open every
    // tool this remediation just locked down. Loading zero filesystem
    // settings sources makes `canUseTool` the sole, unconditional
    // authority for every tool call. See the ASPS-743 handoff for the full
    // investigation and the resolveSettings() evidence.
    settingSources: [],
    // .mcp.json is normally auto-discovered via settingSources: ["project"];
    // wired by hand instead (see context.ts) so MCP still works under
    // isolation mode. strictMcpConfig prevents any other on-disk source
    // (plugins, user settings, agent frontmatter) from smuggling in an
    // MCP server we didn't explicitly approve.
    mcpServers: loadMcpServers(workingDir),
    strictMcpConfig: true,
    // Auto-allow ONLY the two read-only knowledge-engine MCP tools at the
    // SDK level (this also bypasses canUseTool, same mechanism as settings
    // permissions.allow — safe here because these tools take no filesystem
    // path and are genuinely read-only). Read/Grep/Glob are NOT listed here
    // on purpose: they still go through canUseTool so the path guard runs.
    allowedTools: AUTO_ALLOW_MCP_TOOLS,
    systemPrompt: {
      type: "preset",
      preset: "claude_code",
      append: [claudeMd, TELEGRAM_SYSTEM_PROMPT_APPEND].filter((part): part is string => Boolean(part)).join("\n\n"),
    },
    ...(resume ? { resume } : {}),
  };
}

/**
 * Run one Telegram user turn through the Claude Agent SDK.
 *
 * Streams every `SDKMessage` (system/assistant/tool/result) to `onEvent` —
 * the caller uses this to drive the Telegram typing indicator while the
 * agent works. Persists the SDK session id returned in the `result` message
 * per Telegram user so the next call from the same user resumes the same
 * multi-turn conversation via `resume`.
 */
export async function runAgent(
  userId: number,
  userMessage: string,
  onEvent?: (message: SDKMessage) => void,
): Promise<string> {
  const options = buildOptions(userId);

  let finalText = "";

  for await (const message of query({ prompt: userMessage, options })) {
    onEvent?.(message);

    if (message.type === "result") {
      setSessionId(userId, message.session_id);
      if (message.subtype === "success") {
        finalText = message.result;
      } else {
        const detail = message.errors.length ? ` ${message.errors.join(" ")}` : "";
        finalText = `Agent stopped (${message.subtype}).${detail}`;
      }
    }
  }

  return finalText || "(no response)";
}

/**
 * Kept for `/reload` command compatibility. `CLAUDE.md` is now re-read from
 * disk on every turn by `buildOptions` (see `loadClaudeMd`), so there is no
 * in-process cache left to invalidate — reload is inherent.
 */
export function reloadSystemPrompt(): void {
  // No-op: buildOptions() re-reads CLAUDE.md from disk on every turn.
}

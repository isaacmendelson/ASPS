import { query } from "@anthropic-ai/claude-agent-sdk";
import type { CanUseTool, Options, SDKMessage } from "@anthropic-ai/claude-agent-sdk";
import { matchDangerousBashCommand } from "./security.js";
import { getSessionId, setSessionId } from "./session.js";
import { TELEGRAM_SYSTEM_PROMPT_APPEND } from "./context.js";

const DEFAULT_MAX_TURNS = 20; // Safety limit, mirrors the previous hand-rolled agentic loop.

/**
 * Permission guard mirroring the project's destructive-command denylist
 * (see security.ts — single source of truth, do not duplicate the pattern
 * list). Bash commands matching a dangerous pattern are denied; every other
 * tool call (including native Read/Edit/Write/Grep/Glob and the allow-listed
 * MCP tools) is auto-allowed so the bot can operate autonomously over
 * Telegram without a human present to answer a permission prompt.
 *
 * Must never resolve to `null` — an accidental null leaves the SDK's
 * permission request unanswered and the tool call blocked indefinitely.
 */
export const canUseTool: CanUseTool = async (toolName, input) => {
  if (toolName === "Bash" && typeof input.command === "string") {
    const matched = matchDangerousBashCommand(input.command);
    if (matched) {
      return {
        behavior: "deny",
        message: `Blocked: command matches a destructive pattern (${matched.source}). Destructive operations are not auto-executed via Telegram.`,
      };
    }
  }
  return { behavior: "allow" };
};

function buildOptions(userId: number): Options {
  const workingDir = process.env.WORKING_DIR || process.cwd();
  const model = process.env.MODEL;
  const maxTurns = Number(process.env.MAX_TURNS) || DEFAULT_MAX_TURNS;
  const resume = getSessionId(userId);

  return {
    cwd: workingDir,
    ...(model ? { model } : {}),
    maxTurns,
    permissionMode: "default",
    canUseTool,
    // "project" loads CLAUDE.md + .mcp.json + .claude/settings.json from cwd,
    // so the agent onboards itself exactly like an interactive Claude Code
    // session (see CLAUDE.md's own "at session start" instructions).
    settingSources: ["project"],
    // Auto-allow the knowledge-engine MCP tools so Telegram turns don't stall
    // on a permission prompt nobody is present to answer.
    allowedTools: [
      "mcp__knowledge-engine__knowledge_search",
      "mcp__knowledge-engine__knowledge_ask",
    ],
    systemPrompt: {
      type: "preset",
      preset: "claude_code",
      append: TELEGRAM_SYSTEM_PROMPT_APPEND,
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
 * Kept for `/reload` command compatibility. The Agent SDK spawns a fresh
 * Claude Code process per turn and reads CLAUDE.md / project settings from
 * disk every time (`settingSources: ["project"]`), so there is no
 * in-process system-prompt cache left to invalidate — reload is inherent.
 */
export function reloadSystemPrompt(): void {
  // No-op: settingSources: ["project"] re-reads CLAUDE.md on every turn.
}

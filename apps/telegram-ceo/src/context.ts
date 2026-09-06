import { existsSync, readFileSync } from "node:fs";
import path from "node:path";

/**
 * Telegram-transport-specific system-prompt addendum.
 *
 * Project persona, priorities, and communication style come from CLAUDE.md
 * (see `loadClaudeMd` below), which the agent follows itself — including its
 * own "at session start" instructions to read the rest of the hat chain via
 * its own Read tool. This constant only adds the handful of instructions
 * specific to being driven over a Telegram bot bridge instead of a
 * terminal.
 */
export const TELEGRAM_SYSTEM_PROMPT_APPEND = [
  "You are being operated as the ASPS CEO agent through a Telegram bot bridge, not an interactive terminal.",
  "Telegram messages are capped at 4096 characters — the bridge splits longer replies automatically, but prefer being concise.",
  "Use only Telegram-flavored Markdown (bold, italics, code spans, links) sparingly; avoid tables and deeply nested formatting Telegram cannot render.",
  "There is no human watching a live terminal on the other end of a tool call — do not block waiting for an in-band interactive answer; make the best reasonable call, note the assumption, and report what you did in your reply.",
  "Every state-changing tool call (Write, Edit, NotebookEdit, Bash other than the always-blocked destructive patterns, and anything else not explicitly read-only) will pause and wait for the authorized user to tap Approve or Deny in Telegram before it runs — this is expected, not an error; explain what you are about to do so the approval prompt is easy to judge.",
].join("\n");

/**
 * Read `CLAUDE.md` from the working directory, or `undefined` if it doesn't
 * exist.
 *
 * The Claude Agent SDK's `settingSources: ["project"]` option would
 * normally auto-load `CLAUDE.md` — but it also auto-loads
 * `.claude/settings.json`'s `permissions.allow` list (`Bash(*)`, `Write`,
 * `Edit`, ...), which the SDK's permission engine treats as pre-approved
 * and therefore never routes through `canUseTool` (see the security
 * remediation notes in `agent.ts` and the task handoff). Reading CLAUDE.md
 * by hand keeps the bot self-onboarding via CLAUDE.md's own "at session
 * start" instructions without inheriting that settings file's permissive
 * tool rules. Read fresh on every turn (not cached) so edits to CLAUDE.md
 * take effect on the next Telegram message, same as the old `/reload`
 * semantics.
 */
export function loadClaudeMd(workingDir: string): string | undefined {
  const claudeMdPath = path.join(workingDir, "CLAUDE.md");
  if (!existsSync(claudeMdPath)) return undefined;
  try {
    return readFileSync(claudeMdPath, "utf-8");
  } catch {
    return undefined;
  }
}

export interface McpServerFileConfig {
  command: string;
  args?: string[];
  env?: Record<string, string>;
}

/**
 * Read `.mcp.json` from the working directory and return its `mcpServers`
 * map, or `{}` if the file doesn't exist or fails to parse.
 *
 * Read by hand for the same reason as `loadClaudeMd`: the bot no longer
 * passes `settingSources: ["project"]` (SDK isolation mode instead — see
 * `agent.ts`), so `.mcp.json` is no longer auto-discovered. Passed to the
 * SDK explicitly via `Options.mcpServers` alongside `strictMcpConfig: true`.
 */
export function loadMcpServers(workingDir: string): Record<string, McpServerFileConfig> {
  const mcpJsonPath = path.join(workingDir, ".mcp.json");
  if (!existsSync(mcpJsonPath)) return {};
  try {
    const parsed: unknown = JSON.parse(readFileSync(mcpJsonPath, "utf-8"));
    const servers = (parsed as { mcpServers?: unknown })?.mcpServers;
    return servers && typeof servers === "object" ? (servers as Record<string, McpServerFileConfig>) : {};
  } catch {
    return {};
  }
}

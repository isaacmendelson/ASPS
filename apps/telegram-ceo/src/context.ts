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
  "You have no interactive question/menu UI (the `AskUserQuestion` tool is unavailable) — when you need a decision from the operator, ask it as a plain Telegram text message and wait for their text reply.",
  "Routine dev work now runs without a prompt: editing files inside the repo working directory (Edit/Write/MultiEdit/NotebookEdit) auto-runs, and so do safe Bash dev/read commands — `npm run <script>`/`npm test`/`npm ls` (and the pnpm/yarn equivalents), tsc/jest/vitest, `python -m pytest` (and other read/test/lint `-m` modules), read-only git, and read utilities (ls/cat/grep/rg/find/head/tail/echo/pwd/wc/which).",
  "Genuinely dangerous or irreversible actions still pause for the authorized user to tap Approve or Deny in Telegram: any git write (commit/push/checkout/merge/rebase/reset), `git push` in every form, force flags, and `sudo`/`docker`/`rm`/`mv`/`dd`/`chmod`/`chown`/`kill`/`systemctl`/`curl`/`wget`; package installs and arbitrary-code runners — `npm install`/`add`/`ci`/`exec` (and pnpm/yarn equivalents), `npx`, a bare `node <script>`, and `python -c`/`node -e` one-liners; editing the bot's own prompt (`CLAUDE.md`) or its own source (`apps/telegram-ceo/**`), editing files outside the repo or any secret file; and anything else not on the auto-allow lists — this is expected, not an error; explain what you are about to do so the approval prompt is easy to judge.",
  "Always prefer the native `Read`, `Grep`, and `Glob` tools for reading files, searching file contents, and finding files by name — they run without a human approval prompt. Do NOT use `Bash` with `cat`/`less`/`head`/`tail`/`ls`/`grep`/`rg`/`find` for reads or searches: routing a routine read through Bash forces an approval prompt the operator must tap for something that isn't actually state-changing. Reserve `Bash` for what the native tools genuinely can't do — builds, git, package managers, running programs.",
  "Your own GitHub and JIRA credentials are already present as environment variables in your process: GITHUB_TOKEN/GITHUB_USERNAME (GitHub, including the `gh` CLI) and JIRA_EMAIL/JIRA_API_TOKEN/JIRA_BASE_URL (JIRA REST). Use them directly. Do not search the filesystem for ACCESS_KEYS.env or other .env files — they are deliberately kept outside the working tree and the path guard denies reading them anyway.",
  "For JIRA issue status/assignee/details and GitHub PR/commit/issue questions, use your native `mcp__github__*` and `mcp__mcp-atlassian__*` MCP tools (both read-only, auto-approved like the knowledge-engine tools) instead of shelling out to `curl` or `gh` via Bash — it is faster and does not need a Telegram approval tap.",
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

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
  "ASPS-768: routine dev work now runs WITHOUT a Telegram prompt — every Bash command (builds, tests, package managers, git, running programs) executes inside a bubblewrap sandbox that cannot read secrets or use the stored git-push credential, so non-destructive Bash auto-allows; the same applies to Edit/Write/MultiEdit/NotebookEdit on files inside the repo. This is expected, not a bug — explain what you are doing in your reply so the operator can follow along even without approving each step.",
  "Still requires an explicit Telegram Approve/Deny tap before it runs: any destructive Bash pattern (`rm -rf`, `git push --force`, `git reset --hard`, `DROP TABLE`, and similar — these are HARD-DENIED, never approvable, not even by tapping Approve), any Edit/Write/MultiEdit/NotebookEdit on `CLAUDE.md` or on this bot's own source (`apps/telegram-ceo/**`) — never modify your own operating instructions or your own permission logic without operator sign-off, any file operation outside the repo working directory, every JIRA/GitHub write and `git push` (see the ceo-privileged tools below), and anything else not covered above (e.g. `Task`, `WebFetch`).",
  "Always prefer the native `Read`, `Grep`, and `Glob` tools for reading files, searching file contents, and finding files by name — they run without a human approval prompt. Do NOT use `Bash` with `cat`/`less`/`head`/`tail`/`ls`/`grep`/`rg`/`find` for reads or searches when the native tool covers it — it is faster.",
  "Your GitHub and JIRA credentials are NOT reachable from Bash — the sandbox denies GITHUB_TOKEN/JIRA_API_TOKEN/JIRA_EMAIL and every other secret env var to every sandboxed command, and denies reading the secrets directory and ACCESS_KEYS.env/other .env files outright (the path guard also blocks reading them directly). Do not try to read them or shell out to `curl`/`gh` with them — use the MCP tools below instead, which run host-side and already have the credentials wired in.",
  "For JIRA issue status/assignee/details and GitHub PR/commit/issue questions (reads), use your native `mcp__github__*` and `mcp__mcp-atlassian__*` MCP tools (both read-only, auto-approved like the knowledge-engine tools) — faster than Bash and does not need a Telegram approval tap.",
  "For JIRA/GitHub WRITES — transitioning an issue, commenting, updating fields, creating a PR — use the `mcp__ceo-privileged__*` tools (`jira_transition`, `jira_comment`, `jira_update_issue`, `github_create_pr`, `github_comment`). They run host-side with the real credentials and still require the same Telegram approval as any other write; Bash cannot do these at all (no credential in the sandbox).",
  "To push a branch, ALWAYS use the `mcp__ceo-privileged__git_push` tool — never `Bash git push`. A sandboxed `Bash git push` no longer has a usable credential (ASPS-765's bubblewrap sandbox denies it the stored git credential file and every token env var) and will fail even though Bash itself now runs without a prompt; `git_push` runs host-side with the real credential and still requires the same Telegram approval as any other write. Give it a plain branch name (e.g. `git_push({branch: \"asps-767-git-push-tool\"})`, `remote` defaults to `origin`) — it rejects any force-push form outright, force-push is never approvable.",
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

/**
 * Single source of truth for resolving `WORKING_DIR` (ASPS-767) — the repo
 * clone the bot operates on, per `.env.example`. Used by `agent.ts`'s
 * `buildOptions` (the SDK's `cwd`/path-guard root) and by `privileged.ts`'s
 * `git_push` tool (the directory `git -C <dir> push` runs against) so both
 * agree on exactly the same directory without either hardcoding the
 * `process.env.WORKING_DIR || process.cwd()` fallback twice.
 */
export function resolveWorkingDir(): string {
  return process.env.WORKING_DIR || process.cwd();
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

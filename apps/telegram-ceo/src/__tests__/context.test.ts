import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { TELEGRAM_SYSTEM_PROMPT_APPEND, resolveWorkingDir } from "../context.js";

describe("TELEGRAM_SYSTEM_PROMPT_APPEND", () => {
  it("instructs the agent to prefer Read/Grep/Glob over Bash for reads and searches (ASPS-747)", () => {
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND).toMatch(/\bRead\b/);
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND).toMatch(/\bGrep\b/);
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND).toMatch(/\bGlob\b/);
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND.toLowerCase()).toContain("without a human approval prompt".toLowerCase());
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND).toMatch(/do not use `?Bash`?.*(cat|ls|grep|find|head|tail)/is);
  });

  it("tells the agent its GitHub/JIRA credentials are NOT reachable from Bash post-ASPS-768 — corrects the pre-ASPS-768 false claim that they are 'already present as environment variables in your process' for direct use", () => {
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND).toContain("GITHUB_TOKEN");
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND).toContain("JIRA_API_TOKEN");
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND).toMatch(/ACCESS_KEYS\.env/);
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND.toLowerCase()).toContain("not reachable from bash");
    // The old instruction told the agent to "use them directly" from Bash —
    // that must no longer appear now that the sandbox denies every one of
    // these env vars to sandboxed commands (ASPS-765/768).
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND).not.toMatch(/use them directly/i);
  });

  it("instructs JIRA/GitHub WRITES through the gated mcp__ceo-privileged__* tools, not Bash (ASPS-768 — Bash has no credential in the sandbox)", () => {
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND).toContain("mcp__ceo-privileged__");
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND).toContain("jira_transition");
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND).toContain("github_create_pr");
  });

  it("tells the agent routine Bash + in-repo edits now run without a Telegram prompt (ASPS-768), while still naming what stays gated (destructive patterns, self-modification, out-of-repo writes, JIRA/GitHub writes, git push)", () => {
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND).toMatch(/without a Telegram prompt/i);
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND).toContain("CLAUDE.md");
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND).toContain("apps/telegram-ceo");
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND.toLowerCase()).toContain("own operating instructions");
  });

  it("still states that state-changing tool calls pause for operator approval (unchanged expectation)", () => {
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND.toLowerCase()).toContain("approve");
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND).toMatch(/Write|Edit/);
  });

  it("tells the agent it has no AskUserQuestion / interactive menu UI and must ask decisions as plain Telegram text (ASPS-754)", () => {
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND).toContain("AskUserQuestion");
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND.toLowerCase()).toMatch(/plain telegram text message/);
  });

  it("instructs the agent to push via mcp__ceo-privileged__git_push, not Bash git push (ASPS-767 — ambient Bash push is dead post-ASPS-765)", () => {
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND).toContain("mcp__ceo-privileged__git_push");
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND).toMatch(/never `?Bash git push`?/);
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND.toLowerCase()).toContain("no longer has a usable credential");
  });
});

describe("resolveWorkingDir (ASPS-767 — single source of truth for WORKING_DIR resolution)", () => {
  const saved = { WORKING_DIR: process.env.WORKING_DIR };

  beforeEach(() => {
    delete process.env.WORKING_DIR;
  });

  afterEach(() => {
    if (saved.WORKING_DIR === undefined) delete process.env.WORKING_DIR;
    else process.env.WORKING_DIR = saved.WORKING_DIR;
  });

  it("falls back to process.cwd() when WORKING_DIR is unset", () => {
    expect(resolveWorkingDir()).toBe(process.cwd());
  });

  it("honors WORKING_DIR when set", () => {
    process.env.WORKING_DIR = "/home/aspsbot/ASPS";
    expect(resolveWorkingDir()).toBe("/home/aspsbot/ASPS");
  });
});

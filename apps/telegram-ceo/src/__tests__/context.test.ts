import { describe, expect, it } from "vitest";
import { TELEGRAM_SYSTEM_PROMPT_APPEND } from "../context.js";

describe("TELEGRAM_SYSTEM_PROMPT_APPEND", () => {
  it("instructs the agent to prefer Read/Grep/Glob over Bash for reads and searches (ASPS-747)", () => {
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND).toMatch(/\bRead\b/);
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND).toMatch(/\bGrep\b/);
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND).toMatch(/\bGlob\b/);
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND.toLowerCase()).toContain("without a human approval prompt".toLowerCase());
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND).toMatch(/do not use `?Bash`?.*(cat|ls|grep|find|head|tail)/is);
  });

  it("tells the agent its GitHub/JIRA credentials are already in the environment, so it must not hunt the filesystem for them (ASPS-747)", () => {
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND).toContain("GITHUB_TOKEN");
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND).toContain("JIRA_API_TOKEN");
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND).toMatch(/ACCESS_KEYS\.env/);
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND.toLowerCase()).toContain("do not search the filesystem");
  });

  it("still states that state-changing tool calls pause for operator approval (unchanged expectation)", () => {
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND.toLowerCase()).toContain("approve");
    expect(TELEGRAM_SYSTEM_PROMPT_APPEND).toMatch(/Write|Edit/);
  });
});

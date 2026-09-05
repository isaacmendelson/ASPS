import { describe, expect, it } from "vitest";
import { matchDangerousBashCommand } from "../security.js";

describe("matchDangerousBashCommand", () => {
  it.each([
    "rm -rf /home/aspsbot/ASPS",
    "rm -r ./dist",
    "rm --force ./dist",
    "Remove-Item -Recurse -Force C:\\Jobs",
    "del /s /q C:\\Jobs\\ASPS",
    "format C:",
    "DROP TABLE Users",
    "drop database ASPSBackend2DB",
    "git reset --hard origin/main",
    "git push --force origin main",
    "git clean -fdx",
  ])("denies destructive command: %s", (command) => {
    expect(matchDangerousBashCommand(command)).toBeInstanceOf(RegExp);
  });

  it.each([
    "git status",
    "npm run build",
    "dotnet build ASPSBackend.sln -c Debug",
    "ls -la",
    "git log --oneline -5",
    "git push origin feature-branch",
  ])("allows benign command: %s", (command) => {
    expect(matchDangerousBashCommand(command)).toBeUndefined();
  });
});

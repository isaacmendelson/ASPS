import { realpathSync } from "node:fs";
import path from "node:path";

/**
 * Single source of truth for the destructive-command denylist.
 *
 * Used by the Claude Agent SDK `canUseTool` permission hook (see agent.ts)
 * to hard-deny Bash commands that would cause irreversible damage (data
 * loss, force-pushing over history, dropping databases, etc.) — even when
 * the caller would otherwise be granted Telegram approval. Irreversible ops
 * are never one-tap from a phone. Do not duplicate this list elsewhere —
 * import from here.
 */
export const DANGEROUS_BASH_PATTERNS: RegExp[] = [
  /\brm\s+(-rf?|--force|--recursive)\b/i,
  /\bRemove-Item\s+.*-Recurse/i,
  /\bdel\s+\/[sfq]/i,
  /\bformat\b/i,
  /\bDROP\s+(TABLE|DATABASE)\b/i,
  /\bgit\s+(reset\s+--hard|push\s+--force|clean\s+-f)/i,
];

/**
 * Returns the first dangerous pattern that matches `command`, or `undefined`
 * if the command is not on the denylist.
 */
export function matchDangerousBashCommand(command: string): RegExp | undefined {
  return DANGEROUS_BASH_PATTERNS.find((pattern) => pattern.test(command));
}

/**
 * Strict read-only `git` allowlist (ASPS-749, redesigned after PoC-confirmed
 * bypasses in the original subcommand-allowlist + flag-DENYlist design).
 *
 * `createCanUseTool` (agent.ts) auto-allows a Bash call ONLY when
 * `isSafeReadOnlyGitCommand` returns true, to cut Telegram approval friction
 * for routine git plumbing (status/log/diff/...) without weakening the
 * ASPS-743 deny-by-default model. This is a narrow carve-out under `Bash`,
 * not a new tool — everything not on this allowlist (including every git
 * WRITE: commit/push/checkout/merge/rebase/reset/`branch -D`/`remote add`/
 * `config user.name`, ...) still falls through to the existing approval
 * flow, and `matchDangerousBashCommand` above still hard-denies destructive
 * patterns regardless of this allowlist (evaluated first in `agent.ts`).
 *
 * DESIGN: PER-SUBCOMMAND POSITIVE FLAG ALLOWLIST, NOT A FLAG DENYLIST.
 *
 * A subcommand-allowlist + flag-DENYlist is inverted for safety: `git`'s
 * read subcommands expose a large, growing surface of flags that read
 * arbitrary files, hit the network, or exec an external program, and a
 * denylist can never enumerate all of them. The original design was broken
 * by (all closed by this rewrite):
 *
 *  - `git diff --no-index <anyfile> /dev/null` — reads any file, bypassing
 *    the flag denylist entirely (not a denied flag) and the trailing-
 *    anchored secret-path scan (an arbitrary *argument*, not a tool path
 *    field). Closed: `diff`'s safe-flag set has no `--no-index`, and no
 *    non-ref, non-flag token is accepted at all (see `isSafeDiffArgs`).
 *  - `git blame --contents <anyfile> -- <tracked>` — reads any file.
 *    Closed: `blame` is dropped from the allowlist entirely.
 *  - `git config --list --file <anyfile>` / `-f <anyfile>` — reads any
 *    file. Closed: `config` is dropped from the allowlist entirely.
 *  - `git ls-remote <url>` / `git remote show <url|origin>` — network I/O
 *    (SSRF/exfil); `git ls-remote ext::<cmd>` is conditional RCE. Closed:
 *    `ls-remote` and `remote show` are dropped; `remote` only allows bare,
 *    `-v`, or `get-url <name>` with a safe name pattern.
 *  - `git -C <other-dir> ...` — operates outside the working directory.
 *    Closed: `-C` support is dropped entirely; the command must start with
 *    exactly `git ` (the bot always runs in its own `cwd`).
 *  - Attached short-flag forms like `-O<path>` evaded exact-match flag
 *    denies. Closed: flags are matched by an exact positive allowlist per
 *    subcommand, not a denylist, so an unrecognized attached form is
 *    rejected by omission rather than needing its own deny pattern.
 *  - Glob/tilde (`*`, `?`, `[]`, `~`) are shell-expanded and were not
 *    rejected. Closed: rejected on every token (see `hasUnsafeToken`).
 *
 * Prefer DROPPING a risky subcommand over trying to make it safe — minimal
 * surface is the goal. Dropped in this redesign: `config`, `blame`,
 * `ls-remote`, `shortlog` (reads stdin), `remote show`.
 *
 * Two more classes this function must still defend against (unchanged from
 * the original design, kept as defense-in-depth):
 *
 *  (a) Shell metacharacters let one command chain into another (`;`, `&&`,
 *      `|`, backticks/`$(...)` substitution, `(...)`/`{...}` subshells,
 *      `<`/`>` redirection, `\` line continuation, embedded newlines).
 *  (b) `git` itself can be made to execute an external program or override
 *      trusted config via certain flags (`-c`/`--config`, `-o`/`--output`/
 *      `-O`/`--pager`/`--open-files-in-pager`, `--ext-diff`,
 *      `--upload-pack`/`--receive-pack`/`--exec`/`--exec-path`, interactive
 *      flags) — now redundant with the positive per-subcommand allowlist
 *      below (none of these flags appear on any safe-flag set) but kept as
 *      a second, independent layer so a bug in one subcommand's safe-flag
 *      set does not silently reopen one of these.
 *
 * Any doubt resolves to `false` (stays approval-gated) — this function must
 * be fail-safe, not merely fail-closed on the common case.
 */

/**
 * Rule 1: reject if ANY of these appear anywhere in the raw command —
 * semicolon, ampersand, pipe, backtick, dollar-sign, parentheses, braces,
 * angle brackets, backslash, or any control character (including newline
 * `\n` and carriage return `\r`). This forbids chaining, redirection,
 * command substitution, subshells/backgrounding, and multi-line payloads,
 * so a safe command must be a single standalone line.
 */
const SHELL_METACHARACTER_PATTERN = /[;&|`$(){}<>\\]|[\x00-\x1f]/;

/**
 * Rule 1b (ASPS-749 remediation): glob metacharacters are shell-expanded by
 * the caller's shell before `git` ever sees them, so a value that looks
 * innocuous here (`git log v1.*`) can expand to something else entirely at
 * execution time. Reject `*`, `?`, `[`, `]` anywhere in any token.
 */
const GLOB_CHARACTER_PATTERN = /[*?[\]]/;

/**
 * Rule 1c: a token containing `::` (git's "remote helper" transport syntax,
 * e.g. `ext::sh -c ...`) or a URL/transport scheme is never a safe ref/name
 * value — reject outright rather than trying to allowlist safe schemes.
 */
const SCHEME_TOKEN_PATTERN = /^[a-z][a-z0-9+.-]*:\/\//i;
const TRANSPORT_PREFIX_PATTERN = /^(ext|file|ssh|git):/i;

/**
 * Returns true if `token` contains a glob/tilde/scheme/`::` construct that
 * must reject the whole command regardless of which subcommand it is an
 * argument to (rule 1b/1c). Applied to every token, including `git` and the
 * subcommand itself (harmless there, but keeps this a single blanket pass).
 */
function hasUnsafeToken(tokens: readonly string[]): boolean {
  return tokens.some((token) => {
    if (GLOB_CHARACTER_PATTERN.test(token)) return true;
    if (token.startsWith("~")) return true; // leading tilde — home-dir expansion
    if (token.includes("::")) return true; // remote-helper transport syntax
    if (SCHEME_TOKEN_PATTERN.test(token)) return true; // https://, ssh://, ...
    if (TRANSPORT_PREFIX_PATTERN.test(token)) return true; // ext:, file:, ssh:, git:
    return false;
  });
}

/**
 * Safe value-token pattern for a ref/branch/tag/remote-name argument (e.g.
 * `HEAD`, `main`, `origin/main`, `v1.0.0`, `HEAD^`, `abc1234`). Deliberately
 * excludes:
 *  - a leading `/` or `-` (first character must be alphanumeric) — rules
 *    out an absolute path and rules out any flag-shaped value being
 *    smuggled in as a "ref".
 *  - `:` — rules out `<ref>:<path>` show/log forms entirely (loses
 *    `git show HEAD:file`, which is an intentional, accepted trade-off —
 *    simplest safe answer per the ASPS-749 remediation).
 *  - `~` — rules out `HEAD~1`-style relative refs (an intentional, accepted
 *    trade-off — simplest safe answer per the ASPS-749 remediation).
 *  - `..` anywhere in the token — rules out path-traversal segments (e.g.
 *    `../../etc/passwd`) even though the leading-`/`/first-char rule above
 *    already blocks a rooted absolute path.
 */
const SAFE_REF_PATTERN = /^[A-Za-z0-9][A-Za-z0-9._/@^-]{0,100}$/;

function isSafeRefToken(token: string): boolean {
  if (!SAFE_REF_PATTERN.test(token)) return false;
  if (token.includes("..")) return false;
  return true;
}

/** `-n`/`--max-count` numeric value, and the `-<digits>` short form. */
const DIGITS_PATTERN = /^\d{1,6}$/;
const SHORT_COUNT_FLAG_PATTERN = /^-\d{1,6}$/;
const MAX_COUNT_EQUALS_PATTERN = /^--max-count=\d{1,6}$/;

/**
 * Rule 3: strict READ-ONLY git subcommand allowlist. Single source of
 * truth — do not duplicate elsewhere. `blame`, `config`, `ls-remote`, and
 * `shortlog` are deliberately NOT on this list (see the block comment
 * above) — every read-form of those either reads an arbitrary file or does
 * network I/O, and there is no safe subset worth carving out.
 */
export const READ_ONLY_GIT_SUBCOMMANDS: ReadonlySet<string> = new Set([
  "status",
  "log",
  "show",
  "diff",
  "branch",
  "remote",
  "rev-parse",
  "describe",
  "ls-files",
  "tag",
]);

/** `status` — no path arguments accepted, only these report-shape flags. */
const GIT_STATUS_SAFE_FLAGS: ReadonlySet<string> = new Set([
  "-s",
  "--short",
  "-b",
  "--branch",
  "--porcelain",
]);

/**
 * `log` — report-shape flags, an optional bounded count (`-n <N>` /
 * `--max-count=<N>` / `-<N>`), and at most one trailing safe ref token.
 * Deliberately excludes `-L`, `-O`, `--output`, `--format`, `--pretty`,
 * `-G`/`-S` — any of these can read/execute arbitrary content or are
 * rejected simply by not being on this list.
 */
const GIT_LOG_SAFE_FLAGS: ReadonlySet<string> = new Set([
  "--oneline",
  "--stat",
  "--graph",
  "--decorate",
]);

/**
 * `diff` — deliberately the narrowest of all: report-shape flags plus at
 * most one trailing safe ref token, and NOTHING else. No `--no-index`
 * (arbitrary two-file comparison — the confirmed PoC bypass), no pathspec
 * of any kind (a pathspec is how `git diff <ref> -- <path>` or a bare
 * `git diff <path>` reads a specific file's content; simplest safe answer
 * is to allow zero path arguments).
 */
const GIT_DIFF_SAFE_FLAGS: ReadonlySet<string> = new Set([
  "--stat",
  "--cached",
  "--staged",
  "--name-only",
]);

/** `show` — report-shape flags plus at most one trailing safe ref token. */
const GIT_SHOW_SAFE_FLAGS: ReadonlySet<string> = new Set(["--stat", "--name-only"]);

/**
 * `branch` / `tag` have a write-capable form (create/delete/move/force, or a
 * bare name argument creates a branch/tag). Only these list-shaped flags
 * are allowed as arguments, and NO name/ref argument at all — anything else
 * (a name, `-d`/`-D`, `-m`/`-M`, `-f`/`--force`, `--delete`, `--move`, ...)
 * rejects the whole command.
 */
const GIT_BRANCH_SAFE_FLAGS: ReadonlySet<string> = new Set(["-a", "-v", "-l", "--list"]);
const GIT_TAG_SAFE_FLAGS: ReadonlySet<string> = new Set(["-l", "--list", "-n"]);

/** `ls-files` — in-repo listing only, no path arguments. */
const GIT_LS_FILES_SAFE_FLAGS: ReadonlySet<string> = new Set([
  "--cached",
  "--others",
  "--modified",
]);

/** `describe` — no file-reading flags; a safe ref is still allowed. */
const GIT_DESCRIBE_SAFE_FLAGS: ReadonlySet<string> = new Set(["--tags", "--always"]);

/** `rev-parse` — a fixed, closed set of safe forms; no `--git-dir` etc. */
const GIT_REV_PARSE_SAFE_FLAGS: ReadonlySet<string> = new Set(["--abbrev-ref", "--short"]);

/**
 * `remote` has a write-capable form (add/remove/rename/set-url/prune/...)
 * and a network-capable read form (`ls-remote`/`show <url>`, dropped
 * entirely — see block comment above). The only read forms allowed here:
 * bare (lists remotes), `-v`, or `get-url <name>` where `<name>` matches
 * `SAFE_REF_PATTERN` (a configured remote's name, never a URL — a URL
 * token is rejected by `SAFE_REF_PATTERN` disallowing `:`).
 */
function isSafeRemoteArgs(rest: readonly string[]): boolean {
  if (rest.length === 0) return true;
  if (rest.length === 1 && rest[0] === "-v") return true;
  if (rest.length === 2 && rest[0] === "get-url" && isSafeRefToken(rest[1])) return true;
  return false;
}

/** `rev-parse HEAD` / `--abbrev-ref HEAD` / `--short HEAD` / `--verify <ref>`. */
function isSafeRevParseArgs(rest: readonly string[]): boolean {
  if (rest.length === 0) return false;
  if (rest.length === 1) {
    return rest[0] === "HEAD" || isSafeRefToken(rest[0]);
  }
  if (rest.length === 2 && rest[0] === "--verify") {
    return isSafeRefToken(rest[1]);
  }
  if (rest.length === 2 && GIT_REV_PARSE_SAFE_FLAGS.has(rest[0])) {
    return isSafeRefToken(rest[1]);
  }
  return false;
}

/** Generic "report flags + at most one trailing ref token" validator. */
function isSafeFlagsAndOptionalRef(
  rest: readonly string[],
  safeFlags: ReadonlySet<string>,
): boolean {
  let refSeen = false;
  for (const token of rest) {
    if (safeFlags.has(token)) continue;
    if (!refSeen && isSafeRefToken(token)) {
      refSeen = true;
      continue;
    }
    return false;
  }
  return true;
}

function isSafeStatusArgs(rest: readonly string[]): boolean {
  return rest.every((token) => GIT_STATUS_SAFE_FLAGS.has(token));
}

function isSafeLogArgs(rest: readonly string[]): boolean {
  let refSeen = false;
  for (let i = 0; i < rest.length; i++) {
    const token = rest[i];
    if (GIT_LOG_SAFE_FLAGS.has(token)) continue;
    if (token === "-n" || token === "--max-count") {
      const value = rest[i + 1];
      if (!value || !DIGITS_PATTERN.test(value)) return false;
      i += 1;
      continue;
    }
    if (MAX_COUNT_EQUALS_PATTERN.test(token)) continue;
    if (SHORT_COUNT_FLAG_PATTERN.test(token)) continue;
    if (!refSeen && isSafeRefToken(token)) {
      refSeen = true;
      continue;
    }
    return false;
  }
  return true;
}

function isSafeDiffArgs(rest: readonly string[]): boolean {
  return isSafeFlagsAndOptionalRef(rest, GIT_DIFF_SAFE_FLAGS);
}

function isSafeShowArgs(rest: readonly string[]): boolean {
  return isSafeFlagsAndOptionalRef(rest, GIT_SHOW_SAFE_FLAGS);
}

function isSafeDescribeArgs(rest: readonly string[]): boolean {
  return isSafeFlagsAndOptionalRef(rest, GIT_DESCRIBE_SAFE_FLAGS);
}

function isSafeListOnlyArgs(rest: readonly string[], safeFlags: ReadonlySet<string>): boolean {
  return rest.every((token) => safeFlags.has(token));
}

function isSafeLsFilesArgs(rest: readonly string[]): boolean {
  return rest.every((token) => GIT_LS_FILES_SAFE_FLAGS.has(token));
}

/**
 * Rule 4: git flags that can run an external program or override trusted
 * config, denied ANYWHERE in the token stream regardless of subcommand.
 * Redundant with the positive per-subcommand safe-flag allowlists above
 * (none of these ever appear on a safe-flag set), kept as an independent
 * second layer of defense-in-depth. Single source of truth — do not
 * duplicate elsewhere.
 */
export const DENIED_GIT_FLAGS: RegExp[] = [
  /^-c$/, // config-override
  /^--config(=.*)?$/, // config-override
  /^-o$/, // output flag can point at an arbitrary program/target via some subcommands' plumbing
  /^--output(=.*)?$/,
  /^-O/, // orderfile for diff/log (also covers attached forms like -O/etc/passwd) — arbitrary file read
  /^--pager(=.*)?$/, // overrides the pager program to run
  /^--open-files-in-pager(=.*)?$/, // runs the given program with matched files
  /^--ext-diff$/, // allows a configured external diff program to run
  /^--upload-pack(=.*)?$/, // transport program override
  /^--receive-pack(=.*)?$/, // transport program override
  /^--exec(=.*)?$/, // transport/exec program override
  /^--exec-path(=.*)?$/, // exec-path override (bare or with a value — reject either)
  /^--no-index$/, // git diff --no-index — arbitrary two-file comparison (confirmed PoC bypass)
  /^--file$/, // git config --file — arbitrary file read
  /^-f/, // git config -f <path> (also covers attached form -f<path>) — arbitrary file read
  /^--contents(=.*)?$/, // git blame --contents — arbitrary file read
  /^-i$/, // interactive
  /^--interactive$/, // interactive
];

/**
 * Rule 5 (ASPS-749 security re-review Minor, secret-guard invariant):
 * a ref/pathspec value token that is otherwise `SAFE_REF_PATTERN`-shaped can
 * still spell the name of a real secret file (`ACCESS_KEYS.env`, `id_rsa`,
 * `.env`) — `git diff ACCESS_KEYS.env` is a perfectly legal read-only git
 * invocation shape, so the per-subcommand allowlist above has no reason to
 * reject it. This carve-out validates a raw Bash *command string*, not a
 * tool `file_path`/`edits[]` field, so `findSecretPathInInput` (the
 * ASPS-743 fail-closed floor under `PATH_INPUT_FIELD` in agent.ts) never
 * sees it, and a naive scan of the whole command string against
 * `SECRET_PATH_PATTERNS` would also miss it: those patterns anchor a secret
 * name on `^` or a path separator, but inside `"git diff ACCESS_KEYS.env"`
 * the name is preceded by a space, not `^`/`/`. Reusing `matchSecretPath`
 * per-*token* (rather than against the raw command string) fixes that: each
 * token is checked from its own start, so `matchSecretPath`'s `^` anchor
 * lines up correctly. Applied centrally, after the per-subcommand allowlist
 * passes, against every non-flag token in `rest` — this covers every
 * subcommand that accepts a ref/pathspec-shaped value token (`log`/`diff`/
 * `show`/`rev-parse`/`describe`/`remote get-url`), so a secret-named value
 * falls through to Telegram approval instead of being auto-allowed. A flag
 * token (leading `-`) is skipped — flags are already a closed positive
 * allowlist per subcommand and never carry a secret name.
 */
function hasSecretNamedValueToken(tokens: readonly string[]): boolean {
  return tokens.some((token) => !token.startsWith("-") && matchSecretPath(token) !== undefined);
}

/**
 * Returns true ONLY if `command` is a single, standalone, strictly
 * READ-ONLY `git` invocation safe to auto-allow without a Telegram
 * approval. See the block comment above for the full threat model. Any
 * doubt returns false (stays approval-gated) — this is a pure function,
 * easy to unit test exhaustively; keep it that way.
 */
export function isSafeReadOnlyGitCommand(command: string): boolean {
  if (typeof command !== "string" || command.length === 0) return false;

  // Rule 1 — no shell metacharacters anywhere in the raw (untrimmed)
  // command; a leading/trailing newline is itself a rejection, not just an
  // embedded one.
  if (SHELL_METACHARACTER_PATTERN.test(command)) return false;

  const trimmed = command.trim();

  // Rule 2 — must begin with `git ` (case-sensitive, exact prefix — rejects
  // leading text, different case, and lookalike prefixes like `github` or
  // `git-lfs` since the character after `git` must be a space). `-C` is NOT
  // supported — the command must be `git <subcommand> ...` exactly, so the
  // bot's own `cwd` is always the repo operated on.
  if (!trimmed.startsWith("git ")) return false;

  const tokens = trimmed.split(/\s+/).filter((token) => token.length > 0);
  if (tokens[0] !== "git") return false;
  if (tokens.length < 2) return false;

  // Rule 1b/1c — glob/tilde/scheme/`::` rejected on every token.
  if (hasUnsafeToken(tokens)) return false;

  // Rule 3 — the subcommand must be the token immediately after `git` (no
  // `-C <path>`, no other leading flag). Any leading flag before the
  // subcommand (e.g. `--no-pager`, `-C`) is rejected — strict by design.
  const subcommand = tokens[1];
  if (subcommand.startsWith("-") || !READ_ONLY_GIT_SUBCOMMANDS.has(subcommand)) {
    return false;
  }

  const rest = tokens.slice(2);

  // Rule 4 — independent second layer: scan every token for a denied flag,
  // regardless of what the per-subcommand safe-flag allowlist below decides.
  for (const token of tokens) {
    if (DENIED_GIT_FLAGS.some((pattern) => pattern.test(token))) return false;
  }

  // Rule 5 — secret-named value-token guard (ASPS-749 security re-review
  // Minor). Evaluated centrally on `rest` so it covers every subcommand's
  // ref/pathspec-shaped value token with one pass, ahead of the
  // per-subcommand allowlist below (fail fast, and no subcommand branch can
  // forget to call it). See `hasSecretNamedValueToken` above for the full
  // rationale.
  if (hasSecretNamedValueToken(rest)) return false;

  // Rule 3 (continued) — per-subcommand positive safe-flag/safe-arg check.
  // Fail-safe default: an unrecognized subcommand never reaches here
  // because of the READ_ONLY_GIT_SUBCOMMANDS check above; every subcommand
  // reachable here has an explicit validator — there is no fallthrough.
  switch (subcommand) {
    case "status":
      return isSafeStatusArgs(rest);
    case "log":
      return isSafeLogArgs(rest);
    case "diff":
      return isSafeDiffArgs(rest);
    case "show":
      return isSafeShowArgs(rest);
    case "branch":
      return isSafeListOnlyArgs(rest, GIT_BRANCH_SAFE_FLAGS);
    case "tag":
      return isSafeListOnlyArgs(rest, GIT_TAG_SAFE_FLAGS);
    case "remote":
      return isSafeRemoteArgs(rest);
    case "rev-parse":
      return isSafeRevParseArgs(rest);
    case "describe":
      return isSafeDescribeArgs(rest);
    case "ls-files":
      return isSafeLsFilesArgs(rest);
    default:
      return false; // unreachable — defense-in-depth against a future edit.
  }
}

/**
 * Positive Bash allowlist for routine dev/read work (ASPS-762).
 *
 * `createCanUseTool` (agent.ts) auto-allows a `Bash` call ONLY when the WHOLE
 * command is a single, standalone invocation of an allowlisted program, so
 * routine builds/tests/reads stop flooding the operator with Telegram
 * approvals — WITHOUT weakening the ASPS-743 deny-by-default floor. This is
 * an ALLOWLIST, not a denylist: anything not explicitly matched here (an
 * unknown program, `sudo`/`docker`/`rm`/`mv`/`dd`/`chmod`/`chown`/`kill`/
 * `systemctl`/`curl`/`wget`, a git WRITE, a `python -c`/`node -e` one-liner,
 * ...) returns `false` and falls through to the existing Telegram approval
 * flow. `matchDangerousBashCommand` (agent.ts step 3) still hard-denies the
 * destructive patterns FIRST, and `isSafeReadOnlyGitCommand` (step 4) is the
 * single source of truth for read-only `git` — this function deliberately
 * does NOT re-implement git parsing (DRY; every `git` command is decided by
 * that adjacent branch, so `git push`/`reset`/`rebase`/`commit` are never
 * reachable here).
 *
 * These programs are auto-allowed with ANY (metacharacter-free) argument
 * list — dev/build/test tooling plus read utilities that neither mutate
 * state outside the repo nor exec an arbitrary caller-supplied program by
 * themselves. `node`, `python`, and `python3` are NOT here — they get
 * stricter, form-specific handling in `isSafeDevBashCommand` (no inline
 * `-e`/`-c` one-liner, no ad-hoc network script). Single source of truth —
 * do not duplicate elsewhere.
 */
export const SAFE_DEV_PROGRAMS: ReadonlySet<string> = new Set([
  "npm",
  "npx",
  "pnpm",
  "yarn",
  "tsc",
  "jest",
  "vitest",
  "ls",
  "cat",
  "grep",
  "rg",
  "find",
  "head",
  "tail",
  "echo",
  "pwd",
  "wc",
  "which",
]);

/**
 * `python -m <module>` / `python3 -m <module>` modules considered safe: test
 * and static-analysis runners only. Deliberately excludes anything that does
 * network I/O or opens a server/installer (`pip`, `http.server`, `venv`,
 * `ensurepip`, ...) — those stay approval-gated. Single source of truth.
 */
export const SAFE_PYTHON_MODULES: ReadonlySet<string> = new Set([
  "pytest",
  "unittest",
  "mypy",
  "ruff",
  "flake8",
  "pylint",
  "pyflakes",
  "black",
  "isort",
]);

/**
 * Inline-evaluation / preload flags for `node` — an inline one-liner's intent
 * (including network I/O) cannot be reliably parsed, so any of these forces
 * the command to gate. Covers `-e`/`--eval`, `-p`/`--print`, and
 * `-r`/`--require` (module preload), each with or without an `=value`.
 */
const NODE_INLINE_FLAG_PATTERN = /^(-e|--eval|-p|--print|-r|--require)(=.*)?$/;

/**
 * Argument tokens that turn an otherwise read-only utility into an
 * arbitrary-exec or delete vector (e.g. `find . -exec <cmd> {} \;`,
 * `find . -delete`) — denied anywhere in the command so a listed program
 * cannot smuggle execution/deletion past the allowlist.
 */
const UNSAFE_DEV_ARG_TOKENS: ReadonlySet<string> = new Set([
  "-exec",
  "-execdir",
  "-ok",
  "-okdir",
  "-delete",
  "-fprint",
  "-fprint0",
  "-fprintf",
]);

/**
 * Returns true ONLY if `command` is a single, standalone invocation of an
 * allowlisted dev/read program safe to auto-allow without a Telegram
 * approval (ASPS-762). Any doubt returns false (stays approval-gated) — a
 * pure function, kept easy to unit-test exhaustively.
 *
 * Evaluation:
 *  1. No shell metacharacters anywhere (reuses `SHELL_METACHARACTER_PATTERN`)
 *     — rejects chaining (`;`/`&&`/`|`), redirection (`<`/`>`), command
 *     substitution (`` ` ``/`$(...)`), subshells, and multi-line payloads,
 *     so the command is one standalone segment with no redirect to a path
 *     outside the repo.
 *  2. No glob/tilde/scheme/`::` token (reuses `hasUnsafeToken`).
 *  3. No secret-named token (reuses `hasSecretNamedValueToken`) — so
 *     `cat ACCESS_KEYS.env`, `cat .env`, `head server.key`, ... gate rather
 *     than auto-allow.
 *  4. No arbitrary-exec/delete argument token (`UNSAFE_DEV_ARG_TOKENS`).
 *  5. The first token (program) must be allowlisted:
 *       - `node`  — a script file only; any inline `-e`/`-p`/`-r` gates.
 *       - `python`/`python3` — only `-m <safe module>` (see
 *         `SAFE_PYTHON_MODULES`); inline `-c` and bare-script forms gate.
 *       - otherwise a member of `SAFE_DEV_PROGRAMS`.
 * `git` is intentionally NOT handled here — `isSafeReadOnlyGitCommand`
 * decides every git command in the adjacent agent.ts branch.
 */
export function isSafeDevBashCommand(command: string): boolean {
  if (typeof command !== "string" || command.length === 0) return false;

  // 1 — no shell metacharacters anywhere in the raw (untrimmed) command.
  if (SHELL_METACHARACTER_PATTERN.test(command)) return false;

  const tokens = command
    .trim()
    .split(/\s+/)
    .filter((token) => token.length > 0);
  if (tokens.length === 0) return false;

  // 2 — glob/tilde/scheme/`::` rejected on every token.
  if (hasUnsafeToken(tokens)) return false;

  // 3 — no secret-named value token (e.g. `cat ACCESS_KEYS.env`).
  if (hasSecretNamedValueToken(tokens)) return false;

  // 4 — no arbitrary-exec/delete argument token.
  if (tokens.some((token) => UNSAFE_DEV_ARG_TOKENS.has(token))) return false;

  const program = tokens[0];
  const rest = tokens.slice(1);

  // `node <script.js>` — allowed to run a script, but never an inline
  // one-liner or a preload flag whose intent (incl. network I/O) can't be
  // parsed. A bare `node` REPL is meaningless over Telegram — require an arg.
  if (program === "node") {
    if (rest.length === 0) return false;
    return !rest.some((token) => NODE_INLINE_FLAG_PATTERN.test(token));
  }

  // `python -m <safe module>` / `python3 -m <safe module>` only.
  if (program === "python" || program === "python3") {
    return rest.length >= 2 && rest[0] === "-m" && SAFE_PYTHON_MODULES.has(rest[1]);
  }

  return SAFE_DEV_PROGRAMS.has(program);
}

/**
 * Single source of truth for the secret-path denylist (ASPS-743 security
 * remediation, blocker B1).
 *
 * Matched against the fully resolved absolute path by `checkPathAllowed`
 * below. These patterns are rejected **always**, regardless of whether the
 * path is inside the allowed working-directory subtree — a path guard that
 * only checked confinement would still let the agent read
 * `<repo>/ACCESS_KEYS.env` or `<repo>/apps/telegram-ceo/.env`. Do not
 * duplicate this list elsewhere — import from here.
 */
export const SECRET_PATH_PATTERNS: RegExp[] = [
  /(^|[\\/])\.env(\.[^\\/]+)?$/i, // .env, .env.local, foo/.env.production
  /\.key$/i,
  /\.pem$/i,
  /\.pfx$/i,
  /(^|[\\/])id_rsa(\.[^\\/]+)?$/i, // id_rsa, id_rsa.pub
  /\.ppk$/i,
  /(^|[\\/])ACCESS_KEYS[^\\/]*$/i,
  /(^|[\\/])\.ssh([\\/]|$)/i,
  /(^|[\\/])\.aws([\\/]|$)/i,
  /(^|[\\/])\.gnupg([\\/]|$)/i,
];

/**
 * Returns the first secret pattern that matches `resolvedPath`, or
 * `undefined` if it matches none.
 */
export function matchSecretPath(resolvedPath: string): RegExp | undefined {
  return SECRET_PATH_PATTERNS.find((pattern) => pattern.test(resolvedPath));
}

export interface SecretPathHit {
  /** Dotted/bracketed path to the offending field, e.g. `edits[1].new_string`. */
  field: string;
  pattern: RegExp;
}

/**
 * Fail-closed invariant underneath the per-tool `PATH_INPUT_FIELD` allowlist
 * in `agent.ts` (ASPS-743 security re-review, Major M2).
 *
 * `PATH_INPUT_FIELD` only knows to check the ONE field a given tool is
 * documented to carry a path in (`file_path`, `notebook_path`, ...). That
 * breaks down for a write-capable tool the allowlist doesn't know about yet
 * (a future SDK tool), or for a tool whose path-bearing content lives
 * somewhere other than the allowlisted field (e.g. MultiEdit's
 * `edits[].old_string` / `edits[].new_string`). This function is the floor
 * under that allowlist: it recursively scans **every string-valued field**
 * of a tool's input — including array elements and nested objects — against
 * `SECRET_PATH_PATTERNS`, regardless of tool name or field name. A hit here
 * must hard-deny the call; it does not depend on `checkPathAllowed` ever
 * running for that field.
 *
 * Unlike `checkPathAllowed`, this does not resolve relative paths against a
 * working directory or follow symlinks — `SECRET_PATH_PATTERNS` are anchored
 * (`^`/`$`, path separators) so they match a field whose value IS a secret
 * path (or ends in one), which is the shape every known exploit path takes
 * (a tool argument that names the secret file/directory directly).
 */
export function findSecretPathInInput(input: unknown): SecretPathHit | undefined {
  return scan(input, "");
}

function scan(value: unknown, fieldPath: string): SecretPathHit | undefined {
  if (typeof value === "string") {
    const pattern = matchSecretPath(value);
    return pattern ? { field: fieldPath || "(root)", pattern } : undefined;
  }
  if (Array.isArray(value)) {
    for (let i = 0; i < value.length; i++) {
      const hit = scan(value[i], `${fieldPath}[${i}]`);
      if (hit) return hit;
    }
    return undefined;
  }
  if (value && typeof value === "object") {
    for (const [key, v] of Object.entries(value as Record<string, unknown>)) {
      const hit = scan(v, fieldPath ? `${fieldPath}.${key}` : key);
      if (hit) return hit;
    }
    return undefined;
  }
  return undefined;
}

export type PathGuardResult =
  | { allowed: true; resolvedPath: string }
  | { allowed: false; reason: string };

/**
 * Path guard (ASPS-743 security remediation, blocker B1).
 *
 * Consulted from `canUseTool` (see agent.ts) for every tool whose input
 * carries a filesystem path (Read, Edit, Write, NotebookEdit; Grep/Glob
 * where feasible). Mirrors the spirit of the deleted `tools.ts`
 * `safePath()` helper, but as a guard consulted before the tool ever runs,
 * not a throwing wrapper invoked from inside a hand-rolled tool.
 *
 * Order of checks:
 *  1. Resolve `rawPath` to a real, symlink-free absolute path (relative to
 *     `workingDir` when not already absolute).
 *  2. Reject unconditionally if the resolved path matches a secret pattern
 *     (`SECRET_PATH_PATTERNS`), regardless of location.
 *  3. Reject if the resolved path falls outside `workingDir`.
 */
export function checkPathAllowed(rawPath: string, workingDir: string): PathGuardResult {
  const root = resolveRealPath(path.resolve(workingDir));
  const target = resolveRealPath(path.resolve(workingDir, rawPath));

  const secretMatch = matchSecretPath(target);
  if (secretMatch) {
    return {
      allowed: false,
      reason: `path matches a protected secret pattern (${secretMatch.source}): ${rawPath}`,
    };
  }

  const rootWithSep = root.endsWith(path.sep) ? root : root + path.sep;
  if (target !== root && !target.startsWith(rootWithSep)) {
    return {
      allowed: false,
      reason: `path resolves outside the allowed working directory (${root}): ${rawPath}`,
    };
  }

  return { allowed: true, resolvedPath: target };
}

/**
 * Resolve symlinks for a path that may not exist yet (e.g. a new file about
 * to be created by Write). Walks up to the nearest existing ancestor,
 * resolves that ancestor's real path, then rejoins the non-existent
 * remainder — so a symlinked ancestor directory cannot be used to escape
 * the sandbox even for a file that doesn't exist yet.
 */
function resolveRealPath(candidate: string): string {
  try {
    return realpathSync(candidate);
  } catch {
    const parent = path.dirname(candidate);
    if (parent === candidate) return candidate; // reached filesystem root
    return path.join(resolveRealPath(parent), path.basename(candidate));
  }
}

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
 * Strict read-only `git` allowlist (ASPS-749).
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
 * An allowlist over a shell is dangerous for two reasons this function must
 * defend against:
 *
 *  (a) Shell metacharacters let one command chain into another (`;`, `&&`,
 *      `|`, backticks/`$(...)` substitution, `(...)`/`{...}` subshells,
 *      `<`/`>` redirection, `\` line continuation, embedded newlines).
 *  (b) `git` itself can be made to execute an external program or override
 *      trusted config via certain flags (`-c`/`--config`, `-o`/`--output`/
 *      `-O`/`--pager`/`--open-files-in-pager`, `--ext-diff`,
 *      `--upload-pack`/`--receive-pack`/`--exec`/`--exec-path`, interactive
 *      flags).
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
 * Rule 3: strict READ-ONLY git subcommand allowlist. Single source of
 * truth — do not duplicate elsewhere.
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
  "blame",
  "shortlog",
  "ls-files",
  "ls-remote",
  "tag",
  "config",
]);

/**
 * `branch` / `tag` have a write-capable form (create/delete/move/force, or a
 * bare name argument creates a branch/tag). Only these list-shaped tokens
 * are allowed as arguments — anything else (a name, `-d`/`-D`, `-m`/`-M`,
 * `-f`/`--force`, `--delete`, `--move`, ...) rejects the whole command.
 */
const GIT_LIST_ONLY_FLAGS: ReadonlySet<string> = new Set(["-l", "--list", "-a", "-v"]);

/**
 * `remote` has a write-capable form (add/remove/rename/set-url/prune/...).
 * Only these read forms are allowed as the first remaining token: bare
 * (empty — lists remotes), `-v`, `get-url`, `show`.
 */
const GIT_REMOTE_READ_MODES: ReadonlySet<string> = new Set(["-v", "get-url", "show"]);

/**
 * `config` has a write-capable form (`git config key value` sets it). Only
 * these read forms are allowed as the first remaining token.
 */
const GIT_CONFIG_READ_FLAGS: ReadonlySet<string> = new Set(["--get", "--get-all", "--list"]);

/**
 * Rule 4: git flags that can run an external program or override trusted
 * config, denied ANYWHERE in the token stream regardless of subcommand.
 * Single source of truth — do not duplicate elsewhere.
 */
export const DENIED_GIT_FLAGS: RegExp[] = [
  /^-c$/, // config-override
  /^--config(=.*)?$/, // config-override
  /^-o$/, // output flag can point at an arbitrary program/target via some subcommands' plumbing
  /^--output(=.*)?$/,
  /^-O$/, // orderfile for diff — arbitrary file read, treat as untrusted-input risk
  /^--pager(=.*)?$/, // overrides the pager program to run
  /^--open-files-in-pager(=.*)?$/, // runs the given program with matched files
  /^--ext-diff$/, // allows a configured external diff program to run
  /^--upload-pack(=.*)?$/, // transport program override
  /^--receive-pack(=.*)?$/, // transport program override
  /^--exec(=.*)?$/, // transport/exec program override
  /^--exec-path(=.*)?$/, // exec-path override (bare or with a value — reject either)
  /^-i$/, // interactive
  /^--interactive$/, // interactive
];

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
  // `git-lfs` since the character after `git` must be a space).
  if (!trimmed.startsWith("git ")) return false;

  const tokens = trimmed.split(/\s+/).filter((token) => token.length > 0);
  if (tokens[0] !== "git") return false;

  let i = 1;

  // Optional `git -C <path>` — the path is already metacharacter-free (rule
  // 1); additionally reject if it looks like a flag (e.g. an attempt to
  // smuggle `-c`/`--config` in as the "path" argument).
  if (tokens[i] === "-C") {
    const value = tokens[i + 1];
    if (!value || value.startsWith("-")) return false;
    i += 2;
  }

  // Rule 3 — the subcommand is the first non-flag token after `git`/`-C
  // <path>`. Any other leading flag before the subcommand (e.g.
  // `--no-pager`) is not on any allowlist here, so it is rejected — strict
  // by design, not merely by omission.
  const subcommand = tokens[i];
  if (!subcommand || subcommand.startsWith("-") || !READ_ONLY_GIT_SUBCOMMANDS.has(subcommand)) {
    return false;
  }

  const rest = tokens.slice(i + 1);

  // Rule 4 — scan every token (including `git`, the optional `-C <path>`,
  // the subcommand, and every remaining argument) for a denied flag.
  for (const token of tokens) {
    if (DENIED_GIT_FLAGS.some((pattern) => pattern.test(token))) return false;
  }

  // Rule 3 (continued) — subcommand-specific safe-form restriction for the
  // three subcommands that have a write-capable form.
  switch (subcommand) {
    case "remote":
      if (rest.length > 0 && !GIT_REMOTE_READ_MODES.has(rest[0])) return false;
      break;
    case "branch":
    case "tag":
      if (!rest.every((token) => GIT_LIST_ONLY_FLAGS.has(token))) return false;
      break;
    case "config":
      if (rest.length === 0 || !GIT_CONFIG_READ_FLAGS.has(rest[0])) return false;
      break;
    default:
      break; // status/log/show/diff/rev-parse/describe/blame/shortlog/ls-files/ls-remote: no write-capable form.
  }

  return true;
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

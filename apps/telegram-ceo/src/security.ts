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

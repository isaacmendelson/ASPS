/**
 * Single source of truth for the destructive-command denylist.
 *
 * Used by the Claude Agent SDK `canUseTool` permission hook (see agent.ts)
 * to deny Bash commands that would cause irreversible damage (data loss,
 * force-pushing over history, dropping databases, etc.) when the bot is
 * driven autonomously over Telegram. Do not duplicate this list elsewhere —
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

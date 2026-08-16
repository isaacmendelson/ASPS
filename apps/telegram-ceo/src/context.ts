import { readFileSync, existsSync } from "node:fs";
import { join, resolve } from "node:path";

/**
 * Loads the CEO system prompt from the ASPS project's CLAUDE.md
 * and the hat file hierarchy.
 */
export function loadSystemPrompt(workingDir: string): string {
  const parts: string[] = [];

  const filesToLoad = [
    "CLAUDE.md",
    ".claude/hats/ceo/INDEX.md",
    ".claude/hats/ceo/identity.md",
    ".claude/hats/ceo/communication.md",
    ".claude/hats/ceo/operating_principles.md",
    ".claude/hats/ceo/delegation.md",
    ".claude/team/CHARTER.md",
  ];

  for (const relPath of filesToLoad) {
    const fullPath = resolve(join(workingDir, relPath));
    if (existsSync(fullPath)) {
      try {
        const content = readFileSync(fullPath, "utf-8");
        parts.push(`--- ${relPath} ---\n${content}`);
      } catch {
        // Skip files that can't be read
      }
    }
  }

  if (parts.length === 0) {
    return "You are an AI CEO assistant for the ASPS project. Help the user with project management, code review, and technical decisions.";
  }

  const preamble = [
    "You are the CEO of ASPS, communicating via Telegram.",
    "The user is the boss. You have tool access to read/write files and run commands in the project.",
    "Keep responses concise — Telegram messages have a 4096 character limit.",
    "Use markdown formatting sparingly (Telegram supports basic markdown).",
    "When the user writes in Hebrew, respond in Hebrew with English technical terms.",
    "",
    "Project context loaded from the following files:",
    "",
  ].join("\n");

  return preamble + parts.join("\n\n");
}

import { readFileSync, writeFileSync, existsSync, mkdirSync } from "node:fs";
import { execSync } from "node:child_process";
import { resolve, join, dirname } from "node:path";
import type Anthropic from "@anthropic-ai/sdk";

type Tool = Anthropic.Messages.Tool;

const workingDir = process.env.WORKING_DIR || process.cwd();

/** Resolve a path relative to WORKING_DIR, preventing directory traversal. */
function safePath(inputPath: string): string {
  const resolved = resolve(join(workingDir, inputPath));
  // Also allow absolute paths within the working dir
  const abs = resolve(inputPath);
  const target = abs.startsWith(resolve(workingDir)) ? abs : resolved;

  if (!target.startsWith(resolve(workingDir))) {
    throw new Error(`Path escapes working directory: ${inputPath}`);
  }
  return target;
}

// --- Tool definitions for the Claude API ---

export const TOOL_DEFINITIONS: Tool[] = [
  {
    name: "read_file",
    description:
      "Read the contents of a file. Returns the file text. Use absolute paths relative to the project root, or relative paths.",
    input_schema: {
      type: "object" as const,
      properties: {
        path: {
          type: "string",
          description: "File path to read",
        },
      },
      required: ["path"],
    },
  },
  {
    name: "write_file",
    description:
      "Write content to a file. Creates parent directories if needed. Use for creating new files or complete rewrites.",
    input_schema: {
      type: "object" as const,
      properties: {
        path: {
          type: "string",
          description: "File path to write",
        },
        content: {
          type: "string",
          description: "Content to write to the file",
        },
      },
      required: ["path", "content"],
    },
  },
  {
    name: "edit_file",
    description:
      "Edit a file by replacing an exact string match. The old_string must appear exactly once in the file.",
    input_schema: {
      type: "object" as const,
      properties: {
        path: {
          type: "string",
          description: "File path to edit",
        },
        old_string: {
          type: "string",
          description: "Exact string to find and replace",
        },
        new_string: {
          type: "string",
          description: "Replacement string",
        },
      },
      required: ["path", "old_string", "new_string"],
    },
  },
  {
    name: "bash",
    description:
      "Execute a shell command (PowerShell on Windows). Returns stdout and stderr. Use for builds, git, npm, and other CLI operations. Commands run in the project working directory.",
    input_schema: {
      type: "object" as const,
      properties: {
        command: {
          type: "string",
          description: "Shell command to execute",
        },
        timeout_ms: {
          type: "number",
          description: "Timeout in milliseconds (default: 30000)",
        },
      },
      required: ["command"],
    },
  },
  {
    name: "grep",
    description:
      "Search for a pattern in files. Returns matching lines with file paths and line numbers.",
    input_schema: {
      type: "object" as const,
      properties: {
        pattern: {
          type: "string",
          description: "Regex pattern to search for",
        },
        path: {
          type: "string",
          description:
            "Directory or file to search in (default: project root)",
        },
        include: {
          type: "string",
          description: 'File glob filter, e.g. "*.cs" or "*.ts"',
        },
      },
      required: ["pattern"],
    },
  },
  {
    name: "glob",
    description:
      "Find files matching a glob pattern. Returns a list of matching file paths.",
    input_schema: {
      type: "object" as const,
      properties: {
        pattern: {
          type: "string",
          description: 'Glob pattern, e.g. "**/*.cs" or "src/**/*.ts"',
        },
        path: {
          type: "string",
          description: "Base directory to search from (default: project root)",
        },
      },
      required: ["pattern"],
    },
  },
  {
    name: "list_directory",
    description:
      "List files and directories in a given path. Returns names with type indicators.",
    input_schema: {
      type: "object" as const,
      properties: {
        path: {
          type: "string",
          description: "Directory path to list (default: project root)",
        },
      },
      required: [],
    },
  },
];

// --- Tool execution ---

interface ToolInput {
  path?: string;
  content?: string;
  old_string?: string;
  new_string?: string;
  command?: string;
  timeout_ms?: number;
  pattern?: string;
  include?: string;
}

/** Dangerous patterns that require confirmation (not auto-executed). */
const DANGEROUS_PATTERNS = [
  /\brm\s+(-rf?|--force|--recursive)\b/i,
  /\bRemove-Item\s+.*-Recurse/i,
  /\bdel\s+\/[sfq]/i,
  /\bformat\b/i,
  /\bDROP\s+(TABLE|DATABASE)\b/i,
  /\bgit\s+(reset\s+--hard|push\s+--force|clean\s+-f)/i,
];

export async function executeTool(
  name: string,
  input: ToolInput,
): Promise<string> {
  switch (name) {
    case "read_file":
      return toolReadFile(input.path!);
    case "write_file":
      return toolWriteFile(input.path!, input.content!);
    case "edit_file":
      return toolEditFile(input.path!, input.old_string!, input.new_string!);
    case "bash":
      return toolBash(input.command!, input.timeout_ms);
    case "grep":
      return toolGrep(input.pattern!, input.path, input.include);
    case "glob":
      return toolGlob(input.pattern!, input.path);
    case "list_directory":
      return toolListDir(input.path);
    default:
      return `Unknown tool: ${name}`;
  }
}

function toolReadFile(path: string): string {
  try {
    const resolved = safePath(path);
    if (!existsSync(resolved)) {
      return `Error: File not found: ${path}`;
    }
    const content = readFileSync(resolved, "utf-8");
    const lines = content.split("\n");
    if (lines.length > 2000) {
      return `File has ${lines.length} lines. Showing first 2000:\n${lines.slice(0, 2000).join("\n")}`;
    }
    return content;
  } catch (err) {
    return `Error reading file: ${err instanceof Error ? err.message : String(err)}`;
  }
}

function toolWriteFile(path: string, content: string): string {
  try {
    const resolved = safePath(path);
    const dir = dirname(resolved);
    if (!existsSync(dir)) {
      mkdirSync(dir, { recursive: true });
    }
    writeFileSync(resolved, content, "utf-8");
    return `File written: ${path}`;
  } catch (err) {
    return `Error writing file: ${err instanceof Error ? err.message : String(err)}`;
  }
}

function toolEditFile(
  path: string,
  oldString: string,
  newString: string,
): string {
  try {
    const resolved = safePath(path);
    if (!existsSync(resolved)) {
      return `Error: File not found: ${path}`;
    }
    const content = readFileSync(resolved, "utf-8");
    const occurrences = content.split(oldString).length - 1;
    if (occurrences === 0) {
      return `Error: old_string not found in file`;
    }
    if (occurrences > 1) {
      return `Error: old_string found ${occurrences} times — must be unique`;
    }
    const updated = content.replace(oldString, newString);
    writeFileSync(resolved, updated, "utf-8");
    return `File edited: ${path}`;
  } catch (err) {
    return `Error editing file: ${err instanceof Error ? err.message : String(err)}`;
  }
}

function toolBash(command: string, timeoutMs?: number): string {
  // Block dangerous commands
  for (const pattern of DANGEROUS_PATTERNS) {
    if (pattern.test(command)) {
      return `BLOCKED: This command matches a dangerous pattern (${pattern.source}). Destructive operations are not auto-executed via Telegram.`;
    }
  }

  try {
    const result = execSync(command, {
      cwd: workingDir,
      encoding: "utf-8",
      timeout: timeoutMs || 30000,
      shell: "powershell.exe",
      maxBuffer: 1024 * 1024, // 1 MB
    });
    const output = result.trim();
    if (output.length > 10000) {
      return output.slice(0, 10000) + "\n... (truncated)";
    }
    return output || "(no output)";
  } catch (err: unknown) {
    const execErr = err as { stdout?: string; stderr?: string; message?: string };
    const stdout = execErr.stdout?.trim() || "";
    const stderr = execErr.stderr?.trim() || "";
    const combined = [stdout, stderr].filter(Boolean).join("\n");
    if (combined) {
      const output = combined.length > 5000 ? combined.slice(0, 5000) + "\n... (truncated)" : combined;
      return `Command failed:\n${output}`;
    }
    return `Command failed: ${execErr.message || String(err)}`;
  }
}

function toolGrep(
  pattern: string,
  path?: string,
  include?: string,
): string {
  try {
    const searchPath = path ? safePath(path) : workingDir;
    // Use PowerShell Select-String for grep
    let cmd = `Get-ChildItem -Path "${searchPath}" -Recurse -File`;
    if (include) {
      cmd += ` -Filter "${include}"`;
    }
    cmd += ` | Select-String -Pattern "${pattern.replace(/"/g, '`"')}" -ErrorAction SilentlyContinue`;
    cmd += ` | Select-Object -First 50`;
    cmd += ` | ForEach-Object { "$($_.RelativePath($_.Path)):$($_.LineNumber): $($_.Line.Trim())" }`;

    // Simpler approach: use findstr on Windows or rg if available
    let findCmd: string;
    const rgPath = "rg";
    try {
      execSync("rg --version", { cwd: workingDir, encoding: "utf-8", timeout: 3000, shell: "powershell.exe" });
      // ripgrep available
      findCmd = `rg -n --max-count 50 "${pattern.replace(/"/g, '\\"')}"`;
      if (include) findCmd += ` -g "${include}"`;
      findCmd += ` "${searchPath}"`;
    } catch {
      // Fallback to Select-String
      findCmd = cmd;
    }

    const result = execSync(findCmd, {
      cwd: workingDir,
      encoding: "utf-8",
      timeout: 15000,
      shell: "powershell.exe",
      maxBuffer: 512 * 1024,
    });

    const output = result.trim();
    if (!output) return "No matches found.";
    if (output.length > 8000) {
      return output.slice(0, 8000) + "\n... (truncated)";
    }
    return output;
  } catch (err: unknown) {
    const execErr = err as { stdout?: string; message?: string };
    if (execErr.stdout?.trim()) return execErr.stdout.trim();
    return `Grep error: ${execErr.message || String(err)}`;
  }
}

function toolGlob(pattern: string, path?: string): string {
  try {
    const basePath = path ? safePath(path) : workingDir;
    // Use PowerShell Get-ChildItem with glob pattern
    const cmd = `Get-ChildItem -Path "${basePath}" -Recurse -File -Filter "${pattern}" | Select-Object -First 100 | ForEach-Object { $_.FullName.Replace("${workingDir.replace(/\\/g, "\\\\")}\\", "") }`;

    const result = execSync(cmd, {
      cwd: workingDir,
      encoding: "utf-8",
      timeout: 15000,
      shell: "powershell.exe",
      maxBuffer: 512 * 1024,
    });

    const output = result.trim();
    if (!output) return "No files matched.";
    return output;
  } catch (err: unknown) {
    const execErr = err as { stdout?: string; message?: string };
    if (execErr.stdout?.trim()) return execErr.stdout.trim();
    return `Glob error: ${execErr.message || String(err)}`;
  }
}

function toolListDir(path?: string): string {
  try {
    const dirPath = path ? safePath(path) : workingDir;
    const cmd = `Get-ChildItem -Path "${dirPath}" | ForEach-Object { if ($_.PSIsContainer) { "[$($_.Name)]/" } else { $_.Name } }`;
    const result = execSync(cmd, {
      cwd: workingDir,
      encoding: "utf-8",
      timeout: 10000,
      shell: "powershell.exe",
    });
    return result.trim() || "(empty directory)";
  } catch (err: unknown) {
    const execErr = err as { message?: string };
    return `Error listing directory: ${execErr.message || String(err)}`;
  }
}

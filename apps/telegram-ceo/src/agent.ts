import Anthropic from "@anthropic-ai/sdk";
import { TOOL_DEFINITIONS, executeTool } from "./tools.js";
import { loadSystemPrompt } from "./context.js";
import { getMessages, addMessage } from "./session.js";

type MessageParam = Anthropic.Messages.MessageParam;
type ContentBlock = Anthropic.Messages.ContentBlock;
type ToolResultBlockParam = Anthropic.Messages.ToolResultBlockParam;

const anthropic = new Anthropic();

let cachedSystemPrompt: string | null = null;

function getSystemPrompt(): string {
  if (!cachedSystemPrompt) {
    const workingDir = process.env.WORKING_DIR || process.cwd();
    cachedSystemPrompt = loadSystemPrompt(workingDir);
  }
  return cachedSystemPrompt;
}

/** Extract text content from a response. */
function extractText(content: ContentBlock[]): string {
  return content
    .filter((block): block is Anthropic.Messages.TextBlock => block.type === "text")
    .map((block) => block.text)
    .join("\n");
}

/** Check if the response contains tool use blocks. */
function hasToolUse(content: ContentBlock[]): boolean {
  return content.some((block) => block.type === "tool_use");
}

/** Execute all tool calls in a response and return tool results. */
async function executeToolCalls(
  content: ContentBlock[],
): Promise<ToolResultBlockParam[]> {
  const results: ToolResultBlockParam[] = [];

  for (const block of content) {
    if (block.type === "tool_use") {
      const toolResult = await executeTool(
        block.name,
        block.input as Record<string, unknown>,
      );
      results.push({
        type: "tool_result",
        tool_use_id: block.id,
        content: toolResult,
      });
    }
  }

  return results;
}

/**
 * Run the agentic loop: send messages to Claude, execute any tool calls,
 * and continue until Claude produces a final text response.
 *
 * Calls onToolUse callback after each tool round so the caller can
 * send typing indicators.
 */
export async function runAgent(
  userId: number,
  userMessage: string,
  onToolUse?: () => void,
): Promise<string> {
  const messages = getMessages(userId);

  // Add the user's message
  addMessage(userId, { role: "user", content: userMessage });

  const model = process.env.MODEL || "claude-sonnet-4-20250514";
  const maxTokens = Number(process.env.MAX_TOKENS) || 8192;
  const systemPrompt = getSystemPrompt();

  let iterations = 0;
  const maxIterations = 20; // Safety limit on agentic loop

  while (iterations < maxIterations) {
    iterations++;

    const response = await anthropic.messages.create({
      model,
      max_tokens: maxTokens,
      system: systemPrompt,
      messages: getMessages(userId),
      tools: TOOL_DEFINITIONS,
    });

    // If no tool use, we're done — return the text
    if (response.stop_reason === "end_turn" || !hasToolUse(response.content)) {
      const text = extractText(response.content);
      // Add assistant response to history
      addMessage(userId, { role: "assistant", content: response.content });
      return text || "(no response)";
    }

    // Tool use: add assistant message, execute tools, add results
    addMessage(userId, { role: "assistant", content: response.content });

    const toolResults = await executeToolCalls(response.content);
    addMessage(userId, { role: "user", content: toolResults });

    // Notify caller that tools were used (for typing indicator)
    onToolUse?.();
  }

  return "Agent reached maximum iteration limit. The task may be too complex for a single message.";
}

/** Invalidate cached system prompt (e.g., after project files change). */
export function reloadSystemPrompt(): void {
  cachedSystemPrompt = null;
}

import { randomUUID } from "node:crypto";

/**
 * Telegram human-approval mechanism (ASPS-743 security remediation).
 *
 * Every state-changing tool call goes through `requestApproval` from
 * `agent.ts`'s `canUseTool` hook. This module only owns the pending-request
 * bookkeeping and timeout; it knows nothing about Telegram. `bot.ts` wires
 * the actual transport via `setApprovalRequestHandler` (send an
 * inline-keyboard message) and resolves pending requests from its
 * `callback_query` handler via `resolveApproval`. Kept decoupled from
 * `agent.ts`/`bot.ts` to avoid a circular import between the two.
 */

export type ApprovalDecision = "allow" | "deny";

export interface ApprovalRequest {
  /** Correlates this request with the `callback_query` that answers it. */
  id: string;
  /** The Telegram user who owns this turn — only this user may answer. */
  userId: number;
  toolName: string;
  /** Truncated, safe-to-render summary of the tool input (command or path). */
  summary: string;
}

export type ApprovalRequestHandler = (request: ApprovalRequest) => void | Promise<void>;

const DEFAULT_TIMEOUT_MS = 60_000;

interface PendingEntry {
  userId: number;
  resolve: (decision: ApprovalDecision) => void;
}

const pending = new Map<string, PendingEntry>();

let requestHandler: ApprovalRequestHandler | undefined;

/** Wired once at bot startup — how an approval request is actually delivered. */
export function setApprovalRequestHandler(handler: ApprovalRequestHandler | undefined): void {
  requestHandler = handler;
}

function getTimeoutMs(): number {
  const configured = Number(process.env.APPROVAL_TIMEOUT_MS);
  return Number.isFinite(configured) && configured > 0 ? configured : DEFAULT_TIMEOUT_MS;
}

/**
 * Request human approval for a state-changing tool call.
 *
 * Resolves `"allow"` only when the same Telegram user who owns this turn
 * taps Approve. Resolves `"deny"` on an explicit Deny tap, on timeout
 * (`APPROVAL_TIMEOUT_MS`, default 60s), or when no approval transport is
 * wired at all — fail-closed, never hangs forever, never silently allows.
 */
export function requestApproval(
  userId: number,
  toolName: string,
  summary: string,
): Promise<ApprovalDecision> {
  const id = randomUUID();

  return new Promise<ApprovalDecision>((resolve) => {
    const timer = setTimeout(() => {
      pending.delete(id);
      resolve("deny");
    }, getTimeoutMs());
    // A pending approval timer alone should never keep the process alive.
    timer.unref?.();

    pending.set(id, {
      userId,
      resolve: (decision) => {
        clearTimeout(timer);
        resolve(decision);
      },
    });

    if (!requestHandler) {
      pending.delete(id);
      clearTimeout(timer);
      resolve("deny");
      return;
    }

    try {
      void requestHandler({ id, userId, toolName, summary });
    } catch {
      pending.delete(id);
      clearTimeout(timer);
      resolve("deny");
    }
  });
}

/**
 * Resolve a pending approval request from an inbound Telegram
 * `callback_query`.
 *
 * Returns `true` if a matching pending request was resolved. Returns
 * `false` — and resolves nothing — for an unknown/already-settled id, or
 * when `fromUserId` does not match the user the request was issued for.
 * Approvals are strictly per-requesting-user; nobody else's tap can
 * approve or deny someone else's pending action.
 */
export function resolveApproval(id: string, fromUserId: number, decision: ApprovalDecision): boolean {
  const entry = pending.get(id);
  if (!entry || entry.userId !== fromUserId) return false;
  pending.delete(id);
  entry.resolve(decision);
  return true;
}

/** Number of approval requests currently awaiting a Telegram response. Test/diagnostic use. */
export function pendingApprovalCount(): number {
  return pending.size;
}

/** Clears all pending approvals without resolving them. Test/shutdown use only. */
export function clearPendingApprovals(): void {
  pending.clear();
}

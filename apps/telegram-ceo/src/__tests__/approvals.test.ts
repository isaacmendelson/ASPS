import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  clearPendingApprovals,
  pendingApprovalCount,
  requestApproval,
  resolveApproval,
  setApprovalRequestHandler,
} from "../approvals.js";

describe("approvals (Telegram human-approval flow, ASPS-743 blockers B2/B3)", () => {
  afterEach(() => {
    setApprovalRequestHandler(undefined);
    clearPendingApprovals();
    vi.useRealTimers();
    delete process.env.APPROVAL_TIMEOUT_MS;
  });

  it("resolves 'allow' when the requesting user approves via the matching correlation id", async () => {
    let captured: { id: string; userId: number } | undefined;
    setApprovalRequestHandler((req) => {
      captured = req;
    });

    const decisionPromise = requestApproval(111, "Write", "src/agent.ts");
    expect(captured).toBeDefined();

    const resolved = resolveApproval(captured!.id, 111, "allow");
    expect(resolved).toBe(true);
    await expect(decisionPromise).resolves.toBe("allow");
  });

  it("resolves 'deny' when the requesting user denies", async () => {
    let captured: { id: string } | undefined;
    setApprovalRequestHandler((req) => {
      captured = req;
    });

    const decisionPromise = requestApproval(111, "Bash", "git push origin main");
    resolveApproval(captured!.id, 111, "deny");

    await expect(decisionPromise).resolves.toBe("deny");
  });

  it("ignores a callback from a different user — does not resolve the pending request", async () => {
    let captured: { id: string } | undefined;
    setApprovalRequestHandler((req) => {
      captured = req;
    });

    const decisionPromise = requestApproval(111, "Write", "src/agent.ts");

    const resolvedByStranger = resolveApproval(captured!.id, 999, "allow");
    expect(resolvedByStranger).toBe(false);
    expect(pendingApprovalCount()).toBe(1);

    // Clean up without waiting for the real timeout.
    resolveApproval(captured!.id, 111, "deny");
    await decisionPromise;
  });

  it("returns false for an unknown/already-settled correlation id", async () => {
    setApprovalRequestHandler(() => {});
    const decisionPromise = requestApproval(111, "Write", "src/agent.ts");

    expect(resolveApproval("not-a-real-id", 111, "allow")).toBe(false);

    // settle the real one so the test doesn't leave a dangling timer
    const count = pendingApprovalCount();
    expect(count).toBe(1);
    setApprovalRequestHandler(undefined);
    clearPendingApprovals();
    await Promise.race([decisionPromise, Promise.resolve()]);
  });

  it("times out to 'deny' after APPROVAL_TIMEOUT_MS with no response", async () => {
    vi.useFakeTimers();
    process.env.APPROVAL_TIMEOUT_MS = "1000";
    setApprovalRequestHandler(() => {
      // Never responds.
    });

    const decisionPromise = requestApproval(111, "Bash", "npm install left-pad");
    const assertion = expect(decisionPromise).resolves.toBe("deny");

    await vi.advanceTimersByTimeAsync(1000);
    await assertion;
  });

  it("correlates multiple concurrent requests independently", async () => {
    const requests: { id: string; userId: number }[] = [];
    setApprovalRequestHandler((req) => {
      requests.push(req);
    });

    const p1 = requestApproval(111, "Write", "a.ts");
    const p2 = requestApproval(222, "Write", "b.ts");
    expect(requests).toHaveLength(2);
    expect(requests[0].id).not.toBe(requests[1].id);

    resolveApproval(requests[1].id, 222, "allow");
    resolveApproval(requests[0].id, 111, "deny");

    await expect(p1).resolves.toBe("deny");
    await expect(p2).resolves.toBe("allow");
  });

  it("fails closed to 'deny' when no approval transport is wired at all", async () => {
    setApprovalRequestHandler(undefined);
    await expect(requestApproval(111, "Bash", "rm -rf /")).resolves.toBe("deny");
  });
});

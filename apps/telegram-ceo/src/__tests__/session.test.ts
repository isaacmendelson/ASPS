import { beforeEach, describe, expect, it } from "vitest";
import { clearSession, getSessionId, setSessionId } from "../session.js";

describe("session", () => {
  beforeEach(() => {
    clearSession(1);
    clearSession(2);
  });

  it("has no session id for an unseen user", () => {
    expect(getSessionId(1)).toBeUndefined();
  });

  it("stores and returns a session id per user", () => {
    setSessionId(1, "session-a");
    setSessionId(2, "session-b");
    expect(getSessionId(1)).toBe("session-a");
    expect(getSessionId(2)).toBe("session-b");
  });

  it("clears a user's session independently of other users", () => {
    setSessionId(1, "session-a");
    setSessionId(2, "session-b");
    clearSession(1);
    expect(getSessionId(1)).toBeUndefined();
    expect(getSessionId(2)).toBe("session-b");
  });
});

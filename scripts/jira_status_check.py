#!/usr/bin/env python3
"""JIRA-vs-reality reconciliation check (ASPS / MASDP orchestration aid).

Purpose: surface JIRA status drift so the orchestrator can't forget to keep the
board in sync (see the recurring feedback: tickets left at To Do while work is
in flight, or not moved to Done after merge). Designed to run as a Stop hook.

Cheap by default: it computes a signature of the current git state (HEAD sha +
the set of open `asps-*` branches) and short-circuits to SILENT when nothing has
changed since the last run — so ordinary conversation turns cost nothing. It hits
the JIRA/GitHub APIs only on turns where a branch appeared (agent spawned/pushed)
or HEAD moved / a branch was deleted (merge). It is ADVISORY: it reports drift,
never auto-transitions (correct status is a judgment call).

Drift it flags (HIGH-signal only — a noisy hook gets ignored):
  - a ticket with an OPEN PR (branch `asps-<N>-*`) whose status is not
    In Progress/Done -> work is active but the board says otherwise;
  - that ticket's PARENT (epic/story), if still `To Do` while a child is active.
It deliberately does NOT try to infer "should be Done" from a merged branch:
a merged `asps-<N>-*` PR does not mean ticket N is complete (parents span many
PRs; docs/planning branches merge while the work continues) -> too many false
positives. Marking Done stays a human/CEO call at the real completion point.
Exit code is always 0 (a hook must not block); drift is printed to stdout.
"""
from __future__ import annotations

import base64
import json
import os
import re
import subprocess
import sys
import urllib.request
import urllib.error

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MARKER = os.path.join(REPO_ROOT, ".git", "jira_status_check.marker")
ACCESS_KEYS = os.path.join(REPO_ROOT, "ACCESS_KEYS.env")
BRANCH_RE = re.compile(r"^asps-(\d+)", re.IGNORECASE)
DONE_STATUSES = {"Done", "Closed", "Resolved"}


def _run(cmd: list[str]) -> str:
    try:
        return subprocess.run(cmd, cwd=REPO_ROOT, capture_output=True, text=True, timeout=30).stdout
    except Exception:
        return ""


def _git_signature() -> str:
    head = _run(["git", "rev-parse", "HEAD"]).strip()
    branches = _run(["git", "branch", "-r", "--list", "origin/asps-*"]).strip()
    return head + "\n" + "\n".join(sorted(branches.splitlines()))


def _gv(key: str) -> str | None:
    if not os.path.exists(ACCESS_KEYS):
        return None
    for line in open(ACCESS_KEYS, encoding="utf-8"):
        if line.startswith(key + "="):
            return line.split("=", 1)[1].strip().strip('"')
    return None


def _jira_issue(base: str, auth: str, key: str) -> tuple[str | None, str | None]:
    """Return (status_name, parent_key) for a JIRA issue, or (None, None)."""
    req = urllib.request.Request(
        f"{base}/rest/api/2/issue/{key}?fields=status,parent",
        headers={"Authorization": f"Basic {auth}", "Accept": "application/json"},
    )
    try:
        with urllib.request.urlopen(req, timeout=15) as r:
            f = json.load(r)["fields"]
            parent = f.get("parent", {}).get("key") if f.get("parent") else None
            return f["status"]["name"], parent
    except Exception:
        return None, None


def _tickets_from_prs(state: str, limit: int) -> dict[str, str]:
    """Map ASPS-<N> -> branch, from PRs in the given state (open|merged)."""
    out = _run(["gh", "pr", "list", "--state", state, "--limit", str(limit),
                "--json", "headRefName"])
    tickets: dict[str, str] = {}
    try:
        for pr in json.loads(out or "[]"):
            br = pr.get("headRefName", "")
            m = BRANCH_RE.match(br)
            if m:
                tickets.setdefault(f"ASPS-{m.group(1)}", br)
    except Exception:
        pass
    return tickets


def main() -> int:
    sig = _git_signature()
    prev = ""
    try:
        prev = open(MARKER, encoding="utf-8").read()
    except Exception:
        pass
    if sig and sig == prev and "--force" not in sys.argv:
        return 0  # nothing changed -> silent no-op (cheap conversation turns)

    base, email, token = _gv("JIRA_BASE_URL"), _gv("JIRA_EMAIL"), _gv("JIRA_API_TOKEN")
    if not (base and email and token):
        return 0  # no creds here (e.g. sandboxed) -> silent
    auth = base64.b64encode(f"{email}:{token}".encode()).decode()

    open_t = _tickets_from_prs("open", 30)

    drift: list[str] = []
    parents: set[str] = set()
    for key in sorted(open_t):
        st, parent = _jira_issue(base, auth, key)
        if st and st not in DONE_STATUSES and st != "In Progress":
            drift.append(f"  {key}: OPEN PR ({open_t[key]}) but status is '{st}' -> should be In Progress")
        if parent:
            parents.add(parent)
    for pkey in sorted(parents):
        st, _ = _jira_issue(base, auth, pkey)
        if st and st not in DONE_STATUSES and st != "In Progress":
            drift.append(f"  {pkey}: parent of active work but status is '{st}' -> should be In Progress")

    # persist marker regardless, so we don't re-alert the same unchanged state
    try:
        os.makedirs(os.path.dirname(MARKER), exist_ok=True)
        open(MARKER, "w", encoding="utf-8").write(sig)
    except Exception:
        pass

    if drift:
        print("[JIRA-SYNC] status drift vs git reality — reconcile before reporting:")
        print("\n".join(drift))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

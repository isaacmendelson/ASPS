---
name: scrum-863-velopack-decision
description: "SCRUM-863 agent auto-update — the Velopack \"Option B\" architecture decision and its constraints"
metadata: 
  node_type: memory
  type: project
  originSessionId: 79421ab0-5013-4f83-811d-ef1680a5ec72
---

SCRUM-863 (Auto-Update for the Windows desktop agent) uses **Velopack, "Option B"**, decided after a CTO review + a live test that passed on 2026-05-23.

**Key facts (not obvious from code):**
- Velopack's Python `UpdateManager` is feed-based AND throws at construction unless the running app was installed via the Velopack `Setup.exe` (`RuntimeError: Could not auto-locate app manifest`). So it is NOT used.
- Instead the agent shells out to the bundled `Update.exe` CLI: `Update.exe apply --package <our nupkg> --waitPid <pid> --restart`. This applies a package we downloaded+verified ourselves — no feed, no network. **Proven working in a live test 2026-05-23** (swapped a running `--onedir` PyInstaller agent 0.1.1→0.1.2 and relaunched).
- The agent's own backend control plane (Phases 1–5: staged rollout, `RolloutHalted` kill-switch, HMAC download token) is kept — Velopack channels are categorical, not a percentage rollout.
- **Constraint:** every machine's first install must go through `Setup.exe` (silent: `--silent --installto`). This is a real distribution change, not just "add a feature".

**Still open:**
- Build pipeline must move `--onefile`→`--onedir` + `vpk pack` — a separate task; `build_release.py`'s ZIP/Inno steps assume onefile. `_spike/build_test_packages.py` proves the recipe.
- The crash-loop rollback path (`agent_updater._rollback`) is coded but NOT live-verified (live-test step 7 was skipped).

See [[scrum-863-progress]] for phase status. Live-test checklist: `docs/SCRUM-863-velopack-live-test.md`.

---
name: scrum-863-progress
description: SCRUM-863 agent auto-update — phase progress and what remains
metadata: 
  node_type: memory
  type: project
  originSessionId: 79421ab0-5013-4f83-811d-ef1680a5ec72
---

SCRUM-863 (Auto-Update Agent Desktop-Win) — work on branch `863-Auto-Update-Agent-Desktop-Win-to-Latest-Version`.

**Committed (commit `b250774`, 2026-05-23, 28 files +4963/-10, NOT pushed yet):**
- Phase 0 — Velopack spike (GO).
- Phase 1 — shared contract: `DeviceInfo.Version`, `VersionUpdateAvailableNotification`, `VersionUpdateAck`.
- Phase 2 — agent reports `Version` in every `DeviceInfo` (`zmq_client.py`).
- Phase 3 — backend detection: `UserDevice.Version`/`VersionReportedAt`, `AgentUpdateConfiguration` entity, `RealTimeAlertListener` compare + publish + `VersionUpdateAck` routing. Migrations applied to DB.
- Phase 4 — download endpoint (`AgentUpdateController`), HMAC `AgentUpdateTokenService`, `RolloutHalted` kill-switch + staged rollout. Migration applied.
- Phase 5 — agent receives offer, downloads, SHA-256 verifies (`services/agent_updater.py`).
- Phase 6 — agent apply via `Update.exe` CLI, crash-loop marker/rollback, `VersionUpdateAck` send, `velopack.App()` init.
- Phase 7 — buffered messages (`services/alert_buffer.py`, resend on startup).
- QA verdict — PASS (recorded in the commit message).
- Spec: `docs/SCRUM-863-spec.md`; JIRA SCRUM-863 description matches.

**Remaining (NOT in the commit, explicit follow-ups):**
- Phase 8 E2E — real backend offer → agent self-update → ack received (not run; core Velopack mechanism IS proven).
- Build-pipeline rework — `apps/desktop/win/build_release.py` still `--onefile`; must move to `--onedir` + `vpk pack` + first-install via `Setup.exe`. Recipe proven in `_spike/build_test_packages.py`.
- Production `SigningSecret` rotation — dev placeholder in both `appsettings.json`.
- Crash-loop rollback path — coded but not live-tested (live-test Step 7 skipped).
- SCRUM-901 (reverse-engineering protection), SCRUM-902 (code-signing) — separate tickets.

Architecture: see [[scrum-863-velopack-decision]]. JIRA subtasks: SCRUM-896 (backend), SCRUM-897 (Win agent), SCRUM-898/899 (Android/iOS future).

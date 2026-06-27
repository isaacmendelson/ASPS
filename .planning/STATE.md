# State

**Last Updated:** 2026-06-27

## Current Phase

Phase 3 — Production Readiness (חלקי)

## Blockers

| בלוקר | פתרון |
|--------|-------|
| `zappa22` בהיסטוריית git | BFG Repo Cleaner + `git push --force` |
| DB password לא הוחלפה | סיבוב סיסמה + עדכון `.env` בשרת |
| MySQL ללא SSL | `SslMode=Required` ב-connection string |
| HTTPS ל-WebApi | הגדרת certificate |

### Quick Tasks Completed

| # | Description | Date | Commit | Directory |
|---|-------------|------|--------|-----------|
| 001 | Unified ASPS system specification (all 7 subsystems) | 2026-06-27 | 1699aa5 | [001-asps-system-specifications](./quick/001-asps-system-specifications/) |

## Key Decisions

### Architecture
- **RouterSocket במקום RepSocket** — תומך באלפי מכשירים במקביל, תואם ל-`zmq.REQ` קיים
- **Two-phase processing** — Phase 1: ACK מיידי (~1ms). Phase 2: Python/ML analysis ב-`Task.Run`
- **`_sendLock`** — RouterSocket לא thread-safe, כל שליחה תחת lock
- **UserDomainManagerService** — UDAnalysisManager נפרד לכל משתמש, lazy init

### Security
- **CURVE enabled** — `appsettings.Development.json` עם `CurveEnabled: true`
- **Separate public key file** — Backend כותב `curve-server-public-key.txt` (public only). Python קורא ממנו — לא ממנשה ה-keypair המלא
- **Token auth** — Constant-time comparison. `ex.Message` אף פעם לא נשלח ל-client
- **Email verification על RequestToken** — סוגר zero-factor auth vulnerability
- **Localhost filter** — 3 שכבות: Extension JS / Python scan_service / C# backend

### Data
- **EF migrations active** — 51 migrations in `Business/Migrations/`; applied automatically in dev via `db.Database.Migrate()` in `Program.cs` (note: STATE.md previously said "No EF migrations" — this was stale)
- **DeviceTokens table** — `DeviceUid` PK, `TokenValue`, `UserKeyField`, `DateCreated`, `Expiration`; persisted across restarts
- **TokenStore write-through** — in-memory `ConcurrentDictionary` + MySQL persistence; survives backend restart

### Python Desktop
- **Context singleton** — `zmq.Context()` נוצר פעם אחת, נסגר רק ב-shutdown
- **keyring עם fallback** — OS credential manager → file-based אם keyring לא זמין
- **`"MACAddress"` (לא `"MAC"`)** — שם שדה תואם ל-C# `DeviceInfo.MACAddress`

## Environment

| משתנה | ערך |
|--------|-----|
| Repo local | `/root/.openclaw/workspace/asps/repo/ASPSBackend14_J` |
| Gitea | `http://100.92.152.21:3000/admin/asps` |
| DB | MySQL `aspsbackend2db` (local), `ASPSBackend2DB` (Docker) |
| ZMQ ports | 50001 (REQ alerts), 50002 (PUB notifications), 5556 (CQRS) |
| Python device UID | `PC-080a18137ae46ebe` |
| Backend runs via | Visual Studio (multiple startup projects) |

## Git State

| Branch | Commit | Notes |
|--------|--------|-------|
| `main` | `1699aa5` | Latest — unified system specification |
| `test-branch` | `9da4c6a` | Merged into main |

## Session Continuity

**Last session:** 2026-06-27 00:38–01:10 UTC
**Completed:** quick-001 — unified ASPS system specification
**Output:** `docs/system-specifications/ASPS_System_Specification.md`
**Summary:** `.planning/quick/001-asps-system-specifications/001-SUMMARY.md`

## What's Working ✅

- URL alert pipeline end-to-end (extension → backend → analysis → notification)
- CURVE encryption on REQ (port 50001) + SUB (port 50002)
- Device token auth
- Remote access monitoring (TeamViewer, AnyDesk, RDP, 10+ apps)
- Admin dashboard (WebApi + Keycloak OIDC + SignalR)
- Docker Compose (MySQL + Backend + WebApi)
- Hourly CISO security audit cron job
- Roadmap editor (SPA + CQRS + optimistic concurrency)
- UserRiskScore service (SCRUM-904, backend — partial)
- ImmediateDanger detection and notification pipeline

## What Needs Attention

- Tests: 0% coverage — fragile refactoring
- `TypeNameHandling.Auto` in Newtonsoft — deserialization security risk
- `ASView` collections — no locking, race conditions possible under high load
- `LoadDataAsync().Wait()` in ASView — blocking call at startup

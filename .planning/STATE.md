# State

**Last Updated:** 2026-02-28

## Current Phase

Phase 3 — Production Readiness (חלקי)

## Blockers

| בלוקר | פתרון |
|--------|-------|
| `zappa22` בהיסטוריית git | BFG Repo Cleaner + `git push --force` |
| DB password לא הוחלפה | סיבוב סיסמה + עדכון `.env` בשרת |
| MySQL ללא SSL | `SslMode=Required` ב-connection string |
| HTTPS ל-WebApi | הגדרת certificate |

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
- **No EF migrations** — DB נוצר עם raw SQL. שינויי schema דרך SQL ישיר
- **DeviceTokens table** — נוצר ידנית: `DeviceUid` PK, `TokenValue`, `UserKeyField`, `DateCreated`, `Expiration`
- **TokenStore in-memory** — אחרי restart הבאקנד: Python חייב RequestToken מחדש

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
| `main` | `3e9654d` | Latest — localhost URL filter |
| `test-branch` | `9da4c6a` | Merged into main |

## What's Working ✅

- URL alert pipeline end-to-end (extension → backend → analysis → notification)
- CURVE encryption on REQ (port 50001) + SUB (port 50002)
- Device token auth
- Remote access monitoring (TeamViewer detection)
- Admin dashboard (WebApi + SignalR)
- Docker Compose (MySQL + Backend + WebApi)
- Hourly CISO security audit cron job

## What Needs Attention

- Tests: 0% coverage — fragile refactoring
- `TypeNameHandling.Auto` in Newtonsoft — deserialization security risk
- `ASView` collections — no locking, race conditions possible under high load
- `LoadDataAsync().Wait()` in ASView — blocking call at startup

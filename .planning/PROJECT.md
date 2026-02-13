# ASPS - Anti-Scam Protection System

## What This Is

מערכת הגנה מפני פישינג ותרמיות בזמן אמת. מורכבת מ-4 רכיבים: Backend (.NET), אפליקציית Desktop (Python), תוסף Chrome, ו-URL Analyzer חיצוני. המערכת מנתחת כתובות URL שהמשתמש מבקר בהן ומציגה ציון איום בתוסף הדפדפן.

## Core Value

הזרימה המלאה חייבת לעבוד: משתמש מבקר ב-URL -> ניתוח מתבצע -> ציון חוזר ומוצג בתוסף Chrome.

## Current Milestone: v1.1 Cleanup & Fix Communication

**Goal:** ניקוי זבל מהריפוזיטורי ותיקון באגים שמונעים מזרימת הציון לעבוד בפועל.

**Target features:**
- מחיקת קבצים מיותרים (ZIPים, __pycache__, venvs כפולים, build artifacts)
- תיקון באגים קריטיים בתקשורת שנמצאו בסקירת קוד

## Requirements

### Validated

- ✓ Chrome Extension מזהה ניווט לכתובות URL ושולח ל-Desktop App via WebSocket — existing
- ✓ Desktop App מתחבר ל-Backend via ZMQ (port 50001) — existing
- ✓ Backend מקבל alertים ומפעיל ניתוח (indicators, ML, WHOIS, blacklist) — existing
- ✓ Backend מפעיל URL Analyzer חיצוני כסקריפט Python — existing
- ✓ מערכת CQRS עם NetMQ לתקשורת בין WebApi ל-Business layer — existing
- ✓ Google OAuth אימות משתמשים ב-Desktop App — existing
- ✓ דשבורד Admin ב-WebApi עם Razor Pages — existing
- ✓ זיהוי תוכנות Remote Access (AnyDesk, TeamViewer וכו') — existing
- ✓ מערכת Cache ב-Extension ו-Desktop App — existing
- ✓ Lazy Pirate retry pattern ב-ZMQ REQ socket — v1.0 phase 5
- ✓ PendingResults store ב-Desktop App — v1.0 phase 5
- ✓ CurveMQ encryption — v1.0 phase 4
- ✓ asyncio bridge fix (run_coroutine_threadsafe) — v1.0 phase 2
- ✓ Token acquisition flow (RegisterDevice/RequestToken) — v1.0 phase 3

### Active

- [ ] ניקוי קבצים מיותרים מהריפוזיטורי (~1.8GB זבל)
- [ ] תיקון שדה `url` חסר בתגובות scan_service → Extension לא יכול להתאים תוצאות
- [ ] תיקון async/sync mismatch ב-extension_handler.py
- [ ] תיקון message type mismatch ב-content.js (getPageInfo vs page:info:request)
- [ ] תיקון silent broadcast failure ב-notification_handler.py
- [ ] וידוא שהזרימה המלאה עובדת end-to-end

### Out of Scope

- שינוי פורטים — לא משנים פורטים קיימים
- חוב טכני כללי — לא שלנו לתקן
- סיסמאות/אבטחה — לא רלוונטי עכשיו
- פיצ'רים חדשים — המטרה היא לתקן את מה שקיים
- שדרוג טכנולוגי — לא מחליפים frameworks או שפות
- שיפור מבנה תיקיות — רק מחיקת זבל, לא restrucuring
- UI redesign — שומרים על הממשק הנוכחי
- Mobile app — לא רלוונטי כרגע

## Context

**Architecture:**
- Distributed CQRS with Event-Driven Architecture
- 4 processes נפרדים שמתקשרים via ZMQ ו-WebSocket
- ASPSBackend (console app) -> ports: 50001 (alerts), 5555 (business), 5556 (CQRS), 50002 (notifications)
- Desktop App -> WebSocket server on ports 8080-8484
- Chrome Extension -> Manifest V3 service worker

**באגים שנמצאו בסקירת קוד (2026-02-13):**

1. **חסר שדה `url` בתגובות** — `scan_service.py:_create_result()` לא שולח `url` חזרה. ה-Extension לא יכול להתאים תוצאה ל-URL ספציפי → loading אינסופי.

2. **Async/Sync mismatch** — `extension_handler.py` handlers הם sync אבל נקראים מ-async context. חוסם event loop.

3. **Message type mismatch** — `content.js` מגדיר `'getPageInfo'` אבל `ScanService.js` שולח `'page:info:request'`. מידע על trackers/iframes לא מגיע.

4. **Silent broadcast failure** — `notification_handler.py` אם broadcast נכשל (timeout, event loop חסר), שום דבר לא קורה — תוצאה אבודה.

**v1.0 תיקונים שנעשו (פאזות 1-5):**
- asyncio bridge fix (run_coroutine_threadsafe)
- recv_multipart atomicity
- Token acquisition flow
- CurveMQ re-enablement
- Lazy Pirate retry pattern
- PendingResults store
- SW keepalive hardening

**URL Analyzer חיצוני:**
- נמצא ב: `C:\Users\judaz\OneDrive\Desktop\basic-url-analyzer\basic-url-analyzer\basic-url-analyzer`
- Backend מפעיל אותו כסקריפט Python

## Constraints

- **Tech Stack**: C# .NET 8.0, Python 3.8+, JavaScript (Manifest V3) — לא משנים
- **Database**: MySQL 8.0 with EF Core — קיים ועובד
- **Communication**: ZeroMQ (NetMQ/pyzmq) + WebSocket — לא משנים פרוטוקול
- **Security**: ZMQ CurveMQ encryption מוגדר — לשמור

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| לתקן לפני להוסיף | המערכת הבסיסית חייבת לעבוד לפני שמוסיפים פיצ'רים | ✓ Good |
| URL Analyzer חיצוני | Backend מפעיל analyzer כסקריפט Python נפרד | ✓ Good |
| ZMQ לתקשורת Backend | NetMQ/pyzmq עם REQ/REP ו-PUB/SUB | ✓ Good |
| ניקוי רק מחיקה | לא משנים מבנה תיקיות, רק מוחקים זבל | — Pending |
| תיקוני תקשורת מבוססי code review | נמצאו 4 באגים ספציפיים בסקירת קוד | — Pending |

---
*Last updated: 2026-02-13 after milestone v1.1 start*

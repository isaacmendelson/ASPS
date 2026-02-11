# ASPS - Anti-Scam Protection System

## What This Is

מערכת הגנה מפני פישינג ותרמיות בזמן אמת. מורכבת מ-4 רכיבים: Backend (.NET), אפליקציית Desktop (Python), תוסף Chrome, ו-URL Analyzer חיצוני. המערכת מנתחת כתובות URL שהמשתמש מבקר בהן ומציגה ציון איום בתוסף הדפדפן.

## Core Value

הזרימה המלאה חייבת לעבוד: משתמש מבקר ב-URL -> ניתוח מתבצע -> ציון חוזר ומוצג בתוסף Chrome.

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

### Active

- [ ] תיקון זרימת החזרת ציון מ-Backend -> Desktop App -> Extension (הבאג העיקרי)
- [ ] ייצוב תקשורת WebSocket בין Desktop App לתוסף
- [ ] ייצוב תקשורת ZMQ בין Desktop App ל-Backend
- [ ] וידוא שה-Notification Publisher (port 50002) שולח תוצאות חזרה
- [ ] וידוא ש-Desktop App מעביר תוצאות ניתוח לתוסף via WebSocket

- [ ] כתיבת דוח מפורט: מה קרה, למה זה קרה, ואיך לתקן — לצוות השרת

### Out of Scope

- פיצ'רים חדשים — המטרה היא לתקן את מה שקיים
- שדרוג טכנולוגי — לא מחליפים frameworks או שפות
- UI redesign — שומרים על הממשק הנוכחי
- Mobile app — לא רלוונטי כרגע

## Context

**Architecture:**
- Distributed CQRS with Event-Driven Architecture
- 4 processes נפרדים שמתקשרים via ZMQ ו-WebSocket
- ASPSBackend (console app) -> ports: 50001 (alerts), 5555 (business), 5556 (CQRS), 50002 (notifications)
- Desktop App -> WebSocket server on ports 8080-8484
- Chrome Extension -> Manifest V3 service worker

**הבעיה:**
הזרימה המלאה עבדה בעבר. כרגע הציון לא חוזר לתוסף. צריך לאתר איפה בשרשרת הזרימה הדברים נשברים:
1. Extension -> Desktop App (WebSocket) ✓ (כנראה עובד)
2. Desktop App -> Backend (ZMQ REQ port 50001) — לבדוק
3. Backend -> ניתוח + Analyzer חיצוני — לבדוק
4. Backend -> Desktop App (ZMQ PUB port 50002) — חשוד
5. Desktop App -> Extension (WebSocket response) — חשוד

**URL Analyzer חיצוני:**
- נמצא ב: `C:\Users\judaz\OneDrive\Desktop\basic-url-analyzer\basic-url-analyzer\basic-url-analyzer`
- Backend מפעיל אותו כסקריפט Python

**אזורים שבירים (מ-CONCERNS.md):**
- ZMQ REQ/REP דורש חילוף שליחה-קבלה מדויק — תגובה אבודה שוברת socket
- Service Worker של Chrome נהרג אחרי 30 שניות חוסר פעילות
- Extension Message Queue — הודעות עלולות להיות stale
- Heartbeat gap של עד 30 שניות לזיהוי connection מת

## Constraints

- **Tech Stack**: C# .NET 8.0, Python 3.8+, JavaScript (Manifest V3) — לא משנים
- **Database**: MySQL 8.0 with EF Core — קיים ועובד
- **Communication**: ZeroMQ (NetMQ/pyzmq) + WebSocket — לא משנים פרוטוקול
- **Security**: ZMQ CurveMQ encryption מוגדר — לשמור

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| לתקן לפני להוסיף | המערכת הבסיסית חייבת לעבוד לפני שמוסיפים פיצ'רים | — Pending |
| URL Analyzer חיצוני | Backend מפעיל analyzer כסקריפט Python נפרד | ✓ Good |
| ZMQ לתקשורת Backend | NetMQ/pyzmq עם REQ/REP ו-PUB/SUB | ✓ Good |

---
*Last updated: 2026-02-11 after initialization*

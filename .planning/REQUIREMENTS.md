# Requirements: ASPS Score Flow Repair

**Defined:** 2026-02-11
**Core Value:** הזרימה המלאה חייבת לעבוד: URL -> ניתוח -> ציון מוצג בתוסף Chrome

## v1 Requirements

### Communication - תקשורת

- [ ] **COMM-01**: Desktop App מתחבר ל-Backend via ZMQ REQ (port 50001) בהצלחה
- [ ] **COMM-02**: Desktop App מקבל התראות מ-Backend via ZMQ SUB (port 50002)
- [ ] **COMM-03**: Extension מתחבר ל-Desktop App via WebSocket
- [ ] **COMM-04**: ZMQ thread מעביר הודעות ל-asyncio event loop הנכון

### Analysis Flow - זרימת ניתוח

- [ ] **FLOW-01**: URL שנשלח מ-Extension מגיע ל-Backend ומתנתח
- [ ] **FLOW-02**: תוצאת ניתוח חוזרת מ-Backend ל-Desktop App
- [ ] **FLOW-03**: Desktop App מעביר ציון ל-Extension via WebSocket
- [ ] **FLOW-04**: Extension מציג ציון בפופאפ

### Security - אבטחה

- [ ] **SEC-01**: CurveMQ encryption עובד בין Desktop App (pyzmq) ל-Backend (NetMQ)
- [ ] **SEC-02**: מפתחות CURVE מסונכרנים בין הרכיבים

### Reliability - אמינות

- [ ] **REL-01**: ZMQ REQ socket מתאושש מתגובה אבודה
- [ ] **REL-02**: WebSocket reconnection עובד אחרי ניתוק
- [ ] **REL-03**: Chrome service worker שורד keepalive

### Documentation - תיעוד

- [ ] **DOC-01**: דוח באג מפורט — מה קרה, למה, ואיך לתקן — לצוות השרת

## v2 Requirements

### Enhanced Reliability

- **REL-04**: Auto-restart של Desktop App אחרי crash
- **REL-05**: Error telemetry לזיהוי בעיות מרחוק

### Monitoring

- **MON-01**: Health dashboard שמציג סטטוס כל רכיב
- **MON-02**: Alerting על כשלי תקשורת

## Out of Scope

| Feature | Reason |
|---------|--------|
| פיצ'רים חדשים | המטרה היא תיקון בלבד |
| שדרוג frameworks | לא משנים טכנולוגיה |
| UI redesign | שומרים על הממשק הנוכחי |
| Mobile app | לא רלוונטי כרגע |
| Tests מלאים | רק בדיקות שנחוצות לאימות התיקון |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| COMM-01 | Phase 1 | Complete |
| COMM-02 | Phase 2 | Complete |
| COMM-03 | Phase 1 | Complete |
| COMM-04 | Phase 2 | Complete |
| FLOW-01 | Phase 3 | Complete (code) |
| FLOW-02 | Phase 3 | Complete (code) |
| FLOW-03 | Phase 3 | Complete (code) |
| FLOW-04 | Phase 3 | Complete (code) |
| SEC-01 | Phase 4 | Complete |
| SEC-02 | Phase 4 | Complete |
| REL-01 | Phase 5 | Pending |
| REL-02 | Phase 5 | Pending |
| REL-03 | Phase 5 | Pending |
| DOC-01 | Phase 5 | Pending |

**Coverage:**
- v1 requirements: 14 total
- Mapped to phases: 14
- Unmapped: 0

---
*Requirements defined: 2026-02-11*
*Last updated: 2026-02-12 after Phase 4 completion*

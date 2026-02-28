# Roadmap

## Milestone 1 — Core Platform ✅ (בוצע)

### Phase 1 — Infrastructure & Communication ✅
URL alert pipeline end-to-end: Extension → Desktop → ZMQ → Backend → Python Analyzer → ZMQ Notification → Desktop → Extension. Device auth with token. CQRS WebApi↔Backend via NetMQ.

### Phase 2 — Security Hardening ✅
CURVE ZMQ encryption on all sockets. Token-based device auth with OS keyring. Removal of all hardcoded credentials. Separate public key file. Token redaction from logs. Localhost URL filter (3-layer). MAC field name fix. Docker credentials via .env.

### Phase 3 — Production Readiness 🔄 (חלקי)
- [x] Docker Compose with persistent CURVE keys volume
- [x] appsettings.Docker.json sanitized
- [x] .env.example template
- [ ] BFG git history cleanup (`zappa22`)
- [ ] DB password rotation
- [ ] MySQL SSL (`SslMode=Required`)
- [ ] HTTPS for WebApi

---

## Milestone 2 — [ שם עתידי ] 🔜 (לתכנן)

### Phase 4 — [ להגדיר ]
> ממתין לפירוט הפיצ'רים החדשים מאיציק

### Phase 5 — [ להגדיר ]
> ממתין לפירוט

### Phase 6 — [ להגדיר ]
> ממתין לפירוט

---

## Current Position

**עכשיו:** Phase 3 — Production Readiness (חלקי)

**הבלוקר העיקרי:** BFG + password rotation לפני deploy.

**הבא:** לאחר שאיציק יגדיר את הפיצ'רים החדשים — נבנה Milestone 2.

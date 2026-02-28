# Requirements

## v1 — Core Protection Platform (Current)

### URL Threat Detection
- [x] Chrome extension detects page URLs, trackers, iFrames
- [x] Extension sends URL to desktop app via WebSocket
- [x] Desktop app forwards as ZMQ UrlAlert to backend
- [x] Backend validates device token + routes to analyzer
- [x] Python URL analyzer (Playwright, WHOIS, ML, phishing DB)
- [x] Analysis result returned as ZMQ PUB notification to desktop
- [x] Desktop app receives notification and passes to extension
- [x] Extension displays risk score + protective action to user
- [x] URL result cached (TTL-based) to avoid re-analysis

### Remote Access Monitoring
- [x] Desktop app monitors running processes for remote access tools (TeamViewer, AnyDesk, etc.)
- [x] Sends RemoteAccessAlert to backend on detection
- [x] Backend analyzes and flags concurrent remote + suspicious browsing

### Device Authentication
- [x] Device registration flow (RequestToken with email verification)
- [x] Token-based auth for all ZMQ alerts
- [x] OS keyring storage for token (fallback to file)
- [x] DeviceTokens table in MySQL for persistence
- [x] Token expiry (24h default, max 7 days)

### Real-Time Notifications
- [x] ZMQ PUB/SUB notification channel (port 50002)
- [x] CURVE encryption on all ZMQ communication
- [x] Per-device topic subscription: `device:{deviceUid}`
- [x] Notification payload: AnalysisResultNotification with risk + indicators

### Security
- [x] CURVE ZMQ encryption (client + server keypair)
- [x] Separate public key file for client distribution
- [x] Token redacted from all logs
- [x] Localhost/loopback URLs filtered (3-layer: extension, Python, backend)
- [x] No credentials in source code or git

### Admin Dashboard (WebApi)
- [x] Razor Pages admin UI
- [x] View devices, users, alerts, analysis results
- [x] SignalR real-time updates
- [x] Swagger API documentation

### Infrastructure
- [x] Docker Compose (MySQL + ASPSBackend + WebApi)
- [x] Credentials via .env file (not hardcoded)
- [x] CURVE keys persisted in Docker volume

---

## v2 — Features to Add

> ⚠️ המשתמש יפרט את הפיצ'רים הנוספים — הסעיף יעודכן

### [ להגדיר עם איציק ]
- [ ] ?
- [ ] ?
- [ ] ?

---

## Out of Scope (v1)

- Mobile app (Android/iOS)
- Multi-tenant SaaS
- Browser support beyond Chrome
- macOS / Linux desktop client
- SMS / push notifications (beyond ZMQ)
- Paid subscription / billing

---

## Tech Debt Backlog (לא ב-roadmap עדיין)

- [ ] Unit + integration tests (0% coverage currently)
- [ ] Replace `TypeNameHandling.Auto` (Newtonsoft security risk)
- [ ] Fix `ASView` race conditions (no locking on collections)
- [ ] MySQL SSL in production
- [ ] Audit logging
- [ ] Observability / metrics
- [ ] BFG git history cleanup + DB password rotation

# ASPS Desktop Agent — מפרט תוכנה מלא

**גרסה:** 0.1.1.1 | **פלטפורמה:** Windows 10/11 | **שפה:** Python 3.11  
**מיקום קוד:** `apps/desktop/win/src/` | **תאריך:** יולי 2026

---

## 1. סקירה כללית

ה-Desktop Agent הוא תהליך Windows הרץ ברקע ומהווה את הזרוע המקומית של מערכת ASPS. מגן מפני תרחיש שבו רמאי משכנע קורבן להתקין אפליקציית גישה מרחוק (AnyDesk/TeamViewer) ואז ניגש לחשבון הבנק.

### אחריות עיקרית
- **Remote Access Detection** — ניטור AnyDesk, TeamViewer, RustDesk, RDP ו-7 אפליקציות נוספות
- **URL Analysis** — בדיקת URLs שה-Extension שולח, cache מקומי + קריאה לBackend
- **ImmediateDanger Mode** — מצב חירום: session נכנס פעיל + דפדפן עם כניסה לאתר רגיש
- **Extension Relay** — WebSocket server מקומי לתוסף Chrome
- **Browser History Scanning** — סריקת Chrome/Edge/Firefox כל 30 שניות
- **Windows UI** — System Tray, Popup סטטוס, CenteredToast (חלון ImmediateDanger)

---

## 2. ארכיטקטורה

### Threads

| Thread | מה רץ בו |
|---|---|
| Main Thread | pystray icon + customtkinter root.mainloop() |
| asyncio Thread | כל async tasks: remote monitor, browser history, tray update, danger loop |
| ZMQ SUB Thread | NotificationClient._listen() — blocking recv loop |
| Log Watcher Threads | אחד לכל קובץ log שנצפה (AnyDesk/TeamViewer/VNC...) |

### תלויות עיקריות

| ספרייה | שימוש |
|---|---|
| pyzmq | ZMQ REQ/REP + PUB/SUB + CURVE עם Backend |
| websockets | WebSocket server לתוסף Chrome |
| pystray | System Tray icon |
| customtkinter | TrayPopup + CenteredToast UI |
| winotify / win10toast | Windows Toast notifications |
| psutil | Process list, CPU, network connections |
| geoip2 | GeoLite2 country lookup לפי IP |
| keyring | אחסון טוקן ב-Windows Credential Manager |

### קבצים מרכזיים

| קובץ | תפקיד |
|---|---|
| `main.py` | Entry point — AntiScamApp orchestrator |
| `config.py` | כל פרמטרי הקונפיגורציה (מייבא config_override.py) |
| `core/container.py` | DI Container — lazy singleton properties |
| `zmq_client.py` | ZMQ REQ/REP — כל הודעות יוצאות לBackend |
| `notification_client.py` | ZMQ SUB — notifications נכנסים מBackend |
| `extension_server.py` | WebSocket server לתוסף Chrome |
| `remote_monitor.py` | מנוע זיהוי גישה מרחוק (~3,000 שורות) |
| `auth_manager.py` | מחזור חיי הטוקן |
| `services/monitor_service.py` | Async tasks: RA monitor, browser history, tray |
| `services/danger_mode.py` | Singleton flag — מצב ImmediateDanger |
| `services/browser_tabs_policy.py` | מדיניות איסוף browser tabs |
| `handlers/notification_handler.py` | נתב notifications נכנסים מBackend |
| `handlers/extension_handler.py` | נתב הודעות נכנסות מExtension |
| `hardware_id.py` | Device ID — fingerprint חומרה, cached לדיסק |

---

## 3. קונפיגורציה

`config.py` הוא הקובץ הראשי. בסיום, מבצע `from config_override import *` שמאפשר ל-`config_dev.py`/`config_prod.py` לדרוס ערכים. `config_override.py` נוצר ב-build.

### תקשורת ורשת

| פרמטר | ברירת מחדל | תיאור |
|---|---|---|
| `BACKEND_HOST` | `"127.0.0.1"` | כתובת Backend (prod=app.asps.io) |
| `BACKEND_REQ_PORT` | `50001` | ZMQ REQ/REP |
| `BACKEND_SUB_PORT` | `50002` | ZMQ PUB/SUB |
| `BACKEND_SERVER_PUBLIC_KEY_Z85` | מקובץ | מפתח CURVE ציבורי Z85 |
| `EXTENSION_PORTS` | `[8080,8181,8282,8383,8484]` | פורטים לWebSocket server |
| `WEBAPI_URL` | `http://localhost:5001` | לרישום מכשיר |
| `WHITELIST_IPS` | `["127.0.0.1","100.86.253.55"]` | IPים מתעלמים בבדיקת חשד |
| `WHITELIST_PORTS` | `[50001,49569]` | פורטים מתעלמים |

### ניטור

| פרמטר | ברירת מחדל | תיאור |
|---|---|---|
| `MONITOR_INTERVAL` | `5` | מרווח polling fallback (שניות) |
| `IMMEDIATE_DANGER_POLL_INTERVAL_SECONDS` | `3` | polling בזמן ImmediateDanger |
| `IMMEDIATE_DANGER_ALERT_INTERVAL_SECONDS` | `10` | כל כמה שניות danger loop שולח alert מלא עם tabs |
| `BROWSER_TABS_DEFAULT_MODE` | `"incoming_only"` | מדיניות ברירת מחדל לצירוף tabs |
| `BROWSER_TABS_URL_FILTER` | `["","localhost","127.0.0.1"]` | URLs לסינון מtabs |

### אחסון מקומי

| פרמטר | ערך | תיאור |
|---|---|---|
| `DATA_DIR` | `%APPDATA%\AntiScam` | תיקיית נתונים runtime |
| `CACHE_FILE` | `cache.json` | cache תוצאות URL (TTL=3600s) |
| `LOG_FILE` | `events.jsonl` | לוג אירועים JSONL |

### REMOTE_APPS
מילון עם config לכל אפליקציה: `id` (int), `process_names`, `listen_ports`, `service_names`, נתיבי log.  
אפליקציות: **anydesk, teamviewer, rustdesk, chrome_remote_desktop, quick_assist, logmein, connectwise, remotepc, splashtop, rdp**

### CURVE Key Discovery (סדר עדיפויות)
1. `ANTISCAM_CURVE_PUBLIC_KEY` env var
2. `~/.antiscam/curve-public-key.txt`
3. `%LOCALAPPDATA%\ASPS\curve-server-public-key.txt`
4. ריק → חיבור ראשון ללא הצפנה; מפתח מתקבל בתשובת RequestToken

---

## 4. Enumerations (enums.py)

כל enums הם `IntEnum`, משקפים `Common.Enums` ב-Backend.

| Enum | ערכים |
|---|---|
| `RemoteAccessApp` | Unknown=0, AnyDesk=1, TeamViewer=2, ChromeRemoteDesktop=3, RemotePC=4, LogMeIn=5, Splashtop=6, VNC=7 |
| `SessionStatus` | Unknown=0, Open=1, Closed=2 |
| `ConnectionStatus` | Unknown=0, Open=1, Closed=2 |
| `AlertFlagType` | NONE=0, AppRunning=1, ConnectionOpen=2, SessionActive=3 |
| `ProtectiveActionType` | NONE=0, DisplayNotification=1, EmailNotification=2, SoundAlert=3, BlockUrl=4, UserDisplayNotification=5, QuarantineDevice=6, BlockRemoteAccess=7 |
| `Priority` | Low=0, Medium=1, High=2, Critical=3 |
| `DeviceType` | Unknown=0, PersonalComputer=1, SmartPhone=2, Other=3 |
| `OperatingSystemType` | Unknown=0, Windows=1, Linux=2, Mac=3, Android=4, IOS=5 |
| `ResultStatusCode` | Success=200, InvalidOperation=400, ValidationError=422, ServerError=500, Unauthenticated=401, Unauthorized=403, NotFound=404 |

---

## 5. אובייקטי נתונים

### DeviceInfo — משודר בכל הודעה יוצאת

| שדה | סוג | תיאור |
|---|---|---|
| `DeviceUid` | str | `PC-{16 hex chars}` — fingerprint חומרה |
| `DeviceType` | int | PersonalComputer=1 |
| `OperatingSystem` | int | Windows=1 |
| `MACAddress` | str | MAC ראשון שנמצא |
| `IP` | str | IP מקומי — מאוכלס לפני שליחה |
| `ImmediateDanger` | bool | True כאשר danger_mode.active |

### RemoteAppStatus — תוצאת זיהוי לאפליקציה

| שדה | סוג | תיאור |
|---|---|---|
| `app_name` | str | "anydesk", "teamviewer", "rdp"... |
| `app_id` | int | RemoteAccessApp enum |
| `is_running` | bool | התהליך קיים |
| `has_active_session` | bool | session פעיל |
| `process_count` | int | מספר instances |
| `connection_count` | int | חיבורי רשת פעילים |
| `direction` | str? | "incoming" / "outgoing" / "unknown" / None |
| `confidence` | str | "low" / "medium" / "high" |
| `remote_ip` | str? | IP של הצד המרוחק |
| `remote_country` | str? | שם מדינה (GeoIP) |
| `remote_country_code` | str? | קוד ISO 3166 |
| `connection_type` | str? | "direct" / "relay" |
| `remote_id` | str? | AnyDesk numeric ID / TV Partner ID |
| `remote_name` | str? | שם מוצג של הצד המרוחק |
| `remote_os` | str? | OS של הצד המרוחק |
| `remote_version` | str? | גרסת תוכנת גישה מרחוק |
| `file_transfer_active` | bool | העברת קבצים פעילה |
| `file_transfers` | int | מספר העברות שזוהו |
| `logged_user` | str? | משתמש Windows בזמן session (TeamViewer) |
| `connection_id` | str? | GUID session record (TeamViewer) |
| `software` | str? | "AnyDesk"/"TeamViewer"/"VNC"/"ChromeRD" |

### StateChange — שינוי מצב

| שדה | ערכים |
|---|---|
| `app_name` | שם האפליקציה |
| `change_type` | "opened" / "closed" / "session_started" / "session_ended" |
| `timestamp` | datetime UTC |
| `status` | RemoteAppStatus |
| `late_detection` | True כאשר זוהה ב-Startup Scan או חיבור Extension מאוחר |

### CacheEntry — cache לURL

| שדה | תיאור |
|---|---|
| `url` | domain מנורמל (netloc) |
| `score` | 0–100 |
| `risk_type` | list[int] |
| `protective_action` | int |
| `ttl` | שניות לתוקף (default 3600) |
| `saved_at` | Unix timestamp |

### BrowserTab — מהExtension

| שדה | סוג | תיאור |
|---|---|---|
| `TabId` | int | Chrome tab ID |
| `Url` | str | URL נוכחי |
| `IsSensitiveWebsite` | bool | Extension זיהה כאתר רגיש |
| `LoggedIn` | bool? | true=מחובר / false=לא / null=לא ידוע |
| `Timestamp` | ISO-8601 | זמן דיווח |

**LoggedIn semantics:** Backend מפעיל ImmediateDanger רק כאשר `LoggedIn==true`. `null` (לא ידוע) לא מפעיל — מונע false positives מאתרי ממשלה עם session cookies לא מזוהים.

### auth.json — `%APPDATA%\AntiScam\auth.json`

```json
{
  "token": null,
  "user_id": 12345,
  "expires_at": "2026-08-01T12:00:00Z",
  "is_authorized": true,
  "email": "user@example.com"
}
```
`server_public_key` **לא נשמר** — נקרא תמיד מחדש מ-`curve-server-public-key.txt`.

---

## 6. שירותים

### ZMQClient
ממשק יוצא לBackend. כל שליחה: `connect() → send() → recv() → close()`. ה-socket לא נשמר בין קריאות. `threading.Lock` מסדר גישה. Timeout: 5000ms.

**Priority elevation:** כאשר `danger_mode.active`, כל alerts יוצאים עם Priority=CRITICAL.  
**ImmediateDanger stamp:** `ImmediateDanger: True/False` מוצמד ל-DeviceInfo בכל שליחה.

| מתודה | תיאור |
|---|---|
| `send_request_token(device_uid, email)` | אימות מכשיר; מקבל טוקן + CURVE key |
| `send_refresh_token(device_uid, old_token)` | חידוש טוקן שפג |
| `send_url_alert(...)` | בדיקת URL חשוד |
| `send_track_url_alert(...)` | אירוע ניווט בדפדפן |
| `send_remote_access_alert(...)` | שינוי מצב session + כל שדות RemoteAppStatus + BrowserTabs? |
| `send_tab_closed_alert(tab_id, url, ip)` | טאב נסגר (ImmediateDanger) |
| `send_tab_changed_alert(tab_id, url, ...)` | URL שינוי (remote control) + IsSensitiveWebsite, IsLoggedIn |
| `set_server_public_key(key)` | הפעל CURVE |

### NotificationClient
ZMQ SUB socket. מנוי: `"device:{DeviceUid}"`. daemon thread. RCVTIMEO=5000ms.

### ExtensionServer
WebSocket server. מנסה `EXTENSION_PORTS` בסדר. Multi-client. הודעות `heartbeat_ping`, `keepalive`, `browser_tabs_response` מטופלות פנימית.

`request_browser_tabs(timeout=3.0)`: שולח `get_browser_tabs` לכל לקוח, ממתין ל-Futures, ממזג תוצאות.

### AuthManager

| מתודה | תיאור |
|---|---|
| `ensure_authenticated(max_retries=3)` | exponential backoff 2/4/8s |
| `authenticate()` | RequestToken; פותח דפדפן ל-DeviceLogin אם DeviceNotRecognized |
| `refresh_token()` | RefreshToken |
| `handle_auth_response(response)` | מזהה InvalidToken/TokenExpired; re-auth ו-retry |
| `is_expired()` | בדיקת פקיעה עם buffer 5 דקות |

### MonitorService
3 asyncio tasks:

| Task | מרווח | תיאור |
|---|---|---|
| `_monitor_remote_access()` | adaptive (1/2/5/30s); 3s בdanger | loop ראשי לזיהוי RA |
| `_monitor_browser_history()` | 30s | סורק SQLite היסטוריה Chrome/Edge/Firefox |
| `_update_tray_status()` | 5s | מרענן tray וpopup |

**Adaptive Poll Intervals:**
- 1s — pending debounced close/session-end
- 2s — AnyDesk רץ (log parser lag)
- 5s — אפליקציית RA פעילה / session פתוח
- 30s — idle
- 3s (override) — danger_mode.active

**Extension late connect:** כאשר Extension מתחבר post-startup: ממתין 2s, מנסה tabs עד פעמיים, שולח RemoteAccessAlert עם `late_detection=True`.

### ScanService
זרימה: cache hit → return. אחרת: auth → `send_url_alert` → process response. Local URLs דלוגים. על InvalidToken/TokenExpired: re-auth + retry אחד.

### BrowserTabsPolicy
Thread-safe singleton. מצבים: `incoming_only` (default) / `always` / `never`. Backend יכול לשלוח `SetBrowserTabsPolicyNotification` עם `ValidUntil`.

### DangerMode
Thread-safe singleton. `activate()` / `deactivate()`. כאשר active:
- ZMQClient: Priority=CRITICAL
- DebouncedStateTracker: עוקף debounce
- MonitorService: poll קבוע 3s
- DeviceInfo.ImmediateDanger = True

### ProtectionService
מבצע ProtectiveActions מBackend. `BlockRemoteAccess` → `disconnect_remote_session("anydesk")` → `AnyDesk.exe --disconnect`.

---

## 7. תקשורת — Backend (ZMQ)

### ערוץ REQ/REP — פורט 50001

**הודעות יוצאות:**

| AlertType | נשלח כאשר |
|---|---|
| `RequestToken` | startup auth |
| `RefreshToken` | טוקן פג |
| `UrlAlert` | Extension מדווח URL לבדיקה |
| `TrackUrlAlert` | אירוע ניווט; מוסיף FromUrl, Duration, ScamInProgressKey |
| `RemoteAccessAlert` | שינוי מצב session; כולל כל שדות + BrowserTabs? |
| `TabClosedAlert` | טאב נסגר (ImmediateDanger) |
| `TabChangedAlert` | URL שינוי; מוסיף IsSensitiveWebsite, IsLoggedIn |

**מבנה RemoteAccessAlert:**
```json
{
  "AlertType": "RemoteAccessAlert",
  "AlertId": "uuid",
  "Timestamp": "ISO-8601",
  "Priority": 2,
  "Token": "...",
  "DeviceInfo": { "DeviceUid": "PC-...", "DeviceType": 1, "OperatingSystem": 1, "MACAddress": "...", "IP": "...", "ImmediateDanger": false },
  "RemoteAccessApp": 1,
  "ConnectionStatus": 1,
  "SessionStatus": 1,
  "Direction": "incoming",
  "Confidence": "high",
  "RemoteId": "123456789",
  "RemoteCountry": "Israel",
  "RemoteCountryCode": "IL",
  "RemoteOS": "Windows",
  "RemoteVersion": "8.0.0",
  "ConnectionType": "relay",
  "FileTransferActive": false,
  "FileTransfers": 0,
  "Software": "AnyDesk",
  "BrowserTabs": [
    { "TabId": 1, "Url": "https://bankhapoalim.co.il/...", "IsSensitiveWebsite": true, "LoggedIn": true, "Timestamp": "..." }
  ]
}
```

**תשובות:**

| תרחיש | מבנה |
|---|---|
| Async | `{"success": true, "message": "..."}` — תוצאה תגיע ב-SUB |
| Sync (legacy) | `{"Score": int, "RiskType": [...], "ProtectiveAction": int}` |
| Auth error | `{"status": "InvalidToken" | "TokenExpired"}` |
| Token response | `{"status": "TokenCreated|ExistingToken|...", "token": "...", "expiration": "ISO", "serverPublicKey": "Z85"}` |

### ערוץ PUB/SUB — פורט 50002

SUB socket, topic: `"device:{DeviceUid}"`. הודעות: `[topic_bytes, json_bytes]`.

**Notifications נכנסים:**

| Type | פעולה |
|---|---|
| `ImmediateDangerNotification` | מפעיל DangerMode, loop התראות, CenteredToast נעול, broadcast לExtension |
| `ImmediateDangerEndedNotification` | מכבה DangerMode, CenteredToast → ירוק |
| `SetBrowserTabsPolicyNotification` | עדכון mode + ValidUntil |
| `SetTrackedDomainsNotification` | מעביר tracked domains לExtension |
| (URL analysis result) | עדכון cache + ProtectiveActions + broadcast url_result לExtension |

**מעטפת Notification:**
```json
{
  "Type": "ImmediateDangerNotification",
  "Timestamp": "ISO-8601",
  "DeviceUid": "PC-...",
  "Data": { ... }
}
```

---

## 8. תקשורת — Chrome Extension (WebSocket)

Agent = שרת. Extension = לקוח. JSON לשני הכיוונים. Agent מנסה `[8080, 8181, 8282, 8383, 8484]`. Extension אחראי על reconnect.

### הודעות נכנסות (Extension → Agent)

| type | שדות | פעולה |
|---|---|---|
| `url_check` | url, trackers, iframes, tabId, ipAddress | ScanService |
| `track_url_alert` | Url, FromUrl, Duration, ScamInProgressKey, TabId | TrackUrlAlert לBackend |
| `tab_closed_alert` | tabId, url, ipAddress | TabClosedAlert (ImmediateDanger) |
| `tab_changed_alert` | tabId, url, isSensitiveWebsite, isLoggedIn, ipAddress | TabChangedAlert |
| `ping` | — | pong עם email + IP |
| `user_auth` | email | שמירת email |
| `get_user` | — | device_id, signed_in, email |
| `heartbeat_ping` | — | pong (פנימי) |
| `browser_tabs_response` | requestId, tabs[] | resolves Future |

### הודעות יוצאות (Agent → Extension — broadcast)

| type | מתי | שדות מרכזיים |
|---|---|---|
| `url_result` | תוצאת ניתוח | url, score, riskType, protectiveAction, isSensitiveWebsite, fromCache |
| `remote_access_alert` | session_started | toolId, toolName, direction, remoteIP, remote_country, confidence |
| `remote_access_session_end` | session_ended | toolId, toolName |
| `remote_access_app_closed` | app closed | toolId, toolName |
| `set_remote_controlled` | IsDeviceRemoteControlled שינוי | isDeviceRemoteControlled: bool |
| `immediate_danger_started` | ImmediateDangerNotification | dangerKey, userKey, deviceUid, protectiveActions[] |
| `immediate_danger_ended` | ImmediateDangerEndedNotification | dangerKey, endTime |
| `tracked_domains:set` | SetTrackedDomainsNotification | domains[], alwaysSendFormSubmits |
| `get_browser_tabs` | בקשת tabs | requestId UUID |

---

## 9. Remote Access Monitoring

### ארכיטקטורה
שני pipelines: **Log Watchers** (threads, real-time tail) + **Periodic Poll** (`check_all_with_changes()` בכל מחזור adaptive).

### קבצי Log

| אפליקציה | קבצים |
|---|---|
| AnyDesk | `%PROGRAMDATA%\AnyDesk\ad_svc.trace`, `%APPDATA%\AnyDesk\ad.trace`, `connection_trace.txt` |
| TeamViewer | `%APPDATA%\TeamViewer\Connections_incoming.txt`, `Connections.txt`, `TeamViewer15_Logfile.log` |
| VNC | TigerVNC, UltraVNC, TightVNC — נתיבים ספציפיים |
| Chrome Remote Desktop | `%APPDATA%\Google\Chrome Remote Desktop\logs\*.log` |

### Startup Backfill
לפני tail threads: קורא 500 שורות אחרונות, ממיין לפי timestamp, מעביר ל-SessionTracker. Sessions > 10 דקות → נמחקים.

### זיהוי כיוון (Priority)
1. Log Parser — אירועי `incoming_request`, `outgoing_start`, `session_started`, `session_stopped`
2. Network Topology — ESTABLISHED + local port בlisten_ports → INCOMING
3. AnyDesk `connection_trace.txt` — רשומות תוך 30 דקות
4. System port scan — svchost-hosted apps (RDP)

**AnyDesk Relay Race Fix:** "Connecting to `<ID>`" מופיע ~1.25s לפני "Incoming session request". כאשר direction="outgoing" ב-session_started: ממתין עד 1.5s (6×0.25s) לתיקון ל-"incoming".

### DebouncedStateTracker

| שינוי | Debounce | בdanger_mode |
|---|---|---|
| opened | אין | — |
| closed | 1s | מיידי |
| session_started | אין | — |
| session_ended | 4s | מיידי |

### Confidence Scoring

| סיגנל | ניקוד |
|---|---|
| active_connection (ESTABLISHED בפורט) | +2 |
| log_session_active | +2 |
| cpu_active (CPU גבוה לתהליך) | +1 |
| service_running (Windows service) | +1 |

סכום ≥4 → "high" | ≥2 → "medium" | <2 → "low"

### GeoIP
Primary: `GeoLocator` singleton עם `GeoLite2-Country.mmdb` מ-`src/data/`.  
Fallback: HTTP ל-`ip-api.com` (3s timeout, cached).  
IPs פרטיים (10.x, 192.168.x, 172.16–31.x, 127.x, 100.x) מדולגים.

### AnyDesk CLI Integration
`AnyDeskCLI` עוטף `AnyDesk.exe`: `--get-id`, `--get-status`, `--disconnect`.  
`disconnect` מופעל ע"י `BlockRemoteAccess` ProtectiveAction מBackend.

### TeamViewer Forensics
`Connections_incoming.txt`: tab-separated, פורמט `dd-MM-yyyy HH:mm:ss`.  
שדות: PartnerID, DisplayName, StartDate, EndDate, LoggedOnUser, ConnectionType, ConnectionID.  
מאכלס `RemoteAppStatus.logged_user`, `remote_name`, `connection_id`.

---

## 10. ImmediateDanger Mode

### מה מפעיל
Backend שולח `ImmediateDangerNotification` כאשר: session RA נכנס פעיל **ו** אתר רגיש פתוח **ו** `LoggedIn==true` (כניסה מפורשת).

### 5 שלבי Handler
1. `danger_mode.activate()` — Priority=CRITICAL, bypass debounce, poll 3s
2. `start_immediate_danger_loop()` — כל 10s: tabs → RemoteAccessAlert מלא
3. `_build_immediate_danger_details()` — danger_key, sensitive_url, protective_actions
4. `show_display_notification_actions()` — CenteredToast LOCKED (אדום, ללא סגירה, always-on-top)
5. Broadcast `immediate_danger_started` → Extension שולח tab_closed/tab_changed על כל אירוע

כל שלב עטוף ב-`try/except` — כישלון באחד לא מבטל את השאר.

### סגירת ImmediateDanger
Backend שולח `ImmediateDangerEndedNotification` כאשר:
- Session RA הסתיים
- `HasSensitiveBrowserPages==false` (אתר רגיש נסגר / המשתמש התנתק)

Handler: `danger_mode.deactivate()` → עצור loop → `transform_to_cleared()` (אותו חלון, ירוק) → broadcast לExtension.

### Extension בזמן ImmediateDanger
`set_remote_controlled: true` → Extension שולח:
- `tab_closed_alert` — כל סגירת טאב
- `tab_changed_alert` — כל שינוי URL + isSensitiveWebsite + isLoggedIn

Agent מעביר מיד לBackend (לא מחכה ל-10s tick).

---

## 11. אבטחה ואימות

### Device ID (hardware_id.py)
פורמט: `PC-{16 hex chars SHA-256}`. מקור (priority):
1. Win32_BaseBoard.SerialNumber + Win32_BIOS.SerialNumber + Win32_DiskDrive[0].SerialNumber
2. `HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid`
3. `platform.node() + platform.machine()`

Cached: `%APPDATA%\AntiScam\device_id`

### CURVE ZMQ Encryption
כל תקשורת ZMQ מוצפנת עם CurveZMQ (X25519 + ChaCha20-Poly1305).

זרימה:
1. Agent קורא server public key (Z85) מקובץ
2. `ZMQClient.connect()`: ephemeral keypair (`zmq.curve_keypair()`), מגדיר CURVE_PUBLICKEY/SECRETKEY/SERVERKEY
3. לאחר auth ראשוני: `container.apply_curve_keys()` → מעביר key לNotificationClient
4. אם `serverPublicKey` בתשובת Token → ZMQClient מתעדכן
5. Key **לא נשמר** ב-auth.json

### אחסון טוקן

| שכבה | פרטים |
|---|---|
| ראשי — OS Keyring | Windows Credential Manager. Service: "AntiScamApp", Account: device_uid |
| Fallback | auth.json (עם אזהרה) כאשר keyring לא זמין |
| לא נשמר | server_public_key |

### מחזור חיי הטוקן
1. Startup: `ensure_authenticated(max_retries=3)`, backoff 2/4/8s
2. Buffer פקיעה 5 דקות
3. `DeviceNotRecognized` → פתיחת דפדפן ל-DeviceLogin (פעם אחת)
4. `InvalidToken`/`TokenExpired` בalert → re-auth → retry פעם אחת
5. Auth כשל לחלוטין → background thread, retry כל 30s

### חוב אבטחה ידוע
- WebSocket לExtension רץ על `ws://` (ללא TLS) — localhost loopback
- ZMQ ports 5555/5556 ב-Backend bound ל-`*:`
- `WHITELIST_IPS` עם IP hardcoded `100.86.253.55`

---

## 12. Browser Tab Tracking

### מתי נאספים Tabs

| אירוע | מה נשלח | Policy |
|---|---|---|
| session_started | tabs נוכחיים | לפי policy |
| session_ended | `[]` (ריק) | תמיד — ניקוי cache Backend |
| app_opened (עם session) | tabs נוכחיים | לפי policy |
| Extension connected מאוחר | tabs נוכחיים | לפי policy |
| ImmediateDanger periodic (10s) | tabs נוכחיים | תמיד, ללא תלות בpolicy |

**Session end שולח `[]`** — מנקה BrowserTabs cache ב-Backend. ללא זה, tabs מסשן ישן גורמים ל-false positive ImmediateDanger.

### מדיניות

| Mode | התנהגות |
|---|---|
| `incoming_only` (default) | אוסף רק כאשר direction=="incoming" |
| `always` | אוסף בכל RemoteAccessAlert |
| `never` | מחזיר None — שדה לא נשלח |

### Browser History Scanning (פאסיבי)
כל 30s, SQLite history Chrome/Edge/Firefox (temp-file copy). מסנן 5 דקות אחרונות, dedup ב-`_seen_urls`. עובד גם ללא Extension.

---

## 13. ממשק משתמש

### System Tray

| צבע | מצב |
|---|---|
| אפור | מתחיל / לא מוגן |
| ירוק | Backend מחובר |
| צהוב | Extension מחובר, לא Backend |
| אדום | Session נכנס / ImmediateDanger |

תפריט ימני: Show Status, Dashboard, Preferences (Settings / View Logs), About, Exit.

### TrayPopup (borderless window)
280×320px. Borderless. Always-on-top. 97% opacity. סגירה על focus-out (150ms delay).  
מכיל: Hero row (צבע + מצב), Remote Access section (Stop Session), System Status, Footer.

### CenteredToast — חלון ImmediateDanger
Singleton. לכל היותר חלון אחד.

| מצב | התנהגות |
|---|---|
| LOCKED | אדום. ללא Close. Alt+F4 חסום. Draggable. Lift loop 2s. View Details → DangerDetailsWindow |
| CLEARED | ירוק. כפתור Close. ממתין לסגירה ידנית. **אותו חלון** — update in-place |

**DangerDetailsWindow:** CTkToplevel 560×420px. Started, Remote App, Direction, Sensitive URL, Device, User, Danger Key, Protective Actions. גוף scrollable.

### Windows Toast
`winotify` (מועדף) / `win10toast` (fallback).  
Audio: none/low=Default, medium=Reminder, high/critical=LoopingAlarm.

---

## 14. Startup / Shutdown

### Startup
1. `get_or_create_device_id()` — hardware fingerprint
2. `AntiScamApp.__init__()` — Container DI, wireup callbacks
3. `extension_server.start()` — WebSocket על הפורט הראשון הפנוי
4. `auth_manager.ensure_authenticated()` — עד 3 ניסיונות
5. `container.apply_curve_keys()` — CURVE key ל-NotificationClient
6. `notification_client.start()` — daemon thread ZMQ SUB
7. אם auth נכשל: `_background_reconnect()` thread (כל 30s)
8. `monitor_service.start()` — 3 asyncio tasks
9. `remote_monitor.startup_scan()` — מצב נוכחי + alerts
10. `tray_icon.run_blocking()` — **Main Thread blocking** — pystray + CTk mainloop

### Shutdown
1. Tray Exit / KeyboardInterrupt → `_on_exit()`
2. `self._running = False`
3. `monitor_service.stop()` — cancel tasks
4. `extension_server.stop()` — סגירת WebSocket clients
5. `notification_client.stop()` — עצירת SUB thread
6. `zmq_client.destroy()` — ביטול ZMQ Context
7. `tray.stop()` — destroy popup + quit CTk

### Build
`build_release.py --env prod` → מעתיק `config_prod.py → config_override.py` → PyInstaller EXE יחיד.  
Installer: Inno Setup. מתקין ב-Program Files, Startup registration, סמל Desktop.

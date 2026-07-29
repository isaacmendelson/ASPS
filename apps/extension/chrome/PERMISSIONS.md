# Extension Permission Audit — ASPS-625 (EX-8)

Manifest version: 3

Each permission below lists the justification, which feature requires it,
and the risk / minimisation note.

---

## `permissions` (declared in manifest.json)

### `activeTab`
**Justification:** Required to get the URL of the currently-focused tab when
the user clicks the popup "Scan Page" button or when an auto-scan is triggered.
Without this, `chrome.tabs.get()` returns an error for the active tab.
**Feature:** Core URL scan.
**Risk:** Minimal — only grants access to the tab the user explicitly interacted
with, and only while the popup is open or during the event that granted it.

### `tabs`
**Justification:** Required to enumerate open tabs so the background service
worker can (a) send the "remote access warning" overlay to every tab, (b)
respond to the agent's `WS_GET_BROWSER_TABS` request with the list of open
URLs, (c) clean up per-tab storage when a tab closes, and (d) load the
correct per-tab cached score when the user switches tabs.
**Feature:** Remote-access warning propagation, browser-tabs response, tab-score
caching.
**Risk:** Medium — grants access to all tab titles and URLs. Required for the
core multi-tab protection feature. Future minimisation option: if the agent's
browser-tabs request is removed, `tabs` could potentially be replaced by
`activeTab` for most flows.

### `webNavigation`
**Justification:** `chrome.webNavigation.onCompleted` and
`onHistoryStateUpdated` are the reliable hooks for detecting SPA (single-page
app) navigation, which does not fire `chrome.tabs.onUpdated`. Without this,
URL scans are missed on sites like React/Vue apps that use `pushState`.
**Feature:** Automatic URL scan on navigation.
**Risk:** Medium — grants full navigation event stream. No content is read;
only the URL is used to trigger a scan.

### `storage`
**Justification:** `chrome.storage.local` stores per-tab scan scores, user
email (received from the desktop agent), connection state, and the feedback
consent flag. `chrome.storage.session` is used by `MessageQueueService` to
persist the message queue across service-worker terminations.
**Feature:** All stateful features — scan results, queue reliability,
consent tracking.
**Risk:** Low — data stays on-device and is never synced to Google servers
(`storage.sync` is not used).

### `notifications`
**Justification:** Used by `handleNotification()` in background.js to surface
backend-pushed notifications (e.g., "risk elevated" alerts) to the user via
the OS notification system when the popup is not open.
**Feature:** Backend-pushed risk notifications.
**Risk:** Low — limited to displaying text notifications. No data leaves the
device via this permission.

### `alarms`
**Justification:** MV3 service workers terminate after ~30 s of inactivity.
`chrome.alarms` is the only MV3-compliant mechanism to wake the service worker
on a schedule for heartbeat, reconnect, and keepalive ticks.
**Feature:** WebSocket keepalive / reconnect reliability.
**Risk:** Low — only schedules internal wake-ups; no user data is involved.

### `cookies`
**Justification:** Used by the logged-in detection pipeline
(`checkLoggedInByCookies`) to determine whether the user is authenticated on
a given site. This signal is combined with DOM-based detection and sent to the
backend as part of `WS_BROWSER_TABS_RESPONSE` and `WS_TAB_CHANGED_ALERT` to
help the backend evaluate whether a scam is in progress (e.g., a victim logged
into their bank while a remote-access tool is active).
**Feature:** Logged-in detection for scam risk evaluation.
**Risk:** Medium — grants read access to cookies for all `<all_urls>` origins.
This is the most sensitive permission. Minimisation options are blocked by the
core logged-in detection requirement. The cookie values themselves are not
transmitted; only the presence of an auth-shaped cookie name is evaluated.
A future improvement could filter cookie reads to the `sensitiveDomainCache`
set (domains the backend has already classified as sensitive) to reduce surface.

---

## `host_permissions`

### `<all_urls>`
**Justification:** Required by three separate features:
1. `content_scripts` — the content script must inject into every page so the
   user sees the remote-access warning overlay and so the DOM-based logged-in
   detection can run.
2. Auto-scan on navigation — `triggerScan()` must be able to scan any URL the
   user visits, not just a pre-defined list.
3. `cookies` permission — `chrome.cookies.getAll({ url })` and
   `chrome.cookies.onChanged` require host_permissions for the URL being
   queried.
**Feature:** Content-script injection, URL scanning, cookie-based logged-in
detection.
**Risk:** High breadth — applies to every website. This is the correct scope
for a security-tool extension whose purpose is to inspect every URL the user
navigates to. Narrowing to specific domains would defeat the product's purpose.

---

## Content scripts

```json
"matches": ["<all_urls>"],
"js": ["content.js"],
"css": ["content.css"],
"run_at": "document_end"
```

**Justification:** The content script must be injected into every page to:
- Display the remote-access warning overlay (RemoteAccessWarning) when the
  desktop agent detects an incoming remote control connection.
- Respond to `CHECK_LOGGED_IN_REQUEST` messages from the background worker so
  the DOM-based logged-in signal is available.
- Monitor form submissions on tracked domains (FormMonitor) for the
  TrackUrlAlert pipeline.

`run_at: document_end` (not `document_start`) minimises execution before the
page is interactive — reduces the chance of interfering with page load order.

---

## Permissions NOT requested (and why)

| Permission | Why not requested |
|---|---|
| `identity` | Auth is handled by the desktop agent; the extension receives email via WebSocket pong, not OAuth. |
| `history` | Not needed — browser history is read by the Python desktop agent directly (not the extension). |
| `bookmarks` | Not used. |
| `downloads` | Not used. |
| `geolocation` | Not used. |
| `clipboardRead/Write` | Not used. |
| `management` | Not used. |
| `nativeMessaging` | Connection to the desktop agent uses WebSocket (`ws://localhost`), not native messaging. |

---

*This file was created as part of ASPS-625 (EX-8) and must be kept in sync
with `manifest.json`. Review when any permission is added or removed.*

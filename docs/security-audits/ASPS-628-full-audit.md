# ASPS-628 — Independent CISO Security Audit

**Date:** 2026-07-30
**Scope:** Full codebase audit — all 4 components
**Auditor:** Security / CISO agents (4 parallel auditors)
**Epic:** ASPS-607 — Top-Level Code Review Remediation (final task, 21/21)

---

## Executive Summary

The ASPS codebase has undergone 20 remediation tasks since the initial code review. Critical infrastructure — CQRS authenticated channels (HMAC-SHA256 + CURVE), SSRF protection (4-layer defense-in-depth), device auth rate limiting, and EF Core parameterized queries — is well-implemented. However, **10 Blocker findings** remain, primarily: committed secrets in git-tracked Docker config files (4), a plaintext notification channel fallback (1), insecure OAuth token storage (1), dead/broken Google auth code (1), and analyzer API misconfigurations (3). **19 Major findings** span PII/URL logging at INFO level, hardcoded admin usernames, exposed debug endpoints, and unauthenticated API surfaces.

**Verdict: FAIL** — 10 Blockers and 19 Majors require remediation before production deployment.

---

## Findings Summary

| Component | Blockers | Majors | Minors | Nits | Total |
|---|---|---|---|---|---|
| Backend | 4 | 9 | 12 | 2 | 27 |
| Desktop Agent | 3 | 3 | 5 | 2 | 13 |
| Browser Extension | 0 | 2 | 8 | 2 | 12 |
| Analyzer | 3 | 5 | 7 | 1 | 16 |
| **Total** | **10** | **19** | **32** | **7** | **68** |

---

## Findings by Component

### Backend (ASPSBackend14_J/)

| # | Severity | File:Line | Exploit path | Remediation |
|---|---|---|---|---|
| B1 | **Blocker** | `ASPSBackend/appsettings.Docker.json:7` | Committed DB password (`password=zappa22`) in git-tracked file. Any attacker with repo access gets the database password. | Add `appsettings.Docker.json` to `.gitignore`, remove from git tracking, rotate password, use env vars or Docker secrets. |
| B2 | **Blocker** | `WebApi/appsettings.Docker.json:19-20` | Committed Keycloak client secret and CQRS shared secret in git-tracked file. | Same as B1. Rotate the Keycloak client secret immediately. |
| B3 | **Blocker** | `WebApi/appsettings.Docker.json:3` | CQRS shared secret hardcoded in tracked config. `docker-compose.yml` correctly uses env var for backend, but WebApi Docker config hardcodes it. Attacker can forge HMAC-signed CQRS command envelopes. | Remove hardcoded secret. Use env var override. |
| B4 | **Blocker** | `docker-compose.yml:8` | Hardcoded MySQL root password (`zappa22`) in committed docker-compose. Combined with port 3307 exposure, attacker with network access can connect as root. | Use Docker secrets or gitignored `.env` file for `MYSQL_ROOT_PASSWORD`. |
| B5 | **Major** | `WebApi/Program.cs:101` | `RequireHttpsMetadata = false` hardcoded (not environment-conditional). In production, allows OIDC token validation over HTTP — MITM on Keycloak metadata enables forging admin tokens. | Move to config. Set `true` in production, `false` only for local dev. |
| B6 | **Major** | `WebApi/Program.cs:63-66` + `Services/AdminClaimsTransformer.cs:8` | Hardcoded admin usernames `{"asps-admin", "isaac", "admin"}` grant Admin role. If Keycloak allows self-registration, any user creating account named "admin" gets full access. Duplicated in two places. | Remove hardcoded list. Rely solely on Keycloak groups/roles. |
| B7 | **Major** | `WebApi/Pages/DebugClaims.cshtml.cs:6` | DebugClaims page is `[AllowAnonymous]`. Exposes all user claims (groups, roles, email, sub) to anyone who can reach the page. | Remove `AllowAnonymous`. Restrict to Admin or remove for production. |
| B8 | **Major** | `WebApi/Program.cs:176-181` | All user claims dumped to `Console.WriteLine` on every authentication — tokens, group memberships, emails to stdout/log aggregators. | Remove claims enumeration loop. Log only username and isAdmin. |
| B9 | **Major** | `Business/Messaging/NetMQMessageProcessor.cs:80` | Full message JSON logged at Information level — entire deserialized command/query payload containing PII (names, emails, phone numbers). | Log message type only, not payload. |
| B10 | **Major** | `Business/Messaging/NetMQMessageProcessor.cs:89` | Exception messages returned to clients in error responses — can leak internal paths, class names, DB schema. | Return generic error messages. Log details server-side only. |
| B11 | **Major** | `Business/Messaging/CQRSGateway.cs:86,133` | Exception messages returned to CQRS clients — leak internals via Gateway/Processing error responses. | Return generic errors. Log details server-side only. |
| B12 | **Major** | `WebApi/Pages/DeviceLogin.cshtml.cs:163` | Full registration JSON logged at Information level including email and device UID. | Log only message type and device UID. |
| B13 | **Major** | `WebApi/Pages/DeviceLogin.cshtml.cs:183` | Full backend response JSON logged at Information level — may contain tokens. | Log only response status. |
| B14 | **Minor** | `Business/Messaging/RealTimeAlertListener.cs:517` | User email logged at Information level on every device registration — PII exposure. | Log at Debug level or redact email. |
| B15 | **Minor** | `Business/Views/ASView.cs:187` | Browsed URLs logged at Information level — sensitive browsing history of protected users. | Log at Debug level, domain only. |
| B16 | **Minor** | `Business/RealtimeAnalysis/UserDomain/UDUrlAnalyzer.cs:82` | Analyzed URLs logged at Information level — browsing-history exposure. | Log at Debug level, domain only. |
| B17 | **Minor** | `WebApi/Controllers/AlertsController.cs:72-73` | URL and DeviceUid logged at Information level on every TrackUrlAlert. | Log at Debug level. |
| B18 | **Minor** | `WebApi/Pages/Login.cshtml.cs:62-77` | Dev-mode cookie auth grants Admin to any username when Keycloak not configured. If config accidentally removed in production, any login succeeds as Admin. | Refuse to start in production without Keycloak. |
| B19 | **Minor** | `Dockerfile.backend:68` + `Dockerfile.webapi:53` | Containers run as root — no USER directive. Container escape + root = host compromise. | Add non-root user and USER directive. |
| B20 | **Minor** | `WebApi/Program.cs:88-89` | Auth cookie `SecurePolicy = SameAsRequest` — cookie sent over HTTP in dev and potentially on internal hops in production. | Set `CookieSecurePolicy.Always` in production. |
| B21 | **Minor** | All connection strings | MySQL `SslMode=None` — DB connections unencrypted. Mitigated by Docker bridge but dangerous in non-localhost. | Use `SslMode=Required` for production. |
| B22 | **Minor** | `WebApi/Services/NetMQClientService.cs:62,67` | Full JSON payload logged at Debug level. If Debug enabled in production, all CQRS payloads including PII written to logs. | Log message type and size, not payload. |
| B23 | **Minor** | `ASPSBackend/Program.cs:315` | User full name logged to console at startup — PII in startup logs. | Log user key/ID instead of name. |
| B24 | **Minor** | `Business/Data/EF/Repositories/EntityRepositories.cs:57` | User PII in debug `Console.WriteLine` — FirstName, LastName, Key for every user. | Remove or guard behind preprocessor directive. |
| B25 | **Minor** | `docker-compose.yml:61-62` | Keycloak admin password hardcoded as `admin`. If used as production template, trivially compromised. | Use env var for Keycloak admin password. |
| B26 | **Nit** | `ASPS.Tests/WebApi/Services/JsonSerializationTests.cs:23` | `TypeNameHandling.Auto` in test code with misleading comment — gateway uses `.None`, not `.Auto`. | Update comment. |
| B27 | **Nit** | `Business/Services/CurveKeyManager.cs:188` | CURVE public key logged at Warning level — creates noise. | Log at Information level. |

**Backend positive findings:**
- CQRS channel security well-implemented (HMAC-SHA256, nonce replay protection, timestamp validation, client/command allowlists, constant-time comparison)
- CURVE encryption mandatory — `CQRSGateway.Start()` throws if keys missing, cannot silently downgrade
- Zero SQL injection — no `FromSqlRaw`, `ExecuteSqlRaw`, or string-concatenated SQL; all DB access through EF Core
- No unsafe deserialization in production — all `TypeNameHandling.None`; previous `.All` fixed (ASPS-66)
- Device auth has rate limiting and owner-verification on re-registration
- Authorization architecture: `FallbackPolicy = Admin`, Razor pages default to `AdminPolicy`
- Docker analyzer container has excellent hardening (read_only, cap_drop ALL, no-new-privileges, pids_limit, mem_limit, tmpfs noexec)

---

### Desktop Agent (apps/desktop/win/)

| # | Severity | File:Line | Exploit path | Remediation |
|---|---|---|---|---|
| D1 | **Blocker** | `notification_client.py:106-113` | NotificationClient (SUB socket) **silently falls back to plaintext** when `server_public_key` is None. The REQ client in `zmq_client.py:161` correctly refuses, but the SUB client proceeds without CURVE. Attacker on local network can inject fake ImmediateDanger notifications, manipulate tracked-domains, override browser-tabs policy. | Apply same "refuse without CURVE" pattern from `zmq_client.py:161-168`. Raise RuntimeError if no server key. |
| D2 | **Blocker** | `google_auth.py:92-109` | Google OAuth tokens (access_token, refresh_token) stored in **plaintext JSON** (`~/.antiscam/google_token.json`) with no filesystem ACL hardening. On a machine under remote access (the scenario ASPS protects against), attacker reads tokens and impersonates the user. | Use keyring pattern from `auth_manager.py`. Store tokens in Windows Credential Manager. |
| D3 | **Blocker** | `google_auth.py:18` + `config.py` | `google_auth.py` imports `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET` from `config`, but config.py never defines them. Either module is broken/dead-code, or secrets were embedded in a build artifact. If OAuth `client_secret` is in a shipped binary, it's extractable. | If GoogleAuth is used: move env-var lookup to config.py, use PKCE (public client) flow. If dead code: remove `google_auth.py` entirely. |
| D4 | **Major** | `extension_server.py:144-148` | WebSocket server binds to localhost with **no origin checking, no authentication, no message validation** beyond JSON parsing. Any local process can connect to `ws://localhost:8080-8484` and inject messages treated as if from the Chrome extension — `url_check`, `user_auth`, `remote:close_session`, `tab_closed_alert`. | Add origin validation (reject non-extension origins). Add shared-secret handshake. |
| D5 | **Major** | `curve_diagnostic.py:196` | Diagnostic tool prints first 20 characters of auth token: `token[:20]...` — significant fragment capturable from logs/terminal scrollback. | Print only `token[:8]...` or use `[REDACTED]`. |
| D6 | **Major** | `auth_manager.py:145-170` | When keyring unavailable (`_KEYRING_AVAILABLE = False`), auth token written in **plaintext to auth.json** with default permissions. On a compromised machine, attacker reads token and impersonates the device. | Refuse to store without keyring, or encrypt via DPAPI (`win32crypt.CryptProtectData()`), or set permissions to 0600. |
| D7 | **Minor** | `zmq_client.py:307` | CURVE server public key's first 20 bytes logged — leaks key material. | Log only key length or hash. |
| D8 | **Minor** | `extension_handler.py:79-87` + `scan_service.py:142` | URLs from WebSocket forwarded with minimal validation. `_is_local_url` blocks only loopback. `file://`, `ftp://`, `javascript:`, or internal IPs (`192.168.x.x`) could cause SSRF. Legacy `url_check` handler lacks scheme whitelist (unlike `message_envelope.py:20`). | Add URL scheme whitelist (http/https only) and reject private IP ranges. |
| D9 | **Minor** | `remote_monitor_cli.py:67-73` + `hardware_id.py:54` | `subprocess.run()` with `shutil.which()` PATH resolution — malicious PATH directory could intercept. | Low practical risk. Prefer absolute paths for defense-in-depth. |
| D10 | **Minor** | `event_logger.py:49-51` | Event log (`events.jsonl`) contains full URLs, app names, remote IPs with default permissions and no rotation limit. Provides full surveillance trail on compromised machine. | Restrictive permissions, consider encrypting at rest. |
| D11 | **Minor** | `browser_history.py:235-276` | Browser history temp SQLite copy inherits default permissions — race condition for reading before deletion. | Set restrictive permissions on temp file. |
| D12 | **Minor** | `notification_client.py:166-292` | Extensive notification details printed to stdout (URLs, risk scores, analysis results). | Reduce verbosity, gate behind `DEBUG_MODE`. |
| D13 | **Nit** | `zmq_client.py:242-244` | Full alert JSON printed (URLs, device UID, MAC, IP) — token redacted but metadata exposed. | Gate behind `DEBUG_MODE`. |
| D14 | **Nit** | `config.py:353` | `WEBAPI_URL = "http://localhost:5001"` uses HTTP — device UID sent in login URL query string unencrypted. | Change default to `https://localhost:5002`. |

**Desktop positive findings:**
- CURVE encryption on REQ socket is mandatory — `zmq_client.py:160-168` raises RuntimeError if no server key
- Token redaction in logs — `zmq_client.py:242` and `auth_manager.py:133,189` consistently redact
- Keyring integration — Windows Credential Manager via keyring when available
- No command injection — all `subprocess.run()` uses list-form arguments, no `shell=True`
- All 46 dependencies pinned in `requirements.lock.txt`
- Envelope validation — schema version, UUID format, URL canonicalization, immutable context
- `.env` and `config_override.py` are gitignored

---

### Browser Extension (apps/extension/chrome/)

| # | Severity | File:Line | Exploit path | Remediation |
|---|---|---|---|---|
| E1 | **Major** | `content.js:183,205,248` | **XSS via innerHTML** with server-controlled values. Legacy WarningService injects `score` from backend WebSocket message into `innerHTML` template literals without escaping. If WebSocket compromised or agent passes unsanitized data, enables script injection. Newer `RemoteAccessWarning.js` properly uses `escapeHTML()` + Shadow DOM. | Switch to DOM API (`createElement` + `textContent`) or migrate to Shadow-DOM-based `RemoteAccessWarning` pattern. |
| E2 | **Major** | `ConnectionService.js:498` + `AuthService.js:20` | **User email (PII) logged to console in plaintext.** Any extension or devtools on the machine can read. On shared/compromised machines, leaks user identity. | Remove or redact email from log output. |
| E3 | **Minor** | `manifest.json:15` | `<all_urls>` host_permissions + `cookies` permission = broadest cookie access. Documented in PERMISSIONS.md. Compromised extension update would have full cookie-stealing capability. | Accepted risk. Consider limiting cookie reads to sensitive domains only. |
| E4 | **Minor** | `content.js:190,218-219,267,271` | Inline `onclick` handlers in legacy WarningService — CSP-hostile pattern. | Migrate to `addEventListener`. |
| E5 | **Minor** | `MessageBus.js:62-105` | No sender validation on `chrome.runtime.onMessage`. Safe by MV3 design (no `externally_connectable`), but no defense-in-depth. | Add guard: verify `sender.id === chrome.runtime.id`. |
| E6 | **Minor** | `background.js:137-177` | All open tab URLs, titles, login status, user-agent sent to desktop agent over `ws://localhost` — highly sensitive data unencrypted. | Known debt. Ensure never sent to external endpoints. |
| E7 | **Minor** | `ConnectionService.js:87` | WebSocket connects to `ws://localhost` (unencrypted). All scan results, browsing URLs, user email, form metadata traverse plaintext. | Known debt. Implement `wss://` with cert pinning. |
| E8 | **Minor** | `content.js:678-679` | Form monitoring logs domain and tracked status to console — reveals monitoring state to anyone with devtools. | Remove or gate behind debug flag. |
| E9 | **Minor** | `popup.js:473,497` | `innerHTML` used for consent prompt in popup — content is static/hardcoded (no current XSS), but fragile pattern. | Prefer DOM API. Low urgency. |
| E10 | **Minor** | `background.js:418-425` | Notification title/message from desktop agent not validated — compromised agent could display arbitrary content for social engineering. | Validate/sanitize, enforce max lengths, prefix with "[AntiScam]". |
| E11 | **Nit** | `manifest.json` | No explicit `content_security_policy` declared — MV3 defaults are strict, but explicit CSP is best practice. | Add `"content_security_policy": { "extension_pages": "script-src 'self'; object-src 'none'" }`. |
| E12 | **Nit** | `ScanService.js:41,46` | Skipped URLs logged to console — could leak local/internal URLs. | Use `console.debug`, omit URL value. |

**Extension positive findings:**
- No `eval()`, `new Function()`, or dynamic script loading
- No `externally_connectable` — web pages cannot message the extension
- No `window.postMessage` listeners — no cross-origin message attack surface
- No fetch/XHR to external services — all through local WebSocket
- Closed Shadow DOM on RemoteAccessWarning — page JS cannot access
- MutationObserver re-injection defense with debounce/rate-limiting
- Friction controller (7s timer + checkbox) prevents hasty warning bypass
- `escapeHTML()` properly used in `RemoteAccessWarning.js`
- No secrets, API keys, or tokens in any committed file
- Form monitoring collects metadata only (field types, not values)
- Feedback service strips URL path — only domain sent

---

### Analyzer (Analyzers/basic-url-analyzer/)

| # | Severity | File:Line | Exploit path | Remediation |
|---|---|---|---|---|
| A1 | **Blocker** | `api.py:23-28` | CORS: `allow_origins=["*"]` + `allow_credentials=True` simultaneously. Any origin can send credentialed requests. If network-exposed, any website can use the analyzer as an oracle. This combination is explicitly prohibited by the CORS spec. | Remove `allow_credentials=True` with wildcard origins, or restrict origins to known callers. |
| A2 | **Blocker** | `api.py:105-106` | `/analyze` POST catches all exceptions and returns `HTTPException(status_code=500, detail=str(e))` — leaks internal paths, stack traces, library names to remote callers. | Return generic error message. Log full exception server-side. |
| A3 | **Blocker** | `core/ml_classifier.py:295-296` | ML model loaded via `pickle.load(f)` — pickle deserialization executes arbitrary Python code. `.pkl` file tracked in git. Malicious PR modifying model file = arbitrary code execution on every startup. | Replace with safe serialization (joblib, ONNX). Add SHA-256 integrity check before loading. |
| A4 | **Major** | `api.py:34-36` | `AnalyzeRequest` accepts `url: str` with no length limit, no protocol restriction. Multi-megabyte URLs consume memory and regex processing time before downstream validation. `/analyze/raw` passes URL directly without response model constraint. | Add `max_length=2048` to Pydantic field. Add protocol validation at API boundary. |
| A5 | **Major** | `api.py:115-120` | `GET /analyze?url=...` endpoint — URLs logged by servers/proxies/CDNs, susceptible to CSRF via `<img src="...">`. | Remove GET endpoint or restrict to non-production. Analysis via POST only. |
| A6 | **Major** | `api.py:109-112` | `/analyze/raw` returns complete internal analysis dict (scraping status, error traces, ML details, warnings). Described as "private Unix socket" but exposed on same app with no auth. | Protect with auth, or bind to separate app on Unix domain socket only. |
| A7 | **Major** | `api.py:126` + `scripts/install-service.ps1:80` | API binds to `0.0.0.0:8000` — exposed to all interfaces. Combined with no auth, any remote attacker can use as SSRF proxy. | Bind to `127.0.0.1`. Add API key requirement. |
| A8 | **Major** | `.gitignore` (missing) | `.gitignore` does not exclude `.venv/` or `.env`. No protection against accidental `git add .` committing virtual env or secrets. Security rules require `.env` to be gitignored. | Add `.venv/`, `.env`, `*.key`, `*.pfx` to `.gitignore`. |
| A9 | **Minor** | `result.json:1` | Committed file contains local path `C:\Users\judaz\OneDrive\Desktop\...` — developer username disclosure. | Remove from VCS, add to `.gitignore`. |
| A10 | **Minor** | `=8.0` (root file) | File literally named `=8.0` tracked in git — artifact of broken pip command. Discloses developer username. | Remove from VCS, add to `.gitignore`. |
| A11 | **Minor** | `scrapers/playwright_scraper.py:293-295` | No content-size limit on fetched pages. Malicious URL returning multi-GB HTML fully loaded into memory. Timeout provides partial protection but not on fast connections. | Add response size limit (abort if content-length > 10MB). |
| A12 | **Minor** | `utils/logger.py:21-36` + multiple modules | Full analyzed URLs logged at INFO level across URLInspector, PlaywrightScraper, WhoisChecker, ReputationChecker. URLs may contain PII, auth tokens in query strings. | Log URLs at DEBUG only, or redact query parameters. Log domain only at INFO. |
| A13 | **Minor** | `core/analyzer.py:99` | Analyzer logs "Starting analysis" at DEBUG (comment: "URLs are potentially sensitive") but sub-modules log full URL at INFO — policy not enforced. | Enforce URL-sensitivity policy across all modules. |
| A14 | **Minor** | `utils/cache_manager.py:17-33` | Cache uses relative path, stores full analysis results including URLs in plain JSON with no access controls. | Use absolute path, consider encrypting cache. |
| A15 | **Minor** | `api.py:31` | `ScamAnalyzer` initialized as module-level singleton — not thread-safe (synchronous Playwright). Under load, attacker can exhaust workers for DoS. `workers=1` only in `__main__`, not in service installer. | Document single-worker limitation. Add bounded request queue. |
| A16 | **Nit** | `scam_site_html.txt` | 111KB HTML dump from scam site tracked in git — dev artifact. | Remove from git, add to `.gitignore`. |

**Analyzer positive findings:**
- SSRF protection (ASPS-609) is **excellent** — 4-layer defense-in-depth:
  1. Application-layer: `URLSecurityPolicy` validates every IP against `ipaddress.is_global`, blocks private/loopback/link-local/multicast/reserved/6to4
  2. Egress proxy: DNS resolution + IP validation in one step, prevents TOCTOU DNS rebinding
  3. Browser context guards: all Chromium requests validated, post-connection peer addresses checked
  4. Container iptables: network-level firewall blocking all private/reserved CIDRs before dropping root
- Comprehensive SSRF test coverage (`test_ssrf_security.py`, `test_egress_proxy.py`)
- No `--no-sandbox` in Chromium launch
- `bypass_csp=False` and `ignore_https_errors=False` explicitly set

---

## Known Security Debt (documented in CLAUDE.md)

| Item | Documented | Status | Notes |
|---|---|---|---|
| NetMQ port 5555 bound to `tcp://*:5555` | Yes | Unchanged | Internal CQRS processor. No CURVE on this port. |
| NetMQ port 5556 (CQRS Gateway) | Yes | **Improved** | Now has CURVE + authenticated envelopes (HMAC + nonce + timestamp). |
| MySQL 3306 exposed | Yes | Unchanged | Docker maps to 3307. Still uses root with weak password (see B4). |
| `ws://` extension↔agent (ports 8080-8484) | Yes | Unchanged | All extension↔desktop communication is plaintext. No auth, no origin check (see D4). |
| Analyzer bound to `0.0.0.0:8000` | **Not documented** | New finding (A7) | Must be explicitly accepted or fixed. |
| Analyzer API — no authentication | **Not documented** | New finding | Must be explicitly accepted or fixed. |
| CORS wildcard + credentials on analyzer | **Not documented** | New finding (A1) | Must be explicitly accepted or fixed. |

---

## Top 10 Priority Remediation

| Priority | Finding | Component | Why |
|---|---|---|---|
| 1 | B1-B4 | Backend | **Committed secrets** — rotate immediately, gitignore Docker configs |
| 2 | D1 | Desktop | **Plaintext notification fallback** — most sensitive channel (ImmediateDanger) |
| 3 | A1-A2 | Analyzer | **CORS misconfig + error leakage** — trivially exploitable if network-exposed |
| 4 | A3 | Analyzer | **Pickle deserialization** — supply-chain RCE vector |
| 5 | D2-D3 | Desktop | **Google OAuth token storage** — plaintext or dead code with latent risk |
| 6 | B6 | Backend | **Hardcoded admin usernames** — privilege escalation if self-registration enabled |
| 7 | A7 | Analyzer | **API bound to 0.0.0.0 with no auth** — open SSRF proxy |
| 8 | D4 | Desktop | **WebSocket no auth/origin check** — local process injection |
| 9 | B7 | Backend | **DebugClaims AllowAnonymous** — information disclosure |
| 10 | B9-B13 + D5 | Backend/Desktop | **PII/URL logging** — betrays mission of protecting vulnerable users |

---

## Recommendations

1. **Immediate (before any deployment):** Rotate all committed secrets (B1-B4). Add `appsettings.Docker.json` to `.gitignore`. Use env vars or Docker secrets for all passwords, client secrets, and shared secrets.

2. **Short-term (next sprint):** Fix plaintext notification fallback (D1), CORS misconfiguration (A1), error message leakage (A2, B10-B11), pickle deserialization (A3), remove or fix Google auth dead code (D2-D3), remove DebugClaims `AllowAnonymous` (B7), fix hardcoded admin usernames (B6).

3. **Medium-term:** Bind analyzer to localhost (A7), add API authentication, add WebSocket origin validation and shared-secret handshake (D4), add non-root USER directives to Dockerfiles (B19), enforce URL-sensitivity logging policy across all components.

4. **Long-term:** Upgrade `ws://` to `wss://` with certificate pinning, implement `SslMode=Required` for MySQL, add explicit CSP to extension manifest, implement DPAPI encryption for local credential storage.

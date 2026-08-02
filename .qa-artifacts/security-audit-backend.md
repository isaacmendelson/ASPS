# Security Audit -- Backend (ASPSBackend14_J/)

**Date:** 2026-07-30
**Component:** Backend (.NET 8)
**Auditor:** Security Agent (ASPS-628)

---

## Findings

| # | Severity | File:Line | Exploit path | Remediation |
|---|---|---|---|---|| 1 | **Blocker** | ASPSBackend/appsettings.Docker.json:7 | Committed secret: DB password in git-tracked file. appsettings.Docker.json is tracked in git and contains the password in the connection string. Any attacker with repo access gets the database password. | Add appsettings.Docker.json to .gitignore, remove from git tracking, rotate the DB password, use env vars or Docker secrets. |
| 2 | **Blocker** | WebApi/appsettings.Docker.json:19-20 | Committed secret: Keycloak client secret and CQRS shared secret in git-tracked file. | Same as 1. Rotate the Keycloak client secret. |
| 3 | **Blocker** | WebApi/appsettings.Docker.json:3 | Committed secret: CQRS shared secret hardcoded in tracked config. docker-compose.yml correctly uses env var for backend, but WebApi Docker config hardcodes it. Attacker reading repo can forge authenticated CQRS commands. | Remove hardcoded secret. Use env var override. |
| 4 | **Blocker** | docker-compose.yml:8 | Hardcoded MySQL root password in committed docker-compose.yml. Combined with port 3307 exposure, attacker with network access can connect as root. | Use Docker secrets or gitignored .env file for MYSQL_ROOT_PASSWORD. |
| 5 | **Major** | WebApi/Program.cs:101 | RequireHttpsMetadata = false is hardcoded (not environment-conditional). In production, allows OIDC token validation over HTTP, enabling MITM on Keycloak metadata. Attacker intercepting metadata can supply malicious JWKS and forge admin tokens. | Move to config. Set true in production, false only for local dev. |
| 6 | **Major** | WebApi/Program.cs:63-66 + Services/AdminClaimsTransformer.cs:8 | Hardcoded admin usernames (asps-admin, isaac, admin) grant Admin role to anyone authenticating with these names. If Keycloak allows self-registration, any user creating account named admin gets full admin access. Duplicated in two places. | Remove hardcoded username list. Rely solely on Keycloak groups/roles. |
| 7 | **Major** | WebApi/Program.cs:45 + Pages/DebugClaims.cshtml.cs:6 | DebugClaims page is AllowAnonymous. Renders authenticated user full claim set (groups, roles, email, sub). Should not exist in production. | Remove AllowAnonymous. Restrict to Admin or remove page for production. |
| 8 | **Major** | WebApi/Program.cs:176-181 | All user claims logged to Console.WriteLine on every authentication. Writes tokens, group memberships, emails to stdout/log aggregators in production. | Remove claims enumeration loop. Log only username and isAdmin. |
| 9 | **Major** | Business/Messaging/NetMQMessageProcessor.cs:80 | Full message JSON logged at Information level. Entire deserialized command/query payload containing PII (names, emails, phone numbers). | Log message type only, not payload. |
| 10 | **Major** | Business/Messaging/NetMQMessageProcessor.cs:89 | Exception messages returned to clients in error responses. ex.Message can leak internal paths, class names, DB schema. | Return generic error messages. |
| 11 | **Major** | Business/Messaging/CQRSGateway.cs:86,133 | Exception messages returned to CQRS clients. Leak internals via Gateway error and Processing error responses. | Return generic errors. Log details server-side only. |
| 12 | **Major** | WebApi/Pages/DeviceLogin.cshtml.cs:163 | Full registration JSON logged at Information level including email and device UID. | Log only message type and device UID. |
| 13 | **Major** | WebApi/Pages/DeviceLogin.cshtml.cs:183 | Full backend response JSON logged at Information level. May contain tokens. | Log only response status. |
| 14 | **Minor** | Business/Messaging/RealTimeAlertListener.cs:517 | User email logged at Information level on every device registration. PII exposure. | Log at Debug level or redact email. |
| 15 | **Minor** | Business/Views/ASView.cs:187 | Browsed URLs logged at Information level. Sensitive browsing history of protected users. | Log at Debug level, domain only. |
| 16 | **Minor** | Business/RealtimeAnalysis/UserDomain/UDUrlAnalyzer.cs:82 | Analyzed URLs logged at Information level. Browsing-history exposure. | Log at Debug level, domain only. |
| 17 | **Minor** | WebApi/Controllers/AlertsController.cs:72-73 | URL and DeviceUid logged at Information level on every TrackUrlAlert. | Log at Debug level. |
| 18 | **Minor** | WebApi/Pages/Login.cshtml.cs:62-77 | Dev-mode cookie auth grants Admin to any username when Keycloak not configured. If Keycloak config accidentally removed in production, any login succeeds as Admin. | Refuse to start in production without Keycloak, or require dev-mode secret. |
| 19 | **Minor** | Dockerfile.backend:68 + Dockerfile.webapi:53 | Containers run as root. No USER directive. Container escape + root = host compromise. | Add non-root user and USER directive. |
| 20 | **Minor** | WebApi/Program.cs:88-89 | Auth cookie SecurePolicy = SameAsRequest. Cookie sent over HTTP in dev and potentially on internal hops in production. | Set CookieSecurePolicy.Always in production. |
| 21 | **Minor** | All connection strings | MySQL SslMode=None. DB connections unencrypted. Mitigated by Docker bridge but dangerous in non-localhost. | Use SslMode=Required for production. |
| 22 | **Minor** | WebApi/Services/NetMQClientService.cs:62,67 | Full JSON payload logged at Debug level. If Debug enabled in production, all CQRS payloads including PII written to logs. | Log message type and size, not payload. |
| 23 | **Minor** | ASPSBackend/Program.cs:315 | User full name logged to console at startup. PII in startup logs. | Log user key/ID instead of name. |
| 24 | **Minor** | Business/Data/EF/Repositories/EntityRepositories.cs:57 | User PII in debug Console.WriteLine. FirstName, LastName, Key for every user. | Remove or guard behind preprocessor directive. |
| 25 | **Minor** | docker-compose.yml:61-62 | Keycloak admin password hardcoded as admin. If used as production template, trivially compromised. | Use env var for Keycloak admin password. |
| 26 | **Nit** | ASPS.Tests/WebApi/Services/JsonSerializationTests.cs:23 | TypeNameHandling.Auto in test code with misleading comment. Gateway uses .None, not .Auto. | Update comment to reflect production uses .None. |
| 27 | **Nit** | Business/Services/CurveKeyManager.cs:188 | CURVE public key logged at Warning level. Public key is not secret but Warning creates noise. | Log at Information level. |

---

## Known Debt (documented in CLAUDE.md)

| Item | Status | Notes |
|---|---|---|
| NetMQ port 5555 bound to tcp://*:5555 | Documented | Internal CQRS processor. No CURVE. Network-accessible if firewall misconfigured. |
| NetMQ port 5556 (CQRS Gateway) | Improved | Now has CURVE + authenticated envelopes (HMAC + nonce + timestamp). |
| MySQL 3306 exposed | Partially mitigated | Docker compose maps to 3307. Still uses root with weak password. |
| ws:// extension-to-agent | Documented | Out of scope for this backend audit. |

---

## Positive Findings (defenses in place)

1. CQRS channel security is well-implemented. HMAC-SHA256 with nonce replay protection, timestamp validation, client ID allowlist, command allowlist. Constant-time signature comparison.
2. CURVE encryption enforced. CQRSGateway.Start() throws if CURVE keys missing. CQRSClient.CreateSocket() throws if CURVE disabled. Cannot silently downgrade.
3. No SQL injection. No FromSqlRaw, ExecuteSqlRaw, or string-concatenated SQL. All DB access through EF Core parameterized queries.
4. No unsafe deserialization in production. All production JsonConvert.DeserializeObject uses TypeNameHandling.None. Previous .All explicitly fixed (ASPS-66). No BinaryFormatter.
5. Device token validation. Alert processing validates token before accepting. Rate limiting on token/registration endpoints. Re-registration attack prevented (owner check).
6. Authorization architecture. FallbackPolicy = Admin role. Razor pages default to AdminPolicy. API controllers inherit FallbackPolicy.
7. CURVE key lifecycle. Keys stored outside repo. File permissions restricted on Linux. Private key never logged.
8. Docker security (analyzer). Excellent hardening: read_only, cap_drop ALL, no-new-privileges, pids_limit, mem_limit, tmpfs with noexec.

---

## Summary

Verdict: FAIL -- 4 Blocker findings, 9 Major findings.

Critical path:
- Secrets in committed files (Blockers 1-4) require immediate rotation and .gitignore updates.
- Hardcoded admin usernames (Major 6) create a privilege escalation path.
- PII/URL logging (Majors 8-13) violate the mission of protecting vulnerable users privacy.

Strongest defense: The CQRS authenticated channel (HMAC + CURVE) is well-designed and correctly prevents unauthorized command execution.

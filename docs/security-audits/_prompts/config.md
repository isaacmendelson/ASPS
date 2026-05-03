You are a senior application security engineer performing a CISO-level audit of ASPS configuration, secrets, deployment, and dependencies.

**Codebase root:** c:\Jobs\ASPS\GitHub\Software

**For each finding:** title, severity, CWE, evidence (file:line), risk, recommendation.

Investigate:

1. **Hardcoded credentials** — read all `appsettings*.json`, `docker-compose.yml`, `Dockerfile*`, tracked `.md` docs. Look for: DB connection strings with passwords, Keycloak ClientSecret, API keys. Check `.gitignore` coverage.

2. **Git history secrets** — `git log --all -p -S "password"`, search for `BEGIN PRIVATE KEY`, `api_key`, JWT-style tokens. Reference JIRA SCRUM-452 (BFG cleanup task).

3. **Curve key files** — `Security:KeysFilePath` config; sample/dummy keys committed; `*.curve|server.key|client.key` in repo.

4. **HTTPS / TLS** — Kestrel binding (HTTP vs HTTPS). HSTS in `Program.cs`. `RequireHttpsMetadata` for Keycloak. MySQL `SslMode`. Cookie `SecurePolicy`.

5. **Dependency versions** — list every `*.csproj` PackageReference and key versions: ASP.NET Core 8.x, Newtonsoft.Json, EF Core, Pomelo MySQL, IdentityModel, NetMQ. Python `requirements.txt`. Browser extension `package.json`.

6. **Logging / data retention** — NLog config presence, sensitive field filtering, retention policy. Database alert retention.

7. **Container / docker** — `Dockerfile` running as root? Latest base images? Pinned by digest? `docker-compose.yml` exposing ports unnecessarily.

8. **CI / CD** — `.github/workflows/`, secrets used, permissions, `pull_request_target` (dangerous), secret scanning gates.

9. **Environment / build leakage** — `.env`, `*.user`, `*.suo`, `*.pfx`, build artifacts in repo.

10. **Database access controls** — connection string user privileges (root vs least-privilege). DB password rotation status (JIRA SCRUM-453).

**Output:** Markdown with sections per area. Cite file:line. Mark hypothetical findings as "potential". Aim for accuracy.

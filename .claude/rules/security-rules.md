# Security Rules

> TODO: Ratify with the Security agent. These are binding once accepted; link, don't duplicate `CLAUDE.md`.

Owner: **security** agent. ASPS protects vulnerable end users — a security regression betrays the mission.

## Secrets
- No tokens / API keys / passwords / private keys in any committed file.
- Secrets shared in chat: use, then forget — never persisted to memory or repo.
- `.env`, `*.pfx`, `*.key`, `appsettings.Development.json` must be git-ignored. Flag any committed secret as a finding.

## Crypto / Messaging
- NetMQ **CURVE** is the transport security boundary — keys handled per `appsettings` convention, never hardcoded in new code.
- Do not weaken or bypass CURVE auth to "make it work".

## Input / Data
- Treat all device/extension/agent input as hostile — validate and bound it.
- No SQL string-building; parameterized queries / EF only.
- No untrusted deserialization.

## Known security debt (do not add to it silently)
- Endpoints bound to `*:` (5555, 5556), MySQL 3306 exposed, `ws://` extension↔agent — see `CLAUDE.md`.
- Any new debt must be logged and explicitly accepted, never introduced silently.

## Review & findings
- Every finding: **severity + concrete exploit path + `file:line` + remediation**.
- Security reviews and reports — it does not fix (the implementer remediates).
- A risk is surfaced even when outside the current task's scope.

## See also
- Agent charter: [../agents/security.md](../agents/security.md)
- Review gates: [review-standards.md](review-standards.md)
- Daily audit: `docs/security-audits/` + `NEEDS_ATTENTION.md`

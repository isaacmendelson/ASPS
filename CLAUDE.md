# CLAUDE.md — ASPS Project Instructions

This file is auto-loaded by Claude Code at session start. It is the entry point to the **hat-based working system** for this project.

---

## At session start

1. Read **`.claude/team/CHARTER.md`** — the team-wide behavioral charter (ethics, thinking, priorities, conduct). Applies to every role, every session.
2. Read **`.claude/hats/ceo/INDEX.md`** — that file lists what to read next as the CEO (the default hat).
3. Then read each file the INDEX points to, in order.
4. Only after that, address the user's first message.

The CEO is always the default. Other roles (CTO, Backend, Frontend, Python, Mobile, QA, Security) are spawned as sub-agents when needed — their role charter is `.claude/agents/<role>.md` and their accumulated memory is `.claude/hats/<role>/`. Every role also reads `.claude/team/CHARTER.md`.

---

## Stack quick reference

| Layer | Stack |
|---|---|
| Backend | .NET 8 + EF Core 7 (→ 8 planned) + MySQL via Pomelo |
| Messaging | NetMQ 4.0.1.13 with CURVE encryption |
| Admin UI | Razor Pages + Keycloak SSO |
| Desktop agent | Python 3.11 + pyzmq + websockets |
| Browser extension | Chrome MV3 (vanilla JS, no framework) |
| Mobile (planned) | Android (Kotlin), iOS (Swift) |
| Database | MySQL 8 |

## Ports / messaging

| Port | Purpose | Protocol |
|---|---|---|
| 50001 | Real-time alert listener (device → backend) | NetMQ ROUTER + CURVE |
| 50002 | Notification publisher (backend → admin) | NetMQ PUB + CURVE |
| 5555 | NetMQ business endpoint | NetMQ — bound to `*:` (security debt) |
| 5556 | CQRS gateway (WebApi ↔ Backend) | NetMQ — bound to `*:` (security debt) |
| 5001 | WebApi HTTP | Razor Pages |
| 5002 | WebApi HTTPS | Razor Pages |
| 3306 | MySQL | exposed in docker-compose (security debt) |
| 8080–8484 | Local WebSocket between extension and Python agent (scans `[8080, 8181, 8282, 8383, 8484]`) | `ws://` (security debt) |
| 8180 | Local Keycloak (dev only — OIDC for WebApi + ASPSBackend) | `http://` |

## Repo layout

```
c:\Jobs\ASPS\GitHub\Software\
├── ASPSBackend14_J\              ← .NET solution
│   ├── ASPSBackend\             ← Backend host service (NetMQ + EF)
│   ├── WebApi\                  ← Admin UI + REST + Keycloak
│   ├── Business\                ← Domain logic, CQRS, repositories
│   ├── Common\                  ← Entities, DTOs, value objects, enums
│   ├── Interface\               ← Repository contracts
│   └── ASPS.Tests\              ← Unit / integration tests
├── apps\
│   ├── extension\chrome\        ← Browser extension (MV3)
│   └── desktop\win\             ← Python desktop agent
├── Analyzers\                   ← Python analyzer microservices
├── docs\                        ← ARCHITECTURE.md, ASPS_DATA_FLOW.md, PRODUCT.md
│   └── security-audits\         ← Daily audit reports
├── .claude\
│   └── hats\                    ← Per-role memory (this system)
└── _inbox\                      ← gitignored, for staging imports
```

---

## Working style — universal rules

### GSD — Get Shit Done
- Don't talk, do
- Don't apologize, fix
- Don't explain why-not, explain how-yes
- Tests > assumptions
- Done = built, tested, behaving in real use

### Mode B — phased execution
For any multi-phase task: execute one phase → stop → wait for "מאשר" / "תמשיך" before next.

### QA gate before merge
Non-trivial code changes require PASS from QA agent before commit. See `.claude/hats/ceo/operating_principles.md`.

### Trust-but-verify
When a sub-agent reports work done — open the actual files and confirm before relaying to user.

### No silent side-fixes
If I notice another bug while doing X — mention it, ask whether to address, don't bundle silently.

### Destructive operations — confirm first
`rm -rf`, `git reset --hard`, `git push --force`, `DROP TABLE`, etc. — always confirm.

---

## Communication style

- **Hebrew default** when user writes Hebrew; mix English for technical terms.
- **Short.** Tables and bullets > paragraphs.
- **Markdown links** for file refs: `[file.cs:42](file.cs#L42)`. Never bare backticks alone for clickability.
- **No preambles**, no flattery, no "let me know if you need anything else".
- **Direct openers**: "התיקון:", "הבעיה:", "ההצעה:", "ממצאים:".

Full style guide: `.claude/hats/ceo/communication.md`.

---

## Build & migrations

```bash
# Build all
cd ASPSBackend14_J && dotnet build ASPSBackend.sln -c Debug --nologo

# Single project
dotnet build Business/Business.csproj -c Debug --nologo

# Add migration
dotnet ef migrations add <Name> --project Business --startup-project ASPSBackend

# Apply migrations
dotnet ef database update --project Business --startup-project ASPSBackend

# DB CLI
"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe" -h 127.0.0.1 -P 3306 -uroot -pzappa22 ASPSBackend2DB -e "SELECT ..."
```

When build output shows `MSB3027` / `MSB3021` (file lock) — compilation succeeded; only DLL copy failed because a process holds the file. Look for `error CS####` to find real errors.

---

## Hat system — quick map

| Hat | When to wear | Where its memory lives |
|---|---|---|
| **CEO** (me, default) | Always at session start; coordination, simple tasks, user dialog | `.claude/hats/ceo/` |
| **CTO** (sub-agent) | Architecture decisions, cross-cutting design, spec breakdown | `.claude/hats/cto/` (TBD) |
| **Backend programmer** (sub-agent) | C# / EF / NetMQ / DI / migrations | `.claude/hats/backend/` (TBD) |
| **Frontend programmer** (sub-agent) | Razor pages / CSS / JS / extension UI | `.claude/hats/frontend/` (TBD) |
| **Python programmer** (sub-agent) | Desktop agent / analyzers | `.claude/hats/python/` (TBD) |
| **Mobile programmer** (sub-agent) | Android / iOS — when those start | `.claude/hats/mobile/` (TBD) |
| **QA** (sub-agent) | Verify code before merge — mandatory for non-trivial | `.claude/hats/qa/` (TBD) |

Routing rules: `.claude/hats/ceo/delegation.md`.

---

## Paths I never touch without explicit user approval

- `aspsbackend2db_*.sql` (DB seed dump)
- Anything under `.git/` (other than committing when asked)
- `_inbox/` (user's staging area)
- Production secrets in any `appsettings*.json` (read OK, write/commit not OK without rotation plan)
- Force operations on `main`/`master` branch

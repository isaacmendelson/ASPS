# Load-Bearing Decisions

Decisions made in past sessions that remain in effect. Future-me must respect these unless explicitly overturned.

## Architecture

| Date | Decision | Rationale | Effect |
|---|---|---|---|
| 2026-04-28 | **Roadmap entity stores data as a single JSON blob (LONGTEXT)** in MySQL row, not normalized into items/categories tables (Approach A). | Faster build, simpler editor binding, single roadmap per row is small. | All Roadmap features go through `Data` field. Migrations don't normalize. |
| 2026-04-28 | **Roadmap admin uses `AdminPolicy` from existing Razor convention.** | No new auth surface. | `WebApi/Pages/Roadmaps/*` inherit `AuthorizeFolder("/", "AdminPolicy")`. |
| 2026-05-01 | **Daily security audit pattern: 3 parallel general-purpose agents (Backend / Clients / Config-Secrets) + synthesis.** | Each agent ~150K tokens, parallelizes well, each has clean focus. | `docs/security-audits/` directory holds dated reports + `NEEDS_ATTENTION.md` flag. |
| 2026-05-02 | **`ImmediateDanger` modeled as TPH abstract entity** with `ImmediateDangerByRemoteAccess` as the first concrete subclass. | Future subclasses (e.g., `ImmediateDangerByPhishingClick`) without separate tables. | One `ImmediateDangers` table with `Discriminator` column. |
| 2026-05-02 | **`ProtectiveAction[]` persisted as JSON in TEXT column** (`ProtectiveActionsJson`), with a `[NotMapped]` getter that deserializes on read. | EF Core 7 doesn't natively persist arrays of complex objects. | All ProtectiveAction roundtrips go through Newtonsoft.Json. |
| 2026-05-02 | **`Key` (Common.Models.Key) is a value object — never an EF navigation.** | EF would otherwise try to map it as an entity, throwing "needs PK" errors. | Any property typed `Key` must be `[NotMapped]`. The FK column is a separate `string KeyField`. Store `key.Value` (just the GUID), not `key.ToString()` (which is `"Type#Value"` and overflows varchar(36)). |
| 2026-05-03 | **Hat-based memory system established.** | Each role accumulates its own learnings; isolated sub-agents on demand. | Memory at `<repo>/.claude/hats/<role>/`. CEO hat exists; others build out as needed. |

## Process

| Date | Decision | Rationale |
|---|---|---|
| Ongoing | **Mode B: stop after each phase, wait for explicit user approval before next.** | User's explicit preference. |
| Ongoing | **QA gate before merge of non-trivial code.** | Catches drift between intent and implementation. |
| 2026-05-03 | **Daily 05:00 security audit cron** (set up 2026-05-03; session-only — does not survive Claude restart per current CronCreate behavior). | Continuous baseline awareness of regressions. |

## Security baselines (from 2026-05-03 audit)

These are known issues. Future code review must flag if any are reintroduced or worsened:

- **`TypeNameHandling.None`** is the rule for outbound CQRS serialization. `Auto` on the inbound side is open security debt — replace with `None` + explicit type dispatch, or strict `ISerializationBinder`.
- **ZMQ ports 5555/5556** must bind to `tcp://127.0.0.1:` not `tcp://*:` (current code is wrong; tracked).
- **No payload size / `MaxDepth` limits** anywhere — DoS surface, tracked.
- **MySQL `SslMode=None`** — tracked, requires cert provisioning.
- **Hardcoded admin allow-list** in `AdminClaimsTransformer` and `Program.cs:132` — must be removed before production.
- **Dev-mode login granting Admin to any username** in `Login.cshtml.cs:50-92` — must be hard-gated to `IsDevelopment()` only.

Full report: `docs/security-audits/2026-05-03.md`. Action list: `docs/security-audits/NEEDS_ATTENTION.md`.

## Stack lock-in

| Layer | Choice | Why we can't easily change |
|---|---|---|
| Backend runtime | .NET 8 | Many EF + IdentityModel + AspNetCore packages aligned |
| ORM | EF Core 7 (currently) → upgrade to 8 (planned) | Migrations history; large surface |
| DB | MySQL via Pomelo | Connection strings, migrations, SQL dump |
| Messaging | NetMQ 4.0.1.13 with CURVE | Both .NET and Python clients (pyzmq) align |
| Admin UI | Razor Pages | Existing pattern, cookie auth, antiforgery |
| Admin SSO | Keycloak | Realm `asps`, clients `asps-webapi` + `asps-admin-panel` |
| Desktop agent | Python 3.11+ | pyzmq, websockets, psutil |
| Browser extension | Chrome MV3 | manifest_version: 3 |

## Naming conventions

- Migrations: action-prefix descriptive — `AddXTable`, `AddColumnsYToZTable`, `RenameXToY`. Example: `AddedColumnsProtectiveActionsAndScamInProgressToImmediateDangerTable`.
- Memory files: `<role>/<topic>.md` with kebab/snake; INDEX.md as entry.
- Audit reports: `YYYY-MM-DD.md` in `docs/security-audits/`.
- Daily journals (if added later): `<role>/journal/YYYY-MM-DD.md`.

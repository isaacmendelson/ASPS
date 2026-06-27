---
phase: quick-001-asps-system-specifications
plan: 001
type: execute
wave: 1
depends_on: []
files_modified:
  - docs/system-specifications/ASPS_System_Specification.md
autonomous: true

must_haves:
  truths:
    - "A single unified spec document exists at docs/system-specifications/ASPS_System_Specification.md"
    - "All 7 subsystems are documented with the same per-subsystem template"
    - "Every non-trivial claim cites a source (code path with file, KE doc name, or JIRA key)"
    - "Mobile agent and users portal status (built vs planned) is explicitly verified and stated"
    - "Unknown facts are marked TBD/unknown rather than fabricated"
  artifacts:
    - path: "docs/system-specifications/ASPS_System_Specification.md"
      provides: "Unified ASPS system specification covering all subsystems"
      contains: "## Backend"
  key_links:
    - from: "spec document"
      to: "source code / KE / JIRA"
      via: "inline citations on every non-trivial claim"
      pattern: "\\((ASPSBackend14_J|apps|Analyzers|docs)/|KE:|SCRUM-|SPS-"
---

<objective>
Produce a single, comprehensive, citation-backed specification document covering ALL seven ASPS subsystems: Backend (host service), Admin Portal (WebApi: Razor Pages + REST), Desktop Agent (Python), Mobile Agent, Browser Extension (Chrome MV3), Users Portal, and URL Analyzer (Python/FastAPI).

Purpose: Give the team one authoritative reference describing what each subsystem is, how it is built, how the pieces talk to each other, and — critically — what is actually built vs partial vs planned. The current docs are scattered across 80+ markdown files; this consolidates and grounds every claim in a verifiable source.

Output: `docs/system-specifications/ASPS_System_Specification.md` — one unified markdown document.
</objective>

<execution_context>
@C:\Users\Isaac\.claude/get-shit-done/workflows/execute-plan.md
@C:\Users\Isaac\.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@CLAUDE.md

# NOTE: STATE.md is partly stale (mentions OpenClaw / Gitea / "no EF migrations").
# Trust the actual repo + CLAUDE.md. Real stack: .NET 8 + EF Core + MySQL (Pomelo) +
# NetMQ CURVE; Razor Pages admin + Keycloak; Python desktop agent; Chrome MV3 extension;
# Python analyzers.

# Existing design docs to mine (do NOT blindly copy — verify against code):
@docs/ASPS_DATA_FLOW.md
@docs/system-specifications/ASPS_System_Overview.md
</context>

<subsystem_map>
Confirmed layout in the working dir (verify each before writing):

| # | Subsystem | Primary path(s) |
|---|-----------|-----------------|
| 1 | Backend (host service) | `ASPSBackend14_J/ASPSBackend/`, `ASPSBackend14_J/Business/`, `ASPSBackend14_J/Common/`, `ASPSBackend14_J/Interface/` |
| 2 | Admin Portal + REST | `ASPSBackend14_J/WebApi/` (Pages, Controllers, Hubs, Services, DTOs) |
| 3 | Desktop Agent | `apps/desktop/win/` (Python 3.11 + pyzmq + websockets) |
| 4 | Mobile Agent | NO directory found — verify status (likely PLANNED) |
| 5 | Browser Extension | `apps/extension/chrome/` (Chrome MV3, vanilla JS) |
| 6 | Users Portal | NO directory found — verify status. WARNING: a `CustomerPortal` exists but under the SEPARATE LIMAT project, NOT ASPS. Do NOT treat it as the ASPS users portal. |
| 7 | URL Analyzer | `Analyzers/basic-url-analyzer/` (Python/FastAPI) |

Ports/messaging reference (from CLAUDE.md): 50001 alert listener (NetMQ ROUTER+CURVE), 50002 notification publisher (PUB+CURVE), 5555 business endpoint, 5556 CQRS gateway, 5001/5002 WebApi HTTP/HTTPS, 3306 MySQL, 8080-8484 extension↔agent WebSocket, 8180 dev Keycloak.
</subsystem_map>

<per_subsystem_template>
Each subsystem section MUST use this exact structure:

```
## <N>. <Subsystem Name>

**Status:** built | partial | planned   (one word + one-line justification with citation)

### Purpose
What it does and why it exists.

### Components
Key modules/files/classes and their responsibilities.

### Tech Stack
Languages, frameworks, libraries, versions where known.

### Interfaces / Contracts
APIs, message schemas, ports, DTOs, events it produces/consumes.

### Data Flow
How data moves in and out; who it talks to.

### Security
Auth, encryption (CURVE keys, Keycloak), known security debt.

### Open Items
Known gaps, TBDs, planned work (link JIRA keys where applicable).
```

Citation rule: every non-trivial claim ends with a source in parentheses — a code path with file (and line where useful), a KE doc name prefixed `KE:`, or a JIRA key (`SCRUM-###` / `SPS-###`). Prefer writing "TBD" or "unknown" over guessing.
</per_subsystem_template>

<sources>
Use all three sources for every subsystem:

1. **Source code** (read directly, no approval): paths in <subsystem_map>.
2. **Knowledge Engine** RAG (from `C:\AI\Projects\KnowledgeEngine`):
   ```
   printf 'QUESTION\nquit\n' | PYTHONIOENCODING=utf-8 .venv/Scripts/python.exe scripts/run_knowledge_engine.py
   ```
   Cite returned answers as `KE: <source-name>`.
3. **JIRA** cloud `aspsjira.atlassian.net` (projects SCRUM and SPS), Basic auth, email `IsaacMendelson@gmail.com`, token at `C:\Users\Isaac\AppData\Local\Temp\claude\c--Jobs-ASPS-GitHub-Software\76d3e389-66b6-4110-a729-1db9213407b7\scratchpad\.jira_token`:
   ```
   curl -s -u "IsaacMendelson@gmail.com:$(cat <tokenfile>)" -H "Accept: application/json" \
     "https://aspsjira.atlassian.net/rest/api/3/search?jql=project=SCRUM&maxResults=50&fields=summary,description,status"
   ```
</sources>

<tasks>

<task type="auto">
  <name>Task 1: Gather grounded facts for all 7 subsystems</name>
  <files>(no file output — produce structured notes in your working context for Task 2)</files>
  <action>
    For EACH of the 7 subsystems in <subsystem_map>, collect facts against the
    <per_subsystem_template> headings (Purpose, Components, Tech Stack, Interfaces/Contracts,
    Data Flow, Security, Status, Open Items). For every subsystem use all three <sources>.

    Recommended order and emphasis:
    1. Backend host (ASPSBackend): Program.cs / Startup, NetMQ socket setup, EF DbContext,
       DI wiring, CURVE key config. Note ports 50001/50002/5555/5556.
    2. Admin Portal + REST (WebApi): Pages/, Controllers/, Hubs/ (SignalR?), Services/, DTOs/,
       Keycloak/OIDC config (dev port 8180), ports 5001/5002.
    3. Business/Common/Interface: CQRS, repositories, entities, enums, value objects — fold
       these into the Backend section (they are backend layers), or note as a sub-section.
    4. Desktop Agent (apps/desktop/win): entry script, pyzmq CURVE auth (auth.json), websockets
       server for extension (8080-8484 scan), analyzers it calls.
    5. Browser Extension (apps/extension/chrome): manifest.json (MV3), background/service worker,
       content scripts, WebSocket client to the agent.
    6. URL Analyzer (Analyzers/basic-url-analyzer): FastAPI app, endpoints, request/response
       contract, how backend/agent invoke it.
    7. Mobile Agent — VERIFY built vs planned. No directory exists in the repo; search code,
       query KE ("Is there a mobile agent for ASPS? Android or iOS?"), and search JIRA
       (SCRUM/SPS for "mobile", "Android", "iOS"). Conclude with an explicit status + citation.
    8. Users Portal — VERIFY built vs planned. No directory exists. Do NOT conflate with the
       LIMAT CustomerPortal (separate project). Search code, KE ("Does ASPS have a users portal
       / end-user portal?"), and JIRA. Conclude with explicit status + citation. If the only
       user-facing surface is the User Layer / User Risk Score design docs
       (docs/system-specifications/*User Layer*, docs/SCRUM-904-user-risk-score-design.md),
       state that the users portal is a DESIGN/PLANNED artifact, citing those docs.

    For each subsystem record at least: a one-line Purpose, the key files/components with paths,
    tech stack, the inbound/outbound interfaces (ports, endpoints, message types), and a
    built/partial/planned status with a justifying citation. Where a fact cannot be confirmed
    from any of the three sources, mark it TBD — do NOT invent.

    Keep notes concise and citation-tagged; they feed Task 2 directly.
  </action>
  <verify>
    You can state, with a citation each, the status (built/partial/planned) of all 7 subsystems —
    including an explicit, evidence-backed verdict for Mobile Agent and Users Portal that does
    NOT rely on the LIMAT CustomerPortal.
  </verify>
  <done>
    Structured, citation-tagged notes exist for all 7 subsystems covering every template heading,
    with TBD markers where sources are silent.
  </done>
</task>

<task type="auto">
  <name>Task 2: Synthesize the unified specification document</name>
  <files>docs/system-specifications/ASPS_System_Specification.md</files>
  <action>
    Write a single markdown document at docs/system-specifications/ASPS_System_Specification.md
    from the Task 1 notes. Structure:

    1. Title + one-paragraph overview of ASPS (what the system protects against, citing
       docs/system-specifications/ASPS_System_Overview.md and CLAUDE.md).
    2. "Subsystem Status at a Glance" table: | Subsystem | Status | Primary path | Key citation |
       — one row per subsystem (7 rows).
    3. "System Architecture & Data Flow" short section: how the subsystems connect end-to-end
       (device/extension → agent → backend → admin/analyzer → DB), with the ports table from
       CLAUDE.md, citing docs/ASPS_DATA_FLOW.md where it agrees with the code.
    4. One "## N. <Subsystem>" section per subsystem (Backend, Admin Portal, Desktop Agent,
       Mobile Agent, Browser Extension, Users Portal, URL Analyzer) using the EXACT
       <per_subsystem_template>. Order backend-first, then clients, then analyzer — but include
       all 7.
    5. "Open Questions / TBD" appendix consolidating every TBD raised in the sections, each with
       what source could resolve it.

    Rules: every non-trivial claim carries an inline citation (code path with file, `KE:` doc,
    or JIRA key). Use markdown file links for code paths where helpful. Prefer TBD over
    fabrication. Keep Hebrew out of the doc body unless quoting a source title; write the spec
    in English.
  </action>
  <verify>
    File exists; it contains exactly one "## N." section for each of the 7 subsystems; the
    "at a glance" table has 7 rows; Mobile Agent and Users Portal sections each carry an explicit
    status with citation; no section is missing the template headings.
  </verify>
  <done>
    docs/system-specifications/ASPS_System_Specification.md is a complete, single, English,
    citation-backed spec covering all 7 subsystems with consistent structure.
  </done>
</task>

<task type="auto">
  <name>Task 3: Self-check completeness and citations</name>
  <files>docs/system-specifications/ASPS_System_Specification.md</files>
  <action>
    Audit the document against the constraints and fix any gaps in place:
    - Confirm all 7 subsystems present with the full template (Purpose, Components, Tech Stack,
      Interfaces/Contracts, Data Flow, Security, Status, Open Items).
    - Scan for non-trivial claims lacking a citation; add the missing source or downgrade the
      claim to TBD.
    - Confirm Mobile Agent and Users Portal statuses are explicit and that the doc does NOT
      assert the LIMAT CustomerPortal is the ASPS users portal.
    - Confirm the "at a glance" table and the section statuses agree (no contradictions).
    - Confirm the doc is internally consistent with CLAUDE.md's stack/ports (flag any conflict
      as a TBD rather than silently picking one).
    Make edits directly; do not produce a separate report file.
  </action>
  <verify>
    grep for the 7 section headers returns 7 matches; a manual pass finds no uncited non-trivial
    claim and no contradiction between the at-a-glance table and section statuses.
  </verify>
  <done>
    The spec passes the completeness + citation audit; Mobile/Users-Portal verdicts are
    unambiguous and LIMAT is correctly excluded.
  </done>
</task>

</tasks>

<verification>
- `docs/system-specifications/ASPS_System_Specification.md` exists and is a single file.
- All 7 subsystems documented with identical template structure.
- Every non-trivial claim cites code path / KE doc / JIRA key; unknowns marked TBD.
- Mobile Agent and Users Portal statuses explicitly verified (built vs planned), LIMAT excluded.
</verification>

<success_criteria>
- One unified, English, citation-backed spec at the target path.
- 7 subsystems, consistent per-subsystem template, status-at-a-glance table.
- No fabricated facts; TBD used where sources are silent.
- Mobile/Users-Portal status grounded in code + KE + JIRA, not assumed from LIMAT.
</success_criteria>

<output>
After completion, create `.planning/quick/001-asps-system-specifications/001-SUMMARY.md`
</output>

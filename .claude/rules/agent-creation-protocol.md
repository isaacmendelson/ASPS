# Agent Creation Protocol

Standardized checklist for creating a new agent in the ASPS AI Operating System.
Every new agent must pass all items before being considered operational.

---

## Pre-creation

- [ ] **Role justification** — Why does this role need to exist? What gap does it fill that existing agents don't cover?
- [ ] **Layer assignment** — Executive / C-level / Technical? Determines reporting line.
- [ ] **Overlap check** — Verify no existing agent already owns this domain. If overlap exists, define clear boundaries.

---

## 1. Agent definition (`.claude/agents/<role>.md`)

The agent definition file is Layer 2 of the instruction system. It defines *who the role is*.

### Frontmatter (required)

| Field | Description |
|---|---|
| `name` | Kebab-case role name (must match filename without `.md`) |
| `description` | One-line description shown in agent picker |
| `tools` | Comma-separated tool list (Read, Edit, Write, Bash, Grep, Glob, etc.) |
| `model` | Default model (sonnet / opus) |

### Body sections (required skeleton)

| Section | Purpose |
|---|---|
| **Mission** | Why this role exists — one sentence |
| **Reads first** | Which files the agent reads at initialization |
| **Responsibilities** | What it owns — bullet list |
| **Inputs** | What it consumes to do its work |
| **Outputs** | What it produces |
| **Constraints** | Hard limits, non-negotiables, what it does NOT do |
| **Collaboration** | Which roles it works with and how |
| **Definition of Done** | Checklist — the bar for "complete" |

### Mandatory coverage in constraints

Every agent definition must explicitly address:

- [ ] **Security** — What security rules apply? Reference `.claude/rules/security-rules.md`. Sensitive data handling, secret exposure prevention.
- [ ] **Ethics / integrity** — Covered by `.claude/team/CHARTER.md` (Layer 1), but role-specific ethical constraints if any (e.g., "does not fabricate project facts").
- [ ] **Scope boundaries** — What this agent does NOT do. No scope creep.
- [ ] **QA gate** — Does this role's output require QA review? If yes, state it.
- [ ] **No silent side-fixes** — Universal rule, but restate if the role is tempted to "fix while documenting" etc.

---

## 2. Hat directory (`.claude/hats/<role>/`)

The hat directory is Layer 3 of the instruction system. It holds *accumulated learnings*.

### Required files

| File | Purpose |
|---|---|
| `INDEX.md` | Read order for hat files. Lists all files in the directory with a one-line description. Includes memory update rules. |

### Standard topic files (create as empty stubs, populate over time)

| File | Purpose |
|---|---|
| `identity.md` | Role identity, mission depth, working style |
| `decisions.md` | Load-bearing decisions made in this role |
| `inflight.md` | Current initiatives and handoff pointers |
| `operating-principles.md` | Role-specific operating principles beyond the team charter |

Not all roles need all files — create what's relevant. CEO and DevOps have the richest hat directories as reference examples.

---

## 3. Initialization chain

Every agent must read these files at startup, in order:

1. `docs/PROJECT_CONTEXT.md` — mandatory shared context
2. `.claude/team/CHARTER.md` — team-wide behavioral charter (ethics, thinking, priorities, conduct)
3. `.claude/rules/security-rules.md` — binding security rules
4. `.claude/rules/coding-standards.md` — if the agent touches code or reads code
5. `.claude/agents/<role>.md` — the agent's own definition
6. `.claude/hats/<role>/INDEX.md` — then each file the INDEX points to

The **"Reads first"** line in the agent definition must list the role-specific files (items 4+ above). Items 1-3 are universal and loaded by CLAUDE.md / the spawning orchestrator.

---

## 4. Registration

After creating the agent definition and hat directory:

- [ ] **Add to agent README** — Update `.claude/agents/README.md` roster table with the new agent.
- [ ] **Add to CEO delegation** — Update `.claude/hats/ceo/delegation.md` routing matrix: role, stack/domain, and routing rules.
- [ ] **Add to CLAUDE.md** — Update the hat system quick map table if needed.
- [ ] **Verify spawning** — The CEO can spawn the agent with the correct `subagent_type`.

---

## 5. Validation

Before declaring the agent operational:

- [ ] **Reads-first chain works** — All referenced files exist and are readable.
- [ ] **No orphan references** — The agent definition doesn't reference non-existent files.
- [ ] **Team charter compliance** — The agent's constraints don't contradict `.claude/team/CHARTER.md`.
- [ ] **Security rules compliance** — The agent's constraints don't weaken `.claude/rules/security-rules.md`.
- [ ] **Overlap resolved** — No ambiguity about ownership boundaries with adjacent roles.
- [ ] **DRY** — Agent definition does not duplicate content from CHARTER.md or other rules. References them instead.

---

## Quick reference — file locations

```
.claude/
├── agents/
│   ├── <role>.md          ← Agent definition (Layer 2)
│   └── README.md          ← Roster table
├── hats/
│   └── <role>/
│       ├── INDEX.md       ← Hat memory index (Layer 3)
│       ├── identity.md    ← Role identity
│       ├── decisions.md   ← Decisions log
│       ├── inflight.md    ← Current work
│       └── ...            ← Other topic files
├── team/
│   └── CHARTER.md         ← Team charter (Layer 1)
└── rules/
    ├── security-rules.md  ← Binding security rules
    ├── coding-standards.md
    ├── review-standards.md
    ├── team-rules.md
    ├── task-workflow.md
    └── agent-creation-protocol.md  ← This file
```

# Agent Model, Effort, and Efficiency Learning Method

## Purpose

This method helps the ASPS CEO/root agent choose the right:

- specialist Agent role;
- model family;
- reasoning effort;
- task size and context package;
- QA effort;
- concurrency and execution order.

The objective is to improve delivery accuracy and first-pass QA success while reducing unnecessary tokens, elapsed time, retries, and duplicated work.

This is an operational learning system. It does not retrain or modify model weights. It improves future routing decisions from recorded ASPS execution evidence.

## Non-negotiable constraints

Optimization must never bypass:

- the Jira acceptance criteria;
- relevant unit tests before QA;
- independent QA for non-trivial changes;
- security review when the risk warrants it;
- CEO/root trust-but-verify;
- authorization requirements for external or destructive actions.

The cheapest run is not successful if it increases regressions, QA cycles, security risk, or time to a verified result.

## Decision unit

Make and record the routing decision before spawning an Agent. A decision consists of:

1. Jira issue or task identifier.
2. Primary Agent role.
3. Contributing/reviewing roles.
4. Model selection.
5. Reasoning effort.
6. Context size/fork strategy.
7. Expected verification and QA level.
8. Expected dependencies and concurrency slot.

Do not change model or effort in the middle of productive work merely to experiment. Interrupt and respawn only when there is evidence of a material mismatch, repeated failure, or a genuine stall.

## Pre-spawn task scoring

Score every dimension from 0 to 2.

| Dimension | 0 | 1 | 2 |
|---|---|---|---|
| Logical complexity | Mechanical/local | Several branches or states | Algorithmic, concurrent, or state-machine work |
| Component span | One file/module | One component | Cross-component/protocol |
| Security sensitivity | None | Security-adjacent | Auth, crypto, secrets, deserialization, SSRF, permissions |
| Blast radius | Isolated | Component behavior | System-wide/data/protocol compatibility |
| Requirement ambiguity | Exact | Some judgment | Missing/conflicting contract |
| Migration/compatibility | None | Local compatibility | Versioned migration/backward compatibility |
| Test difficulty | Existing focused tests | New mocks/integration | Distributed, adversarial, timing, or browser/process tests |
| Domain novelty | Repeated known pattern | Some unfamiliar code | New architecture/library or weak project precedent |

Calculate:

`task_risk_score = sum(all eight dimensions)`

Maximum: 16.

Security sensitivity of 2 or component span of 2 is an escalation signal even when the total score is modest.

## Initial routing policy

### Model family

| Situation | Default selection |
|---|---|
| Documentation, inventory, mechanical changes | Efficient/balanced model |
| Bounded implementation in one component | Balanced coding model |
| Complex implementation, protocol, migration, concurrency | Frontier coding model |
| Critical security implementation or adversarial QA | Frontier coding model |
| Architecture-only design | Frontier reasoning/coding model with architect role |

Use only models currently available to the Agent tool. Record the actual selected model or `inherited` when no override is used.

### Reasoning effort

| Risk score / condition | Default effort |
|---|---|
| 0–3, mechanical and well tested | Low |
| 4–7, bounded single-component work | Medium |
| 8–11, complex or cross-cutting work | High |
| 12–16, critical architecture/security | High; consider xhigh only when evidence shows benefit |
| Any auth/crypto/deserialization/SSRF boundary | At least High |
| Independent QA of Highest/Critical work | High |
| CISO/security audit | High |

Do not choose High automatically for every task. Higher effort may increase per-turn time and token use. Its value is fewer mistakes and QA cycles on work where mistakes are expensive.

### Context strategy

- Use the smallest context that contains the requirement, applicable instructions, specification, ownership boundaries, and relevant prior decisions.
- Prefer a bounded history fork over full-history when the Agent only needs recent task context.
- Give exact Jira title and acceptance criteria in the spawn prompt.
- Give exact file/module ownership.
- State that other Agents share the worktree and must not revert concurrent edits.
- Require precise reporting: files, commands, counts, result, blockers, and QA readiness.
- Do not copy irrelevant discussions, secrets, or complete repository history.

## Concurrency and slot policy

The root/CEO occupies one slot and remains available for coordination.

Default allocation:

- up to two implementation Agents;
- one slot reserved for independent QA;
- root/CEO for orchestration, verification, Jira, commits, and user communication.

Use three implementation Agents only when no task is expected to reach QA soon. Free or reserve a QA slot before implementers report `PRE-QA READY`.

Security/CISO audits may be deferred to the final audit wave when continuous QA needs the slot, unless an active critical threat requires immediate review.

## Runtime progress checks

`Running` is a sampling state, not proof of progress.

Use evidence-based checkpoints:

1. Ask for completed work, current action, changed files, latest test command/result, and blocker.
2. Inspect actual worktree changes without modifying them.
3. Look for recent build/test artifacts and process output.
4. Compare with the previous checkpoint.
5. If no evidence changes, send a focused follow-up.
6. If the same blocking condition persists, interrupt and resume from a written handoff.

Do not interrupt an Agent that is producing verified progress solely because elapsed time feels long.

## Post-task execution record

Record one entry after every Jira implementation/QA cycle.

Required fields:

| Field | Meaning |
|---|---|
| Jira ID and title | Exact task identity |
| Task category | Backend, Analyzer, Desktop, Extension, cross-stack, QA, security, docs |
| Pre-spawn score | Eight dimension values and total |
| Agent role | Primary implementer/reviewer |
| Model | Exact model or `inherited` |
| Effort | Low, Medium, High, xhigh, etc. |
| Context strategy | Full, bounded turns, or none plus explicit prompt |
| Start/end checkpoints | Available timestamps or relative checkpoints |
| Files/modules | Actual scope changed |
| Test evidence | Commands and pass/fail/skip counts |
| QA result | PASS/FAIL and severity/count of findings |
| QA cycles | Number required to reach PASS |
| Rework | Main causes |
| Interruptions/restarts | Count and reason |
| Final assessment | Underpowered, appropriate, or overpowered |
| Routing lesson | Concrete future adjustment |

Never store secrets or raw credential-bearing commands in the record.

## Efficiency metrics

Primary quality/time metrics:

- first-pass QA rate;
- number of QA cycles;
- regression count;
- Critical/High findings missed by implementer;
- time/checkpoints to `PRE-QA READY`;
- time/checkpoints to `QA PASS`;
- build/test success rate;
- restarts and ownership conflicts;
- amount of scope completed versus planned.

Token usage per Agent is not currently exposed. Until it is available, use these proxies:

- number of Agent turns;
- number of tool calls/retries;
- repeated file reads;
- size of context supplied;
- amount of rework;
- QA cycles;
- unnecessary output volume.

If exact token metrics become available, add input, output, cached, and total tokens per Agent and compute tokens per accepted Jira task.

## Learning loop

### After each task

1. Compare predicted risk with actual difficulty.
2. Classify the routing choice:
   - `underpowered`: avoidable errors, repeated rework, or QA failures caused by insufficient reasoning/context;
   - `appropriate`: acceptable time and first-pass or efficient QA success;
   - `overpowered`: no quality gain relative to repeated comparable tasks, with materially higher cost/time.
3. Write one specific lesson.
4. Do not change global policy from one outlier.

### Every 5 completed tasks

Review by category and model/effort:

- first-pass QA rate;
- average QA cycles;
- common failure classes;
- interruptions;
- evidence of underpowered/overpowered selections.

Adjust a routing default only when at least three comparable tasks support the change, unless a security failure requires immediate escalation.

### Every 20 completed tasks

Perform a policy review:

- retire rules unsupported by evidence;
- split categories that behave differently;
- identify Agents with strong domain/task fit;
- update effort thresholds;
- improve spawn prompt templates;
- document model/version changes that invalidate older comparisons.

## Decision examples

### Bounded Desktop bug with focused tests

- Complexity: 1
- Component span: 1
- Security: 0
- Blast radius: 1
- Ambiguity: 0
- Migration: 0
- Test difficulty: 1
- Novelty: 0
- Total: 4
- Route: `desktop-agent`, balanced coding model, Medium.

### Cross-component message correlation

- Complexity: 2
- Component span: 2
- Security: 1
- Blast radius: 2
- Ambiguity: 1
- Migration: 2
- Test difficulty: 2
- Novelty: 1
- Total: 13
- Route: architect design followed by component implementers, frontier coding model, High, independent High-effort QA.

### SSRF and hostile-browser isolation

- Complexity: 2
- Component span: 1
- Security: 2
- Blast radius: 2
- Ambiguity: 1
- Migration: 0
- Test difficulty: 2
- Novelty: 1
- Total: 11 plus mandatory security escalation
- Route: `analyzer-ai`, frontier coding model, High, adversarial QA and later CISO review.

## Governance

- CEO/root owns the routing decision and learning record.
- VP Engineering may recommend sequencing and staffing.
- Implementers supply test and execution evidence.
- QA independently reports quality outcomes.
- Security/CISO reports security outcomes.
- Knowledge Manager may curate aggregate lessons but cannot weaken gates.
- The user may override model/effort choices explicitly.

This method is mandatory guidance for future ASPS Agent assignments. When evidence conflicts with the initial table, evidence wins, subject to the non-negotiable quality and security constraints.

---
name: threat-model
description: Produce a STRIDE-based threat model for a specific component before or just after implementation. Forces explicit enumeration of trust boundaries, assets, attacker capabilities, and concrete mitigations.
---

# /threat-model

Generates a STRIDE-based threat model document for a single component or subsystem. Sits next to the SCRUM design doc and the ADR — design says *what we built*, ADR says *why we chose it*, threat model says *how it can be attacked and what stops the attack*.

## When to invoke
- A new component is being designed or has just landed that touches a trust boundary: a new network endpoint, a new auth mechanism, a new data ingestion path, a new external integration, a new file-touching capability.
- User says "threat model", "STRIDE this", "what could go wrong with X", "security review of <component>".
- After `/security-audit-now` surfaces a pattern of findings in one area — write up the threat model to make the systemic mitigation explicit.

## Don't write a threat model for
- Internal refactors that don't change trust boundaries.
- UI changes that don't introduce new data flows.
- Components covered by an existing threat model whose attack surface hasn't changed.

## Ask first

1. **What component?** Be precise — "the SCRUM-863 auto-update path on the Windows agent", not "the agent".
2. **What are its trust boundaries?**
   - Where does untrusted input enter?
   - What credentials, keys, or capabilities does it hold?
   - What does it expose to the outside world?
3. **What assets does the component protect?**
   - User data (PII, browsing, messages, calls, …)
   - Credentials (CURVE keys, Keycloak tokens, GitHub PATs, …)
   - System integrity (can the attacker run code as the user?)
   - Service availability
4. **Who is in scope as an attacker?** Pick the realistic adversaries:
   - Remote unauth (anyone on the internet)
   - Remote auth (a malicious authenticated user, e.g. a scammer running a customer account)
   - Local unprivileged (malware running as the user)
   - Local privileged (admin / root — usually out of scope per industry convention; state this explicitly)
   - Supply chain (compromised dependency, package registry, GitHub action)

If the user can't answer 2-4, the component isn't ready to threat-model — propose a design review first.

## File layout

Path: `docs/threat-models/<scope>-<component>.md` — kebab-case. Examples:
- `docs/threat-models/auto-update-windows-agent.md`
- `docs/threat-models/netmq-port-50001-alert-ingest.md`
- `docs/threat-models/extension-mv3-content-script.md`

## Template

```markdown
# Threat Model — <Component>

**Status:** Draft | Reviewed | Implemented (with mitigations cross-linked)
**Last updated:** YYYY-MM-DD
**Owner:** Isaac
**Scope:** <one-line definition of the component>
**Related:** [SCRUM-###](<link>), [design doc](../SCRUM-###-...md), [ADR NNNN](../adrs/NNNN-...)

## 1. Component summary

What the component does, in 3-5 sentences. Not the design — the elevator pitch. Anyone reading just this should know what's at risk.

## 2. Trust boundaries

A diagram or table showing where untrusted input crosses into the component. Each boundary lists:
- Direction (inbound / outbound)
- Protocol (HTTPS, NetMQ+CURVE, WebSocket, file write, …)
- What's transmitted
- Who's on the other side

Example:
| Boundary | Direction | Protocol | Payload | Counterparty |
|---|---|---|---|---|
| Port 50001 | inbound | NetMQ ROUTER + CURVE | Alert (JSON) | Authenticated device |
| Backend webapi → DB | outbound | EF Core + MySQL | SQL | Trusted DB |

## 3. Assets

What we're protecting, with sensitivity:

| Asset | Sensitivity | Notes |
|---|---|---|
| CURVE server private key | Critical | Compromise = impersonation of backend to any device |
| User browsing telemetry | High | PII; under URL_BROWSING_ANALYSIS consent |
| ... | ... | ... |

## 4. Attacker capabilities (in scope)

For each adversary, state what they can do:

- **Remote unauth:** Send arbitrary TCP/UDP to exposed ports. Read public docs and config.
- **Remote auth (compromised device account):** All of above + valid CURVE handshake + valid auth token.
- **Local unprivileged (malware as the user):** All of above + read user-readable files + intercept localhost WebSocket traffic + modify user-writable files (e.g. `auth.json`, extension `chrome.storage`).
- **Local privileged:** *Out of scope per project convention.*
- **Supply chain:** Inject malicious code via a compromised NuGet / npm / pip dependency.

## 5. STRIDE enumeration

For each STRIDE category, list concrete threats that apply to this component. Skip categories that genuinely don't apply, but justify *why* they don't apply.

### Spoofing
- **Threat:** <one-line title>
  - **Description:** What the attacker does.
  - **Assets at risk:** <list>
  - **Likelihood:** Low / Medium / High
  - **Impact:** Low / Medium / High
  - **Mitigation:** What stops or detects it. Link to code if implemented.
  - **Status:** Mitigated | Accepted risk | Open (with follow-up issue)

### Tampering
(same shape)

### Repudiation
(same shape — often "N/A: not a multi-party transaction" for internal components)

### Information disclosure
(same shape)

### Denial of service
(same shape)

### Elevation of privilege
(same shape)

## 6. Residual risks

Threats we explicitly accept (with rationale) or have only partially mitigated. State the trigger that would force a re-evaluation:

> **Risk:** Port 5555 binds to `*:` without CURVE. Local malware running as the user can spoof device messages.
> **Accepted because:** Existing security debt — see ADR NNNN.
> **Re-evaluate when:** A device-to-backend mutual auth migration is funded (SCRUM-### TBD).

## 7. Detection / response

Logging, alerting, and incident-response steps for the threats above. What signal would tell us we're under attack? Where does it go? Who acts on it?

## 8. Follow-up actions

Numbered list of unresolved items with owners and trigger conditions. These should become JIRA tickets.

## 9. Review log

> **YYYY-MM-DD** — Initial draft by Isaac.
> **YYYY-MM-DD** — Reviewed; added <change>.
```

## STRIDE quick reference (for prompting the user)

| Category | Attacker goal | Typical mitigation |
|---|---|---|
| **S**poofing | Impersonate a legit entity | Authentication, mutual TLS, CURVE handshake, signed tokens |
| **T**ampering | Modify data in flight or at rest | Integrity checks (HMAC, SHA-256), signed updates, write-once logs |
| **R**epudiation | Deny having performed an action | Append-only audit logs, signed actions, immutable history |
| **I**nformation disclosure | Read data they shouldn't | Encryption (TLS, CURVE, at-rest), access control, consent boundaries |
| **D**enial of service | Make the system unavailable | Rate limits, resource caps, isolation, graceful degradation |
| **E**levation of privilege | Get more capability than authorized | Authorization checks, least-privilege, sandboxing, separation of duties |

## Verification

- **All 6 STRIDE categories addressed.** If a category genuinely doesn't apply, the doc must say so explicitly (don't just omit).
- **Every threat has a status** — Mitigated, Accepted risk, or Open. No threats in limbo.
- **Mitigations link to code** when implemented. "We do this" with no link is a claim, not a fact.
- **Residual risks have re-evaluation triggers.** "Accepted forever" is rarely the right answer.
- **Cross-links resolve** — design doc, ADRs, JIRA tickets.

## Output convention

```
Threat model: docs/threat-models/<file>.md
Component: <name>
STRIDE coverage: 6/6 categories (N marked N/A with justification)
Threats: <count> total
  Mitigated: <count>
  Accepted: <count>
  Open: <count>
Residual risks: <count> (with triggers)
Follow-up actions: <count>
Related: <links>
```

## Never

- Write a threat model in past tense for a component that's already shipped without re-validating each mitigation against current code. Use `/security-audit-now` first to baseline.
- Mark a threat "Mitigated" without a code link. The link is the difference between an aspiration and a fact.
- Skip a STRIDE category silently. State "N/A — <reason>" or it looks like an oversight.
- Conflate threat model with security audit. The model is *forward-looking* and *component-scoped*; the audit is *current-state* and *codebase-wide*.

# Review Standards

Standards for QA review, security review, and code review. Every agent performing a review must read this file before starting.

---

## Severity scale

| Severity | Definition | Impact on verdict |
|---|---|---|
| **Blocker** | Prevents correct operation, causes data loss, or introduces a security vulnerability. Must be fixed before merge. | FAIL |
| **Major** | Significant correctness issue, missing requirement, or logic error that will cause problems in production. | FAIL |
| **Minor** | Code quality issue, missing edge case handling, or deviation from standards that does not break functionality. | PASS with conditions — must be tracked for follow-up |
| **Nit** | Style, naming, formatting, or cosmetic issues. No functional impact. | PASS — fix is optional |

**Verdict rule:** any Blocker or Major → FAIL. Minor-only → conditional PASS (issues tracked). Nit-only → PASS.

---

## Finding format

Every finding must include:

```
| Severity | File:Line | Description |
```

- **Severity** — Blocker / Major / Minor / Nit
- **File:Line** — exact location (e.g., `CQRSGateway.cs:57`)
- **Description** — what is wrong, why it matters, and suggested fix

---

## Code review guide

Code review verifies **code quality, security, and maintainability**. It is distinct from QA review (which verifies functional correctness against acceptance criteria).

### What to check — in priority order

1. **Correctness** — Does the code do what it claims? Are edge cases handled? Are there off-by-one errors, null dereferences, race conditions?
2. **Security** — No injection vectors (SQL, XSS, command). No hardcoded secrets. Input validated at system boundaries. Auth/authz enforced. See [security-rules.md](security-rules.md).
3. **Reuse and simplification** — Is there existing code that does the same thing? Can the change be simpler? Are there unnecessary abstractions or premature generalizations?
4. **Error handling** — Are exceptions observable (logged with context)? No silent swallowing. Fail-fast where appropriate.
5. **Performance** — No obvious N+1 queries, unbounded loops, or memory leaks. Only flag when the impact is concrete.
6. **Tests** — Do tests cover the changed behavior? Are they testing the right thing (behavior, not implementation)?
7. **DRY** — No duplicated logic, constants, or rules. See [coding-standards.md](coding-standards.md).
8. **Naming and clarity** — Code is self-documenting. Names reveal intent.

### What NOT to review

- Style preferences that don't affect readability (brace placement, blank lines) — unless they violate project conventions.
- Unrelated code outside the diff — flag only if the change creates a new inconsistency with existing code.

### Review output format

```markdown
## Code Review — <JIRA-ID> <title>

**Verdict:** PASS / FAIL

**Summary:** <1-2 sentences>

| Severity | File:Line | Description |
|---|---|---|
| ... | ... | ... |
```

### Delegation

The orchestrator (CEO) is the default code reviewer. The orchestrator may delegate to:
- The **architect** agent — for cross-cutting design or API changes
- A **peer developer** agent — for component-specific changes within their domain

The delegated reviewer follows this same guide and returns the verdict to the orchestrator.

---

## QA review guide

QA review verifies **functional correctness against the acceptance criteria**. It is independent of the implementing agent.

### What to check

1. **Acceptance criteria met** — every criterion from the JIRA issue is verified with evidence.
2. **Tests exist and pass** — the implementing agent's reported test results are independently verified.
3. **No regressions** — the change does not break existing functionality.
4. **Edge cases** — boundary values, empty inputs, error conditions.
5. **Logging and observability** — no sensitive data in logs (passwords, tokens, PII). Errors are observable.

### QA output format

```markdown
## QA Verdict: PASS / FAIL

| Criterion | Status | Evidence |
|---|---|---|
| <acceptance criterion> | PASS/FAIL | <file:line or test result> |

| Severity | File:Line | Finding |
|---|---|---|
| ... | ... | ... |
```

---

## Security review guide

Security review checks for vulnerabilities and compliance. See [security-rules.md](security-rules.md) for binding rules.

### What to check

1. **OWASP Top 10** — injection, broken auth, sensitive data exposure, XXE, broken access control, misconfiguration, XSS, insecure deserialization, known vulnerabilities, insufficient logging.
2. **ASPS-specific** — CURVE encryption integrity, device auth flow, sensitive payload handling, WebSocket security.
3. **Secrets** — no tokens, API keys, passwords, or private keys in committed files.

### Security output format

Every finding must include: **severity + concrete exploit path + `file:line` + remediation**.

```markdown
## Security Review — <scope>

| Severity | File:Line | Exploit path | Remediation |
|---|---|---|---|
| ... | ... | ... | ... |
```

# CEO Memory — Adaptive Agent Routing

The CEO/root agent owns the decision made at every Agent spawn:

- specialist role and ownership boundary;
- model selection;
- reasoning effort;
- context/fork strategy;
- concurrency slot and dependency order;
- QA model and effort.

Before spawning any implementation, QA, architecture, security, or DevOps
Agent, read and apply:

[`docs/agent-operations/AGENT_MODEL_EFFORT_LEARNING_METHOD.md`](../../../docs/agent-operations/AGENT_MODEL_EFFORT_LEARNING_METHOD.md)

This is a durable operating rule. The CEO must record post-task evidence and
use it to improve future routing accuracy, first-pass QA success, elapsed time,
and token efficiency. Optimization must never weaken unit tests, independent
QA, security review, acceptance criteria, or trust-but-verify.

Current measurement limitation: per-Agent token usage is not exposed. Use the
method's proxy metrics until exact token telemetry becomes available.

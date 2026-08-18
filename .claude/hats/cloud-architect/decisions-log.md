# Cloud Architecture Decisions Log

Lightweight ADR log for cloud infrastructure decisions. Full ADRs in `docs/architecture/decisions/` when applicable.

---

## CAD-001: Container Apps over AKS

**Date:** 2026-08
**Status:** Accepted
**Context:** ASPS needs a container orchestration platform for dev and eventually production.
**Decision:** Azure Container Apps (Consumption tier).
**Rationale:**
- Simpler operational model than AKS (no cluster management, node pools, upgrades).
- Consumption pricing — pay per request, not per node. Critical for dev where traffic is minimal.
- Built-in HTTPS ingress with managed TLS certificates.
- Sufficient for the current scale (3 container apps, <10 containers total).
**Trade-off:** Less control than AKS. If we need custom networking, GPU workloads, or >100 containers, revisit.

---

## CAD-002: Sidecar Pattern for ZMQ/CURVE Communication

**Date:** 2026-08
**Status:** Accepted
**Context:** WebApi communicates with Backend via CQRS over NetMQ (ZMQ with CURVE encryption). Container Apps TCP ingress (Envoy) does not forward ZMTP wire protocol.
**Decision:** Run Backend as sidecar container in WebApi's Container App, communicating via localhost.
**Rationale:**
- Verified that `additionalPortMappings` with TCP transport fails for ZMQ traffic.
- Sidecar pattern provides localhost networking — ZMQ works perfectly.
- No additional infrastructure cost.
**Trade-off:** Device-facing ports (50001, 50002) not externally reachable. Acceptable for dev; production needs separate solution.

---

## CAD-003: PostgreSQL for Keycloak

**Date:** 2026-08
**Status:** Accepted
**Context:** Keycloak needs a database. Azure MySQL has `lower_case_table_names=1` (read-only) which breaks Keycloak's mixed-case Liquibase migrations.
**Decision:** Separate PostgreSQL Flexible Server for Keycloak.
**Rationale:**
- PostgreSQL is Keycloak's recommended database.
- No case-sensitivity issues.
- Keeps Keycloak isolated from ASPS's MySQL.
**Trade-off:** Two managed database servers = higher cost. Justified by correctness.

---

## CAD-004: North Europe Region

**Date:** 2026-08
**Status:** Accepted
**Context:** Israel Central and West Europe both rejected resource creation for different reasons.
**Decision:** North Europe for all ASPS dev resources.
**Rationale:**
- All required resource types available (Container Apps, MySQL, PostgreSQL, Key Vault, Storage, App Insights).
- Reasonable latency from Israel (~40-60ms).
- Capacity available for our subscription.
**Trade-off:** Higher latency than Israel Central would have had (~10-20ms).

---

## CAD-005: OIDC Federated Credentials for GitHub Actions

**Date:** 2026-08
**Status:** Accepted
**Context:** CI/CD needs Azure authentication. Options: service principal secret, OIDC federated credentials, managed identity.
**Decision:** Azure AD app with OIDC federated credentials.
**Rationale:**
- No password to rotate — only 3 non-secret values stored in GitHub.
- Industry best practice for GitHub-to-Azure auth.
- Scoped to specific repository and branches.
**Trade-off:** Slightly more complex initial setup than a stored secret.

---

## CAD-006: Key Vault with RBAC (Not Access Policies)

**Date:** 2026-08
**Status:** Accepted
**Context:** Key Vault supports two authorization models: access policies and Azure RBAC.
**Decision:** Azure RBAC mode.
**Rationale:**
- Unified with the rest of Azure IAM.
- Granular role assignments (Secrets User, Certificates Officer, etc.).
- Works with managed identity natively.
- Access policies are legacy.
**Trade-off:** None — RBAC is strictly better for new deployments.

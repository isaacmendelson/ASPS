# Azure Patterns & Platform Quirks — Learned from ASPS Deployment

Knowledge accumulated from ASPS-693 deployment. Every entry is verified first-hand.

---

## Validated Patterns

### 1. Sidecar Pattern for Non-HTTP Protocols

**Problem:** Container Apps TCP ingress uses Envoy proxy, which does NOT forward ZMQ/CURVE (ZMTP wire protocol) between separate Container Apps. Even `additionalPortMappings` with TCP transport fails.

**Solution:** Run the Backend as a sidecar container in the WebApi Container App. Both containers share localhost networking — CQRS over `tcp://localhost:5556` works perfectly.

**When to use:** Any time two containers need to communicate via a non-HTTP protocol (ZMQ, raw TCP with custom wire format, gRPC without HTTP/2 framing).

**When NOT to use:** Pure HTTP/HTTPS or gRPC-over-HTTP/2 — Container Apps ingress handles these natively.

**YAML structure:**
```yaml
containers:
  - name: webapi        # main container — gets HTTP ingress
    image: ...
    resources:
      cpu: 0.5
      memory: 1Gi
  - name: backend       # sidecar — no ingress, shares localhost
    image: ...
    resources:
      cpu: 1.0
      memory: 2Gi
```

**Trade-off:** Device-facing TCP ports (50001, 50002) are NOT externally reachable in sidecar mode. Dev OK; production needs a different solution (separate Container App with TCP ingress, or Azure Load Balancer).

---

### 2. Keycloak on PostgreSQL, Not MySQL

**Problem:** Azure MySQL enforces `lower_case_table_names=1` (read-only, cannot be changed). Keycloak 26.0's Liquibase changelogs have mixed-case table names that collide under case-insensitive comparison → "Table already exists" on first-start migration.

**Solution:** Use PostgreSQL Flexible Server for Keycloak. PostgreSQL is Keycloak's recommended database. No case-insensitivity issues.

**Impact:** Two managed database servers in dev (MySQL for ASPS, PostgreSQL for Keycloak). Acceptable cost for correctness.

---

### 3. OIDC Federated Credentials for CI/CD

**Problem:** Storing Azure service principal secrets in GitHub is a security risk and requires rotation.

**Solution:** Azure AD app with OIDC federated credentials for GitHub Actions. Three non-secret values (`CLIENT_ID`, `TENANT_ID`, `SUBSCRIPTION_ID`) — no password rotation needed.

**Setup:** `azure/login@v2` action with `client-id`, `tenant-id`, `subscription-id`. App needs `Contributor` on resource group + `AcrPush` on ACR.

---

### 4. Managed Identity for Service-to-Service Auth

**Pattern:** User-assigned managed identity (`id-asps-dev`) with RBAC roles:
- **Key Vault Secrets User** — read secrets at runtime
- **AcrPull** — pull container images

Container Apps reference the identity; no credentials stored anywhere.

---

### 5. Azure Files for Shared Volumes

**Use case:** CURVE encryption keys shared between WebApi and Backend containers.

**Setup:** Storage Account → File Share → mount as volume in Container App. Both containers (main + sidecar) mount the same volume.

---

## Platform Quirks

### Region Availability

Not all Azure regions support all resource types equally:
- **Israel Central** — may reject Container Apps managed-environment creation for certain subscriptions.
- **West Europe** — may reject automatic Log Analytics workspace creation ("not accepting new customers").
- **North Europe** — worked for everything ASPS needs.

**Lesson:** Always have 2-3 fallback regions. Check availability BEFORE designing the architecture.

### MySQL `lower_case_table_names`

Azure MySQL Flexible Server sets `lower_case_table_names=1` and it's **read-only**. Any application with mixed-case table names in its migration scripts will hit collisions. Check BEFORE selecting MySQL for third-party software.

### Container Apps Ingress Limitations

- HTTP/HTTPS ingress: works perfectly, handles TLS termination.
- TCP ingress with `additionalPortMappings`: only forwards standard TCP — NOT custom wire protocols like ZMTP (ZMQ), AMQP-over-raw-TCP, or other binary protocols that Envoy doesn't understand.
- Sidecar containers: no individual ingress, share localhost with main container.

### EF Core Migration Gaps

EF migrations can have gaps — migrations exist as SQL files but have no `.Designer.cs` file, so EF Core can't track them. Apply these manually via SQL and record in `__EFMigrationsHistory`.

### Key Vault Reference Integration

Container Apps Key Vault reference integration (where env vars resolve from Key Vault at startup) failed in our setup. Using direct secret values in container secrets instead. May revisit with newer Container Apps runtime.

### Keycloak User ID Sync

Regular `POST /admin/realms/{realm}/users` ignores the `id` field. To create a user with a specific UUID (e.g., syncing between environments), use `POST /admin/realms/{realm}/partialImport` which preserves user IDs.

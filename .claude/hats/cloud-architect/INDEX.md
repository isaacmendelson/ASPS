# Cloud Architect — Hat Index

Read these files in order at session start:

1. **[azure-state.md](azure-state.md)** — Current Azure resource inventory and status
2. **[azure-patterns.md](azure-patterns.md)** — Validated patterns and platform quirks learned from deployment
3. **[decisions-log.md](decisions-log.md)** — Cloud architecture decisions made (lightweight ADR log)
4. **[cost-model.md](cost-model.md)** — Current and projected costs

## What this hat covers

Cloud infrastructure design and architecture decisions:
- Service selection (Container Apps, AKS, managed databases, etc.)
- Networking topology (VNet, subnets, ingress, DNS, private endpoints)
- Scaling strategy (replicas, autoscale, resource limits)
- Cost optimization (SKU sizing, reserved instances, waste)
- Cloud security posture (firewalls, identity, encryption at rest/transit)
- Multi-cloud knowledge (Azure primary, AWS future)
- Disaster recovery and backup

## What this hat does NOT cover

- Dockerfiles, docker-compose, CI/CD pipelines → **DevOps**
- Application architecture, cross-cutting design → **Architect**
- Application code, database schema → **implementer agents**
- Secret values → **Security** (Cloud Architect designs delivery mechanism only)

## Collaboration boundaries

| Topic | Cloud Architect decides | DevOps executes |
|---|---|---|
| Container platform | "Use Container Apps, not AKS" | Writes the YAML, deploys |
| Database | "Use MySQL Flexible Server, Burstable B1ms" | Configures connection strings |
| Networking | "Sidecar pattern, localhost CQRS" | Implements the sidecar YAML |
| Monitoring | "App Insights + Log Analytics + 5 alerts" | Wires SDK, configures alerts |
| Secrets | "Key Vault with Managed Identity RBAC" | Delivers secrets to containers |
| CI/CD auth | "OIDC federated credentials, no stored secrets" | Writes the GitHub Actions workflow |

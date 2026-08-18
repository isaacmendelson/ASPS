# Azure State — ASPS Dev Environment

**Region:** North Europe (`northeurope`)
**Subscription:** `d9f067ae-8b6e-42a9-a45f-4918c20f2bbb`
**Tenant:** `5d3a01c0-eccb-4b50-8798-609b92c89098`
**Last verified:** 2026-08-18

## Resource Inventory

| Resource | Name | SKU / Tier | Status |
|---|---|---|---|
| Resource Group | `rg-asps-dev` | — | Active |
| VNet | `vnet-asps-dev` | 10.0.0.0/16 | Active |
| Container Apps Env | `cae-asps-dev` | Consumption | Succeeded |
| Container Registry | `acraspsisaacdev` | Basic | Operational |
| MySQL Flexible Server | `mysql-asps-dev` | Burstable B1ms | Ready |
| PostgreSQL Flexible Server | `pg-asps-keycloak-dev` | Burstable B1ms | Ready |
| Key Vault | `kv-asps-dev` | Standard, RBAC | 7 secrets |
| Managed Identity | `id-asps-dev` | User-assigned | KV Secrets User + AcrPull |
| Storage Account | `staspsdev` | Standard LRS | curve-keys file share |
| Application Insights | `appi-asps-dev` | Workspace-based | Active |
| Log Analytics | `log-asps-dev` | Pay-as-you-go | Connected to CAE |

## Container Apps

| App | Status | Architecture | Image |
|---|---|---|---|
| `ca-keycloak-dev` | Running | Standalone | `quay.io/keycloak/keycloak:26.0` |
| `ca-webapi-dev` | Running | WebApi + Backend **sidecar** | WebApi `0.1.0` + Backend `0.2.0` |
| `ca-backend-dev` | **Deactivated** | Was standalone | Replaced by sidecar |

## Key Vault Secrets

| Secret | Purpose |
|---|---|
| `mysql-admin-password` | MySQL root |
| `mysql-connection-string` | Full MySQL connection string |
| `postgres-keycloak-password` | PostgreSQL for Keycloak |
| `keycloak-admin-password` | Keycloak admin console |
| `cqrs-shared-secret` | HMAC-SHA256 for Backend↔WebApi CQRS |
| `kc-webapi-client-secret` | Keycloak OIDC client for WebApi |
| `storage-account-key` | Azure Files account key |

## GitHub Actions (CI/CD)

| Secret | Set | Value source |
|---|---|---|
| `AZURE_CLIENT_ID` | Yes | App `github-actions-asps` |
| `AZURE_TENANT_ID` | Yes | Tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Yes | Subscription ID |

Environment `dev` created. Pending: required reviewers + branch restriction.

## Region History

- **Israel Central** — rejected Container Apps managed-environment creation.
- **West Europe** — rejected automatic Log Analytics workspace creation (not accepting new customers).
- **North Europe** — selected, all resources created successfully.

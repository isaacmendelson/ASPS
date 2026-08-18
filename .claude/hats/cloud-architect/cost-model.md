# Cost Model — ASPS Azure Dev Environment

**Last updated:** 2026-08-18
**Region:** North Europe
**Tier:** Development (minimal traffic)

## Current Monthly Estimate

| Resource | SKU | Est. Monthly Cost | Notes |
|---|---|---|---|
| MySQL Flexible Server | Burstable B1ms | ~$13 | 1 vCore, 2 GiB RAM, can stop when idle |
| PostgreSQL Flexible Server | Burstable B1ms | ~$13 | Keycloak DB |
| Container Apps (3 apps) | Consumption | ~$0-5 | Pay per vCPU-s and GiB-s; near-zero in dev |
| Container Registry | Basic | ~$5 | 10 GiB storage |
| Key Vault | Standard | ~$0-1 | Per-operation pricing |
| Storage Account | Standard LRS | ~$0.10 | Tiny file share for CURVE keys |
| Application Insights | Workspace-based | ~$0-2 | 5 GB/month free tier |
| Log Analytics | Pay-as-you-go | ~$0-2 | 5 GB/month free tier |
| **Total dev estimate** | | **~$34-41/month** | |

## Cost Optimization Notes

- **Database servers** are the biggest cost. Both can be stopped during non-working hours to save ~50%.
- **Container Apps Consumption** is near-free when idle — no baseline cost.
- **ACR Basic** is the cheapest tier. Upgrade to Standard only if we need geo-replication or >10 GiB.
- **App Insights + Log Analytics** have generous free tiers (5 GB/month each).

## Production Projection (10x scale)

| Resource | Production SKU | Est. Monthly Cost |
|---|---|---|
| MySQL Flexible Server | General Purpose D2ds_v4 | ~$130 |
| PostgreSQL Flexible Server | Burstable B2ms | ~$26 |
| Container Apps | Consumption (more replicas) | ~$50-100 |
| Container Registry | Standard | ~$20 |
| Key Vault | Standard | ~$5-10 |
| App Insights | 50 GB/month | ~$115 |
| **Total production estimate** | | **~$350-400/month** |

## Decisions Impacting Cost

- **Two database servers** (MySQL + PostgreSQL) — driven by Keycloak/MySQL incompatibility. See [decisions-log.md](decisions-log.md) CAD-003.
- **Container Apps over AKS** — saves ~$150-200/month in node costs. See CAD-001.
- **Consumption tier** — no baseline cost; scales to zero when idle.

---
title: Azure Foundation for ASPS
status: in-progress
last-updated: 2026-08-01
environment: Development
owner: Isaac Mendelson
agent-role: Cloud Architect
---

# Azure Foundation for ASPS

## Completed
- Installed Azure CLI and authenticated.
- Confirmed active subscription.
- Created `rg-asps-dev`.
- Registered `Microsoft.ContainerRegistry`.
- Created `acraspsisaacdev`.
- Pushed `asps-backend:0.1.0`.

## Region decisions
- `israelcentral`: unavailable for Container Apps managed environment in this subscription.
- `westeurope`: automatic Log Analytics workspace creation rejected because the region was not accepting new customers.
- `northeurope`: next target region.

## Next step
Create an explicit Log Analytics workspace in North Europe, then create the Container Apps Environment using that workspace.

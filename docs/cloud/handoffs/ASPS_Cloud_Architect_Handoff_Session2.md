# ASPS Cloud Architect -- Handoff (Session 2)

**Purpose**

This document captures the current Azure state of the ASPS project and
provides the information required for a future Cloud Architect agent or
engineer to continue the work without relying on conversation history.

------------------------------------------------------------------------

# Project

ASPS (Anti-Scam Protection System)

Repository root:

`C:\Jobs\ASPS\GitHub\Software`

This repository contains both the application source code and the
operational documentation.

------------------------------------------------------------------------

# Cloud Documentation Philosophy

Nothing is tribal knowledge.

Every cloud action must be:

1.  Planned
2.  Executed
3.  Validated
4.  Documented

Documentation is part of the deliverable.

------------------------------------------------------------------------

# Azure Status

## Azure Account

Azure CLI installed.

Authenticated successfully.

Subscription verified.

------------------------------------------------------------------------

## Resource Group

Name:

`rg-asps-dev`

Status:

Created.

------------------------------------------------------------------------

## Azure Container Registry

Registry:

`acraspsisaacdev`

Login Server:

`acraspsisaacdev.azurecr.io`

Status:

Operational.

Backend image pushed successfully.

Repository:

`asps-backend`

Tag:

`0.1.0`

------------------------------------------------------------------------

## Log Analytics

Workspace:

`log-asps-dev`

Region:

`North Europe`

Provisioning:

Succeeded.

------------------------------------------------------------------------

## Azure Container Apps Environment

Environment:

`cae-asps-dev`

Region:

`North Europe`

Provisioning:

Succeeded.

------------------------------------------------------------------------

# Important Lessons Learned

## Resource Providers

New Azure subscriptions may require explicit provider registration.

------------------------------------------------------------------------

## Israel Central

Container Apps Environment creation was not available for this
subscription.

------------------------------------------------------------------------

## West Europe

Automatic Log Analytics workspace creation failed because the region was
temporarily not accepting new customers.

------------------------------------------------------------------------

## North Europe

Successfully selected for the development environment.

------------------------------------------------------------------------

# Current Architecture

Azure Subscription

    └── Resource Group

            ├── Azure Container Registry

            ├── Log Analytics Workspace

            └── Container Apps Environment

Application services have not yet been deployed.

------------------------------------------------------------------------

# Current Local State

Backend runs successfully in Docker.

Keycloak runs in Docker.

Docker Compose environment is operational.

Backend image has already been published to Azure Container Registry.

------------------------------------------------------------------------

# Next Session (Session 3)

Before deploying containers, design the complete ASPS cloud
architecture.

Topics:

-   Public vs internal services
-   Container Apps layout
-   MySQL strategy
-   Keycloak deployment
-   CURVE key management
-   Azure Key Vault
-   Secrets
-   Networking
-   Managed Identity
-   Monitoring
-   CI/CD
-   Cost considerations
-   Production readiness

No application deployment should occur before these decisions are
documented.

------------------------------------------------------------------------

# Deliverables Required From Session 3

1.  Cloud Architecture document
2.  Architecture Decision Records (ADR)
3.  Deployment sequence
4.  Updated Runbooks
5.  Updated Troubleshooting
6.  Cloud Architect Agent knowledge

------------------------------------------------------------------------

# Operating Rule

Every deployment must satisfy three conditions:

• It works.

• It is understood.

• It is documented.

If any condition is missing, the task is incomplete.

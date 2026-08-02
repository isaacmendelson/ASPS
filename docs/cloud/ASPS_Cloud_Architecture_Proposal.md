---
author: ChatGPT
date: 2026-08-02
status: Draft
title: ASPS Cloud Architecture Proposal
---

# ASPS Cloud Architecture Proposal (Learning Edition)

## Purpose

This document recommends a target cloud architecture for ASPS.

It is intentionally written as both:

-   a learning guide for Isaac
-   a design document for the future Cloud Architect agent

The objective is **not** to deploy everything immediately. The objective
is to understand *why* each component exists before building it.

------------------------------------------------------------------------

# Design Principles

1.  Security first
2.  Least privilege
3.  Everything containerized
4.  Managed services where practical
5.  Infrastructure should be reproducible
6.  Secrets never stored in source code
7.  Monitoring is part of the platform
8.  Production architecture grows from Development architecture

------------------------------------------------------------------------

# High-Level Architecture

``` text
                Internet
                    |
          Azure Front Door (future)
                    |
          -------------------------
                    |
             Web API (Public)
                    |
        -----------------------
        |                     |
   Backend Service      Keycloak
        |
   Internal Messaging
        |
   Analyzer
        |
   Azure Database for MySQL
        |
   Azure Key Vault
```

------------------------------------------------------------------------

# Recommended Azure Services

  -----------------------------------------------------------------------
  ASPS Component          Azure Service           Why
  ----------------------- ----------------------- -----------------------
  Docker Images           Azure Container         Native registry
                          Registry                integrated with Azure

  WebApi                  Azure Container Apps    HTTPS endpoint,
                                                  automatic scaling

  Backend                 Azure Container Apps    Long-running service
                                                  without VM management

  Analyzer                Azure Container Apps    Internal-only compute
                          (internal)              

  Database                Azure Database for      Managed backups,
                          MySQL Flexible Server   patching, HA options

  Authentication          Keycloak (Container App Keeps compatibility
                          initially)              with existing
                                                  implementation

  Secrets                 Azure Key Vault         Central secret
                                                  management

  Monitoring              Azure Monitor + Log     Platform observability
                          Analytics               

  Diagnostics             Application Insights    Performance and
                          (later)                 distributed tracing

  Storage                 Azure Files / Blob      Persistent files and
                          Storage                 artifacts
  -----------------------------------------------------------------------

------------------------------------------------------------------------

# Why Container Apps?

At the current stage of ASPS:

-   simpler than Kubernetes
-   cheaper for development
-   supports containers directly
-   automatic HTTPS
-   scaling included
-   integrates well with ACR

Recommendation:

Start with Container Apps.

Move to AKS only if operational requirements justify it.

------------------------------------------------------------------------

# Why Azure Database for MySQL?

Current local Docker MySQL is excellent for development.

Cloud production should avoid self-managed databases when possible.

Managed MySQL provides:

-   automatic backups
-   patching
-   monitoring
-   easier operations

Recommendation:

Development may temporarily use containerized MySQL for experiments.

Target architecture should use Azure Database for MySQL Flexible Server.

------------------------------------------------------------------------

# Keycloak

Short term:

Deploy existing Keycloak container.

Long term:

Evaluate Microsoft Entra ID integration while keeping Keycloak if its
authorization model remains advantageous.

------------------------------------------------------------------------

# Secrets

Never store:

-   CQRS Shared Secret
-   CURVE keys
-   database passwords
-   OAuth client secrets

inside Git or Docker images.

Target:

Azure Key Vault becomes the single source of truth.

------------------------------------------------------------------------

# Networking

Public:

-   Web API

Private:

-   Backend
-   Analyzer
-   Database
-   Key Vault
-   Internal messaging

Only the minimum required endpoints should be exposed.

------------------------------------------------------------------------

# Deployment Order

1.  Azure foundation
2.  Azure Container Registry
3.  Log Analytics
4.  Container Apps Environment
5.  Azure Database for MySQL
6.  Key Vault
7.  Backend
8.  Analyzer
9.  WebApi
10. Keycloak
11. Browser Extension integration
12. Monitoring
13. CI/CD

------------------------------------------------------------------------

# Learning Goals

During Azure learning, every service should answer:

-   What problem does it solve?
-   Why is it preferable to alternatives?
-   What does it cost?
-   What security implications does it have?
-   How is it monitored?
-   How is it replaced if requirements change?

------------------------------------------------------------------------

# Decisions Deferred

These topics intentionally remain open until more Azure experience is
gained:

-   AKS vs Container Apps for production
-   Multi-region deployment
-   Global load balancing
-   Disaster recovery topology
-   Terraform/Bicep adoption
-   Full CI/CD design

------------------------------------------------------------------------

# Recommendation

Do not optimize for the final production platform yet.

Instead:

1.  Learn Azure deeply.
2.  Build a working development platform.
3.  Understand operational trade-offs.
4.  Evolve toward a production-grade architecture using documented
    Architecture Decision Records (ADR).

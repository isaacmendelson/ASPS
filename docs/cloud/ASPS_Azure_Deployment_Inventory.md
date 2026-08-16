# ASPS Azure Deployment Inventory

## Azure services required

### Already created
- Resource Group: `rg-asps-dev`
- Azure Container Registry: `acraspsisaacdev`
- Log Analytics Workspace: `log-asps-dev`
- Azure Container Apps Environment: `cae-asps-dev`

### Still required
1. **Azure Database for MySQL Flexible Server**
   - One managed MySQL server.
   - Separate databases recommended:
     - `aspsbackend2db`
     - `keycloak`

2. **Azure Key Vault**
   - Store:
     - MySQL credentials
     - CQRS shared secret
     - Keycloak client secrets
     - Keycloak database password
     - storage credentials if needed
   - CURVE private key may be stored as a Key Vault secret or persisted in Azure Files, depending on the final key-loading design.

3. **User-assigned Managed Identity**
   - Allow Container Apps to pull images from ACR.
   - Allow Container Apps to read secrets from Key Vault.

4. **Azure Storage Account**
   - Azure Files share for persistent CURVE key files if the current file-based implementation is retained.
   - Optional additional shares for other persistent files.

5. **Azure Container Apps**
   - Public WebApi app.
   - Internal Backend app.
   - Keycloak app.
   - Backend and URL Analyzer should initially run as two containers in the same Container App replica if they continue communicating through a Unix socket.

6. **Internal service communication**
   - Use Azure Container Apps internal ingress and service discovery.
   - WebApi to Backend: internal TCP ingress on the CQRS port.
   - Do not add Azure Service Bus yet unless ASPS is redesigned for durable asynchronous messaging.

7. **Monitoring**
   - Existing Log Analytics Workspace.
   - Azure Monitor.
   - Application Insights later for .NET telemetry and distributed tracing.

8. **Optional later services**
   - Azure Front Door for global entry point, WAF, and custom-domain routing.
   - Azure DNS for the ASPS domain.
   - Azure Service Bus for durable event-driven messaging.
   - Container Apps Jobs for database migrations or scheduled processing.
   - Azure Backup / expanded disaster-recovery configuration.
   - Bicep or Terraform for Infrastructure as Code.
   - GitHub Actions for CI/CD.

## Container images to upload and run

1. **ASPS Backend**
   - Repository: `asps-backend`
   - Already pushed: `acraspsisaacdev.azurecr.io/asps-backend:0.1.0`

2. **ASPS WebApi / Razor Pages**
   - Proposed repository: `asps-webapi`

3. **URL Analyzer**
   - Proposed repository: `asps-url-analyzer`
   - Initially deploy as a sidecar container in the Backend Container App if the Unix-socket architecture remains.

4. **Keycloak**
   - Either:
     - use the official Keycloak image directly, pinned to an exact version, or
     - mirror that exact image into ACR for controlled deployments.
   - Recommended repository if mirrored: `keycloak`

## Images not required

- **MySQL image**: not required when using Azure Database for MySQL Flexible Server.
- **Browser extension**: distributed to browsers, not run as an Azure container.
- **Windows/Desktop agent**: installed on client devices, not run as an Azure container.

## Items that may still be missing

Before deployment, confirm whether ASPS also has:

- a separate notification service
- a dedicated simulation worker
- scheduled/background jobs that should not live inside Backend
- a dedicated customer portal or website image separate from WebApi
- database migration tooling or a migration job
- externally reachable TCP clients on ports `50001` and `50002`
- any mobile backend or push-notification integration

These decisions affect the final number of Container Apps and public endpoints.

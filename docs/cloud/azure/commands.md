# Azure Commands — ASPS Development

```powershell
$resourceGroup = "rg-asps-dev"
$location = "northeurope"
$acrName = "acraspsisaacdev"
$loginServer = "acraspsisaacdev.azurecr.io"
$containerAppsEnvironment = "cae-asps-dev"
$logAnalyticsWorkspace = "log-asps-dev"
```

## Verify providers
```powershell
az provider show --namespace Microsoft.App --query registrationState -o tsv
az provider show --namespace Microsoft.OperationalInsights --query registrationState -o tsv
```

## Register if needed
```powershell
az provider register --namespace Microsoft.App
az provider register --namespace Microsoft.OperationalInsights
```

## Create Log Analytics workspace
```powershell
az monitor log-analytics workspace create `
  --resource-group $resourceGroup `
  --workspace-name $logAnalyticsWorkspace `
  --location $location `
  --tags Project=ASPS Environment=Development
```

## Read workspace values
```powershell
$workspaceId = az monitor log-analytics workspace show `
  --resource-group $resourceGroup `
  --workspace-name $logAnalyticsWorkspace `
  --query customerId `
  --output tsv

$workspaceKey = az monitor log-analytics workspace get-shared-keys `
  --resource-group $resourceGroup `
  --workspace-name $logAnalyticsWorkspace `
  --query primarySharedKey `
  --output tsv
```

Do not print or commit `$workspaceKey`.

## Create Container Apps Environment
```powershell
az containerapp env create `
  --name $containerAppsEnvironment `
  --resource-group $resourceGroup `
  --location $location `
  --logs-workspace-id $workspaceId `
  --logs-workspace-key $workspaceKey `
  --tags Project=ASPS Environment=Development
```

## Validate
```powershell
az containerapp env show `
  --name $containerAppsEnvironment `
  --resource-group $resourceGroup `
  --query "{Name:name,Location:location,State:properties.provisioningState}" `
  --output table
```

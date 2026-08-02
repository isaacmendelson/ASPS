# Azure Troubleshooting — ASPS

## MissingSubscriptionRegistration
Cause: required resource provider was not registered.

Resolution:
```powershell
az provider register --namespace Microsoft.ContainerRegistry
az provider show --namespace Microsoft.ContainerRegistry --query registrationState -o tsv
```

## Container Apps unavailable in Israel Central
Symptom: Azure rejected creation of a Container Apps managed environment in `israelcentral`.

Resolution: choose a region returned as eligible by Azure CLI.

## West Europe rejected new Log Analytics workspace
Symptom:
```text
RequestDisallowedByAzure
The selected region is currently not accepting new customers.
```

Resolution: move the deployment to `northeurope` and create the Log Analytics workspace explicitly.

## Multiline JMESPath query failed in PowerShell
Cause: PowerShell split the query incorrectly.

Resolution: prefer short single-line `--query` expressions or one provider check per command.

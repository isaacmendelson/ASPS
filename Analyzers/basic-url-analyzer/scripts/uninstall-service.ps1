# uninstall-service.ps1 - Remove URL Analyzer Windows Service
# Run as Administrator!

$ServiceName = "URLAnalyzerAPI"

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "ERROR: Run as Administrator!" -ForegroundColor Red
    exit 1
}

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    Write-Host "Stopping service..." -ForegroundColor Yellow
    if ($service.Status -eq "Running") {
        Stop-Service -Name $ServiceName -Force
        Start-Sleep -Seconds 2
    }

    Write-Host "Removing service..." -ForegroundColor Yellow
    nssm remove $ServiceName confirm

    Write-Host "Service removed successfully!" -ForegroundColor Green
} else {
    Write-Host "Service not found" -ForegroundColor Yellow
}

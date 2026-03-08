# install-service.ps1 - Install URL Analyzer as Windows Service
# Run as Administrator!
# Usage: .\install-service.ps1

$ErrorActionPreference = "Stop"

# Configuration
$ServiceName = "URLAnalyzerAPI"
$ServiceDisplayName = "URL Scam Analyzer API"
$ServiceDescription = "REST API for analyzing URLs for potential scams"
$ProjectPath = $PSScriptRoot | Split-Path -Parent
$PythonPath = (Get-Command python -ErrorAction SilentlyContinue).Source
$Port = 8000

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  URL Analyzer Service Installer" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "ERROR: This script must be run as Administrator!" -ForegroundColor Red
    Write-Host "Right-click PowerShell and select 'Run as Administrator'" -ForegroundColor Yellow
    exit 1
}

Write-Host "Project path: $ProjectPath"
Write-Host "Python path: $PythonPath"
Write-Host ""

# Check Python
if (-not $PythonPath) {
    Write-Host "ERROR: Python not found in PATH" -ForegroundColor Red
    exit 1
}

# Check if NSSM is installed
$nssmPath = Get-Command nssm -ErrorAction SilentlyContinue
if (-not $nssmPath) {
    Write-Host "NSSM not found. Installing via winget..." -ForegroundColor Yellow

    try {
        winget install nssm --accept-package-agreements --accept-source-agreements
        $env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path", "User")
        $nssmPath = Get-Command nssm -ErrorAction SilentlyContinue
    } catch {
        Write-Host "ERROR: Could not install NSSM automatically" -ForegroundColor Red
        Write-Host "Please install NSSM manually from: https://nssm.cc/download" -ForegroundColor Yellow
        exit 1
    }
}

Write-Host "NSSM found: $($nssmPath.Source)" -ForegroundColor Green

# Remove existing service if exists
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "Removing existing service..." -ForegroundColor Yellow
    if ($existingService.Status -eq "Running") {
        Stop-Service -Name $ServiceName -Force
        Start-Sleep -Seconds 2
    }
    nssm remove $ServiceName confirm
    Start-Sleep -Seconds 1
}

# Install dependencies
Write-Host "Installing Python dependencies..." -ForegroundColor Yellow
& $PythonPath -m pip install -r "$ProjectPath\requirements.txt" --quiet
& $PythonPath -m pip install uvicorn fastapi --quiet
Write-Host "Dependencies installed" -ForegroundColor Green

# Create the service using NSSM
Write-Host "Creating Windows service..." -ForegroundColor Yellow

# NSSM install
nssm install $ServiceName $PythonPath
nssm set $ServiceName AppParameters "-m uvicorn api:app --host 0.0.0.0 --port $Port"
nssm set $ServiceName AppDirectory $ProjectPath
nssm set $ServiceName DisplayName $ServiceDisplayName
nssm set $ServiceName Description $ServiceDescription
nssm set $ServiceName Start SERVICE_AUTO_START
nssm set $ServiceName AppStdout "$ProjectPath\logs\service.log"
nssm set $ServiceName AppStderr "$ProjectPath\logs\service-error.log"
nssm set $ServiceName AppRotateFiles 1
nssm set $ServiceName AppRotateBytes 1048576

# Create logs directory
$LogDir = "$ProjectPath\logs"
if (-not (Test-Path $LogDir)) {
    New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
}

Write-Host "Service created successfully!" -ForegroundColor Green

# Start the service
Write-Host "Starting service..." -ForegroundColor Yellow
Start-Service -Name $ServiceName
Start-Sleep -Seconds 3

$service = Get-Service -Name $ServiceName
if ($service.Status -eq "Running") {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  Installation Complete!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Service Status: Running" -ForegroundColor Green
    Write-Host "API URL: http://localhost:$Port" -ForegroundColor Cyan
    Write-Host "Health: http://localhost:$Port/health" -ForegroundColor Cyan
    Write-Host "Docs: http://localhost:$Port/docs" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Commands:" -ForegroundColor Yellow
    Write-Host "  Deploy update: .\scripts\deploy.ps1"
    Write-Host "  Stop service:  Stop-Service $ServiceName"
    Write-Host "  Start service: Start-Service $ServiceName"
    Write-Host "  View logs:     Get-Content .\logs\service.log -Tail 50"
    Write-Host ""
} else {
    Write-Host "WARNING: Service installed but not running" -ForegroundColor Yellow
    Write-Host "Check logs at: $ProjectPath\logs\service-error.log" -ForegroundColor Yellow
}

# deploy.ps1 - Pull from Git and restart the API service
# Usage: .\deploy.ps1

$ErrorActionPreference = "Stop"

# Configuration
$ServiceName = "URLAnalyzerAPI"
$ProjectPath = $PSScriptRoot | Split-Path -Parent
$LogFile = "$ProjectPath\logs\deploy.log"

# Create logs directory if not exists
$LogDir = Split-Path $LogFile -Parent
if (-not (Test-Path $LogDir)) {
    New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
}

function Write-Log {
    param([string]$Message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] $Message"
    Write-Host $logMessage
    Add-Content -Path $LogFile -Value $logMessage
}

Write-Log "========== Starting deployment =========="
Write-Log "Project path: $ProjectPath"

# Step 1: Pull from Git
Write-Log "Pulling latest code from Git..."
Set-Location $ProjectPath

try {
    $gitOutput = git pull 2>&1
    Write-Log "Git output: $gitOutput"

    if ($gitOutput -match "Already up to date") {
        Write-Log "No changes to deploy"
    }
} catch {
    Write-Log "ERROR: Git pull failed - $_"
    exit 1
}

# Step 2: Stop the service (if running)
Write-Log "Stopping service '$ServiceName'..."
try {
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($service -and $service.Status -eq "Running") {
        Stop-Service -Name $ServiceName -Force
        Start-Sleep -Seconds 2
        Write-Log "Service stopped"
    } else {
        Write-Log "Service not running or not found"
    }
} catch {
    Write-Log "Warning: Could not stop service - $_"
}

# Step 3: Update dependencies (if requirements.txt changed)
Write-Log "Checking for new dependencies..."
try {
    $pipOutput = & python -m pip install --no-deps -r "$ProjectPath\requirements.lock.txt" --quiet 2>&1
    & python -m pip check
    Write-Log "Dependencies updated"
} catch {
    Write-Log "Warning: pip install failed - $_"
}

# Step 4: Start the service
Write-Log "Starting service '$ServiceName'..."
try {
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($service) {
        Start-Service -Name $ServiceName
        Start-Sleep -Seconds 3

        $service = Get-Service -Name $ServiceName
        if ($service.Status -eq "Running") {
            Write-Log "Service started successfully"
        } else {
            Write-Log "ERROR: Service failed to start (status: $($service.Status))"
            exit 1
        }
    } else {
        Write-Log "Service not installed. Run install-service.ps1 first."
        exit 1
    }
} catch {
    Write-Log "ERROR: Could not start service - $_"
    exit 1
}

# Step 5: Health check
Write-Log "Running health check..."
Start-Sleep -Seconds 2
try {
    $response = Invoke-RestMethod -Uri "http://localhost:8000/health" -TimeoutSec 10
    if ($response.status -eq "healthy") {
        Write-Log "Health check passed!"
    } else {
        Write-Log "Warning: Unexpected health response"
    }
} catch {
    Write-Log "Warning: Health check failed - $_"
}

Write-Log "========== Deployment complete =========="

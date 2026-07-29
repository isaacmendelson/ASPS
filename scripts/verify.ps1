[CmdletBinding()]
param(
    [switch]$Bootstrap,
    [switch]$AllowRuntimeMismatch,
    [switch]$BypassDotnetSdkPin,
    [string]$PythonExecutable
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$resultsDir = Join-Path $repoRoot "artifacts/asps626"
$analyzerDir = Join-Path $repoRoot "Analyzers/basic-url-analyzer"
$desktopDir = Join-Path $repoRoot "apps/desktop/win"
$extensionTestsDir = Join-Path $repoRoot "apps/extension/chrome/tests"
$solution = Join-Path $repoRoot "ASPSBackend14_J/ASPSBackend.sln"

New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null
foreach ($resultName in @("dotnet.trx", "analyzer.xml", "desktop.xml", "extension.json")) {
    $resultPath = Join-Path $resultsDir $resultName
    if (Test-Path -LiteralPath $resultPath) {
        Remove-Item -LiteralPath $resultPath -Force
    }
}

function Invoke-Required {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][string]$Label
    )
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Label failed with exit code $LASTEXITCODE"
    }
}

function Assert-Version {
    param([string]$Label, [string]$Actual, [string]$Expected)
    if ($Actual.Trim() -ne $Expected) {
        if ($AllowRuntimeMismatch) {
            Write-Warning "$Label version is $($Actual.Trim()); reproducible baseline pins $Expected"
        } else {
            throw "$Label version is $($Actual.Trim()); expected exactly $Expected"
        }
    }
}

$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$node = (Get-Command node -ErrorAction Stop).Source
$npm = (Get-Command npm -ErrorAction Stop).Source

if ($PythonExecutable) {
    $basePython = (Resolve-Path $PythonExecutable).Path
    $basePythonArgs = @()
} else {
    $basePython = (Get-Command py -ErrorAction Stop).Source
    $basePythonArgs = @("-3.11")
}

if ($BypassDotnetSdkPin) {
    Push-Location ([IO.Path]::GetTempPath())
    try {
        $dotnetVersion = (& $dotnet --version)
    } finally {
        Pop-Location
    }
} else {
    Push-Location $repoRoot
    try {
        $dotnetVersion = (& $dotnet --version)
    } finally {
        Pop-Location
    }
}
Assert-Version ".NET SDK" $dotnetVersion "9.0.308"
Assert-Version "Node.js" ((& $node --version).TrimStart("v")) "22.23.2"
Assert-Version "npm" (& $npm --version) "10.9.8"

$analyzerPython = Join-Path $analyzerDir ".venv/Scripts/python.exe"
$desktopPython = Join-Path $desktopDir ".venv/Scripts/python.exe"

if ($Bootstrap) {
    if (-not (Test-Path $analyzerPython)) {
        Invoke-Required $basePython (@($basePythonArgs) + @("-m", "venv", (Join-Path $analyzerDir ".venv"))) "Analyzer venv"
    }
    if (-not (Test-Path $desktopPython)) {
        Invoke-Required $basePython (@($basePythonArgs) + @("-m", "venv", (Join-Path $desktopDir ".venv"))) "Desktop venv"
    }

    Invoke-Required $analyzerPython @(
        "-m", "pip", "install", "--disable-pip-version-check", "--no-deps",
        "-r", (Join-Path $analyzerDir "requirements.lock.txt")
    ) "Analyzer dependency install"
    Invoke-Required $desktopPython @(
        "-m", "pip", "install", "--disable-pip-version-check", "--no-deps",
        "-r", (Join-Path $desktopDir "requirements.lock.txt")
    ) "Desktop dependency install"
    Invoke-Required $analyzerPython @("-m", "pip", "check") "Analyzer dependency check"
    Invoke-Required $desktopPython @("-m", "pip", "check") "Desktop dependency check"

    Push-Location $extensionTestsDir
    try {
        Invoke-Required $npm @("ci", "--ignore-scripts") "Extension npm ci"
    } finally {
        Pop-Location
    }

    if ($BypassDotnetSdkPin) {
        Push-Location ([IO.Path]::GetTempPath())
    } else {
        Push-Location $repoRoot
    }
    try {
        Invoke-Required $dotnet @("restore", $solution, "--locked-mode") "NuGet locked restore"
    } finally {
        Pop-Location
    }
}

Assert-Version "Analyzer Python" (& $analyzerPython -c "import platform; print(platform.python_version())") "3.11.9"
Assert-Version "Desktop Python" (& $desktopPython -c "import platform; print(platform.python_version())") "3.11.9"

if ($BypassDotnetSdkPin) {
    Push-Location ([IO.Path]::GetTempPath())
} else {
    Push-Location $repoRoot
}
try {
    Invoke-Required $dotnet @("build", $solution, "-c", "Debug", "--no-restore", "--nologo") ".NET build"
    & $dotnet test (Join-Path $repoRoot "ASPSBackend14_J/ASPS.Tests/ASPS.Tests.csproj") `
        -c Debug --no-build --nologo `
        --logger "trx;LogFileName=dotnet.trx" `
        --results-directory $resultsDir
} finally {
    Pop-Location
}

Push-Location $analyzerDir
try {
    & $analyzerPython -m pytest tests --ignore=tests/test_real_sites.py -q `
        --junitxml (Join-Path $resultsDir "analyzer.xml")
} finally {
    Pop-Location
}

Push-Location $desktopDir
try {
    & $desktopPython -m pytest -q --junitxml (Join-Path $resultsDir "desktop.xml")
} finally {
    Pop-Location
}

$extensionJson = Join-Path $resultsDir "extension.json"
Push-Location $extensionTestsDir
try {
    & $npm test -- --runInBand --silent --json "--outputFile=$extensionJson"
} finally {
    Pop-Location
}

Invoke-Required $analyzerPython @(
    (Join-Path $repoRoot "scripts/check_test_baseline.py"),
    "--manifest", (Join-Path $repoRoot "scripts/test-baseline-exceptions.json"),
    "--dotnet", (Join-Path $resultsDir "dotnet.trx"),
    "--analyzer", (Join-Path $resultsDir "analyzer.xml"),
    "--desktop", (Join-Path $resultsDir "desktop.xml"),
    "--extension", $extensionJson
) "Exact exception baseline"

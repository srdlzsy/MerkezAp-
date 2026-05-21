param(
    [string]$RepoRoot = "",
    [string]$SiteName = "FurpaMerkez",
    [string]$PublishPath = "",
    [string]$ProjectPath = "",
    [string]$LocalSettingsPath = "",
    [string]$HealthBaseUrl = "http://10.0.0.100:7508",
    [switch]$SkipPublish,
    [switch]$SkipRestore,
    [switch]$RunStandalone,
    [int]$StandalonePort = 5026
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = Split-Path -Parent $PSScriptRoot
}

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Join-Path $RepoRoot "src\FurpaMerkezApi.WebApi\FurpaMerkezApi.WebApi.csproj"
}

if ([string]::IsNullOrWhiteSpace($LocalSettingsPath)) {
    $LocalSettingsPath = Join-Path $RepoRoot "src\FurpaMerkezApi.WebApi\appsettings.Local.json"
}

$appPool = $null

if (Test-Path $RepoRoot) {
    Set-Location $RepoRoot
}

try {
    Import-Module WebAdministration -ErrorAction Stop
    $site = Get-Website -Name $SiteName -ErrorAction SilentlyContinue

    if ($site -ne $null) {
        $appPool = $site.ApplicationPool

        if ([string]::IsNullOrWhiteSpace($PublishPath)) {
            $PublishPath = $site.PhysicalPath
        }
    }
}
catch {
    Write-Warning "WebAdministration module could not be loaded. IIS-specific steps will be skipped unless paths are provided."
}

if ([string]::IsNullOrWhiteSpace($PublishPath)) {
    $PublishPath = "C:\inetpub\FurpaMerkezApi"
}

Write-Step "Deployment settings"
Write-Host "Repo        : $RepoRoot"
Write-Host "Project     : $ProjectPath"
Write-Host "Publish path: $PublishPath"
Write-Host "Local config: $LocalSettingsPath"
Write-Host "Site        : $SiteName"
Write-Host "App Pool    : $appPool"

if (-not $SkipPublish -and -not (Test-Path $ProjectPath)) {
    throw "Project file was not found: $ProjectPath. If this server only has published files, run with -SkipPublish."
}

if ($appPool) {
    Write-Step "Stopping App Pool"
    Stop-WebAppPool $appPool -ErrorAction SilentlyContinue
}

if (-not $SkipPublish) {
    if (-not $SkipRestore) {
        Write-Step "Restoring NuGet packages"
        dotnet restore $ProjectPath
    }

    Write-Step "Publishing Release build"
    dotnet publish $ProjectPath -c Release -o $PublishPath
}
else {
    Write-Step "Skipping dotnet publish"
}

if (Test-Path $LocalSettingsPath) {
    Write-Step "Copying appsettings.Local.json"
    Copy-Item $LocalSettingsPath (Join-Path $PublishPath "appsettings.Local.json") -Force
}
else {
    Write-Warning "appsettings.Local.json was not found: $LocalSettingsPath"
}

Write-Step "Creating writable folders"
$logsPath = Join-Path $PublishPath "logs"
$keysPath = Join-Path $PublishPath "AppDataKeys"
$operationsExportsPath = Join-Path $PublishPath "App_Data\OperationsExports"
New-Item -ItemType Directory -Force -Path $logsPath | Out-Null
New-Item -ItemType Directory -Force -Path $keysPath | Out-Null
New-Item -ItemType Directory -Force -Path $operationsExportsPath | Out-Null

Write-Step "Applying IIS permissions"
if ($appPool) {
    $identity = "IIS AppPool\$appPool"
    icacls $PublishPath /grant "${identity}:(OI)(CI)(RX)" | Out-Host
    icacls $logsPath /grant "${identity}:(OI)(CI)(M)" /T | Out-Host
    icacls $keysPath /grant "${identity}:(OI)(CI)(M)" /T | Out-Host
    icacls $operationsExportsPath /grant "${identity}:(OI)(CI)(M)" /T | Out-Host

    Write-Step "Setting App Pool to No Managed Code"
    Set-ItemProperty "IIS:\AppPools\$appPool" -Name managedRuntimeVersion -Value ""

    Write-Step "Starting App Pool"
    Start-WebAppPool $appPool
    Get-WebAppPoolState $appPool | Out-Host
}
else {
    icacls $logsPath /grant "IIS_IUSRS:(OI)(CI)(M)" /T | Out-Host
    icacls $keysPath /grant "IIS_IUSRS:(OI)(CI)(M)" /T | Out-Host
    icacls $operationsExportsPath /grant "IIS_IUSRS:(OI)(CI)(M)" /T | Out-Host
}

if (-not [string]::IsNullOrWhiteSpace($HealthBaseUrl)) {
    Write-Step "Testing IIS endpoints"
    curl.exe -i "$HealthBaseUrl/"
    curl.exe -i "$HealthBaseUrl/health/ready"
    curl.exe -i "$HealthBaseUrl/__test__"
}

if ($RunStandalone) {
    Write-Step "Running standalone app on port $StandalonePort"
    Set-Location $PublishPath
    dotnet .\FurpaMerkezApi.WebApi.dll --urls="http://0.0.0.0:$StandalonePort"
}

param(
    [string]$RepoRoot = "",
    [string]$SiteName = "FurpaMerkez",
    [string]$PublishPath = "",
    [string]$ProjectPath = "",
    [string]$LocalSettingsPath = "",
    [string]$HealthBaseUrl = "http://10.0.0.100:7508",
    [ValidateSet("None", "Legacy", "Desktop")]
    [string]$MikroDatabaseTarget = "None",
    [switch]$ConfirmMikroApiMapping,
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
Write-Host "Mikro target: $MikroDatabaseTarget"

if ($MikroDatabaseTarget -ne "None" -and -not $ConfirmMikroApiMapping) {
    throw "Confirm the Mikro API FURPA_2024 company mapping, then re-run with -ConfirmMikroApiMapping."
}

if ($MikroDatabaseTarget -ne "None" -and -not $appPool) {
    throw "An IIS application pool is required for a controlled Mikro database cutover."
}

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

$publishedLocalSettingsPath = Join-Path $PublishPath "appsettings.Local.json"
$publishedProductionSettingsPath = Join-Path $PublishPath "appsettings.Production.json"
$effectiveSettingsPath = if (Test-Path -LiteralPath $publishedLocalSettingsPath) {
    $publishedLocalSettingsPath
}
else {
    $publishedProductionSettingsPath
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

    Write-Step "Configuring App Pool for continuously running background workers"
    Set-ItemProperty "IIS:\AppPools\$appPool" -Name startMode -Value "AlwaysRunning"
    Set-ItemProperty "IIS:\AppPools\$appPool" -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)

    Write-Step "Enabling IIS application preload"
    Set-WebConfigurationProperty `
        -PSPath "MACHINE/WEBROOT/APPHOST" `
        -Filter "system.applicationHost/sites/site[@name='$SiteName']/application[@path='/']" `
        -Name "preloadEnabled" `
        -Value $true

    $applicationInitializationModule = Get-WebGlobalModule -Name "ApplicationInitializationModule" -ErrorAction SilentlyContinue
    if ($null -eq $applicationInitializationModule) {
        Write-Warning "IIS Application Initialization is not installed. preloadEnabled was set, but preload requires the Web-AppInit Windows feature."
    }

    if ($MikroDatabaseTarget -ne "None") {
        Write-Step "Applying controlled Mikro database cutover"
        $switchScript = Join-Path $PSScriptRoot "Switch-MikroDatabase.ps1"
        if (-not (Test-Path -LiteralPath $switchScript)) {
            throw "Mikro database switch script was not found: $switchScript"
        }

        & $switchScript `
            -Target $MikroDatabaseTarget `
            -ConfigurationPath $effectiveSettingsPath `
            -SiteName $SiteName `
            -HealthBaseUrl $HealthBaseUrl `
            -Apply `
            -ConfirmMikroApiMapping
    }
    else {
        Write-Step "Starting App Pool"
        Start-WebAppPool $appPool
        Get-WebAppPoolState $appPool | Out-Host
    }

    Write-Host "Start mode  : $((Get-ItemProperty "IIS:\AppPools\$appPool").startMode)"
    Write-Host "Idle timeout: $((Get-ItemProperty "IIS:\AppPools\$appPool").processModel.idleTimeout)"
    Write-Host "Preload     : $((Get-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter "system.applicationHost/sites/site[@name='$SiteName']/application[@path='/']" -Name 'preloadEnabled').Value)"
}
else {
    icacls $logsPath /grant "IIS_IUSRS:(OI)(CI)(M)" /T | Out-Host
    icacls $keysPath /grant "IIS_IUSRS:(OI)(CI)(M)" /T | Out-Host
    icacls $operationsExportsPath /grant "IIS_IUSRS:(OI)(CI)(M)" /T | Out-Host
}

if (-not [string]::IsNullOrWhiteSpace($HealthBaseUrl)) {
    Write-Step "Testing IIS endpoints"
    $rootResponse = Invoke-RestMethod -Uri ($HealthBaseUrl.TrimEnd('/') + "/") -Method Get -TimeoutSec 15
    $rootResponse | ConvertTo-Json -Depth 10 | Write-Host

    if ($MikroDatabaseTarget -ne "None") {
        $expectedDatabase = if ($MikroDatabaseTarget -eq "Desktop") {
            "MikroDesktop_FURPA_2024"
        }
        else {
            "MikroDB_V16_FURPA_2024"
        }

        if (-not ([string]$rootResponse.businessDatabase).Equals(
            $expectedDatabase,
            [StringComparison]::OrdinalIgnoreCase)) {
            if ($appPool) {
                Stop-WebAppPool $appPool -ErrorAction SilentlyContinue
            }
            throw "Published application reports '$($rootResponse.businessDatabase)' instead of '$expectedDatabase'. The app pool was stopped."
        }
    }

    Invoke-WebRequest `
        -Uri ($HealthBaseUrl.TrimEnd('/') + "/health/ready") `
        -Method Get `
        -TimeoutSec 15 `
        -UseBasicParsing | Out-Null
    Write-Host "Readiness check succeeded."
}

if ($RunStandalone) {
    Write-Step "Running standalone app on port $StandalonePort"
    Set-Location $PublishPath
    dotnet .\FurpaMerkezApi.WebApi.dll --urls="http://0.0.0.0:$StandalonePort"
}

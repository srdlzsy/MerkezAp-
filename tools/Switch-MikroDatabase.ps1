[CmdletBinding()]
param(
    [ValidateSet("Legacy", "Desktop")]
    [string]$Target = "Desktop",
    [string]$ConfigurationPath = "src/FurpaMerkezApi.WebApi/appsettings.Production.json",
    [string]$SiteName = "FurpaMerkez",
    [string]$HealthBaseUrl = "http://10.0.0.100:7508",
    [switch]$Apply,
    [switch]$SkipIis,
    [switch]$ConfirmMikroApiMapping
)

$ErrorActionPreference = "Stop"

$legacyDatabase = "MikroDB_V16_FURPA_2024"
$desktopDatabase = "MikroDesktop_FURPA_2024"
$targetDatabase = if ($Target -eq "Desktop") { $desktopDatabase } else { $legacyDatabase }
$referenceSourceDatabase = if ($Target -eq "Desktop") { $legacyDatabase } else { $desktopDatabase }
$resolvedConfigurationPath = (Resolve-Path -LiteralPath $ConfigurationPath).Path
$configuration = Get-Content -LiteralPath $resolvedConfigurationPath -Raw | ConvertFrom-Json

function Get-DatabaseName {
    param([Parameter(Mandatory)][string]$ConnectionString)

    $match = [regex]::Match($ConnectionString, '(?i)(?:Database|Initial Catalog)=([^;]+)')
    if (-not $match.Success) {
        throw "Database name could not be resolved from the Mikro connection string."
    }

    return $match.Groups[1].Value.Trim()
}

function Set-DatabaseName {
    param(
        [Parameter(Mandatory)][string]$ConnectionString,
        [Parameter(Mandatory)][string]$DatabaseName
    )

    $builder = [System.Data.Common.DbConnectionStringBuilder]::new()
    $builder.ConnectionString = $ConnectionString
    if ($builder.ContainsKey("Database")) {
        $builder["Database"] = $DatabaseName
    }
    elseif ($builder.ContainsKey("Initial Catalog")) {
        $builder["Initial Catalog"] = $DatabaseName
    }
    else {
        $builder["Database"] = $DatabaseName
    }

    return $builder.ConnectionString
}

function Get-AppPoolName {
    param([string]$IisSiteName)

    Import-Module WebAdministration -ErrorAction Stop
    $site = Get-Website -Name $IisSiteName -ErrorAction Stop
    return [string]$site.ApplicationPool
}

function Wait-ForApplicationDatabase {
    param(
        [Parameter(Mandatory)][string]$BaseUrl,
        [Parameter(Mandatory)][string]$ExpectedDatabase,
        [int]$AttemptCount = 20,
        [int]$DelaySeconds = 3
    )

    $rootUrl = $BaseUrl.TrimEnd('/') + "/"
    $lastError = $null

    for ($attempt = 1; $attempt -le $AttemptCount; $attempt++) {
        try {
            $response = Invoke-RestMethod -Uri $rootUrl -Method Get -TimeoutSec 10
            $activeDatabase = [string]$response.businessDatabase
            if ($activeDatabase.Equals($ExpectedDatabase, [StringComparison]::OrdinalIgnoreCase)) {
                Write-Host "Application verified on database '$activeDatabase'."

                try {
                    $readyUrl = $BaseUrl.TrimEnd('/') + "/health/ready"
                    Invoke-WebRequest -Uri $readyUrl -Method Get -TimeoutSec 15 -UseBasicParsing | Out-Null
                    Write-Host "Readiness check succeeded."
                }
                catch {
                    Write-Warning "Application started on the correct database, but readiness reported another dependency problem: $($_.Exception.Message)"
                }

                return
            }

            $lastError = "Application reports '$activeDatabase' instead of '$ExpectedDatabase'."
        }
        catch {
            $lastError = $_.Exception.Message
        }

        if ($attempt -lt $AttemptCount) {
            Start-Sleep -Seconds $DelaySeconds
        }
    }

    throw "Application database verification failed after $AttemptCount attempts. Last error: $lastError"
}

$readConnection = [string]$configuration.ConnectionStrings.MikroConnection
$writeConnection = [string]$configuration.ConnectionStrings.MikroWriteConnection
if ([string]::IsNullOrWhiteSpace($readConnection) -or
    [string]::IsNullOrWhiteSpace($writeConnection)) {
    throw "MikroConnection and MikroWriteConnection must both be configured."
}

$currentReadDatabase = Get-DatabaseName $readConnection
$currentWriteDatabase = Get-DatabaseName $writeConnection
if (-not $currentReadDatabase.Equals($currentWriteDatabase, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Read and write Mikro connections currently target different databases. Resolve this before switching."
}

$mikroApiRoutes = @($configuration.MikroWriteRouting.PSObject.Properties |
    Where-Object { [string]$_.Value -eq "MikroApi" } |
    Select-Object -ExpandProperty Name)

Write-Host "Current database : $currentReadDatabase"
Write-Host "Target database  : $targetDatabase"
Write-Host "Configuration    : $resolvedConfigurationPath"
Write-Host "Mikro API routes : $($mikroApiRoutes.Count)"

$referenceScript = Join-Path $PSScriptRoot "Set-FurpaMikroDatabaseReferences.ps1"
if (-not (Test-Path -LiteralPath $referenceScript)) {
    throw "Database reference script was not found: $referenceScript"
}

$referencePreview = & $referenceScript `
    -ConfigurationPath $resolvedConfigurationPath `
    -SourceDatabase $referenceSourceDatabase `
    -TargetDatabase $targetDatabase `
    -PassThru

Write-Host "Furpa SQL objects : $($referencePreview.MatchedObjectCount)"
Write-Host "Old DB references: $($referencePreview.SourceReferenceCount)"

if ($mikroApiRoutes.Count -gt 0) {
    Write-Warning "Mikro API company FURPA_2024 must target '$targetDatabase' at cutover time."
    if ($Apply -and -not $ConfirmMikroApiMapping) {
        throw "Re-run with -ConfirmMikroApiMapping after the Mikro API company mapping is changed."
    }
}

if (-not $Apply) {
    Write-Host "Dry-run completed. No configuration, IIS, or SQL object was changed."
    Write-Host "Apply at cutover time with:"
    Write-Host ".\tools\Switch-MikroDatabase.ps1 -Target $Target -ConfigurationPath 'C:\inetpub\FurpaMerkezApi\appsettings.Production.json' -SiteName '$SiteName' -Apply -ConfirmMikroApiMapping"
    return
}

$backupPath = "$resolvedConfigurationPath.before-$Target-$(Get-Date -Format 'yyyyMMdd-HHmmss').bak"
$appPoolName = $null
$configurationUpdated = $false
$referencesUpdated = $false
$appPoolStarted = $false
$switchFailed = $false

try {
    if (-not $SkipIis) {
        $appPoolName = Get-AppPoolName $SiteName
        $appPoolState = (Get-WebAppPoolState -Name $appPoolName).Value
        if ($appPoolState -eq "Started") {
            Write-Host "Stopping IIS app pool '$appPoolName'..."
            Stop-WebAppPool -Name $appPoolName -ErrorAction Stop
        }
        else {
            Write-Host "IIS app pool '$appPoolName' is already stopped."
        }
    }

    Copy-Item -LiteralPath $resolvedConfigurationPath -Destination $backupPath -Force

    $referenceResult = & $referenceScript `
        -ConfigurationPath $resolvedConfigurationPath `
        -SourceDatabase $referenceSourceDatabase `
        -TargetDatabase $targetDatabase `
        -Apply `
        -PassThru
    $referencesUpdated = $referenceResult.UpdatedObjectCount -gt 0

    $configuration.ConnectionStrings.MikroConnection = Set-DatabaseName $readConnection $targetDatabase
    $configuration.ConnectionStrings.MikroWriteConnection = Set-DatabaseName $writeConnection $targetDatabase
    if ($null -eq $configuration.MikroDatabase) {
        $configuration | Add-Member -NotePropertyName MikroDatabase -NotePropertyValue ([pscustomobject]@{})
    }
    if ($null -eq $configuration.MikroDatabase.PSObject.Properties["ExpectedDatabase"]) {
        $configuration.MikroDatabase | Add-Member -NotePropertyName ExpectedDatabase -NotePropertyValue $targetDatabase
    }
    else {
        $configuration.MikroDatabase.ExpectedDatabase = $targetDatabase
    }

    $json = $configuration | ConvertTo-Json -Depth 100
    [System.IO.File]::WriteAllText($resolvedConfigurationPath, $json, [System.Text.UTF8Encoding]::new($false))
    $configurationUpdated = $true

    $verifiedConfiguration = Get-Content -LiteralPath $resolvedConfigurationPath -Raw | ConvertFrom-Json
    $verifiedReadDatabase = Get-DatabaseName ([string]$verifiedConfiguration.ConnectionStrings.MikroConnection)
    $verifiedWriteDatabase = Get-DatabaseName ([string]$verifiedConfiguration.ConnectionStrings.MikroWriteConnection)
    if ($verifiedReadDatabase -ne $targetDatabase -or $verifiedWriteDatabase -ne $targetDatabase) {
        throw "Configuration verification failed after the database switch."
    }

    Write-Host "Mikro database switch completed: $currentReadDatabase -> $targetDatabase"
    Write-Host "Configuration backup: $backupPath"

    if (-not $SkipIis) {
        Write-Host "Starting IIS app pool '$appPoolName'..."
        Start-WebAppPool -Name $appPoolName -ErrorAction Stop
        $appPoolStarted = $true
        Get-WebAppPoolState -Name $appPoolName | Out-Host
        Wait-ForApplicationDatabase -BaseUrl $HealthBaseUrl -ExpectedDatabase $targetDatabase
    }
}
catch {
    $switchFailed = $true
    Write-Warning "Database switch failed. Restoring the previous configuration and SQL references. Error: $($_.Exception.Message)"

    if (-not $SkipIis -and $appPoolStarted) {
        Stop-WebAppPool -Name $appPoolName -ErrorAction SilentlyContinue
        $appPoolStarted = $false
    }

    if ($configurationUpdated -and (Test-Path -LiteralPath $backupPath)) {
        Copy-Item -LiteralPath $backupPath -Destination $resolvedConfigurationPath -Force
    }

    if ($referencesUpdated) {
        Write-Warning "Rolling Furpa procedure references back to '$referenceSourceDatabase'."
        & $referenceScript `
            -ConfigurationPath $resolvedConfigurationPath `
            -SourceDatabase $targetDatabase `
            -TargetDatabase $referenceSourceDatabase `
            -Apply
    }

    if (-not $SkipIis -and -not [string]::IsNullOrWhiteSpace($appPoolName)) {
        Write-Warning "IIS app pool '$appPoolName' is intentionally left stopped."
        Write-Warning "Point Mikro API company FURPA_2024 back to '$referenceSourceDatabase', then run the Legacy/Desktop switch command for that database."
    }

    throw
}
finally {
    if (-not $SkipIis -and
        -not [string]::IsNullOrWhiteSpace($appPoolName) -and
        -not $appPoolStarted -and
        -not $switchFailed) {
        Write-Host "Starting IIS app pool '$appPoolName'..."
        Start-WebAppPool -Name $appPoolName -ErrorAction Stop
        Get-WebAppPoolState -Name $appPoolName | Out-Host
    }
}

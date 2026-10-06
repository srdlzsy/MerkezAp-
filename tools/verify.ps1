param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

function Invoke-DotNet {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

Push-Location $root
try {
    Invoke-DotNet restore FurpaMerkezApi.sln
    Invoke-DotNet restore tools/AuthDbMigrator/AuthDbMigrator.csproj
    Invoke-DotNet restore tools/OfflineRecoveryMigration/OfflineRecoveryMigration.csproj

    Invoke-DotNet build FurpaMerkezApi.sln --configuration $Configuration --no-restore
    Invoke-DotNet build tools/AuthDbMigrator/AuthDbMigrator.csproj --configuration $Configuration --no-restore
    Invoke-DotNet build tools/OfflineRecoveryMigration/OfflineRecoveryMigration.csproj --configuration $Configuration --no-restore

    Invoke-DotNet test FurpaMerkezApi.sln --configuration $Configuration --no-build

    Invoke-DotNet list FurpaMerkezApi.sln package --vulnerable --include-transitive
    Invoke-DotNet list tools/AuthDbMigrator/AuthDbMigrator.csproj package --vulnerable --include-transitive
}
finally {
    Pop-Location
}

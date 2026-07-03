param(
    [string]$SettingsPath = "",
    [string]$SqlServer = "10.0.0.207",
    [string[]]$ConnectionNames = @(
        "AuthConnection",
        "FurpaConnection",
        "MikroConnection",
        "MikroWriteConnection",
        "MaydayConnection",
        "UyumConnection",
        "AxataConnection"
    ),
    [int]$IntervalSeconds = 5,
    [int]$Samples = 120,
    [string]$OutPath = "",
    [switch]$IncludeSqlActivity,
    [int]$SlowRequestSeconds = 5,
    [switch]$IncludePerfCounters
)

$ErrorActionPreference = "Stop"

function Resolve-RepoRoot {
    return Split-Path -Parent $PSScriptRoot
}

function New-HealthRow {
    param(
        [string]$Kind,
        [string]$Target,
        [bool]$Success,
        [Nullable[int]]$ElapsedMs,
        [string]$Detail
    )

    [pscustomobject]@{
        Timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss.fff")
        Kind = $Kind
        Target = $Target
        Success = $Success
        ElapsedMs = $ElapsedMs
        Detail = $Detail
    }
}

function Read-Settings {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Settings file was not found: $Path"
    }

    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Measure-Ping {
    param([string]$ComputerName)

    $ping = New-Object System.Net.NetworkInformation.Ping

    try {
        $reply = $ping.Send($ComputerName, 3000)
        if ($reply.Status -ne [System.Net.NetworkInformation.IPStatus]::Success) {
            throw "Ping status: $($reply.Status)"
        }

        New-HealthRow -Kind "Ping" -Target $ComputerName -Success $true -ElapsedMs ([int]$reply.RoundtripTime) -Detail "Reply received"
    }
    catch {
        New-HealthRow -Kind "Ping" -Target $ComputerName -Success $false -ElapsedMs $null -Detail $_.Exception.Message
    }
    finally {
        $ping.Dispose()
    }
}

function Measure-TcpPort {
    param(
        [string]$ComputerName,
        [int]$Port
    )

    $client = New-Object System.Net.Sockets.TcpClient
    $watch = [System.Diagnostics.Stopwatch]::StartNew()

    try {
        $connectTask = $client.ConnectAsync($ComputerName, $Port)
        if (-not $connectTask.Wait(3000)) {
            throw "TCP connect timed out after 3000ms"
        }

        $watch.Stop()
        New-HealthRow -Kind "TcpPort" -Target "$ComputerName`:$Port" -Success $true -ElapsedMs ([int]$watch.ElapsedMilliseconds) -Detail "Connected"
    }
    catch {
        $watch.Stop()
        New-HealthRow -Kind "TcpPort" -Target "$ComputerName`:$Port" -Success $false -ElapsedMs ([int]$watch.ElapsedMilliseconds) -Detail $_.Exception.Message
    }
    finally {
        $client.Dispose()
    }
}

function Measure-SqlConnection {
    param(
        [string]$Name,
        [string]$ConnectionString
    )

    $ConnectionString = Convert-ToPowerShellSqlConnectionString -ConnectionString $ConnectionString
    $connection = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
    $command = $connection.CreateCommand()
    $command.CommandTimeout = 5
    $command.CommandText = "SET NOCOUNT ON; SELECT DB_NAME();"
    $watch = [System.Diagnostics.Stopwatch]::StartNew()

    try {
        $connection.Open()
        $databaseName = [string]$command.ExecuteScalar()
        $watch.Stop()

        New-HealthRow -Kind "SqlSelect1" -Target "$Name/$databaseName" -Success $true -ElapsedMs ([int]$watch.ElapsedMilliseconds) -Detail "Open + SELECT DB_NAME()"
    }
    catch {
        $watch.Stop()
        New-HealthRow -Kind "SqlSelect1" -Target $Name -Success $false -ElapsedMs ([int]$watch.ElapsedMilliseconds) -Detail $_.Exception.Message
    }
    finally {
        $command.Dispose()
        $connection.Dispose()
    }
}

function Measure-SqlActivity {
    param(
        [string]$ConnectionString,
        [int]$MinimumElapsedSeconds
    )

    $ConnectionString = Convert-ToPowerShellSqlConnectionString -ConnectionString $ConnectionString
    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $ConnectionString
    $builder["Initial Catalog"] = "master"
    $connection = New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
    $command = $connection.CreateCommand()
    $command.CommandTimeout = 5
    $command.CommandText = @"
SET NOCOUNT ON;
SELECT
    COUNT(*) AS active_requests,
    SUM(CASE WHEN r.blocking_session_id <> 0 THEN 1 ELSE 0 END) AS blocked_requests,
    MAX(r.total_elapsed_time) AS max_elapsed_ms
FROM sys.dm_exec_requests AS r
INNER JOIN sys.dm_exec_sessions AS s ON s.session_id = r.session_id
WHERE r.session_id <> @@SPID
  AND s.is_user_process = 1
  AND r.total_elapsed_time >= @minimum_elapsed_ms;
"@
    [void]$command.Parameters.Add("@minimum_elapsed_ms", [System.Data.SqlDbType]::Int)
    $command.Parameters["@minimum_elapsed_ms"].Value = $MinimumElapsedSeconds * 1000
    $watch = [System.Diagnostics.Stopwatch]::StartNew()

    try {
        $connection.Open()
        $reader = $command.ExecuteReader()
        [void]$reader.Read()

        $active = if ($reader.IsDBNull(0)) { 0 } else { $reader.GetInt32(0) }
        $blocked = if ($reader.IsDBNull(1)) { 0 } else { $reader.GetInt32(1) }
        $maxElapsed = if ($reader.IsDBNull(2)) { 0 } else { $reader.GetInt32(2) }
        $reader.Close()
        $watch.Stop()

        $detail = "Active requests >= ${MinimumElapsedSeconds}s: $active; blocked: $blocked; max elapsed ms: $maxElapsed"
        New-HealthRow -Kind "SqlActivity" -Target $SqlServer -Success $true -ElapsedMs ([int]$watch.ElapsedMilliseconds) -Detail $detail
    }
    catch {
        $watch.Stop()
        New-HealthRow -Kind "SqlActivity" -Target $SqlServer -Success $false -ElapsedMs ([int]$watch.ElapsedMilliseconds) -Detail $_.Exception.Message
    }
    finally {
        $command.Dispose()
        $connection.Dispose()
    }
}

function Measure-PerfCounters {
    param([string]$ComputerName)

    $counters = @(
        "\Processor(_Total)\% Processor Time",
        "\Memory\Available MBytes",
        "\PhysicalDisk(_Total)\% Disk Time",
        "\PhysicalDisk(_Total)\Avg. Disk sec/Read",
        "\PhysicalDisk(_Total)\Avg. Disk sec/Write"
    )

    try {
        $samples = Get-Counter -ComputerName $ComputerName -Counter $counters -ErrorAction Stop
        foreach ($sample in $samples.CounterSamples) {
            New-HealthRow -Kind "PerfCounter" -Target $sample.Path -Success $true -ElapsedMs $null -Detail ([math]::Round($sample.CookedValue, 3).ToString())
        }
    }
    catch {
        New-HealthRow -Kind "PerfCounter" -Target $ComputerName -Success $false -ElapsedMs $null -Detail $_.Exception.Message
    }
}

function Convert-ToPowerShellSqlConnectionString {
    param([string]$ConnectionString)

    # Windows PowerShell's built-in SqlClient does not understand Encrypt=Optional.
    return ($ConnectionString -replace "(?i)Encrypt=Optional", "Encrypt=False")
}

$repoRoot = Resolve-RepoRoot

if ([string]::IsNullOrWhiteSpace($SettingsPath)) {
    $localSettingsPath = Join-Path $repoRoot "src\FurpaMerkezApi.WebApi\appsettings.Local.json"
    $productionSettingsPath = Join-Path $repoRoot "src\FurpaMerkezApi.WebApi\appsettings.Production.json"

    if (Test-Path -LiteralPath $localSettingsPath) {
        $SettingsPath = $localSettingsPath
    }
    else {
        $SettingsPath = $productionSettingsPath
    }
}

if ([string]::IsNullOrWhiteSpace($OutPath)) {
    $logDir = Join-Path $repoRoot "logs"
    if (-not (Test-Path -LiteralPath $logDir)) {
        New-Item -ItemType Directory -Path $logDir | Out-Null
    }

    $OutPath = Join-Path $logDir ("sql-207-health-{0}.csv" -f (Get-Date).ToString("yyyyMMdd-HHmmss"))
}

$settings = Read-Settings -Path $SettingsPath
$connectionStrings = $settings.ConnectionStrings
$resolvedConnectionNames = @()
foreach ($name in $ConnectionNames) {
    $resolvedConnectionNames += ($name -split "," | ForEach-Object { $_.Trim() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

$selectedConnections = @()

foreach ($name in $resolvedConnectionNames) {
    $value = $connectionStrings.$name
    if (-not [string]::IsNullOrWhiteSpace($value) -and $value -match "Server=$([regex]::Escape($SqlServer))") {
        $selectedConnections += [pscustomobject]@{
            Name = $name
            ConnectionString = $value
        }
    }
}

if ($selectedConnections.Count -eq 0) {
    throw "No connection strings for SQL server '$SqlServer' were found in $SettingsPath."
}

Write-Host "Watching SQL server: $SqlServer"
Write-Host "Settings         : $SettingsPath"
Write-Host "Output CSV       : $OutPath"
Write-Host "Interval/Samples : $IntervalSeconds sec / $Samples"
Write-Host "Connections      : $($selectedConnections.Name -join ', ')"
Write-Host ""

for ($index = 1; $index -le $Samples; $index++) {
    $rows = @()
    $rows += Measure-Ping -ComputerName $SqlServer
    $rows += Measure-TcpPort -ComputerName $SqlServer -Port 1433

    foreach ($connection in $selectedConnections) {
        $rows += Measure-SqlConnection -Name $connection.Name -ConnectionString $connection.ConnectionString
    }

    if ($IncludeSqlActivity) {
        $rows += Measure-SqlActivity -ConnectionString $selectedConnections[0].ConnectionString -MinimumElapsedSeconds $SlowRequestSeconds
    }

    if ($IncludePerfCounters) {
        $rows += Measure-PerfCounters -ComputerName $SqlServer
    }

    $rows | Export-Csv -LiteralPath $OutPath -NoTypeInformation -Append -Encoding UTF8

    $summary = $rows |
        Where-Object { $_.Kind -in @("Ping", "TcpPort", "SqlSelect1") } |
        ForEach-Object { "$($_.Kind) $($_.Target)=$($_.ElapsedMs)ms/$($_.Success)" }

    Write-Host ("[{0}/{1}] {2}" -f $index, $Samples, ($summary -join "; "))

    if ($index -lt $Samples) {
        Start-Sleep -Seconds $IntervalSeconds
    }
}

Write-Host ""
Write-Host "Done. CSV: $OutPath"

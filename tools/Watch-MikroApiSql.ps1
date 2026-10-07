param(
    [string]$SettingsPath = "",

    [string]$ConnectionName = "MikroConnection",

    [ValidateRange(100, 60000)]
    [int]$IntervalMilliseconds = 1000,

    [ValidateRange(1, 3600)]
    [int]$DurationSeconds = 90,

    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($SettingsPath)) {
    $SettingsPath = Join-Path $repoRoot "src\FurpaMerkezApi.WebApi\appsettings.Production.json"
}

if (-not (Test-Path -LiteralPath $SettingsPath)) {
    throw "Settings file was not found: $SettingsPath"
}

$settings = Get-Content -Raw -LiteralPath $SettingsPath | ConvertFrom-Json
$connectionString = [string]$settings.ConnectionStrings.$ConnectionName

if ([string]::IsNullOrWhiteSpace($connectionString)) {
    throw "Connection string '$ConnectionName' was not found in $SettingsPath."
}

$connectionString = $connectionString -replace "(?i)Encrypt=Optional", "Encrypt=False"
$builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $connectionString
$builder["Initial Catalog"] = "master"

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $outputDirectory = Join-Path $repoRoot "outputs"
    if (-not (Test-Path -LiteralPath $outputDirectory)) {
        New-Item -ItemType Directory -Path $outputDirectory | Out-Null
    }

    $OutputPath = Join-Path $outputDirectory (
        "mikro-api-sql-{0}.csv" -f (Get-Date).ToString("yyyyMMdd-HHmmss"))
}

$query = @"
SET NOCOUNT ON;

SELECT
    SYSDATETIME() AS snapshot_time,
    s.session_id,
    s.login_name,
    s.host_name,
    s.host_process_id,
    s.program_name,
    s.status AS session_status,
    r.status AS request_status,
    r.command,
    r.wait_type,
    r.wait_time,
    r.blocking_session_id,
    r.cpu_time,
    r.total_elapsed_time,
    r.logical_reads,
    r.reads,
    r.writes,
    s.open_transaction_count,
    DB_NAME(r.database_id) AS database_name,
    REPLACE(REPLACE(t.text, CHAR(13), ' '), CHAR(10), ' ') AS sql_text
FROM sys.dm_exec_sessions AS s
INNER JOIN sys.dm_exec_requests AS r
    ON r.session_id = s.session_id
OUTER APPLY sys.dm_exec_sql_text(r.sql_handle) AS t
WHERE s.is_user_process = 1
  AND s.session_id <> @@SPID
  AND (
      s.host_name = N'MIKRO-SUNUCU'
      OR (
      s.program_name LIKE N'%MİKRO APİ%'
      OR s.program_name LIKE N'%MIKRO API%'
      OR s.program_name LIKE N'%MikroApi%'
      )
  )
ORDER BY r.total_elapsed_time DESC;
"@

$connection = New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
$command = $connection.CreateCommand()
$command.CommandTimeout = 5
$command.CommandText = $query

$sampleCount = [math]::Ceiling(($DurationSeconds * 1000) / $IntervalMilliseconds)
$capturedRowCount = 0

Write-Host "Watching Mikro API SQL sessions"
Write-Host "SQL server : $($builder.DataSource)"
Write-Host "Duration   : $DurationSeconds sec"
Write-Host "Interval   : $IntervalMilliseconds ms"
Write-Host "Output CSV : $OutputPath"
Write-Host ""

try {
    $connection.Open()

    for ($sample = 1; $sample -le $sampleCount; $sample++) {
        $table = New-Object System.Data.DataTable
        $adapter = New-Object System.Data.SqlClient.SqlDataAdapter $command

        try {
            [void]$adapter.Fill($table)
        }
        finally {
            $adapter.Dispose()
        }

        $rows = foreach ($row in $table.Rows) {
            [pscustomobject]@{
                snapshot_time = $row.snapshot_time
                session_id = $row.session_id
                login_name = $row.login_name
                host_name = $row.host_name
                host_process_id = $row.host_process_id
                program_name = $row.program_name
                session_status = $row.session_status
                request_status = $row.request_status
                command = $row.command
                wait_type = $row.wait_type
                wait_time = $row.wait_time
                blocking_session_id = $row.blocking_session_id
                cpu_time = $row.cpu_time
                total_elapsed_time = $row.total_elapsed_time
                logical_reads = $row.logical_reads
                reads = $row.reads
                writes = $row.writes
                open_transaction_count = $row.open_transaction_count
                database_name = $row.database_name
                sql_text = $row.sql_text
            }
        }

        if (@($rows).Count -gt 0) {
            $append = Test-Path -LiteralPath $OutputPath
            $rows | Export-Csv -LiteralPath $OutputPath -NoTypeInformation -Append:$append -Encoding UTF8
            $capturedRowCount += @($rows).Count

            $summary = $rows | ForEach-Object {
                "sid=$($_.session_id) wait=$($_.wait_type) elapsed=$($_.total_elapsed_time)ms cpu=$($_.cpu_time)ms blocker=$($_.blocking_session_id)"
            }
            Write-Host ("[{0}/{1}] {2}" -f $sample, $sampleCount, ($summary -join "; "))
        }
        else {
            Write-Host ("[{0}/{1}] no active Mikro API SQL request" -f $sample, $sampleCount)
        }

        if ($sample -lt $sampleCount) {
            Start-Sleep -Milliseconds $IntervalMilliseconds
        }
    }
}
finally {
    $command.Dispose()
    $connection.Dispose()
}

Write-Host ""
Write-Host "Captured rows: $capturedRowCount"
if ($capturedRowCount -gt 0) {
    Write-Host "Result file : $OutputPath"
}
else {
    Write-Host "No active Mikro API SQL request was captured."
}

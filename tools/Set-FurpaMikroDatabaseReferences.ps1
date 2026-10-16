[CmdletBinding()]
param(
    [string]$ConfigurationPath = "src/FurpaMerkezApi.WebApi/appsettings.Production.json",
    [string]$SourceDatabase = "MikroDB_V16_FURPA_2024",
    [string]$TargetDatabase = "MikroDesktop_FURPA_2024",
    [string[]]$ProcedureNames = @(),
    [switch]$Apply,
    [switch]$RollbackAfterValidation,
    [switch]$PassThru
)

$ErrorActionPreference = "Stop"

function Get-ConnectionStringPart {
    param(
        [Parameter(Mandatory)]
        [string]$ConnectionString,
        [Parameter(Mandatory)]
        [string]$Pattern
    )

    $match = [regex]::Match($ConnectionString, $Pattern)
    if (-not $match.Success) {
        throw "Connection string field could not be resolved for pattern '$Pattern'."
    }

    return $match.Groups[1].Value.Trim()
}

if ($SourceDatabase -notmatch '^[A-Za-z0-9_]+$' -or
    $TargetDatabase -notmatch '^[A-Za-z0-9_]+$') {
    throw "Database names may contain only letters, numbers, and underscores."
}

if ($SourceDatabase.Equals($TargetDatabase, [StringComparison]::OrdinalIgnoreCase)) {
    throw "SourceDatabase and TargetDatabase must be different."
}

$normalizedProcedureNames = @($ProcedureNames |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Sort-Object -Unique)
if ($normalizedProcedureNames.Where({ $_ -notmatch '^[A-Za-z0-9_]+$' }).Count -gt 0) {
    throw "Procedure names may contain only letters, numbers, and underscores."
}

$resolvedConfigurationPath = (Resolve-Path -LiteralPath $ConfigurationPath).Path
$configuration = Get-Content -LiteralPath $resolvedConfigurationPath -Raw | ConvertFrom-Json
$connectionString = [string]$configuration.ConnectionStrings.FurpaConnection
if ([string]::IsNullOrWhiteSpace($connectionString)) {
    throw "ConnectionStrings:FurpaConnection is missing from '$resolvedConfigurationPath'."
}

$server = Get-ConnectionStringPart $connectionString '(?i)(?:Server|Data Source)=([^;]+)'
$furpaDatabase = Get-ConnectionStringPart $connectionString '(?i)(?:Database|Initial Catalog)=([^;]+)'
$username = Get-ConnectionStringPart $connectionString '(?i)(?:User ID|UID)=([^;]+)'
$password = Get-ConnectionStringPart $connectionString '(?i)(?:Password|PWD)=([^;]+)'
$expectedProcedureCount = $normalizedProcedureNames.Count

if ($expectedProcedureCount -gt 0) {
    $escapedProcedures = $normalizedProcedureNames |
        ForEach-Object { "N'$($_.Replace("'", "''"))'" }
    $procedurePredicate = "AND o.name IN ($($escapedProcedures -join ','))"
}
else {
    $procedurePredicate = @"
AND
(
    m.definition LIKE N'%' + @sourceDatabase + N'%'
    OR m.definition LIKE N'%' + @targetDatabase + N'%'
)
"@
}

$applyValue = if ($Apply) { 1 } else { 0 }
$rollbackValue = if ($RollbackAfterValidation) { 1 } else { 0 }
$sourceProgramIdColumn = "cha_disyazilimid"
$targetProgramIdColumn = "cha_ExternalProgramId"
$sourceProgramTypeColumn = "cha_disyazilim_tip"
$targetProgramTypeColumn = "cha_ExternalProgramType"
if ($TargetDatabase.Equals("MikroDB_V16_FURPA_2024", [StringComparison]::OrdinalIgnoreCase)) {
    $sourceProgramIdColumn = "cha_ExternalProgramId"
    $targetProgramIdColumn = "cha_disyazilimid"
    $sourceProgramTypeColumn = "cha_ExternalProgramType"
    $targetProgramTypeColumn = "cha_disyazilim_tip"
}

$query = @"
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @sourceDatabase sysname = N'$SourceDatabase';
DECLARE @targetDatabase sysname = N'$TargetDatabase';
DECLARE @apply bit = $applyValue;
DECLARE @rollbackAfterValidation bit = $rollbackValue;
DECLARE @expectedProcedureCount int = $expectedProcedureCount;

IF DB_ID(@targetDatabase) IS NULL
    THROW 51000, 'Target Mikro database does not exist.', 1;

IF OBJECT_ID(QUOTENAME(@targetDatabase) + N'.dbo.STOK_HAREKETLERI') IS NULL
   OR OBJECT_ID(QUOTENAME(@targetDatabase) + N'.dbo.CARI_HESAP_HAREKETLERI') IS NULL
   OR OBJECT_ID(QUOTENAME(@targetDatabase) + N'.dbo.SIPARISLER') IS NULL
    THROW 51001, 'Target Mikro database is missing required business tables.', 1;

DECLARE @targets TABLE
(
    ObjectId int NOT NULL PRIMARY KEY,
    SchemaName sysname NOT NULL,
    ProcedureName sysname NOT NULL,
    Definition nvarchar(max) NOT NULL,
    SourceReferenceCount int NOT NULL,
    TargetReferenceCount int NOT NULL
);

INSERT INTO @targets
(
    ObjectId,
    SchemaName,
    ProcedureName,
    Definition,
    SourceReferenceCount,
    TargetReferenceCount
)
SELECT
    o.object_id,
    SCHEMA_NAME(o.schema_id),
    o.name,
    m.definition,
    (LEN(m.definition) - LEN(REPLACE(m.definition, @sourceDatabase, N''))) / LEN(@sourceDatabase),
    (LEN(m.definition) - LEN(REPLACE(m.definition, @targetDatabase, N''))) / LEN(@targetDatabase)
FROM sys.objects o
JOIN sys.sql_modules m ON m.object_id = o.object_id
WHERE o.type = 'P'
  AND SCHEMA_NAME(o.schema_id) = N'dbo'
  $procedurePredicate;

IF @expectedProcedureCount > 0
   AND (SELECT COUNT(*) FROM @targets) <> @expectedProcedureCount
    THROW 51002, 'One or more requested Furpa procedures could not be found.', 1;

DECLARE @missingDependency nvarchar(776);
SELECT TOP (1)
    @missingDependency = QUOTENAME(COALESCE(d.referenced_schema_name, N'dbo'))
        + N'.' + QUOTENAME(d.referenced_entity_name)
FROM sys.sql_expression_dependencies d
JOIN @targets t ON t.ObjectId = d.referencing_id
WHERE d.referenced_database_name IN (@sourceDatabase, @targetDatabase)
  AND d.referenced_entity_name IS NOT NULL
  AND OBJECT_ID(
      QUOTENAME(@targetDatabase)
      + N'.' + QUOTENAME(COALESCE(d.referenced_schema_name, N'dbo'))
      + N'.' + QUOTENAME(d.referenced_entity_name)) IS NULL;

IF @missingDependency IS NOT NULL
BEGIN
    DECLARE @missingMessage nvarchar(2048) =
        N'Target Mikro database is missing referenced object ' + @missingDependency + N'.';
    THROW 51004, @missingMessage, 1;
END;

SELECT
    SchemaName,
    ProcedureName,
    SourceReferenceCount,
    TargetReferenceCount,
    CASE WHEN @apply = 1 THEN N'Apply' ELSE N'DryRun' END AS Mode
FROM @targets
ORDER BY SchemaName, ProcedureName;

DECLARE @matchedObjectCount int = (SELECT COUNT(*) FROM @targets);
DECLARE @initialSourceReferenceCount int =
    COALESCE((SELECT SUM(SourceReferenceCount) FROM @targets), 0);

IF @apply = 0
BEGIN
    PRINT CONCAT(
        N'CUTOVER_SUMMARY|Matched=', @matchedObjectCount,
        N'|SourceRefs=', @initialSourceReferenceCount,
        N'|Updated=0|Remaining=', @initialSourceReferenceCount,
        N'|Result=DryRun');
    RETURN;
END;

BEGIN TRANSACTION;

DECLARE @schemaName sysname;
DECLARE @procedureName sysname;
DECLARE @definition nvarchar(max);
DECLARE @updatedObjectCount int = 0;
DECLARE procedure_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT SchemaName, ProcedureName, Definition
    FROM @targets
    WHERE SourceReferenceCount > 0;

OPEN procedure_cursor;
FETCH NEXT FROM procedure_cursor INTO @schemaName, @procedureName, @definition;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @definition = REPLACE(@definition, @sourceDatabase, @targetDatabase);
    SET @definition = REPLACE(@definition, N'$sourceProgramIdColumn', N'$targetProgramIdColumn');
    SET @definition = REPLACE(@definition, N'$sourceProgramTypeColumn', N'$targetProgramTypeColumn');

    DECLARE @upperDefinition nvarchar(max) = UPPER(@definition);
    DECLARE @createPosition int = 0;
    IF CHARINDEX(N'CREATE OR ALTER PROCEDURE', @upperDefinition) = 0
       AND CHARINDEX(N'CREATE OR ALTER PROC', @upperDefinition) = 0
    BEGIN
        SET @createPosition = CHARINDEX(N'CREATE PROCEDURE', @upperDefinition);
        IF @createPosition = 0
            SET @createPosition = CHARINDEX(N'CREATE PROC', @upperDefinition);
    END;

    IF @createPosition > 0
        SET @definition = STUFF(@definition, @createPosition, 6, N'ALTER');

    EXEC sys.sp_executesql @definition;
    SET @updatedObjectCount += 1;
    FETCH NEXT FROM procedure_cursor INTO @schemaName, @procedureName, @definition;
END;

CLOSE procedure_cursor;
DEALLOCATE procedure_cursor;

DECLARE refresh_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT SchemaName, ProcedureName
    FROM @targets
    WHERE SourceReferenceCount > 0;

OPEN refresh_cursor;
FETCH NEXT FROM refresh_cursor INTO @schemaName, @procedureName;

WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @qualifiedProcedure nvarchar(776) =
        QUOTENAME(@schemaName) + N'.' + QUOTENAME(@procedureName);
    EXEC sys.sp_refreshsqlmodule @name = @qualifiedProcedure;
    FETCH NEXT FROM refresh_cursor INTO @schemaName, @procedureName;
END;

CLOSE refresh_cursor;
DEALLOCATE refresh_cursor;

DECLARE @remainingSourceReferenceCount int =
(
    SELECT COALESCE(SUM(
        (LEN(m.definition) - LEN(REPLACE(m.definition, @sourceDatabase, N'')))
        / LEN(@sourceDatabase)), 0)
    FROM sys.sql_modules m
    JOIN @targets t ON t.ObjectId = m.object_id
);

IF @remainingSourceReferenceCount > 0
BEGIN
    ROLLBACK TRANSACTION;
    THROW 51003, 'Old Mikro database references remain after the update.', 1;
END;

IF @rollbackAfterValidation = 1
BEGIN
    ROLLBACK TRANSACTION;
    PRINT CONCAT(
        N'CUTOVER_SUMMARY|Matched=', @matchedObjectCount,
        N'|SourceRefs=', @initialSourceReferenceCount,
        N'|Updated=0|Remaining=', @initialSourceReferenceCount,
        N'|Result=ValidatedAndRolledBack');
    RETURN;
END;

COMMIT TRANSACTION;

SELECT
    t.SchemaName,
    t.ProcedureName AS UpdatedProcedure
FROM @targets t
WHERE t.SourceReferenceCount > 0
ORDER BY t.SchemaName, t.ProcedureName;

PRINT CONCAT(
    N'CUTOVER_SUMMARY|Matched=', @matchedObjectCount,
    N'|SourceRefs=', @initialSourceReferenceCount,
    N'|Updated=', @updatedObjectCount,
    N'|Remaining=0|Result=Applied');
"@

$previousSqlCmdPassword = $env:SQLCMDPASSWORD
try {
    $env:SQLCMDPASSWORD = $password
    $sqlOutput = @(& sqlcmd `
        -S $server `
        -d $furpaDatabase `
        -U $username `
        -C `
        -b `
        -W `
        -s "|" `
        -Q $query 2>&1)
    $sqlCmdExitCode = $LASTEXITCODE

    $sqlOutput | ForEach-Object { Write-Host ([string]$_) }
    if ($sqlCmdExitCode -ne 0) {
        throw "sqlcmd failed with exit code $sqlCmdExitCode."
    }

    $summaryMatch = $null
    foreach ($line in $sqlOutput) {
        $candidate = [regex]::Match(
            [string]$line,
            'CUTOVER_SUMMARY\|Matched=(\d+)\|SourceRefs=(\d+)\|Updated=(\d+)\|Remaining=(\d+)\|Result=([A-Za-z]+)')
        if ($candidate.Success) {
            $summaryMatch = $candidate
        }
    }

    if ($null -eq $summaryMatch) {
        throw "The SQL reference operation completed without a parseable summary."
    }

    $result = [pscustomobject]@{
        SourceDatabase = $SourceDatabase
        TargetDatabase = $TargetDatabase
        MatchedObjectCount = [int]$summaryMatch.Groups[1].Value
        SourceReferenceCount = [int]$summaryMatch.Groups[2].Value
        UpdatedObjectCount = [int]$summaryMatch.Groups[3].Value
        RemainingSourceReferenceCount = [int]$summaryMatch.Groups[4].Value
        Result = $summaryMatch.Groups[5].Value
    }

    if ($PassThru) {
        return $result
    }
}
finally {
    if ($null -eq $previousSqlCmdPassword) {
        Remove-Item Env:SQLCMDPASSWORD -ErrorAction SilentlyContinue
    }
    else {
        $env:SQLCMDPASSWORD = $previousSqlCmdPassword
    }
}

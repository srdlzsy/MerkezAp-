using System.Data;
using System.Text.Json;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;

const string target = "20261002111146_HardenOfflineCreateRecovery";
const string previous = "20261001105720_AddUserClientRoleMappings";
if (args.Length != 4 || args[0] is not ("inspect" or "backup" or "apply"))
    throw new ArgumentException("Usage: inspect|backup|apply <webapi-directory> <expected-server> <expected-database>");
var config = new ConfigurationBuilder().SetBasePath(Path.GetFullPath(args[1]))
    .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Production.json")
    .AddEnvironmentVariables().Build();
var builder = new SqlConnectionStringBuilder(config.GetConnectionString("AuthConnection"));
if (builder.DataSource != args[2] || builder.InitialCatalog != args[3])
    throw new InvalidOperationException("Configured target does not match explicit expected server/database.");
builder.ConnectTimeout = 15;
await using var connection = new SqlConnection(builder.ConnectionString);
await connection.OpenAsync();
var options = new DbContextOptionsBuilder<AuthDbContext>().UseSqlServer(connection).Options;
await using var db = new AuthDbContext(options);
Console.WriteLine(JsonSerializer.Serialize(new { server = builder.DataSource, database = builder.InitialCatalog, utc = DateTime.UtcNow, mode = args[0] }));
await PrintAsync("identity", "SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')) server_name, DB_NAME() database_name, DATABASEPROPERTYEX(DB_NAME(), 'Status') database_status;");
await PrintAsync("history", "SELECT MigrationId, ProductVersion FROM __EFMigrationsHistory ORDER BY MigrationId;");
await PrintAsync("columns", "SELECT name, TYPE_NAME(user_type_id) type, max_length, is_nullable FROM sys.columns WHERE object_id=OBJECT_ID('dbo.mobile_offline_sync_requests') AND name IN ('error_code','retryable','revision');");
await PrintAsync("requests", "SELECT status, COUNT_BIG(*) count FROM dbo.mobile_offline_sync_requests GROUP BY status;");
await PrintAsync("backfill_candidates", "SELECT COUNT_BIG(*) count FROM dbo.mobile_offline_sync_requests WHERE status='Failed' AND error_message LIKE 'The existing Mikro document does not match the requested document content.%';");
await PrintAsync("backup", "SELECT TOP (3) type, backup_start_date, backup_finish_date, is_copy_only, has_backup_checksums FROM msdb.dbo.backupset WHERE database_name=DB_NAME() ORDER BY backup_finish_date DESC;");
await PrintAsync("storage", "SELECT SUM(CONVERT(bigint,size))*8/1024 size_mb, CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath')) backup_directory FROM sys.database_files;");
if (args[0] == "inspect") return;
if (args[0] == "backup")
{
    await using var backup = connection.CreateCommand();
    backup.CommandTimeout = 300;
    backup.CommandText = "SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath'));";
    var directory = await backup.ExecuteScalarAsync() as string;
    if (string.IsNullOrWhiteSpace(directory))
    {
        backup.CommandText = "DECLARE @directory nvarchar(4000); EXEC master.dbo.xp_instance_regread N'HKEY_LOCAL_MACHINE', N'Software\\Microsoft\\MSSQLServer\\MSSQLServer', N'BackupDirectory', @directory OUTPUT; SELECT @directory;";
        directory = await backup.ExecuteScalarAsync() as string;
    }
    if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("SQL Server default backup directory is unavailable.");
    var path = Path.Combine(directory, builder.InitialCatalog + "_before_" + target + "_" + Guid.NewGuid().ToString("N") + ".bak");
    backup.CommandText = "BACKUP DATABASE " + new SqlCommandBuilder().QuoteIdentifier(builder.InitialCatalog) + " TO DISK=@path WITH COPY_ONLY, CHECKSUM, COMPRESSION;";
    backup.Parameters.AddWithValue("@path", path);
    await backup.ExecuteNonQueryAsync();
    backup.CommandText = "RESTORE VERIFYONLY FROM DISK=@path WITH CHECKSUM;";
    await backup.ExecuteNonQueryAsync();
    Console.WriteLine(JsonSerializer.Serialize(new { verifiedBackup = path, utc = DateTime.UtcNow }));
    return;
}

// Apply only the reviewed migration, inside one bounded transaction with an exclusive table lock.
await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable);
await db.Database.UseTransactionAsync(transaction);
await db.Database.ExecuteSqlRawAsync("SET LOCK_TIMEOUT 15000; SET XACT_ABORT ON;");
await db.Database.ExecuteSqlRawAsync("SELECT TOP (1) id FROM dbo.mobile_offline_sync_requests WITH (TABLOCKX, HOLDLOCK);");
await db.Database.ExecuteSqlRawAsync("SELECT status, COUNT_BIG(*) count INTO #before_counts FROM dbo.mobile_offline_sync_requests GROUP BY status;");
var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
var known = db.Database.GetMigrations().ToArray();
if (applied.Contains(target))
{
    Console.WriteLine("Already applied; no changes made.");
    await transaction.RollbackAsync();
    return;
}
if (applied.LastOrDefault() != previous || !applied.SequenceEqual(known.TakeWhile(x => x != target)))
    throw new InvalidOperationException("Unexpected migration history; refusing to apply unrelated migrations.");
await using (var check = connection.CreateCommand())
{
    check.Transaction = (SqlTransaction)transaction;
    check.CommandText = "SELECT COUNT(*) FROM msdb.dbo.backupset WHERE database_name=DB_NAME() AND type='D' AND backup_finish_date >= DATEADD(hour,-24,GETDATE());";
    if (Convert.ToInt32(await check.ExecuteScalarAsync()) == 0)
        throw new InvalidOperationException("No full backup in the last 24 hours recorded in msdb; application refused.");
    check.CommandText = "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.mobile_offline_sync_requests') AND name IN ('error_code','retryable','revision');";
    if (Convert.ToInt32(await check.ExecuteScalarAsync()) != 0)
        throw new InvalidOperationException("Partial schema already exists; inspect manually.");
}
var assembly = db.GetService<IMigrationsAssembly>();
var migration = assembly.CreateMigration(assembly.Migrations[target], db.Database.ProviderName!);
foreach (var sql in db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations))
{
    if (sql.TransactionSuppressed) throw new InvalidOperationException("Nontransactional migration is not supported.");
    await db.Database.ExecuteSqlRawAsync(sql.CommandText);
}
var history = db.GetService<IHistoryRepository>().GetInsertScript(new HistoryRow(target, ProductInfo.GetVersion()));
await db.Database.ExecuteSqlRawAsync(history);
await db.Database.ExecuteSqlRawAsync("""
    IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.mobile_offline_sync_requests') AND
        ((name='error_code' AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=200 AND is_nullable=1) OR
         (name='retryable' AND TYPE_NAME(user_type_id)='bit' AND is_nullable=1) OR
         (name='revision' AND TYPE_NAME(user_type_id)='uniqueidentifier' AND is_nullable=0))) <> 3
        THROW 51000, 'Migration schema verification failed', 1;
    IF EXISTS (SELECT status, count FROM #before_counts EXCEPT SELECT status, COUNT_BIG(*) FROM dbo.mobile_offline_sync_requests GROUP BY status)
        OR EXISTS (SELECT status, COUNT_BIG(*) FROM dbo.mobile_offline_sync_requests GROUP BY status EXCEPT SELECT status, count FROM #before_counts)
        THROW 51001, 'Request status counts changed unexpectedly', 1;
    IF EXISTS (SELECT 1 FROM dbo.mobile_offline_sync_requests WHERE status='Failed'
        AND error_message LIKE 'The existing Mikro document does not match the requested document content.%'
        AND (ISNULL(error_code,'') <> 'MIKRO_DOCUMENT_CONTENT_MISMATCH' OR ISNULL(CONVERT(int,retryable),1) <> 0))
        THROW 51002, 'Backfill verification failed', 1;
    """);
await PrintAsync("verified_columns", "SELECT name, TYPE_NAME(user_type_id) type, max_length, is_nullable FROM sys.columns WHERE object_id=OBJECT_ID('dbo.mobile_offline_sync_requests') AND name IN ('error_code','retryable','revision');", (SqlTransaction)transaction);
await PrintAsync("verified_backfill", "SELECT COUNT_BIG(*) candidates, SUM(CASE WHEN error_code='MIKRO_DOCUMENT_CONTENT_MISMATCH' AND retryable=0 THEN 1 ELSE 0 END) classified FROM dbo.mobile_offline_sync_requests WHERE status='Failed' AND error_message LIKE 'The existing Mikro document does not match the requested document content.%';", (SqlTransaction)transaction);
await transaction.CommitAsync();
Console.WriteLine(JsonSerializer.Serialize(new { committed = target, utc = DateTime.UtcNow }));

async Task PrintAsync(string label, string sql, SqlTransaction? transaction = null)
{
    await using var command = connection.CreateCommand();
    command.CommandTimeout = 30;
    command.Transaction = transaction;
    command.CommandText = sql;
    await using var reader = await command.ExecuteReaderAsync();
    var rows = new List<Dictionary<string, object?>>();
    while (await reader.ReadAsync())
        rows.Add(Enumerable.Range(0, reader.FieldCount).ToDictionary(reader.GetName, i => reader.IsDBNull(i) ? null : reader.GetValue(i)));
    Console.WriteLine(JsonSerializer.Serialize(new { label, rows }));
}

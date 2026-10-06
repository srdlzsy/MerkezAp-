using FurpaMerkezApi.Infrastructure.Migrations;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.OfflineSync;

public sealed class OfflineSyncSqlServerTests
{
    [SqlServerFact]
    public async Task MigrationAndConcurrentRetry_UseSqlServerConcurrencyCheck()
    {
        // Opt-in and disposable: the configured server is used only to host a unique temporary database.
        var database = "FurpaOfflineTest_" + Guid.NewGuid().ToString("N");
        var masterConnection = ResolveMasterConnection();
        await using var master = new SqlConnection(masterConnection);
        await master.OpenAsync();
        await using var command = master.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{database}]";
        await command.ExecuteNonQueryAsync();
        try
        {
            var connection = new SqlConnectionStringBuilder(masterConnection) { InitialCatalog = database }.ConnectionString;
            var options = new DbContextOptionsBuilder<AuthDbContext>().UseSqlServer(connection).Options;
            await using (var db = new AuthDbContext(options))
            {
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE mobile_offline_sync_requests (
                        id uniqueidentifier NOT NULL PRIMARY KEY,
                        operation_code nvarchar(100) NOT NULL,
                        requested_by_user_id uniqueidentifier NOT NULL,
                        warehouse_no int NOT NULL,
                        client_request_id nvarchar(50) NOT NULL,
                        request_fingerprint nvarchar(64) NOT NULL,
                        request_payload nvarchar(max) NULL,
                        status nvarchar(20) NOT NULL,
                        response_payload nvarchar(max) NULL,
                        error_message nvarchar(max) NULL,
                        created_at_utc datetime2 NOT NULL,
                        updated_at_utc datetime2 NULL,
                        completed_at_utc datetime2 NULL,
                        CONSTRAINT ux_test_request UNIQUE(operation_code, requested_by_user_id, client_request_id)
                    );
                    INSERT INTO mobile_offline_sync_requests
                        (id, operation_code, requested_by_user_id, warehouse_no, client_request_id,
                         request_fingerprint, status, error_message, created_at_utc)
                    VALUES (NEWID(), 'old.create', NEWID(), 56, CONVERT(nvarchar(50), NEWID()), 'old-hash', 'Failed',
                        'The existing Mikro document does not match the requested document content. Manual review is required;', SYSUTCDATETIME());
                    """);
                var migration = new HardenOfflineCreateRecovery();
                foreach (var sql in db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations))
                    await db.Database.ExecuteSqlRawAsync(sql.CommandText);
                var old = await db.MobileOfflineSyncRequests.SingleAsync();
                Assert.False(old.Retryable);
                Assert.Equal("MIKRO_DOCUMENT_CONTENT_MISMATCH", old.ErrorCode);
            }
            await MobileOfflineSyncServiceTests.VerifyConcurrentRetryAsync(options);
        }
        finally
        {
            command.CommandText = $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]";
            await command.ExecuteNonQueryAsync();
        }
    }

    private static string ResolveMasterConnection()
    {
        var configuredConnection = Environment.GetEnvironmentVariable("FURPA_SQLSERVER_TEST_CONNECTION");

        if (!string.IsNullOrWhiteSpace(configuredConnection))
        {
            return new SqlConnectionStringBuilder(configuredConnection)
            {
                InitialCatalog = "master",
                Pooling = false
            }.ConnectionString;
        }

        return @"Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true;Pooling=false";
    }
}

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        var hasServerConnection = !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("FURPA_SQLSERVER_TEST_CONNECTION"));
        var useLocalDb = OperatingSystem.IsWindows() &&
            Environment.GetEnvironmentVariable("FURPA_RUN_LOCALDB_TESTS") == "true";

        if (!hasServerConnection && !useLocalDb)
        {
            Skip = "Set FURPA_SQLSERVER_TEST_CONNECTION for a disposable SQL Server test database, or enable FURPA_RUN_LOCALDB_TESTS on Windows.";
        }
    }
}

using FurpaMerkezApi.Infrastructure.Modules.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.Common;

public sealed class MikroStockMovementBulkWriterTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(50)]
    public void BuildDataTable_MapsWholeDocumentToOneBatch(int lineCount)
    {
        using var dbContext = CreateDbContext();
        var movements = Enumerable.Range(0, lineCount)
            .Select(CreateMovement)
            .ToArray();

        using var table = MikroStockMovementBulkWriter.BuildDataTable(dbContext, movements);

        Assert.Equal(lineCount, table.Rows.Count);
        Assert.Equal(
            typeof(STOK_HAREKETLERI).GetProperties().Length,
            table.Columns.Count);
        Assert.Equal(0, MikroStockMovementBulkWriter.SingleBatchSize);

        for (var rowNo = 0; rowNo < lineCount; rowNo++)
        {
            Assert.Equal(movements[rowNo].sth_Guid, table.Rows[rowNo][nameof(STOK_HAREKETLERI.sth_Guid)]);
            Assert.Equal(rowNo, table.Rows[rowNo][nameof(STOK_HAREKETLERI.sth_satirno)]);
            Assert.Equal("F110", table.Rows[rowNo][nameof(STOK_HAREKETLERI.sth_evrakno_seri)]);
            Assert.Equal("FR-BULK-TEST", table.Rows[rowNo][nameof(STOK_HAREKETLERI.sth_eticaret_kanal_kodu)]);
        }
    }

    [Theory]
    [InlineData(17, 2, 6, 0, "F110")]
    [InlineData(13, 0, 0, 0, "A110")]
    [InlineData(1, 1, 0, 1, "F110")]
    public void BuildDataTable_PreservesFlowSpecificMovementFields(
        byte documentType,
        byte movementType,
        byte movementGenre,
        byte returnType,
        string documentSerie)
    {
        using var dbContext = CreateDbContext();
        var movements = Enumerable.Range(0, 3)
            .Select(rowNo => CreateMovementForFlow(
                rowNo,
                documentType,
                movementType,
                movementGenre,
                returnType,
                documentSerie))
            .ToArray();

        using var table = MikroStockMovementBulkWriter.BuildDataTable(dbContext, movements);

        Assert.Equal(3, table.Rows.Count);
        for (var rowNo = 0; rowNo < movements.Length; rowNo++)
        {
            var row = table.Rows[rowNo];
            Assert.Equal(documentType, row[nameof(STOK_HAREKETLERI.sth_evraktip)]);
            Assert.Equal(movementType, row[nameof(STOK_HAREKETLERI.sth_tip)]);
            Assert.Equal(movementGenre, row[nameof(STOK_HAREKETLERI.sth_cins)]);
            Assert.Equal(returnType, row[nameof(STOK_HAREKETLERI.sth_normal_iade)]);
            Assert.Equal(documentSerie, row[nameof(STOK_HAREKETLERI.sth_evrakno_seri)]);
            Assert.Equal(rowNo, row[nameof(STOK_HAREKETLERI.sth_satirno)]);
        }
    }

    [Fact]
    public void RequiredOptions_PreserveMikroTriggerAndConstraintsWithoutTableLock()
    {
        var options = MikroStockMovementBulkWriter.RequiredOptions;

        Assert.True(options.HasFlag(SqlBulkCopyOptions.FireTriggers));
        Assert.True(options.HasFlag(SqlBulkCopyOptions.CheckConstraints));
        Assert.False(options.HasFlag(SqlBulkCopyOptions.TableLock));
        Assert.False(options.HasFlag(SqlBulkCopyOptions.UseInternalTransaction));
    }

    [MikroBulkSqlServerTheory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(50)]
    public async Task WriteAsync_WritesOneTriggerBatchAndRollsBack(int lineCount)
    {
        var connectionString = Environment.GetEnvironmentVariable("FURPA_MIKRO_BULK_TEST_CONNECTION")!;
        var options = new DbContextOptionsBuilder<MikroWriteDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        await using var dbContext = new MikroWriteDbContext(options);
        var template = await dbContext.STOK_HAREKETLERIs
            .AsNoTracking()
            .Where(movement =>
                movement.sth_iptal == false &&
                movement.sth_evraktip == 17 &&
                movement.sth_tip == 2 &&
                movement.sth_cins == 6)
            .OrderByDescending(movement => movement.sth_create_date)
            .FirstAsync();
        var documentSerie = "BTST";
        var documentOrderNo = Random.Shared.Next(1_500_000_000, 2_000_000_000);
        var traceKey = "FR" + Guid.NewGuid().ToString("N")[..22];
        var movements = Enumerable.Range(0, lineCount)
            .Select(rowNo => CloneForTest(
                template,
                documentSerie,
                documentOrderNo,
                traceKey,
                rowNo))
            .ToArray();

        await dbContext.Database.OpenConnectionAsync();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        var probeSuffix = Guid.NewGuid().ToString("N");
        var probeTableName = $"__FurpaBulkProbe_{probeSuffix}";
        var probeTriggerName = $"__FurpaBulkTrigger_{probeSuffix}";

        try
        {
            await CreateTriggerProbeAsync(dbContext, probeTableName, probeTriggerName);
            await MikroStockMovementBulkWriter.WriteAsync(
                dbContext,
                movements,
                timeoutSeconds: 300,
                CancellationToken.None);

            var insertedLineCount = await dbContext.STOK_HAREKETLERIs
                .AsNoTracking()
                .CountAsync(movement =>
                    movement.sth_evrakno_seri == documentSerie &&
                    movement.sth_evrakno_sira == documentOrderNo &&
                    movement.sth_eticaret_kanal_kodu == traceKey);
            var probe = await ReadTriggerProbeAsync(dbContext, probeTableName);

            Assert.Equal(lineCount, insertedLineCount);
            Assert.Equal(1, probe.ExecutionCount);
            Assert.Equal(lineCount, probe.InsertedLineCount);
        }
        finally
        {
            await transaction.RollbackAsync();
        }

        var persistedLineCount = await dbContext.STOK_HAREKETLERIs
            .AsNoTracking()
            .CountAsync(movement =>
                movement.sth_evrakno_seri == documentSerie &&
                movement.sth_evrakno_sira == documentOrderNo &&
                movement.sth_eticaret_kanal_kodu == traceKey);

        Assert.Equal(0, persistedLineCount);
    }

    private static MikroWriteDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MikroWriteDbContext>()
            .UseSqlServer("Server=(local);Database=MikroBulkWriterModelTest;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new MikroWriteDbContext(options);
    }

    private static STOK_HAREKETLERI CreateMovement(int rowNo) =>
        new()
        {
            sth_Guid = Guid.NewGuid(),
            sth_DBCno = 0,
            sth_create_date = new DateTime(2026, 10, 12, 10, 30, 0),
            sth_evraktip = 17,
            sth_tip = 2,
            sth_cins = 6,
            sth_evrakno_seri = "F110",
            sth_evrakno_sira = 1001,
            sth_satirno = rowNo,
            sth_stok_kod = $"STK-{rowNo:000}",
            sth_miktar = rowNo + 1,
            sth_birim_pntr = 1,
            sth_cikis_depo_no = 110,
            sth_giris_depo_no = 1,
            sth_nakliyedeposu = 116,
            sth_eticaret_kanal_kodu = "FR-BULK-TEST"
        };

    private static STOK_HAREKETLERI CreateMovementForFlow(
        int rowNo,
        byte documentType,
        byte movementType,
        byte movementGenre,
        byte returnType,
        string documentSerie)
    {
        var movement = CreateMovement(rowNo);
        movement.sth_evraktip = documentType;
        movement.sth_tip = movementType;
        movement.sth_cins = movementGenre;
        movement.sth_normal_iade = returnType;
        movement.sth_evrakno_seri = documentSerie;
        return movement;
    }

    private static STOK_HAREKETLERI CloneForTest(
        STOK_HAREKETLERI template,
        string documentSerie,
        int documentOrderNo,
        string traceKey,
        int rowNo)
    {
        var movement = new STOK_HAREKETLERI();
        foreach (var property in typeof(STOK_HAREKETLERI).GetProperties()
                     .Where(property => property.CanRead && property.CanWrite))
        {
            property.SetValue(movement, property.GetValue(template));
        }

        movement.sth_Guid = Guid.NewGuid();
        movement.sth_create_date = DateTime.Now;
        movement.sth_lastup_date = DateTime.Now;
        movement.sth_evrakno_seri = documentSerie;
        movement.sth_evrakno_sira = documentOrderNo;
        movement.sth_satirno = rowNo;
        movement.sth_eticaret_kanal_kodu = traceKey;
        return movement;
    }

    private static async Task CreateTriggerProbeAsync(
        MikroWriteDbContext dbContext,
        string tableName,
        string triggerName)
    {
        var connection = (SqlConnection)dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = (SqlTransaction?)dbContext.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            CREATE TABLE [dbo].[{tableName}]
            (
                [InsertedLineCount] int NOT NULL
            );

            EXEC(N'
                CREATE TRIGGER [dbo].[{triggerName}]
                ON [dbo].[STOK_HAREKETLERI]
                AFTER INSERT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    INSERT INTO [dbo].[{tableName}] ([InsertedLineCount])
                    SELECT COUNT(*) FROM inserted;
                END');
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<(int ExecutionCount, int InsertedLineCount)> ReadTriggerProbeAsync(
        MikroWriteDbContext dbContext,
        string tableName)
    {
        var connection = (SqlConnection)dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = (SqlTransaction?)dbContext.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            SELECT COUNT(*) AS ExecutionCount,
                   COALESCE(SUM([InsertedLineCount]), 0) AS InsertedLineCount
            FROM [dbo].[{tableName}];
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetInt32(0), reader.GetInt32(1));
    }
}

public sealed class MikroBulkSqlServerTheoryAttribute : TheoryAttribute
{
    public MikroBulkSqlServerTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("FURPA_MIKRO_BULK_TEST_CONNECTION")))
        {
            Skip = "Set FURPA_MIKRO_BULK_TEST_CONNECTION to a disposable Mikro test database.";
        }
    }
}

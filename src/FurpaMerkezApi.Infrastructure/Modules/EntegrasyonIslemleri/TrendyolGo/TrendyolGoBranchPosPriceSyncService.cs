using System.Text.Json;
using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.TrendyolGo;

internal sealed class TrendyolGoBranchPosPriceSyncService(
    AuthDbContext authDbContext,
    FurpaDbContext furpaDbContext,
    IClock clock,
    IOptionsMonitor<TrendyolGoOptions> options,
    ILogger<TrendyolGoBranchPosPriceSyncService> logger)
{
    public async Task EnqueueAsync(long storeId, int warehouseNo, IReadOnlyCollection<BranchPosPriceItem> items, CancellationToken cancellationToken)
    {
        if (!options.CurrentValue.BranchPosPriceSync.Enabled || items.Count == 0)
        {
            return;
        }

        try
        {
            using var persistenceTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            authDbContext.TrendyolGoBranchPosPriceSyncTasks.Add(new TrendyolGoBranchPosPriceSyncTask(
                Guid.NewGuid(), storeId, warehouseNo, JsonSerializer.Serialize(items), clock.UtcNow));
            await authDbContext.SaveChangesAsync(persistenceTimeout.Token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Branch POS price synchronization could not be queued. StoreId={StoreId}, WarehouseNo={WarehouseNo}", storeId, warehouseNo);
        }
    }

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken)
    {
        var config = options.CurrentValue.BranchPosPriceSync;
        if (!config.Enabled)
        {
            return 0;
        }

        var now = clock.UtcNow;
        var candidate = await authDbContext.TrendyolGoBranchPosPriceSyncTasks
            .AsNoTracking()
            .Where(item =>
                (item.Status == TrendyolGoBranchPosPriceSyncStatus.Pending ||
                 item.Status == TrendyolGoBranchPosPriceSyncStatus.Processing) &&
                item.NextAttemptAtUtc <= now)
            .OrderBy(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (candidate is null)
        {
            return 0;
        }

        var leaseExpiresAtUtc = now.AddSeconds(Math.Clamp(config.CommandTimeoutSeconds, 30, 120) + 60);
        var claimed = await authDbContext.TrendyolGoBranchPosPriceSyncTasks
            .Where(item => item.Id == candidate.Id && item.Status == candidate.Status && item.NextAttemptAtUtc <= now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, TrendyolGoBranchPosPriceSyncStatus.Processing)
                .SetProperty(item => item.NextAttemptAtUtc, leaseExpiresAtUtc), cancellationToken);
        if (claimed == 0)
        {
            return 0;
        }

        var task = await authDbContext.TrendyolGoBranchPosPriceSyncTasks
            .SingleAsync(item => item.Id == candidate.Id, cancellationToken);

        try
        {
            var host = await ResolveHostAsync(config, task.WarehouseNo, cancellationToken);
            if (string.IsNullOrWhiteSpace(host))
            {
                throw new InvalidOperationException($"Warehouse {task.WarehouseNo} has no configured PostgreSQL host.");
            }

            var items = JsonSerializer.Deserialize<BranchPosPriceItem[]>(task.PayloadJson)
                ?? throw new InvalidOperationException("Branch POS price task payload is invalid.");
            if (items.Length == 0)
            {
                throw new InvalidOperationException("Branch POS price task has no items.");
            }

            var connectionString = new NpgsqlConnectionStringBuilder
            {
                Host = host,
                Port = Math.Clamp(config.Port, 1, 65535),
                Database = config.Database.Trim(),
                Username = config.Username.Trim(),
                Password = config.Password,
                SslMode = SslMode.Disable,
                Timeout = Math.Clamp(config.ConnectionTimeoutSeconds, 1, 60),
                CommandTimeout = Math.Clamp(config.CommandTimeoutSeconds, 1, 120),
                Pooling = true
            }.ConnectionString;

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            foreach (var item in items
                         .GroupBy(item => new { item.StockCode, item.PriceListNo, item.UnitPointer })
                         .Select(group => group.Last())
                         .OrderBy(item => item.StockCode, StringComparer.Ordinal)
                         .ThenBy(item => item.PriceListNo)
                         .ThenBy(item => item.UnitPointer))
            {
                await UpsertAsync(
                    connection,
                    transaction,
                    task.WarehouseNo,
                    item,
                    task.CreatedAtUtc,
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            task.MarkSucceeded(clock.UtcNow);
            await authDbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Branch POS price synchronization completed. WarehouseNo={WarehouseNo}, TaskId={TaskId}, ItemCount={ItemCount}", task.WarehouseNo, task.Id, items.Length);
            return items.Length;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var retryDelay = Math.Clamp(config.RetryDelaySeconds, 30, 3600);
            task.ScheduleRetry(exception.Message, clock.UtcNow.AddSeconds(retryDelay));
            await authDbContext.SaveChangesAsync(cancellationToken);
            logger.LogWarning(exception, "Branch POS price synchronization failed and will retry. WarehouseNo={WarehouseNo}, TaskId={TaskId}", task.WarehouseNo, task.Id);
            return 0;
        }
    }

    private async Task<string?> ResolveHostAsync(
        TrendyolGoBranchPosPriceSyncOptions config,
        int warehouseNo,
        CancellationToken cancellationToken)
    {
        if (config.WarehouseHosts.TryGetValue(warehouseNo.ToString(), out var warehouseHost) &&
            !string.IsNullOrWhiteSpace(warehouseHost))
        {
            return warehouseHost.Trim();
        }

        if (!string.IsNullOrWhiteSpace(config.Host))
        {
            return config.Host.Trim();
        }

        return await furpaDbContext.BranchDetails
            .Where(item => item.BranchNo == warehouseNo)
            .Select(item => item.BranchIpAddress)
            .SingleOrDefaultAsync(cancellationToken);
    }

    internal const string UpsertCommandText = """
        INSERT INTO market.stoksatisfiyat (
            sdp_depo_no, sfiyat_stokkod, sfiyat_listesirano, sfiyat_birim_pntr,
            sfiyat_birim_adi, sfiyat_fiyati, sfiyat_satisdursun, sfiyat_sipdursun,
            sfiyat_malkabuldursun, sfiyat_lastup_date, sdp_lastup_date, fiyat_tip_kodu
        )
        VALUES (
            @sdp_depo_no, @sfiyat_stokkod, @sfiyat_listesirano, @sfiyat_birim_pntr,
            @sfiyat_birim_adi, @sfiyat_fiyati, @sfiyat_satisdursun, @sfiyat_sipdursun,
            @sfiyat_malkabuldursun, @sfiyat_lastup_date, @sdp_lastup_date, 1
        )
        ON CONFLICT (sdp_depo_no, sfiyat_stokkod, sfiyat_listesirano, fiyat_tip_kodu)
        DO UPDATE SET
            sfiyat_fiyati = EXCLUDED.sfiyat_fiyati,
            sfiyat_birim_pntr = EXCLUDED.sfiyat_birim_pntr,
            sfiyat_birim_adi = EXCLUDED.sfiyat_birim_adi,
            sfiyat_satisdursun = EXCLUDED.sfiyat_satisdursun,
            sfiyat_sipdursun = EXCLUDED.sfiyat_sipdursun,
            sfiyat_malkabuldursun = EXCLUDED.sfiyat_malkabuldursun,
            sfiyat_lastup_date = EXCLUDED.sfiyat_lastup_date,
            sdp_lastup_date = EXCLUDED.sdp_lastup_date
        WHERE EXCLUDED.sfiyat_lastup_date > market.stoksatisfiyat.sfiyat_lastup_date
           OR market.stoksatisfiyat.sfiyat_lastup_date IS NULL;
        """;

    private static async Task UpsertAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int warehouseNo,
        BranchPosPriceItem item,
        DateTime taskCreatedAtUtc,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = UpsertCommandText;
        command.Parameters.AddWithValue("sdp_depo_no", warehouseNo);
        command.Parameters.AddWithValue("sfiyat_stokkod", item.StockCode);
        command.Parameters.AddWithValue("sfiyat_listesirano", item.PriceListNo);
        command.Parameters.AddWithValue("sfiyat_birim_pntr", item.UnitPointer);
        command.Parameters.AddWithValue("sfiyat_birim_adi", item.UnitName);
        command.Parameters.AddWithValue("sfiyat_fiyati", item.Price);
        command.Parameters.AddWithValue("sfiyat_satisdursun", item.SalesBlocked);
        command.Parameters.AddWithValue("sfiyat_sipdursun", item.OrderBlocked);
        command.Parameters.AddWithValue("sfiyat_malkabuldursun", item.GoodsAcceptanceBlocked);
        command.Parameters.AddWithValue(
            "sfiyat_lastup_date",
            ResolveSourceVersion(item.PriceUpdatedAtUtc, taskCreatedAtUtc));
        command.Parameters.AddWithValue(
            "sdp_lastup_date",
            ResolveSourceVersion(item.WarehouseUpdatedAtUtc, taskCreatedAtUtc));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static DateTime ResolveSourceVersion(DateTime? sourceUpdatedAtUtc, DateTime taskCreatedAtUtc) =>
        NormalizeUtc(sourceUpdatedAtUtc ?? taskCreatedAtUtc);

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}

internal sealed record BranchPosPriceItem(
    string StockCode,
    decimal Price,
    int UnitPointer,
    string UnitName,
    int SalesBlocked,
    int OrderBlocked,
    int GoodsAcceptanceBlocked,
    DateTime? PriceUpdatedAtUtc,
    DateTime? WarehouseUpdatedAtUtc,
    int PriceListNo);

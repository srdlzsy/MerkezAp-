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
            var branchIp = await furpaDbContext.BranchDetails
                .Where(item => item.BranchNo == task.WarehouseNo)
                .Select(item => item.BranchIpAddress)
                .SingleOrDefaultAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(branchIp))
            {
                throw new InvalidOperationException($"Warehouse {task.WarehouseNo} has no branch IP address.");
            }

            var items = JsonSerializer.Deserialize<BranchPosPriceItem[]>(task.PayloadJson)
                ?? throw new InvalidOperationException("Branch POS price task payload is invalid.");
            if (items.Length == 0)
            {
                throw new InvalidOperationException("Branch POS price task has no items.");
            }

            var connectionString = new NpgsqlConnectionStringBuilder
            {
                Host = branchIp.Trim(),
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
            foreach (var item in items)
            {
                await UpsertAsync(connection, transaction, task.WarehouseNo, item, cancellationToken);
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

    private static async Task UpsertAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, int warehouseNo, BranchPosPriceItem item, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            WITH upd AS (
                UPDATE stoksatisfiyat SET
                    fiyati = @fiyati,
                    fiyat_tip_kodu = @fiyat_tip_kodu,
                    sto_birim_ad = @sto_birim_ad,
                    sdp_satisdursun = @sdp_satisdursun,
                    sdp_sipdursun = @sdp_sipdursun,
                    sdp_malkabuldursun = @sdp_malkabuldursun,
                    sfiyat_lastup_date = @sfiyat_lastup_date,
                    sdp_lastup_date = @sdp_lastup_date
                WHERE sdp_depo_no = @sdp_depo_no
                  AND sfiyat_stokkod = @sfiyat_stokkod
                  AND (sfiyat_listesirano = @sfiyat_listesirano OR (sfiyat_listesirano IS NULL AND @sfiyat_listesirano IS NULL))
                  AND (fiyat_tip_kodu = @fiyat_tip_kodu OR (fiyat_tip_kodu IS NULL AND @fiyat_tip_kodu IS NULL))
                RETURNING 1
            )
            INSERT INTO stoksatisfiyat (
                sdp_depo_no, sfiyat_stokkod, fiyati, fiyat_tip_kodu, sto_birim_ad,
                sdp_satisdursun, sdp_sipdursun, sdp_malkabuldursun, sfiyat_lastup_date,
                sdp_lastup_date, sfiyat_listesirano)
            SELECT
                @sdp_depo_no, @sfiyat_stokkod, @fiyati, @fiyat_tip_kodu, @sto_birim_ad,
                @sdp_satisdursun, @sdp_sipdursun, @sdp_malkabuldursun, @sfiyat_lastup_date,
                @sdp_lastup_date, @sfiyat_listesirano
            WHERE NOT EXISTS (SELECT 1 FROM upd);
            """;
        command.Parameters.AddWithValue("sdp_depo_no", warehouseNo);
        command.Parameters.AddWithValue("sfiyat_stokkod", item.StockCode);
        command.Parameters.AddWithValue("fiyati", item.Price);
        command.Parameters.AddWithValue("fiyat_tip_kodu", item.UnitPointer);
        command.Parameters.AddWithValue("sto_birim_ad", item.UnitName);
        command.Parameters.AddWithValue("sdp_satisdursun", item.SalesBlocked);
        command.Parameters.AddWithValue("sdp_sipdursun", item.OrderBlocked);
        command.Parameters.AddWithValue("sdp_malkabuldursun", item.GoodsAcceptanceBlocked);
        command.Parameters.AddWithValue("sfiyat_lastup_date", item.PriceUpdatedAtUtc ?? DateTime.UtcNow);
        command.Parameters.AddWithValue("sdp_lastup_date", item.WarehouseUpdatedAtUtc ?? DateTime.UtcNow);
        command.Parameters.AddWithValue("sfiyat_listesirano", item.PriceListNo);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
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

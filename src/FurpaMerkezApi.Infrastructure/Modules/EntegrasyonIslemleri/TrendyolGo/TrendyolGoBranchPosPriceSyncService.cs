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
            await EnqueueRequiredAsync(storeId, warehouseNo, items, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Branch POS price synchronization could not be queued. StoreId={StoreId}, WarehouseNo={WarehouseNo}", storeId, warehouseNo);
        }
    }

    public async Task EnqueueRequiredAsync(
        long storeId,
        int warehouseNo,
        IReadOnlyCollection<BranchPosPriceItem> items,
        CancellationToken cancellationToken)
    {
        if (!options.CurrentValue.BranchPosPriceSync.Enabled || items.Count == 0)
        {
            return;
        }

        using var persistenceTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        persistenceTimeout.CancelAfter(TimeSpan.FromSeconds(10));
        authDbContext.TrendyolGoBranchPosPriceSyncTasks.Add(new TrendyolGoBranchPosPriceSyncTask(
            Guid.NewGuid(), storeId, warehouseNo, JsonSerializer.Serialize(items), clock.UtcNow));
        await authDbContext.SaveChangesAsync(persistenceTimeout.Token);
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
        SELECT pg_advisory_xact_lock(
            hashtextextended(CONCAT(@sdp_depo_no, '|', @sfiyat_stokkod, '|', @sfiyat_listesirano, '|1'), 0));

        WITH updated AS
        (
            UPDATE public.stoksatisfiyat
            SET sfiyat_fiyati = CASE
                    WHEN sfiyat_lastup_date IS NULL OR @sfiyat_lastup_date > sfiyat_lastup_date
                        THEN @sfiyat_fiyati ELSE sfiyat_fiyati END,
                sfiyat_birim_pntr = CASE
                    WHEN sfiyat_lastup_date IS NULL OR @sfiyat_lastup_date > sfiyat_lastup_date
                        THEN @sfiyat_birim_pntr ELSE sfiyat_birim_pntr END,
                sfiyat_birim_adi = CASE
                    WHEN sfiyat_lastup_date IS NULL OR @sfiyat_lastup_date > sfiyat_lastup_date
                        THEN @sfiyat_birim_adi ELSE sfiyat_birim_adi END,
                sfiyat_satisdursun = CASE
                    WHEN sdp_lastup_date IS NULL OR @sdp_lastup_date > sdp_lastup_date
                        THEN @sfiyat_satisdursun ELSE sfiyat_satisdursun END,
                sfiyat_sipdursun = CASE
                    WHEN sdp_lastup_date IS NULL OR @sdp_lastup_date > sdp_lastup_date
                        THEN @sfiyat_sipdursun ELSE sfiyat_sipdursun END,
                sfiyat_malkabuldursun = CASE
                    WHEN sdp_lastup_date IS NULL OR @sdp_lastup_date > sdp_lastup_date
                        THEN @sfiyat_malkabuldursun ELSE sfiyat_malkabuldursun END,
                sfiyat_lastup_date = CASE
                    WHEN sfiyat_lastup_date IS NULL OR @sfiyat_lastup_date > sfiyat_lastup_date
                        THEN @sfiyat_lastup_date ELSE sfiyat_lastup_date END,
                sdp_lastup_date = CASE
                    WHEN sdp_lastup_date IS NULL OR @sdp_lastup_date > sdp_lastup_date
                        THEN @sdp_lastup_date ELSE sdp_lastup_date END
            WHERE sdp_depo_no = @sdp_depo_no
              AND sfiyat_stokkod = @sfiyat_stokkod
              AND sfiyat_listesirano = @sfiyat_listesirano
              AND fiyat_tip_kodu = 1
              AND (sfiyat_lastup_date IS NULL OR @sfiyat_lastup_date > sfiyat_lastup_date
                   OR sdp_lastup_date IS NULL OR @sdp_lastup_date > sdp_lastup_date)
            RETURNING 1
        )
        INSERT INTO public.stoksatisfiyat (
            sdp_depo_no, sfiyat_stokkod, sfiyat_listesirano, sfiyat_birim_pntr,
            sfiyat_birim_adi, sfiyat_fiyati, sfiyat_satisdursun, sfiyat_sipdursun,
            sfiyat_malkabuldursun, sfiyat_lastup_date, sdp_lastup_date, fiyat_tip_kodu
        )
        SELECT @sdp_depo_no, @sfiyat_stokkod, @sfiyat_listesirano, @sfiyat_birim_pntr,
               @sfiyat_birim_adi, @sfiyat_fiyati, @sfiyat_satisdursun, @sfiyat_sipdursun,
               @sfiyat_malkabuldursun, @sfiyat_lastup_date, @sdp_lastup_date, 1
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM public.stoksatisfiyat
            WHERE sdp_depo_no = @sdp_depo_no
              AND sfiyat_stokkod = @sfiyat_stokkod
              AND sfiyat_listesirano = @sfiyat_listesirano
              AND fiyat_tip_kodu = 1
        );
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

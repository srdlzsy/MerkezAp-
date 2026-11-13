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
    TrendyolGoRuntimeSettingsProvider runtimeSettingsProvider,
    ILogger<TrendyolGoBranchPosPriceSyncService> logger)
{
    public async Task EnqueueAsync(long storeId, int warehouseNo, IReadOnlyCollection<BranchPosPriceItem> items, CancellationToken cancellationToken)
    {
        var runtime = await runtimeSettingsProvider.GetAsync(cancellationToken);
        if (!runtime.BranchPosPriceSyncEnabled || items.Count == 0)
        {
            return;
        }

        try
        {
            await EnqueueCoreAsync(storeId, warehouseNo, items, cancellationToken);
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
        var runtime = await runtimeSettingsProvider.GetAsync(cancellationToken);
        if (!runtime.BranchPosPriceSyncEnabled || items.Count == 0)
        {
            return;
        }

        await EnqueueCoreAsync(storeId, warehouseNo, items, cancellationToken);
    }

    private async Task EnqueueCoreAsync(
        long storeId,
        int warehouseNo,
        IReadOnlyCollection<BranchPosPriceItem> items,
        CancellationToken cancellationToken)
    {
        using var persistenceTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        persistenceTimeout.CancelAfter(TimeSpan.FromSeconds(10));
        authDbContext.TrendyolGoBranchPosPriceSyncTasks.Add(new TrendyolGoBranchPosPriceSyncTask(
            Guid.NewGuid(), storeId, warehouseNo, JsonSerializer.Serialize(items), clock.UtcNow));
        await authDbContext.SaveChangesAsync(persistenceTimeout.Token);
    }

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken)
    {
        var config = options.CurrentValue.BranchPosPriceSync;
        var runtime = await runtimeSettingsProvider.GetAsync(cancellationToken);
        if (!runtime.BranchPosPriceSyncEnabled)
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
            var distinctItems = items
                .GroupBy(item => new { item.StockCode, item.PriceListNo })
                .Select(group => group.Last())
                .OrderBy(item => item.StockCode, StringComparer.Ordinal)
                .ThenBy(item => item.PriceListNo)
                .ToArray();
            await using var upsertCommand = CreateUpsertCommand(
                connection,
                transaction,
                task.WarehouseNo,
                distinctItems[0],
                task.CreatedAtUtc);
            foreach (var item in distinctItems)
            {
                SetUpsertParameterValues(upsertCommand, item, task.CreatedAtUtc);
                await upsertCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await AdvanceOnlinePriceSyncKeyAsync(
                connection,
                transaction,
                task.Id,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            task.MarkSucceeded(clock.UtcNow);
            await authDbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Branch POS price synchronization completed. WarehouseNo={WarehouseNo}, TaskId={TaskId}, ItemCount={ItemCount}", task.WarehouseNo, task.Id, items.Length);
            return items.Length;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var failureTime = clock.UtcNow;
            var maxAttempts = Math.Clamp(config.MaxAttempts, 1, 100);
            if (task.AttemptCount + 1 >= maxAttempts)
            {
                task.MarkFailed(exception.Message, failureTime);
            }
            else
            {
                var retryDelay = Math.Clamp(config.RetryDelaySeconds, 30, 3600);
                task.ScheduleRetry(exception.Message, failureTime.AddSeconds(retryDelay));
            }
            await authDbContext.SaveChangesAsync(cancellationToken);
            if (task.Status == TrendyolGoBranchPosPriceSyncStatus.Failed)
            {
                logger.LogError(
                    exception,
                    "Branch POS price synchronization stopped after {AttemptCount} attempts. WarehouseNo={WarehouseNo}, TaskId={TaskId}",
                    task.AttemptCount,
                    task.WarehouseNo,
                    task.Id);
            }
            else
            {
                logger.LogWarning(
                    exception,
                    "Branch POS price synchronization failed and will retry. Attempt={AttemptCount}/{MaxAttempts}, WarehouseNo={WarehouseNo}, TaskId={TaskId}",
                    task.AttemptCount,
                    maxAttempts,
                    task.WarehouseNo,
                    task.Id);
            }
            // The task was claimed and its retry/failure state was persisted. Let the
            // worker continue draining other branches in the same cycle.
            return 1;
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
            SET fiyati = CASE
                    WHEN sfiyat_lastup_date IS NULL OR @sfiyat_lastup_date > sfiyat_lastup_date
                        THEN @sfiyat_fiyati ELSE fiyati END,
                sto_birim_ad = CASE
                    WHEN sfiyat_lastup_date IS NULL OR @sfiyat_lastup_date > sfiyat_lastup_date
                        THEN @sfiyat_birim_adi ELSE sto_birim_ad END,
                sdp_satisdursun = CASE
                    WHEN sdp_lastup_date IS NULL OR @sdp_lastup_date > sdp_lastup_date
                        THEN @sfiyat_satisdursun ELSE sdp_satisdursun END,
                sdp_sipdursun = CASE
                    WHEN sdp_lastup_date IS NULL OR @sdp_lastup_date > sdp_lastup_date
                        THEN @sfiyat_sipdursun ELSE sdp_sipdursun END,
                sdp_malkabuldursun = CASE
                    WHEN sdp_lastup_date IS NULL OR @sdp_lastup_date > sdp_lastup_date
                        THEN @sfiyat_malkabuldursun ELSE sdp_malkabuldursun END,
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
            sdp_depo_no, sfiyat_stokkod, sfiyat_listesirano,
            sto_birim_ad, fiyati, sdp_satisdursun, sdp_sipdursun,
            sdp_malkabuldursun, sfiyat_lastup_date, sdp_lastup_date, fiyat_tip_kodu
        )
        SELECT @sdp_depo_no, @sfiyat_stokkod, @sfiyat_listesirano,
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

    internal const string AdvanceSyncKeyCommandText = """
        INSERT INTO public.sync (sync_type, sync_key, created_at, updated_at)
        VALUES ('online_satis_fiyatlari_sync', @sync_key, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
        ON CONFLICT (sync_type) DO UPDATE
        SET sync_key = EXCLUDED.sync_key,
            updated_at = CURRENT_TIMESTAMP;
        """;

    private static NpgsqlCommand CreateUpsertCommand(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int warehouseNo,
        BranchPosPriceItem item,
        DateTime taskCreatedAtUtc)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = UpsertCommandText;
        command.Parameters.AddWithValue("sdp_depo_no", warehouseNo);
        command.Parameters.AddWithValue("sfiyat_stokkod", item.StockCode);
        command.Parameters.AddWithValue("sfiyat_listesirano", item.PriceListNo);
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
        return command;
    }

    private static void SetUpsertParameterValues(
        NpgsqlCommand command,
        BranchPosPriceItem item,
        DateTime taskCreatedAtUtc)
    {
        command.Parameters["sfiyat_stokkod"].Value = item.StockCode;
        command.Parameters["sfiyat_listesirano"].Value = item.PriceListNo;
        command.Parameters["sfiyat_birim_adi"].Value = item.UnitName;
        command.Parameters["sfiyat_fiyati"].Value = item.Price;
        command.Parameters["sfiyat_satisdursun"].Value = item.SalesBlocked;
        command.Parameters["sfiyat_sipdursun"].Value = item.OrderBlocked;
        command.Parameters["sfiyat_malkabuldursun"].Value = item.GoodsAcceptanceBlocked;
        command.Parameters["sfiyat_lastup_date"].Value =
            ResolveSourceVersion(item.PriceUpdatedAtUtc, taskCreatedAtUtc);
        command.Parameters["sdp_lastup_date"].Value =
            ResolveSourceVersion(item.WarehouseUpdatedAtUtc, taskCreatedAtUtc);
    }

    private static async Task AdvanceOnlinePriceSyncKeyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = AdvanceSyncKeyCommandText;
        command.Parameters.AddWithValue("sync_key", $"FurpaMerkezApi_{taskId:N}");
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

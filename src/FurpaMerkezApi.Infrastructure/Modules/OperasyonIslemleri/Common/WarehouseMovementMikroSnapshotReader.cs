using System.Data;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.Common;

public interface IWarehouseMovementMikroSnapshotReader
{
    Task<WarehouseMovementMikroSnapshot> ReadAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken);
}

public sealed class WarehouseMovementMikroSnapshotReader(
    MikroDbContext mikroDbContext,
    IMemoryCache cache,
    ILogger<WarehouseMovementMikroSnapshotReader> logger)
    : IWarehouseMovementMikroSnapshotReader
{
    private const byte InterWarehouseShippingDocumentType = 17;
    private const byte DeliveredToTargetWarehouseState = 1;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public async Task<WarehouseMovementMikroSnapshot> ReadAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"warehouse-operation-mikro:{startDate:yyyyMMdd}:{endDate:yyyyMMdd}";
        if (cache.TryGetValue(cacheKey, out WarehouseMovementMikroSnapshot? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var snapshot = new WarehouseMovementMikroSnapshot(
                true,
                await ExecuteQueryAsync(startDate, endDate, cancellationToken));
            cache.Set(cacheKey, snapshot, CacheDuration);
            return snapshot;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Mikro warehouse movement snapshot could not be loaded. StartDate={StartDate}, EndDate={EndDate}",
                startDate,
                endDate);
            return WarehouseMovementMikroSnapshot.Unavailable;
        }
    }

    private async Task<IReadOnlyCollection<WarehouseMovementMikroRow>> ExecuteQueryAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var start = startDate.ToDateTime(TimeOnly.MinValue);
        var endExclusive = endDate.AddDays(1).ToDateTime(TimeOnly.MinValue);

        async Task<List<WarehouseMovementProjection>> QueryAsync()
        {
            return await mikroDbContext.STOK_HAREKETLERIs
                .AsNoTracking()
                .Where(movement =>
                    movement.sth_evraktip == InterWarehouseShippingDocumentType &&
                    movement.sth_tarih.HasValue &&
                    movement.sth_tarih.Value >= start &&
                    movement.sth_tarih.Value < endExclusive)
                .GroupBy(movement => new
                {
                    movement.sth_evrakno_seri,
                    movement.sth_evrakno_sira,
                    movement.sth_normal_iade,
                    movement.sth_cikis_depo_no
                })
                .Select(group => new WarehouseMovementProjection(
                    group.Key.sth_evrakno_seri ?? string.Empty,
                    group.Key.sth_evrakno_sira ?? 0,
                    group.Key.sth_normal_iade == 1,
                    group.Key.sth_cikis_depo_no ?? 0,
                    group.Min(movement => movement.sth_nakliyedeposu) ?? 0,
                    group.Min(movement => movement.sth_giris_depo_no) ?? 0,
                    group.Min(movement => movement.sth_nakliyedurumu) ?? 0,
                    group.Max(movement => movement.sth_nakliyedurumu) ?? 0,
                    group.Count(),
                    group.Count(movement =>
                        movement.sth_kilitli == true &&
                        movement.sth_belge_no != null &&
                        movement.sth_belge_no.StartsWith("FRM") &&
                        movement.sth_aciklama != null &&
                        movement.sth_aciklama.Length == 36),
                    group.Min(movement => movement.sth_tarih),
                    group.Min(movement => movement.sth_create_date),
                    group.Max(movement => movement.sth_lastup_date)))
                .ToListAsync(cancellationToken);
        }

        List<WarehouseMovementProjection> rows;
        if (!IsSqlServerProvider())
        {
            rows = await QueryAsync();
        }
        else
        {
            var executionStrategy = mikroDbContext.Database.CreateExecutionStrategy();
            rows = await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await mikroDbContext.Database.BeginTransactionAsync(
                    IsolationLevel.ReadUncommitted,
                    cancellationToken);
                var result = await QueryAsync();
                await transaction.CommitAsync(cancellationToken);
                return result;
            });
        }

        return rows
            .Where(row => row.SourceWarehouseNo > 0 && row.DocumentDate.HasValue)
            .Select(row =>
            {
                var isAccepted = row.MinimumTransportState == DeliveredToTargetWarehouseState &&
                                 row.MaximumTransportState == DeliveredToTargetWarehouseState;
                return new WarehouseMovementMikroRow(
                    row.DocumentSerie.Trim(),
                    row.DocumentOrderNo,
                    row.IsReturn,
                    row.SourceWarehouseNo,
                    isAccepted ? row.AcceptedTargetWarehouseNo : row.PendingTargetWarehouseNo,
                    DateOnly.FromDateTime(row.DocumentDate!.Value),
                    row.CreatedAt,
                    row.UpdatedAt,
                    isAccepted,
                    row.LineCount > 0 && row.SentEDespatchLineCount == row.LineCount);
            })
            .ToArray();
    }

    private bool IsSqlServerProvider() =>
        mikroDbContext.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true;

    private sealed record WarehouseMovementProjection(
        string DocumentSerie,
        int DocumentOrderNo,
        bool IsReturn,
        int SourceWarehouseNo,
        int PendingTargetWarehouseNo,
        int AcceptedTargetWarehouseNo,
        byte MinimumTransportState,
        byte MaximumTransportState,
        int LineCount,
        int SentEDespatchLineCount,
        DateTime? DocumentDate,
        DateTime CreatedAt,
        DateTime? UpdatedAt);
}

public sealed record WarehouseMovementMikroSnapshot(
    bool IsAvailable,
    IReadOnlyCollection<WarehouseMovementMikroRow> Rows)
{
    public static WarehouseMovementMikroSnapshot Unavailable { get; } = new(false, []);
}

public sealed record WarehouseMovementMikroRow(
    string DocumentSerie,
    int DocumentOrderNo,
    bool IsReturn,
    int SourceWarehouseNo,
    int TargetWarehouseNo,
    DateOnly DocumentDate,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool IsAccepted,
    bool IsEDespatchSent);

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
            var queryResult = await ExecuteQueryAsync(startDate, endDate, cancellationToken);
            var snapshot = new WarehouseMovementMikroSnapshot(
                true,
                queryResult.WarehouseRows,
                queryResult.CompanyRows);
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

    private async Task<MovementQueryResult> ExecuteQueryAsync(
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
                    movement.sth_iptal != true &&
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

        async Task<List<CompanyMovementProjection>> QueryCompanyMovementsAsync()
        {
            return await mikroDbContext.STOK_HAREKETLERIs
                .AsNoTracking()
                .Where(movement =>
                    movement.sth_iptal != true &&
                    movement.sth_belge_tarih.HasValue &&
                    movement.sth_belge_tarih.Value >= start &&
                    movement.sth_belge_tarih.Value < endExclusive &&
                    movement.sth_evraktip == 1 &&
                    movement.sth_tip == 1 &&
                    (movement.sth_normal_iade == 0 || movement.sth_normal_iade == 1))
                .GroupBy(movement => new
                {
                    movement.sth_evrakno_seri,
                    movement.sth_evrakno_sira,
                    movement.sth_normal_iade,
                    movement.sth_cikis_depo_no
                })
                .Select(group => new CompanyMovementProjection(
                    group.Key.sth_evrakno_seri ?? string.Empty,
                    group.Key.sth_evrakno_sira ?? 0,
                    group.Key.sth_normal_iade == 1,
                    group.Key.sth_cikis_depo_no ?? 0,
                    group.Count(),
                    group.Count(movement =>
                        movement.sth_kilitli == true &&
                        movement.sth_belge_no != null &&
                        movement.sth_belge_no.StartsWith("FRM") &&
                        movement.sth_aciklama != null &&
                        movement.sth_aciklama.Length == 36),
                    group.Min(movement => movement.sth_belge_tarih),
                    group.Min(movement => movement.sth_create_date),
                    group.Max(movement => movement.sth_lastup_date)))
                .ToListAsync(cancellationToken);
        }

        (List<WarehouseMovementProjection> WarehouseRows, List<CompanyMovementProjection> CompanyRows) rows;
        if (!IsSqlServerProvider())
        {
            rows = (await QueryAsync(), await QueryCompanyMovementsAsync());
        }
        else
        {
            var executionStrategy = mikroDbContext.Database.CreateExecutionStrategy();
            rows = await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await mikroDbContext.Database.BeginTransactionAsync(
                    IsolationLevel.ReadUncommitted,
                    cancellationToken);
                var result = (await QueryAsync(), await QueryCompanyMovementsAsync());
                await transaction.CommitAsync(cancellationToken);
                return result;
            });
        }

        var warehouseRows = rows.WarehouseRows
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
        var companyRows = rows.CompanyRows
            .Where(row => row.SourceWarehouseNo > 0 && row.DocumentDate.HasValue)
            .Select(row => new CompanyMovementMikroRow(
                row.DocumentSerie.Trim(),
                row.DocumentOrderNo,
                row.IsReturn,
                row.SourceWarehouseNo,
                DateOnly.FromDateTime(row.DocumentDate!.Value),
                row.CreatedAt,
                row.UpdatedAt,
                row.LineCount > 0 && row.SentEDespatchLineCount == row.LineCount))
            .ToArray();

        return new MovementQueryResult(warehouseRows, companyRows);
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

    private sealed record CompanyMovementProjection(
        string DocumentSerie,
        int DocumentOrderNo,
        bool IsReturn,
        int SourceWarehouseNo,
        int LineCount,
        int SentEDespatchLineCount,
        DateTime? DocumentDate,
        DateTime CreatedAt,
        DateTime? UpdatedAt);

    private sealed record MovementQueryResult(
        IReadOnlyCollection<WarehouseMovementMikroRow> WarehouseRows,
        IReadOnlyCollection<CompanyMovementMikroRow> CompanyRows);
}

public sealed record WarehouseMovementMikroSnapshot(
    bool IsAvailable,
    IReadOnlyCollection<WarehouseMovementMikroRow> Rows,
    IReadOnlyCollection<CompanyMovementMikroRow>? CompanyRows = null)
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

public sealed record CompanyMovementMikroRow(
    string DocumentSerie,
    int DocumentOrderNo,
    bool IsReturn,
    int SourceWarehouseNo,
    DateOnly DocumentDate,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool IsEDespatchSent);

using System.Data;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.Common;

public interface IAxataOrderMikroSnapshotReader
{
    Task<AxataOrderMikroSnapshot> ReadAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken);
}

public sealed class AxataOrderMikroSnapshotReader(
    MikroDbContext mikroDbContext,
    IMemoryCache cache,
    IOptionsMonitor<AxataSynchronizationOptions> options,
    ILogger<AxataOrderMikroSnapshotReader> logger)
    : IAxataOrderMikroSnapshotReader
{
    private const string IssuedWarehouseTask = "issued-warehouse-order-sync";
    private const string WarehouseInboundTask = "warehouse-inbound-order-sync";
    private const string ReceivedCompanyTask = "received-company-order-sync";
    private const string CompanyReceivingTask = "company-receiving-sync";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public async Task<AxataOrderMikroSnapshot> ReadAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(options.CurrentValue);
        var cacheKey = $"warehouse-operation-axata-orders:{startDate:yyyyMMdd}:{endDate:yyyyMMdd}:{scope.CacheKey}";
        if (cache.TryGetValue(cacheKey, out AxataOrderMikroSnapshot? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var rows = scope.IsEnabled
                ? await ExecuteQueryAsync(startDate, endDate, scope, cancellationToken)
                : Array.Empty<AxataOrderMikroRow>();
            var snapshot = new AxataOrderMikroSnapshot(true, rows);
            cache.Set(cacheKey, snapshot, CacheDuration);
            return snapshot;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Mikro AXATA order snapshot could not be loaded. StartDate={StartDate}, EndDate={EndDate}",
                startDate,
                endDate);
            return AxataOrderMikroSnapshot.Unavailable;
        }
    }

    private async Task<IReadOnlyCollection<AxataOrderMikroRow>> ExecuteQueryAsync(
        DateOnly startDate,
        DateOnly endDate,
        AxataOrderScope scope,
        CancellationToken cancellationToken)
    {
        var start = startDate.ToDateTime(TimeOnly.MinValue);
        var endExclusive = endDate.AddDays(1).ToDateTime(TimeOnly.MinValue);

        async Task<List<WarehouseOrderProjection>> QueryWarehouseOrdersAsync()
        {
            return await mikroDbContext.DEPOLAR_ARASI_SIPARISLERs
                .AsNoTracking()
                .Where(order =>
                    order.ssip_iptal != true &&
                    ((order.ssip_tarih.HasValue &&
                      order.ssip_tarih.Value >= start &&
                      order.ssip_tarih.Value < endExclusive) ||
                     (order.ssip_create_date >= start && order.ssip_create_date < endExclusive) ||
                     (order.ssip_lastup_date.HasValue &&
                      order.ssip_lastup_date.Value >= start &&
                      order.ssip_lastup_date.Value < endExclusive)))
                .GroupBy(order => new
                {
                    order.ssip_tarih,
                    order.ssip_evrakno_seri,
                    order.ssip_evrakno_sira,
                    order.ssip_girdepo,
                    order.ssip_cikdepo
                })
                .Select(group => new WarehouseOrderProjection(
                    group.Key.ssip_evrakno_seri ?? string.Empty,
                    group.Key.ssip_evrakno_sira ?? 0,
                    group.Key.ssip_girdepo ?? 0,
                    group.Key.ssip_cikdepo ?? 0,
                    group.Count(),
                    group.Count(order => order.ssip_special1 == "1"),
                    group.Key.ssip_tarih,
                    group.Min(order => order.ssip_create_date),
                    group.Max(order => order.ssip_lastup_date),
                    group.Sum(order => order.ssip_miktar ?? 0d),
                    group.Sum(order => order.ssip_teslim_miktar ?? 0d)))
                .ToListAsync(cancellationToken);
        }

        async Task<List<CompanyOrderProjection>> QueryCompanyOrdersAsync()
        {
            var orders = await mikroDbContext.SIPARISLERs
                .AsNoTracking()
                .Where(order =>
                    order.sip_cins == 0 &&
                    order.sip_iptal != true &&
                    order.sip_kapat_fl != true &&
                    ((order.sip_tarih.HasValue &&
                      order.sip_tarih.Value >= start &&
                      order.sip_tarih.Value < endExclusive) ||
                     (order.sip_create_date >= start && order.sip_create_date < endExclusive) ||
                     (order.sip_lastup_date.HasValue &&
                      order.sip_lastup_date.Value >= start &&
                      order.sip_lastup_date.Value < endExclusive)))
                .GroupBy(order => new
                {
                    order.sip_tarih,
                    order.sip_evrakno_seri,
                    order.sip_evrakno_sira,
                    order.sip_depono,
                    order.sip_tip
                })
                .Select(group => new CompanyOrderProjection(
                    group.Key.sip_evrakno_seri ?? string.Empty,
                    group.Key.sip_evrakno_sira ?? 0,
                    group.Key.sip_depono ?? 0,
                    group.Key.sip_tip ?? 0,
                    group.Count(),
                    group.Count(order => order.sip_special1 == "1"),
                    group.Key.sip_tarih,
                    group.Min(order => order.sip_create_date),
                    group.Max(order => order.sip_lastup_date),
                    group.Sum(order => order.sip_miktar ?? 0d),
                    group.Sum(order => order.sip_teslim_miktar ?? 0d)))
                .ToListAsync(cancellationToken);

            return orders
                .Where(order => order.TotalQuantity > order.DeliveredQuantity + 0.000001d)
                .ToList();
        }

        (List<WarehouseOrderProjection> WarehouseRows, List<CompanyOrderProjection> CompanyRows) result;
        if (!IsSqlServerProvider())
        {
            result = (await QueryWarehouseOrdersAsync(), await QueryCompanyOrdersAsync());
        }
        else
        {
            var strategy = mikroDbContext.Database.CreateExecutionStrategy();
            result = await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await mikroDbContext.Database.BeginTransactionAsync(
                    IsolationLevel.ReadUncommitted,
                    cancellationToken);
                var rows = (await QueryWarehouseOrdersAsync(), await QueryCompanyOrdersAsync());
                await transaction.CommitAsync(cancellationToken);
                return rows;
            });
        }

        var rows = new List<AxataOrderMikroRow>();
        foreach (var order in result.WarehouseRows)
        {
            if (scope.IssuedWarehouseNo.HasValue && order.OutWarehouseNo == scope.IssuedWarehouseNo.Value)
            {
                rows.Add(CreateWarehouseRow(
                    order,
                    DocumentFlowType.IssuedWarehouseOrder,
                    order.InWarehouseNo,
                    order.OutWarehouseNo,
                    IssuedWarehouseTask));
            }

            if (scope.InboundWarehouseNo.HasValue && order.InWarehouseNo == scope.InboundWarehouseNo.Value)
            {
                rows.Add(CreateWarehouseRow(
                    order,
                    DocumentFlowType.ReceivedWarehouseOrder,
                    order.OutWarehouseNo,
                    order.InWarehouseNo,
                    WarehouseInboundTask));
            }
        }

        foreach (var order in result.CompanyRows)
        {
            if (order.OrderType == 0 &&
                scope.ReceivedCompanyEnabled &&
                (!scope.ReceivedCompanyWarehouseNo.HasValue ||
                 order.WarehouseNo == scope.ReceivedCompanyWarehouseNo.Value))
            {
                rows.Add(CreateCompanyRow(order, DocumentFlowType.ReceivedCompanyOrder, ReceivedCompanyTask));
            }

            if (order.OrderType == 1 &&
                scope.CompanyReceivingWarehouseNo.HasValue &&
                order.WarehouseNo == scope.CompanyReceivingWarehouseNo.Value)
            {
                rows.Add(CreateCompanyRow(order, DocumentFlowType.IssuedCompanyOrder, CompanyReceivingTask));
            }
        }

        return rows;
    }

    private static AxataOrderMikroRow CreateWarehouseRow(
        WarehouseOrderProjection order,
        DocumentFlowType documentType,
        int sourceWarehouseNo,
        int targetWarehouseNo,
        string taskCode) =>
        new(
            documentType,
            sourceWarehouseNo,
            targetWarehouseNo,
            order.DocumentSerie.Trim(),
            order.DocumentOrderNo,
            order.DocumentDate.HasValue ? DateOnly.FromDateTime(order.DocumentDate.Value) : null,
            order.CreatedAt,
            order.UpdatedAt,
            order.LineCount > 0 && order.SentLineCount == order.LineCount,
            order.TotalQuantity,
            order.DeliveredQuantity,
            taskCode);

    private static AxataOrderMikroRow CreateCompanyRow(
        CompanyOrderProjection order,
        DocumentFlowType documentType,
        string taskCode) =>
        new(
            documentType,
            order.WarehouseNo,
            null,
            order.DocumentSerie.Trim(),
            order.DocumentOrderNo,
            order.DocumentDate.HasValue ? DateOnly.FromDateTime(order.DocumentDate.Value) : null,
            order.CreatedAt,
            order.UpdatedAt,
            order.LineCount > 0 && order.SentLineCount == order.LineCount,
            order.TotalQuantity,
            order.DeliveredQuantity,
            taskCode);

    private static AxataOrderScope ResolveScope(AxataSynchronizationOptions current)
    {
        if (!current.Enabled)
        {
            return AxataOrderScope.Disabled;
        }

        int? WarehouseFor(string taskCode) =>
            current.Tasks.TryGetValue(taskCode, out var task) &&
            task.Enabled &&
            task.DefaultWarehouseNo is > 0
                ? task.DefaultWarehouseNo
                : null;

        var receivedCompanyEnabled = current.Tasks.TryGetValue(ReceivedCompanyTask, out var receivedCompanyTask) &&
                                     receivedCompanyTask.Enabled;

        return new AxataOrderScope(
            WarehouseFor(IssuedWarehouseTask),
            WarehouseFor(WarehouseInboundTask),
            receivedCompanyEnabled,
            receivedCompanyTask?.DefaultWarehouseNo is > 0
                ? receivedCompanyTask.DefaultWarehouseNo
                : null,
            WarehouseFor(CompanyReceivingTask));
    }

    private bool IsSqlServerProvider() =>
        mikroDbContext.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true;

    private sealed record WarehouseOrderProjection(
        string DocumentSerie,
        int DocumentOrderNo,
        int InWarehouseNo,
        int OutWarehouseNo,
        int LineCount,
        int SentLineCount,
        DateTime? DocumentDate,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        double TotalQuantity,
        double DeliveredQuantity);

    private sealed record CompanyOrderProjection(
        string DocumentSerie,
        int DocumentOrderNo,
        int WarehouseNo,
        byte OrderType,
        int LineCount,
        int SentLineCount,
        DateTime? DocumentDate,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        double TotalQuantity,
        double DeliveredQuantity);

    private sealed record AxataOrderScope(
        int? IssuedWarehouseNo,
        int? InboundWarehouseNo,
        bool ReceivedCompanyEnabled,
        int? ReceivedCompanyWarehouseNo,
        int? CompanyReceivingWarehouseNo)
    {
        public static AxataOrderScope Disabled { get; } = new(null, null, false, null, null);

        public bool IsEnabled =>
            IssuedWarehouseNo.HasValue ||
            InboundWarehouseNo.HasValue ||
            ReceivedCompanyEnabled ||
            CompanyReceivingWarehouseNo.HasValue;

        public string CacheKey =>
            $"{IssuedWarehouseNo}:{InboundWarehouseNo}:{ReceivedCompanyEnabled}:{ReceivedCompanyWarehouseNo}:{CompanyReceivingWarehouseNo}";
    }
}

public sealed record AxataOrderMikroSnapshot(
    bool IsAvailable,
    IReadOnlyCollection<AxataOrderMikroRow> Rows)
{
    public static AxataOrderMikroSnapshot Unavailable { get; } = new(false, []);
}

public sealed record AxataOrderMikroRow(
    DocumentFlowType DocumentType,
    int SourceWarehouseNo,
    int? TargetWarehouseNo,
    string DocumentSerie,
    int DocumentOrderNo,
    DateOnly? DocumentDate,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool IsSent,
    double TotalQuantity,
    double DeliveredQuantity,
    string TaskCode)
{
    public string FlowKey => DocumentFlowKeys.Create(
        DocumentType,
        SourceWarehouseNo,
        DocumentSerie,
        DocumentOrderNo);
}

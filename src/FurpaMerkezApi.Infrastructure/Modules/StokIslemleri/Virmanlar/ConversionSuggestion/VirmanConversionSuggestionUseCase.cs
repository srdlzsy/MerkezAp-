using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar.ConversionSuggestion;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Virmanlar.ConversionSuggestion;

public sealed class VirmanConversionSuggestionUseCase(
    MikroDbContext mikroDbContext,
    IMemoryCache cache,
    IClock clock) : IVirmanConversionSuggestionUseCase
{
    internal const int MinimumSampleCount = 10;
    internal const int MaximumSampleCount = 500;
    internal const double MinimumConfidencePercent = 95d;

    private const int LookbackDays = 365;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);

    public async Task<VirmanConversionSuggestionDto> ExecuteAsync(
        VirmanConversionSuggestionRequest request,
        CancellationToken cancellationToken)
    {
        var sourceStockCode = NormalizeStockCode(request.SourceStockCode);
        ValidateQuantity(request.SourceQuantity);

        var cacheKey = $"virman-conversion-history:{sourceStockCode.ToUpperInvariant()}";
        if (!cache.TryGetValue(cacheKey, out HistorySnapshot? snapshot) || snapshot is null)
        {
            snapshot = await LoadSnapshotAsync(sourceStockCode, cancellationToken);
            cache.Set(cacheKey, snapshot, CacheDuration);
        }

        double? targetQuantity = snapshot.IsReliable && snapshot.Multiplier.HasValue
            ? Math.Round(request.SourceQuantity * snapshot.Multiplier.Value, 6, MidpointRounding.AwayFromZero)
            : null;

        return new VirmanConversionSuggestionDto(
            snapshot.SourceStockCode,
            snapshot.SourceStockName,
            snapshot.SourceUnitName,
            request.SourceQuantity,
            snapshot.IsReliable ? snapshot.TargetStockCode : null,
            snapshot.IsReliable ? snapshot.TargetStockName : null,
            snapshot.IsReliable ? snapshot.TargetUnitName : null,
            snapshot.IsReliable ? snapshot.Multiplier : null,
            targetQuantity,
            snapshot.SampleCount,
            snapshot.TargetMatchCount,
            snapshot.MultiplierMatchCount,
            snapshot.TargetConfidencePercent,
            snapshot.MultiplierConfidencePercent,
            snapshot.ConfidencePercent,
            snapshot.IsReliable,
            snapshot.IsReliable ? "VirmanHistory" : "None",
            snapshot.LookbackStartDate,
            snapshot.LookbackEndDate,
            MinimumSampleCount,
            MaximumSampleCount,
            MinimumConfidencePercent,
            snapshot.Warning);
    }

    private async Task<HistorySnapshot> LoadSnapshotAsync(
        string sourceStockCode,
        CancellationToken cancellationToken)
    {
        var sourceStock = await mikroDbContext.STOKLARs
            .AsNoTracking()
            .Where(stock => stock.sto_kod == sourceStockCode)
            .Select(stock => new StockSnapshot(
                stock.sto_kod,
                stock.sto_isim ?? string.Empty,
                stock.sto_birim1_ad ?? string.Empty,
                stock.sto_iptal == true,
                stock.sto_pasif_fl == true))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Stock card was not found: {sourceStockCode}");

        var lookbackEndDate = clock.UtcNow.Date;
        var lookbackStartDate = lookbackEndDate.AddDays(-LookbackDays);
        var lookbackEndExclusive = lookbackEndDate.AddDays(1);

        if (!IsActive(sourceStock))
        {
            return HistorySnapshot.Unreliable(
                sourceStock,
                lookbackStartDate,
                lookbackEndDate,
                "Kaynak stok pasif veya DLS durumunda oldugu icin otomatik donusum onerilmedi.");
        }

        var sourceRows = mikroDbContext.STOK_HAREKETLERIs
            .AsNoTracking()
            .Where(source =>
                source.sth_stok_kod == sourceStockCode &&
                source.sth_tarih >= lookbackStartDate &&
                source.sth_tarih < lookbackEndExclusive &&
                source.sth_iptal != true &&
                source.sth_evraktip == 6 &&
                source.sth_cins == 3 &&
                source.sth_normal_iade == 0 &&
                source.sth_tip == 1 &&
                source.sth_miktar > 0d)
            .OrderByDescending(source => source.sth_tarih)
            .ThenByDescending(source => source.sth_create_date)
            .ThenByDescending(source => source.sth_evrakno_sira)
            .ThenByDescending(source => source.sth_satirno)
            .Take(MaximumSampleCount);

        var pairedRows = await (
            from source in sourceRows
            join target in mikroDbContext.STOK_HAREKETLERIs.AsNoTracking()
                on new
                {
                    source.sth_DBCno,
                    CompanyNo = source.sth_firmano,
                    BranchNo = source.sth_subeno,
                    DocumentType = source.sth_evraktip,
                    DocumentSerie = source.sth_evrakno_seri,
                    DocumentOrderNo = source.sth_evrakno_sira,
                    RowNo = source.sth_satirno + 1
                }
                equals new
                {
                    target.sth_DBCno,
                    CompanyNo = target.sth_firmano,
                    BranchNo = target.sth_subeno,
                    DocumentType = target.sth_evraktip,
                    DocumentSerie = target.sth_evrakno_seri,
                    DocumentOrderNo = target.sth_evrakno_sira,
                    RowNo = target.sth_satirno
                }
            where target.sth_iptal != true &&
                  target.sth_cins == 3 &&
                  target.sth_normal_iade == 0 &&
                  target.sth_tip == 0 &&
                  target.sth_miktar > 0d &&
                  target.sth_stok_kod != sourceStockCode &&
                  target.sth_cikis_depo_no == source.sth_cikis_depo_no &&
                  target.sth_giris_depo_no == source.sth_giris_depo_no
            select new PairedMovementRow(
                target.sth_stok_kod ?? string.Empty,
                source.sth_miktar ?? 0d,
                target.sth_miktar ?? 0d))
            .TagWith("Virman conversion suggestion from historical adjacent movement pairs")
            .ToArrayAsync(cancellationToken);

        var analysis = VirmanConversionHistoryAnalyzer.Analyze(pairedRows);
        if (analysis is null)
        {
            return HistorySnapshot.Unreliable(
                sourceStock,
                lookbackStartDate,
                lookbackEndDate,
                "Guvenilir otomatik donusum bulunamadi; hedef urun ve miktari manuel secin.");
        }

        var targetStock = await mikroDbContext.STOKLARs
            .AsNoTracking()
            .Where(stock => stock.sto_kod == analysis.TargetStockCode)
            .Select(stock => new StockSnapshot(
                stock.sto_kod,
                stock.sto_isim ?? string.Empty,
                stock.sto_birim1_ad ?? string.Empty,
                stock.sto_iptal == true,
                stock.sto_pasif_fl == true))
            .SingleOrDefaultAsync(cancellationToken);

        var meetsThreshold = analysis.SampleCount >= MinimumSampleCount &&
                             analysis.TargetConfidencePercent >= MinimumConfidencePercent &&
                             analysis.MultiplierConfidencePercent >= MinimumConfidencePercent;
        var isReliable = meetsThreshold && targetStock is not null && IsActive(targetStock);

        var warning = isReliable
            ? null
            : targetStock is not null && !IsActive(targetStock)
                ? "Gecmisteki hedef stok pasif veya DLS durumunda; hedef urunu manuel secin."
                : "Guvenilir otomatik donusum bulunamadi; hedef urun ve miktari manuel secin.";

        return new HistorySnapshot(
            sourceStock.StockCode.Trim(),
            sourceStock.StockName.Trim(),
            sourceStock.UnitName.Trim(),
            targetStock?.StockCode.Trim(),
            targetStock?.StockName.Trim(),
            targetStock?.UnitName.Trim(),
            analysis.Multiplier,
            analysis.SampleCount,
            analysis.TargetMatchCount,
            analysis.MultiplierMatchCount,
            analysis.TargetConfidencePercent,
            analysis.MultiplierConfidencePercent,
            Math.Min(analysis.TargetConfidencePercent, analysis.MultiplierConfidencePercent),
            isReliable,
            lookbackStartDate,
            lookbackEndDate,
            warning);
    }

    private static string NormalizeStockCode(string? stockCode)
    {
        var normalized = stockCode?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Source stock code is required.", nameof(stockCode));
        }

        if (normalized.Length > 25)
        {
            throw new ArgumentException("Source stock code can not exceed 25 characters.", nameof(stockCode));
        }

        return normalized;
    }

    private static void ValidateQuantity(double quantity)
    {
        if (!double.IsFinite(quantity) || quantity <= 0d)
        {
            throw new ArgumentException("Source quantity must be greater than zero.", nameof(quantity));
        }
    }

    private static bool IsActive(StockSnapshot stock) =>
        !stock.IsCancelled &&
        !stock.IsPassive &&
        !stock.StockName.TrimStart().StartsWith("DLS", StringComparison.OrdinalIgnoreCase);

    private sealed record StockSnapshot(
        string StockCode,
        string StockName,
        string UnitName,
        bool IsCancelled,
        bool IsPassive);

    private sealed record HistorySnapshot(
        string SourceStockCode,
        string SourceStockName,
        string SourceUnitName,
        string? TargetStockCode,
        string? TargetStockName,
        string? TargetUnitName,
        double? Multiplier,
        int SampleCount,
        int TargetMatchCount,
        int MultiplierMatchCount,
        double TargetConfidencePercent,
        double MultiplierConfidencePercent,
        double ConfidencePercent,
        bool IsReliable,
        DateTime LookbackStartDate,
        DateTime LookbackEndDate,
        string? Warning)
    {
        public static HistorySnapshot Unreliable(
            StockSnapshot source,
            DateTime lookbackStartDate,
            DateTime lookbackEndDate,
            string warning) =>
            new(
                source.StockCode.Trim(),
                source.StockName.Trim(),
                source.UnitName.Trim(),
                null,
                null,
                null,
                null,
                0,
                0,
                0,
                0d,
                0d,
                0d,
                false,
                lookbackStartDate,
                lookbackEndDate,
                warning);
    }
}

internal sealed record PairedMovementRow(
    string TargetStockCode,
    double SourceQuantity,
    double TargetQuantity);

internal sealed record VirmanConversionHistoryAnalysis(
    string TargetStockCode,
    double Multiplier,
    int SampleCount,
    int TargetMatchCount,
    int MultiplierMatchCount,
    double TargetConfidencePercent,
    double MultiplierConfidencePercent);

internal static class VirmanConversionHistoryAnalyzer
{
    public static VirmanConversionHistoryAnalysis? Analyze(IEnumerable<PairedMovementRow> rows)
    {
        var observations = rows
            .Select(CreateObservation)
            .Where(observation => observation is not null)
            .Select(observation => observation!)
            .ToArray();

        if (observations.Length == 0)
        {
            return null;
        }

        var targetGroup = observations
            .GroupBy(observation => observation.TargetStockCode, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .First();

        var multiplierGroup = targetGroup
            .GroupBy(observation => observation.Multiplier)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .First();

        var targetMatchCount = targetGroup.Count();
        var multiplierMatchCount = multiplierGroup.Count();

        return new VirmanConversionHistoryAnalysis(
            targetGroup.Key,
            multiplierGroup.Key,
            observations.Length,
            targetMatchCount,
            multiplierMatchCount,
            Percent(targetMatchCount, observations.Length),
            Percent(multiplierMatchCount, targetMatchCount));
    }

    private static Observation? CreateObservation(PairedMovementRow row)
    {
        var targetStockCode = row.TargetStockCode.Trim();
        if (string.IsNullOrWhiteSpace(targetStockCode) ||
            !double.IsFinite(row.SourceQuantity) ||
            !double.IsFinite(row.TargetQuantity) ||
            row.SourceQuantity <= 0d ||
            row.TargetQuantity <= 0d)
        {
            return null;
        }

        var multiplier = Math.Round(
            row.TargetQuantity / row.SourceQuantity,
            6,
            MidpointRounding.AwayFromZero);

        return double.IsFinite(multiplier) && multiplier > 0d
            ? new Observation(targetStockCode, multiplier)
            : null;
    }

    private static double Percent(int numerator, int denominator) =>
        denominator <= 0
            ? 0d
            : Math.Round(numerator * 100d / denominator, 2, MidpointRounding.AwayFromZero);

    private sealed record Observation(string TargetStockCode, double Multiplier);
}

using System.Data;
using System.Data.Common;
using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.StokIslemleri.StokAnomaliMerkezi;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.StokAnomaliMerkezi;

public sealed class StockAnomalyCenterService(
    AuthDbContext authDbContext,
    MikroDbContext mikroDbContext,
    IClock clock,
    ILogger<StockAnomalyCenterService> logger)
    : IStockAnomalyCenterService
{
    private const double QuantityTolerance = 0.000001d;

    public async Task<StockAnomalyListResponse> ListAsync(
        StockAnomalyListRequest request,
        CancellationToken cancellationToken)
    {
        var take = Math.Clamp(request.Take, 1, 500);
        var query = ApplyListFilters(authDbContext.StockAnomalies.AsNoTracking(), request);

        var totalCount = await query.CountAsync(cancellationToken);
        var summary = await BuildSummaryAsync(query, cancellationToken);
        var items = await query
            .OrderByDescending(anomaly => anomaly.LastDetectedAtUtc)
            .ThenByDescending(anomaly => anomaly.Severity)
            .Take(take)
            .Select(anomaly => new StockAnomalyListItemDto(
                anomaly.Id,
                anomaly.Type.ToString(),
                anomaly.Severity.ToString(),
                anomaly.Status.ToString(),
                anomaly.WarehouseNo,
                anomaly.RelatedWarehouseNo,
                anomaly.WarehouseName,
                anomaly.RelatedWarehouseName,
                anomaly.ProductCode,
                anomaly.ProductName,
                anomaly.DocumentSerie,
                anomaly.DocumentOrderNo,
                anomaly.DocumentNo,
                anomaly.Quantity,
                anomaly.ExpectedQuantity,
                anomaly.ActualQuantity,
                anomaly.AverageQuantity,
                anomaly.OccurredAtUtc,
                anomaly.Message,
                anomaly.FirstDetectedAtUtc,
                anomaly.LastDetectedAtUtc))
            .ToListAsync(cancellationToken);

        return new StockAnomalyListResponse(totalCount, summary, items);
    }

    public async Task<StockAnomalyDetailDto> GetAsync(
        Guid id,
        int? allowedWarehouseNo,
        CancellationToken cancellationToken)
    {
        var query = authDbContext.StockAnomalies
            .AsNoTracking()
            .Include(anomaly => anomaly.Events)
            .Where(anomaly => anomaly.Id == id);

        if (allowedWarehouseNo.HasValue)
        {
            var warehouseNo = allowedWarehouseNo.Value;
            query = query.Where(anomaly =>
                anomaly.WarehouseNo == warehouseNo ||
                anomaly.RelatedWarehouseNo == warehouseNo);
        }

        var anomaly = await query.SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Stock anomaly was not found.");

        return ToDetail(anomaly);
    }

    public async Task<StockAnomalyScanResponse> ScanAsync(
        StockAnomalyScanRequest request,
        CancellationToken cancellationToken)
    {
        var startedAt = clock.UtcNow;
        var normalized = NormalizeScanRequest(request);

        logger.LogInformation(
            "Stock anomaly scan started. WarehouseNo={WarehouseNo}, StartDate={StartDate}, EndDate={EndDate}",
            normalized.WarehouseNo,
            normalized.StartDate,
            normalized.EndDateExclusive.AddDays(-1));

        var detected = new List<DetectedStockAnomaly>();
        var ruleResults = new List<StockAnomalyScanRuleResultDto>();

        await AddRuleAsync(detected, ruleResults, StockAnomalyType.NegativeStock, () => FindNegativeStockAsync(normalized, cancellationToken));
        await AddRuleAsync(detected, ruleResults, StockAnomalyType.DuplicateDocument, () => FindDuplicateDocumentsAsync(normalized, cancellationToken));
        await AddRuleAsync(detected, ruleResults, StockAnomalyType.ReceivingDifference, () => FindReceivingDifferencesAsync(normalized, cancellationToken));
        await AddRuleAsync(detected, ruleResults, StockAnomalyType.HighQuantity, () => FindHighQuantitiesAsync(normalized, cancellationToken));
        await AddRuleAsync(detected, ruleResults, StockAnomalyType.DormantStock, () => FindDormantStockAsync(normalized, cancellationToken));
        await AddRuleAsync(detected, ruleResults, StockAnomalyType.PendingInterWarehouseTransfer, () => FindPendingInterWarehouseTransfersAsync(normalized, cancellationToken));

        await UpsertDetectedAsync(detected, startedAt, cancellationToken);

        var finishedAt = clock.UtcNow;
        logger.LogInformation(
            "Stock anomaly scan finished. DetectedCount={DetectedCount}, ElapsedMs={ElapsedMs}",
            detected.Count,
            (finishedAt - startedAt).TotalMilliseconds);

        return new StockAnomalyScanResponse(startedAt, finishedAt, detected.Count, ruleResults);
    }

    public async Task<StockAnomalyDetailDto> ChangeStatusAsync(
        ChangeStockAnomalyStatusRequest request,
        CancellationToken cancellationToken)
    {
        var query = authDbContext.StockAnomalies
            .Include(anomaly => anomaly.Events)
            .Where(anomaly => anomaly.Id == request.Id);

        if (request.AllowedWarehouseNo.HasValue)
        {
            var warehouseNo = request.AllowedWarehouseNo.Value;
            query = query.Where(anomaly =>
                anomaly.WarehouseNo == warehouseNo ||
                anomaly.RelatedWarehouseNo == warehouseNo);
        }

        var anomaly = await query.SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Stock anomaly was not found.");

        anomaly.ChangeStatus(request.Status, request.Note, request.ChangedByUserId, clock.UtcNow);
        await authDbContext.SaveChangesAsync(cancellationToken);

        return ToDetail(anomaly);
    }

    private static IQueryable<StockAnomaly> ApplyListFilters(
        IQueryable<StockAnomaly> query,
        StockAnomalyListRequest request)
    {
        if (request.WarehouseNo.HasValue)
        {
            var warehouseNo = request.WarehouseNo.Value;
            query = query.Where(anomaly =>
                anomaly.WarehouseNo == warehouseNo ||
                anomaly.RelatedWarehouseNo == warehouseNo);
        }

        if (request.Type.HasValue)
        {
            query = query.Where(anomaly => anomaly.Type == request.Type.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(anomaly => anomaly.Status == request.Status.Value);
        }

        if (request.Severity.HasValue)
        {
            query = query.Where(anomaly => anomaly.Severity == request.Severity.Value);
        }

        if (request.StartDate.HasValue)
        {
            var startDate = ToUtcDate(request.StartDate.Value.Date);
            query = query.Where(anomaly => anomaly.LastDetectedAtUtc >= startDate);
        }

        if (request.EndDate.HasValue)
        {
            var endDateExclusive = ToUtcDate(request.EndDate.Value.Date.AddDays(1));
            query = query.Where(anomaly => anomaly.LastDetectedAtUtc < endDateExclusive);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(anomaly =>
                (anomaly.ProductCode != null && anomaly.ProductCode.Contains(search)) ||
                (anomaly.ProductName != null && anomaly.ProductName.Contains(search)) ||
                (anomaly.DocumentSerie != null && anomaly.DocumentSerie.Contains(search)) ||
                (anomaly.DocumentNo != null && anomaly.DocumentNo.Contains(search)) ||
                anomaly.Message.Contains(search));
        }

        return query;
    }

    private static async Task<StockAnomalySummaryDto> BuildSummaryAsync(
        IQueryable<StockAnomaly> query,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(_ => 1)
            .Select(group => new
            {
                OpenCount = group.Count(anomaly => anomaly.Status == StockAnomalyStatus.Open),
                AcknowledgedCount = group.Count(anomaly => anomaly.Status == StockAnomalyStatus.Acknowledged),
                ResolvedCount = group.Count(anomaly => anomaly.Status == StockAnomalyStatus.Resolved),
                IgnoredCount = group.Count(anomaly => anomaly.Status == StockAnomalyStatus.Ignored),
                CriticalCount = group.Count(anomaly => anomaly.Severity == StockAnomalySeverity.Critical),
                HighCount = group.Count(anomaly => anomaly.Severity == StockAnomalySeverity.High)
            })
            .SingleOrDefaultAsync(cancellationToken);

        return rows is null
            ? new StockAnomalySummaryDto(0, 0, 0, 0, 0, 0)
            : new StockAnomalySummaryDto(
                rows.OpenCount,
                rows.AcknowledgedCount,
                rows.ResolvedCount,
                rows.IgnoredCount,
                rows.CriticalCount,
                rows.HighCount);
    }

    private async Task AddRuleAsync(
        List<DetectedStockAnomaly> detected,
        List<StockAnomalyScanRuleResultDto> ruleResults,
        StockAnomalyType type,
        Func<Task<IReadOnlyCollection<DetectedStockAnomaly>>> find)
    {
        try
        {
            var rows = await find();
            detected.AddRange(rows);
            ruleResults.Add(new StockAnomalyScanRuleResultDto(type.ToString(), rows.Count));
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Stock anomaly rule failed. Type={Type}",
                type);
            ruleResults.Add(new StockAnomalyScanRuleResultDto(type.ToString(), 0, exception.Message));
        }
    }

    private async Task UpsertDetectedAsync(
        IReadOnlyCollection<DetectedStockAnomaly> detected,
        DateTime detectedAtUtc,
        CancellationToken cancellationToken)
    {
        if (detected.Count == 0)
        {
            return;
        }

        var sourceKeys = detected.Select(item => item.SourceKey).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var existing = await authDbContext.StockAnomalies
            .Include(anomaly => anomaly.Events)
            .Where(anomaly => sourceKeys.Contains(anomaly.SourceKey))
            .ToDictionaryAsync(anomaly => anomaly.SourceKey, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var item in detected)
        {
            if (!existing.TryGetValue(item.SourceKey, out var anomaly))
            {
                anomaly = new StockAnomaly(
                    Guid.NewGuid(),
                    item.SourceKey,
                    item.Type,
                    item.Severity,
                    item.WarehouseNo,
                    detectedAtUtc);
                authDbContext.StockAnomalies.Add(anomaly);
                existing[item.SourceKey] = anomaly;
            }

            anomaly.Detect(
                item.Severity,
                item.RelatedWarehouseNo,
                item.WarehouseName,
                item.RelatedWarehouseName,
                item.ProductCode,
                item.ProductName,
                item.DocumentSerie,
                item.DocumentOrderNo,
                item.DocumentNo,
                item.MovementGuid,
                item.Quantity,
                item.ExpectedQuantity,
                item.ActualQuantity,
                item.AverageQuantity,
                item.OccurredAtUtc,
                item.Message,
                item.Evidence,
                detectedAtUtc);
        }

        await authDbContext.SaveChangesAsync(cancellationToken);
    }

    private Task<IReadOnlyCollection<DetectedStockAnomaly>> FindNegativeStockAsync(
        NormalizedScanRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (@take)
                summary.sho_Depo AS WarehouseNo,
                warehouse.dep_adi AS WarehouseName,
                summary.sho_StokKodu AS ProductCode,
                stock.sto_isim AS ProductName,
                SUM(ISNULL(summary.sho_GirisNormal, 0) + ISNULL(summary.sho_CikisIade, 0)
                  - ISNULL(summary.sho_CikisNormal, 0) - ISNULL(summary.sho_GirisIade, 0)) AS Quantity
            FROM dbo.STOK_HAREKETLERI_OZET AS summary WITH (NOLOCK)
            LEFT JOIN dbo.DEPOLAR AS warehouse WITH (NOLOCK) ON warehouse.dep_no = summary.sho_Depo
            LEFT JOIN dbo.STOKLAR AS stock WITH (NOLOCK) ON stock.sto_kod = summary.sho_StokKodu
            WHERE summary.sho_Depo IS NOT NULL
              AND summary.sho_StokKodu IS NOT NULL
              AND (@warehouseNo IS NULL OR summary.sho_Depo = @warehouseNo)
            GROUP BY summary.sho_Depo, warehouse.dep_adi, summary.sho_StokKodu, stock.sto_isim
            HAVING SUM(ISNULL(summary.sho_GirisNormal, 0) + ISNULL(summary.sho_CikisIade, 0)
                     - ISNULL(summary.sho_CikisNormal, 0) - ISNULL(summary.sho_GirisIade, 0)) < -0.000001
            ORDER BY Quantity ASC
            """;

        return ReadDetectedAsync(sql, request, reader =>
        {
            var warehouseNo = ReadInt(reader, "WarehouseNo") ?? 0;
            var productCode = ReadString(reader, "ProductCode") ?? string.Empty;
            var quantity = ReadDouble(reader, "Quantity") ?? 0d;
            return new DetectedStockAnomaly(
                $"negative-stock:{warehouseNo}:{productCode}".ToUpperInvariant(),
                StockAnomalyType.NegativeStock,
                quantity < -10d ? StockAnomalySeverity.Critical : StockAnomalySeverity.High,
                warehouseNo,
                null,
                ReadString(reader, "WarehouseName"),
                null,
                productCode,
                ReadString(reader, "ProductName"),
                null,
                null,
                null,
                null,
                quantity,
                0d,
                quantity,
                null,
                null,
                $"Depo stok bakiyesi eksiye dusmus. Mevcut stok: {quantity:0.###}.",
                $"Depo={warehouseNo}; Stok={productCode}; Bakiye={quantity:0.######}");
        }, cancellationToken);
    }

    private Task<IReadOnlyCollection<DetectedStockAnomaly>> FindDuplicateDocumentsAsync(
        NormalizedScanRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (@take)
                movement.sth_evraktip AS DocumentType,
                movement.sth_tip AS MovementType,
                movement.sth_cins AS MovementKind,
                movement.sth_normal_iade AS NormalReturn,
                movement.sth_evrakno_seri AS DocumentSerie,
                movement.sth_evrakno_sira AS DocumentOrderNo,
                movement.sth_belge_no AS DocumentNo,
                movement.sth_stok_kod AS ProductCode,
                stock.sto_isim AS ProductName,
                ISNULL(movement.sth_cikis_depo_no, movement.sth_giris_depo_no) AS WarehouseNo,
                movement.sth_giris_depo_no AS RelatedWarehouseNo,
                warehouse.dep_adi AS WarehouseName,
                relatedWarehouse.dep_adi AS RelatedWarehouseName,
                movement.sth_miktar AS Quantity,
                MIN(movement.sth_tarih) AS FirstMovementDate,
                COUNT_BIG(*) AS DuplicateCount
            FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
            LEFT JOIN dbo.STOKLAR AS stock WITH (NOLOCK) ON stock.sto_kod = movement.sth_stok_kod
            LEFT JOIN dbo.DEPOLAR AS warehouse WITH (NOLOCK) ON warehouse.dep_no = ISNULL(movement.sth_cikis_depo_no, movement.sth_giris_depo_no)
            LEFT JOIN dbo.DEPOLAR AS relatedWarehouse WITH (NOLOCK) ON relatedWarehouse.dep_no = movement.sth_giris_depo_no
            WHERE movement.sth_iptal <> 1
              AND movement.sth_tarih >= @startDate
              AND movement.sth_tarih < @endDateExclusive
              AND movement.sth_evrakno_seri IS NOT NULL
              AND movement.sth_evrakno_sira IS NOT NULL
              AND (@warehouseNo IS NULL OR movement.sth_cikis_depo_no = @warehouseNo OR movement.sth_giris_depo_no = @warehouseNo)
            GROUP BY movement.sth_evraktip, movement.sth_tip, movement.sth_cins, movement.sth_normal_iade,
                movement.sth_evrakno_seri, movement.sth_evrakno_sira, movement.sth_belge_no, movement.sth_stok_kod,
                stock.sto_isim, ISNULL(movement.sth_cikis_depo_no, movement.sth_giris_depo_no), movement.sth_giris_depo_no,
                warehouse.dep_adi, relatedWarehouse.dep_adi, movement.sth_miktar
            HAVING COUNT_BIG(*) > 1
            ORDER BY DuplicateCount DESC, FirstMovementDate DESC
            """;

        return ReadDetectedAsync(sql, request, reader =>
        {
            var warehouseNo = ReadInt(reader, "WarehouseNo") ?? 0;
            var productCode = ReadString(reader, "ProductCode") ?? string.Empty;
            var serie = ReadString(reader, "DocumentSerie") ?? string.Empty;
            var orderNo = ReadInt(reader, "DocumentOrderNo");
            var quantity = ReadDouble(reader, "Quantity");
            var duplicateCount = ReadLong(reader, "DuplicateCount") ?? 0L;
            var docType = ReadInt(reader, "DocumentType") ?? 0;
            var movementType = ReadInt(reader, "MovementType") ?? 0;
            var movementKind = ReadInt(reader, "MovementKind") ?? 0;
            var normalReturn = ReadInt(reader, "NormalReturn") ?? 0;

            return new DetectedStockAnomaly(
                $"duplicate-document:{docType}:{movementType}:{movementKind}:{normalReturn}:{warehouseNo}:{serie}:{orderNo}:{productCode}:{quantity:0.######}".ToUpperInvariant(),
                StockAnomalyType.DuplicateDocument,
                StockAnomalySeverity.High,
                warehouseNo,
                ReadInt(reader, "RelatedWarehouseNo"),
                ReadString(reader, "WarehouseName"),
                ReadString(reader, "RelatedWarehouseName"),
                productCode,
                ReadString(reader, "ProductName"),
                serie,
                orderNo,
                ReadString(reader, "DocumentNo"),
                null,
                quantity,
                1d,
                duplicateCount,
                null,
                ReadDateTime(reader, "FirstMovementDate"),
                $"Ayni belge/stok/miktar kombinasyonu {duplicateCount} kez gorunuyor.",
                $"EvrakTip={docType}; Tip={movementType}; Cins={movementKind}; Iade={normalReturn}; Depo={warehouseNo}; Seri={serie}; Sira={orderNo}; Stok={productCode}; Miktar={quantity:0.######}");
        }, cancellationToken);
    }

    private Task<IReadOnlyCollection<DetectedStockAnomaly>> FindReceivingDifferencesAsync(
        NormalizedScanRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (@take)
                movement.sth_Guid AS MovementGuid,
                movement.sth_tarih AS MovementDate,
                movement.sth_belge_no AS DocumentNo,
                movement.sth_evrakno_seri AS DocumentSerie,
                movement.sth_evrakno_sira AS DocumentOrderNo,
                movement.sth_stok_kod AS ProductCode,
                stock.sto_isim AS ProductName,
                movement.sth_cikis_depo_no AS WarehouseNo,
                sourceWarehouse.dep_adi AS WarehouseName,
                movement.sth_giris_depo_no AS RelatedWarehouseNo,
                targetWarehouse.dep_adi AS RelatedWarehouseName,
                movement.sth_miktar AS Quantity,
                movement.sth_FormulMiktar AS ActualQuantity
            FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
            LEFT JOIN dbo.STOKLAR AS stock WITH (NOLOCK) ON stock.sto_kod = movement.sth_stok_kod
            LEFT JOIN dbo.DEPOLAR AS sourceWarehouse WITH (NOLOCK) ON sourceWarehouse.dep_no = movement.sth_cikis_depo_no
            LEFT JOIN dbo.DEPOLAR AS targetWarehouse WITH (NOLOCK) ON targetWarehouse.dep_no = movement.sth_giris_depo_no
            WHERE movement.sth_iptal <> 1
              AND movement.sth_tarih >= @startDate
              AND movement.sth_tarih < @endDateExclusive
              AND movement.sth_evraktip = 17
              AND movement.sth_nakliyedurumu = 1
              AND movement.sth_FormulMiktar IS NOT NULL
              AND ABS(ISNULL(movement.sth_FormulMiktar, 0) - ISNULL(movement.sth_miktar, 0)) > 0.000001
              AND (@warehouseNo IS NULL OR movement.sth_cikis_depo_no = @warehouseNo OR movement.sth_giris_depo_no = @warehouseNo)
            ORDER BY movement.sth_tarih DESC
            """;

        return ReadDetectedAsync(sql, request, reader =>
        {
            var warehouseNo = ReadInt(reader, "WarehouseNo") ?? 0;
            var relatedWarehouseNo = ReadInt(reader, "RelatedWarehouseNo");
            var productCode = ReadString(reader, "ProductCode") ?? string.Empty;
            var serie = ReadString(reader, "DocumentSerie") ?? string.Empty;
            var orderNo = ReadInt(reader, "DocumentOrderNo");
            var expected = ReadDouble(reader, "Quantity") ?? 0d;
            var actual = ReadDouble(reader, "ActualQuantity") ?? 0d;
            var difference = actual - expected;

            return new DetectedStockAnomaly(
                $"receiving-difference:{ReadGuid(reader, "MovementGuid")}".ToUpperInvariant(),
                StockAnomalyType.ReceivingDifference,
                Math.Abs(difference) >= 10d ? StockAnomalySeverity.High : StockAnomalySeverity.Medium,
                warehouseNo,
                relatedWarehouseNo,
                ReadString(reader, "WarehouseName"),
                ReadString(reader, "RelatedWarehouseName"),
                productCode,
                ReadString(reader, "ProductName"),
                serie,
                orderNo,
                ReadString(reader, "DocumentNo"),
                ReadGuid(reader, "MovementGuid"),
                difference,
                expected,
                actual,
                null,
                ReadDateTime(reader, "MovementDate"),
                $"Sevk miktari ile kabul miktari farkli. Sevk: {expected:0.###}, kabul: {actual:0.###}.",
                $"KaynakDepo={warehouseNo}; HedefDepo={relatedWarehouseNo}; Seri={serie}; Sira={orderNo}; Stok={productCode}; Sevk={expected:0.######}; Kabul={actual:0.######}");
        }, cancellationToken);
    }

    private Task<IReadOnlyCollection<DetectedStockAnomaly>> FindHighQuantitiesAsync(
        NormalizedScanRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH MovementRows AS (
                SELECT
                    movement.sth_Guid,
                    movement.sth_tarih,
                    movement.sth_evrakno_seri,
                    movement.sth_evrakno_sira,
                    movement.sth_belge_no,
                    movement.sth_stok_kod,
                    stock.sto_isim,
                    CASE WHEN movement.sth_tip = 0 THEN movement.sth_giris_depo_no ELSE movement.sth_cikis_depo_no END AS WarehouseNo,
                    warehouse.dep_adi AS WarehouseName,
                    ISNULL(movement.sth_miktar, 0) AS Quantity
                FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
                LEFT JOIN dbo.STOKLAR AS stock WITH (NOLOCK) ON stock.sto_kod = movement.sth_stok_kod
                LEFT JOIN dbo.DEPOLAR AS warehouse WITH (NOLOCK)
                    ON warehouse.dep_no = CASE WHEN movement.sth_tip = 0 THEN movement.sth_giris_depo_no ELSE movement.sth_cikis_depo_no END
                WHERE movement.sth_iptal <> 1
                  AND movement.sth_tarih >= @lookbackStartDate
                  AND movement.sth_tarih < @endDateExclusive
                  AND movement.sth_stok_kod IS NOT NULL
                  AND ISNULL(movement.sth_miktar, 0) > 0
                  AND (@warehouseNo IS NULL OR movement.sth_cikis_depo_no = @warehouseNo OR movement.sth_giris_depo_no = @warehouseNo)
            ),
            Averages AS (
                SELECT WarehouseNo, sth_stok_kod AS ProductCode, AVG(Quantity) AS AverageQuantity
                FROM MovementRows
                WHERE WarehouseNo IS NOT NULL
                GROUP BY WarehouseNo, sth_stok_kod
            )
            SELECT TOP (@take)
                row.sth_Guid AS MovementGuid,
                row.sth_tarih AS MovementDate,
                row.sth_evrakno_seri AS DocumentSerie,
                row.sth_evrakno_sira AS DocumentOrderNo,
                row.sth_belge_no AS DocumentNo,
                row.sth_stok_kod AS ProductCode,
                row.sto_isim AS ProductName,
                row.WarehouseNo,
                row.WarehouseName,
                row.Quantity,
                average.AverageQuantity
            FROM MovementRows AS row
            INNER JOIN Averages AS average ON average.WarehouseNo = row.WarehouseNo AND average.ProductCode = row.sth_stok_kod
            WHERE row.sth_tarih >= @startDate
              AND row.sth_tarih < @endDateExclusive
              AND row.Quantity >= @highQuantityMinimum
              AND row.Quantity > average.AverageQuantity * @highQuantityMultiplier
              AND average.AverageQuantity > 0
            ORDER BY row.Quantity DESC
            """;

        return ReadDetectedAsync(sql, request, reader =>
        {
            var warehouseNo = ReadInt(reader, "WarehouseNo") ?? 0;
            var productCode = ReadString(reader, "ProductCode") ?? string.Empty;
            var quantity = ReadDouble(reader, "Quantity") ?? 0d;
            var average = ReadDouble(reader, "AverageQuantity") ?? 0d;
            var serie = ReadString(reader, "DocumentSerie") ?? string.Empty;
            var orderNo = ReadInt(reader, "DocumentOrderNo");

            return new DetectedStockAnomaly(
                $"high-quantity:{ReadGuid(reader, "MovementGuid")}".ToUpperInvariant(),
                StockAnomalyType.HighQuantity,
                quantity >= average * 10d ? StockAnomalySeverity.Critical : StockAnomalySeverity.High,
                warehouseNo,
                null,
                ReadString(reader, "WarehouseName"),
                null,
                productCode,
                ReadString(reader, "ProductName"),
                serie,
                orderNo,
                ReadString(reader, "DocumentNo"),
                ReadGuid(reader, "MovementGuid"),
                quantity,
                average * request.HighQuantityMultiplier,
                quantity,
                average,
                ReadDateTime(reader, "MovementDate"),
                $"Hareket miktari son {request.HighQuantityLookbackDays} gun ortalamasinin uzerinde. Miktar: {quantity:0.###}, ortalama: {average:0.###}.",
                $"Depo={warehouseNo}; Seri={serie}; Sira={orderNo}; Stok={productCode}; Miktar={quantity:0.######}; Ortalama={average:0.######}; Katsayi={request.HighQuantityMultiplier:0.##}");
        }, cancellationToken);
    }

    private Task<IReadOnlyCollection<DetectedStockAnomaly>> FindDormantStockAsync(
        NormalizedScanRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH Balances AS (
                SELECT
                    summary.sho_Depo AS WarehouseNo,
                    summary.sho_StokKodu AS ProductCode,
                    SUM(ISNULL(summary.sho_GirisNormal, 0) + ISNULL(summary.sho_CikisIade, 0)
                      - ISNULL(summary.sho_CikisNormal, 0) - ISNULL(summary.sho_GirisIade, 0)) AS Quantity
                FROM dbo.STOK_HAREKETLERI_OZET AS summary WITH (NOLOCK)
                WHERE summary.sho_Depo IS NOT NULL
                  AND summary.sho_StokKodu IS NOT NULL
                  AND (@warehouseNo IS NULL OR summary.sho_Depo = @warehouseNo)
                GROUP BY summary.sho_Depo, summary.sho_StokKodu
                HAVING SUM(ISNULL(summary.sho_GirisNormal, 0) + ISNULL(summary.sho_CikisIade, 0)
                         - ISNULL(summary.sho_CikisNormal, 0) - ISNULL(summary.sho_GirisIade, 0)) > 0.000001
            )
            SELECT TOP (@take)
                balances.WarehouseNo,
                warehouse.dep_adi AS WarehouseName,
                balances.ProductCode,
                stock.sto_isim AS ProductName,
                balances.Quantity,
                lastMovements.LastMovementDate
            FROM Balances AS balances
            OUTER APPLY (
                SELECT TOP (1) movement.sth_tarih AS LastMovementDate
                FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
                WHERE movement.sth_iptal <> 1
                  AND movement.sth_stok_kod = balances.ProductCode
                  AND (
                      (movement.sth_tip = 0 AND movement.sth_giris_depo_no = balances.WarehouseNo) OR
                      (movement.sth_tip <> 0 AND movement.sth_cikis_depo_no = balances.WarehouseNo)
                  )
                ORDER BY movement.sth_tarih DESC
            ) AS lastMovements
            LEFT JOIN dbo.DEPOLAR AS warehouse WITH (NOLOCK) ON warehouse.dep_no = balances.WarehouseNo
            LEFT JOIN dbo.STOKLAR AS stock WITH (NOLOCK) ON stock.sto_kod = balances.ProductCode
            WHERE lastMovements.LastMovementDate IS NULL OR lastMovements.LastMovementDate < @dormantCutoffDate
            ORDER BY balances.Quantity DESC
            """;

        return ReadDetectedAsync(sql, request, reader =>
        {
            var warehouseNo = ReadInt(reader, "WarehouseNo") ?? 0;
            var productCode = ReadString(reader, "ProductCode") ?? string.Empty;
            var quantity = ReadDouble(reader, "Quantity") ?? 0d;
            var lastMovementDate = ReadDateTime(reader, "LastMovementDate");

            return new DetectedStockAnomaly(
                $"dormant-stock:{warehouseNo}:{productCode}".ToUpperInvariant(),
                StockAnomalyType.DormantStock,
                StockAnomalySeverity.Medium,
                warehouseNo,
                null,
                ReadString(reader, "WarehouseName"),
                null,
                productCode,
                ReadString(reader, "ProductName"),
                null,
                null,
                null,
                null,
                quantity,
                null,
                quantity,
                null,
                lastMovementDate,
                $"Depoda stok var ama {request.DormantDays} gundur hareket gorunmuyor. Mevcut stok: {quantity:0.###}.",
                $"Depo={warehouseNo}; Stok={productCode}; Bakiye={quantity:0.######}; SonHareket={lastMovementDate:O}");
        }, cancellationToken);
    }

    private Task<IReadOnlyCollection<DetectedStockAnomaly>> FindPendingInterWarehouseTransfersAsync(
        NormalizedScanRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (@take)
                movement.sth_Guid AS MovementGuid,
                movement.sth_tarih AS MovementDate,
                movement.sth_evrakno_seri AS DocumentSerie,
                movement.sth_evrakno_sira AS DocumentOrderNo,
                movement.sth_belge_no AS DocumentNo,
                movement.sth_stok_kod AS ProductCode,
                stock.sto_isim AS ProductName,
                movement.sth_cikis_depo_no AS WarehouseNo,
                sourceWarehouse.dep_adi AS WarehouseName,
                ISNULL(movement.sth_nakliyedeposu, movement.sth_giris_depo_no) AS RelatedWarehouseNo,
                targetWarehouse.dep_adi AS RelatedWarehouseName,
                movement.sth_miktar AS Quantity,
                movement.sth_nakliyedurumu AS ShippingState
            FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
            LEFT JOIN dbo.STOKLAR AS stock WITH (NOLOCK) ON stock.sto_kod = movement.sth_stok_kod
            LEFT JOIN dbo.DEPOLAR AS sourceWarehouse WITH (NOLOCK) ON sourceWarehouse.dep_no = movement.sth_cikis_depo_no
            LEFT JOIN dbo.DEPOLAR AS targetWarehouse WITH (NOLOCK)
                ON targetWarehouse.dep_no = ISNULL(movement.sth_nakliyedeposu, movement.sth_giris_depo_no)
            WHERE movement.sth_iptal <> 1
              AND movement.sth_evraktip = 17
              AND ISNULL(movement.sth_nakliyedurumu, 0) <> 1
              AND movement.sth_tarih >= @startDate
              AND movement.sth_tarih < @endDateExclusive
              AND movement.sth_tarih < @pendingTransferCutoffDate
              AND (@warehouseNo IS NULL OR movement.sth_cikis_depo_no = @warehouseNo OR movement.sth_giris_depo_no = @warehouseNo OR movement.sth_nakliyedeposu = @warehouseNo)
            ORDER BY movement.sth_tarih ASC
            """;

        return ReadDetectedAsync(sql, request, reader =>
        {
            var warehouseNo = ReadInt(reader, "WarehouseNo") ?? 0;
            var relatedWarehouseNo = ReadInt(reader, "RelatedWarehouseNo");
            var productCode = ReadString(reader, "ProductCode") ?? string.Empty;
            var serie = ReadString(reader, "DocumentSerie") ?? string.Empty;
            var orderNo = ReadInt(reader, "DocumentOrderNo");
            var movementDate = ReadDateTime(reader, "MovementDate");
            var quantity = ReadDouble(reader, "Quantity") ?? 0d;

            return new DetectedStockAnomaly(
                $"pending-transfer:{ReadGuid(reader, "MovementGuid")}".ToUpperInvariant(),
                StockAnomalyType.PendingInterWarehouseTransfer,
                StockAnomalySeverity.High,
                warehouseNo,
                relatedWarehouseNo,
                ReadString(reader, "WarehouseName"),
                ReadString(reader, "RelatedWarehouseName"),
                productCode,
                ReadString(reader, "ProductName"),
                serie,
                orderNo,
                ReadString(reader, "DocumentNo"),
                ReadGuid(reader, "MovementGuid"),
                quantity,
                null,
                quantity,
                null,
                movementDate,
                $"Depolar arasi sevk {request.PendingTransferHours} saati asti ama teslim alinmamis gorunuyor.",
                $"KaynakDepo={warehouseNo}; HedefDepo={relatedWarehouseNo}; Seri={serie}; Sira={orderNo}; Stok={productCode}; Miktar={quantity:0.######}; Tarih={movementDate:O}; NakliyeDurumu={ReadInt(reader, "ShippingState")}");
        }, cancellationToken);
    }

    private async Task<IReadOnlyCollection<DetectedStockAnomaly>> ReadDetectedAsync(
        string sql,
        NormalizedScanRequest request,
        Func<DbDataReader, DetectedStockAnomaly> map,
        CancellationToken cancellationToken)
    {
        var connection = mikroDbContext.Database.GetDbConnection();
        var shouldClose = connection.State == ConnectionState.Closed;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 180;
            AddParameter(command, "@take", request.TakePerRule);
            AddParameter(command, "@warehouseNo", request.WarehouseNo);
            AddParameter(command, "@startDate", request.StartDate);
            AddParameter(command, "@endDateExclusive", request.EndDateExclusive);
            AddParameter(command, "@lookbackStartDate", request.LookbackStartDate);
            AddParameter(command, "@dormantCutoffDate", request.DormantCutoffDate);
            AddParameter(command, "@pendingTransferCutoffDate", request.PendingTransferCutoffDate);
            AddParameter(command, "@highQuantityMinimum", request.HighQuantityMinimum);
            AddParameter(command, "@highQuantityMultiplier", request.HighQuantityMultiplier);

            var rows = new List<DetectedStockAnomaly>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var anomaly = map(reader);
                if (anomaly.WarehouseNo > 0)
                {
                    rows.Add(anomaly);
                }
            }

            return rows;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static NormalizedScanRequest NormalizeScanRequest(StockAnomalyScanRequest request)
    {
        if (request.WarehouseNo is <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        var endDate = (request.EndDate ?? DateTime.Today).Date;
        var startDate = (request.StartDate ?? endDate.AddDays(-7)).Date;
        if (endDate < startDate)
        {
            throw new ArgumentException("End date can not be earlier than start date.");
        }

        var dormantDays = Math.Clamp(request.DormantDays, 1, 3650);
        var pendingTransferHours = Math.Clamp(request.PendingTransferHours, 1, 24 * 30);
        var lookbackDays = Math.Clamp(request.HighQuantityLookbackDays, 1, 365);
        var multiplier = request.HighQuantityMultiplier <= 1d ? 3d : request.HighQuantityMultiplier;
        var minimum = request.HighQuantityMinimum < 0d ? 0d : request.HighQuantityMinimum;
        var takePerRule = Math.Clamp(request.TakePerRule, 1, 1000);

        return new NormalizedScanRequest(
            request.WarehouseNo,
            ToUtcDate(startDate),
            ToUtcDate(endDate.AddDays(1)),
            ToUtcDate(endDate.AddDays(1).AddDays(-lookbackDays)),
            ToUtcDate(DateTime.Today.AddDays(-dormantDays)),
            DateTime.UtcNow.AddHours(-pendingTransferHours),
            dormantDays,
            pendingTransferHours,
            lookbackDays,
            multiplier,
            minimum,
            takePerRule);
    }

    private static StockAnomalyDetailDto ToDetail(StockAnomaly anomaly) =>
        new(
            anomaly.Id,
            anomaly.SourceKey,
            anomaly.Type.ToString(),
            anomaly.Severity.ToString(),
            anomaly.Status.ToString(),
            anomaly.WarehouseNo,
            anomaly.RelatedWarehouseNo,
            anomaly.WarehouseName,
            anomaly.RelatedWarehouseName,
            anomaly.ProductCode,
            anomaly.ProductName,
            anomaly.DocumentSerie,
            anomaly.DocumentOrderNo,
            anomaly.DocumentNo,
            anomaly.MovementGuid,
            anomaly.Quantity,
            anomaly.ExpectedQuantity,
            anomaly.ActualQuantity,
            anomaly.AverageQuantity,
            anomaly.OccurredAtUtc,
            anomaly.Message,
            anomaly.Evidence,
            anomaly.LastChangedByUserId,
            anomaly.FirstDetectedAtUtc,
            anomaly.LastDetectedAtUtc,
            anomaly.ResolvedAtUtc,
            anomaly.Events
                .OrderBy(anomalyEvent => anomalyEvent.OccurredAtUtc)
                .Select(anomalyEvent => new StockAnomalyEventDto(
                    anomalyEvent.Id,
                    anomalyEvent.EventType.ToString(),
                    anomalyEvent.Status.ToString(),
                    anomalyEvent.Message,
                    anomalyEvent.ChangedByUserId,
                    anomalyEvent.OccurredAtUtc))
                .ToArray());

    private static DateTime ToUtcDate(DateTime value) =>
        DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);

    private static string? ReadString(DbDataReader reader, string name) =>
        reader[name] == DBNull.Value ? null : Convert.ToString(reader[name]);

    private static int? ReadInt(DbDataReader reader, string name) =>
        reader[name] == DBNull.Value ? null : Convert.ToInt32(reader[name]);

    private static long? ReadLong(DbDataReader reader, string name) =>
        reader[name] == DBNull.Value ? null : Convert.ToInt64(reader[name]);

    private static double? ReadDouble(DbDataReader reader, string name) =>
        reader[name] == DBNull.Value ? null : Convert.ToDouble(reader[name]);

    private static Guid? ReadGuid(DbDataReader reader, string name) =>
        reader[name] == DBNull.Value ? null : (Guid)reader[name];

    private static DateTime? ReadDateTime(DbDataReader reader, string name) =>
        reader[name] == DBNull.Value ? null : DateTime.SpecifyKind(Convert.ToDateTime(reader[name]), DateTimeKind.Utc);

    private sealed record NormalizedScanRequest(
        int? WarehouseNo,
        DateTime StartDate,
        DateTime EndDateExclusive,
        DateTime LookbackStartDate,
        DateTime DormantCutoffDate,
        DateTime PendingTransferCutoffDate,
        int DormantDays,
        int PendingTransferHours,
        int HighQuantityLookbackDays,
        double HighQuantityMultiplier,
        double HighQuantityMinimum,
        int TakePerRule);

    private sealed record DetectedStockAnomaly(
        string SourceKey,
        StockAnomalyType Type,
        StockAnomalySeverity Severity,
        int WarehouseNo,
        int? RelatedWarehouseNo,
        string? WarehouseName,
        string? RelatedWarehouseName,
        string? ProductCode,
        string? ProductName,
        string? DocumentSerie,
        int? DocumentOrderNo,
        string? DocumentNo,
        Guid? MovementGuid,
        double? Quantity,
        double? ExpectedQuantity,
        double? ActualQuantity,
        double? AverageQuantity,
        DateTime? OccurredAtUtc,
        string Message,
        string? Evidence);
}

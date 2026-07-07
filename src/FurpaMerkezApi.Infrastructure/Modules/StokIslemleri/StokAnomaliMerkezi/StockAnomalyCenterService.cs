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
    private const int UpsertRetryCount = 3;
    private static readonly SemaphoreSlim UpsertLock = new(1, 1);

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
                anomaly.ProductManagerCode,
                anomaly.ProductManagerName,
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

        var uniqueDetected = await EnrichProductManagersAsync(
            DeduplicateDetected(detected),
            cancellationToken);
        await UpsertDetectedWithRetryAsync(uniqueDetected, startedAt, cancellationToken);

        var finishedAt = clock.UtcNow;
        logger.LogInformation(
            "Stock anomaly scan finished. DetectedCount={DetectedCount}, ElapsedMs={ElapsedMs}",
            uniqueDetected.Count,
            (finishedAt - startedAt).TotalMilliseconds);

        return new StockAnomalyScanResponse(startedAt, finishedAt, uniqueDetected.Count, ruleResults);
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

    public async Task<IReadOnlyCollection<StockAnomalyProductManagerDto>> ListProductManagersAsync(
        StockAnomalyProductManagerListRequest request,
        CancellationToken cancellationToken)
    {
        var query = authDbContext.StockAnomalies
            .AsNoTracking()
            .Where(anomaly => anomaly.ProductCode != null && anomaly.ProductCode != string.Empty);

        if (request.WarehouseNo.HasValue)
        {
            var warehouseNo = request.WarehouseNo.Value;
            query = query.Where(anomaly =>
                anomaly.WarehouseNo == warehouseNo ||
                anomaly.RelatedWarehouseNo == warehouseNo);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(anomaly => anomaly.Status == request.Status.Value);
        }

        var rows = await query
            .GroupBy(anomaly => anomaly.ProductManagerCode ?? string.Empty)
            .Select(group => new
            {
                Code = group.Key,
                Name = group.Max(anomaly => anomaly.ProductManagerName),
                AnomalyCount = group.Count()
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new StockAnomalyProductManagerDto(
                row.Code,
                row.Code == string.Empty
                    ? "ATANMAMIS"
                    : string.IsNullOrWhiteSpace(row.Name) ? row.Code : row.Name,
                row.AnomalyCount,
                row.Code != string.Empty))
            .OrderByDescending(item => item.IsAssigned)
            .ThenBy(item => item.Name)
            .ToArray();
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

        if (!string.IsNullOrWhiteSpace(request.ProductManagerCode))
        {
            var productManagerCode = request.ProductManagerCode.Trim();
            query = query.Where(anomaly => anomaly.ProductManagerCode == productManagerCode);
        }

        if (request.HasProductManager.HasValue)
        {
            query = request.HasProductManager.Value
                ? query.Where(anomaly => anomaly.ProductManagerCode != null && anomaly.ProductManagerCode != string.Empty)
                : query.Where(anomaly =>
                    anomaly.ProductCode != null &&
                    anomaly.ProductCode != string.Empty &&
                    (anomaly.ProductManagerCode == null || anomaly.ProductManagerCode == string.Empty));
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

    private async Task<IReadOnlyCollection<DetectedStockAnomaly>> EnrichProductManagersAsync(
        IReadOnlyCollection<DetectedStockAnomaly> detected,
        CancellationToken cancellationToken)
    {
        var candidates = detected
            .Where(item => !string.IsNullOrWhiteSpace(item.ProductCode))
            .Select(item => new
            {
                item.WarehouseNo,
                ProductCode = item.ProductCode!.Trim()
            })
            .Distinct()
            .ToArray();

        if (candidates.Length == 0)
        {
            return detected;
        }

        var managerCodesByProduct = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var warehouseGroup in candidates.GroupBy(candidate => candidate.WarehouseNo))
        {
            foreach (var productCodeChunk in warehouseGroup
                         .Select(candidate => candidate.ProductCode)
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .Chunk(1000))
            {
                var stocks = await mikroDbContext.STOKLARs
                    .AsNoTracking()
                    .Where(stock => stock.sto_kod != null && productCodeChunk.Contains(stock.sto_kod))
                    .Select(stock => new
                    {
                        ProductCode = stock.sto_kod!,
                        ManagerCode = stock.sto_urun_sorkod
                    })
                    .ToListAsync(cancellationToken);
                var warehouseDetails = await mikroDbContext.STOK_DEPO_DETAYLARIs
                    .AsNoTracking()
                    .Where(detail =>
                        detail.sdp_depo_no == warehouseGroup.Key &&
                        detail.sdp_depo_kod != null &&
                        productCodeChunk.Contains(detail.sdp_depo_kod))
                    .Select(detail => new
                    {
                        ProductCode = detail.sdp_depo_kod!,
                        ManagerCode = detail.sdp_UrunSorumlusuKodu
                    })
                    .ToDictionaryAsync(
                        detail => detail.ProductCode,
                        detail => detail.ManagerCode,
                        StringComparer.OrdinalIgnoreCase,
                        cancellationToken);

                foreach (var stock in stocks)
                {
                    warehouseDetails.TryGetValue(stock.ProductCode, out var warehouseManagerCode);
                    var managerCode = NormalizeManagerValue(warehouseManagerCode)
                                      ?? NormalizeManagerValue(stock.ManagerCode);

                    if (managerCode is not null)
                    {
                        managerCodesByProduct[BuildProductManagerKey(warehouseGroup.Key, stock.ProductCode)] = managerCode;
                    }
                }
            }
        }

        var managerNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var managerCodeChunk in managerCodesByProduct.Values
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .Chunk(1000))
        {
            var personnel = await mikroDbContext.CARI_PERSONEL_TANIMLARIs
                .AsNoTracking()
                .Where(person =>
                    person.cari_per_kod != null &&
                    managerCodeChunk.Contains(person.cari_per_kod) &&
                    person.cari_per_iptal != true)
                .Select(person => new
                {
                    Code = person.cari_per_kod!,
                    person.cari_per_adi,
                    person.cari_per_soyadi
                })
                .ToListAsync(cancellationToken);

            foreach (var person in personnel)
            {
                var name = string.Join(
                    " ",
                    new[] { person.cari_per_adi, person.cari_per_soyadi }
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!.Trim()));
                managerNames[person.Code] = string.IsNullOrWhiteSpace(name) ? person.Code : name;
            }
        }

        return detected
            .Select(item =>
            {
                if (string.IsNullOrWhiteSpace(item.ProductCode) ||
                    !managerCodesByProduct.TryGetValue(
                        BuildProductManagerKey(item.WarehouseNo, item.ProductCode),
                        out var managerCode))
                {
                    return item;
                }

                return item with
                {
                    ProductManagerCode = managerCode,
                    ProductManagerName = managerNames.GetValueOrDefault(managerCode, managerCode)
                };
            })
            .ToArray();
    }

    private static string BuildProductManagerKey(int warehouseNo, string productCode) =>
        $"{warehouseNo}:{productCode.Trim()}";

    private static string? NormalizeManagerValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task UpsertDetectedWithRetryAsync(
        IReadOnlyCollection<DetectedStockAnomaly> detected,
        DateTime detectedAtUtc,
        CancellationToken cancellationToken)
    {
        await UpsertLock.WaitAsync(cancellationToken);

        try
        {
            for (var attempt = 1; attempt <= UpsertRetryCount; attempt++)
            {
                try
                {
                    await UpsertDetectedAsync(detected, detectedAtUtc, cancellationToken);
                    return;
                }
                catch (DbUpdateConcurrencyException exception) when (attempt < UpsertRetryCount)
                {
                    logger.LogWarning(
                        exception,
                        "Stock anomaly upsert concurrency conflict. Attempt={Attempt}/{AttemptCount}",
                        attempt,
                        UpsertRetryCount);

                    authDbContext.ChangeTracker.Clear();
                    await Task.Delay(TimeSpan.FromMilliseconds(attempt * 100), cancellationToken);
                }
                catch (DbUpdateConcurrencyException exception)
                {
                    logger.LogWarning(
                        exception,
                        "Stock anomaly EF upsert still conflicts after retries. Falling back to atomic SQL upsert.");

                    authDbContext.ChangeTracker.Clear();
                    await UpsertDetectedAtomicallyAsync(detected, detectedAtUtc, cancellationToken);
                    return;
                }
            }
        }
        finally
        {
            UpsertLock.Release();
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

        var sourceKeys = detected
            .Select(item => item.SourceKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var existing = new Dictionary<string, StockAnomaly>(StringComparer.OrdinalIgnoreCase);

        foreach (var sourceKeyChunk in sourceKeys.Chunk(1000))
        {
            var anomalies = await authDbContext.StockAnomalies
                .Where(anomaly => sourceKeyChunk.Contains(anomaly.SourceKey))
                .ToListAsync(cancellationToken);

            foreach (var anomaly in anomalies)
            {
                existing[anomaly.SourceKey] = anomaly;
            }
        }

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
                item.ProductManagerCode,
                item.ProductManagerName,
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

    private async Task UpsertDetectedAtomicallyAsync(
        IReadOnlyCollection<DetectedStockAnomaly> detected,
        DateTime detectedAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SET NOCOUNT ON;

            DECLARE @anomalyId uniqueidentifier;
            DECLARE @currentStatus nvarchar(20);

            SELECT
                @anomalyId = anomaly.id,
                @currentStatus = anomaly.status
            FROM dbo.stock_anomalies AS anomaly WITH (UPDLOCK, HOLDLOCK)
            WHERE anomaly.source_key = @sourceKey;

            IF @anomalyId IS NULL
            BEGIN
                SET @anomalyId = @newAnomalyId;
                SET @currentStatus = N'Open';

                INSERT INTO dbo.stock_anomalies (
                    id,
                    source_key,
                    type,
                    severity,
                    status,
                    warehouse_no,
                    related_warehouse_no,
                    warehouse_name,
                    related_warehouse_name,
                    product_code,
                    product_name,
                    product_manager_code,
                    product_manager_name,
                    document_serie,
                    document_order_no,
                    document_no,
                    movement_guid,
                    quantity,
                    expected_quantity,
                    actual_quantity,
                    average_quantity,
                    occurred_at_utc,
                    message,
                    evidence,
                    last_changed_by_user_id,
                    first_detected_at_utc,
                    last_detected_at_utc,
                    resolved_at_utc)
                VALUES (
                    @anomalyId,
                    @sourceKey,
                    @type,
                    @severity,
                    @currentStatus,
                    @warehouseNo,
                    @relatedWarehouseNo,
                    @warehouseName,
                    @relatedWarehouseName,
                    @productCode,
                    @productName,
                    @productManagerCode,
                    @productManagerName,
                    @documentSerie,
                    @documentOrderNo,
                    @documentNo,
                    @movementGuid,
                    @quantity,
                    @expectedQuantity,
                    @actualQuantity,
                    @averageQuantity,
                    @occurredAtUtc,
                    @message,
                    @evidence,
                    NULL,
                    @detectedAtUtc,
                    @detectedAtUtc,
                    NULL);
            END
            ELSE
            BEGIN
                UPDATE dbo.stock_anomalies
                SET
                    severity = @severity,
                    status = CASE WHEN status = N'Resolved' THEN N'Open' ELSE status END,
                    related_warehouse_no = @relatedWarehouseNo,
                    warehouse_name = @warehouseName,
                    related_warehouse_name = @relatedWarehouseName,
                    product_code = @productCode,
                    product_name = @productName,
                    product_manager_code = @productManagerCode,
                    product_manager_name = @productManagerName,
                    document_serie = @documentSerie,
                    document_order_no = @documentOrderNo,
                    document_no = @documentNo,
                    movement_guid = @movementGuid,
                    quantity = @quantity,
                    expected_quantity = @expectedQuantity,
                    actual_quantity = @actualQuantity,
                    average_quantity = @averageQuantity,
                    occurred_at_utc = @occurredAtUtc,
                    message = @message,
                    evidence = @evidence,
                    last_detected_at_utc = @detectedAtUtc,
                    resolved_at_utc = CASE WHEN status = N'Resolved' THEN NULL ELSE resolved_at_utc END
                WHERE id = @anomalyId;

                SELECT @currentStatus = status
                FROM dbo.stock_anomalies
                WHERE id = @anomalyId;
            END

            INSERT INTO dbo.stock_anomaly_events (
                id,
                stock_anomaly_id,
                event_type,
                status,
                message,
                changed_by_user_id,
                occurred_at_utc)
            VALUES (
                @eventId,
                @anomalyId,
                N'Detected',
                @currentStatus,
                N'Anomali taramada yakalandi.',
                NULL,
                @detectedAtUtc);
            """;

        var connection = authDbContext.Database.GetDbConnection();
        var shouldClose = connection.State == ConnectionState.Closed;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            foreach (var item in detected)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.CommandTimeout = 60;

                AddParameter(command, "@newAnomalyId", Guid.NewGuid());
                AddParameter(command, "@eventId", Guid.NewGuid());
                AddParameter(command, "@sourceKey", item.SourceKey);
                AddParameter(command, "@type", item.Type.ToString());
                AddParameter(command, "@severity", item.Severity.ToString());
                AddParameter(command, "@warehouseNo", item.WarehouseNo);
                AddParameter(command, "@relatedWarehouseNo", item.RelatedWarehouseNo);
                AddParameter(command, "@warehouseName", item.WarehouseName);
                AddParameter(command, "@relatedWarehouseName", item.RelatedWarehouseName);
                AddParameter(command, "@productCode", item.ProductCode);
                AddParameter(command, "@productName", item.ProductName);
                AddParameter(command, "@productManagerCode", item.ProductManagerCode);
                AddParameter(command, "@productManagerName", item.ProductManagerName);
                AddParameter(command, "@documentSerie", item.DocumentSerie);
                AddParameter(command, "@documentOrderNo", item.DocumentOrderNo);
                AddParameter(command, "@documentNo", item.DocumentNo);
                AddParameter(command, "@movementGuid", item.MovementGuid);
                AddParameter(command, "@quantity", item.Quantity);
                AddParameter(command, "@expectedQuantity", item.ExpectedQuantity);
                AddParameter(command, "@actualQuantity", item.ActualQuantity);
                AddParameter(command, "@averageQuantity", item.AverageQuantity);
                AddParameter(command, "@occurredAtUtc", item.OccurredAtUtc);
                AddParameter(command, "@message", item.Message);
                AddParameter(command, "@evidence", item.Evidence);
                AddParameter(command, "@detectedAtUtc", detectedAtUtc);

                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static IReadOnlyCollection<DetectedStockAnomaly> DeduplicateDetected(
        IEnumerable<DetectedStockAnomaly> detected) =>
        detected
            .GroupBy(item => item.SourceKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

    private Task<IReadOnlyCollection<DetectedStockAnomaly>> FindNegativeStockAsync(
        NormalizedScanRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH Balances AS (
                SELECT TOP (@take)
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
                         - ISNULL(summary.sho_CikisNormal, 0) - ISNULL(summary.sho_GirisIade, 0)) < -0.000001
                ORDER BY Quantity ASC
            )
            SELECT
                balances.WarehouseNo,
                warehouse.dep_adi AS WarehouseName,
                balances.ProductCode,
                stock.sto_isim AS ProductName,
                balances.Quantity
            FROM Balances AS balances
            LEFT JOIN dbo.DEPOLAR AS warehouse WITH (NOLOCK) ON warehouse.dep_no = balances.WarehouseNo
            LEFT JOIN dbo.STOKLAR AS stock WITH (NOLOCK) ON stock.sto_kod = balances.ProductCode
            ORDER BY balances.Quantity ASC
            OPTION (RECOMPILE)
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
            WITH Duplicates AS (
                SELECT TOP (@take)
                    movement.sth_evraktip AS DocumentType,
                    movement.sth_tip AS MovementType,
                    movement.sth_cins AS MovementKind,
                    movement.sth_normal_iade AS NormalReturn,
                    movement.sth_evrakno_seri AS DocumentSerie,
                    movement.sth_evrakno_sira AS DocumentOrderNo,
                    movement.sth_belge_no AS DocumentNo,
                    movement.sth_stok_kod AS ProductCode,
                    ISNULL(movement.sth_cikis_depo_no, movement.sth_giris_depo_no) AS WarehouseNo,
                    movement.sth_giris_depo_no AS RelatedWarehouseNo,
                    movement.sth_miktar AS Quantity,
                    MIN(movement.sth_tarih) AS FirstMovementDate,
                    COUNT_BIG(*) AS DuplicateCount
                FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
                WHERE movement.sth_iptal <> 1
                  AND movement.sth_tarih >= @startDate
                  AND movement.sth_tarih < @endDateExclusive
                  AND movement.sth_evrakno_seri IS NOT NULL
                  AND movement.sth_evrakno_sira IS NOT NULL
                  AND (@warehouseNo IS NULL OR movement.sth_cikis_depo_no = @warehouseNo OR movement.sth_giris_depo_no = @warehouseNo)
                GROUP BY movement.sth_evraktip, movement.sth_tip, movement.sth_cins, movement.sth_normal_iade,
                    movement.sth_evrakno_seri, movement.sth_evrakno_sira, movement.sth_belge_no, movement.sth_stok_kod,
                    ISNULL(movement.sth_cikis_depo_no, movement.sth_giris_depo_no), movement.sth_giris_depo_no, movement.sth_miktar
                HAVING COUNT_BIG(*) > 1
                ORDER BY DuplicateCount DESC, FirstMovementDate DESC
            )
            SELECT
                duplicate.DocumentType,
                duplicate.MovementType,
                duplicate.MovementKind,
                duplicate.NormalReturn,
                duplicate.DocumentSerie,
                duplicate.DocumentOrderNo,
                duplicate.DocumentNo,
                duplicate.ProductCode,
                stock.sto_isim AS ProductName,
                duplicate.WarehouseNo,
                duplicate.RelatedWarehouseNo,
                warehouse.dep_adi AS WarehouseName,
                relatedWarehouse.dep_adi AS RelatedWarehouseName,
                duplicate.Quantity,
                duplicate.FirstMovementDate,
                duplicate.DuplicateCount
            FROM Duplicates AS duplicate
            LEFT JOIN dbo.STOKLAR AS stock WITH (NOLOCK) ON stock.sto_kod = duplicate.ProductCode
            LEFT JOIN dbo.DEPOLAR AS warehouse WITH (NOLOCK) ON warehouse.dep_no = duplicate.WarehouseNo
            LEFT JOIN dbo.DEPOLAR AS relatedWarehouse WITH (NOLOCK) ON relatedWarehouse.dep_no = duplicate.RelatedWarehouseNo
            ORDER BY duplicate.DuplicateCount DESC, duplicate.FirstMovementDate DESC
            OPTION (RECOMPILE)
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
            WITH Differences AS (
                SELECT TOP (@take)
                    movement.sth_Guid AS MovementGuid,
                    movement.sth_tarih AS MovementDate,
                    movement.sth_belge_no AS DocumentNo,
                    movement.sth_evrakno_seri AS DocumentSerie,
                    movement.sth_evrakno_sira AS DocumentOrderNo,
                    movement.sth_stok_kod AS ProductCode,
                    movement.sth_cikis_depo_no AS WarehouseNo,
                    movement.sth_giris_depo_no AS RelatedWarehouseNo,
                    movement.sth_miktar AS Quantity,
                    movement.sth_FormulMiktar AS ActualQuantity
                FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
                WHERE movement.sth_iptal <> 1
                  AND movement.sth_tarih >= @startDate
                  AND movement.sth_tarih < @endDateExclusive
                  AND movement.sth_evraktip = 17
                  AND movement.sth_nakliyedurumu = 1
                  AND movement.sth_FormulMiktar IS NOT NULL
                  AND ABS(ISNULL(movement.sth_FormulMiktar, 0) - ISNULL(movement.sth_miktar, 0)) > 0.000001
                  AND (@warehouseNo IS NULL OR movement.sth_cikis_depo_no = @warehouseNo OR movement.sth_giris_depo_no = @warehouseNo)
                ORDER BY movement.sth_tarih DESC
            )
            SELECT
                difference.MovementGuid,
                difference.MovementDate,
                difference.DocumentNo,
                difference.DocumentSerie,
                difference.DocumentOrderNo,
                difference.ProductCode,
                stock.sto_isim AS ProductName,
                difference.WarehouseNo,
                sourceWarehouse.dep_adi AS WarehouseName,
                difference.RelatedWarehouseNo,
                targetWarehouse.dep_adi AS RelatedWarehouseName,
                difference.Quantity,
                difference.ActualQuantity
            FROM Differences AS difference
            LEFT JOIN dbo.STOKLAR AS stock WITH (NOLOCK) ON stock.sto_kod = difference.ProductCode
            LEFT JOIN dbo.DEPOLAR AS sourceWarehouse WITH (NOLOCK) ON sourceWarehouse.dep_no = difference.WarehouseNo
            LEFT JOIN dbo.DEPOLAR AS targetWarehouse WITH (NOLOCK) ON targetWarehouse.dep_no = difference.RelatedWarehouseNo
            ORDER BY difference.MovementDate DESC
            OPTION (RECOMPILE)
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
                    CASE WHEN movement.sth_tip = 0 THEN movement.sth_giris_depo_no ELSE movement.sth_cikis_depo_no END AS WarehouseNo,
                    ISNULL(movement.sth_miktar, 0) AS Quantity
                FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
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
            ),
            HighRows AS (
                SELECT TOP (@take)
                    row.sth_Guid AS MovementGuid,
                    row.sth_tarih AS MovementDate,
                    row.sth_evrakno_seri AS DocumentSerie,
                    row.sth_evrakno_sira AS DocumentOrderNo,
                    row.sth_belge_no AS DocumentNo,
                    row.sth_stok_kod AS ProductCode,
                    row.WarehouseNo,
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
            )
            SELECT
                highRow.MovementGuid,
                highRow.MovementDate,
                highRow.DocumentSerie,
                highRow.DocumentOrderNo,
                highRow.DocumentNo,
                highRow.ProductCode,
                stock.sto_isim AS ProductName,
                highRow.WarehouseNo,
                warehouse.dep_adi AS WarehouseName,
                highRow.Quantity,
                highRow.AverageQuantity
            FROM HighRows AS highRow
            LEFT JOIN dbo.STOKLAR AS stock WITH (NOLOCK) ON stock.sto_kod = highRow.ProductCode
            LEFT JOIN dbo.DEPOLAR AS warehouse WITH (NOLOCK) ON warehouse.dep_no = highRow.WarehouseNo
            ORDER BY highRow.Quantity DESC
            OPTION (RECOMPILE)
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
            ),
            DormantBalances AS (
                SELECT TOP (@take)
                    balances.WarehouseNo,
                    balances.ProductCode,
                    balances.Quantity
                FROM Balances AS balances
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM dbo.STOK_HAREKETLERI AS recentMovement WITH (NOLOCK)
                    WHERE recentMovement.sth_iptal <> 1
                      AND recentMovement.sth_tarih >= @dormantCutoffDate
                      AND recentMovement.sth_stok_kod = balances.ProductCode
                      AND (
                          (recentMovement.sth_tip = 0 AND recentMovement.sth_giris_depo_no = balances.WarehouseNo) OR
                          (recentMovement.sth_tip <> 0 AND recentMovement.sth_cikis_depo_no = balances.WarehouseNo)
                      )
                )
                ORDER BY balances.Quantity DESC
            )
            SELECT
                dormant.WarehouseNo,
                warehouse.dep_adi AS WarehouseName,
                dormant.ProductCode,
                stock.sto_isim AS ProductName,
                dormant.Quantity,
                lastMovements.LastMovementDate
            FROM DormantBalances AS dormant
            OUTER APPLY (
                SELECT TOP (1) movement.sth_tarih AS LastMovementDate
                FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
                WHERE movement.sth_iptal <> 1
                  AND movement.sth_stok_kod = dormant.ProductCode
                  AND (
                      (movement.sth_tip = 0 AND movement.sth_giris_depo_no = dormant.WarehouseNo) OR
                      (movement.sth_tip <> 0 AND movement.sth_cikis_depo_no = dormant.WarehouseNo)
                  )
                ORDER BY movement.sth_tarih DESC
            ) AS lastMovements
            LEFT JOIN dbo.DEPOLAR AS warehouse WITH (NOLOCK) ON warehouse.dep_no = dormant.WarehouseNo
            LEFT JOIN dbo.STOKLAR AS stock WITH (NOLOCK) ON stock.sto_kod = dormant.ProductCode
            ORDER BY dormant.Quantity DESC
            OPTION (RECOMPILE)
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
            WITH PendingTransfers AS (
                SELECT TOP (@take)
                    movement.sth_Guid AS MovementGuid,
                    movement.sth_tarih AS MovementDate,
                    movement.sth_evrakno_seri AS DocumentSerie,
                    movement.sth_evrakno_sira AS DocumentOrderNo,
                    movement.sth_belge_no AS DocumentNo,
                    movement.sth_stok_kod AS ProductCode,
                    movement.sth_cikis_depo_no AS WarehouseNo,
                    ISNULL(movement.sth_nakliyedeposu, movement.sth_giris_depo_no) AS RelatedWarehouseNo,
                    movement.sth_miktar AS Quantity,
                    movement.sth_nakliyedurumu AS ShippingState
                FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
                WHERE movement.sth_iptal <> 1
                  AND movement.sth_evraktip = 17
                  AND ISNULL(movement.sth_nakliyedurumu, 0) <> 1
                  AND movement.sth_tarih >= @startDate
                  AND movement.sth_tarih < @endDateExclusive
                  AND movement.sth_tarih < @pendingTransferCutoffDate
                  AND (@warehouseNo IS NULL OR movement.sth_cikis_depo_no = @warehouseNo OR movement.sth_giris_depo_no = @warehouseNo OR movement.sth_nakliyedeposu = @warehouseNo)
                ORDER BY movement.sth_tarih ASC
            )
            SELECT
                pending.MovementGuid,
                pending.MovementDate,
                pending.DocumentSerie,
                pending.DocumentOrderNo,
                pending.DocumentNo,
                pending.ProductCode,
                stock.sto_isim AS ProductName,
                pending.WarehouseNo,
                sourceWarehouse.dep_adi AS WarehouseName,
                pending.RelatedWarehouseNo,
                targetWarehouse.dep_adi AS RelatedWarehouseName,
                pending.Quantity,
                pending.ShippingState
            FROM PendingTransfers AS pending
            LEFT JOIN dbo.STOKLAR AS stock WITH (NOLOCK) ON stock.sto_kod = pending.ProductCode
            LEFT JOIN dbo.DEPOLAR AS sourceWarehouse WITH (NOLOCK) ON sourceWarehouse.dep_no = pending.WarehouseNo
            LEFT JOIN dbo.DEPOLAR AS targetWarehouse WITH (NOLOCK) ON targetWarehouse.dep_no = pending.RelatedWarehouseNo
            ORDER BY pending.MovementDate ASC
            OPTION (RECOMPILE)
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
            anomaly.ProductManagerCode,
            anomaly.ProductManagerName,
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
        string? Evidence,
        string? ProductManagerCode = null,
        string? ProductManagerName = null);
}

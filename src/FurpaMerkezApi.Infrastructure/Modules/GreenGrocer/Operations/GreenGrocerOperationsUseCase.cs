using System.Data;
using System.Data.Common;
using System.Globalization;
using FurpaMerkezApi.Application.Modules.GreenGrocer.Operations;
using FurpaMerkezApi.Infrastructure.OfflineSync;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.GreenGrocer.Operations;

public sealed class GreenGrocerOperationsUseCase(
    MikroDbContext mikroDbContext,
    MikroWriteDbContext mikroWriteDbContext,
    AuthDbContext authDbContext,
    IOptions<MikroWriteOptions> mikroWriteOptions,
    MobileOfflineSyncService mobileOfflineSyncService)
    : IGreenGrocerOperationsUseCase
{
    private const int DefaultWarehouseNo = 56;
    private const int DefaultCounterWarehouseNo = 1;
    private const int DefaultTake = 500;
    private const int MaxTake = 2000;
    private const int FirstDocumentOrderNo = 0;
    private const short MovementFileId = 16;
    private const short MikroUserNo = 39;
    private const byte NormalMovement = 0;
    private const byte AdjustmentGenre = 10;
    private const byte IncreaseMovementType = 0;
    private const byte DecreaseMovementType = 1;
    private const byte IncreaseDocumentType = 12;
    private const byte DecreaseDocumentType = 0;
    private const string IncreaseSerie = "MNVE";
    private const string DecreaseSerie = "MNVF";
    private const string DefaultReasonCode = "weighing-difference";
    private const string OfflineOperationCode = "green-grocer.operations.adjustment.apply";

    private static readonly DateTime MikroEmptyDate = new(1899, 12, 30);
    private static readonly string[] GreenGrocerModelCodes = ["10", "11", "12", "23"];

    public async Task<GreenGrocerOperationsOverviewDto> GetOverviewAsync(
        GreenGrocerOperationsOverviewRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeOverview(request);
        var warehouseNameTask = GetWarehouseNameAsync(normalized.WarehouseNo, cancellationToken);
        var rows = await ListOperationRowsAsync(normalized, cancellationToken);
        var caseInfoByStockCode = await GetOrderCaseInfoByStockCodeAsync(
            normalized,
            rows.Select(row => row.StockCode).ToArray(),
            cancellationToken);

        var items = rows
            .Select(row => BuildItem(row, caseInfoByStockCode.GetValueOrDefault(row.StockCode)))
            .ToArray();
        var statusSummaries = items
            .GroupBy(item => new
            {
                item.PrimaryStatusCode,
                item.PrimaryStatusName
            })
            .OrderBy(group => group.Key.PrimaryStatusName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new GreenGrocerOperationsStatusSummaryDto(
                group.Key.PrimaryStatusCode,
                group.Key.PrimaryStatusName,
                group.Count(),
                Round(group.Sum(item => item.CurrentStockQuantity)),
                Round(group.Sum(item => item.PurchaseQuantity)),
                Round(group.Sum(item => item.AdjustmentNetQuantity)),
                Round(group.Sum(item => item.OrderEstimatedQuantity)),
                Round(group.Sum(item => item.ShipmentQuantity))))
            .ToArray();

        return new GreenGrocerOperationsOverviewDto(
            normalized.WarehouseNo,
            await warehouseNameTask,
            normalized.StartDate,
            normalized.EndDate,
            items.Length,
            Round(items.Sum(item => item.CurrentStockQuantity)),
            Round(items.Sum(item => item.PurchaseQuantity)),
            Round(items.Sum(item => item.PurchaseAmount)),
            Round(items.Sum(item => item.AdjustmentInQuantity)),
            Round(items.Sum(item => item.AdjustmentOutQuantity)),
            Round(items.Sum(item => item.AdjustmentNetQuantity)),
            Round(items.Sum(item => item.OrderInputQuantity)),
            Round(items.Sum(item => item.OrderEstimatedQuantity)),
            Round(items.Sum(item => item.ShipmentQuantity)),
            Round(items.Sum(item => item.LastCountQuantity ?? 0d)),
            statusSummaries,
            items);
    }

    public GreenGrocerOperationsAdjustmentPreviewDto PreviewAdjustment(
        GreenGrocerOperationsAdjustmentPreviewRequest request)
    {
        var normalized = NormalizePreview(request);

        return new GreenGrocerOperationsAdjustmentPreviewDto(
            normalized.WarehouseNo,
            normalized.CounterWarehouseNo,
            normalized.Direction,
            normalized.DirectionName,
            normalized.DocumentSerie,
            normalized.MovementType,
            normalized.MovementGenre,
            normalized.DocumentType,
            normalized.ReasonCode,
            normalized.ReasonName,
            normalized.Lines.Count,
            Round(normalized.Lines.Sum(line => line.Quantity)),
            Round(normalized.Lines.Sum(line => line.Quantity * line.UnitPrice)));
    }

    public async Task<GreenGrocerOperationsAdjustmentApplyResponse> ApplyAdjustmentAsync(
        GreenGrocerOperationsAdjustmentApplyRequest request,
        CancellationToken cancellationToken)
    {
        ValidateApplyIdentity(request);

        var acquireResult = await mobileOfflineSyncService
            .AcquireAsync<GreenGrocerOperationsAdjustmentApplyRequest, GreenGrocerOperationsAdjustmentApplyResponse>(
                OfflineOperationCode,
                request.RequestedByUserId,
                request.WarehouseNo,
                request.ClientRequestId,
                request,
                (_, innerCancellationToken) => TryRecoverAdjustmentResponseAsync(
                    request,
                    innerCancellationToken),
                cancellationToken);

        if (acquireResult.State == MobileOfflineSyncAcquireState.Completed)
        {
            return acquireResult.Response!;
        }

        if (acquireResult.State == MobileOfflineSyncAcquireState.Processing)
        {
            throw new InvalidOperationException(
                "A green grocer adjustment request with the same clientRequestId is already being processed.");
        }

        try
        {
            var response = await ExecuteAdjustmentDatabaseAsync(request, cancellationToken);
            await mobileOfflineSyncService.CompleteAsync(
                OfflineOperationCode,
                request.RequestedByUserId,
                request.ClientRequestId,
                response,
                cancellationToken);

            return response;
        }
        catch (Exception exception)
        {
            await TryMarkFailedAsync(
                request.RequestedByUserId,
                request.ClientRequestId,
                exception.Message,
                cancellationToken);
            throw;
        }
    }

    private async Task<GreenGrocerOperationsAdjustmentApplyResponse> ExecuteAdjustmentDatabaseAsync(
        GreenGrocerOperationsAdjustmentApplyRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeApply(request);
        await EnsureGreenGrocerStocksAsync(normalized.Lines, cancellationToken);

        var options = mikroWriteOptions.Value;
        var now = DateTime.Now;
        var movementDate = (request.MovementDate ?? DateTime.Today).Date;
        var documentDate = (request.DocumentDate ?? movementDate).Date;
        var documentNo = NormalizeText(request.DocumentNo, 50);
        var creator = NormalizeText(request.Creator, 25);
        var acceptor = NormalizeText(request.Acceptor, 25);
        var traceKey = MobileOfflineSyncService.ToTraceKey(request.ClientRequestId);
        var headerDescription = CreateTraceDescription(request.Description, traceKey);
        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            try
            {
                var documentOrderNo = await GetNextDocumentOrderNoAsync(
                    normalized.DocumentSerie,
                    normalized.MovementType,
                    normalized.DocumentType,
                    cancellationToken);
                var movements = normalized.Lines
                    .Select((line, rowNo) => CreateAdjustmentMovement(
                        normalized,
                        line,
                        rowNo,
                        now,
                        movementDate,
                        documentDate,
                        documentNo,
                        documentOrderNo,
                        creator,
                        acceptor,
                        headerDescription,
                        traceKey))
                    .ToArray();

                await mikroWriteDbContext.STOK_HAREKETLERIs.AddRangeAsync(movements, cancellationToken);
                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new GreenGrocerOperationsAdjustmentApplyResponse(
                    request.ClientRequestId,
                    "Completed",
                    normalized.WarehouseNo,
                    normalized.CounterWarehouseNo,
                    normalized.Direction,
                    normalized.DocumentSerie,
                    documentOrderNo,
                    movementDate,
                    documentDate,
                    documentNo,
                    normalized.ReasonCode,
                    normalized.ReasonName,
                    movements.Length,
                    Round(movements.Sum(movement => movement.sth_miktar ?? 0d)),
                    Round(movements.Sum(movement => movement.sth_tutar ?? 0d)),
                    options.ConnectionStringName,
                    movements.Select(movement => movement.sth_Guid).ToArray());
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<IReadOnlyCollection<GreenGrocerOperationsRawRow>> ListOperationRowsAsync(
        NormalizedOverviewRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SET NOCOUNT ON;

            WITH ProductSeed AS (
                SELECT
                    stock.sto_kod AS StockCode,
                    COALESCE(NULLIF(stock.sto_kisa_ismi, ''), stock.sto_isim, '') AS StockName,
                    COALESCE(stock.sto_model_kodu, '') AS ModelCode,
                    COALESCE(NULLIF(stock.sto_birim1_ad, ''), '') AS UnitName
                FROM dbo.STOKLAR AS stock WITH (NOLOCK)
                WHERE COALESCE(stock.sto_iptal, 0) = 0
                  AND stock.sto_model_kodu IN ('10', '11', '12', '23')
                  AND (@typeCode IS NULL OR stock.sto_model_kodu = @typeCode)
                  AND (
                        @search IS NULL
                        OR stock.sto_kod LIKE @searchLike
                        OR stock.sto_isim LIKE @searchLike
                        OR stock.sto_kisa_ismi LIKE @searchLike
                  )
            ),
            PurchaseAgg AS (
                SELECT
                    movement.sth_stok_kod AS StockCode,
                    SUM(COALESCE(movement.sth_miktar, 0)) AS PurchaseQuantity,
                    SUM(COALESCE(movement.sth_tutar, 0)) AS PurchaseAmount,
                    COUNT(DISTINCT CONCAT(
                        COALESCE(movement.sth_evrakno_seri, ''),
                        '|',
                        COALESCE(CONVERT(varchar(20), movement.sth_evrakno_sira), ''),
                        '|',
                        COALESCE(movement.sth_belge_no, '')
                    )) AS PurchaseDocumentCount,
                    MAX(movement.sth_tarih) AS LastPurchaseDate
                FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
                WHERE COALESCE(movement.sth_iptal, 0) = 0
                  AND movement.sth_giris_depo_no = @warehouseNo
                  AND movement.sth_tip = 0
                  AND movement.sth_normal_iade = 0
                  AND movement.sth_evraktip = 3
                  AND movement.sth_cins = 16
                  AND movement.sth_tarih >= @startDate
                  AND movement.sth_tarih < @endDateExclusive
                GROUP BY movement.sth_stok_kod
            ),
            LastPurchase AS (
                SELECT *
                FROM (
                    SELECT
                        movement.sth_stok_kod AS StockCode,
                        COALESCE(NULLIF(movement.sth_belge_no, ''), CONCAT(
                            COALESCE(movement.sth_evrakno_seri, ''),
                            '/',
                            COALESCE(CONVERT(varchar(20), movement.sth_evrakno_sira), '')
                        )) AS LastPurchaseDocument,
                        COALESCE(movement.sth_cari_kodu, '') AS LastSupplierCode,
                        COALESCE(NULLIF(customer.cari_unvan1, ''), customer.cari_unvan2, '') AS LastSupplierName,
                        ROW_NUMBER() OVER (
                            PARTITION BY movement.sth_stok_kod
                            ORDER BY movement.sth_tarih DESC, movement.sth_create_date DESC, movement.sth_Guid DESC
                        ) AS RowNo
                    FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
                    LEFT JOIN dbo.CARI_HESAPLAR AS customer WITH (NOLOCK)
                        ON customer.cari_kod = movement.sth_cari_kodu
                    WHERE COALESCE(movement.sth_iptal, 0) = 0
                      AND movement.sth_giris_depo_no = @warehouseNo
                      AND movement.sth_tip = 0
                      AND movement.sth_normal_iade = 0
                      AND movement.sth_evraktip = 3
                      AND movement.sth_cins = 16
                      AND movement.sth_tarih >= @startDate
                      AND movement.sth_tarih < @endDateExclusive
                ) AS rows
                WHERE rows.RowNo = 1
            ),
            AdjustmentAgg AS (
                SELECT
                    movement.sth_stok_kod AS StockCode,
                    SUM(CASE
                        WHEN movement.sth_tip = 0 AND movement.sth_giris_depo_no = @warehouseNo
                        THEN COALESCE(movement.sth_miktar, 0)
                        ELSE 0
                    END) AS AdjustmentInQuantity,
                    SUM(CASE
                        WHEN movement.sth_tip = 1 AND movement.sth_cikis_depo_no = @warehouseNo
                        THEN COALESCE(movement.sth_miktar, 0)
                        ELSE 0
                    END) AS AdjustmentOutQuantity,
                    COUNT(DISTINCT CONCAT(
                        COALESCE(movement.sth_evrakno_seri, ''),
                        '|',
                        COALESCE(CONVERT(varchar(20), movement.sth_evrakno_sira), '')
                    )) AS AdjustmentDocumentCount,
                    MAX(movement.sth_tarih) AS LastAdjustmentDate
                FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
                WHERE COALESCE(movement.sth_iptal, 0) = 0
                  AND movement.sth_normal_iade = 0
                  AND movement.sth_cins = 10
                  AND (
                        COALESCE(movement.sth_evrakno_seri, '') LIKE 'MNV%'
                        OR COALESCE(movement.sth_evrakno_seri, '') = 'MERC'
                  )
                  AND (
                        (movement.sth_tip = 0 AND movement.sth_giris_depo_no = @warehouseNo)
                        OR (movement.sth_tip = 1 AND movement.sth_cikis_depo_no = @warehouseNo)
                  )
                  AND movement.sth_tarih >= @startDate
                  AND movement.sth_tarih < @endDateExclusive
                GROUP BY movement.sth_stok_kod
            ),
            LastAdjustment AS (
                SELECT *
                FROM (
                    SELECT
                        movement.sth_stok_kod AS StockCode,
                        CONCAT(
                            COALESCE(movement.sth_evrakno_seri, ''),
                            '/',
                            COALESCE(CONVERT(varchar(20), movement.sth_evrakno_sira), '')
                        ) AS LastAdjustmentDocument,
                        COALESCE(movement.sth_evrakno_seri, '') AS LastAdjustmentSeries,
                        COALESCE(movement.sth_aciklama, '') AS LastAdjustmentReason,
                        ROW_NUMBER() OVER (
                            PARTITION BY movement.sth_stok_kod
                            ORDER BY movement.sth_tarih DESC, movement.sth_create_date DESC, movement.sth_Guid DESC
                        ) AS RowNo
                    FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
                    WHERE COALESCE(movement.sth_iptal, 0) = 0
                      AND movement.sth_normal_iade = 0
                      AND movement.sth_cins = 10
                      AND (
                            COALESCE(movement.sth_evrakno_seri, '') LIKE 'MNV%'
                            OR COALESCE(movement.sth_evrakno_seri, '') = 'MERC'
                      )
                      AND (
                            (movement.sth_tip = 0 AND movement.sth_giris_depo_no = @warehouseNo)
                            OR (movement.sth_tip = 1 AND movement.sth_cikis_depo_no = @warehouseNo)
                      )
                      AND movement.sth_tarih >= @startDate
                      AND movement.sth_tarih < @endDateExclusive
                ) AS rows
                WHERE rows.RowNo = 1
            ),
            OrderAgg AS (
                SELECT
                    warehouseOrder.ssip_stok_kod AS StockCode,
                    SUM(COALESCE(warehouseOrder.ssip_miktar, 0)) AS OrderMicroQuantity,
                    COUNT(*) AS OrderLineCount,
                    COUNT(DISTINCT warehouseOrder.ssip_girdepo) AS OrderBranchCount,
                    MAX(warehouseOrder.ssip_tarih) AS LastOrderDate
                FROM dbo.DEPOLAR_ARASI_SIPARISLER AS warehouseOrder WITH (NOLOCK)
                WHERE COALESCE(warehouseOrder.ssip_iptal, 0) = 0
                  AND warehouseOrder.ssip_cikdepo = @warehouseNo
                  AND warehouseOrder.ssip_tarih >= @startDate
                  AND warehouseOrder.ssip_tarih < @endDateExclusive
                GROUP BY warehouseOrder.ssip_stok_kod
            ),
            ShipmentAgg AS (
                SELECT
                    movement.sth_stok_kod AS StockCode,
                    SUM(COALESCE(movement.sth_miktar, 0)) AS ShipmentQuantity,
                    COUNT(DISTINCT CONCAT(
                        COALESCE(movement.sth_evrakno_seri, ''),
                        '|',
                        COALESCE(CONVERT(varchar(20), movement.sth_evrakno_sira), '')
                    )) AS ShipmentDocumentCount,
                    COUNT(DISTINCT movement.sth_giris_depo_no) AS ShipmentBranchCount,
                    MAX(movement.sth_tarih) AS LastShipmentDate
                FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
                WHERE COALESCE(movement.sth_iptal, 0) = 0
                  AND movement.sth_cikis_depo_no = @warehouseNo
                  AND movement.sth_tip = 2
                  AND movement.sth_normal_iade = 0
                  AND movement.sth_evraktip = 17
                  AND movement.sth_cins = 6
                  AND movement.sth_tarih >= @startDate
                  AND movement.sth_tarih < @endDateExclusive
                GROUP BY movement.sth_stok_kod
            ),
            LastShipment AS (
                SELECT *
                FROM (
                    SELECT
                        movement.sth_stok_kod AS StockCode,
                        CONCAT(
                            COALESCE(movement.sth_evrakno_seri, ''),
                            '/',
                            COALESCE(CONVERT(varchar(20), movement.sth_evrakno_sira), '')
                        ) AS LastShipmentDocument,
                        ROW_NUMBER() OVER (
                            PARTITION BY movement.sth_stok_kod
                            ORDER BY movement.sth_tarih DESC, movement.sth_create_date DESC, movement.sth_Guid DESC
                        ) AS RowNo
                    FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
                    WHERE COALESCE(movement.sth_iptal, 0) = 0
                      AND movement.sth_cikis_depo_no = @warehouseNo
                      AND movement.sth_tip = 2
                      AND movement.sth_normal_iade = 0
                      AND movement.sth_evraktip = 17
                      AND movement.sth_cins = 6
                      AND movement.sth_tarih >= @startDate
                      AND movement.sth_tarih < @endDateExclusive
                ) AS rows
                WHERE rows.RowNo = 1
            ),
            CountGrouped AS (
                SELECT
                    counts.sym_Stokkodu AS StockCode,
                    CAST(counts.sym_tarihi AS date) AS LastCountDate,
                    counts.sym_evrakno AS LastCountDocumentNo,
                    MAX(counts.sym_create_date) AS LastCountCreateDate,
                    SUM(COALESCE(counts.sym_miktar1, 0)) AS LastCountQuantity
                FROM dbo.SAYIM_SONUCLARI AS counts WITH (NOLOCK)
                WHERE COALESCE(counts.sym_iptal, 0) = 0
                  AND counts.sym_depono = @warehouseNo
                  AND counts.sym_tarihi < @endDateExclusive
                GROUP BY counts.sym_Stokkodu, CAST(counts.sym_tarihi AS date), counts.sym_evrakno
            ),
            LatestCount AS (
                SELECT *
                FROM (
                    SELECT
                        CountGrouped.*,
                        ROW_NUMBER() OVER (
                            PARTITION BY CountGrouped.StockCode
                            ORDER BY CountGrouped.LastCountDate DESC,
                                     CountGrouped.LastCountCreateDate DESC,
                                     CountGrouped.LastCountDocumentNo DESC
                        ) AS RowNo
                    FROM CountGrouped
                ) AS rows
                WHERE rows.RowNo = 1
            ),
            JoinedRows AS (
                SELECT
                    product.StockCode,
                    product.StockName,
                    product.ModelCode,
                    product.UnitName,
                    currentStock.CurrentStockQuantity,
                    COALESCE(purchase.PurchaseQuantity, 0) AS PurchaseQuantity,
                    COALESCE(purchase.PurchaseAmount, 0) AS PurchaseAmount,
                    CASE
                        WHEN COALESCE(purchase.PurchaseQuantity, 0) = 0 THEN 0
                        ELSE COALESCE(purchase.PurchaseAmount, 0) / NULLIF(purchase.PurchaseQuantity, 0)
                    END AS PurchaseUnitPrice,
                    COALESCE(purchase.PurchaseDocumentCount, 0) AS PurchaseDocumentCount,
                    purchase.LastPurchaseDate,
                    COALESCE(lastPurchase.LastPurchaseDocument, '') AS LastPurchaseDocument,
                    COALESCE(lastPurchase.LastSupplierCode, '') AS LastSupplierCode,
                    COALESCE(lastPurchase.LastSupplierName, '') AS LastSupplierName,
                    COALESCE(adjustment.AdjustmentInQuantity, 0) AS AdjustmentInQuantity,
                    COALESCE(adjustment.AdjustmentOutQuantity, 0) AS AdjustmentOutQuantity,
                    COALESCE(adjustment.AdjustmentInQuantity, 0) - COALESCE(adjustment.AdjustmentOutQuantity, 0) AS AdjustmentNetQuantity,
                    COALESCE(adjustment.AdjustmentDocumentCount, 0) AS AdjustmentDocumentCount,
                    adjustment.LastAdjustmentDate,
                    COALESCE(lastAdjustment.LastAdjustmentDocument, '') AS LastAdjustmentDocument,
                    COALESCE(lastAdjustment.LastAdjustmentSeries, '') AS LastAdjustmentSeries,
                    COALESCE(lastAdjustment.LastAdjustmentReason, '') AS LastAdjustmentReason,
                    COALESCE(orders.OrderMicroQuantity, 0) AS OrderMicroQuantity,
                    COALESCE(orders.OrderLineCount, 0) AS OrderLineCount,
                    COALESCE(orders.OrderBranchCount, 0) AS OrderBranchCount,
                    COALESCE(shipment.ShipmentQuantity, 0) AS ShipmentQuantity,
                    COALESCE(shipment.ShipmentDocumentCount, 0) AS ShipmentDocumentCount,
                    COALESCE(shipment.ShipmentBranchCount, 0) AS ShipmentBranchCount,
                    shipment.LastShipmentDate,
                    COALESCE(lastShipment.LastShipmentDocument, '') AS LastShipmentDocument,
                    latestCount.LastCountDate,
                    latestCount.LastCountDocumentNo,
                    latestCount.LastCountQuantity,
                    CASE
                        WHEN latestCount.LastCountDate IS NULL THEN NULL
                        ELSE COALESCE(dbo.fn_DepodakiMiktar(product.StockCode, @warehouseNo, latestCount.LastCountDate), 0)
                    END AS SystemQuantityAtCountDate,
                    (
                        SELECT MAX(activityDate)
                        FROM (VALUES
                            (purchase.LastPurchaseDate),
                            (adjustment.LastAdjustmentDate),
                            (orders.LastOrderDate),
                            (shipment.LastShipmentDate),
                            (CAST(latestCount.LastCountDate AS datetime))
                        ) AS activity(activityDate)
                    ) AS LatestActivityDate
                FROM ProductSeed AS product
                OUTER APPLY (
                    SELECT COALESCE(dbo.fn_DepodakiMiktar(product.StockCode, @warehouseNo, @reportDate), 0) AS CurrentStockQuantity
                ) AS currentStock
                LEFT JOIN PurchaseAgg AS purchase ON purchase.StockCode = product.StockCode
                LEFT JOIN LastPurchase AS lastPurchase ON lastPurchase.StockCode = product.StockCode
                LEFT JOIN AdjustmentAgg AS adjustment ON adjustment.StockCode = product.StockCode
                LEFT JOIN LastAdjustment AS lastAdjustment ON lastAdjustment.StockCode = product.StockCode
                LEFT JOIN OrderAgg AS orders ON orders.StockCode = product.StockCode
                LEFT JOIN ShipmentAgg AS shipment ON shipment.StockCode = product.StockCode
                LEFT JOIN LastShipment AS lastShipment ON lastShipment.StockCode = product.StockCode
                LEFT JOIN LatestCount AS latestCount ON latestCount.StockCode = product.StockCode
            )
            SELECT TOP (@take)
                StockCode,
                StockName,
                ModelCode,
                UnitName,
                CurrentStockQuantity,
                PurchaseQuantity,
                PurchaseAmount,
                PurchaseUnitPrice,
                PurchaseDocumentCount,
                LastPurchaseDate,
                LastPurchaseDocument,
                LastSupplierCode,
                LastSupplierName,
                AdjustmentInQuantity,
                AdjustmentOutQuantity,
                AdjustmentNetQuantity,
                AdjustmentDocumentCount,
                LastAdjustmentDate,
                LastAdjustmentDocument,
                LastAdjustmentSeries,
                LastAdjustmentReason,
                OrderMicroQuantity,
                OrderLineCount,
                OrderBranchCount,
                ShipmentQuantity,
                ShipmentDocumentCount,
                ShipmentBranchCount,
                LastShipmentDate,
                LastShipmentDocument,
                LastCountDate,
                LastCountDocumentNo,
                LastCountQuantity,
                SystemQuantityAtCountDate,
                CASE
                    WHEN LastCountQuantity IS NULL OR SystemQuantityAtCountDate IS NULL THEN NULL
                    ELSE LastCountQuantity - SystemQuantityAtCountDate
                END AS CountDifferenceAtCountDate,
                LatestActivityDate
            FROM JoinedRows
            WHERE @onlyWithActivity = 0
               OR ABS(CurrentStockQuantity) > 0.0001
               OR ABS(PurchaseQuantity) > 0.0001
               OR ABS(AdjustmentInQuantity) > 0.0001
               OR ABS(AdjustmentOutQuantity) > 0.0001
               OR ABS(OrderMicroQuantity) > 0.0001
               OR ABS(ShipmentQuantity) > 0.0001
               OR LastCountDate IS NOT NULL
            ORDER BY
                CASE WHEN LatestActivityDate IS NULL THEN 1 ELSE 0 END,
                LatestActivityDate DESC,
                StockName,
                StockCode
            OPTION (RECOMPILE);
            """;

        return await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@warehouseNo", request.WarehouseNo, DbType.Int32);
                AddParameter(command, "@startDate", request.StartDate, DbType.DateTime);
                AddParameter(command, "@endDateExclusive", request.EndDateExclusive, DbType.DateTime);
                AddParameter(command, "@reportDate", request.EndDate, DbType.DateTime);
                AddParameter(command, "@typeCode", request.TypeCode, DbType.String);
                AddParameter(command, "@search", request.Search, DbType.String);
                AddParameter(command, "@searchLike", ToLike(request.Search), DbType.String);
                AddParameter(command, "@onlyWithActivity", request.OnlyWithActivity, DbType.Boolean);
                AddParameter(command, "@take", request.Take, DbType.Int32);
            },
            ReadOperationRow,
            cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, GreenGrocerOperationOrderCaseInfo>> GetOrderCaseInfoByStockCodeAsync(
        NormalizedOverviewRequest request,
        IReadOnlyCollection<string> stockCodes,
        CancellationToken cancellationToken)
    {
        var normalizedStockCodes = stockCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedStockCodes.Length == 0)
        {
            return new Dictionary<string, GreenGrocerOperationOrderCaseInfo>(StringComparer.OrdinalIgnoreCase);
        }

        var snapshots = await authDbContext.GreenGrocerOrderLineSnapshots
            .AsNoTracking()
            .Where(snapshot =>
                snapshot.SourceWarehouseNo == request.WarehouseNo &&
                snapshot.OrderDate >= request.StartDate &&
                snapshot.OrderDate < request.EndDateExclusive &&
                normalizedStockCodes.Contains(snapshot.StockCode))
            .Select(snapshot => new
            {
                snapshot.StockCode,
                snapshot.InputQuantity,
                snapshot.EstimatedQuantity
            })
            .ToListAsync(cancellationToken);

        return snapshots
            .GroupBy(snapshot => snapshot.StockCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => new GreenGrocerOperationOrderCaseInfo(
                    Round(group.Sum(item => item.InputQuantity)),
                    Round(group.Sum(item => item.EstimatedQuantity))),
                StringComparer.OrdinalIgnoreCase);
    }

    private async Task<string> GetWarehouseNameAsync(int warehouseNo, CancellationToken cancellationToken)
    {
        var warehouseName = await mikroDbContext.DEPOLARs
            .AsNoTracking()
            .Where(warehouse => warehouse.dep_no == warehouseNo)
            .Select(warehouse => warehouse.dep_adi)
            .FirstOrDefaultAsync(cancellationToken);

        return NormalizeText(warehouseName, 100);
    }

    private async Task EnsureGreenGrocerStocksAsync(
        IReadOnlyCollection<GreenGrocerOperationsAdjustmentLineRequest> lines,
        CancellationToken cancellationToken)
    {
        var stockCodes = lines
            .Select(line => NormalizeText(line.StockCode, 25))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var stocks = await mikroWriteDbContext.STOKLARs
            .AsNoTracking()
            .Where(stock => stockCodes.Contains(stock.sto_kod))
            .Select(stock => new
            {
                stock.sto_kod,
                stock.sto_iptal,
                stock.sto_model_kodu
            })
            .ToListAsync(cancellationToken);
        var stockByCode = stocks.ToDictionary(
            stock => stock.sto_kod,
            StringComparer.OrdinalIgnoreCase);

        foreach (var stockCode in stockCodes)
        {
            if (!stockByCode.TryGetValue(stockCode, out var stock))
            {
                throw new KeyNotFoundException($"Stock code '{stockCode}' was not found.");
            }

            if (stock.sto_iptal == true)
            {
                throw new InvalidOperationException($"Stock code '{stockCode}' is deleted/passive.");
            }

            if (string.IsNullOrWhiteSpace(stock.sto_model_kodu) ||
                !GreenGrocerModelCodes.Contains(stock.sto_model_kodu))
            {
                throw new InvalidOperationException(
                    $"Stock code '{stockCode}' is not a green grocer model product.");
            }
        }
    }

    private async Task<int> GetNextDocumentOrderNoAsync(
        string documentSerie,
        byte movementType,
        byte documentType,
        CancellationToken cancellationToken)
    {
        var currentMax = await mikroWriteDbContext.STOK_HAREKETLERIs
            .Where(movement =>
                movement.sth_evraktip == documentType &&
                movement.sth_tip == movementType &&
                movement.sth_normal_iade == NormalMovement &&
                movement.sth_cins == AdjustmentGenre &&
                movement.sth_evrakno_seri == documentSerie)
            .MaxAsync(movement => movement.sth_evrakno_sira, cancellationToken);

        return currentMax.HasValue ? currentMax.Value + 1 : FirstDocumentOrderNo;
    }

    private async Task<GreenGrocerOperationsAdjustmentApplyResponse?> TryRecoverAdjustmentResponseAsync(
        GreenGrocerOperationsAdjustmentApplyRequest request,
        CancellationToken cancellationToken)
    {
        var traceKey = MobileOfflineSyncService.ToTraceKey(request.ClientRequestId);
        var rows = await mikroWriteDbContext.STOK_HAREKETLERIs
            .AsNoTracking()
            .Where(movement =>
                movement.sth_cins == AdjustmentGenre &&
                movement.sth_normal_iade == NormalMovement &&
                movement.sth_aciklama != null &&
                movement.sth_aciklama.Contains(traceKey) &&
                (
                    movement.sth_giris_depo_no == request.WarehouseNo ||
                    movement.sth_cikis_depo_no == request.WarehouseNo
                ))
            .Select(movement => new
            {
                movement.sth_Guid,
                movement.sth_tarih,
                movement.sth_belge_tarih,
                movement.sth_belge_no,
                movement.sth_evrakno_seri,
                movement.sth_evrakno_sira,
                movement.sth_tip,
                movement.sth_evraktip,
                movement.sth_giris_depo_no,
                movement.sth_cikis_depo_no,
                movement.sth_miktar,
                movement.sth_tutar
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return null;
        }

        var headerCount = rows
            .Select(row => new
            {
                row.sth_evrakno_seri,
                row.sth_evrakno_sira,
                row.sth_tip,
                row.sth_evraktip
            })
            .Distinct()
            .Count();

        if (headerCount > 1)
        {
            throw new InvalidOperationException(
                "More than one green grocer adjustment document matched the same clientRequestId trace.");
        }

        var firstRow = rows[0];
        var direction = firstRow.sth_tip == IncreaseMovementType ? "increase" : "decrease";
        var directionInfo = ResolveDirection(direction);

        return new GreenGrocerOperationsAdjustmentApplyResponse(
            request.ClientRequestId,
            "Completed",
            request.WarehouseNo,
            directionInfo.Direction == "increase"
                ? firstRow.sth_cikis_depo_no ?? request.CounterWarehouseNo
                : firstRow.sth_giris_depo_no ?? request.CounterWarehouseNo,
            directionInfo.Direction,
            firstRow.sth_evrakno_seri ?? directionInfo.DefaultDocumentSerie,
            firstRow.sth_evrakno_sira ?? 0,
            firstRow.sth_tarih?.Date ?? DateTime.Today,
            firstRow.sth_belge_tarih?.Date ?? firstRow.sth_tarih?.Date ?? DateTime.Today,
            firstRow.sth_belge_no ?? string.Empty,
            NormalizeReasonCode(request.ReasonCode),
            ResolveReasonName(request.ReasonCode),
            rows.Count,
            Round(rows.Sum(row => row.sth_miktar ?? 0d)),
            Round(rows.Sum(row => row.sth_tutar ?? 0d)),
            mikroWriteOptions.Value.ConnectionStringName,
            rows.Select(row => row.sth_Guid).ToArray());
    }

    private async Task TryMarkFailedAsync(
        Guid requestedByUserId,
        Guid clientRequestId,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            await mobileOfflineSyncService.MarkFailedAsync(
                OfflineOperationCode,
                requestedByUserId,
                clientRequestId,
                errorMessage,
                cancellationToken);
        }
        catch
        {
            // Best effort only; preserve the original business exception.
        }
    }

    private static GreenGrocerOperationsProductItemDto BuildItem(
        GreenGrocerOperationsRawRow row,
        GreenGrocerOperationOrderCaseInfo? caseInfo)
    {
        var orderInputQuantity = caseInfo?.InputQuantity ?? 0d;
        var orderEstimatedQuantity = caseInfo?.EstimatedQuantity ?? row.OrderMicroQuantity;
        double? countDifference = row.CountDifferenceAtCountDate.HasValue
            ? Round(row.CountDifferenceAtCountDate.Value)
            : null;
        var flags = BuildFlags(row, caseInfo, countDifference);
        var status = ResolveStatus(row, orderEstimatedQuantity, countDifference);

        return new GreenGrocerOperationsProductItemDto(
            row.StockCode,
            row.StockName,
            row.ModelCode,
            row.UnitName,
            Round(row.CurrentStockQuantity),
            Round(row.PurchaseQuantity),
            Round(row.PurchaseAmount),
            Round(row.PurchaseUnitPrice),
            row.PurchaseDocumentCount,
            row.LastPurchaseDate,
            row.LastPurchaseDocument,
            row.LastSupplierCode,
            row.LastSupplierName,
            Round(row.AdjustmentInQuantity),
            Round(row.AdjustmentOutQuantity),
            Round(row.AdjustmentNetQuantity),
            row.AdjustmentDocumentCount,
            row.LastAdjustmentDate,
            row.LastAdjustmentDocument,
            row.LastAdjustmentSeries,
            row.LastAdjustmentReason,
            Round(orderInputQuantity),
            Round(orderEstimatedQuantity),
            Round(row.OrderMicroQuantity),
            row.OrderLineCount,
            row.OrderBranchCount,
            Round(row.ShipmentQuantity),
            row.ShipmentDocumentCount,
            row.ShipmentBranchCount,
            row.LastShipmentDate,
            row.LastShipmentDocument,
            row.LastCountDate,
            row.LastCountDocumentNo,
            row.LastCountQuantity.HasValue ? Round(row.LastCountQuantity.Value) : (double?)null,
            row.SystemQuantityAtCountDate.HasValue ? Round(row.SystemQuantityAtCountDate.Value) : (double?)null,
            countDifference,
            status.Code,
            status.Name,
            flags);
    }

    private static IReadOnlyCollection<string> BuildFlags(
        GreenGrocerOperationsRawRow row,
        GreenGrocerOperationOrderCaseInfo? caseInfo,
        double? countDifference)
    {
        var flags = new List<string>();

        AddFlag(flags, row.CurrentStockQuantity, "current-stock");
        AddFlag(flags, row.PurchaseQuantity, "purchase");
        AddFlag(flags, row.AdjustmentInQuantity, "adjustment-in");
        AddFlag(flags, row.AdjustmentOutQuantity, "adjustment-out");
        AddFlag(flags, row.OrderMicroQuantity, "order");
        AddFlag(flags, row.ShipmentQuantity, "shipment");

        if (row.OrderMicroQuantity > 0 && caseInfo is null)
        {
            flags.Add("order-case-snapshot-missing");
        }

        if (row.LastCountDate.HasValue)
        {
            flags.Add("count");
        }

        if (Math.Abs(countDifference ?? 0d) > 0.01d)
        {
            flags.Add("count-difference");
        }

        if (flags.Count == 0)
        {
            flags.Add("no-activity");
        }

        return flags;
    }

    private static void AddFlag(List<string> flags, double value, string flag)
    {
        if (Math.Abs(value) > 0.0001d)
        {
            flags.Add(flag);
        }
    }

    private static OperationStatus ResolveStatus(
        GreenGrocerOperationsRawRow row,
        double orderEstimatedQuantity,
        double? countDifference)
    {
        if (Math.Abs(countDifference ?? 0d) > 0.01d)
        {
            return new OperationStatus("count-difference", "Sayim Farki Var");
        }

        if (row.PurchaseQuantity > 0 && Math.Abs(row.AdjustmentNetQuantity) > 0.0001d)
        {
            return new OperationStatus("purchase-adjusted", "Alis ve Tartim Farki Islenmis");
        }

        if (row.PurchaseQuantity > 0)
        {
            return new OperationStatus("purchase-without-adjustment", "Alis Var");
        }

        if (orderEstimatedQuantity > 0 && row.ShipmentQuantity <= 0)
        {
            return new OperationStatus("order-waiting-shipment", "Siparis Sevk Bekliyor");
        }

        if (row.ShipmentQuantity > 0)
        {
            return new OperationStatus("shipment-done", "Sevk Var");
        }

        if (Math.Abs(row.CurrentStockQuantity) > 0.0001d)
        {
            return new OperationStatus("stock-only", "Stok Var");
        }

        return new OperationStatus("watch", "Izle");
    }

    private static STOK_HAREKETLERI CreateAdjustmentMovement(
        NormalizedAdjustmentRequest request,
        GreenGrocerOperationsAdjustmentLineRequest line,
        int rowNo,
        DateTime now,
        DateTime movementDate,
        DateTime documentDate,
        string documentNo,
        int documentOrderNo,
        string creator,
        string acceptor,
        string headerDescription,
        string traceKey)
    {
        var amount = line.Quantity * line.UnitPrice;
        var lineDescription = CreateTraceDescription(line.Description ?? headerDescription, traceKey);

        return new STOK_HAREKETLERI
        {
            sth_Guid = Guid.NewGuid(),
            sth_DBCno = 0,
            sth_SpecRECno = 0,
            sth_iptal = false,
            sth_fileid = MovementFileId,
            sth_hidden = false,
            sth_kilitli = false,
            sth_degisti = false,
            sth_checksum = 0,
            sth_create_user = MikroUserNo,
            sth_create_date = now,
            sth_lastup_user = MikroUserNo,
            sth_lastup_date = now,
            sth_special1 = string.Empty,
            sth_special2 = string.Empty,
            sth_special3 = string.Empty,
            sth_firmano = 0,
            sth_subeno = 0,
            sth_tarih = movementDate,
            sth_tip = request.MovementType,
            sth_cins = request.MovementGenre,
            sth_normal_iade = NormalMovement,
            sth_evraktip = request.DocumentType,
            sth_evrakno_seri = request.DocumentSerie,
            sth_evrakno_sira = documentOrderNo,
            sth_satirno = rowNo,
            sth_belge_no = documentNo,
            sth_belge_tarih = documentDate,
            sth_stok_kod = NormalizeText(line.StockCode, 25),
            sth_isk_mas1 = 0,
            sth_isk_mas2 = 1,
            sth_isk_mas3 = 1,
            sth_isk_mas4 = 1,
            sth_isk_mas5 = 1,
            sth_isk_mas6 = 1,
            sth_isk_mas7 = 1,
            sth_isk_mas8 = 1,
            sth_isk_mas9 = 1,
            sth_isk_mas10 = 1,
            sth_sat_iskmas1 = false,
            sth_sat_iskmas2 = false,
            sth_sat_iskmas3 = false,
            sth_sat_iskmas4 = false,
            sth_sat_iskmas5 = false,
            sth_sat_iskmas6 = false,
            sth_sat_iskmas7 = false,
            sth_sat_iskmas8 = false,
            sth_sat_iskmas9 = false,
            sth_sat_iskmas10 = false,
            sth_pos_satis = 0,
            sth_promosyon_fl = false,
            sth_cari_cinsi = 0,
            sth_cari_kodu = string.Empty,
            sth_cari_grup_no = 0,
            sth_isemri_gider_kodu = string.Empty,
            sth_plasiyer_kodu = string.Empty,
            sth_har_doviz_cinsi = 0,
            sth_har_doviz_kuru = 1d,
            sth_alt_doviz_kuru = 0d,
            sth_stok_doviz_cinsi = 0,
            sth_stok_doviz_kuru = 1d,
            sth_miktar = line.Quantity,
            sth_miktar2 = 0d,
            sth_birim_pntr = Convert.ToByte(line.UnitPointer),
            sth_tutar = amount,
            sth_iskonto1 = 0d,
            sth_iskonto2 = 0d,
            sth_iskonto3 = 0d,
            sth_iskonto4 = 0d,
            sth_iskonto5 = 0d,
            sth_iskonto6 = 0d,
            sth_masraf1 = 0d,
            sth_masraf2 = 0d,
            sth_masraf3 = 0d,
            sth_masraf4 = 0d,
            sth_vergi_pntr = 0,
            sth_vergi = 0d,
            sth_masraf_vergi_pntr = 0,
            sth_masraf_vergi = 0d,
            sth_netagirlik = 0d,
            sth_odeme_op = 0,
            sth_aciklama = lineDescription,
            sth_sip_uid = Guid.Empty,
            sth_fat_uid = Guid.Empty,
            sth_giris_depo_no = request.Direction == "increase" ? request.WarehouseNo : request.CounterWarehouseNo,
            sth_cikis_depo_no = request.Direction == "increase" ? request.CounterWarehouseNo : request.WarehouseNo,
            sth_malkbl_sevk_tarihi = movementDate,
            sth_cari_srm_merkezi = string.Empty,
            sth_stok_srm_merkezi = string.Empty,
            sth_fis_tarihi = MikroEmptyDate,
            sth_fis_sirano = 0,
            sth_vergisiz_fl = false,
            sth_maliyet_ana = 0d,
            sth_maliyet_alternatif = 0d,
            sth_maliyet_orjinal = 0d,
            sth_adres_no = 1,
            sth_parti_kodu = NormalizeText(line.PartyCode, 25),
            sth_lot_no = line.LotNo,
            sth_kons_uid = Guid.Empty,
            sth_proje_kodu = NormalizeText(line.ProjectCode, 25),
            sth_exim_kodu = string.Empty,
            sth_otv_pntr = 0,
            sth_otv_vergi = 0d,
            sth_brutagirlik = 0d,
            sth_disticaret_turu = 0,
            sth_otvtutari = 0d,
            sth_otvvergisiz_fl = false,
            sth_oiv_pntr = 0,
            sth_oiv_vergi = 0d,
            sth_oivvergisiz_fl = false,
            sth_fiyat_liste_no = 1,
            sth_oivtutari = 0d,
            sth_Tevkifat_turu = 0,
            sth_nakliyedeposu = 0,
            sth_nakliyedurumu = 0,
            sth_yetkili_uid = Guid.Empty,
            sth_taxfree_fl = false,
            sth_ilave_edilecek_kdv = 0d,
            sth_ismerkezi_kodu = string.Empty,
            sth_HareketGrupKodu1 = creator,
            sth_HareketGrupKodu2 = acceptor,
            sth_HareketGrupKodu3 = request.ReasonCode,
            sth_Olcu1 = 0d,
            sth_Olcu2 = 0d,
            sth_Olcu3 = 0d,
            sth_Olcu4 = 0d,
            sth_Olcu5 = 0d,
            sth_FormulMiktarNo = 0,
            sth_FormulMiktar = 0d,
            sth_eirs_senaryo = 0,
            sth_eirs_tipi = 0,
            sth_teslim_tarihi = movementDate,
            sth_matbu_fl = false,
            sth_satis_fiyat_doviz_cinsi = 0,
            sth_satis_fiyat_doviz_kuru = 1d,
            sth_eticaret_kanal_kodu = string.Empty,
            sth_bagli_ithalat_kodu = string.Empty,
            sth_tevkifat_sifirlandi_fl = false
        };
    }

    private static NormalizedOverviewRequest NormalizeOverview(GreenGrocerOperationsOverviewRequest request)
    {
        var warehouseNo = request.WarehouseNo <= 0 ? DefaultWarehouseNo : request.WarehouseNo;
        var endDate = request.EndDate == default ? DateTime.Today : request.EndDate.Date;
        var startDate = request.StartDate == default ? endDate.AddDays(-7) : request.StartDate.Date;

        if (endDate < startDate)
        {
            throw new ArgumentException("End date can not be earlier than start date.", nameof(request.EndDate));
        }

        return new NormalizedOverviewRequest(
            warehouseNo,
            startDate,
            endDate,
            endDate.AddDays(1),
            NormalizeTypeCode(request.TypeCode),
            NormalizeOrNull(request.Search),
            request.OnlyWithActivity,
            NormalizeTake(request.Take));
    }

    private static NormalizedAdjustmentRequest NormalizePreview(
        GreenGrocerOperationsAdjustmentPreviewRequest request)
    {
        var warehouseNo = request.WarehouseNo <= 0 ? DefaultWarehouseNo : request.WarehouseNo;
        var direction = ResolveDirection(request.Direction);
        var documentSerie = ResolveDocumentSerie(request.DocumentSerie, direction);
        var lines = NormalizeAdjustmentLines(request.Lines);
        var reasonCode = NormalizeReasonCode(request.ReasonCode);

        return new NormalizedAdjustmentRequest(
            warehouseNo,
            DefaultCounterWarehouseNo,
            direction.Direction,
            direction.DirectionName,
            documentSerie,
            direction.MovementType,
            AdjustmentGenre,
            direction.DocumentType,
            reasonCode,
            ResolveReasonName(reasonCode),
            lines);
    }

    private static NormalizedAdjustmentRequest NormalizeApply(
        GreenGrocerOperationsAdjustmentApplyRequest request)
    {
        var preview = NormalizePreview(new GreenGrocerOperationsAdjustmentPreviewRequest(
            request.WarehouseNo,
            request.Direction,
            request.MovementDate,
            request.DocumentSerie,
            request.ReasonCode,
            request.Lines));
        var counterWarehouseNo = request.CounterWarehouseNo <= 0
            ? DefaultCounterWarehouseNo
            : request.CounterWarehouseNo;

        if (preview.WarehouseNo == counterWarehouseNo)
        {
            throw new ArgumentException("Warehouse no and counter warehouse no can not be the same.");
        }

        if (request.DocumentDate.HasValue &&
            request.MovementDate.HasValue &&
            request.DocumentDate.Value.Date < request.MovementDate.Value.Date)
        {
            throw new ArgumentException("Document date can not be earlier than movement date.", nameof(request.DocumentDate));
        }

        return preview with { CounterWarehouseNo = counterWarehouseNo };
    }

    private static void ValidateApplyIdentity(GreenGrocerOperationsAdjustmentApplyRequest request)
    {
        if (request.RequestedByUserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Current user id was not found.");
        }

        if (request.ClientRequestId == Guid.Empty)
        {
            throw new ArgumentException("Client request id is required.", nameof(request.ClientRequestId));
        }
    }

    private static IReadOnlyCollection<GreenGrocerOperationsAdjustmentLineRequest> NormalizeAdjustmentLines(
        IReadOnlyCollection<GreenGrocerOperationsAdjustmentLineRequest>? lines)
    {
        if (lines is null || lines.Count == 0)
        {
            throw new ArgumentException("At least one adjustment line is required.", nameof(lines));
        }

        return lines
            .Select(line =>
            {
                var stockCode = NormalizeText(line.StockCode, 25);
                if (string.IsNullOrWhiteSpace(stockCode))
                {
                    throw new ArgumentException("Stock code is required.", nameof(lines));
                }

                if (line.Quantity <= 0)
                {
                    throw new ArgumentException("Line quantity must be greater than zero.", nameof(lines));
                }

                if (line.UnitPointer is < 1 or > byte.MaxValue)
                {
                    throw new ArgumentException("Line unit pointer must be between 1 and 255.", nameof(lines));
                }

                if (line.UnitPrice < 0)
                {
                    throw new ArgumentException("Line unit price can not be negative.", nameof(lines));
                }

                if (line.LotNo < 0)
                {
                    throw new ArgumentException("Line lot no can not be negative.", nameof(lines));
                }

                return line with
                {
                    StockCode = stockCode,
                    Description = NormalizeText(line.Description, 50),
                    PartyCode = NormalizeText(line.PartyCode, 25),
                    ProjectCode = NormalizeText(line.ProjectCode, 25)
                };
            })
            .ToArray();
    }

    private static AdjustmentDirection ResolveDirection(string? value)
    {
        var normalized = NormalizeOrNull(value)?.ToLowerInvariant();

        return normalized switch
        {
            "increase" or "artis" or "giris" or "stok-artir" =>
                new AdjustmentDirection("increase", "Stok Artir", IncreaseSerie, IncreaseMovementType, IncreaseDocumentType),
            "decrease" or "azalis" or "cikis" or "stok-azalt" =>
                new AdjustmentDirection("decrease", "Stok Azalt", DecreaseSerie, DecreaseMovementType, DecreaseDocumentType),
            _ => throw new ArgumentException("Adjustment direction must be increase or decrease.", nameof(value))
        };
    }

    private static string ResolveDocumentSerie(string? value, AdjustmentDirection direction)
    {
        var documentSerie = NormalizeText(value, 20).ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(documentSerie))
        {
            return direction.DefaultDocumentSerie;
        }

        var isAllowed = direction.Direction == "increase"
            ? documentSerie is IncreaseSerie or "MNVG" or "MNVI"
            : documentSerie is DecreaseSerie;

        if (!isAllowed)
        {
            throw new ArgumentException(
                $"Document serie '{documentSerie}' is not valid for {direction.Direction} adjustment.",
                nameof(value));
        }

        return documentSerie;
    }

    private static string? NormalizeTypeCode(string? value)
    {
        var normalized = NormalizeOrNull(value)?.ToLowerInvariant();

        return normalized switch
        {
            null or "all" or "tum" or "tumu" => null,
            "10" => "10",
            "11" => "11",
            "12" or "green" or "greens" or "yesillik" => "12",
            "23" => "23",
            _ => throw new ArgumentException("Unsupported green grocer type code.")
        };
    }

    private static int NormalizeTake(int take) =>
        take <= 0 ? DefaultTake : Math.Min(take, MaxTake);

    private static string NormalizeReasonCode(string? value) =>
        NormalizeText(value, 25).ToLowerInvariant() switch
        {
            "" => DefaultReasonCode,
            var reasonCode => reasonCode
        };

    private static string ResolveReasonName(string? value) =>
        NormalizeReasonCode(value) switch
        {
            "weighing-difference" => "Hal faturasi / ic tartim farki",
            "manual-correction" => "Manuel stok duzeltme",
            "count-review" => "Sayim kontrol duzeltmesi",
            var reasonCode => reasonCode
        };

    private static string CreateTraceDescription(string? description, string traceKey)
    {
        var normalizedDescription = NormalizeText(description, 50);
        if (string.IsNullOrWhiteSpace(normalizedDescription))
        {
            return NormalizeText($"API {traceKey}", 50);
        }

        if (normalizedDescription.Contains(traceKey, StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeText(normalizedDescription, 50);
        }

        var maxDescriptionLength = Math.Max(0, 50 - traceKey.Length - 1);
        var trimmedDescription = normalizedDescription.Length <= maxDescriptionLength
            ? normalizedDescription
            : normalizedDescription[..maxDescriptionLength];

        return NormalizeText($"{trimmedDescription} {traceKey}", 50);
    }

    private static string? NormalizeOrNull(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string NormalizeText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? ToLike(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : $"%{value.Trim()}%";

    private async Task<IReadOnlyCollection<T>> ExecuteReaderAsync<T>(
        string sql,
        Action<DbCommand> configureCommand,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken)
    {
        var items = new List<T>();
        var connection = mikroDbContext.Database.GetDbConnection();
        var closeConnection = connection.State == ConnectionState.Closed;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 300;
            configureCommand(command);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(map(reader));
            }
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }

        return items;
    }

    private static GreenGrocerOperationsRawRow ReadOperationRow(DbDataReader reader) =>
        new(
            ReadString(reader, "StockCode"),
            ReadString(reader, "StockName"),
            ReadString(reader, "ModelCode"),
            ReadString(reader, "UnitName"),
            ReadDouble(reader, "CurrentStockQuantity"),
            ReadDouble(reader, "PurchaseQuantity"),
            ReadDouble(reader, "PurchaseAmount"),
            ReadDouble(reader, "PurchaseUnitPrice"),
            ReadInt(reader, "PurchaseDocumentCount"),
            ReadNullableDateTime(reader, "LastPurchaseDate"),
            ReadString(reader, "LastPurchaseDocument"),
            ReadString(reader, "LastSupplierCode"),
            ReadString(reader, "LastSupplierName"),
            ReadDouble(reader, "AdjustmentInQuantity"),
            ReadDouble(reader, "AdjustmentOutQuantity"),
            ReadDouble(reader, "AdjustmentNetQuantity"),
            ReadInt(reader, "AdjustmentDocumentCount"),
            ReadNullableDateTime(reader, "LastAdjustmentDate"),
            ReadString(reader, "LastAdjustmentDocument"),
            ReadString(reader, "LastAdjustmentSeries"),
            ReadString(reader, "LastAdjustmentReason"),
            ReadDouble(reader, "OrderMicroQuantity"),
            ReadInt(reader, "OrderLineCount"),
            ReadInt(reader, "OrderBranchCount"),
            ReadDouble(reader, "ShipmentQuantity"),
            ReadInt(reader, "ShipmentDocumentCount"),
            ReadInt(reader, "ShipmentBranchCount"),
            ReadNullableDateTime(reader, "LastShipmentDate"),
            ReadString(reader, "LastShipmentDocument"),
            ReadNullableDateTime(reader, "LastCountDate"),
            ReadNullableInt(reader, "LastCountDocumentNo"),
            ReadNullableDouble(reader, "LastCountQuantity"),
            ReadNullableDouble(reader, "SystemQuantityAtCountDate"),
            ReadNullableDouble(reader, "CountDifferenceAtCountDate"));

    private static void AddParameter(DbCommand command, string name, object? value, DbType dbType)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static int ReadInt(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? 0
            : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static int? ReadNullableInt(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static double ReadDouble(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? 0d
            : Convert.ToDouble(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static double? ReadNullableDouble(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToDouble(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static DateTime? ReadNullableDateTime(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToDateTime(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static string ReadString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? string.Empty
            : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
    }

    private static double Round(double value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed record NormalizedOverviewRequest(
        int WarehouseNo,
        DateTime StartDate,
        DateTime EndDate,
        DateTime EndDateExclusive,
        string? TypeCode,
        string? Search,
        bool OnlyWithActivity,
        int Take);

    private sealed record NormalizedAdjustmentRequest(
        int WarehouseNo,
        int CounterWarehouseNo,
        string Direction,
        string DirectionName,
        string DocumentSerie,
        byte MovementType,
        byte MovementGenre,
        byte DocumentType,
        string ReasonCode,
        string ReasonName,
        IReadOnlyCollection<GreenGrocerOperationsAdjustmentLineRequest> Lines);

    private sealed record AdjustmentDirection(
        string Direction,
        string DirectionName,
        string DefaultDocumentSerie,
        byte MovementType,
        byte DocumentType);

    private sealed record GreenGrocerOperationsRawRow(
        string StockCode,
        string StockName,
        string ModelCode,
        string UnitName,
        double CurrentStockQuantity,
        double PurchaseQuantity,
        double PurchaseAmount,
        double PurchaseUnitPrice,
        int PurchaseDocumentCount,
        DateTime? LastPurchaseDate,
        string LastPurchaseDocument,
        string LastSupplierCode,
        string LastSupplierName,
        double AdjustmentInQuantity,
        double AdjustmentOutQuantity,
        double AdjustmentNetQuantity,
        int AdjustmentDocumentCount,
        DateTime? LastAdjustmentDate,
        string LastAdjustmentDocument,
        string LastAdjustmentSeries,
        string LastAdjustmentReason,
        double OrderMicroQuantity,
        int OrderLineCount,
        int OrderBranchCount,
        double ShipmentQuantity,
        int ShipmentDocumentCount,
        int ShipmentBranchCount,
        DateTime? LastShipmentDate,
        string LastShipmentDocument,
        DateTime? LastCountDate,
        int? LastCountDocumentNo,
        double? LastCountQuantity,
        double? SystemQuantityAtCountDate,
        double? CountDifferenceAtCountDate);

    private sealed record GreenGrocerOperationOrderCaseInfo(
        double InputQuantity,
        double EstimatedQuantity);

    private sealed record OperationStatus(
        string Code,
        string Name);
}



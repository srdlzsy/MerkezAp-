using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Text;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.UrunDagilimlari;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.UrunDagilimlari;

public sealed class ProductDistributionService(
    MikroDbContext mikroDbContext,
    MikroWriteDbContext mikroWriteDbContext,
    FurpaDbContext furpaDbContext,
    IProductDistributionNotificationMailer notificationMailer)
    : IProductDistributionService
{
    private const int DefaultSalesDayCount = 42;
    private const int MinSalesDayCount = 1;
    private const int MaxSalesDayCount = 365;
    private const int DefaultTake = 100;
    private const int MaxTake = 500;
    private const string DefaultQuantityUnitName = "adet";
    private const string CaseUnitName = "koli";
    private const int FirstDocumentOrderNo = 0;
    private const int LongRunningQueryTimeoutSeconds = 300;
    private const string FinalizeDescriptionPrefix = "Dagilim";
    private static readonly int[] KnownDistributionCenters = [50, 53, 56];
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    public async Task<IReadOnlyCollection<ProductDistributionCenterDto>> GetDistributionCentersAsync(
        CancellationToken cancellationToken)
    {
        var centers = await mikroDbContext.DEPOLARs
            .AsNoTracking()
            .Where(warehouse =>
                warehouse.dep_no.HasValue &&
                warehouse.dep_no.Value > 0 &&
                warehouse.dep_iptal != true &&
                warehouse.dep_envanter_harici_fl != true &&
                (KnownDistributionCenters.Contains(warehouse.dep_no.Value) || warehouse.dep_no.Value < 100))
            .OrderBy(warehouse => warehouse.dep_no)
            .Select(warehouse => new ProductDistributionCenterDto(
                warehouse.dep_no!.Value,
                warehouse.dep_adi ?? $"Depo {warehouse.dep_no.Value}",
                warehouse.dep_bolge_kodu))
            .ToListAsync(cancellationToken);

        return centers;
    }

    public async Task<ProductDistributionProposalDto> CreateProposalAsync(
        ProductDistributionProposalRequest request,
        CancellationToken cancellationToken)
    {
        ValidateProposalRequest(request);

        var stockCode = NormalizeStockCode(request.StockCode);
        var salesDayCount = ClampSalesDayCount(request.SalesDayCount);
        var referenceDate = (request.ReferenceDate ?? DateTime.Today).Date;
        var periodStart = referenceDate.AddDays(-salesDayCount + 1);
        var periodEndExclusive = referenceDate.AddDays(1);

        var stock = await GetStockAsync(stockCode, cancellationToken);
        var distributionCenter = await GetWarehouseAsync(request.DistributionCenterWarehouseNo, cancellationToken);
        var salesRows = await GetBranchSalesRowsAsync(
            stockCode,
            periodStart,
            periodEndExclusive,
            referenceDate,
            cancellationToken);

        var warnings = new List<string>();
        if (salesRows.Count == 0)
        {
            warnings.Add("Aktif sube/depo bulunamadi; dagilim satiri uretilmedi.");
        }

        var rowsForAllocation = salesRows
            .Where(row => request.IncludeBranchesWithoutSales || row.LastSalesQuantity > 0d)
            .ToArray();

        if (rowsForAllocation.Length == 0 && salesRows.Count > 0)
        {
            warnings.Add("Secilen donemde satisi olan sube bulunamadi; dagitim miktari elle duzenlenmeli.");
        }

        var totalSales = salesRows.Sum(row => Math.Max(0d, row.LastSalesQuantity));
        var companyAverageDailySales = salesRows.Count == 0 ? 0d : totalSales / salesDayCount / salesRows.Count;
        var allocations = AllocateCases(rowsForAllocation, request.TotalCaseQuantity);
        var allocatedCaseQuantity = allocations.Values.Sum();
        var quantityUnitName = ResolveQuantityUnitName(stock);
        var lines = salesRows
            .Where(row => request.IncludeBranchesWithoutSales || row.LastSalesQuantity > 0d || allocations.ContainsKey(row.WarehouseNo))
            .Select(row =>
            {
                var caseQuantity = allocations.GetValueOrDefault(row.WarehouseNo);
                var reason = caseQuantity > 0
                    ? totalSales > 0d ? "sales-share" : "equal-share"
                    : row.LastSalesQuantity <= 0d
                        ? "no-period-sales"
                        : "rounded-to-zero";
                var regionCode = NormalizeOptionalText(row.RegionCode);

                return new ProductDistributionLineDto(
                    row.WarehouseNo,
                    row.WarehouseName,
                    regionCode,
                    ResolveRegionName(regionCode),
                    Round(row.LastSalesQuantity),
                    Round(row.CurrentStockQuantity),
                    Round(companyAverageDailySales),
                    Round(row.LastSalesQuantity / salesDayCount),
                    CalculatePercent(row.LastSalesQuantity, totalSales),
                    CalculatePercent(caseQuantity, allocatedCaseQuantity),
                    caseQuantity,
                    checked(caseQuantity * stock.PackageFactor),
                    quantityUnitName,
                    CaseUnitName,
                    reason);
            })
            .OrderByDescending(line => line.CaseQuantity)
            .ThenByDescending(line => line.LastSalesQuantity)
            .ThenBy(line => line.RegionCode)
            .ThenBy(line => line.WarehouseNo)
            .ToArray();

        var summary = new ProductDistributionSummaryDto(
            salesDayCount,
            referenceDate,
            lines.Length,
            request.TotalCaseQuantity,
            allocatedCaseQuantity,
            request.TotalCaseQuantity - allocatedCaseQuantity,
            lines.Sum(line => line.UnitQuantity),
            allocatedCaseQuantity == request.TotalCaseQuantity,
            allocatedCaseQuantity == request.TotalCaseQuantity
                ? "Dagilim toplam koli ile dengeli."
                : "Dagilim toplam koli ile dengeli degil; UI tarafinda satirlar duzenlenmeli.");

        return new ProductDistributionProposalDto(
            stock,
            distributionCenter,
            summary,
            lines,
            warnings);
    }


    public async Task<ProductDistributionBalanceDto> BalanceAsync(
        ProductDistributionBalanceRequest request,
        CancellationToken cancellationToken)
    {
        ValidateBalanceRequest(request);

        var stock = await GetStockAsync(NormalizeStockCode(request.StockCode), cancellationToken);
        var salesDayCount = ClampSalesDayCount(request.SalesDayCount);
        var referenceDate = (request.ReferenceDate ?? DateTime.Today).Date;
        var warnings = new List<string>();
        var lines = BalanceLines(request, stock.PackageFactor, ResolveQuantityUnitName(stock), warnings);
        var allocatedCaseQuantity = lines.Sum(line => line.CaseQuantity);
        var caseDifference = request.TargetCaseQuantity - allocatedCaseQuantity;
        var isBalanced = caseDifference == 0;
        var summary = new ProductDistributionSummaryDto(
            salesDayCount,
            referenceDate,
            lines.Count,
            request.TargetCaseQuantity,
            allocatedCaseQuantity,
            caseDifference,
            lines.Sum(line => line.UnitQuantity),
            isBalanced,
            isBalanced
                ? "Dagilim hedef koli ile dengeli."
                : "Dagilim hedef koli ile dengeli degil; kilitli satirlar veya sifir satirlar kontrol edilmeli.");

        return new ProductDistributionBalanceDto(
            stock,
            summary,
            lines,
            warnings);
    }
    public async Task<IReadOnlyCollection<ProductDistributionListItemDto>> ListAsync(
        ProductDistributionListRequest request,
        CancellationToken cancellationToken)
    {
        var take = Math.Clamp(request.Take ?? DefaultTake, 1, MaxTake);
        var rows = await QueryDistributionListRowsAsync(request, take, cancellationToken);

        if (rows.Count == 0)
        {
            return Array.Empty<ProductDistributionListItemDto>();
        }

        var stockCodes = rows.Select(row => row.StockCode).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var warehouseNos = rows.Select(row => row.DistributionCenterWarehouseNo).Distinct().ToArray();
        var stocks = await GetStocksAsync(stockCodes, cancellationToken);
        var warehouses = await GetWarehousesAsync(warehouseNos, cancellationToken);

        return rows
            .Select(row =>
            {
                var stock = stocks.GetValueOrDefault(row.StockCode)
                    ?? new ProductDistributionStockDto(row.StockCode, row.StockCode, null, 1, null);
                var warehouse = warehouses.GetValueOrDefault(row.DistributionCenterWarehouseNo)
                    ?? new ProductDistributionWarehouseDto(
                        row.DistributionCenterWarehouseNo,
                        $"Depo {row.DistributionCenterWarehouseNo}",
                        null);

                return new ProductDistributionListItemDto(
                    row.DocumentNo,
                    GetStatus(row.Status),
                    row.CreatedAt,
                    row.FinalizedAt,
                    stock,
                    warehouse,
                    row.DistributedBy,
                    row.LineCount,
                    row.TotalCaseQuantity,
                    row.TotalUnitQuantity);
            })
            .ToArray();
    }

    public async Task<ProductDistributionDetailDto> GetAsync(
        string documentNo,
        CancellationToken cancellationToken)
    {
        var normalizedDocumentNo = NormalizeDocumentNo(documentNo);
        var rows = await QueryDistributionDocumentRowsAsync(normalizedDocumentNo, cancellationToken);
        if (rows.Count == 0)
        {
            throw new KeyNotFoundException($"Dagilim evraki bulunamadi: {normalizedDocumentNo}");
        }

        return await MapDetailAsync(rows, cancellationToken);
    }

    public async Task<ProductDistributionDetailDto> SaveAsync(
        ProductDistributionSaveRequest request,
        CancellationToken cancellationToken)
    {
        ValidateSaveRequest(request);

        var stock = await GetStockAsync(NormalizeStockCode(request.StockCode), cancellationToken);
        await GetWarehouseAsync(request.DistributionCenterWarehouseNo, cancellationToken);
        var preparedLines = await PrepareSaveLinesAsync(request, stock.PackageFactor, cancellationToken);

        var executionStrategy = furpaDbContext.Database.CreateExecutionStrategy();
        var documentNo = await executionStrategy.ExecuteAsync(async () =>
        {
            await furpaDbContext.Database.OpenConnectionAsync(cancellationToken);
            await using var transaction = await furpaDbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            try
            {
                var nextDocumentNo = await GetNextDistributionDocumentNoAsync(cancellationToken);
                await InsertDistributionRowsAsync(
                    nextDocumentNo,
                    stock.StockCode,
                    request.DistributionCenterWarehouseNo,
                    request.DistributedBy,
                    preparedLines,
                    cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                return nextDocumentNo;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });

        return await GetAsync(documentNo, cancellationToken);
    }

    public async Task<ProductDistributionDetailDto> UpdateAsync(
        string documentNo,
        ProductDistributionSaveRequest request,
        CancellationToken cancellationToken)
    {
        ValidateSaveRequest(request);

        var normalizedDocumentNo = NormalizeDocumentNo(documentNo);
        var existingRows = await QueryDistributionDocumentRowsAsync(normalizedDocumentNo, cancellationToken);
        if (existingRows.Count == 0)
        {
            throw new KeyNotFoundException($"Dagilim evraki bulunamadi: {normalizedDocumentNo}");
        }

        var status = existingRows.Max(row => row.Status);
        if (status != 0)
        {
            throw new InvalidOperationException("Sadece bilgilendirme yapilmamis dagilim kayitlari guncellenebilir.");
        }

        var stock = await GetStockAsync(NormalizeStockCode(request.StockCode), cancellationToken);
        await GetWarehouseAsync(request.DistributionCenterWarehouseNo, cancellationToken);
        var preparedLines = await PrepareSaveLinesAsync(request, stock.PackageFactor, cancellationToken);

        var executionStrategy = furpaDbContext.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            await furpaDbContext.Database.OpenConnectionAsync(cancellationToken);
            await using var transaction = await furpaDbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            try
            {
                await DeleteDistributionRowsAsync(normalizedDocumentNo, cancellationToken);
                await InsertDistributionRowsAsync(
                    normalizedDocumentNo,
                    stock.StockCode,
                    request.DistributionCenterWarehouseNo,
                    request.DistributedBy,
                    preparedLines,
                    cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });

        return await GetAsync(normalizedDocumentNo, cancellationToken);
    }

    public async Task<ProductDistributionNotificationDto> NotifyAsync(
        string documentNo,
        ProductDistributionNotifyRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedDocumentNo = NormalizeDocumentNo(documentNo);
        var detail = await GetAsync(normalizedDocumentNo, cancellationToken);
        if (detail.Header.Status.Code == 2)
        {
            throw new InvalidOperationException("Kesinlesmis dagilim tekrar bilgilendirilemez.");
        }

        var recipients = await QueryRegionManagersAsync(normalizedDocumentNo, cancellationToken);
        var mailResults = notificationMailer.IsEnabled
            ? await SendNotificationMailsAsync(normalizedDocumentNo, detail, recipients, cancellationToken)
            : Array.Empty<ProductDistributionNotificationMailResultDto>();
        var notificationSucceeded = !notificationMailer.IsEnabled ||
            (mailResults.Count > 0 && mailResults.All(result => result.Sent));
        var statusChanged = false;
        var stockOrderingStopped = false;

        if (notificationSucceeded)
        {
            statusChanged = await MarkDistributionStatusAsync(normalizedDocumentNo, 1, cancellationToken) > 0;
            stockOrderingStopped = request.MarkStockOrderingStopped &&
                await MarkStockOrderingStoppedAsync(detail.Header.Stock.StockCode, cancellationToken);
        }

        var refreshedStatus = notificationSucceeded || detail.Header.Status.Code == 1
            ? GetStatus(1)
            : detail.Header.Status;
        var subject = $"Urun dagilimi {normalizedDocumentNo} - {detail.Header.Stock.StockName}";
        var message = BuildNotificationMessage(
            notificationMailer.IsEnabled,
            recipients.Count,
            mailResults,
            statusChanged,
            detail.Header.Status.Code);

        return new ProductDistributionNotificationDto(
            normalizedDocumentNo,
            refreshedStatus,
            statusChanged,
            stockOrderingStopped,
            recipients,
            subject,
            message,
            notificationMailer.IsEnabled,
            mailResults.Count(result => result.Sent),
            mailResults.Count(result => !result.Sent),
            mailResults);
    }

    public async Task<ProductDistributionFinalizeDto> FinalizeAsync(
        string documentNo,
        ProductDistributionFinalizeRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedDocumentNo = NormalizeDocumentNo(documentNo);
        var detail = await GetAsync(normalizedDocumentNo, cancellationToken);

        var positiveLines = detail.Lines
            .Where(line => line.UnitQuantity > 0)
            .OrderBy(line => line.WarehouseNo)
            .ToArray();

        if (positiveLines.Length == 0)
        {
            throw new InvalidOperationException("Kesinlestirilecek adet miktari olan dagilim satiri bulunamadi.");
        }

        var now = DateTime.Now;
        var orderDate = (request.OrderDate ?? DateTime.Today).Date;
        var deliveryDate = (request.DeliveryDate ?? orderDate).Date;
        if (deliveryDate < orderDate)
        {
            throw new ArgumentException("Teslim tarihi siparis tarihinden once olamaz.", nameof(request.DeliveryDate));
        }

        var description = BuildFinalizeDescription(normalizedDocumentNo);
        var orders = await CreateWarehouseOrdersAsync(
            detail,
            positiveLines,
            description,
            orderDate,
            deliveryDate,
            now,
            cancellationToken);

        await MarkDistributionFinalizedAsync(normalizedDocumentNo, now, cancellationToken);

        return new ProductDistributionFinalizeDto(
            normalizedDocumentNo,
            GetStatus(2),
            now,
            orders.Count(order => !order.AlreadyExisted),
            orders.Count(order => order.AlreadyExisted),
            positiveLines.Sum(line => line.UnitQuantity),
            orders);
    }

    public async Task<ProductDistributionDeleteDto> DeleteAsync(
        string documentNo,
        CancellationToken cancellationToken)
    {
        var normalizedDocumentNo = NormalizeDocumentNo(documentNo);
        var rows = await QueryDistributionDocumentRowsAsync(normalizedDocumentNo, cancellationToken);
        if (rows.Count == 0)
        {
            throw new KeyNotFoundException($"Dagilim evraki bulunamadi: {normalizedDocumentNo}");
        }

        if (rows.Max(row => row.Status) != 0)
        {
            throw new InvalidOperationException("Sadece bilgilendirme yapilmamis dagilim kayitlari silinebilir.");
        }

        var deletedRows = await DeleteDistributionRowsAsync(normalizedDocumentNo, cancellationToken);
        return new ProductDistributionDeleteDto(
            normalizedDocumentNo,
            deletedRows > 0,
            deletedRows > 0 ? "Dagilim evraki silindi." : "Silinecek dagilim satiri bulunamadi.");
    }

    private async Task<ProductDistributionDetailDto> MapDetailAsync(
        IReadOnlyCollection<DistributionDocumentRow> rows,
        CancellationToken cancellationToken)
    {
        var first = rows.First();
        var stocks = await GetStocksAsync([first.StockCode], cancellationToken);
        var stock = stocks.GetValueOrDefault(first.StockCode)
            ?? new ProductDistributionStockDto(first.StockCode, first.StockCode, null, 1, null);
        var warehouseNos = rows
            .Select(row => row.WarehouseNo)
            .Append(first.DistributionCenterWarehouseNo)
            .Distinct()
            .ToArray();
        var warehouses = await GetWarehousesAsync(warehouseNos, cancellationToken);
        var center = warehouses.GetValueOrDefault(first.DistributionCenterWarehouseNo)
            ?? new ProductDistributionWarehouseDto(
                first.DistributionCenterWarehouseNo,
                $"Depo {first.DistributionCenterWarehouseNo}",
                null);
        var totalSalesQuantity = rows.Sum(row => Math.Max(0d, row.LastSalesQuantity));
        var totalCaseQuantity = rows.Sum(row => row.CaseQuantity);
        var quantityUnitName = ResolveQuantityUnitName(stock);

        var lines = rows
            .OrderBy(row => row.RegionCode)
            .ThenBy(row => row.WarehouseNo)
            .Select(row =>
            {
                var warehouse = warehouses.GetValueOrDefault(row.WarehouseNo);
                var regionCode = NormalizeOptionalText(warehouse?.RegionCode ?? row.RegionCode);
                return new ProductDistributionLineDto(
                    row.WarehouseNo,
                    warehouse?.WarehouseName ?? $"Depo {row.WarehouseNo}",
                    regionCode,
                    ResolveRegionName(regionCode),
                    Round(row.LastSalesQuantity),
                    0d,
                    Round(row.CompanyAverageDailySales),
                    Round(row.BranchAverageDailySales),
                    CalculatePercent(row.LastSalesQuantity, totalSalesQuantity),
                    CalculatePercent(row.CaseQuantity, totalCaseQuantity),
                    row.CaseQuantity,
                    row.UnitQuantity,
                    quantityUnitName,
                    CaseUnitName,
                    row.UnitQuantity > 0 ? "saved" : "no-allocation");
            })
            .ToArray();

        var summary = new ProductDistributionSummaryDto(
            DefaultSalesDayCount,
            first.CreatedAt.Date,
            lines.Length,
            totalCaseQuantity,
            totalCaseQuantity,
            0,
            lines.Sum(line => line.UnitQuantity),
            true,
            "Kayitli dagilim satirlari dengeli.");

        var header = new ProductDistributionHeaderDto(
            first.DocumentNo,
            GetStatus(first.Status),
            first.CreatedAt,
            first.FinalizedAt,
            stock,
            center,
            first.DistributedBy);

        return new ProductDistributionDetailDto(
            header,
            summary,
            lines,
            CreateActions(first.Status));
    }

    private async Task<ProductDistributionStockDto> GetStockAsync(
        string stockCode,
        CancellationToken cancellationToken)
    {
        var stock = await mikroDbContext.STOKLARs
            .AsNoTracking()
            .Where(item => item.sto_kod == stockCode && item.sto_iptal != true)
            .Select(item => new
            {
                item.sto_kod,
                item.sto_isim,
                item.sto_birim1_ad,
                item.sto_birim2_katsayi
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (stock is null)
        {
            throw new KeyNotFoundException($"Stok bulunamadi: {stockCode}");
        }

        var barcode = await mikroDbContext.BARKOD_TANIMLARIs
            .AsNoTracking()
            .Where(item => item.bar_stokkodu == stockCode && item.bar_iptal != true)
            .OrderByDescending(item => item.bar_master == true)
            .ThenBy(item => item.bar_kodu)
            .Select(item => item.bar_kodu)
            .FirstOrDefaultAsync(cancellationToken);

        return new ProductDistributionStockDto(
            stock.sto_kod,
            stock.sto_isim ?? stock.sto_kod,
            barcode,
            NormalizePackageFactor(stock.sto_birim2_katsayi),
            stock.sto_birim1_ad);
    }

    private async Task<Dictionary<string, ProductDistributionStockDto>> GetStocksAsync(
        IReadOnlyCollection<string> stockCodes,
        CancellationToken cancellationToken)
    {
        if (stockCodes.Count == 0)
        {
            return new Dictionary<string, ProductDistributionStockDto>(StringComparer.OrdinalIgnoreCase);
        }

        var rows = await mikroDbContext.STOKLARs
            .AsNoTracking()
            .Where(item => stockCodes.Contains(item.sto_kod))
            .Select(item => new
            {
                item.sto_kod,
                item.sto_isim,
                item.sto_birim1_ad,
                item.sto_birim2_katsayi
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            item => item.sto_kod,
            item => new ProductDistributionStockDto(
                item.sto_kod,
                item.sto_isim ?? item.sto_kod,
                null,
                NormalizePackageFactor(item.sto_birim2_katsayi),
                item.sto_birim1_ad),
            StringComparer.OrdinalIgnoreCase);
    }

    private async Task<ProductDistributionWarehouseDto> GetWarehouseAsync(
        int warehouseNo,
        CancellationToken cancellationToken)
    {
        var warehouse = await mikroDbContext.DEPOLARs
            .AsNoTracking()
            .Where(item => item.dep_no == warehouseNo && item.dep_iptal != true)
            .Select(item => new ProductDistributionWarehouseDto(
                item.dep_no!.Value,
                item.dep_adi ?? $"Depo {item.dep_no.Value}",
                item.dep_bolge_kodu))
            .FirstOrDefaultAsync(cancellationToken);

        return warehouse ?? throw new KeyNotFoundException($"Depo bulunamadi: {warehouseNo}");
    }

    private async Task<Dictionary<int, ProductDistributionWarehouseDto>> GetWarehousesAsync(
        IReadOnlyCollection<int> warehouseNos,
        CancellationToken cancellationToken)
    {
        if (warehouseNos.Count == 0)
        {
            return new Dictionary<int, ProductDistributionWarehouseDto>();
        }

        var rows = await mikroDbContext.DEPOLARs
            .AsNoTracking()
            .Where(item => item.dep_no.HasValue && warehouseNos.Contains(item.dep_no.Value))
            .Select(item => new ProductDistributionWarehouseDto(
                item.dep_no!.Value,
                item.dep_adi ?? $"Depo {item.dep_no.Value}",
                item.dep_bolge_kodu))
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(item => item.WarehouseNo);
    }

    private async Task<IReadOnlyCollection<BranchSalesRow>> GetBranchSalesRowsAsync(
        string stockCode,
        DateTime periodStart,
        DateTime periodEndExclusive,
        DateTime referenceDate,
        CancellationToken cancellationToken)
    {
        await mikroDbContext.Database.OpenConnectionAsync(cancellationToken);
        var connection = mikroDbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandTimeout = LongRunningQueryTimeoutSeconds;
        command.CommandText = """
            WITH Sales AS (
                SELECT
                    movement.sth_cikis_depo_no AS WarehouseNo,
                    SUM(movement.sth_miktar) AS LastSalesQuantity
                FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
                WHERE movement.sth_stok_kod = @stockCode
                  AND movement.sth_tarih >= @periodStart
                  AND movement.sth_tarih < @periodEndExclusive
                  AND movement.sth_tip = 1
                  AND movement.sth_cins = 1
                  AND COALESCE(movement.sth_normal_iade, 0) = 0
                  AND movement.sth_cikis_depo_no > 100
                GROUP BY movement.sth_cikis_depo_no
            )
            SELECT
                warehouse.dep_no AS WarehouseNo,
                warehouse.dep_adi AS WarehouseName,
                warehouse.dep_bolge_kodu AS RegionCode,
                COALESCE(sales.LastSalesQuantity, 0) AS LastSalesQuantity,
                COALESCE(dbo.fn_DepodakiMiktar(@stockCode, warehouse.dep_no, @referenceDate), 0) AS CurrentStockQuantity
            FROM dbo.DEPOLAR AS warehouse WITH (NOLOCK)
            LEFT JOIN Sales AS sales
                ON sales.WarehouseNo = warehouse.dep_no
            WHERE warehouse.dep_no > 100
              AND COALESCE(warehouse.dep_iptal, 0) = 0
              AND COALESCE(warehouse.dep_envanter_harici_fl, 0) = 0
            ORDER BY warehouse.dep_bolge_kodu, warehouse.dep_no
            OPTION (RECOMPILE);
            """;
        AddParameter(command, "@stockCode", stockCode);
        AddParameter(command, "@periodStart", periodStart);
        AddParameter(command, "@periodEndExclusive", periodEndExclusive);
        AddParameter(command, "@referenceDate", referenceDate);

        var rows = new List<BranchSalesRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new BranchSalesRow(
                GetInt32(reader, "WarehouseNo"),
                GetString(reader, "WarehouseName") ?? $"Depo {GetInt32(reader, "WarehouseNo")}",
                GetString(reader, "RegionCode"),
                GetDouble(reader, "LastSalesQuantity"),
                GetDouble(reader, "CurrentStockQuantity")));
        }

        return rows;
    }

    private async Task<IReadOnlyCollection<DistributionListRow>> QueryDistributionListRowsAsync(
        ProductDistributionListRequest request,
        int take,
        CancellationToken cancellationToken)
    {
        await furpaDbContext.Database.OpenConnectionAsync(cancellationToken);
        var connection = furpaDbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (@take)
                Evrak_No AS DocumentNo,
                Stok_Kodu AS StockCode,
                COALESCE(TRY_CONVERT(int, NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(50), Dagitim_Merkezi))), '')), 0) AS DistributionCenterWarehouseNo,
                COALESCE(MAX(TRY_CONVERT(int, NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(50), Durum))), ''))), 0) AS Status,
                MIN(Kayit_Tarihi) AS CreatedAt,
                MAX(TRY_CONVERT(datetime, NULLIF(Kesinlestirme_Tarihi, ''))) AS FinalizedAt,
                MAX(Dagilimi_Yapan) AS DistributedBy,
                COUNT(1) AS LineCount,
                COALESCE(SUM(TRY_CONVERT(int, TRY_CONVERT(decimal(18, 4), REPLACE(NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(50), Dagilim_Koli_Miktar))), ''), ',', '.')))), 0) AS TotalCaseQuantity,
                COALESCE(SUM(TRY_CONVERT(int, TRY_CONVERT(decimal(18, 4), REPLACE(NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(50), Dagilim_Adet_Miktar))), ''), ',', '.')))), 0) AS TotalUnitQuantity
            FROM dbo.STOK_DAGILIM WITH (NOLOCK)
            WHERE (@status IS NULL OR COALESCE(TRY_CONVERT(int, NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(50), Durum))), '')), 0) = @status)
              AND (@documentNo IS NULL OR Evrak_No = @documentNo)
              AND (@stockCode IS NULL OR Stok_Kodu = @stockCode)
              AND (@distributionCenterWarehouseNo IS NULL OR COALESCE(TRY_CONVERT(int, NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(50), Dagitim_Merkezi))), '')), 0) = @distributionCenterWarehouseNo)
              AND (@createdFrom IS NULL OR Kayit_Tarihi >= @createdFrom)
              AND (@createdToExclusive IS NULL OR Kayit_Tarihi < @createdToExclusive)
            GROUP BY Evrak_No, Stok_Kodu, Dagitim_Merkezi
            ORDER BY MIN(Kayit_Tarihi) DESC, TRY_CONVERT(int, Evrak_No) DESC, Evrak_No DESC;
            """;
        AddParameter(command, "@take", take);
        AddParameter(command, "@status", request.Status);
        AddParameter(command, "@documentNo", NormalizeOptionalText(request.DocumentNo));
        AddParameter(command, "@stockCode", NormalizeOptionalText(request.StockCode)?.ToUpperInvariant());
        AddParameter(command, "@distributionCenterWarehouseNo", request.DistributionCenterWarehouseNo);
        AddParameter(command, "@createdFrom", request.CreatedFrom?.Date);
        AddParameter(command, "@createdToExclusive", request.CreatedTo?.Date.AddDays(1));

        var rows = new List<DistributionListRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new DistributionListRow(
                GetString(reader, "DocumentNo") ?? string.Empty,
                GetString(reader, "StockCode") ?? string.Empty,
                GetInt32(reader, "DistributionCenterWarehouseNo"),
                GetInt32(reader, "Status"),
                GetDateTime(reader, "CreatedAt") ?? DateTime.MinValue,
                GetDateTime(reader, "FinalizedAt"),
                GetString(reader, "DistributedBy"),
                GetInt32(reader, "LineCount"),
                GetInt32(reader, "TotalCaseQuantity"),
                GetInt32(reader, "TotalUnitQuantity")));
        }

        return rows;
    }

    private async Task<IReadOnlyCollection<DistributionDocumentRow>> QueryDistributionDocumentRowsAsync(
        string documentNo,
        CancellationToken cancellationToken)
    {
        await furpaDbContext.Database.OpenConnectionAsync(cancellationToken);
        var connection = furpaDbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                Evrak_No AS DocumentNo,
                Kayit_Tarihi AS CreatedAt,
                Stok_Kodu AS StockCode,
                Bolge AS RegionCode,
                COALESCE(TRY_CONVERT(int, NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(50), Sube_Kodu))), '')), 0) AS WarehouseNo,
                COALESCE(TRY_CONVERT(float, REPLACE(NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(50), Toplam_Satis_42_Gun))), ''), ',', '.')), 0) AS LastSalesQuantity,
                COALESCE(TRY_CONVERT(float, REPLACE(NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(50), Sirket_Ortalama_Satisi))), ''), ',', '.')), 0) AS CompanyAverageDailySales,
                COALESCE(TRY_CONVERT(float, REPLACE(NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(50), Sube_Ortalama_Satisi))), ''), ',', '.')), 0) AS BranchAverageDailySales,
                COALESCE(TRY_CONVERT(int, TRY_CONVERT(decimal(18, 4), REPLACE(NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(50), Dagilim_Koli_Miktar))), ''), ',', '.'))), 0) AS CaseQuantity,
                COALESCE(TRY_CONVERT(int, TRY_CONVERT(decimal(18, 4), REPLACE(NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(50), Dagilim_Adet_Miktar))), ''), ',', '.'))), 0) AS UnitQuantity,
                Dagilimi_Yapan AS DistributedBy,
                COALESCE(TRY_CONVERT(int, NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(50), Durum))), '')), 0) AS Status,
                TRY_CONVERT(datetime, NULLIF(Kesinlestirme_Tarihi, '')) AS FinalizedAt,
                COALESCE(TRY_CONVERT(int, NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(50), Dagitim_Merkezi))), '')), 0) AS DistributionCenterWarehouseNo
            FROM dbo.STOK_DAGILIM WITH (NOLOCK)
            WHERE Evrak_No = @documentNo
            ORDER BY Bolge, Sube_Kodu;
            """;
        AddParameter(command, "@documentNo", documentNo);

        var rows = new List<DistributionDocumentRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new DistributionDocumentRow(
                GetString(reader, "DocumentNo") ?? documentNo,
                GetDateTime(reader, "CreatedAt") ?? DateTime.MinValue,
                GetString(reader, "StockCode") ?? string.Empty,
                GetString(reader, "RegionCode"),
                GetInt32(reader, "WarehouseNo"),
                GetDouble(reader, "LastSalesQuantity"),
                GetDouble(reader, "CompanyAverageDailySales"),
                GetDouble(reader, "BranchAverageDailySales"),
                GetInt32(reader, "CaseQuantity"),
                GetInt32(reader, "UnitQuantity"),
                GetString(reader, "DistributedBy"),
                GetInt32(reader, "Status"),
                GetDateTime(reader, "FinalizedAt"),
                GetInt32(reader, "DistributionCenterWarehouseNo")));
        }

        return rows;
    }

    private async Task<IReadOnlyCollection<ProductDistributionNotificationRecipientDto>> QueryRegionManagersAsync(
        string documentNo,
        CancellationToken cancellationToken)
    {
        await furpaDbContext.Database.OpenConnectionAsync(cancellationToken);
        var connection = furpaDbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                distribution.Bolge AS RegionCode,
                MAX(manager.bolge_muduru) AS ManagerName,
                MAX(manager.bolge_muduru_eposta) AS Email,
                COUNT(1) AS LineCount,
                COALESCE(SUM(TRY_CONVERT(int, TRY_CONVERT(decimal(18, 4), REPLACE(NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(50), distribution.Dagilim_Koli_Miktar))), ''), ',', '.')))), 0) AS TotalCaseQuantity,
                COALESCE(SUM(TRY_CONVERT(int, TRY_CONVERT(decimal(18, 4), REPLACE(NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(50), distribution.Dagilim_Adet_Miktar))), ''), ',', '.')))), 0) AS TotalUnitQuantity
            FROM dbo.STOK_DAGILIM AS distribution WITH (NOLOCK)
            LEFT JOIN dbo.Bolge_Yoneticileri AS manager WITH (NOLOCK)
                ON CONVERT(nvarchar(25), manager.bolge_kodu) = CONVERT(nvarchar(25), distribution.Bolge)
            WHERE distribution.Evrak_No = @documentNo
            GROUP BY distribution.Bolge
            ORDER BY distribution.Bolge;
            """;
        AddParameter(command, "@documentNo", documentNo);

        var rows = new List<ProductDistributionNotificationRecipientDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new ProductDistributionNotificationRecipientDto(
                GetString(reader, "RegionCode"),
                GetString(reader, "ManagerName"),
                GetString(reader, "Email"),
                GetInt32(reader, "LineCount"),
                GetInt32(reader, "TotalCaseQuantity"),
                GetInt32(reader, "TotalUnitQuantity")));
        }

        return rows;
    }

    private async Task<IReadOnlyCollection<ProductDistributionNotificationMailResultDto>> SendNotificationMailsAsync(
        string documentNo,
        ProductDistributionDetailDto detail,
        IReadOnlyCollection<ProductDistributionNotificationRecipientDto> recipients,
        CancellationToken cancellationToken)
    {
        var results = new List<ProductDistributionNotificationMailResultDto>(recipients.Count);
        foreach (var recipient in recipients)
        {
            var email = NormalizeOptionalText(recipient.Email);
            if (email is null)
            {
                results.Add(new ProductDistributionNotificationMailResultDto(
                    recipient.RegionCode,
                    recipient.ManagerName,
                    recipient.Email,
                    false,
                    "Bolge yoneticisi e-posta adresi bulunamadi."));
                continue;
            }

            var regionLines = detail.Lines
                .Where(line => RegionMatches(line.RegionCode, recipient.RegionCode))
                .OrderBy(line => line.WarehouseNo)
                .ToArray();
            if (regionLines.Length == 0)
            {
                results.Add(new ProductDistributionNotificationMailResultDto(
                    recipient.RegionCode,
                    recipient.ManagerName,
                    email,
                    false,
                    "Bolge icin dagilim satiri bulunamadi."));
                continue;
            }

            var subject = BuildRegionNotificationSubject(recipient.RegionCode);
            var body = BuildNotificationMailBody(documentNo, detail, recipient, regionLines);

            try
            {
                await notificationMailer.SendAsync(
                    new ProductDistributionMailRequest(email, subject, body),
                    cancellationToken);

                results.Add(new ProductDistributionNotificationMailResultDto(
                    recipient.RegionCode,
                    recipient.ManagerName,
                    email,
                    true,
                    "Mail gonderildi."));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                results.Add(new ProductDistributionNotificationMailResultDto(
                    recipient.RegionCode,
                    recipient.ManagerName,
                    email,
                    false,
                    $"Mail gonderilemedi: {exception.Message}"));
            }
        }

        return results;
    }

    private static string BuildNotificationMailBody(
        string documentNo,
        ProductDistributionDetailDto detail,
        ProductDistributionNotificationRecipientDto recipient,
        IReadOnlyCollection<ProductDistributionLineDto> lines)
    {
        var stockLabel = $"{detail.Header.Stock.StockCode} - {detail.Header.Stock.StockName}";
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html>");
        builder.AppendLine("<html>");
        builder.AppendLine("<head>");
        builder.AppendLine("<meta charset=\"utf-8\" />");
        builder.AppendLine("<style>");
        builder.AppendLine("body{font-family:Arial,Helvetica,sans-serif;color:#1f2937;font-size:14px;line-height:1.45}");
        builder.AppendLine("h2{font-size:18px;margin:0 0 12px}");
        builder.AppendLine("table{border-collapse:collapse;width:100%;margin:12px 0}");
        builder.AppendLine("th,td{border:1px solid #d1d5db;padding:8px;text-align:left;vertical-align:top}");
        builder.AppendLine("th{background:#f3f4f6;font-weight:700}");
        builder.AppendLine(".meta th{width:180px}");
        builder.AppendLine(".number{text-align:right;white-space:nowrap}");
        builder.AppendLine(".muted{color:#6b7280}");
        builder.AppendLine("</style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.AppendLine("<h2>Urun Dagilimi Bilgilendirmesi</h2>");
        builder.Append("<p>");
        builder.Append(HtmlEncode(recipient.ManagerName ?? "Bolge yoneticisi"));
        builder.AppendLine(" dikkatine,</p>");
        builder.AppendLine("<table class=\"meta\">");
        AppendMetaRow(builder, "Evrak No", documentNo);
        AppendMetaRow(builder, "Bolge", BuildRegionLabel(recipient.RegionCode));
        AppendMetaRow(builder, "Stok", stockLabel);
        AppendMetaRow(builder, "Dagitim Merkezi", detail.Header.DistributionCenter.WarehouseName);
        AppendMetaRow(builder, "Dagilimi Yapan", detail.Header.DistributedBy ?? "-");
        AppendMetaRow(builder, "Kayit Tarihi", detail.Header.CreatedAt.ToString("dd.MM.yyyy", TurkishCulture));
        builder.AppendLine("</table>");
        builder.AppendLine("<table>");
        builder.AppendLine("<thead>");
        builder.Append("<tr><th>Sube</th><th>");
        builder.Append(HtmlEncode(stockLabel));
        builder.AppendLine("</th><th class=\"number\">Koli</th><th class=\"number\">Adet</th></tr>");
        builder.AppendLine("</thead>");
        builder.AppendLine("<tbody>");

        foreach (var line in lines)
        {
            builder.Append("<tr><td>");
            builder.Append(HtmlEncode($"{line.WarehouseNo} - {line.WarehouseName}"));
            builder.Append("</td><td>");
            builder.Append(line.UnitQuantity > 0 ? "Dagitim" : "<span class=\"muted\">Dagilim Yapilmadi</span>");
            builder.Append("</td><td class=\"number\">");
            builder.Append(FormatWholeNumber(line.CaseQuantity));
            builder.Append(' ');
            builder.Append(HtmlEncode(line.CaseUnitName));
            builder.Append("</td><td class=\"number\">");
            builder.Append(FormatWholeNumber(line.UnitQuantity));
            builder.Append(' ');
            builder.Append(HtmlEncode(line.QuantityUnitName));
            builder.AppendLine("</td></tr>");
        }

        builder.AppendLine("</tbody>");
        builder.AppendLine("<tfoot>");
        builder.Append("<tr><th colspan=\"2\">Toplam</th><th class=\"number\">");
        builder.Append(FormatWholeNumber(lines.Sum(line => line.CaseQuantity)));
        builder.Append(' ');
        builder.Append(HtmlEncode(lines.First().CaseUnitName));
        builder.Append("</th><th class=\"number\">");
        builder.Append(FormatWholeNumber(lines.Sum(line => line.UnitQuantity)));
        builder.Append(' ');
        builder.Append(HtmlEncode(lines.First().QuantityUnitName));
        builder.AppendLine("</th></tr>");
        builder.AppendLine("</tfoot>");
        builder.AppendLine("</table>");
        builder.AppendLine("<p class=\"muted\">Bu mail Furpa Merkez API urun dagilimi bilgilendirme akisi tarafindan olusturulmustur.</p>");
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");

        return builder.ToString();
    }

    private static string BuildNotificationMessage(
        bool mailSendingEnabled,
        int recipientCount,
        IReadOnlyCollection<ProductDistributionNotificationMailResultDto> mailResults,
        bool statusChanged,
        int previousStatus)
    {
        if (!mailSendingEnabled)
        {
            return recipientCount == 0
                ? "Bolge yoneticisi e-posta kaydi bulunamadi; SMTP kapali oldugu icin eski akisa uygun olarak durum bilgilendirildi isaretlendi."
                : "Bilgilendirme hazirlandi; SMTP kapali oldugu icin mail gonderimi yapilmadi.";
        }

        if (recipientCount == 0)
        {
            return previousStatus == 1
                ? "Bolge yoneticisi e-posta kaydi bulunamadi; durum zaten bilgilendirildi."
                : "Bolge yoneticisi e-posta kaydi bulunamadi; mail gonderilmedi ve durum degistirilmedi.";
        }

        var sentCount = mailResults.Count(result => result.Sent);
        var failedCount = mailResults.Count(result => !result.Sent);
        if (failedCount > 0)
        {
            return $"Mail gonderimi tamamlanamadi. Gonderilen: {sentCount}, sorunlu: {failedCount}. Durum degistirilmedi.";
        }

        return statusChanged
            ? $"Bilgilendirme maili gonderildi ({sentCount} alici); durum bilgilendirildi olarak isaretlendi."
            : $"Bilgilendirme maili gonderildi ({sentCount} alici); durum zaten bilgilendirildi.";
    }

    private static void AppendMetaRow(StringBuilder builder, string label, string value)
    {
        builder.Append("<tr><th>");
        builder.Append(HtmlEncode(label));
        builder.Append("</th><td>");
        builder.Append(HtmlEncode(value));
        builder.AppendLine("</td></tr>");
    }

    private static string BuildRegionNotificationSubject(string? regionCode)
    {
        var normalizedRegionCode = NormalizeOptionalText(regionCode);
        return normalizedRegionCode is null
            ? "Bolge, Urun Dagilimi Hk."
            : $"{normalizedRegionCode}. Bolge, Urun Dagilimi Hk.";
    }

    private static string BuildRegionLabel(string? regionCode)
    {
        var normalizedRegionCode = NormalizeOptionalText(regionCode);
        return normalizedRegionCode is null ? "-" : $"{normalizedRegionCode}. Bolge";
    }

    private static bool RegionMatches(string? left, string? right) =>
        string.Equals(NormalizeOptionalText(left), NormalizeOptionalText(right), StringComparison.OrdinalIgnoreCase);

    private static string FormatWholeNumber(int value) =>
        value.ToString("N0", TurkishCulture);

    private static string HtmlEncode(string value) =>
        WebUtility.HtmlEncode(value);

    private async Task<IReadOnlyCollection<PreparedSaveLine>> PrepareSaveLinesAsync(
        ProductDistributionSaveRequest request,
        int packageFactor,
        CancellationToken cancellationToken)
    {
        var groupedLines = request.Lines
            .GroupBy(line => line.WarehouseNo)
            .Select(group => group.Last())
            .ToArray();

        if (groupedLines.Length != request.Lines.Count)
        {
            throw new ArgumentException("Ayni sube/depo birden fazla satirda gonderilemez.", nameof(request.Lines));
        }

        var warehouseNos = groupedLines.Select(line => line.WarehouseNo).ToArray();
        if (warehouseNos.Contains(request.DistributionCenterWarehouseNo))
        {
            throw new ArgumentException("Dagitim merkezi dagitim satiri olarak kullanilamaz.", nameof(request.Lines));
        }

        var warehouses = await GetWarehousesAsync(warehouseNos, cancellationToken);
        var preparedLines = new List<PreparedSaveLine>(groupedLines.Length);

        foreach (var line in groupedLines)
        {
            if (line.WarehouseNo <= 0)
            {
                throw new ArgumentException("Sube/depo kodu sifirdan buyuk olmalidir.", nameof(request.Lines));
            }

            if (line.CaseQuantity < 0)
            {
                throw new ArgumentException("Dagilim koli miktari negatif olamaz.", nameof(request.Lines));
            }

            if (!warehouses.TryGetValue(line.WarehouseNo, out var warehouse))
            {
                throw new KeyNotFoundException($"Sube/depo bulunamadi: {line.WarehouseNo}");
            }

            var unitQuantity = line.UnitQuantity ?? checked(line.CaseQuantity * packageFactor);
            if (unitQuantity < 0)
            {
                throw new ArgumentException("Dagilim adet miktari negatif olamaz.", nameof(request.Lines));
            }

            preparedLines.Add(new PreparedSaveLine(
                ParseRegionNo(warehouse.RegionCode),
                line.WarehouseNo,
                line.CaseQuantity,
                unitQuantity,
                line.LastSalesQuantity ?? 0d,
                line.CompanyAverageDailySales ?? 0d,
                line.BranchAverageDailySales ?? 0d));
        }

        var expectedCaseQuantity = ResolveExpectedCaseQuantity(request);
        var totalCaseQuantity = preparedLines.Sum(line => line.CaseQuantity);
        if (totalCaseQuantity != expectedCaseQuantity)
        {
            throw new InvalidOperationException(
                $"Dagilim koli toplami {totalCaseQuantity}; beklenen toplam {expectedCaseQuantity}.");
        }

        return preparedLines;
    }

    private async Task<string> GetNextDistributionDocumentNoAsync(CancellationToken cancellationToken)
    {
        var transaction = furpaDbContext.Database.CurrentTransaction?.GetDbTransaction();
        var connection = furpaDbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT COALESCE(MAX(TRY_CONVERT(int, Evrak_No)), 0) + 1
            FROM dbo.STOK_DAGILIM WITH (UPDLOCK, HOLDLOCK)
            WHERE TRY_CONVERT(int, Evrak_No) IS NOT NULL;
            """;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToString(result, CultureInfo.InvariantCulture) ?? "1";
    }

    private async Task InsertDistributionRowsAsync(
        string documentNo,
        string stockCode,
        int distributionCenterWarehouseNo,
        string? distributedBy,
        IReadOnlyCollection<PreparedSaveLine> lines,
        CancellationToken cancellationToken)
    {
        var transaction = furpaDbContext.Database.CurrentTransaction?.GetDbTransaction();
        var connection = furpaDbContext.Database.GetDbConnection();

        foreach (var line in lines)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO dbo.STOK_DAGILIM (
                    Evrak_No,
                    Kayit_Tarihi,
                    Stok_Kodu,
                    Bolge,
                    Sube_Kodu,
                    Toplam_Satis_42_Gun,
                    Sirket_Ortalama_Satisi,
                    Sube_Ortalama_Satisi,
                    Dagilim_Koli_Miktar,
                    Dagilim_Adet_Miktar,
                    Dagilimi_Yapan,
                    Durum,
                    Kesinlestirme_Tarihi,
                    Dagitim_Merkezi
                )
                VALUES (
                    @documentNo,
                    @createdAt,
                    @stockCode,
                    @regionNo,
                    @warehouseNo,
                    @lastSalesQuantity,
                    @companyAverageDailySales,
                    @branchAverageDailySales,
                    @caseQuantity,
                    @unitQuantity,
                    @distributedBy,
                    0,
                    @finalizedAt,
                    @distributionCenterWarehouseNo
                );
                """;
            AddParameter(command, "@documentNo", documentNo);
            AddParameter(command, "@createdAt", DateTime.Today);
            AddParameter(command, "@stockCode", stockCode);
            AddParameter(command, "@regionNo", line.RegionNo);
            AddParameter(command, "@warehouseNo", line.WarehouseNo);
            AddParameter(command, "@lastSalesQuantity", line.LastSalesQuantity);
            AddParameter(command, "@companyAverageDailySales", line.CompanyAverageDailySales);
            AddParameter(command, "@branchAverageDailySales", line.BranchAverageDailySales);
            AddParameter(command, "@caseQuantity", line.CaseQuantity);
            AddParameter(command, "@unitQuantity", line.UnitQuantity);
            AddParameter(command, "@distributedBy", NormalizeOptionalText(distributedBy));
            AddParameter(command, "@finalizedAt", string.Empty);
            AddParameter(command, "@distributionCenterWarehouseNo", distributionCenterWarehouseNo);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task<int> DeleteDistributionRowsAsync(
        string documentNo,
        CancellationToken cancellationToken)
    {
        await furpaDbContext.Database.OpenConnectionAsync(cancellationToken);
        var transaction = furpaDbContext.Database.CurrentTransaction?.GetDbTransaction();
        var connection = furpaDbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM dbo.STOK_DAGILIM WHERE Evrak_No = @documentNo;";
        AddParameter(command, "@documentNo", documentNo);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<int> MarkDistributionStatusAsync(
        string documentNo,
        int status,
        CancellationToken cancellationToken)
    {
        await furpaDbContext.Database.OpenConnectionAsync(cancellationToken);
        var connection = furpaDbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.STOK_DAGILIM
            SET Durum = @status
            WHERE Evrak_No = @documentNo
              AND COALESCE(TRY_CONVERT(int, NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(50), Durum))), '')), 0) <> 2
              AND COALESCE(TRY_CONVERT(int, NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(50), Durum))), '')), 0) <> @status;
            """;
        AddParameter(command, "@documentNo", documentNo);
        AddParameter(command, "@status", status);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task MarkDistributionFinalizedAsync(
        string documentNo,
        DateTime finalizedAt,
        CancellationToken cancellationToken)
    {
        await furpaDbContext.Database.OpenConnectionAsync(cancellationToken);
        var connection = furpaDbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.STOK_DAGILIM
            SET Durum = 2,
                Kesinlestirme_Tarihi = @finalizedAt
            WHERE Evrak_No = @documentNo
              AND COALESCE(TRY_CONVERT(int, NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(50), Durum))), '')), 0) <> 2;
            """;
        AddParameter(command, "@documentNo", documentNo);
        AddParameter(command, "@finalizedAt", finalizedAt.Date);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<bool> MarkStockOrderingStoppedAsync(
        string stockCode,
        CancellationToken cancellationToken)
    {
        var updatedRows = await mikroWriteDbContext.STOKLARs
            .Where(stock => stock.sto_kod == stockCode && stock.sto_siparis_dursun != 1)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(stock => stock.sto_siparis_dursun, (byte?)1),
                cancellationToken);

        return updatedRows > 0;
    }

    private async Task<IReadOnlyCollection<ProductDistributionWarehouseOrderDto>> CreateWarehouseOrdersAsync(
        ProductDistributionDetailDto detail,
        IReadOnlyCollection<ProductDistributionLineDto> positiveLines,
        string description,
        DateTime orderDate,
        DateTime deliveryDate,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            try
            {
                var orders = new List<ProductDistributionWarehouseOrderDto>();
                var existingOrders = await QueryExistingWarehouseOrdersAsync(
                    detail,
                    description,
                    cancellationToken);

                foreach (var line in positiveLines)
                {
                    if (existingOrders.TryGetValue(line.WarehouseNo, out var existingOrder))
                    {
                        orders.Add(MapWarehouseOrder(
                            existingOrder.DocumentSerie,
                            existingOrder.DocumentOrderNo,
                            line,
                            detail,
                            alreadyExisted: true));
                        continue;
                    }

                    var documentSerie = $"D{line.WarehouseNo}";
                    var documentOrderNo = await GetNextWarehouseOrderNoAsync(documentSerie, cancellationToken);
                    var entity = AutomaticWarehouseOrderFactory.CreateOrderLine(
                        line.WarehouseNo,
                        detail.Header.DistributionCenter.WarehouseNo,
                        orderDate,
                        deliveryDate,
                        documentSerie,
                        documentOrderNo,
                        0,
                        now,
                        detail.Header.Stock.StockCode,
                        line.UnitQuantity,
                        0d,
                        1,
                        description,
                        null,
                        null);
                    entity.ssip_rezervasyon_miktari = line.CaseQuantity;

                    await mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs.AddAsync(entity, cancellationToken);
                    orders.Add(MapWarehouseOrder(
                        documentSerie,
                        documentOrderNo,
                        line,
                        detail,
                        alreadyExisted: false));
                }

                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return orders;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<Dictionary<int, ExistingWarehouseOrderRow>> QueryExistingWarehouseOrdersAsync(
        ProductDistributionDetailDto detail,
        string description,
        CancellationToken cancellationToken)
    {
        var stockCode = detail.Header.Stock.StockCode;
        var outWarehouseNo = detail.Header.DistributionCenter.WarehouseNo;

        var rows = await mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs
            .AsNoTracking()
            .Where(order =>
                order.ssip_iptal != true &&
                order.ssip_stok_kod == stockCode &&
                order.ssip_cikdepo == outWarehouseNo &&
                order.ssip_aciklama == description &&
                order.ssip_girdepo.HasValue)
            .GroupBy(order => new
            {
                InWarehouseNo = order.ssip_girdepo!.Value,
                DocumentSerie = order.ssip_evrakno_seri ?? string.Empty,
                DocumentOrderNo = order.ssip_evrakno_sira ?? 0
            })
            .Select(group => new ExistingWarehouseOrderRow(
                group.Key.InWarehouseNo,
                group.Key.DocumentSerie,
                group.Key.DocumentOrderNo))
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.InWarehouseNo)
            .ToDictionary(group => group.Key, group => group.OrderBy(row => row.DocumentOrderNo).First());
    }

    private async Task<int> GetNextWarehouseOrderNoAsync(
        string documentSerie,
        CancellationToken cancellationToken)
    {
        var currentMax = await mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs
            .Where(order => order.ssip_evrakno_seri == documentSerie)
            .MaxAsync(order => order.ssip_evrakno_sira, cancellationToken);

        return currentMax.HasValue ? currentMax.Value + 1 : FirstDocumentOrderNo;
    }

    private static ProductDistributionWarehouseOrderDto MapWarehouseOrder(
        string documentSerie,
        int documentOrderNo,
        ProductDistributionLineDto line,
        ProductDistributionDetailDto detail,
        bool alreadyExisted) =>
        new(
            documentSerie,
            documentOrderNo,
            line.WarehouseNo,
            line.WarehouseName,
            detail.Header.DistributionCenter.WarehouseNo,
            detail.Header.DistributionCenter.WarehouseName,
            1,
            line.UnitQuantity,
            alreadyExisted);

    private static Dictionary<int, int> AllocateCases(
        IReadOnlyCollection<BranchSalesRow> rows,
        int totalCaseQuantity)
    {
        if (rows.Count == 0 || totalCaseQuantity <= 0)
        {
            return new Dictionary<int, int>();
        }

        var weightedRows = rows
            .Select(row => new
            {
                row.WarehouseNo,
                Weight = Math.Max(0d, row.LastSalesQuantity)
            })
            .ToArray();

        var totalWeight = weightedRows.Sum(row => row.Weight);
        if (totalWeight <= 0d)
        {
            weightedRows = weightedRows
                .Select(row => new
                {
                    row.WarehouseNo,
                    Weight = 1d
                })
                .ToArray();
            totalWeight = weightedRows.Sum(row => row.Weight);
        }

        var allocationRows = weightedRows
            .Select(row =>
            {
                var raw = totalCaseQuantity * row.Weight / totalWeight;
                var floor = (int)Math.Floor(raw);
                return new AllocationRow(row.WarehouseNo, floor, raw - floor, row.Weight);
            })
            .ToArray();

        var remaining = totalCaseQuantity - allocationRows.Sum(row => row.CaseQuantity);
        foreach (var row in allocationRows
                     .OrderByDescending(row => row.Fraction)
                     .ThenByDescending(row => row.Weight)
                     .ThenBy(row => row.WarehouseNo)
                     .Take(remaining))
        {
            row.CaseQuantity++;
        }

        return allocationRows.ToDictionary(row => row.WarehouseNo, row => row.CaseQuantity);
    }


    private static IReadOnlyCollection<ProductDistributionBalanceLineDto> BalanceLines(
        ProductDistributionBalanceRequest request,
        int packageFactor,
        string quantityUnitName,
        ICollection<string> warnings)
    {
        var groupedLines = request.Lines
            .GroupBy(line => line.WarehouseNo)
            .Select(group => group.Last())
            .ToArray();

        if (groupedLines.Length != request.Lines.Count)
        {
            throw new ArgumentException("Ayni sube/depo birden fazla satirda gonderilemez.", nameof(request.Lines));
        }

        var workingLines = groupedLines
            .Select(line => new BalanceWorkingLine(
                line.WarehouseNo,
                string.IsNullOrWhiteSpace(line.WarehouseName) ? $"Depo {line.WarehouseNo}" : line.WarehouseName.Trim(),
                string.IsNullOrWhiteSpace(line.RegionCode) ? null : line.RegionCode.Trim(),
                line.LastSalesQuantity,
                line.CurrentStockQuantity,
                line.CompanyAverageDailySales,
                line.BranchAverageDailySales,
                line.CaseQuantity,
                line.IsLocked))
            .ToArray();

        var difference = request.TargetCaseQuantity - workingLines.Sum(line => line.CaseQuantity);
        if (difference > 0)
        {
            AddMissingCases(workingLines, difference, warnings);
        }
        else if (difference < 0)
        {
            ReduceExtraCases(workingLines, -difference, warnings);
        }

        var totalSalesQuantity = workingLines.Sum(line => Math.Max(0d, line.LastSalesQuantity));
        var allocatedCaseQuantity = workingLines.Sum(line => line.CaseQuantity);

        return workingLines
            .Select(line =>
            {
                var regionCode = NormalizeOptionalText(line.RegionCode);
                return new ProductDistributionBalanceLineDto(
                    line.WarehouseNo,
                    line.WarehouseName,
                    regionCode,
                    ResolveRegionName(regionCode),
                    Round(line.LastSalesQuantity),
                    Round(line.CurrentStockQuantity),
                    Round(line.CompanyAverageDailySales),
                    Round(line.BranchAverageDailySales),
                    CalculatePercent(line.LastSalesQuantity, totalSalesQuantity),
                    CalculatePercent(line.CaseQuantity, allocatedCaseQuantity),
                    line.OriginalCaseQuantity,
                    line.CaseQuantity,
                    line.CaseQuantity - line.OriginalCaseQuantity,
                    checked(line.CaseQuantity * packageFactor),
                    quantityUnitName,
                    CaseUnitName,
                    line.IsLocked,
                    GetBalanceReason(line));
            })
            .ToArray();
    }

    private static void AddMissingCases(
        IReadOnlyCollection<BalanceWorkingLine> lines,
        int missingCaseQuantity,
        ICollection<string> warnings)
    {
        var candidates = lines
            .Where(line => !line.IsLocked)
            .ToArray();

        if (candidates.Length == 0)
        {
            warnings.Add("Eksik koli var ama kilitli olmayan satir yok; hedefe denge kurulamadi.");
            return;
        }

        var additions = AllocateCases(
            candidates
                .Select(line => new BranchSalesRow(
                    line.WarehouseNo,
                    line.WarehouseName,
                    line.RegionCode,
                    Math.Max(0d, line.LastSalesQuantity),
                    line.CurrentStockQuantity))
                .ToArray(),
            missingCaseQuantity);

        foreach (var line in candidates)
        {
            line.CaseQuantity = checked(line.CaseQuantity + additions.GetValueOrDefault(line.WarehouseNo));
        }
    }

    private static void ReduceExtraCases(
        IReadOnlyCollection<BalanceWorkingLine> lines,
        int extraCaseQuantity,
        ICollection<string> warnings)
    {
        var remaining = extraCaseQuantity;
        var candidates = lines
            .Where(line => !line.IsLocked && line.CaseQuantity > 0)
            .OrderBy(line => Math.Max(0d, line.LastSalesQuantity))
            .ThenBy(line => Math.Max(0d, line.BranchAverageDailySales))
            .ThenByDescending(line => line.CaseQuantity)
            .ThenBy(line => line.WarehouseNo)
            .ToArray();

        if (candidates.Length == 0)
        {
            warnings.Add("Fazla koli var ama dusulebilecek kilitli olmayan satir yok; hedefe denge kurulamadi.");
            return;
        }

        foreach (var line in candidates)
        {
            if (remaining == 0)
            {
                break;
            }

            var removed = Math.Min(line.CaseQuantity, remaining);
            line.CaseQuantity -= removed;
            remaining -= removed;
        }

        if (remaining > 0)
        {
            warnings.Add($"Fazla {extraCaseQuantity} kolinin {extraCaseQuantity - remaining} kolisi dusulebildi; kalan {remaining} koli icin kilitli satirlar kontrol edilmeli.");
        }
    }

    private static string GetBalanceReason(BalanceWorkingLine line)
    {
        if (line.IsLocked)
        {
            return "locked";
        }

        var delta = line.CaseQuantity - line.OriginalCaseQuantity;
        return delta switch
        {
            > 0 => "balanced-up",
            < 0 => "balanced-down",
            _ => "unchanged"
        };
    }
    private static IReadOnlyCollection<ProductDistributionActionDto> CreateActions(int status) =>
        status switch
        {
            0 =>
            [
                new("update", "Guncelle", true, null),
                new("delete", "Sil", true, null),
                new("notify", "Bilgilendir", true, null),
                new("finalize", "Kesinlestir", true, null)
            ],
            1 =>
            [
                new("update", "Guncelle", false, "Bilgilendirilmis dagilim guncellenemez."),
                new("delete", "Sil", false, "Bilgilendirilmis dagilim silinemez."),
                new("notify", "Bilgilendir", true, "Tekrar bilgilendirme hazirlanabilir."),
                new("finalize", "Kesinlestir", true, null)
            ],
            _ =>
            [
                new("update", "Guncelle", false, "Kesinlesmis dagilim guncellenemez."),
                new("delete", "Sil", false, "Kesinlesmis dagilim silinemez."),
                new("notify", "Bilgilendir", false, "Kesinlesmis dagilim tekrar bilgilendirilemez."),
                new("finalize", "Kesinlestir", false, "Dagilim zaten kesinlesmis.")
            ]
        };

    private static ProductDistributionStatusDto GetStatus(int status) =>
        status switch
        {
            0 => new ProductDistributionStatusDto(0, "Kaydedildi", "info"),
            1 => new ProductDistributionStatusDto(1, "Bilgilendirildi", "warning"),
            2 => new ProductDistributionStatusDto(2, "Dagilim Yapildi", "success"),
            _ => new ProductDistributionStatusDto(status, "Bilinmiyor", "muted")
        };

    private static void ValidateProposalRequest(ProductDistributionProposalRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.StockCode))
        {
            throw new ArgumentException("Stok kodu zorunludur.", nameof(request.StockCode));
        }

        if (request.DistributionCenterWarehouseNo <= 0)
        {
            throw new ArgumentException("Dagitim merkezi zorunludur.", nameof(request.DistributionCenterWarehouseNo));
        }

        if (request.TotalCaseQuantity <= 0)
        {
            throw new ArgumentException("Toplam koli miktari sifirdan buyuk olmalidir.", nameof(request.TotalCaseQuantity));
        }
    }


    private static void ValidateBalanceRequest(ProductDistributionBalanceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.StockCode))
        {
            throw new ArgumentException("Stok kodu zorunludur.", nameof(request.StockCode));
        }

        if (request.TargetCaseQuantity < 0)
        {
            throw new ArgumentException("Hedef koli miktari negatif olamaz.", nameof(request.TargetCaseQuantity));
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new ArgumentException("En az bir dagilim satiri zorunludur.", nameof(request.Lines));
        }

        foreach (var line in request.Lines)
        {
            if (line.WarehouseNo <= 0)
            {
                throw new ArgumentException("Sube/depo kodu sifirdan buyuk olmalidir.", nameof(request.Lines));
            }

            if (line.CaseQuantity < 0)
            {
                throw new ArgumentException("Dagilim koli miktari negatif olamaz.", nameof(request.Lines));
            }
        }
    }
    private static void ValidateSaveRequest(ProductDistributionSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.StockCode))
        {
            throw new ArgumentException("Stok kodu zorunludur.", nameof(request.StockCode));
        }

        if (request.DistributionCenterWarehouseNo <= 0)
        {
            throw new ArgumentException("Dagitim merkezi zorunludur.", nameof(request.DistributionCenterWarehouseNo));
        }

        if (request.TotalCaseQuantity < 0)
        {
            throw new ArgumentException("Toplam koli miktari negatif olamaz.", nameof(request.TotalCaseQuantity));
        }

        if (request.TargetCaseQuantity is < 0)
        {
            throw new ArgumentException("Hedef koli miktari negatif olamaz.", nameof(request.TargetCaseQuantity));
        }

        if (request.AllocatedCaseQuantity is < 0)
        {
            throw new ArgumentException("Dagitilan koli miktari negatif olamaz.", nameof(request.AllocatedCaseQuantity));
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new ArgumentException("En az bir dagilim satiri zorunludur.", nameof(request.Lines));
        }
    }

    private static int ResolveExpectedCaseQuantity(ProductDistributionSaveRequest request) =>
        request.TargetCaseQuantity ?? request.AllocatedCaseQuantity ?? request.TotalCaseQuantity;

    private static int ClampSalesDayCount(int? salesDayCount) =>
        Math.Clamp(salesDayCount ?? DefaultSalesDayCount, MinSalesDayCount, MaxSalesDayCount);

    private static int NormalizePackageFactor(double? packageFactor)
    {
        var factor = packageFactor.HasValue ? Math.Abs(packageFactor.Value) : 1d;
        return factor > 1d ? Math.Max(1, Convert.ToInt32(Math.Round(factor))) : 1;
    }

    private static string NormalizeStockCode(string stockCode) =>
        stockCode.Trim().ToUpperInvariant();

    private static string NormalizeDocumentNo(string documentNo)
    {
        if (string.IsNullOrWhiteSpace(documentNo))
        {
            throw new ArgumentException("Evrak no zorunludur.", nameof(documentNo));
        }

        return documentNo.Trim();
    }

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ResolveQuantityUnitName(ProductDistributionStockDto stock) =>
        NormalizeOptionalText(stock.UnitName) ?? DefaultQuantityUnitName;

    private static string? ResolveRegionName(string? regionCode)
    {
        var normalized = NormalizeOptionalText(regionCode);
        return normalized is null ? null : $"Bolge {normalized}";
    }

    private static double CalculatePercent(double value, double total) =>
        total <= 0d ? 0d : Round(Math.Max(0d, value) * 100d / total);

    private static int? ParseRegionNo(string? regionCode) =>
        int.TryParse(regionCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var regionNo)
            ? regionNo
            : null;

    private static string BuildFinalizeDescription(string documentNo) =>
        LimitText($"{FinalizeDescriptionPrefix} {documentNo}", 50);

    private static string LimitText(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static double Round(double value) =>
        Math.Round(value, 4, MidpointRounding.AwayFromZero);

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string? GetString(IDataRecord reader, string name) =>
        reader[name] is DBNull ? null : Convert.ToString(reader[name], CultureInfo.InvariantCulture);

    private static int GetInt32(IDataRecord reader, string name)
    {
        var value = reader[name];
        if (value is DBNull)
        {
            return 0;
        }

        if (value is string text)
        {
            var normalized = NormalizeNumericText(text);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return 0;
            }

            if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
            {
                return intValue;
            }

            if (double.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var doubleValue))
            {
                return Convert.ToInt32(doubleValue);
            }
        }

        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static double GetDouble(IDataRecord reader, string name)
    {
        var value = reader[name];
        if (value is DBNull)
        {
            return 0d;
        }

        if (value is string text)
        {
            var normalized = NormalizeNumericText(text);
            return string.IsNullOrWhiteSpace(normalized)
                ? 0d
                : Convert.ToDouble(normalized, CultureInfo.InvariantCulture);
        }

        return Convert.ToDouble(value, CultureInfo.InvariantCulture);
    }

    private static string NormalizeNumericText(string value) =>
        value.Trim().Replace(',', '.');

    private static DateTime? GetDateTime(IDataRecord reader, string name) =>
        reader[name] is DBNull ? null : Convert.ToDateTime(reader[name], CultureInfo.InvariantCulture);

    private sealed record BranchSalesRow(
        int WarehouseNo,
        string WarehouseName,
        string? RegionCode,
        double LastSalesQuantity,
        double CurrentStockQuantity);

    private sealed record DistributionListRow(
        string DocumentNo,
        string StockCode,
        int DistributionCenterWarehouseNo,
        int Status,
        DateTime CreatedAt,
        DateTime? FinalizedAt,
        string? DistributedBy,
        int LineCount,
        int TotalCaseQuantity,
        int TotalUnitQuantity);

    private sealed record DistributionDocumentRow(
        string DocumentNo,
        DateTime CreatedAt,
        string StockCode,
        string? RegionCode,
        int WarehouseNo,
        double LastSalesQuantity,
        double CompanyAverageDailySales,
        double BranchAverageDailySales,
        int CaseQuantity,
        int UnitQuantity,
        string? DistributedBy,
        int Status,
        DateTime? FinalizedAt,
        int DistributionCenterWarehouseNo);

    private sealed record PreparedSaveLine(
        int? RegionNo,
        int WarehouseNo,
        int CaseQuantity,
        int UnitQuantity,
        double LastSalesQuantity,
        double CompanyAverageDailySales,
        double BranchAverageDailySales);

    private sealed record ExistingWarehouseOrderRow(
        int InWarehouseNo,
        string DocumentSerie,
        int DocumentOrderNo);


    private sealed class BalanceWorkingLine(
        int warehouseNo,
        string warehouseName,
        string? regionCode,
        double lastSalesQuantity,
        double currentStockQuantity,
        double companyAverageDailySales,
        double branchAverageDailySales,
        int caseQuantity,
        bool isLocked)
    {
        public int WarehouseNo { get; } = warehouseNo;

        public string WarehouseName { get; } = warehouseName;

        public string? RegionCode { get; } = regionCode;

        public double LastSalesQuantity { get; } = lastSalesQuantity;

        public double CurrentStockQuantity { get; } = currentStockQuantity;

        public double CompanyAverageDailySales { get; } = companyAverageDailySales;

        public double BranchAverageDailySales { get; } = branchAverageDailySales;

        public int OriginalCaseQuantity { get; } = caseQuantity;

        public int CaseQuantity { get; set; } = caseQuantity;

        public bool IsLocked { get; } = isLocked;
    }
    private sealed class AllocationRow(
        int warehouseNo,
        int caseQuantity,
        double fraction,
        double weight)
    {
        public int WarehouseNo { get; } = warehouseNo;

        public int CaseQuantity { get; set; } = caseQuantity;

        public double Fraction { get; } = fraction;

        public double Weight { get; } = weight;
    }
}


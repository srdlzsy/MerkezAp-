using System.Data;
using System.Data.Common;
using System.Globalization;
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
    FurpaDbContext furpaDbContext)
    : IProductDistributionService
{
    private const int DefaultSalesDayCount = 42;
    private const int MinSalesDayCount = 1;
    private const int MaxSalesDayCount = 365;
    private const int DefaultTake = 100;
    private const int MaxTake = 500;
    private const int FirstDocumentOrderNo = 0;
    private const string FinalizeDescriptionPrefix = "Dagilim";
    private static readonly int[] KnownDistributionCenters = [50, 53, 56];

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

                return new ProductDistributionLineDto(
                    row.WarehouseNo,
                    row.WarehouseName,
                    row.RegionCode,
                    Round(row.LastSalesQuantity),
                    Round(row.CurrentStockQuantity),
                    Round(companyAverageDailySales),
                    Round(row.LastSalesQuantity / salesDayCount),
                    caseQuantity,
                    checked(caseQuantity * stock.PackageFactor),
                    reason);
            })
            .OrderByDescending(line => line.CaseQuantity)
            .ThenByDescending(line => line.LastSalesQuantity)
            .ThenBy(line => line.RegionCode)
            .ThenBy(line => line.WarehouseNo)
            .ToArray();

        var allocatedCaseQuantity = lines.Sum(line => line.CaseQuantity);
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
        var statusChanged = await MarkDistributionStatusAsync(normalizedDocumentNo, 1, cancellationToken) > 0;
        var stockOrderingStopped = request.MarkStockOrderingStopped &&
            await MarkStockOrderingStoppedAsync(detail.Header.Stock.StockCode, cancellationToken);

        var refreshedStatus = GetStatus(1);
        var subject = $"Urun dagilimi {normalizedDocumentNo} - {detail.Header.Stock.StockName}";
        var message = recipients.Count == 0
            ? "Bolge yoneticisi e-posta kaydi bulunamadi; durum bilgilendirildi olarak isaretlendi."
            : "Bilgilendirme hazirlandi; UI/entegrasyon katmani alicilara mail veya outbox ile gonderebilir.";

        return new ProductDistributionNotificationDto(
            normalizedDocumentNo,
            refreshedStatus,
            statusChanged,
            stockOrderingStopped,
            recipients,
            subject,
            message);
    }

    public async Task<ProductDistributionFinalizeDto> FinalizeAsync(
        string documentNo,
        ProductDistributionFinalizeRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedDocumentNo = NormalizeDocumentNo(documentNo);
        var detail = await GetAsync(normalizedDocumentNo, cancellationToken);
        if (detail.Header.Status.Code == 0 && !request.AllowFinalizeWithoutNotification)
        {
            throw new InvalidOperationException("Dagilim kesinlestirmeden once bilgilendirilmelidir.");
        }

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
        var stock = await GetStockAsync(first.StockCode, cancellationToken);
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

        var lines = rows
            .OrderBy(row => row.RegionCode)
            .ThenBy(row => row.WarehouseNo)
            .Select(row =>
            {
                var warehouse = warehouses.GetValueOrDefault(row.WarehouseNo);
                return new ProductDistributionLineDto(
                    row.WarehouseNo,
                    warehouse?.WarehouseName ?? $"Depo {row.WarehouseNo}",
                    warehouse?.RegionCode ?? row.RegionCode,
                    Round(row.LastSalesQuantity),
                    0d,
                    Round(row.CompanyAverageDailySales),
                    Round(row.BranchAverageDailySales),
                    row.CaseQuantity,
                    row.UnitQuantity,
                    row.UnitQuantity > 0 ? "saved" : "no-allocation");
            })
            .ToArray();

        var totalCaseQuantity = lines.Sum(line => line.CaseQuantity);
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
        command.CommandText = """
            SELECT
                warehouse.dep_no AS WarehouseNo,
                warehouse.dep_adi AS WarehouseName,
                warehouse.dep_bolge_kodu AS RegionCode,
                COALESCE(SUM(movement.sth_miktar), 0) AS LastSalesQuantity,
                COALESCE(dbo.fn_DepodakiMiktar(@stockCode, warehouse.dep_no, @referenceDate), 0) AS CurrentStockQuantity
            FROM dbo.DEPOLAR AS warehouse WITH (NOLOCK)
            LEFT JOIN dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
                ON movement.sth_cikis_depo_no = warehouse.dep_no
               AND movement.sth_stok_kod = @stockCode
               AND movement.sth_tarih >= @periodStart
               AND movement.sth_tarih < @periodEndExclusive
               AND movement.sth_tip = 1
               AND movement.sth_cins = 1
               AND COALESCE(movement.sth_normal_iade, 0) = 0
            WHERE warehouse.dep_no > 100
              AND COALESCE(warehouse.dep_iptal, 0) = 0
              AND COALESCE(warehouse.dep_envanter_harici_fl, 0) = 0
            GROUP BY warehouse.dep_no, warehouse.dep_adi, warehouse.dep_bolge_kodu
            ORDER BY warehouse.dep_bolge_kodu, warehouse.dep_no;
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
                COALESCE(Dagitim_Merkezi, 0) AS DistributionCenterWarehouseNo,
                COALESCE(MAX(Durum), 0) AS Status,
                MIN(Kayit_Tarihi) AS CreatedAt,
                MAX(TRY_CONVERT(datetime, NULLIF(Kesinlestirme_Tarihi, ''))) AS FinalizedAt,
                MAX(Dagilimi_Yapan) AS DistributedBy,
                COUNT(1) AS LineCount,
                COALESCE(SUM(CONVERT(int, Dagilim_Koli_Miktar)), 0) AS TotalCaseQuantity,
                COALESCE(SUM(CONVERT(int, Dagilim_Adet_Miktar)), 0) AS TotalUnitQuantity
            FROM dbo.STOK_DAGILIM WITH (NOLOCK)
            WHERE (@status IS NULL OR Durum = @status)
              AND (@documentNo IS NULL OR Evrak_No = @documentNo)
              AND (@stockCode IS NULL OR Stok_Kodu = @stockCode)
              AND (@distributionCenterWarehouseNo IS NULL OR Dagitim_Merkezi = @distributionCenterWarehouseNo)
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
                Sube_Kodu AS WarehouseNo,
                COALESCE(Toplam_Satis_42_Gun, 0) AS LastSalesQuantity,
                COALESCE(Sirket_Ortalama_Satisi, 0) AS CompanyAverageDailySales,
                COALESCE(Sube_Ortalama_Satisi, 0) AS BranchAverageDailySales,
                COALESCE(Dagilim_Koli_Miktar, 0) AS CaseQuantity,
                COALESCE(Dagilim_Adet_Miktar, 0) AS UnitQuantity,
                Dagilimi_Yapan AS DistributedBy,
                COALESCE(Durum, 0) AS Status,
                TRY_CONVERT(datetime, NULLIF(Kesinlestirme_Tarihi, '')) AS FinalizedAt,
                COALESCE(Dagitim_Merkezi, 0) AS DistributionCenterWarehouseNo
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
                COALESCE(SUM(CONVERT(int, distribution.Dagilim_Koli_Miktar)), 0) AS TotalCaseQuantity,
                COALESCE(SUM(CONVERT(int, distribution.Dagilim_Adet_Miktar)), 0) AS TotalUnitQuantity
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

        var totalCaseQuantity = preparedLines.Sum(line => line.CaseQuantity);
        if (totalCaseQuantity != request.TotalCaseQuantity)
        {
            throw new InvalidOperationException(
                $"Dagilim koli toplami {totalCaseQuantity}; beklenen toplam {request.TotalCaseQuantity}.");
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
              AND COALESCE(Durum, 0) <> 2
              AND COALESCE(Durum, 0) <> @status;
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
              AND COALESCE(Durum, 0) <> 2;
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

    private static IReadOnlyCollection<ProductDistributionActionDto> CreateActions(int status) =>
        status switch
        {
            0 =>
            [
                new("update", "Guncelle", true, null),
                new("delete", "Sil", true, null),
                new("notify", "Bilgilendir", true, null),
                new("finalize", "Kesinlestir", false, "Once bolge bilgilendirmesi yapilmali.")
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

        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new ArgumentException("En az bir dagilim satiri zorunludur.", nameof(request.Lines));
        }
    }

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
        if (reader[name] is DBNull)
        {
            return 0;
        }

        return Convert.ToInt32(reader[name], CultureInfo.InvariantCulture);
    }

    private static double GetDouble(IDataRecord reader, string name)
    {
        if (reader[name] is DBNull)
        {
            return 0d;
        }

        return Convert.ToDouble(reader[name], CultureInfo.InvariantCulture);
    }

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

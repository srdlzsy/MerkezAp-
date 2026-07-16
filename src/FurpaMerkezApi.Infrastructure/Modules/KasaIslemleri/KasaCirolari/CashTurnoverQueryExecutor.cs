using System.Data;
using System.Data.Common;
using System.Globalization;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaCirolari;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Shopigo;
using FurpaMerkezApi.Infrastructure.Persistence.Shopigo.Models;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaCirolari;

public sealed class CashTurnoverQueryExecutor(
    ShopigoCiroDbContext shopigoCiroDbContext,
    MikroDbContext mikroDbContext)
{
    private const string CompletedReceivedSaleStatus = "4";

    public async Task<IReadOnlyCollection<CashTurnoverListItemDto>> ListAsync(
        CashTurnoverListRequest request,
        CancellationToken cancellationToken)
    {
        var items = new List<CashTurnoverListItemDto>();

        if (request.Source is CashTurnoverSource.New or CashTurnoverSource.All)
        {
            items.AddRange(await ListNewAsync(request, cancellationToken));
        }

        if (request.Source is CashTurnoverSource.Old or CashTurnoverSource.All)
        {
            items.AddRange(await ListOldAsync(request, cancellationToken));
        }

        return items
            .OrderBy(item => item.BusinessDate)
            .ThenBy(item => item.WarehouseNo)
            .ThenBy(item => item.ShiftNo)
            .ThenBy(item => item.Source, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.CashierCode, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<CashTurnoverOverviewDto> GetOverviewAsync(
        CashTurnoverOverviewRequest request,
        CancellationToken cancellationToken)
    {
        var (startDate, endDateExclusive) = NormalizeOverviewDateRange(
            request.WarehouseNo,
            request.StartDate,
            request.EndDate);
        var items = new List<BranchOverviewRow>();

        if (request.Source is CashTurnoverSource.New or CashTurnoverSource.All)
        {
            items.AddRange(await GetNewOverviewItemsAsync(
                request.WarehouseNo,
                startDate,
                endDateExclusive,
                cancellationToken));
        }

        if (request.Source is CashTurnoverSource.Old or CashTurnoverSource.All)
        {
            items.AddRange(await GetOldOverviewItemsAsync(
                request.WarehouseNo,
                startDate,
                endDateExclusive,
                cancellationToken));
        }

        var branchItems = MergeOverviewItems(items);
        var dailyTotal = Round(branchItems.Sum(item => item.OverallTotal));
        var dailyCustomerCount = branchItems.Sum(item => item.CustomerCount);

        return new CashTurnoverOverviewDto(
            dailyTotal,
            Round(branchItems.Sum(item => item.CashTotal)),
            Round(branchItems.Sum(item => item.CreditTotal)),
            Round(branchItems.Sum(item => item.GiftCardTotal)),
            Round(branchItems.Sum(item => item.ExpenseNoteTotal)),
            dailyCustomerCount,
            branchItems.Sum(item => item.FurparaCardCustomerCount),
            branchItems.Sum(item => item.DiscountCardCustomerCount),
            branchItems.Sum(item => item.ExpenseNoteCount),
            Divide(dailyTotal, dailyCustomerCount),
            branchItems.Sum(item => item.FuturesSalesCount),
            Round(branchItems.Sum(item => item.FuturesSalesTotal)),
            branchItems);
    }

    public async Task<CashTurnoverDetailDto> GetDetailAsync(
        CashTurnoverDetailRequest request,
        CancellationToken cancellationToken)
    {
        ValidateDetailRequest(request);

        var details = new List<CashTurnoverDetailDto>();

        if (request.Source is CashTurnoverSource.New or CashTurnoverSource.All)
        {
            var newDetail = await TryGetNewDetailAsync(request, cancellationToken);

            if (newDetail is not null)
            {
                details.Add(newDetail);
            }
        }

        if (request.Source is CashTurnoverSource.Old or CashTurnoverSource.All)
        {
            var oldDetail = await TryGetOldDetailAsync(request, cancellationToken);

            if (oldDetail is not null)
            {
                details.Add(oldDetail);
            }
        }

        if (details.Count == 0)
        {
            throw new KeyNotFoundException("Cash turnover detail was not found.");
        }

        return details.Count == 1
            ? details[0]
            : MergeDetails(details, request.Source);
    }

    private async Task<IReadOnlyCollection<CashTurnoverListItemDto>> ListNewAsync(
        CashTurnoverListRequest request,
        CancellationToken cancellationToken)
    {
        var (startDate, endDateExclusive) = NormalizeDateRange(request.WarehouseNo, request.StartDate, request.EndDate);
        var salesRows = await GetNewSalesSummaryRowsAsync(request.WarehouseNo, startDate, endDateExclusive, cancellationToken);
        var collectionRows = await GetNewCollectionSummaryRowsAsync(request.WarehouseNo, startDate, endDateExclusive, cancellationToken);

        var salesByKey = salesRows.ToDictionary(row => row.Key);
        var collectionsByKey = collectionRows.ToDictionary(row => row.Key);

        return salesByKey.Keys
            .Union(collectionsByKey.Keys)
            .OrderBy(key => key.BusinessDate)
            .ThenBy(key => key.WarehouseNo)
            .ThenBy(key => key.ShiftNo)
            .ThenBy(key => key.CashierCode, StringComparer.OrdinalIgnoreCase)
            .Select(key =>
            {
                salesByKey.TryGetValue(key, out var salesRow);
                collectionsByKey.TryGetValue(key, out var collectionRow);

                var warehouseName = FirstNonEmpty(salesRow?.WarehouseName, collectionRow?.WarehouseName);
                var cashierName = CombineName(
                    FirstNonEmpty(salesRow?.CashierFirstName, collectionRow?.CashierFirstName),
                    FirstNonEmpty(salesRow?.CashierLastName, collectionRow?.CashierLastName));
                var totalSalesAmount = Round(salesRow?.TotalSalesAmount ?? 0d);
                var totalCollectionAmount = Round(collectionRow?.TotalCollectionAmount ?? 0d);
                var totalCustomerCommission = Round(collectionRow?.TotalCustomerCommission ?? 0d);

                return new CashTurnoverListItemDto(
                    key.BusinessDate,
                    key.WarehouseNo,
                    warehouseName,
                    key.ShiftNo,
                    key.CashierCode,
                    cashierName,
                    salesRow?.ProductLineCount ?? 0,
                    Round(salesRow?.TotalSalesQuantity ?? 0d),
                    totalSalesAmount,
                    collectionRow?.PaymentLineCount ?? 0,
                    totalCollectionAmount,
                    totalCustomerCommission,
                    Round(totalCollectionAmount - totalCustomerCommission),
                    CashTurnoverSource.New.ToApiValue());
            })
            .ToArray();
    }

    private async Task<IReadOnlyCollection<CashTurnoverListItemDto>> ListOldAsync(
        CashTurnoverListRequest request,
        CancellationToken cancellationToken)
    {
        var (startDate, endDateExclusive) = NormalizeDateRange(request.WarehouseNo, request.StartDate, request.EndDate);

        const string sql = """
            WITH LegacyTotals AS (
                SELECT
                    CAST(tt.TurnoverDate AS date) AS BusinessDate,
                    tt.BranchNo AS WarehouseNo,
                    SUM(tt.CustomerCount) AS CustomerCount,
                    SUM(tt.TurnoverOverallTotal) AS TotalAmount
                FROM TurnoverTotals tt
                WHERE tt.TurnoverDate >= @startDate
                  AND tt.TurnoverDate < @endDateExclusive
                  AND (@warehouseNo IS NULL OR tt.BranchNo = @warehouseNo)
                GROUP BY
                    CAST(tt.TurnoverDate AS date),
                    tt.BranchNo
            )
            SELECT
                lt.BusinessDate,
                lt.WarehouseNo,
                COALESCE(w.dep_adi, '') AS WarehouseName,
                lt.CustomerCount,
                lt.TotalAmount
            FROM LegacyTotals lt
            LEFT JOIN DEPOLAR w
                ON lt.WarehouseNo = w.dep_no
            WHERE lt.TotalAmount <> 0
            ORDER BY
                lt.BusinessDate,
                lt.WarehouseNo;
            """;

        return await ExecuteLegacyReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@startDate", startDate);
                AddParameter(command, "@endDateExclusive", endDateExclusive);
                AddParameter(command, "@warehouseNo", request.WarehouseNo);
            },
            reader =>
            {
                var totalAmount = Round(ReadDouble(reader, "TotalAmount"));

                return new CashTurnoverListItemDto(
                    ReadDateTime(reader, "BusinessDate"),
                    ReadInt(reader, "WarehouseNo"),
                    ReadString(reader, "WarehouseName"),
                    0,
                    string.Empty,
                    string.Empty,
                    0,
                    0d,
                    totalAmount,
                    ReadInt(reader, "CustomerCount"),
                    totalAmount,
                    0d,
                    totalAmount,
                    CashTurnoverSource.Old.ToApiValue());
            },
            cancellationToken);
    }

    private async Task<CashTurnoverDetailDto?> TryGetNewDetailAsync(
        CashTurnoverDetailRequest request,
        CancellationToken cancellationToken)
    {
        var businessDate = request.BusinessDate.Date;
        var nextDate = businessDate.AddDays(1);
        var detailKey = new CashTurnoverSummaryKey(
            businessDate,
            request.WarehouseNo,
            request.ShiftNo,
            NormalizeCode(request.CashierCode));
        var salesRows = await GetNewSalesSummaryRowsAsync(request.WarehouseNo, businessDate, nextDate, cancellationToken);
        var collectionRows = await GetNewCollectionSummaryRowsAsync(request.WarehouseNo, businessDate, nextDate, cancellationToken);
        var paymentRows = await GetNewPaymentDetailRowsAsync(request, cancellationToken);

        var salesRow = salesRows.FirstOrDefault(row => row.Key == detailKey);
        var collectionRow = collectionRows.FirstOrDefault(row => row.Key == detailKey);

        if (salesRow is null && collectionRow is null && paymentRows.Count == 0)
        {
            return null;
        }

        var warehouseName = FirstNonEmpty(salesRow?.WarehouseName, collectionRow?.WarehouseName);
        var cashierName = CombineName(
            FirstNonEmpty(salesRow?.CashierFirstName, collectionRow?.CashierFirstName),
            FirstNonEmpty(salesRow?.CashierLastName, collectionRow?.CashierLastName));
        var totalSalesAmount = Round(salesRow?.TotalSalesAmount ?? 0d);
        var totalCollectionAmount = Round(collectionRow?.TotalCollectionAmount ?? paymentRows.Sum(item => item.Amount));
        var totalCustomerCommission = Round(collectionRow?.TotalCustomerCommission ?? paymentRows.Sum(item => item.CustomerCommission));

        return new CashTurnoverDetailDto(
            new CashTurnoverHeaderDto(
                detailKey.BusinessDate,
                detailKey.WarehouseNo,
                warehouseName,
                detailKey.ShiftNo,
                detailKey.CashierCode,
                cashierName,
                salesRow?.ProductLineCount ?? 0,
                Round(salesRow?.TotalSalesQuantity ?? 0d),
                totalSalesAmount,
                collectionRow?.PaymentLineCount ?? paymentRows.Sum(item => item.PaymentLineCount),
                totalCollectionAmount,
                totalCustomerCommission,
                Round(totalCollectionAmount - totalCustomerCommission),
                CashTurnoverSource.New.ToApiValue()),
            paymentRows
                .OrderBy(item => item.PaymentTypeNo)
                .ThenBy(item => item.CashBankCode, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private Task<CashTurnoverDetailDto?> TryGetOldDetailAsync(
        CashTurnoverDetailRequest request,
        CancellationToken cancellationToken)
    {
        _ = request;
        _ = cancellationToken;

        return Task.FromResult<CashTurnoverDetailDto?>(null);
    }

    private async Task<IReadOnlyCollection<BranchOverviewRow>> GetOldOverviewItemsAsync(
        int? warehouseNo,
        DateTime startDate,
        DateTime endDateExclusive,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH TotalBranch AS (
                SELECT
                    tt.BranchNo,
                    SUM(tt.CustomerCount) AS CustomerCount,
                    SUM(tt.DiscountCardCustomerCount) AS DiscountCardCustomerCount,
                    SUM(tt.FurparaCardCustomerCount) AS FurparaCardCustomerCount,
                    SUM(tt.ExpenseNoteCount) AS ExpenseNoteCount,
                    SUM(tt.FuturesSalesCount) AS FuturesSalesCount,
                    SUM(tt.TurnoverOverallTotal) AS OverallTotal
                FROM TurnoverTotals tt
                WHERE tt.TurnoverDate >= @startDate
                  AND tt.TurnoverDate < @endDateExclusive
                  AND (@warehouseNo IS NULL OR tt.BranchNo = @warehouseNo)
                GROUP BY tt.BranchNo
            ),
            DetailBranch AS (
                SELECT
                    tt.BranchNo,
                    MAX(td.LastBillTime) AS LastBillTime,
                    SUM(td.CashTotal) AS CashTotal,
                    SUM(td.CreditTotal) AS CreditTotal,
                    SUM(td.GiftCardTotal) AS GiftCardTotal,
                    SUM(td.ExpenseNoteTotal) AS ExpenseNoteTotal,
                    SUM(td.FuturesSalesTotal) AS FuturesSalesTotal
                FROM TurnoverTotals tt
                INNER JOIN TurnoverDetails td
                    ON tt.TurnoverId = td.TurnoverId
                WHERE tt.TurnoverDate >= @startDate
                  AND tt.TurnoverDate < @endDateExclusive
                  AND (@warehouseNo IS NULL OR tt.BranchNo = @warehouseNo)
                GROUP BY tt.BranchNo
            )
            SELECT
                COALESCE(dep.dep_bolge_kodu, '') AS Region,
                tb.BranchNo,
                COALESCE(dep.dep_adi, '') AS BranchName,
                tb.CustomerCount,
                tb.DiscountCardCustomerCount,
                tb.FurparaCardCustomerCount,
                db.LastBillTime,
                COALESCE(db.CashTotal, 0) AS CashTotal,
                COALESCE(db.CreditTotal, 0) AS CreditTotal,
                COALESCE(db.GiftCardTotal, 0) AS GiftCardTotal,
                COALESCE(db.ExpenseNoteTotal, 0) AS ExpenseNoteTotal,
                tb.ExpenseNoteCount,
                tb.OverallTotal,
                COALESCE(db.FuturesSalesTotal, 0) AS FuturesSalesTotal,
                tb.FuturesSalesCount
            FROM TotalBranch tb
            LEFT JOIN DetailBranch db
                ON tb.BranchNo = db.BranchNo
            LEFT JOIN DEPOLAR dep
                ON tb.BranchNo = dep.dep_no
            WHERE tb.OverallTotal <> 0
               OR COALESCE(db.CashTotal, 0) <> 0
               OR COALESCE(db.CreditTotal, 0) <> 0
               OR COALESCE(db.GiftCardTotal, 0) <> 0
               OR COALESCE(db.ExpenseNoteTotal, 0) <> 0
               OR COALESCE(db.FuturesSalesTotal, 0) <> 0
            ORDER BY tb.BranchNo;
            """;

        return await ExecuteLegacyReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@startDate", startDate);
                AddParameter(command, "@endDateExclusive", endDateExclusive);
                AddParameter(command, "@warehouseNo", warehouseNo);
            },
            reader =>
            {
                var overallTotal = Round(ReadDouble(reader, "OverallTotal"));
                var customerCount = ReadInt(reader, "CustomerCount");

                return new BranchOverviewRow(
                    ReadString(reader, "Region"),
                    ReadInt(reader, "BranchNo"),
                    ReadString(reader, "BranchName"),
                    customerCount,
                    ReadInt(reader, "DiscountCardCustomerCount"),
                    ReadInt(reader, "FurparaCardCustomerCount"),
                    ReadTimeSpan(reader, "LastBillTime"),
                    Round(ReadDouble(reader, "CashTotal")),
                    Round(ReadDouble(reader, "CreditTotal")),
                    Round(ReadDouble(reader, "GiftCardTotal")),
                    Round(ReadDouble(reader, "ExpenseNoteTotal")),
                    ReadInt(reader, "ExpenseNoteCount"),
                    overallTotal,
                    Round(ReadDouble(reader, "FuturesSalesTotal")),
                    ReadInt(reader, "FuturesSalesCount"),
                    Divide(overallTotal, customerCount));
            },
            cancellationToken);
    }

    private async Task<IReadOnlyCollection<BranchOverviewRow>> GetNewOverviewItemsAsync(
        int? warehouseNo,
        DateTime startDate,
        DateTime endDateExclusive,
        CancellationToken cancellationToken)
    {
        var warehouseCode = warehouseNo?.ToString(CultureInfo.InvariantCulture);
        var salesQuery = CreateCompletedReceivedSalesQuery(startDate, endDateExclusive);

        if (!string.IsNullOrWhiteSpace(warehouseCode))
        {
            salesQuery = salesQuery.Where(sale => sale.Subeno == warehouseCode);
        }

        // Each receipt should count as a single customer even when the source contains repeated sale rows.
        var salesRows = await salesQuery
            .Select(sale => new
            {
                BusinessDate = sale.ReceivedAt!.Value.Date,
                WarehouseCode = sale.Subeno ?? string.Empty,
                CashRegisterCode = sale.Kasano ?? string.Empty,
                ReceiptNumberKey = sale.ReceiptNumber != null && sale.ReceiptNumber != string.Empty
                    ? sale.ReceiptNumber
                    : null,
                UuidKey = (sale.ReceiptNumber == null || sale.ReceiptNumber == string.Empty) &&
                          sale.Uuid != null &&
                          sale.Uuid != string.Empty
                    ? sale.Uuid
                    : null,
                SaleIdKey = (sale.ReceiptNumber == null || sale.ReceiptNumber == string.Empty) &&
                            (sale.Uuid == null || sale.Uuid == string.Empty)
                    ? sale.Id
                    : 0,
                sale.ReceivedAt,
                TotalPrice = sale.TotalPrice ?? 0d
            })
            .GroupBy(sale => new
            {
                sale.BusinessDate,
                sale.WarehouseCode,
                sale.CashRegisterCode,
                sale.ReceiptNumberKey,
                sale.UuidKey,
                sale.SaleIdKey
            })
            .Select(grouped => new
            {
                grouped.Key.WarehouseCode,
                LastBillAt = grouped.Max(item => item.ReceivedAt),
                OverallTotal = grouped.Max(item => item.TotalPrice)
            })
            .GroupBy(sale => sale.WarehouseCode)
            .Select(grouped => new NewBranchSalesSqlRow(
                grouped.Key,
                grouped.Count(),
                grouped.Max(item => item.LastBillAt),
                grouped.Sum(item => item.OverallTotal)))
            .ToListAsync(cancellationToken);

        var resolvedPaymentRows = await LoadResolvedNewPaymentRowsAsync(
            startDate,
            endDateExclusive,
            warehouseCode,
            null,
            null,
            cancellationToken);
        var paymentRows = resolvedPaymentRows
            .GroupBy(payment => new
            {
                payment.WarehouseCode,
                payment.PaymentMethodCode
            })
            .Select(grouped => new NewBranchPaymentSqlRow(
                grouped.Key.WarehouseCode,
                grouped.Key.PaymentMethodCode,
                grouped.Sum(item => item.Amount)))
            .ToArray();
        var warehouseNos = salesRows
            .Select(item => ParseInt(item.WarehouseCode))
            .Concat(paymentRows.Select(item => ParseInt(item.WarehouseCode)))
            .Where(value => value > 0)
            .Distinct()
            .OrderBy(value => value)
            .ToArray();

        if (warehouseNos.Length == 0)
        {
            return Array.Empty<BranchOverviewRow>();
        }

        var shopigoBranchLookup = await LoadBranchLookupAsync(warehouseNos, cancellationToken);
        var legacyWarehouseLookup = await LoadLegacyWarehouseLookupAsync(warehouseNos, cancellationToken);
        var paymentMethodLookup = await LoadPaymentMethodLookupAsync(
            paymentRows.Select(item => item.PaymentMethodCode),
            cancellationToken);
        var salesByWarehouse = salesRows
            .Select(item => new
            {
                WarehouseNo = ParseInt(item.WarehouseCode),
                Row = item
            })
            .Where(item => item.WarehouseNo > 0)
            .GroupBy(item => item.WarehouseNo)
            .ToDictionary(
                grouped => grouped.Key,
                grouped => new NewBranchSalesSqlRow(
                    grouped.First().Row.WarehouseCode,
                    grouped.Sum(item => item.Row.CustomerCount),
                    grouped.Max(item => item.Row.LastBillAt),
                    grouped.Sum(item => item.Row.OverallTotal)));
        var paymentTotalsByWarehouse = paymentRows
            .Select(item =>
            {
                paymentMethodLookup.TryGetValue(item.PaymentMethodCode, out var paymentMethod);

                return new
                {
                    WarehouseNo = ParseInt(item.WarehouseCode),
                    Category = ClassifyNewPaymentMethod(paymentMethod, item.PaymentMethodCode),
                    item.Amount
                };
            })
            .Where(item => item.WarehouseNo > 0)
            .GroupBy(item => item.WarehouseNo)
            .ToDictionary(
                grouped => grouped.Key,
                grouped => new PaymentCategoryTotals(
                    Round(grouped.Where(item => item.Category == PaymentCategory.Cash).Sum(item => item.Amount)),
                    Round(grouped.Where(item => item.Category == PaymentCategory.Credit).Sum(item => item.Amount)),
                    Round(grouped.Where(item => item.Category == PaymentCategory.GiftCard).Sum(item => item.Amount)),
                    Round(grouped.Where(item => item.Category != PaymentCategory.None).Sum(item => item.Amount))));

        return warehouseNos
            .Select(currentWarehouseNo =>
            {
                salesByWarehouse.TryGetValue(currentWarehouseNo, out var salesRow);
                paymentTotalsByWarehouse.TryGetValue(currentWarehouseNo, out var paymentTotals);
                shopigoBranchLookup.TryGetValue(currentWarehouseNo, out var shopigoBranchName);
                legacyWarehouseLookup.TryGetValue(currentWarehouseNo, out var legacyWarehouseInfo);

                var overallTotal = Round(paymentTotals?.TotalAmount ?? salesRow?.OverallTotal ?? 0d);
                var customerCount = salesRow?.CustomerCount ?? 0;

                return new BranchOverviewRow(
                    legacyWarehouseInfo?.Region ?? string.Empty,
                    currentWarehouseNo,
                    FirstNonEmpty(shopigoBranchName, legacyWarehouseInfo?.Name),
                    customerCount,
                    0,
                    0,
                    salesRow?.LastBillAt?.TimeOfDay,
                    paymentTotals?.CashTotal ?? 0d,
                    paymentTotals?.CreditTotal ?? 0d,
                    paymentTotals?.GiftCardTotal ?? 0d,
                    0d,
                    0,
                    overallTotal,
                    0d,
                    0,
                    Divide(overallTotal, customerCount));
            })
            .Where(item =>
                item.OverallTotal != 0 ||
                item.CashTotal != 0 ||
                item.CreditTotal != 0 ||
                item.GiftCardTotal != 0)
            .ToArray();
    }

    private async Task<IReadOnlyCollection<SalesSummaryRow>> GetNewSalesSummaryRowsAsync(
        int? warehouseNo,
        DateTime startDate,
        DateTime endDateExclusive,
        CancellationToken cancellationToken)
    {
        var warehouseCode = warehouseNo?.ToString(CultureInfo.InvariantCulture);
        var rows = await (
            from sale in CreateCompletedReceivedSalesQuery(startDate, endDateExclusive)
            join saleItem in shopigoCiroDbContext.SaleItems.AsNoTracking()
                on sale.Uuid equals saleItem.SaleUuid
            where saleItem.DeletedAt == null &&
                  saleItem.Refunded == 0 &&
                  (warehouseCode == null || sale.Subeno == warehouseCode)
            group saleItem by new
            {
                BusinessDate = sale.ReceivedAt!.Value.Date,
                WarehouseCode = sale.Subeno,
                ShiftCode = sale.Kasano,
                CashierCode = sale.InitiatedBy
            }
            into grouped
            select new SalesSummarySqlRow(
                grouped.Key.BusinessDate,
                grouped.Key.WarehouseCode ?? string.Empty,
                grouped.Key.ShiftCode ?? string.Empty,
                grouped.Key.CashierCode ?? string.Empty,
                grouped.Count(),
                grouped.Sum(item => item.Quantity ?? 0m),
                grouped.Sum(item => item.TotalPrice ?? 0d)))
            .ToListAsync(cancellationToken);

        return await MapSalesSummaryRowsAsync(rows, cancellationToken);
    }

    private async Task<IReadOnlyCollection<CollectionSummaryRow>> GetNewCollectionSummaryRowsAsync(
        int? warehouseNo,
        DateTime startDate,
        DateTime endDateExclusive,
        CancellationToken cancellationToken)
    {
        var warehouseCode = warehouseNo?.ToString(CultureInfo.InvariantCulture);
        var paymentRows = await LoadResolvedNewPaymentRowsAsync(
            startDate,
            endDateExclusive,
            warehouseCode,
            null,
            null,
            cancellationToken);
        var rows = paymentRows
            .GroupBy(payment => new
            {
                payment.BusinessDate,
                payment.WarehouseCode,
                payment.ShiftCode,
                payment.CashierCode
            })
            .Select(grouped => new CollectionSummarySqlRow(
                grouped.Key.BusinessDate,
                grouped.Key.WarehouseCode,
                grouped.Key.ShiftCode,
                grouped.Key.CashierCode,
                grouped.Count(),
                grouped.Sum(item => item.Amount)))
            .ToArray();

        return await MapCollectionSummaryRowsAsync(rows, cancellationToken);
    }

    private async Task<IReadOnlyCollection<CashTurnoverPaymentDetailItemDto>> GetNewPaymentDetailRowsAsync(
        CashTurnoverDetailRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseCode = request.WarehouseNo.ToString(CultureInfo.InvariantCulture);
        var shiftCode = request.ShiftNo.ToString(CultureInfo.InvariantCulture);
        var cashierCode = NormalizeCode(request.CashierCode);
        var businessDate = request.BusinessDate.Date;
        var nextDate = businessDate.AddDays(1);

        var paymentRows = await LoadResolvedNewPaymentRowsAsync(
            businessDate,
            nextDate,
            warehouseCode,
            shiftCode,
            cashierCode,
            cancellationToken);
        var rows = paymentRows
            .GroupBy(payment => payment.PaymentMethodCode)
            .Select(grouped => new PaymentDetailSqlRow(
                grouped.Key,
                grouped.Count(),
                grouped.Sum(item => item.Amount)))
            .ToArray();

        var paymentMethodLookup = await LoadPaymentMethodLookupAsync(
            rows.Select(item => item.PaymentMethodCode),
            cancellationToken);

        return rows
            .Select(row =>
            {
                paymentMethodLookup.TryGetValue(row.PaymentMethodCode, out var paymentMethod);
                var resolvedPaymentMethodName = FirstNonEmpty(paymentMethod?.Name, row.PaymentMethodCode);

                return new CashTurnoverPaymentDetailItemDto(
                    ParseInt(row.PaymentMethodCode),
                    resolvedPaymentMethodName,
                    row.PaymentMethodCode,
                    resolvedPaymentMethodName,
                    row.PaymentLineCount,
                    Round(row.Amount),
                    0d,
                    Round(row.Amount),
                    CashTurnoverSource.New.ToApiValue());
            })
            .ToArray();
    }

    private IQueryable<ShopigoReceivedSale> CreateCompletedReceivedSalesQuery(
        DateTime startDate,
        DateTime endDateExclusive) =>
        shopigoCiroDbContext.ReceivedSales.AsNoTracking()
            .Where(sale =>
                sale.DeletedAt == null &&
                sale.Status == CompletedReceivedSaleStatus &&
                sale.ReceivedAt.HasValue &&
                sale.ReceivedAt.Value >= startDate &&
                sale.ReceivedAt.Value < endDateExclusive);

    private async Task<IReadOnlyCollection<NewPaymentSaleRow>> LoadResolvedNewPaymentRowsAsync(
        DateTime startDate,
        DateTime endDateExclusive,
        string? warehouseCode,
        string? shiftCode,
        string? cashierCode,
        CancellationToken cancellationToken)
    {
        var salesQuery = CreateCompletedReceivedSalesQuery(startDate, endDateExclusive);

        if (!string.IsNullOrWhiteSpace(warehouseCode))
        {
            salesQuery = salesQuery.Where(sale => sale.Subeno == warehouseCode);
        }

        if (!string.IsNullOrWhiteSpace(shiftCode))
        {
            salesQuery = salesQuery.Where(sale => sale.Kasano == shiftCode);
        }

        if (!string.IsNullOrWhiteSpace(cashierCode))
        {
            salesQuery = salesQuery.Where(sale => (sale.InitiatedBy ?? string.Empty) == cashierCode);
        }

        var rows = await (
            from sale in salesQuery
            join payment in shopigoCiroDbContext.Payments.AsNoTracking()
                on sale.Uuid equals payment.SaleUuid
            where payment.DeletedAt == null &&
                  payment.Refunded == 0
            select new
            {
                BusinessDate = sale.ReceivedAt!.Value.Date,
                WarehouseCode = sale.Subeno ?? string.Empty,
                ShiftCode = sale.Kasano ?? string.Empty,
                CashierCode = sale.InitiatedBy ?? string.Empty,
                SaleUuid = sale.Uuid ?? string.Empty,
                SaleTotal = sale.TotalPrice ?? 0d,
                PaymentId = payment.Id,
                PaymentMethodCode = payment.PaymentMethod ?? string.Empty,
                Amount = payment.Amount ?? 0d
            })
            .ToListAsync(cancellationToken);

        return ResolveNewPaymentRows(rows.Select(row => new NewPaymentSaleRow(
            row.BusinessDate,
            row.WarehouseCode,
            row.ShiftCode,
            row.CashierCode,
            row.SaleUuid,
            row.SaleTotal,
            row.PaymentId,
            row.PaymentMethodCode,
            row.Amount)));
    }

    private async Task<IReadOnlyCollection<SalesSummaryRow>> MapSalesSummaryRowsAsync(
        IReadOnlyCollection<SalesSummarySqlRow> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return Array.Empty<SalesSummaryRow>();
        }

        var warehouseLookup = await LoadBranchLookupAsync(
            rows.Select(item => ParseInt(item.WarehouseCode)),
            cancellationToken);
        var employeeLookup = await LoadEmployeeLookupAsync(
            rows.Select(item => item.CashierCode),
            cancellationToken);

        return rows
            .Select(row =>
            {
                var key = new CashTurnoverSummaryKey(
                    row.BusinessDate,
                    ParseInt(row.WarehouseCode),
                    ParseInt(row.ShiftCode),
                    NormalizeCode(row.CashierCode));
                warehouseLookup.TryGetValue(key.WarehouseNo, out var warehouseName);
                employeeLookup.TryGetValue(key.CashierCode, out var employeeName);

                return new SalesSummaryRow(
                    key,
                    warehouseName ?? string.Empty,
                    employeeName?.FirstName ?? string.Empty,
                    employeeName?.LastName ?? string.Empty,
                    row.ProductLineCount,
                    decimal.ToDouble(row.TotalSalesQuantity),
                    row.TotalSalesAmount);
            })
            .GroupBy(row => row.Key)
            .Select(grouped => new SalesSummaryRow(
                grouped.Key,
                FirstNonEmpty(grouped.Select(item => item.WarehouseName).ToArray()),
                FirstNonEmpty(grouped.Select(item => item.CashierFirstName).ToArray()),
                FirstNonEmpty(grouped.Select(item => item.CashierLastName).ToArray()),
                grouped.Sum(item => item.ProductLineCount),
                grouped.Sum(item => item.TotalSalesQuantity),
                grouped.Sum(item => item.TotalSalesAmount)))
            .ToArray();
    }

    private async Task<IReadOnlyCollection<CollectionSummaryRow>> MapCollectionSummaryRowsAsync(
        IReadOnlyCollection<CollectionSummarySqlRow> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return Array.Empty<CollectionSummaryRow>();
        }

        var warehouseLookup = await LoadBranchLookupAsync(
            rows.Select(item => ParseInt(item.WarehouseCode)),
            cancellationToken);
        var employeeLookup = await LoadEmployeeLookupAsync(
            rows.Select(item => item.CashierCode),
            cancellationToken);

        return rows
            .Select(row =>
            {
                var key = new CashTurnoverSummaryKey(
                    row.BusinessDate,
                    ParseInt(row.WarehouseCode),
                    ParseInt(row.ShiftCode),
                    NormalizeCode(row.CashierCode));
                warehouseLookup.TryGetValue(key.WarehouseNo, out var warehouseName);
                employeeLookup.TryGetValue(key.CashierCode, out var employeeName);

                return new CollectionSummaryRow(
                    key,
                    warehouseName ?? string.Empty,
                    employeeName?.FirstName ?? string.Empty,
                    employeeName?.LastName ?? string.Empty,
                    row.PaymentLineCount,
                    row.TotalCollectionAmount,
                    0d);
            })
            .GroupBy(row => row.Key)
            .Select(grouped => new CollectionSummaryRow(
                grouped.Key,
                FirstNonEmpty(grouped.Select(item => item.WarehouseName).ToArray()),
                FirstNonEmpty(grouped.Select(item => item.CashierFirstName).ToArray()),
                FirstNonEmpty(grouped.Select(item => item.CashierLastName).ToArray()),
                grouped.Sum(item => item.PaymentLineCount),
                grouped.Sum(item => item.TotalCollectionAmount),
                0d))
            .ToArray();
    }

    private async Task<IReadOnlyDictionary<int, string>> LoadBranchLookupAsync(
        IEnumerable<int> warehouseNumbers,
        CancellationToken cancellationToken)
    {
        var warehouseList = warehouseNumbers
            .Where(value => value > 0)
            .Distinct()
            .ToArray();

        if (warehouseList.Length == 0)
        {
            return new Dictionary<int, string>();
        }

        var rows = await shopigoCiroDbContext.Branches.AsNoTracking()
            .Where(branch => branch.DeletedAt == null && warehouseList.Contains(branch.DepoId))
            .Select(branch => new
            {
                branch.DepoId,
                branch.Name
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.DepoId)
            .ToDictionary(
                grouped => grouped.Key,
                grouped => FirstNonEmpty(grouped.Select(item => item.Name).ToArray()));
    }

    private async Task<IReadOnlyDictionary<int, LegacyWarehouseInfo>> LoadLegacyWarehouseLookupAsync(
        IEnumerable<int> warehouseNumbers,
        CancellationToken cancellationToken)
    {
        var warehouseList = warehouseNumbers
            .Where(value => value > 0)
            .Distinct()
            .ToArray();

        if (warehouseList.Length == 0)
        {
            return new Dictionary<int, LegacyWarehouseInfo>();
        }

        var rows = await mikroDbContext.DEPOLARs.AsNoTracking()
            .Where(warehouse => warehouse.dep_no.HasValue && warehouseList.Contains(warehouse.dep_no.Value))
            .Select(warehouse => new
            {
                WarehouseNo = warehouse.dep_no!.Value,
                warehouse.dep_adi,
                warehouse.dep_bolge_kodu
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.WarehouseNo)
            .ToDictionary(
                grouped => grouped.Key,
                grouped => new LegacyWarehouseInfo(
                    FirstNonEmpty(grouped.Select(item => item.dep_adi).ToArray()),
                    FirstNonEmpty(grouped.Select(item => item.dep_bolge_kodu).ToArray())));
    }

    private async Task<IReadOnlyDictionary<string, EmployeeName>> LoadEmployeeLookupAsync(
        IEnumerable<string> cashierCodes,
        CancellationToken cancellationToken)
    {
        var normalizedCodes = cashierCodes
            .Select(NormalizeCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedCodes.Length == 0)
        {
            return new Dictionary<string, EmployeeName>(StringComparer.OrdinalIgnoreCase);
        }

        var rows = await shopigoCiroDbContext.Employees.AsNoTracking()
            .Where(employee => employee.DeletedAt == null && employee.Code != null && normalizedCodes.Contains(employee.Code))
            .Select(employee => new
            {
                employee.Code,
                employee.Name,
                employee.Surname
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => NormalizeCode(row.Code), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                grouped => grouped.Key,
                grouped => new EmployeeName(
                    FirstNonEmpty(grouped.Select(item => item.Name).ToArray()),
                    FirstNonEmpty(grouped.Select(item => item.Surname).ToArray())),
                StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyDictionary<string, PaymentMethodInfo>> LoadPaymentMethodLookupAsync(
        IEnumerable<string> paymentMethodCodes,
        CancellationToken cancellationToken)
    {
        var paymentMethodNumbers = paymentMethodCodes
            .Select(ParseInt)
            .Where(value => value > 0)
            .Distinct()
            .ToArray();

        if (paymentMethodNumbers.Length == 0)
        {
            return new Dictionary<string, PaymentMethodInfo>(StringComparer.OrdinalIgnoreCase);
        }

        var rows = await shopigoCiroDbContext.PaymentMethods.AsNoTracking()
            .Where(method =>
                method.Status == 1 &&
                (paymentMethodNumbers.Contains(method.PavoMediator) || paymentMethodNumbers.Contains(method.Id)))
            .Select(method => new
            {
                method.Id,
                method.Name,
                method.PavoType,
                method.PavoMediator
            })
            .ToListAsync(cancellationToken);

        var lookup = new Dictionary<string, PaymentMethodInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var paymentMethodNumber in paymentMethodNumbers)
        {
            var row = rows
                .Where(item => item.PavoMediator == paymentMethodNumber)
                .OrderBy(item => item.Id == paymentMethodNumber ? 0 : 1)
                .ThenBy(item => item.Id)
                .FirstOrDefault()
                ?? rows
                    .Where(item => item.Id == paymentMethodNumber)
                    .OrderBy(item => item.Id)
                    .FirstOrDefault();

            if (row is null)
            {
                continue;
            }

            lookup[paymentMethodNumber.ToString(CultureInfo.InvariantCulture)] = new PaymentMethodInfo(
                row.Id,
                row.Name?.Trim() ?? string.Empty,
                row.PavoType,
                row.PavoMediator);
        }

        return lookup;
    }

    private async Task<IReadOnlyCollection<T>> ExecuteLegacyReaderAsync<T>(
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
            command.CommandTimeout = 180;
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

    private static CashTurnoverDetailDto MergeDetails(
        IReadOnlyCollection<CashTurnoverDetailDto> details,
        CashTurnoverSource source)
    {
        var firstDetail = details.First();

        var header = new CashTurnoverHeaderDto(
            firstDetail.Header.BusinessDate,
            firstDetail.Header.WarehouseNo,
            FirstNonEmpty(details.Select(item => item.Header.WarehouseName).ToArray()),
            firstDetail.Header.ShiftNo,
            firstDetail.Header.CashierCode,
            FirstNonEmpty(details.Select(item => item.Header.CashierName).ToArray()),
            details.Sum(item => item.Header.ProductLineCount),
            Round(details.Sum(item => item.Header.TotalSalesQuantity)),
            Round(details.Sum(item => item.Header.TotalSalesAmount)),
            details.Sum(item => item.Header.PaymentLineCount),
            Round(details.Sum(item => item.Header.TotalCollectionAmount)),
            Round(details.Sum(item => item.Header.TotalCustomerCommission)),
            Round(details.Sum(item => item.Header.NetCollectionAmount)),
            source.ToApiValue());

        var payments = details
            .SelectMany(item => item.Payments)
            .GroupBy(item => new
            {
                item.Source,
                item.PaymentTypeNo,
                item.PaymentTypeName,
                item.CashBankCode,
                item.CashBankName
            })
            .Select(grouped => new CashTurnoverPaymentDetailItemDto(
                grouped.Key.PaymentTypeNo,
                grouped.Key.PaymentTypeName,
                grouped.Key.CashBankCode,
                grouped.Key.CashBankName,
                grouped.Sum(item => item.PaymentLineCount),
                Round(grouped.Sum(item => item.Amount)),
                Round(grouped.Sum(item => item.CustomerCommission)),
                Round(grouped.Sum(item => item.NetAmount)),
                grouped.Key.Source))
            .OrderBy(item => item.Source, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.PaymentTypeNo)
            .ThenBy(item => item.PaymentTypeName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CashTurnoverDetailDto(header, payments);
    }

    private static IReadOnlyCollection<CashTurnoverBranchOverviewItemDto> MergeOverviewItems(
        IReadOnlyCollection<BranchOverviewRow> items) =>
        items
            .GroupBy(item => item.BranchNo)
            .Select(grouped =>
            {
                var overallTotal = Round(grouped.Sum(item => item.OverallTotal));
                var customerCount = grouped.Sum(item => item.CustomerCount);

                return new CashTurnoverBranchOverviewItemDto(
                    FirstNonEmpty(grouped.Select(item => item.Region).ToArray()),
                    grouped.Key,
                    FirstNonEmpty(grouped.Select(item => item.BranchName).ToArray()),
                    customerCount,
                    grouped.Sum(item => item.DiscountCardCustomerCount),
                    grouped.Sum(item => item.FurparaCardCustomerCount),
                    FormatTime(MaxTime(grouped.Select(item => item.LastBillTime))),
                    Round(grouped.Sum(item => item.CashTotal)),
                    Round(grouped.Sum(item => item.CreditTotal)),
                    Round(grouped.Sum(item => item.GiftCardTotal)),
                    Round(grouped.Sum(item => item.ExpenseNoteTotal)),
                    grouped.Sum(item => item.ExpenseNoteCount),
                    overallTotal,
                    Round(grouped.Sum(item => item.FuturesSalesTotal)),
                    grouped.Sum(item => item.FuturesSalesCount),
                    Divide(overallTotal, customerCount));
            })
            .OrderBy(item => item.BranchName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.BranchNo)
            .ToArray();

    private static (DateTime StartDate, DateTime EndDateExclusive) NormalizeOverviewDateRange(
        int? warehouseNo,
        DateTime startDate,
        DateTime endDate)
    {
        if (warehouseNo.HasValue && warehouseNo.Value <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(warehouseNo));
        }

        var normalizedStartDate = startDate.Date;
        var normalizedEndDate = endDate.Date;

        if (normalizedEndDate < normalizedStartDate)
        {
            throw new ArgumentException("End date can not be earlier than start date.", nameof(endDate));
        }

        return (normalizedStartDate, normalizedEndDate.AddDays(1));
    }

    private static (DateTime StartDate, DateTime EndDateExclusive) NormalizeDateRange(
        int? warehouseNo,
        DateTime startDate,
        DateTime endDate)
    {
        if (warehouseNo is <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(warehouseNo));
        }

        var normalizedStartDate = startDate.Date;
        var normalizedEndDate = endDate.Date;

        if (normalizedEndDate < normalizedStartDate)
        {
            throw new ArgumentException("End date can not be earlier than start date.", nameof(endDate));
        }

        return (normalizedStartDate, normalizedEndDate.AddDays(1));
    }

    private static void ValidateDetailRequest(CashTurnoverDetailRequest request)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        if (request.ShiftNo < 0)
        {
            throw new ArgumentException("Shift no can not be negative.", nameof(request.ShiftNo));
        }

        if (string.IsNullOrWhiteSpace(request.CashierCode))
        {
            throw new ArgumentException("Cashier code is required.", nameof(request.CashierCode));
        }
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
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

    private static double ReadDouble(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? 0d
            : Convert.ToDouble(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static DateTime ReadDateTime(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? default
            : Convert.ToDateTime(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static TimeSpan? ReadTimeSpan(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetValue(ordinal);

        return value switch
        {
            TimeSpan timeSpan => timeSpan,
            DateTime dateTime => dateTime.TimeOfDay,
            _ when TimeSpan.TryParse(
                Convert.ToString(value, CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            _ => null
        };
    }

    private static string ReadString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? string.Empty
            : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
    }

    private static int ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;

    private static string NormalizeCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string CombineName(string? firstName, string? lastName) =>
        string.Join(
            " ",
            new[] { firstName, lastName }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static double Round(double value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static double Divide(double total, int divisor) =>
        divisor <= 0 ? 0d : total / divisor;

    private static TimeSpan? MaxTime(IEnumerable<TimeSpan?> values)
    {
        var timeValues = values
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();

        return timeValues.Length == 0 ? null : timeValues.Max();
    }

    private static string FormatTime(TimeSpan? value) =>
        value?.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture) ?? string.Empty;

    private static IReadOnlyCollection<NewPaymentSaleRow> ResolveNewPaymentRows(IEnumerable<NewPaymentSaleRow> rows) =>
        rows
            .GroupBy(row => new
            {
                row.BusinessDate,
                row.WarehouseCode,
                row.ShiftCode,
                row.CashierCode,
                row.SaleUuid
            })
            .SelectMany(grouped => ResolveSalePaymentRows(grouped.ToArray()))
            .ToArray();

    private static IReadOnlyCollection<NewPaymentSaleRow> ResolveSalePaymentRows(
        IReadOnlyList<NewPaymentSaleRow> saleRows)
    {
        if (saleRows.Count == 0)
        {
            return Array.Empty<NewPaymentSaleRow>();
        }

        var orderedRows = saleRows
            .OrderBy(row => row.PaymentId)
            .ToArray();
        var saleTotal = orderedRows[0].SaleTotal;

        if (AmountsMatch(orderedRows.Sum(row => row.Amount), saleTotal))
        {
            return orderedRows;
        }

        var matchingSubset = FindPaymentSubsetMatchingTotal(orderedRows, saleTotal);

        if (matchingSubset is not null)
        {
            return matchingSubset;
        }

        return orderedRows
            .GroupBy(row => new
            {
                row.PaymentMethodCode,
                Amount = ToCents(row.Amount)
            })
            .Select(grouped => grouped.OrderBy(row => row.PaymentId).First())
            .OrderBy(row => row.PaymentId)
            .ToArray();
    }

    private static IReadOnlyCollection<NewPaymentSaleRow>? FindPaymentSubsetMatchingTotal(
        IReadOnlyList<NewPaymentSaleRow> rows,
        double total)
    {
        var target = ToCents(total);

        if (target <= 0 || rows.Count > 24)
        {
            return null;
        }

        var orderedRows = rows
            .OrderByDescending(row => ToCents(row.Amount))
            .ThenBy(row => row.PaymentId)
            .ToArray();
        var selectedRows = new List<NewPaymentSaleRow>();

        if (!TryFindPaymentSubset(0, 0))
        {
            return null;
        }

        return selectedRows
            .OrderBy(row => row.PaymentId)
            .ToArray();

        bool TryFindPaymentSubset(int index, long totalInCents)
        {
            if (totalInCents == target)
            {
                return true;
            }

            if (totalInCents > target || index >= orderedRows.Length)
            {
                return false;
            }

            var row = orderedRows[index];
            selectedRows.Add(row);

            if (TryFindPaymentSubset(index + 1, totalInCents + ToCents(row.Amount)))
            {
                return true;
            }

            selectedRows.RemoveAt(selectedRows.Count - 1);

            return TryFindPaymentSubset(index + 1, totalInCents);
        }
    }

    private static bool AmountsMatch(double left, double right) =>
        Math.Abs(Round(left) - Round(right)) <= 0.01d;

    private static long ToCents(double value) =>
        Convert.ToInt64(Math.Round(value * 100d, MidpointRounding.AwayFromZero));

    private static PaymentCategory ClassifyNewPaymentMethod(
        PaymentMethodInfo? paymentMethod,
        string paymentMethodCode)
    {
        if (paymentMethod is null)
        {
            return ClassifyNewPaymentMethodByName(paymentMethodCode);
        }

        return paymentMethod.PavoType switch
        {
            1 => PaymentCategory.Cash,
            2 => PaymentCategory.Credit,
            3 => PaymentCategory.GiftCard,
            14 when NormalizeToken(paymentMethod.Name).Contains("odemesiz", StringComparison.OrdinalIgnoreCase) => PaymentCategory.None,
            _ => ClassifyNewPaymentMethodByName(paymentMethod.Name)
        };
    }

    private static PaymentCategory ClassifyNewPaymentMethodByName(string paymentMethodName)
    {
        var normalized = NormalizeToken(paymentMethodName);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return PaymentCategory.Credit;
        }

        if (normalized.Contains("odemesiz", StringComparison.OrdinalIgnoreCase))
        {
            return PaymentCategory.None;
        }

        if (normalized.Contains("nakit", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("cash", StringComparison.OrdinalIgnoreCase))
        {
            return PaymentCategory.Cash;
        }

        if (normalized.Contains("multinet", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("ticket", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("metropol", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("gift", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("setcard", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("tokenflex", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("birlikkart", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("premiumkart", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("pluxee", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("restaurant", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("yemekkarti", StringComparison.OrdinalIgnoreCase))
        {
            return PaymentCategory.GiftCard;
        }

        return PaymentCategory.Credit;
    }

    private static string NormalizeToken(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value
                .Trim()
                .Where(character => !char.IsWhiteSpace(character) && character != '-' && character != '_')
                .ToArray())
            .ToLowerInvariant();

    private sealed record EmployeeName(
        string FirstName,
        string LastName);

    private sealed record LegacyWarehouseInfo(
        string Name,
        string Region);

    private sealed record PaymentMethodInfo(
        int Id,
        string Name,
        int PavoType,
        int PavoMediator);

    private sealed record SalesSummarySqlRow(
        DateTime BusinessDate,
        string WarehouseCode,
        string ShiftCode,
        string CashierCode,
        int ProductLineCount,
        decimal TotalSalesQuantity,
        double TotalSalesAmount);

    private sealed record CollectionSummarySqlRow(
        DateTime BusinessDate,
        string WarehouseCode,
        string ShiftCode,
        string CashierCode,
        int PaymentLineCount,
        double TotalCollectionAmount);

    private sealed record NewPaymentSaleRow(
        DateTime BusinessDate,
        string WarehouseCode,
        string ShiftCode,
        string CashierCode,
        string SaleUuid,
        double SaleTotal,
        int PaymentId,
        string PaymentMethodCode,
        double Amount);

    private sealed record PaymentDetailSqlRow(
        string PaymentMethodCode,
        int PaymentLineCount,
        double Amount);

    private sealed record NewBranchSalesSqlRow(
        string WarehouseCode,
        int CustomerCount,
        DateTime? LastBillAt,
        double OverallTotal);

    private sealed record NewBranchPaymentSqlRow(
        string WarehouseCode,
        string PaymentMethodCode,
        double Amount);

    private sealed record CashTurnoverSummaryKey(
        DateTime BusinessDate,
        int WarehouseNo,
        int ShiftNo,
        string CashierCode);

    private sealed record SalesSummaryRow(
        CashTurnoverSummaryKey Key,
        string WarehouseName,
        string CashierFirstName,
        string CashierLastName,
        int ProductLineCount,
        double TotalSalesQuantity,
        double TotalSalesAmount);

    private sealed record CollectionSummaryRow(
        CashTurnoverSummaryKey Key,
        string WarehouseName,
        string CashierFirstName,
        string CashierLastName,
        int PaymentLineCount,
        double TotalCollectionAmount,
        double TotalCustomerCommission);

    private sealed record PaymentCategoryTotals(
        double CashTotal,
        double CreditTotal,
        double GiftCardTotal,
        double TotalAmount);

    private sealed record BranchOverviewRow(
        string Region,
        int BranchNo,
        string BranchName,
        int CustomerCount,
        int DiscountCardCustomerCount,
        int FurparaCardCustomerCount,
        TimeSpan? LastBillTime,
        double CashTotal,
        double CreditTotal,
        double GiftCardTotal,
        double ExpenseNoteTotal,
        int ExpenseNoteCount,
        double OverallTotal,
        double FuturesSalesTotal,
        int FuturesSalesCount,
        double AverageBasketAmount);

    private enum PaymentCategory
    {
        None = 0,
        Cash = 1,
        Credit = 2,
        GiftCard = 3
    }
}

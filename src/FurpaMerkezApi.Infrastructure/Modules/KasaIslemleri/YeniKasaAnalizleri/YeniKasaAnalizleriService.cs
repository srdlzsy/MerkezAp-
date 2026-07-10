using System.Globalization;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.YeniKasaAnalizleri;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Shopigo;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.YeniKasaAnalizleri;

public sealed class YeniKasaAnalizleriService(
    ShopigoCiroDbContext shopigoCiroDbContext,
    MikroDbContext mikroDbContext)
    : IYeniKasaAnalizleriService
{
    private const string CompletedReceivedSaleStatus = "4";
    private const int MaxTake = 2000;

    public async Task<IReadOnlyCollection<YeniKasaCiroOzetItemDto>> GetCiroOzetiAsync(
        YeniKasaAnalizRequest request,
        CancellationToken cancellationToken)
    {
        var data = await LoadAnalysisDataAsync(NormalizeRequest(request), cancellationToken);

        return data.Receipts
            .GroupBy(receipt => new
            {
                receipt.BusinessDate,
                receipt.WarehouseNo,
                receipt.CashRegisterNo,
                receipt.CashierCode
            })
            .Select(grouped =>
            {
                var saleUuids = grouped
                    .Select(item => item.Uuid)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var itemTotals = SumItems(data.ItemTotalsBySaleUuid, saleUuids);
                var paymentTotals = SumPayments(data.PaymentTotalsBySaleUuid, saleUuids);
                data.BranchNames.TryGetValue(grouped.Key.WarehouseNo, out var branchName);
                data.EmployeeNames.TryGetValue(grouped.Key.CashierCode, out var cashierName);
                var saleTotal = Round(grouped.Sum(item => item.SaleTotal));
                var paymentTotal = Round(paymentTotals.TotalAmount);

                return new YeniKasaCiroOzetItemDto(
                    grouped.Key.BusinessDate,
                    grouped.Key.WarehouseNo,
                    branchName ?? string.Empty,
                    grouped.Key.CashRegisterNo,
                    grouped.Key.CashierCode,
                    cashierName ?? string.Empty,
                    grouped.Sum(item => item.SaleRowCount),
                    grouped.Count(),
                    itemTotals.LineCount,
                    Round(itemTotals.Quantity),
                    saleTotal,
                    paymentTotals.LineCount,
                    paymentTotal,
                    Round(saleTotal - paymentTotal),
                    grouped.Min(item => item.FirstReceivedAt),
                    grouped.Max(item => item.LastReceivedAt));
            })
            .OrderBy(item => item.BusinessDate)
            .ThenBy(item => item.WarehouseNo)
            .ThenBy(item => item.CashRegisterNo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.CashierCode, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<YeniKasaKasaOzetItemDto>> GetKasaOzetiAsync(
        YeniKasaAnalizRequest request,
        CancellationToken cancellationToken)
    {
        var data = await LoadAnalysisDataAsync(NormalizeRequest(request), cancellationToken);

        return data.Receipts
            .GroupBy(receipt => new
            {
                receipt.BusinessDate,
                receipt.WarehouseNo,
                receipt.CashRegisterNo
            })
            .Select(grouped =>
            {
                var saleUuids = grouped
                    .Select(item => item.Uuid)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var paymentTotals = SumPayments(data.PaymentTotalsBySaleUuid, saleUuids);
                data.BranchNames.TryGetValue(grouped.Key.WarehouseNo, out var branchName);
                var saleTotal = Round(grouped.Sum(item => item.SaleTotal));
                var paymentTotal = Round(paymentTotals.TotalAmount);

                return new YeniKasaKasaOzetItemDto(
                    grouped.Key.BusinessDate,
                    grouped.Key.WarehouseNo,
                    branchName ?? string.Empty,
                    grouped.Key.CashRegisterNo,
                    grouped.Sum(item => item.SaleRowCount),
                    grouped.Count(),
                    saleTotal,
                    paymentTotal,
                    Round(paymentTotals.CashTotal),
                    Round(paymentTotals.CreditCardTotal),
                    Round(paymentTotals.GiftCardTotal),
                    Round(paymentTotals.OtherTotal),
                    Round(paymentTotals.UnknownTotal),
                    Round(saleTotal - paymentTotal),
                    grouped
                        .Select(item => item.CashierCode)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count(),
                    grouped.Max(item => item.LastReceivedAt));
            })
            .OrderBy(item => item.BusinessDate)
            .ThenBy(item => item.WarehouseNo)
            .ThenBy(item => item.CashRegisterNo, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<YeniKasaFisMutabakatItemDto>> GetFisMutabakatiAsync(
        YeniKasaAnalizRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedRequest = NormalizeRequest(request);
        var data = await LoadAnalysisDataAsync(normalizedRequest, cancellationToken);
        var items = CreateReconciliationItems(data);

        if (normalizedRequest.OnlyProblematic)
        {
            items = items
                .Where(item => item.Issues.Count > 0)
                .ToArray();
        }

        return items
            .OrderByDescending(item => item.Issues.Count)
            .ThenByDescending(item => Math.Abs(item.SalePaymentDifference))
            .ThenBy(item => item.BusinessDate)
            .ThenBy(item => item.WarehouseNo)
            .ThenBy(item => item.CashRegisterNo, StringComparer.OrdinalIgnoreCase)
            .Take(normalizedRequest.Take)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<YeniKasaAnomalyItemDto>> GetAnomalilerAsync(
        YeniKasaAnalizRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedRequest = NormalizeRequest(request);
        var data = await LoadAnalysisDataAsync(normalizedRequest, cancellationToken);
        var anomalies = new List<YeniKasaAnomalyItemDto>();

        foreach (var item in CreateReconciliationItems(data).Where(item => item.Issues.Count > 0))
        {
            foreach (var issue in item.Issues)
            {
                anomalies.Add(new YeniKasaAnomalyItemDto(
                    issue,
                    ResolveSeverity(issue),
                    item.BusinessDate,
                    item.WarehouseNo,
                    item.WarehouseName,
                    item.CashRegisterNo,
                    item.CashierCode,
                    item.Uuid,
                    item.ReceiptNumber,
                    item.SaleTotal,
                    item.PaymentTotal,
                    item.SalePaymentDifference,
                    ResolveIssueDescription(issue, item)));
            }
        }

        anomalies.AddRange(CreateDuplicateUuidAnomalies(data));
        anomalies.AddRange(CreateDuplicateReceiptNumberAnomalies(data));
        anomalies.AddRange(CreateUnknownPaymentMethodAnomalies(data));

        return anomalies
            .OrderBy(item => SeverityRank(item.Severity))
            .ThenBy(item => item.BusinessDate)
            .ThenBy(item => item.WarehouseNo)
            .Take(normalizedRequest.Take)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<YeniKasaPaymentMethodItemDto>> GetOdemeTipleriAsync(
        YeniKasaAnalizRequest request,
        CancellationToken cancellationToken)
    {
        var data = await LoadAnalysisDataAsync(NormalizeRequest(request), cancellationToken);

        return data.Payments
            .GroupBy(payment => payment.PaymentMethodCode, StringComparer.OrdinalIgnoreCase)
            .Select(grouped =>
            {
                var paymentMethodCode = grouped.Key;
                var paymentMethod = ResolvePaymentMethod(data.PaymentMethodsByCode, paymentMethodCode);
                var category = ClassifyPaymentMethod(paymentMethod, paymentMethodCode);

                return new YeniKasaPaymentMethodItemDto(
                    paymentMethodCode,
                    FirstNonEmpty(paymentMethod?.Name, paymentMethodCode),
                    category.ToString(),
                    paymentMethod?.Id,
                    paymentMethod?.PavoMediator,
                    paymentMethod?.PavoType,
                    grouped.Count(),
                    Round(grouped.Sum(item => item.Amount)),
                    paymentMethod is not null);
            })
            .OrderBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.PaymentMethodName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<YeniKasaSaglikOzetItemDto>> GetSaglikOzetiAsync(
        YeniKasaAnalizRequest request,
        CancellationToken cancellationToken)
    {
        var data = await LoadAnalysisDataAsync(NormalizeRequest(request), cancellationToken);

        return CreateReconciliationItems(data)
            .GroupBy(item => new
            {
                item.BusinessDate,
                item.WarehouseNo,
                item.WarehouseName,
                item.CashRegisterNo
            })
            .Select(grouped =>
            {
                var rows = grouped.ToArray();
                var problemReceiptCount = rows.Count(item => item.Issues.Count > 0);
                var criticalProblemCount = rows.Count(item => item.Issues.Any(issue => ResolveSeverity(issue) == "High"));
                var saleTotal = Round(rows.Sum(item => item.SaleTotal));
                var paymentTotal = Round(rows.Sum(item => item.PaymentTotal));
                var differenceTotal = Round(rows.Sum(item => Math.Abs(item.SalePaymentDifference)));
                var topIssues = rows
                    .SelectMany(item => item.Issues)
                    .GroupBy(issue => issue, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(issueGroup => issueGroup.Count())
                    .ThenBy(issueGroup => issueGroup.Key, StringComparer.OrdinalIgnoreCase)
                    .Take(5)
                    .Select(issueGroup => issueGroup.Key)
                    .ToArray();

                return new YeniKasaSaglikOzetItemDto(
                    grouped.Key.BusinessDate,
                    grouped.Key.WarehouseNo,
                    grouped.Key.WarehouseName,
                    grouped.Key.CashRegisterNo,
                    rows.Length,
                    problemReceiptCount,
                    criticalProblemCount,
                    saleTotal,
                    paymentTotal,
                    differenceTotal,
                    rows.Max(item => item.ReceivedAt),
                    ResolveRiskLevel(problemReceiptCount, criticalProblemCount, differenceTotal),
                    topIssues);
            })
            .OrderByDescending(item => RiskRank(item.RiskLevel))
            .ThenBy(item => item.BusinessDate)
            .ThenBy(item => item.WarehouseNo)
            .ThenBy(item => item.CashRegisterNo, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<YeniKasaFisDetayDto?> GetFisDetayiAsync(
        YeniKasaFisDetayRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedRequest = NormalizeFisDetayRequest(request);
        var sales = await CreateFisDetaySalesQuery(normalizedRequest)
            .Select(sale => new SaleRow(
                sale.Id,
                sale.Uuid ?? string.Empty,
                sale.ReceiptNumber ?? string.Empty,
                sale.Subeno ?? string.Empty,
                sale.Kasano ?? string.Empty,
                sale.InitiatedBy ?? string.Empty,
                sale.ReceivedAt!.Value,
                sale.TotalPrice ?? 0d,
                sale.RemainingAmount ?? 0d,
                sale.MarketId ?? string.Empty,
                sale.Status ?? string.Empty))
            .ToListAsync(cancellationToken);

        if (sales.Count == 0)
        {
            return null;
        }

        var receipts = CreateReceipts(sales);
        var saleUuids = receipts
            .Select(receipt => receipt.Uuid)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var saleItems = await LoadSaleItemsAsync(saleUuids, cancellationToken);
        var rawPayments = await LoadPaymentsAsync(saleUuids, cancellationToken);
        var payments = ResolvePaymentRows(rawPayments, receipts);
        var paymentMethodCodes = rawPayments
            .Select(item => item.PaymentMethodCode)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var paymentMethodsByCode = await LoadPaymentMethodsAsync(paymentMethodCodes, cancellationToken);
        var warehouseNos = receipts
            .Select(item => item.WarehouseNo)
            .Where(value => value > 0)
            .Distinct()
            .ToArray();
        var branchNames = await LoadBranchNamesAsync(warehouseNos, cancellationToken);
        var employeeNames = await LoadEmployeeNamesAsync(
            receipts.Select(item => item.CashierCode),
            cancellationToken);
        var itemTotalsBySaleUuid = saleItems
            .GroupBy(item => item.SaleUuid, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                grouped => grouped.Key,
                grouped => new ItemTotals(
                    grouped.Count(),
                    decimal.ToDouble(grouped.Sum(item => item.Quantity)),
                    Round(grouped.Sum(item => item.TotalPrice))),
                StringComparer.OrdinalIgnoreCase);
        var paymentTotalsBySaleUuid = payments
            .GroupBy(item => item.SaleUuid, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                grouped => grouped.Key,
                grouped => ToPaymentTotals(grouped, paymentMethodsByCode),
                StringComparer.OrdinalIgnoreCase);
        var data = new AnalysisData(
            sales,
            receipts,
            saleItems,
            payments,
            itemTotalsBySaleUuid,
            paymentTotalsBySaleUuid,
            paymentMethodsByCode,
            branchNames,
            employeeNames);
        var reconciliationItems = CreateReconciliationItems(data)
            .OrderBy(item => item.BusinessDate)
            .ThenBy(item => item.WarehouseNo)
            .ThenBy(item => item.CashRegisterNo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.CashierCode, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var firstReceipt = reconciliationItems.FirstOrDefault();
        var saleTotal = Round(reconciliationItems.Sum(item => item.SaleTotal));
        var productLineTotal = Round(saleItems.Sum(item => item.TotalPrice));
        var paymentTotal = Round(payments.Sum(item => item.Amount));
        var issues = reconciliationItems
            .SelectMany(item => item.Issues)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var includedPaymentIds = payments
            .Select(item => item.Id)
            .ToHashSet();

        return new YeniKasaFisDetayDto(
            FirstNonEmpty(new[] { firstReceipt?.Uuid }.Concat(sales.Select(item => item.Uuid)).ToArray()),
            FirstNonEmpty(new[] { firstReceipt?.ReceiptNumber }.Concat(sales.Select(item => item.ReceiptNumber)).ToArray()),
            firstReceipt?.BusinessDate,
            firstReceipt?.WarehouseNo ?? 0,
            firstReceipt?.WarehouseName ?? string.Empty,
            firstReceipt?.CashRegisterNo ?? string.Empty,
            firstReceipt?.CashierCode ?? string.Empty,
            firstReceipt?.CashierName ?? string.Empty,
            saleTotal,
            productLineTotal,
            paymentTotal,
            Round(saleTotal - paymentTotal),
            Round(saleTotal - productLineTotal),
            issues.Length == 0 ? "OK" : "Problem",
            issues,
            reconciliationItems,
            sales
                .OrderBy(item => item.ReceivedAt)
                .ThenBy(item => item.Id)
                .Select(item => new YeniKasaFisSatisSatiriDto(
                    item.Id,
                    item.Uuid,
                    item.ReceiptNumber,
                    item.ReceivedAt,
                    item.WarehouseNo,
                    item.WarehouseCode,
                    item.CashRegisterNo,
                    item.CashierCode,
                    item.SaleTotal,
                    item.RemainingAmount,
                    item.MarketId,
                    item.Status))
                .ToArray(),
            saleItems
                .OrderBy(item => item.Id)
                .Select(item => new YeniKasaFisUrunSatiriDto(
                    item.Id,
                    item.SaleUuid,
                    item.Quantity,
                    item.TotalPrice))
                .ToArray(),
            rawPayments
                .OrderBy(item => item.Id)
                .Select(item =>
                {
                    var paymentMethod = ResolvePaymentMethod(paymentMethodsByCode, item.PaymentMethodCode);
                    var category = ClassifyPaymentMethod(paymentMethod, item.PaymentMethodCode);

                    return new YeniKasaFisOdemeSatiriDto(
                        item.Id,
                        item.SaleUuid,
                        item.PaymentMethodCode,
                        FirstNonEmpty(paymentMethod?.Name, item.PaymentMethodCode),
                        category.ToString(),
                        paymentMethod?.Id,
                        paymentMethod?.PavoMediator,
                        paymentMethod?.PavoType,
                        item.Amount,
                        includedPaymentIds.Contains(item.Id));
                })
                .ToArray());
    }

    private async Task<AnalysisData> LoadAnalysisDataAsync(
        YeniKasaAnalizRequest request,
        CancellationToken cancellationToken)
    {
        var sales = await CreateSalesQuery(request)
            .Select(sale => new SaleRow(
                sale.Id,
                sale.Uuid ?? string.Empty,
                sale.ReceiptNumber ?? string.Empty,
                sale.Subeno ?? string.Empty,
                sale.Kasano ?? string.Empty,
                sale.InitiatedBy ?? string.Empty,
                sale.ReceivedAt!.Value,
                sale.TotalPrice ?? 0d,
                sale.RemainingAmount ?? 0d,
                sale.MarketId ?? string.Empty,
                sale.Status ?? string.Empty))
            .ToListAsync(cancellationToken);
        var receipts = CreateReceipts(sales);
        var saleUuids = receipts
            .Select(receipt => receipt.Uuid)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var saleItems = await LoadSaleItemsAsync(saleUuids, cancellationToken);
        var payments = ResolvePaymentRows(await LoadPaymentsAsync(saleUuids, cancellationToken), receipts);
        var paymentMethodCodes = payments
            .Select(item => item.PaymentMethodCode)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var paymentMethodsByCode = await LoadPaymentMethodsAsync(paymentMethodCodes, cancellationToken);
        var warehouseNos = receipts
            .Select(item => item.WarehouseNo)
            .Where(value => value > 0)
            .Distinct()
            .ToArray();
        var branchNames = await LoadBranchNamesAsync(warehouseNos, cancellationToken);
        var employeeNames = await LoadEmployeeNamesAsync(
            receipts.Select(item => item.CashierCode),
            cancellationToken);
        var itemTotalsBySaleUuid = saleItems
            .GroupBy(item => item.SaleUuid, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                grouped => grouped.Key,
                grouped => new ItemTotals(
                    grouped.Count(),
                    decimal.ToDouble(grouped.Sum(item => item.Quantity)),
                    Round(grouped.Sum(item => item.TotalPrice))),
                StringComparer.OrdinalIgnoreCase);
        var paymentTotalsBySaleUuid = payments
            .GroupBy(item => item.SaleUuid, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                grouped => grouped.Key,
                grouped => ToPaymentTotals(grouped, paymentMethodsByCode),
                StringComparer.OrdinalIgnoreCase);

        return new AnalysisData(
            sales,
            receipts,
            saleItems,
            payments,
            itemTotalsBySaleUuid,
            paymentTotalsBySaleUuid,
            paymentMethodsByCode,
            branchNames,
            employeeNames);
    }

    private IQueryable<Persistence.Shopigo.Models.ShopigoReceivedSale> CreateSalesQuery(
        YeniKasaAnalizRequest request)
    {
        var query = shopigoCiroDbContext.ReceivedSales.AsNoTracking()
            .Where(sale =>
                sale.DeletedAt == null &&
                sale.Status == CompletedReceivedSaleStatus &&
                sale.ReceivedAt.HasValue &&
                sale.ReceivedAt.Value >= request.StartDate &&
                sale.ReceivedAt.Value < request.EndDate.AddDays(1));

        if (request.WarehouseNo.HasValue)
        {
            var warehouseCode = request.WarehouseNo.Value.ToString(CultureInfo.InvariantCulture);
            query = query.Where(sale => sale.Subeno == warehouseCode);
        }

        if (!string.IsNullOrWhiteSpace(request.CashRegisterNo))
        {
            var cashRegisterNo = NormalizeCode(request.CashRegisterNo);
            query = query.Where(sale => (sale.Kasano ?? string.Empty) == cashRegisterNo);
        }

        if (!string.IsNullOrWhiteSpace(request.CashierCode))
        {
            var cashierCode = NormalizeCode(request.CashierCode);
            query = query.Where(sale => (sale.InitiatedBy ?? string.Empty) == cashierCode);
        }

        return query;
    }

    private IQueryable<Persistence.Shopigo.Models.ShopigoReceivedSale> CreateFisDetaySalesQuery(
        YeniKasaFisDetayRequest request)
    {
        var query = shopigoCiroDbContext.ReceivedSales.AsNoTracking()
            .Where(sale =>
                sale.DeletedAt == null &&
                sale.Status == CompletedReceivedSaleStatus &&
                sale.ReceivedAt.HasValue);

        if (!string.IsNullOrWhiteSpace(request.Uuid))
        {
            var uuid = NormalizeCode(request.Uuid);
            return query.Where(sale => (sale.Uuid ?? string.Empty) == uuid);
        }

        var businessDate = request.BusinessDate!.Value.Date;
        var warehouseCode = request.WarehouseNo!.Value.ToString(CultureInfo.InvariantCulture);
        var cashRegisterNo = NormalizeCode(request.CashRegisterNo);
        var receiptNumber = NormalizeCode(request.ReceiptNumber);

        return query.Where(sale =>
            sale.ReceivedAt!.Value >= businessDate &&
            sale.ReceivedAt.Value < businessDate.AddDays(1) &&
            (sale.Subeno ?? string.Empty) == warehouseCode &&
            (sale.Kasano ?? string.Empty) == cashRegisterNo &&
            (sale.ReceiptNumber ?? string.Empty) == receiptNumber);
    }

    private async Task<IReadOnlyCollection<SaleItemRow>> LoadSaleItemsAsync(
        IReadOnlyCollection<string> saleUuids,
        CancellationToken cancellationToken)
    {
        var rows = new List<SaleItemRow>();

        foreach (var chunk in saleUuids.Chunk(1000))
        {
            var currentChunk = chunk.ToArray();
            rows.AddRange(await shopigoCiroDbContext.SaleItems.AsNoTracking()
                .Where(item =>
                    item.DeletedAt == null &&
                    item.Refunded == 0 &&
                    item.SaleUuid != null &&
                    currentChunk.Contains(item.SaleUuid))
                .Select(item => new SaleItemRow(
                    item.Id,
                    item.SaleUuid ?? string.Empty,
                    item.Quantity ?? 0m,
                    item.TotalPrice ?? 0d))
                .ToListAsync(cancellationToken));
        }

        return rows;
    }

    private async Task<IReadOnlyCollection<PaymentRow>> LoadPaymentsAsync(
        IReadOnlyCollection<string> saleUuids,
        CancellationToken cancellationToken)
    {
        var rows = new List<PaymentRow>();

        foreach (var chunk in saleUuids.Chunk(1000))
        {
            var currentChunk = chunk.ToArray();
            rows.AddRange(await shopigoCiroDbContext.Payments.AsNoTracking()
                .Where(payment =>
                    payment.DeletedAt == null &&
                    payment.Refunded == 0 &&
                    payment.SaleUuid != null &&
                    currentChunk.Contains(payment.SaleUuid))
                .Select(payment => new PaymentRow(
                    payment.Id,
                    payment.SaleUuid ?? string.Empty,
                    payment.PaymentMethod ?? string.Empty,
                    payment.Amount ?? 0d))
                .ToListAsync(cancellationToken));
        }

        return rows;
    }

    private async Task<IReadOnlyDictionary<string, PaymentMethodInfo>> LoadPaymentMethodsAsync(
        IReadOnlyCollection<string> paymentMethodCodes,
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
                (paymentMethodNumbers.Contains(method.Id) || paymentMethodNumbers.Contains(method.PavoMediator)))
            .Select(method => new PaymentMethodInfo(
                method.Id,
                method.Name ?? string.Empty,
                method.PavoType,
                method.PavoMediator))
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

            if (row is not null)
            {
                lookup[paymentMethodNumber.ToString(CultureInfo.InvariantCulture)] = row;
            }
        }

        return lookup;
    }

    private async Task<IReadOnlyDictionary<int, string>> LoadBranchNamesAsync(
        IReadOnlyCollection<int> warehouseNos,
        CancellationToken cancellationToken)
    {
        if (warehouseNos.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        var shopigoRows = await shopigoCiroDbContext.Branches.AsNoTracking()
            .Where(branch => branch.DeletedAt == null && warehouseNos.Contains(branch.DepoId))
            .Select(branch => new
            {
                branch.DepoId,
                branch.Name
            })
            .ToListAsync(cancellationToken);
        var lookup = shopigoRows
            .GroupBy(item => item.DepoId)
            .ToDictionary(
                grouped => grouped.Key,
                grouped => FirstNonEmpty(grouped.Select(item => item.Name).ToArray()));
        var missingWarehouseNos = warehouseNos
            .Where(item => !lookup.ContainsKey(item))
            .ToArray();

        if (missingWarehouseNos.Length == 0)
        {
            return lookup;
        }

        var mikroRows = await mikroDbContext.DEPOLARs.AsNoTracking()
            .Where(warehouse => warehouse.dep_no.HasValue && missingWarehouseNos.Contains(warehouse.dep_no.Value))
            .Select(warehouse => new
            {
                WarehouseNo = warehouse.dep_no!.Value,
                warehouse.dep_adi
            })
            .ToListAsync(cancellationToken);

        foreach (var row in mikroRows)
        {
            lookup[row.WarehouseNo] = row.dep_adi ?? string.Empty;
        }

        return lookup;
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadEmployeeNamesAsync(
        IEnumerable<string> cashierCodes,
        CancellationToken cancellationToken)
    {
        var normalizedCodes = cashierCodes
            .Select(NormalizeCode)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedCodes.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var rows = await shopigoCiroDbContext.Employees.AsNoTracking()
            .Where(employee =>
                employee.DeletedAt == null &&
                employee.Code != null &&
                normalizedCodes.Contains(employee.Code))
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
                grouped => string.Join(
                    " ",
                    new[]
                    {
                        FirstNonEmpty(grouped.Select(item => item.Name).ToArray()),
                        FirstNonEmpty(grouped.Select(item => item.Surname).ToArray())
                    }.Where(value => !string.IsNullOrWhiteSpace(value))),
                StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyCollection<ReceiptRow> CreateReceipts(IReadOnlyCollection<SaleRow> sales) =>
        sales
            .GroupBy(sale => new
            {
                sale.BusinessDate,
                sale.WarehouseCode,
                sale.CashRegisterNo,
                sale.CashierCode,
                SaleKey = ResolveSaleKey(sale)
            })
            .Select(grouped => new ReceiptRow(
                grouped.Key.BusinessDate,
                ParseInt(grouped.Key.WarehouseCode),
                grouped.Key.WarehouseCode,
                NormalizeCode(grouped.Key.CashRegisterNo),
                NormalizeCode(grouped.Key.CashierCode),
                grouped.Key.SaleKey,
                FirstNonEmpty(grouped.Select(item => item.Uuid).ToArray()),
                FirstNonEmpty(grouped.Select(item => item.ReceiptNumber).ToArray()),
                grouped.Count(),
                Round(grouped.Max(item => item.SaleTotal)),
                grouped.Min(item => item.ReceivedAt),
                grouped.Max(item => item.ReceivedAt)))
            .ToArray();

    private static IReadOnlyCollection<PaymentRow> ResolvePaymentRows(
        IReadOnlyCollection<PaymentRow> rows,
        IReadOnlyCollection<ReceiptRow> receipts)
    {
        var saleTotalsByUuid = receipts
            .Where(item => !string.IsNullOrWhiteSpace(item.Uuid))
            .GroupBy(item => item.Uuid, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                grouped => grouped.Key,
                grouped => grouped.Max(item => item.SaleTotal),
                StringComparer.OrdinalIgnoreCase);

        return rows
            .GroupBy(row => row.SaleUuid, StringComparer.OrdinalIgnoreCase)
            .SelectMany(grouped =>
            {
                saleTotalsByUuid.TryGetValue(grouped.Key, out var saleTotal);
                return ResolveSalePaymentRows(grouped.ToArray(), saleTotal);
            })
            .ToArray();
    }

    private static IReadOnlyCollection<PaymentRow> ResolveSalePaymentRows(
        IReadOnlyList<PaymentRow> saleRows,
        double saleTotal)
    {
        if (saleRows.Count == 0)
        {
            return Array.Empty<PaymentRow>();
        }

        var orderedRows = saleRows
            .OrderBy(item => item.Id)
            .ToArray();

        if (AmountsMatch(orderedRows.Sum(item => item.Amount), saleTotal))
        {
            return orderedRows;
        }

        return orderedRows
            .GroupBy(row => new
            {
                row.PaymentMethodCode,
                Amount = ToCents(row.Amount)
            })
            .Select(grouped => grouped.OrderBy(row => row.Id).First())
            .OrderBy(row => row.Id)
            .ToArray();
    }

    private static IReadOnlyCollection<YeniKasaFisMutabakatItemDto> CreateReconciliationItems(AnalysisData data) =>
        data.Receipts
            .Select(receipt =>
            {
                data.BranchNames.TryGetValue(receipt.WarehouseNo, out var branchName);
                data.EmployeeNames.TryGetValue(receipt.CashierCode, out var cashierName);
                data.ItemTotalsBySaleUuid.TryGetValue(receipt.Uuid, out var itemTotals);
                data.PaymentTotalsBySaleUuid.TryGetValue(receipt.Uuid, out var paymentTotals);
                itemTotals ??= ItemTotals.Empty;
                paymentTotals ??= PaymentTotals.Empty;
                var salePaymentDifference = Round(receipt.SaleTotal - paymentTotals.TotalAmount);
                var saleLineDifference = Round(receipt.SaleTotal - itemTotals.TotalAmount);
                var issues = ResolveReceiptIssues(
                    receipt,
                    itemTotals,
                    paymentTotals,
                    salePaymentDifference,
                    saleLineDifference);

                return new YeniKasaFisMutabakatItemDto(
                    receipt.BusinessDate,
                    receipt.WarehouseNo,
                    branchName ?? string.Empty,
                    receipt.CashRegisterNo,
                    receipt.CashierCode,
                    cashierName ?? string.Empty,
                    receipt.Uuid,
                    receipt.ReceiptNumber,
                    receipt.SaleRowCount,
                    itemTotals.LineCount,
                    paymentTotals.LineCount,
                    receipt.SaleTotal,
                    Round(itemTotals.TotalAmount),
                    Round(paymentTotals.TotalAmount),
                    salePaymentDifference,
                    saleLineDifference,
                    issues.Count == 0 ? "OK" : "Problem",
                    issues,
                    receipt.LastReceivedAt);
            })
            .ToArray();

    private static IReadOnlyCollection<string> ResolveReceiptIssues(
        ReceiptRow receipt,
        ItemTotals itemTotals,
        PaymentTotals paymentTotals,
        double salePaymentDifference,
        double saleLineDifference)
    {
        var issues = new List<string>();

        if (string.IsNullOrWhiteSpace(receipt.Uuid))
        {
            issues.Add("MissingUuid");
        }

        if (receipt.WarehouseNo <= 0)
        {
            issues.Add("MissingWarehouseMapping");
        }

        if (string.IsNullOrWhiteSpace(receipt.CashRegisterNo))
        {
            issues.Add("EmptyCashRegisterNo");
        }

        if (string.IsNullOrWhiteSpace(receipt.CashierCode))
        {
            issues.Add("MissingCashier");
        }

        if (receipt.SaleRowCount > 1)
        {
            issues.Add("DuplicateSaleRow");
        }

        if (itemTotals.LineCount == 0 && receipt.SaleTotal != 0d)
        {
            issues.Add("MissingSaleItems");
        }

        if (paymentTotals.LineCount == 0 && receipt.SaleTotal != 0d)
        {
            issues.Add("MissingPayment");
        }

        if (paymentTotals.LineCount > 0 && !AmountsMatch(receipt.SaleTotal, paymentTotals.TotalAmount))
        {
            issues.Add(salePaymentDifference > 0 ? "MissingPaymentAmount" : "OverPaymentAmount");
        }

        if (itemTotals.LineCount > 0 && !AmountsMatch(receipt.SaleTotal, itemTotals.TotalAmount))
        {
            issues.Add("SaleLineTotalMismatch");
        }

        return issues;
    }

    private static IEnumerable<YeniKasaAnomalyItemDto> CreateDuplicateUuidAnomalies(AnalysisData data) =>
        data.Sales
            .Where(item => !string.IsNullOrWhiteSpace(item.Uuid))
            .GroupBy(item => item.Uuid, StringComparer.OrdinalIgnoreCase)
            .Where(grouped => grouped.Count() > 1)
            .Select(grouped =>
            {
                var first = grouped.OrderBy(item => item.ReceivedAt).First();
                data.BranchNames.TryGetValue(first.WarehouseNo, out var branchName);

                return new YeniKasaAnomalyItemDto(
                    "DuplicateUuid",
                    "High",
                    first.BusinessDate,
                    first.WarehouseNo,
                    branchName ?? string.Empty,
                    first.CashRegisterNo,
                    first.CashierCode,
                    grouped.Key,
                    first.ReceiptNumber,
                    Round(grouped.Sum(item => item.SaleTotal)),
                    0d,
                    0d,
                    $"Ayni uuid {grouped.Count()} satis satirinda bulundu.");
            });

    private static IEnumerable<YeniKasaAnomalyItemDto> CreateDuplicateReceiptNumberAnomalies(AnalysisData data) =>
        data.Sales
            .Where(item => !string.IsNullOrWhiteSpace(item.ReceiptNumber))
            .GroupBy(item => new
            {
                item.BusinessDate,
                item.WarehouseNo,
                item.CashRegisterNo,
                item.ReceiptNumber
            })
            .Where(grouped => grouped.Count() > 1)
            .Select(grouped =>
            {
                var first = grouped.OrderBy(item => item.ReceivedAt).First();
                data.BranchNames.TryGetValue(first.WarehouseNo, out var branchName);

                return new YeniKasaAnomalyItemDto(
                    "DuplicateReceiptNumber",
                    "Medium",
                    first.BusinessDate,
                    first.WarehouseNo,
                    branchName ?? string.Empty,
                    first.CashRegisterNo,
                    first.CashierCode,
                    first.Uuid,
                    grouped.Key.ReceiptNumber,
                    Round(grouped.Sum(item => item.SaleTotal)),
                    0d,
                    0d,
                    $"Ayni fis no {grouped.Count()} satis satirinda bulundu.");
            });

    private static IEnumerable<YeniKasaAnomalyItemDto> CreateUnknownPaymentMethodAnomalies(AnalysisData data) =>
        data.Payments
            .Where(payment => ResolvePaymentMethod(data.PaymentMethodsByCode, payment.PaymentMethodCode) is null)
            .GroupBy(payment => payment.PaymentMethodCode, StringComparer.OrdinalIgnoreCase)
            .Select(grouped => new YeniKasaAnomalyItemDto(
                "UnknownPaymentMethod",
                "Medium",
                null,
                0,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                0d,
                Round(grouped.Sum(item => item.Amount)),
                0d,
                $"Odeme tipi eslesmedi: {grouped.Key}. Satir: {grouped.Count()}."));

    private static PaymentTotals SumPayments(
        IReadOnlyDictionary<string, PaymentTotals> paymentTotalsBySaleUuid,
        IReadOnlyCollection<string> saleUuids)
    {
        if (saleUuids.Count == 0)
        {
            return PaymentTotals.Empty;
        }

        return new PaymentTotals(
            saleUuids.Sum(uuid => paymentTotalsBySaleUuid.TryGetValue(uuid, out var totals) ? totals.LineCount : 0),
            Round(saleUuids.Sum(uuid => paymentTotalsBySaleUuid.TryGetValue(uuid, out var totals) ? totals.TotalAmount : 0d)),
            Round(saleUuids.Sum(uuid => paymentTotalsBySaleUuid.TryGetValue(uuid, out var totals) ? totals.CashTotal : 0d)),
            Round(saleUuids.Sum(uuid => paymentTotalsBySaleUuid.TryGetValue(uuid, out var totals) ? totals.CreditCardTotal : 0d)),
            Round(saleUuids.Sum(uuid => paymentTotalsBySaleUuid.TryGetValue(uuid, out var totals) ? totals.GiftCardTotal : 0d)),
            Round(saleUuids.Sum(uuid => paymentTotalsBySaleUuid.TryGetValue(uuid, out var totals) ? totals.OtherTotal : 0d)),
            Round(saleUuids.Sum(uuid => paymentTotalsBySaleUuid.TryGetValue(uuid, out var totals) ? totals.UnknownTotal : 0d)));
    }

    private static ItemTotals SumItems(
        IReadOnlyDictionary<string, ItemTotals> itemTotalsBySaleUuid,
        IReadOnlyCollection<string> saleUuids)
    {
        if (saleUuids.Count == 0)
        {
            return ItemTotals.Empty;
        }

        return new ItemTotals(
            saleUuids.Sum(uuid => itemTotalsBySaleUuid.TryGetValue(uuid, out var totals) ? totals.LineCount : 0),
            saleUuids.Sum(uuid => itemTotalsBySaleUuid.TryGetValue(uuid, out var totals) ? totals.Quantity : 0d),
            Round(saleUuids.Sum(uuid => itemTotalsBySaleUuid.TryGetValue(uuid, out var totals) ? totals.TotalAmount : 0d)));
    }

    private static PaymentTotals ToPaymentTotals(
        IEnumerable<PaymentRow> payments,
        IReadOnlyDictionary<string, PaymentMethodInfo> paymentMethodsByCode)
    {
        var rows = payments.ToArray();
        var categorizedRows = rows
            .Select(item =>
            {
                var paymentMethod = ResolvePaymentMethod(paymentMethodsByCode, item.PaymentMethodCode);
                return new
                {
                    Category = ClassifyPaymentMethod(paymentMethod, item.PaymentMethodCode),
                    item.Amount
                };
            })
            .ToArray();

        return new PaymentTotals(
            rows.Length,
            Round(rows.Sum(item => item.Amount)),
            Round(categorizedRows.Where(item => item.Category == PaymentCategory.Cash).Sum(item => item.Amount)),
            Round(categorizedRows.Where(item => item.Category == PaymentCategory.CreditCard).Sum(item => item.Amount)),
            Round(categorizedRows.Where(item => item.Category == PaymentCategory.GiftCard).Sum(item => item.Amount)),
            Round(categorizedRows.Where(item => item.Category == PaymentCategory.Other).Sum(item => item.Amount)),
            Round(categorizedRows.Where(item => item.Category == PaymentCategory.Unknown).Sum(item => item.Amount)));
    }

    private static PaymentMethodInfo? ResolvePaymentMethod(
        IReadOnlyDictionary<string, PaymentMethodInfo> paymentMethodsByCode,
        string paymentMethodCode)
    {
        var normalizedCode = ParseInt(paymentMethodCode).ToString(CultureInfo.InvariantCulture);
        paymentMethodsByCode.TryGetValue(normalizedCode, out var paymentMethod);
        return paymentMethod;
    }

    private static PaymentCategory ClassifyPaymentMethod(
        PaymentMethodInfo? paymentMethod,
        string paymentMethodCode)
    {
        if (paymentMethod is null)
        {
            return string.IsNullOrWhiteSpace(paymentMethodCode)
                ? PaymentCategory.Unknown
                : ClassifyPaymentMethodByName(paymentMethodCode, PaymentCategory.Unknown);
        }

        return paymentMethod.PavoType switch
        {
            1 => PaymentCategory.Cash,
            2 => PaymentCategory.CreditCard,
            3 => PaymentCategory.GiftCard,
            14 => PaymentCategory.Other,
            _ => ClassifyPaymentMethodByName(paymentMethod.Name, PaymentCategory.Other)
        };
    }

    private static PaymentCategory ClassifyPaymentMethodByName(
        string paymentMethodName,
        PaymentCategory fallback)
    {
        var normalized = NormalizeToken(paymentMethodName);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return fallback;
        }

        if (normalized.Contains("nakit", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("cash", StringComparison.OrdinalIgnoreCase))
        {
            return PaymentCategory.Cash;
        }

        if (normalized.Contains("multinet", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("ticket", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("metropol", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("setcard", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("pluxee", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("sodexo", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("gift", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("yemek", StringComparison.OrdinalIgnoreCase))
        {
            return PaymentCategory.GiftCard;
        }

        if (normalized.Contains("kredi", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("credit", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("kart", StringComparison.OrdinalIgnoreCase))
        {
            return PaymentCategory.CreditCard;
        }

        return fallback;
    }

    private static string ResolveIssueDescription(string issue, YeniKasaFisMutabakatItemDto item) =>
        issue switch
        {
            "MissingUuid" => "Fis uuid alani bos.",
            "MissingWarehouseMapping" => "Sube kodu Mikro/Shopigo depo eslesmesine donusturulemedi.",
            "EmptyCashRegisterNo" => "Kasa no bos geldi.",
            "MissingCashier" => "Kasiyer kodu bos geldi.",
            "DuplicateSaleRow" => "Ayni satis anahtari birden fazla satirda geldi.",
            "MissingSaleItems" => "Satis toplamı var fakat urun satiri bulunamadi.",
            "MissingPayment" => "Satis toplamı var fakat odeme satiri bulunamadi.",
            "MissingPaymentAmount" => "Fis toplami odeme toplamindan buyuk.",
            "OverPaymentAmount" => "Odeme toplami fis toplamindan buyuk.",
            "SaleLineTotalMismatch" => "Fis toplami ile urun satirlari toplami uyusmuyor.",
            _ => $"{item.ReceiptNumber} fisinde {issue} anomalisi bulundu."
        };

    private static string ResolveSeverity(string issue) =>
        issue switch
        {
            "MissingPayment" or "MissingPaymentAmount" or "OverPaymentAmount" or "SaleLineTotalMismatch" => "High",
            "DuplicateSaleRow" or "MissingSaleItems" => "Medium",
            _ => "Low"
        };

    private static int SeverityRank(string severity) =>
        severity switch
        {
            "High" => 0,
            "Medium" => 1,
            _ => 2
        };

    private static string ResolveRiskLevel(
        int problemReceiptCount,
        int criticalProblemCount,
        double differenceTotal)
    {
        if (criticalProblemCount > 0 || !AmountsMatch(differenceTotal, 0d))
        {
            return "Critical";
        }

        return problemReceiptCount > 0 ? "Warning" : "Healthy";
    }

    private static int RiskRank(string riskLevel) =>
        riskLevel switch
        {
            "Critical" => 3,
            "Warning" => 2,
            _ => 1
        };

    private static YeniKasaAnalizRequest NormalizeRequest(YeniKasaAnalizRequest request)
    {
        if (request.StartDate == default)
        {
            throw new ArgumentException("Start date is required.", nameof(request.StartDate));
        }

        if (request.EndDate == default)
        {
            throw new ArgumentException("End date is required.", nameof(request.EndDate));
        }

        if (request.WarehouseNo is <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        var startDate = request.StartDate.Date;
        var endDate = request.EndDate.Date;

        if (endDate < startDate)
        {
            throw new ArgumentException("End date can not be earlier than start date.", nameof(request.EndDate));
        }

        return request with
        {
            StartDate = startDate,
            EndDate = endDate,
            CashRegisterNo = NormalizeCode(request.CashRegisterNo),
            CashierCode = NormalizeCode(request.CashierCode),
            Take = Math.Clamp(request.Take <= 0 ? 500 : request.Take, 1, MaxTake)
        };
    }

    private static YeniKasaFisDetayRequest NormalizeFisDetayRequest(YeniKasaFisDetayRequest request)
    {
        var uuid = NormalizeCode(request.Uuid);

        if (!string.IsNullOrWhiteSpace(uuid))
        {
            return request with
            {
                Uuid = uuid,
                CashRegisterNo = NormalizeCode(request.CashRegisterNo),
                ReceiptNumber = NormalizeCode(request.ReceiptNumber)
            };
        }

        if (!request.BusinessDate.HasValue)
        {
            throw new ArgumentException("Business date is required when uuid is empty.", nameof(request.BusinessDate));
        }

        if (request.WarehouseNo is null or <= 0)
        {
            throw new ArgumentException("Warehouse no is required when uuid is empty.", nameof(request.WarehouseNo));
        }

        if (string.IsNullOrWhiteSpace(request.CashRegisterNo))
        {
            throw new ArgumentException("Cash register no is required when uuid is empty.", nameof(request.CashRegisterNo));
        }

        if (string.IsNullOrWhiteSpace(request.ReceiptNumber))
        {
            throw new ArgumentException("Receipt number is required when uuid is empty.", nameof(request.ReceiptNumber));
        }

        return request with
        {
            Uuid = string.Empty,
            BusinessDate = request.BusinessDate.Value.Date,
            CashRegisterNo = NormalizeCode(request.CashRegisterNo),
            ReceiptNumber = NormalizeCode(request.ReceiptNumber)
        };
    }

    private static string ResolveSaleKey(SaleRow sale)
    {
        if (!string.IsNullOrWhiteSpace(sale.Uuid))
        {
            return $"uuid:{NormalizeCode(sale.Uuid)}";
        }

        if (!string.IsNullOrWhiteSpace(sale.ReceiptNumber))
        {
            return $"receipt:{sale.BusinessDate:yyyyMMdd}:{NormalizeCode(sale.WarehouseCode)}:{NormalizeCode(sale.CashRegisterNo)}:{NormalizeCode(sale.ReceiptNumber)}";
        }

        return $"id:{sale.Id.ToString(CultureInfo.InvariantCulture)}";
    }

    private static bool AmountsMatch(double left, double right) =>
        Math.Abs(Round(left) - Round(right)) <= 0.01d;

    private static long ToCents(double value) =>
        Convert.ToInt64(Math.Round(value * 100d, MidpointRounding.AwayFromZero));

    private static int ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;

    private static string NormalizeCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static double Round(double value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string NormalizeToken(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value
                .Trim()
                .Where(character => !char.IsWhiteSpace(character) && character != '-' && character != '_')
                .ToArray())
            .ToLowerInvariant();

    private sealed record AnalysisData(
        IReadOnlyCollection<SaleRow> Sales,
        IReadOnlyCollection<ReceiptRow> Receipts,
        IReadOnlyCollection<SaleItemRow> SaleItems,
        IReadOnlyCollection<PaymentRow> Payments,
        IReadOnlyDictionary<string, ItemTotals> ItemTotalsBySaleUuid,
        IReadOnlyDictionary<string, PaymentTotals> PaymentTotalsBySaleUuid,
        IReadOnlyDictionary<string, PaymentMethodInfo> PaymentMethodsByCode,
        IReadOnlyDictionary<int, string> BranchNames,
        IReadOnlyDictionary<string, string> EmployeeNames);

    private sealed record SaleRow(
        int Id,
        string Uuid,
        string ReceiptNumber,
        string WarehouseCode,
        string CashRegisterNo,
        string CashierCode,
        DateTime ReceivedAt,
        double SaleTotal,
        double RemainingAmount,
        string MarketId,
        string Status)
    {
        public DateTime BusinessDate => ReceivedAt.Date;

        public int WarehouseNo => ParseInt(WarehouseCode);
    }

    private sealed record ReceiptRow(
        DateTime BusinessDate,
        int WarehouseNo,
        string WarehouseCode,
        string CashRegisterNo,
        string CashierCode,
        string SaleKey,
        string Uuid,
        string ReceiptNumber,
        int SaleRowCount,
        double SaleTotal,
        DateTime FirstReceivedAt,
        DateTime LastReceivedAt);

    private sealed record SaleItemRow(
        int Id,
        string SaleUuid,
        decimal Quantity,
        double TotalPrice);

    private sealed record PaymentRow(
        int Id,
        string SaleUuid,
        string PaymentMethodCode,
        double Amount);

    private sealed record ItemTotals(
        int LineCount,
        double Quantity,
        double TotalAmount)
    {
        public static ItemTotals Empty { get; } = new(0, 0d, 0d);
    }

    private sealed record PaymentTotals(
        int LineCount,
        double TotalAmount,
        double CashTotal,
        double CreditCardTotal,
        double GiftCardTotal,
        double OtherTotal,
        double UnknownTotal)
    {
        public static PaymentTotals Empty { get; } = new(0, 0d, 0d, 0d, 0d, 0d, 0d);
    }

    private sealed record PaymentMethodInfo(
        int Id,
        string Name,
        int PavoType,
        int PavoMediator);

    private enum PaymentCategory
    {
        Cash,
        CreditCard,
        GiftCard,
        Other,
        Unknown
    }
}

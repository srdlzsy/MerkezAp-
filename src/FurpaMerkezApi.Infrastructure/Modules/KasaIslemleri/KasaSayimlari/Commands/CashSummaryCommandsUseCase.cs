using System.Data;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari.Commands;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari.Files;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaSayimlari;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaSayimlari.Commands;

public sealed class CashSummaryCommandsUseCase(
    MikroWriteDbContext mikroWriteDbContext,
    IOptions<MikroWriteOptions> mikroWriteOptions,
    IGetCashSummaryZReportTotalUseCase getCashSummaryZReportTotalUseCase)
    : ICashSummaryCommandsUseCase
{
    private const short MikroUserNo = 39;
    private const int FirstDocumentOrderNo = 1;
    private const int CashTotalPaymentTypeId = 500;
    private const int DefaultCashTotalSlipNumber = 1;
    private const int StoreExpensePaymentTypeStart = 110;
    private const int StoreExpensePaymentTypeEnd = 113;
    private const string CashTotalTypeName = "Nakit";
    private const string CashTotalDescription = "Nakit Toplam";

    public async Task<CreateCashSummaryResponse> CreateAsync(
        CreateCashSummaryRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var options = mikroWriteOptions.Value;
        var now = DateTime.Now;
        var summaryDate = request.SummaryDate.Date;
        var documentSerie = BuildDocumentSerie(request.WarehouseNo, request.CashNo);
        var banknoteLines = request.BanknoteMovements
            .Where(ShouldWriteBanknoteLine)
            .ToArray();
        var cashAmount = ResolveCashTotalAmount(request.PaymentTypes, banknoteLines);
        var paymentLines = request.PaymentTypes
            .Where(line => !IsCashPaymentLine(line))
            .Where(ShouldWritePaymentLine)
            .ToArray();
        var storeExpenseLines = request.StoreExpenses
            .Where(ShouldWriteStoreExpenseLine)
            .ToArray();
        var cashSlipNumber = ResolveCashSlipNumber(request.PaymentTypes, banknoteLines);
        var documentTotal = CalculateCreateDocumentTotal(
            paymentLines,
            storeExpenseLines,
            cashAmount);
        var summaryLines = paymentLines
            .Select(line => CreateSummaryEntity(request, line, documentTotal, now))
            .Concat(storeExpenseLines.Select(line => CreateSummaryEntity(request, line, documentTotal, now)))
            .Prepend(CreateCashTotalSummaryEntity(request, cashAmount, documentTotal, cashSlipNumber, now))
            .ToArray();
        var giftCheckLines = request.GiftCheckMovements
            .Where(ShouldWriteGiftCheckLine)
            .ToArray();
        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            try
            {
                var documentOrderNo = await GetNextDocumentOrderNoAsync(documentSerie, cancellationToken);

                foreach (var summary in summaryLines)
                {
                    summary.DocumentSerie = documentSerie;
                    summary.DocumentOrderNo = documentOrderNo;
                }

                var customerMovementDocumentOrderNo = await GetNextCustomerMovementDocumentOrderNoAsync(cancellationToken);
                var customerMovementLines = await BuildCustomerMovementLinesAsync(summaryLines, cancellationToken);
                var banknoteEntities = banknoteLines
                    .Select(line => CreateBanknoteMovementEntity(request, line, documentSerie, documentOrderNo, now))
                    .ToArray();
                var giftCheckEntities = giftCheckLines
                    .Select(line => CreateGiftCheckMovementEntity(request, line, documentSerie, documentOrderNo, now))
                    .ToArray();
                var customerMovements = CashSummaryCustomerMovementFactory.CreateMovements(
                    summaryLines[0],
                    customerMovementLines,
                    request.ZTotalValue,
                    documentTotal,
                    customerMovementDocumentOrderNo,
                    now)
                    .ToArray();

                await mikroWriteDbContext.Summaries.AddRangeAsync(summaryLines, cancellationToken);
                await mikroWriteDbContext.BanknoteMovements.AddRangeAsync(banknoteEntities, cancellationToken);
                await mikroWriteDbContext.GiftCheckMovements.AddRangeAsync(giftCheckEntities, cancellationToken);
                await mikroWriteDbContext.CARI_HESAP_HAREKETLERIs.AddRangeAsync(customerMovements, cancellationToken);
                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new CreateCashSummaryResponse(
                    documentSerie,
                    documentOrderNo,
                    summaryDate,
                    request.WarehouseNo,
                    summaryLines.Length,
                    documentTotal,
                    options.ConnectionStringName);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<UpdateCashSummaryDetailsResponse> UpdateDetailsAsync(
        UpdateCashSummaryDetailsRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            try
            {
                var existingSummaries = await mikroWriteDbContext.Summaries
                    .Where(item =>
                        item.WarehouseNo == request.WarehouseNo &&
                        item.DocumentSerie == request.DocumentSerie &&
                        item.DocumentOrderNo == request.DocumentOrderNo)
                    .OrderBy(item => item.Id)
                    .ToListAsync(cancellationToken);

                if (existingSummaries.Count == 0)
                {
                    throw new KeyNotFoundException("Cash summary detail was not found.");
                }

                var header = existingSummaries[0];
                var now = DateTime.Now;
                var cashAmount = await ResolveCashTotalAmountAsync(
                    request,
                    existingSummaries,
                    cancellationToken);
                var cashSlipNumber = await ResolveCashSlipNumberAsync(
                    request,
                    existingSummaries,
                    cancellationToken);
                var detailLines = request.Details
                    .Where(line => !IsCashDetailLine(line))
                    .Where(ShouldWriteDetailLine)
                    .ToArray();
                var totalAmount = CalculateDocumentTotal(detailLines, cashAmount);

                mikroWriteDbContext.Summaries.RemoveRange(existingSummaries);

                var updatedSummaries = detailLines
                    .Select(detail => new SummaryEntity
                    {
                        Id = Guid.NewGuid(),
                        DocumentSerie = header.DocumentSerie,
                        DocumentOrderNo = header.DocumentOrderNo,
                        CreateUser = MikroUserNo,
                        CreateDate = now,
                        UpdateUser = MikroUserNo,
                        UpdateDate = now,
                        CashNo = header.CashNo,
                        ZReportNo = header.ZReportNo,
                        CashierNo = header.CashierNo,
                        ManagerNo = header.ManagerNo,
                        SummaryDate = header.SummaryDate,
                        Total = totalAmount,
                        PaymentTypeId = detail.PaymentTypeId,
                        Amount = detail.Amount,
                        WarehouseNo = header.WarehouseNo,
                        TypeName = NormalizeText(detail.TypeName),
                        AccountCode = NormalizeText(detail.AccountCode),
                        SlipNumber = detail.SlipNumber,
                        TerminalId = NormalizeText(detail.TerminalId),
                        Description = NormalizeText(detail.Description),
                        StoreExpenseType = ResolveStoreExpenseType(detail.PaymentTypeId)
                    })
                    .Prepend(CreateCashTotalSummaryEntity(header, cashAmount, totalAmount, cashSlipNumber, now))
                    .ToArray();

                await mikroWriteDbContext.Summaries.AddRangeAsync(updatedSummaries, cancellationToken);
                await RebuildCustomerMovementsAsync(
                    header,
                    updatedSummaries,
                    totalAmount,
                    now,
                    cancellationToken);
                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new UpdateCashSummaryDetailsResponse(
                    request.DocumentSerie,
                    request.DocumentOrderNo,
                    updatedSummaries.Length,
                    totalAmount);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<UpdateCashSummaryBanknotesResponse> UpdateBanknotesAsync(
        UpdateCashSummaryBanknotesRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            try
            {
                var existingSummaries = await mikroWriteDbContext.Summaries
                    .Where(item =>
                        item.WarehouseNo == request.WarehouseNo &&
                        item.DocumentSerie == request.DocumentSerie &&
                        item.DocumentOrderNo == request.DocumentOrderNo)
                    .OrderBy(item => item.Id)
                    .ToListAsync(cancellationToken);

                if (existingSummaries.Count == 0)
                {
                    throw new KeyNotFoundException("Cash summary was not found.");
                }

                var summaryHeader = existingSummaries[0];

                var existingBanknotes = await mikroWriteDbContext.BanknoteMovements
                    .Where(item =>
                        item.WarehouseNo == request.WarehouseNo &&
                        item.DocumentSerie == request.DocumentSerie &&
                        item.DocumentOrderNo == request.DocumentOrderNo)
                    .ToListAsync(cancellationToken);

                mikroWriteDbContext.BanknoteMovements.RemoveRange(existingBanknotes);

                var now = DateTime.Now;
                var updatedBanknotes = request.BanknoteMovements
                    .Where(item => item.Quantity > 0)
                    .Select(item => new BanknoteMovementEntity
                    {
                        Id = Guid.NewGuid(),
                        CreateUser = MikroUserNo,
                        CreateDate = summaryHeader.SummaryDate.Date,
                        UpdateUser = MikroUserNo,
                        UpdateDate = now,
                        DocumentSerie = request.DocumentSerie,
                        DocumentOrderNo = request.DocumentOrderNo,
                        SummaryDate = summaryHeader.SummaryDate,
                        WarehouseNo = request.WarehouseNo,
                        CashNo = summaryHeader.CashNo,
                        Value = item.Value,
                        BanknoteType = item.BanknoteType,
                        Quantity = item.Quantity,
                        Total = item.Total
                    })
                    .ToArray();
                var cashAmount = updatedBanknotes.Sum(item => item.Total);
                var cashSlipNumber = ResolveCashSlipNumber(updatedBanknotes);
                var totalAmount = CalculateDocumentTotal(existingSummaries, cashAmount);

                await mikroWriteDbContext.BanknoteMovements.AddRangeAsync(updatedBanknotes, cancellationToken);
                EnsureCashTotalSummary(existingSummaries, summaryHeader, cashAmount, totalAmount, cashSlipNumber, now);
                UpdateSummaryDocumentTotals(existingSummaries, totalAmount);
                await RebuildCustomerMovementsAsync(
                    summaryHeader,
                    existingSummaries,
                    totalAmount,
                    now,
                    cancellationToken);
                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new UpdateCashSummaryBanknotesResponse(
                    request.DocumentSerie,
                    request.DocumentOrderNo,
                    updatedBanknotes.Length,
                    cashAmount);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<UpdateCashSummaryGiftChecksResponse> UpdateGiftChecksAsync(
        UpdateCashSummaryGiftChecksRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            try
            {
                var summaryHeader = await mikroWriteDbContext.Summaries
                    .Where(item =>
                        item.WarehouseNo == request.WarehouseNo &&
                        item.DocumentSerie == request.DocumentSerie &&
                        item.DocumentOrderNo == request.DocumentOrderNo)
                    .OrderBy(item => item.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                if (summaryHeader is null)
                {
                    throw new KeyNotFoundException("Cash summary was not found.");
                }

                var existingGiftChecks = await mikroWriteDbContext.GiftCheckMovements
                    .Where(item =>
                        item.WarehouseNo == request.WarehouseNo &&
                        item.DocumentSerie == request.DocumentSerie &&
                        item.DocumentOrderNo == request.DocumentOrderNo)
                    .ToListAsync(cancellationToken);

                mikroWriteDbContext.GiftCheckMovements.RemoveRange(existingGiftChecks);

                var now = DateTime.Now;
                var updatedGiftChecks = request.GiftCheckMovements
                    .Where(item => item.Quantity > 0)
                    .Select(item => CreateGiftCheckMovementEntity(request, item, summaryHeader, now))
                    .ToArray();

                await mikroWriteDbContext.GiftCheckMovements.AddRangeAsync(updatedGiftChecks, cancellationToken);
                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new UpdateCashSummaryGiftChecksResponse(
                    request.DocumentSerie,
                    request.DocumentOrderNo,
                    updatedGiftChecks.Length,
                    updatedGiftChecks.Sum(item => item.Total));
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<DeleteCashSummaryResponse> DeleteAsync(
        DeleteCashSummaryRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            try
            {
                var summaries = await mikroWriteDbContext.Summaries
                    .Where(item =>
                        item.WarehouseNo == request.WarehouseNo &&
                        item.DocumentSerie == request.DocumentSerie &&
                        item.DocumentOrderNo == request.DocumentOrderNo)
                    .ToListAsync(cancellationToken);
                var banknotes = await mikroWriteDbContext.BanknoteMovements
                    .Where(item =>
                        item.WarehouseNo == request.WarehouseNo &&
                        item.DocumentSerie == request.DocumentSerie &&
                        item.DocumentOrderNo == request.DocumentOrderNo)
                    .ToListAsync(cancellationToken);
                var giftChecks = await mikroWriteDbContext.GiftCheckMovements
                    .Where(item =>
                        item.WarehouseNo == request.WarehouseNo &&
                        item.DocumentSerie == request.DocumentSerie &&
                        item.DocumentOrderNo == request.DocumentOrderNo)
                    .ToListAsync(cancellationToken);
                var customerMovements = await GetCustomerMovementsForCashSummaryAsync(
                    request.DocumentSerie,
                    request.DocumentOrderNo,
                    cancellationToken);

                mikroWriteDbContext.Summaries.RemoveRange(summaries);
                mikroWriteDbContext.BanknoteMovements.RemoveRange(banknotes);
                mikroWriteDbContext.GiftCheckMovements.RemoveRange(giftChecks);
                mikroWriteDbContext.CARI_HESAP_HAREKETLERIs.RemoveRange(customerMovements);
                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new DeleteCashSummaryResponse(
                    request.DocumentSerie,
                    request.DocumentOrderNo,
                    summaries.Count,
                    banknotes.Count,
                    giftChecks.Count,
                    customerMovements.Count);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<int> GetNextDocumentOrderNoAsync(
        string documentSerie,
        CancellationToken cancellationToken)
    {
        var currentMax = await mikroWriteDbContext.Summaries
            .Where(item => item.DocumentSerie == documentSerie)
            .MaxAsync(item => (int?)item.DocumentOrderNo, cancellationToken);

        return currentMax.HasValue ? currentMax.Value + 1 : FirstDocumentOrderNo;
    }

    private async Task<int> GetNextCustomerMovementDocumentOrderNoAsync(CancellationToken cancellationToken)
    {
        var currentMax = await mikroWriteDbContext.CARI_HESAP_HAREKETLERIs
            .Where(item =>
                item.cha_evrak_tip == CashSummaryCustomerMovementFactory.CustomerMovementDocumentType &&
                item.cha_evrakno_seri == CashSummaryCustomerMovementFactory.CustomerMovementDocumentSerie)
            .MaxAsync(item => item.cha_evrakno_sira, cancellationToken);

        return (currentMax ?? 0) + 1;
    }

    private async Task RebuildCustomerMovementsAsync(
        SummaryEntity header,
        IReadOnlyCollection<SummaryEntity> summaries,
        double totalAmount,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existingCustomerMovements = await GetCustomerMovementsForCashSummaryAsync(
            header.DocumentSerie,
            header.DocumentOrderNo,
            cancellationToken);
        var existingCustomerMovementDocumentOrderNo = existingCustomerMovements
            .Where(item => item.cha_evrakno_seri == CashSummaryCustomerMovementFactory.CustomerMovementDocumentSerie)
            .Select(item => item.cha_evrakno_sira)
            .FirstOrDefault(item => item.HasValue);
        var customerMovementDocumentOrderNo = existingCustomerMovementDocumentOrderNo ??
                                              await GetNextCustomerMovementDocumentOrderNoAsync(cancellationToken);
        var zTotalValue = CashSummaryCustomerMovementFactory.ResolveExistingZTotalValue(
            existingCustomerMovements,
            header.WarehouseNo);

        if (IsZero(zTotalValue))
        {
            zTotalValue = await ResolveZReportTotalValueAsync(header, cancellationToken);
        }

        var customerMovementLines = await BuildCustomerMovementLinesAsync(summaries, cancellationToken);
        var newCustomerMovements = CashSummaryCustomerMovementFactory.CreateMovements(
                header,
                customerMovementLines,
                zTotalValue,
                totalAmount,
                customerMovementDocumentOrderNo,
                now)
            .ToArray();

        mikroWriteDbContext.CARI_HESAP_HAREKETLERIs.RemoveRange(existingCustomerMovements);
        await mikroWriteDbContext.CARI_HESAP_HAREKETLERIs.AddRangeAsync(newCustomerMovements, cancellationToken);
    }

    private async Task<List<CARI_HESAP_HAREKETLERI>> GetCustomerMovementsForCashSummaryAsync(
        string documentSerie,
        int documentOrderNo,
        CancellationToken cancellationToken)
    {
        var legacyDescription = CashSummaryCustomerMovementFactory.BuildLegacyDescription(
            documentSerie,
            documentOrderNo);

        return await mikroWriteDbContext.CARI_HESAP_HAREKETLERIs
            .Where(item =>
                item.cha_evrak_tip == CashSummaryCustomerMovementFactory.CustomerMovementDocumentType &&
                ((item.cha_evrakno_seri == CashSummaryCustomerMovementFactory.CustomerMovementDocumentSerie &&
                  item.cha_aciklama == legacyDescription) ||
                 (item.cha_evrakno_seri == documentSerie &&
                  item.cha_evrakno_sira == documentOrderNo)))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyCollection<CashSummaryCustomerMovementLine>> BuildCustomerMovementLinesAsync(
        IEnumerable<SummaryEntity> summaries,
        CancellationToken cancellationToken)
    {
        var summaryLines = summaries.ToArray();
        var paymentTypeNos = summaryLines
            .Where(item => item.PaymentTypeId < 100)
            .Select(item => item.PaymentTypeId)
            .Distinct()
            .ToArray();
        var accountCodes = paymentTypeNos.Length == 0
            ? new Dictionary<int, string>()
            : await mikroWriteDbContext.PaymentTypes
                .Where(item => paymentTypeNos.Contains(item.PaymentTypeNo))
                .ToDictionaryAsync(
                    item => item.PaymentTypeNo,
                    item => item.AccountCode ?? string.Empty,
                    cancellationToken);
        var lines = new List<CashSummaryCustomerMovementLine>();

        foreach (var summary in summaryLines.Where(item => item.PaymentTypeId < 100))
        {
            var accountCode = !string.IsNullOrWhiteSpace(summary.AccountCode)
                ? summary.AccountCode
                : accountCodes.GetValueOrDefault(summary.PaymentTypeId, string.Empty);

            lines.Add(new CashSummaryCustomerMovementLine(
                summary.PaymentTypeId,
                accountCode,
                Math.Round(summary.Amount, 2)));
        }

        var cashAmount = summaryLines
            .Where(IsCashSummary)
            .Sum(item => item.Amount);

        if (!IsZero(cashAmount))
        {
            lines.Add(new CashSummaryCustomerMovementLine(
                CashSummaryCustomerMovementFactory.CashPaymentTypeNo,
                CashSummaryCustomerMovementFactory.ZDifferenceAccountCode,
                Math.Round(cashAmount, 2)));
        }

        return lines;
    }

    private async Task<double> ResolveZReportTotalValueAsync(
        SummaryEntity header,
        CancellationToken cancellationToken)
    {
        try
        {
            var zTotalValue = await getCashSummaryZReportTotalUseCase.ExecuteAsync(
                new ZReportValueRequest(
                    header.WarehouseNo,
                    header.DocumentSerie,
                    header.ZReportNo,
                    header.CashNo),
                cancellationToken);

            return zTotalValue > 0d ? Math.Round(zTotalValue, 2) : 0d;
        }
        catch (IOException)
        {
            return 0d;
        }
        catch (UnauthorizedAccessException)
        {
            return 0d;
        }
    }

    private async Task<double> ResolveCashTotalAmountAsync(
        UpdateCashSummaryDetailsRequest request,
        IReadOnlyCollection<SummaryEntity> existingSummaries,
        CancellationToken cancellationToken)
    {
        var banknoteTotal = await GetBanknoteTotalAsync(
            request.WarehouseNo,
            request.DocumentSerie,
            request.DocumentOrderNo,
            cancellationToken);

        if (!IsZero(banknoteTotal))
        {
            return banknoteTotal;
        }

        var existingCashTotal = existingSummaries
            .Where(IsCashSummary)
            .Sum(item => item.Amount);

        if (!IsZero(existingCashTotal))
        {
            return existingCashTotal;
        }

        return request.Details
            .Where(IsCashDetailLine)
            .Sum(item => item.Amount);
    }

    private async Task<double> GetBanknoteTotalAsync(
        int warehouseNo,
        string documentSerie,
        int documentOrderNo,
        CancellationToken cancellationToken)
    {
        var total = await mikroWriteDbContext.BanknoteMovements
            .Where(item =>
                item.WarehouseNo == warehouseNo &&
                item.DocumentSerie == documentSerie &&
                item.DocumentOrderNo == documentOrderNo)
            .SumAsync(item => (double?)item.Total, cancellationToken);

        return total ?? 0d;
    }

    private async Task<int> ResolveCashSlipNumberAsync(
        UpdateCashSummaryDetailsRequest request,
        IReadOnlyCollection<SummaryEntity> existingSummaries,
        CancellationToken cancellationToken)
    {
        var banknoteQuantity = await GetBanknoteQuantityAsync(
            request.WarehouseNo,
            request.DocumentSerie,
            request.DocumentOrderNo,
            cancellationToken);

        if (banknoteQuantity > 0)
        {
            return banknoteQuantity;
        }

        var existingCashSlipNumber = existingSummaries
            .Where(IsCashSummary)
            .Sum(item => item.SlipNumber);

        if (existingCashSlipNumber > 0)
        {
            return existingCashSlipNumber;
        }

        return request.Details
            .Where(IsCashDetailLine)
            .Sum(item => item.SlipNumber);
    }

    private async Task<int> GetBanknoteQuantityAsync(
        int warehouseNo,
        string documentSerie,
        int documentOrderNo,
        CancellationToken cancellationToken)
    {
        var quantity = await mikroWriteDbContext.BanknoteMovements
            .Where(item =>
                item.WarehouseNo == warehouseNo &&
                item.DocumentSerie == documentSerie &&
                item.DocumentOrderNo == documentOrderNo)
            .SumAsync(item => (int?)item.Quantity, cancellationToken);

        return quantity ?? 0;
    }

    private void EnsureCashTotalSummary(
        List<SummaryEntity> summaries,
        SummaryEntity header,
        double cashAmount,
        double documentTotal,
        int cashSlipNumber,
        DateTime now)
    {
        var cashSummaries = summaries
            .Where(IsCashSummary)
            .ToArray();
        var cashSummary = cashSummaries.FirstOrDefault();

        if (cashSummary is null)
        {
            cashSummary = CreateCashTotalSummaryEntity(header, cashAmount, documentTotal, cashSlipNumber, now);
            summaries.Add(cashSummary);
            mikroWriteDbContext.Summaries.Add(cashSummary);
        }
        else
        {
            ApplyCashTotalSummary(cashSummary, header, cashAmount, documentTotal, cashSlipNumber, now);
        }

        foreach (var duplicate in cashSummaries.Skip(1))
        {
            summaries.Remove(duplicate);
            mikroWriteDbContext.Summaries.Remove(duplicate);
        }
    }

    private static void UpdateSummaryDocumentTotals(
        IEnumerable<SummaryEntity> summaries,
        double documentTotal)
    {
        foreach (var summary in summaries)
        {
            summary.Total = documentTotal;
        }
    }

    private static double ResolveCashTotalAmount(
        IEnumerable<CreateCashSummaryPaymentLineRequest> paymentLines,
        IEnumerable<CreateCashSummaryBanknoteLineRequest> banknoteLines)
    {
        var banknoteTotal = banknoteLines.Sum(item => item.Total);

        return !IsZero(banknoteTotal)
            ? banknoteTotal
            : paymentLines
                .Where(IsCashPaymentLine)
                .Sum(item => item.AmountValue);
    }

    private static double CalculateCreateDocumentTotal(
        IEnumerable<CreateCashSummaryPaymentLineRequest> paymentLines,
        IEnumerable<CreateCashSummaryStoreExpenseLineRequest> storeExpenseLines,
        double cashAmount) =>
        cashAmount +
        paymentLines
            .Where(ShouldCountInDocumentTotal)
            .Sum(item => item.AmountValue) +
        storeExpenseLines.Sum(item => item.AmountValue);

    private static double CalculateDocumentTotal(
        IEnumerable<UpdateCashSummaryDetailLineRequest> detailLines,
        double cashAmount) =>
        cashAmount + detailLines
            .Where(ShouldCountInDocumentTotal)
            .Sum(item => item.Amount);

    private static double CalculateDocumentTotal(
        IEnumerable<SummaryEntity> summaries,
        double cashAmount) =>
        cashAmount + summaries
            .Where(item => !IsCashSummary(item) && ShouldCountInDocumentTotal(item))
            .Sum(item => item.Amount);

    private static bool ShouldCountInDocumentTotal(UpdateCashSummaryDetailLineRequest line) =>
        line.PaymentTypeId < 100 ||
        IsStoreExpensePaymentType(line.PaymentTypeId);

    private static bool ShouldCountInDocumentTotal(CreateCashSummaryPaymentLineRequest line) =>
        line.PaymentTypeNo < 100 &&
        !CashSummaryCategoryMatcher.IsStoreExpensePaymentType(line.PaymentName);

    private static bool ShouldCountInDocumentTotal(SummaryEntity line) =>
        line.PaymentTypeId < 100 ||
        line.StoreExpenseType is not null ||
        IsStoreExpensePaymentType(line.PaymentTypeId);

    private static bool IsCashPaymentLine(CreateCashSummaryPaymentLineRequest line) =>
        line.PaymentTypeNo == CashTotalPaymentTypeId ||
        CashSummaryCategoryMatcher.IsCashPaymentType(line.PaymentName);

    private static bool ShouldWritePaymentLine(CreateCashSummaryPaymentLineRequest line) =>
        !IsZero(line.AmountValue) || line.SlipNumber != 0;

    private static bool ShouldWriteDetailLine(UpdateCashSummaryDetailLineRequest line) =>
        !IsZero(line.Amount) || line.SlipNumber != 0;

    private static bool ShouldWriteStoreExpenseLine(CreateCashSummaryStoreExpenseLineRequest line) =>
        !IsZero(line.AmountValue);

    private static bool ShouldWriteBanknoteLine(CreateCashSummaryBanknoteLineRequest line) =>
        line.Quantity > 0 || !IsZero(line.Total);

    private static bool ShouldWriteGiftCheckLine(CreateCashSummaryGiftCheckLineRequest line) =>
        line.Quantity > 0 || !IsZero(line.Total);

    private static bool IsCashDetailLine(UpdateCashSummaryDetailLineRequest line) =>
        line.PaymentTypeId == CashTotalPaymentTypeId ||
        CashSummaryCategoryMatcher.IsCashPaymentType(line.TypeName);

    private static bool IsCashSummary(SummaryEntity item) =>
        item.PaymentTypeId == CashTotalPaymentTypeId;

    private static int? ResolveStoreExpenseType(int paymentTypeId) =>
        IsStoreExpensePaymentType(paymentTypeId)
            ? paymentTypeId
            : null;

    private static bool IsStoreExpensePaymentType(int paymentTypeId) =>
        paymentTypeId is >= StoreExpensePaymentTypeStart and <= StoreExpensePaymentTypeEnd;

    private static string BuildDocumentSerie(int warehouseNo, int cashNo) =>
        $"F{warehouseNo}.{cashNo}";

    private static bool IsZero(double value) =>
        Math.Abs(value) < 0.000_001d;

    private static int ResolveCashSlipNumber(
        IEnumerable<CreateCashSummaryPaymentLineRequest> paymentLines,
        IEnumerable<CreateCashSummaryBanknoteLineRequest> banknoteLines)
    {
        var banknoteQuantity = banknoteLines.Sum(item => item.Quantity);

        if (banknoteQuantity > 0)
        {
            return banknoteQuantity;
        }

        return paymentLines
            .Where(IsCashPaymentLine)
            .Sum(item => item.SlipNumber);
    }

    private static int ResolveCashSlipNumber(IEnumerable<BanknoteMovementEntity> banknoteLines) =>
        banknoteLines.Sum(item => item.Quantity);

    private static int ResolveCashSummarySlipNumber(double cashAmount, int cashSlipNumber)
    {
        if (IsZero(cashAmount))
        {
            return 0;
        }

        return cashSlipNumber > 0 ? cashSlipNumber : DefaultCashTotalSlipNumber;
    }

    private static SummaryEntity CreateCashTotalSummaryEntity(
        CreateCashSummaryRequest request,
        double cashAmount,
        double documentTotal,
        int cashSlipNumber,
        DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            CreateUser = MikroUserNo,
            CreateDate = now,
            UpdateUser = MikroUserNo,
            UpdateDate = now,
            CashNo = request.CashNo,
            ZReportNo = request.ZReportNo,
            CashierNo = request.CashierNo,
            ManagerNo = request.ManagerNo,
            SummaryDate = request.SummaryDate.Date,
            Total = documentTotal,
            PaymentTypeId = CashTotalPaymentTypeId,
            Amount = cashAmount,
            WarehouseNo = request.WarehouseNo,
            TypeName = CashTotalTypeName,
            AccountCode = string.Empty,
            SlipNumber = ResolveCashSummarySlipNumber(cashAmount, cashSlipNumber),
            TerminalId = string.Empty,
            Description = CashTotalDescription,
            StoreExpenseType = null
        };

    private static SummaryEntity CreateCashTotalSummaryEntity(
        SummaryEntity header,
        double cashAmount,
        double documentTotal,
        int cashSlipNumber,
        DateTime now)
    {
        var summary = new SummaryEntity();
        ApplyCashTotalSummary(summary, header, cashAmount, documentTotal, cashSlipNumber, now);
        return summary;
    }

    private static void ApplyCashTotalSummary(
        SummaryEntity summary,
        SummaryEntity header,
        double cashAmount,
        double documentTotal,
        int cashSlipNumber,
        DateTime now)
    {
        summary.DocumentSerie = header.DocumentSerie;
        summary.DocumentOrderNo = header.DocumentOrderNo;
        if (summary.Id == Guid.Empty)
        {
            summary.Id = Guid.NewGuid();
        }

        summary.CreateUser = MikroUserNo;
        summary.CreateDate = now;
        summary.UpdateUser = MikroUserNo;
        summary.UpdateDate = now;
        summary.CashNo = header.CashNo;
        summary.ZReportNo = header.ZReportNo;
        summary.CashierNo = header.CashierNo;
        summary.ManagerNo = header.ManagerNo;
        summary.SummaryDate = header.SummaryDate;
        summary.Total = documentTotal;
        summary.PaymentTypeId = CashTotalPaymentTypeId;
        summary.Amount = cashAmount;
        summary.WarehouseNo = header.WarehouseNo;
        summary.TypeName = CashTotalTypeName;
        summary.AccountCode = string.Empty;
        summary.SlipNumber = ResolveCashSummarySlipNumber(cashAmount, cashSlipNumber);
        summary.TerminalId = string.Empty;
        summary.Description = CashTotalDescription;
        summary.StoreExpenseType = null;
    }

    private static SummaryEntity CreateSummaryEntity(
        CreateCashSummaryRequest request,
        CreateCashSummaryPaymentLineRequest line,
        double documentTotal,
        DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            CreateUser = MikroUserNo,
            CreateDate = now,
            UpdateUser = MikroUserNo,
            UpdateDate = now,
            CashNo = request.CashNo,
            ZReportNo = request.ZReportNo,
            CashierNo = request.CashierNo,
            ManagerNo = request.ManagerNo,
            SummaryDate = request.SummaryDate.Date,
            Total = documentTotal,
            PaymentTypeId = line.PaymentTypeNo,
            Amount = line.AmountValue,
            WarehouseNo = request.WarehouseNo,
            TypeName = NormalizeText(line.PaymentName),
            AccountCode = NormalizeText(line.AccountCode),
            SlipNumber = line.SlipNumber,
            TerminalId = NormalizeText(line.TerminalId),
            Description = string.Empty,
            StoreExpenseType = null
        };

    private static SummaryEntity CreateSummaryEntity(
        CreateCashSummaryRequest request,
        CreateCashSummaryStoreExpenseLineRequest line,
        double documentTotal,
        DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            CreateUser = MikroUserNo,
            CreateDate = now,
            UpdateUser = MikroUserNo,
            UpdateDate = now,
            CashNo = request.CashNo,
            ZReportNo = request.ZReportNo,
            CashierNo = request.CashierNo,
            ManagerNo = request.ManagerNo,
            SummaryDate = request.SummaryDate.Date,
            Total = documentTotal,
            PaymentTypeId = line.StoreExpenseType,
            Amount = line.AmountValue,
            WarehouseNo = request.WarehouseNo,
            TypeName = NormalizeText(string.IsNullOrWhiteSpace(line.Description) ? "StoreExpense" : line.Description),
            AccountCode = string.Empty,
            SlipNumber = 1,
            TerminalId = string.Empty,
            Description = NormalizeText(line.Description),
            StoreExpenseType = line.StoreExpenseType
        };

    private static BanknoteMovementEntity CreateBanknoteMovementEntity(
        CreateCashSummaryRequest request,
        CreateCashSummaryBanknoteLineRequest line,
        string documentSerie,
        int documentOrderNo,
        DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            CreateUser = MikroUserNo,
            CreateDate = request.SummaryDate.Date,
            UpdateUser = MikroUserNo,
            UpdateDate = now,
            DocumentSerie = documentSerie,
            DocumentOrderNo = documentOrderNo,
            SummaryDate = request.SummaryDate.Date,
            WarehouseNo = request.WarehouseNo,
            CashNo = request.CashNo,
            Value = line.Value,
            BanknoteType = line.BanknoteType,
            Quantity = line.Quantity,
            Total = line.Total
        };

    private static GiftCheckMovementEntity CreateGiftCheckMovementEntity(
        CreateCashSummaryRequest request,
        CreateCashSummaryGiftCheckLineRequest line,
        string documentSerie,
        int documentOrderNo,
        DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            CreateDate = now,
            DocumentSerie = documentSerie,
            DocumentOrderNo = documentOrderNo,
            SummaryDate = request.SummaryDate.Date,
            WarehouseNo = request.WarehouseNo,
            CashNo = request.CashNo,
            Value = line.Value,
            GiftCheckType = line.GiftCheckType,
            Quantity = line.Quantity,
            Total = line.Total
        };

    private static GiftCheckMovementEntity CreateGiftCheckMovementEntity(
        UpdateCashSummaryGiftChecksRequest request,
        UpdateCashSummaryGiftCheckLineRequest line,
        SummaryEntity header,
        DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            CreateDate = now,
            DocumentSerie = request.DocumentSerie,
            DocumentOrderNo = request.DocumentOrderNo,
            SummaryDate = header.SummaryDate,
            WarehouseNo = request.WarehouseNo,
            CashNo = header.CashNo,
            Value = line.Value,
            GiftCheckType = line.GiftCheckType,
            Quantity = line.Quantity,
            Total = line.Total
        };

    private static void Validate(CreateCashSummaryRequest request)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        if (request.CashNo <= 0 || request.ZReportNo < 0 || request.CashierNo <= 0 || request.ManagerNo <= 0)
        {
            throw new ArgumentException("Cash, Z report, cashier and manager values must be valid.");
        }

        if (request.SummaryDate == default)
        {
            throw new ArgumentException("Summary date is required.", nameof(request.SummaryDate));
        }

        if (request.PaymentTypes.Count == 0 &&
            request.StoreExpenses.Count == 0 &&
            request.BanknoteMovements.Count == 0)
        {
            throw new ArgumentException("At least one summary detail line is required.");
        }
    }

    private static void Validate(UpdateCashSummaryDetailsRequest request)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        if (string.IsNullOrWhiteSpace(request.DocumentSerie))
        {
            throw new ArgumentException("Document serie is required.", nameof(request.DocumentSerie));
        }

        if (request.DocumentOrderNo < 0)
        {
            throw new ArgumentException("Document order no can not be negative.", nameof(request.DocumentOrderNo));
        }

        if (request.Details.Count == 0)
        {
            throw new ArgumentException("At least one detail line is required.", nameof(request.Details));
        }
    }

    private static void Validate(UpdateCashSummaryBanknotesRequest request)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        if (string.IsNullOrWhiteSpace(request.DocumentSerie))
        {
            throw new ArgumentException("Document serie is required.", nameof(request.DocumentSerie));
        }

        if (request.DocumentOrderNo < 0)
        {
            throw new ArgumentException("Document order no can not be negative.", nameof(request.DocumentOrderNo));
        }
    }

    private static void Validate(UpdateCashSummaryGiftChecksRequest request)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        if (string.IsNullOrWhiteSpace(request.DocumentSerie))
        {
            throw new ArgumentException("Document serie is required.", nameof(request.DocumentSerie));
        }

        if (request.DocumentOrderNo < 0)
        {
            throw new ArgumentException("Document order no can not be negative.", nameof(request.DocumentOrderNo));
        }
    }

    private static void Validate(DeleteCashSummaryRequest request)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        if (string.IsNullOrWhiteSpace(request.DocumentSerie))
        {
            throw new ArgumentException("Document serie is required.", nameof(request.DocumentSerie));
        }

        if (request.DocumentOrderNo < 0)
        {
            throw new ArgumentException("Document order no can not be negative.", nameof(request.DocumentOrderNo));
        }
    }

    private static string NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}

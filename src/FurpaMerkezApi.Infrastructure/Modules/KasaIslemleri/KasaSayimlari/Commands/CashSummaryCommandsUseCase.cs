using System.Data;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari.Commands;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaSayimlari;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaSayimlari.Commands;

public sealed class CashSummaryCommandsUseCase(
    MikroWriteDbContext mikroWriteDbContext,
    IOptions<MikroWriteOptions> mikroWriteOptions)
    : ICashSummaryCommandsUseCase
{
    private const short MikroUserNo = 39;
    private const short CustomerMovementFileId = 1;
    private const byte CustomerMovementDocumentType = 0;
    private const byte CustomerMovementType = 0;
    private const byte CustomerMovementGenre = 0;
    private const byte CustomerMovementNormalReturn = 0;
    private const byte CustomerMovementTpoz = 0;
    private const byte CustomerMovementTradeType = 0;
    private const int FirstDocumentOrderNo = 1;
    private const int CashTotalPaymentTypeId = 500;
    private const int CashTotalSlipNumber = 1;
    private const int StoreExpensePaymentTypeStart = 110;
    private const int StoreExpensePaymentTypeEnd = 113;
    private const string CashTotalTypeName = "Nakit";
    private const string CashTotalDescription = "Nakit Toplam";
    private static readonly DateTime MikroEmptyDate = new(1899, 12, 30);

    public async Task<CreateCashSummaryResponse> CreateAsync(
        CreateCashSummaryRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var options = mikroWriteOptions.Value;
        var now = DateTime.Now;
        var summaryDate = request.SummaryDate.Date;
        var documentSerie = BuildDocumentSerie(request.WarehouseNo, request.CashNo);
        var banknoteLines = request.BanknoteMovements.ToArray();
        var cashAmount = ResolveCashTotalAmount(request.PaymentTypes, banknoteLines);
        var paymentLines = request.PaymentTypes
            .Where(line => !IsCashPaymentLine(line))
            .ToArray();
        var storeExpenseLines = request.StoreExpenses.ToArray();
        var documentTotal = ResolveCreateDocumentTotal(
            request.Total,
            paymentLines,
            cashAmount);
        var summaryLines = paymentLines
            .Select(line => CreateSummaryEntity(request, line, documentTotal, now))
            .Concat(storeExpenseLines.Select(line => CreateSummaryEntity(request, line, documentTotal, now)))
            .Prepend(CreateCashTotalSummaryEntity(request, cashAmount, documentTotal, now))
            .ToArray();
        var giftCheckLines = request.GiftCheckMovements.ToArray();
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

                var banknoteEntities = banknoteLines
                    .Select(line => CreateBanknoteMovementEntity(request, line, documentSerie, documentOrderNo, now))
                    .ToArray();
                var giftCheckEntities = giftCheckLines
                    .Select(line => CreateGiftCheckMovementEntity(request, line, documentSerie, documentOrderNo, now))
                    .ToArray();
                var customerMovement = CreateCustomerMovementEntity(
                    request,
                    summaryDate,
                    documentSerie,
                    documentOrderNo,
                    documentTotal,
                    now);

                await mikroWriteDbContext.Summaries.AddRangeAsync(summaryLines, cancellationToken);
                await mikroWriteDbContext.BanknoteMovements.AddRangeAsync(banknoteEntities, cancellationToken);
                await mikroWriteDbContext.GiftCheckMovements.AddRangeAsync(giftCheckEntities, cancellationToken);
                await mikroWriteDbContext.CARI_HESAP_HAREKETLERIs.AddAsync(customerMovement, cancellationToken);
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
                var detailLines = request.Details
                    .Where(line => !IsCashDetailLine(line))
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
                    .Prepend(CreateCashTotalSummaryEntity(header, cashAmount, totalAmount, now))
                    .ToArray();

                await mikroWriteDbContext.Summaries.AddRangeAsync(updatedSummaries, cancellationToken);
                await UpdateCustomerMovementTotalsAsync(
                    request.DocumentSerie,
                    request.DocumentOrderNo,
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
                        CreateDate = now,
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
                var totalAmount = CalculateDocumentTotal(existingSummaries, cashAmount);

                await mikroWriteDbContext.BanknoteMovements.AddRangeAsync(updatedBanknotes, cancellationToken);
                EnsureCashTotalSummary(existingSummaries, summaryHeader, cashAmount, totalAmount, now);
                UpdateSummaryDocumentTotals(existingSummaries, totalAmount);
                await UpdateCustomerMovementTotalsAsync(
                    request.DocumentSerie,
                    request.DocumentOrderNo,
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
                var customerMovements = await mikroWriteDbContext.CARI_HESAP_HAREKETLERIs
                    .Where(item =>
                        item.cha_evrakno_seri == request.DocumentSerie &&
                        item.cha_evrakno_sira == request.DocumentOrderNo)
                    .ToListAsync(cancellationToken);

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

    private async Task UpdateCustomerMovementTotalsAsync(
        string documentSerie,
        int documentOrderNo,
        double totalAmount,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var customerMovements = await mikroWriteDbContext.CARI_HESAP_HAREKETLERIs
            .Where(item =>
                item.cha_evrakno_seri == documentSerie &&
                item.cha_evrakno_sira == documentOrderNo)
            .ToListAsync(cancellationToken);

        foreach (var movement in customerMovements)
        {
            movement.cha_meblag = totalAmount;
            movement.cha_aratoplam = totalAmount;
            movement.cha_lastup_user = MikroUserNo;
            movement.cha_lastup_date = now;
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

    private void EnsureCashTotalSummary(
        List<SummaryEntity> summaries,
        SummaryEntity header,
        double cashAmount,
        double documentTotal,
        DateTime now)
    {
        var cashSummaries = summaries
            .Where(IsCashSummary)
            .ToArray();
        var cashSummary = cashSummaries.FirstOrDefault();

        if (cashSummary is null)
        {
            cashSummary = CreateCashTotalSummaryEntity(header, cashAmount, documentTotal, now);
            summaries.Add(cashSummary);
            mikroWriteDbContext.Summaries.Add(cashSummary);
        }
        else
        {
            ApplyCashTotalSummary(cashSummary, header, cashAmount, documentTotal, now);
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

    private static double ResolveCreateDocumentTotal(
        double requestTotal,
        IEnumerable<CreateCashSummaryPaymentLineRequest> paymentLines,
        double cashAmount) =>
        !IsZero(requestTotal)
            ? requestTotal
            : cashAmount + paymentLines
                .Where(ShouldCountInDocumentTotal)
                .Sum(item => item.AmountValue);

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
        line.PaymentTypeId < 100 &&
        !CashSummaryCategoryMatcher.IsStoreExpensePaymentType(line.TypeName);

    private static bool ShouldCountInDocumentTotal(CreateCashSummaryPaymentLineRequest line) =>
        line.PaymentTypeNo < 100 &&
        !CashSummaryCategoryMatcher.IsStoreExpensePaymentType(line.PaymentName);

    private static bool ShouldCountInDocumentTotal(SummaryEntity line) =>
        line.PaymentTypeId < 100 && line.StoreExpenseType is null;

    private static bool IsCashPaymentLine(CreateCashSummaryPaymentLineRequest line) =>
        line.PaymentTypeNo == CashTotalPaymentTypeId ||
        CashSummaryCategoryMatcher.IsCashPaymentType(line.PaymentName);

    private static bool IsCashDetailLine(UpdateCashSummaryDetailLineRequest line) =>
        line.PaymentTypeId == CashTotalPaymentTypeId ||
        CashSummaryCategoryMatcher.IsCashPaymentType(line.TypeName);

    private static bool IsCashSummary(SummaryEntity item) =>
        item.PaymentTypeId == CashTotalPaymentTypeId;

    private static int? ResolveStoreExpenseType(int paymentTypeId) =>
        paymentTypeId is >= StoreExpensePaymentTypeStart and <= StoreExpensePaymentTypeEnd
            ? paymentTypeId
            : null;

    private static string BuildDocumentSerie(int warehouseNo, int cashNo) =>
        $"F{warehouseNo}.{cashNo}";

    private static bool IsZero(double value) =>
        Math.Abs(value) < 0.000_001d;

    private static SummaryEntity CreateCashTotalSummaryEntity(
        CreateCashSummaryRequest request,
        double cashAmount,
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
            PaymentTypeId = CashTotalPaymentTypeId,
            Amount = cashAmount,
            WarehouseNo = request.WarehouseNo,
            TypeName = CashTotalTypeName,
            AccountCode = string.Empty,
            SlipNumber = IsZero(cashAmount) ? 0 : CashTotalSlipNumber,
            TerminalId = string.Empty,
            Description = CashTotalDescription,
            StoreExpenseType = null
        };

    private static SummaryEntity CreateCashTotalSummaryEntity(
        SummaryEntity header,
        double cashAmount,
        double documentTotal,
        DateTime now)
    {
        var summary = new SummaryEntity();
        ApplyCashTotalSummary(summary, header, cashAmount, documentTotal, now);
        return summary;
    }

    private static void ApplyCashTotalSummary(
        SummaryEntity summary,
        SummaryEntity header,
        double cashAmount,
        double documentTotal,
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
        summary.SlipNumber = IsZero(cashAmount) ? 0 : CashTotalSlipNumber;
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
            CreateDate = now,
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

    private static CARI_HESAP_HAREKETLERI CreateCustomerMovementEntity(
        CreateCashSummaryRequest request,
        DateTime summaryDate,
        string documentSerie,
        int documentOrderNo,
        double documentTotal,
        DateTime now) =>
        new()
        {
            cha_Guid = Guid.NewGuid(),
            cha_DBCno = 0,
            cha_SpecRecNo = 0,
            cha_iptal = false,
            cha_fileid = CustomerMovementFileId,
            cha_hidden = false,
            cha_kilitli = false,
            cha_degisti = false,
            cha_CheckSum = 0,
            cha_create_user = MikroUserNo,
            cha_create_date = now,
            cha_lastup_user = MikroUserNo,
            cha_lastup_date = now,
            cha_special1 = string.Empty,
            cha_special2 = string.Empty,
            cha_special3 = string.Empty,
            cha_firmano = 0,
            cha_subeno = 0,
            cha_evrak_tip = CustomerMovementDocumentType,
            cha_evrakno_seri = documentSerie,
            cha_evrakno_sira = documentOrderNo,
            cha_satir_no = 0,
            cha_tarihi = summaryDate,
            cha_tip = CustomerMovementType,
            cha_cinsi = CustomerMovementGenre,
            cha_normal_Iade = CustomerMovementNormalReturn,
            cha_tpoz = CustomerMovementTpoz,
            cha_ticaret_turu = CustomerMovementTradeType,
            cha_belge_no = $"{request.CashNo}-{request.ZReportNo}",
            cha_belge_tarih = summaryDate,
            cha_aciklama = $"Kasa sayimi {documentSerie}/{documentOrderNo}",
            cha_satici_kodu = request.CashierNo.ToString(),
            cha_cari_cins = 0,
            cha_kod = $"KASA-{request.WarehouseNo}",
            cha_d_cins = 0,
            cha_d_kur = 1d,
            cha_altd_kur = 0d,
            cha_grupno = 0,
            cha_srmrkkodu = string.Empty,
            cha_kasa_hizmet = 0,
            cha_kasa_hizkod = request.CashNo.ToString(),
            cha_karsidcinsi = 0,
            cha_karsid_kur = 1d,
            cha_karsidgrupno = 0,
            cha_karsisrmrkkodu = string.Empty,
            cha_miktari = 1d,
            cha_meblag = documentTotal,
            cha_aratoplam = documentTotal,
            cha_vade = 0,
            cha_Vade_Farki_Yuz = 0d,
            cha_ft_iskonto1 = 0d,
            cha_ft_iskonto2 = 0d,
            cha_ft_iskonto3 = 0d,
            cha_ft_iskonto4 = 0d,
            cha_ft_iskonto5 = 0d,
            cha_ft_iskonto6 = 0d,
            cha_ft_masraf1 = 0d,
            cha_ft_masraf2 = 0d,
            cha_ft_masraf3 = 0d,
            cha_ft_masraf4 = 0d,
            cha_isk_mas1 = 0,
            cha_isk_mas2 = 0,
            cha_isk_mas3 = 0,
            cha_isk_mas4 = 0,
            cha_isk_mas5 = 0,
            cha_isk_mas6 = 0,
            cha_isk_mas7 = 0,
            cha_isk_mas8 = 0,
            cha_isk_mas9 = 0,
            cha_isk_mas10 = 0,
            cha_sat_iskmas1 = false,
            cha_sat_iskmas2 = false,
            cha_sat_iskmas3 = false,
            cha_sat_iskmas4 = false,
            cha_sat_iskmas5 = false,
            cha_sat_iskmas6 = false,
            cha_sat_iskmas7 = false,
            cha_sat_iskmas8 = false,
            cha_sat_iskmas9 = false,
            cha_sat_iskmas10 = false,
            cha_yuvarlama = 0d,
            cha_StFonPntr = 0,
            cha_stopaj = 0d,
            cha_savsandesfonu = 0d,
            cha_avansmak_damgapul = 0d,
            cha_vergipntr = 0,
            cha_vergisiz_fl = false,
            cha_otvtutari = 0d,
            cha_otvvergisiz_fl = false,
            cha_oiv_pntr = 0,
            cha_oivtutari = 0d,
            cha_oiv_vergi = 0d,
            cha_oivergisiz_fl = false,
            cha_fis_tarih = MikroEmptyDate,
            cha_fis_sirano = 0,
            cha_trefno = string.Empty,
            cha_sntck_poz = 0,
            cha_reftarihi = summaryDate,
            cha_istisnakodu = 0,
            cha_pos_hareketi = 0,
            cha_meblag_ana_doviz_icin_gecersiz_fl = 0,
            cha_meblag_alt_doviz_icin_gecersiz_fl = 0,
            cha_meblag_orj_doviz_icin_gecersiz_fl = 0,
            cha_sip_uid = Guid.Empty,
            cha_kirahar_uid = Guid.Empty,
            cha_vardiya_tarihi = summaryDate,
            cha_vardiya_no = Convert.ToByte(Math.Clamp(request.CashNo, 0, byte.MaxValue)),
            cha_vardiya_evrak_ti = 0,
            cha_ebelge_turu = 0,
            cha_tevkifat_toplam = 0d,
            cha_e_islem_turu = 0,
            cha_fatura_belge_turu = 0,
            cha_diger_belge_adi = string.Empty,
            cha_uuid = string.Empty,
            cha_adres_no = 0,
            cha_vergifon_toplam = 0d,
            cha_ilk_belge_tarihi = summaryDate,
            cha_ilk_belge_doviz_kuru = 1d,
            cha_HareketGrupKodu1 = string.Empty,
            cha_HareketGrupKodu2 = string.Empty,
            cha_HareketGrupKodu3 = string.Empty,
            cha_ebelgeno_seri = string.Empty,
            cha_ebelgeno_sira = 0,
            cha_hubid = string.Empty,
            cha_hubglbid = string.Empty,
            cha_disyazilimid = string.Empty,
            cha_disyazilim_tip = 0,
            cha_bsba_e_belge_mi = 0,
            cha_eticaret_kanal_kodu = string.Empty,
            cha_hizli_satis_kasa_no = Convert.ToInt16(Math.Clamp(request.CashNo, 0, short.MaxValue)),
            cha_ebelge_Islemturu = 0,
            cha_tevkifat_sifirlandi_fl = false,
            cha_vergi1 = 0d,
            cha_vergi2 = 0d,
            cha_vergi3 = 0d,
            cha_vergi4 = 0d,
            cha_vergi5 = 0d,
            cha_vergi6 = 0d,
            cha_vergi7 = 0d,
            cha_vergi8 = 0d,
            cha_vergi9 = 0d,
            cha_vergi10 = 0d,
            cha_vergi11 = 0d,
            cha_vergi12 = 0d,
            cha_vergi13 = 0d,
            cha_vergi14 = 0d,
            cha_vergi15 = 0d,
            cha_vergi16 = 0d,
            cha_vergi17 = 0d,
            cha_vergi18 = 0d,
            cha_vergi19 = 0d,
            cha_vergi20 = 0d,
            cha_ilave_edilecek_kdv1 = 0d,
            cha_ilave_edilecek_kdv2 = 0d,
            cha_ilave_edilecek_kdv3 = 0d,
            cha_ilave_edilecek_kdv4 = 0d,
            cha_ilave_edilecek_kdv5 = 0d,
            cha_ilave_edilecek_kdv6 = 0d,
            cha_ilave_edilecek_kdv7 = 0d,
            cha_ilave_edilecek_kdv8 = 0d,
            cha_ilave_edilecek_kdv9 = 0d,
            cha_ilave_edilecek_kdv10 = 0d,
            cha_ilave_edilecek_kdv11 = 0d,
            cha_ilave_edilecek_kdv12 = 0d,
            cha_ilave_edilecek_kdv13 = 0d,
            cha_ilave_edilecek_kdv14 = 0d,
            cha_ilave_edilecek_kdv15 = 0d,
            cha_ilave_edilecek_kdv16 = 0d,
            cha_ilave_edilecek_kdv17 = 0d,
            cha_ilave_edilecek_kdv18 = 0d,
            cha_ilave_edilecek_kdv19 = 0d,
            cha_ilave_edilecek_kdv20 = 0d,
            cha_efatura_belge_tipi = 0
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

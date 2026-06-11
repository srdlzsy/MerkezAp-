using System.Data;
using System.Data.Common;
using System.Globalization;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.PosMuhasebeAktarimi;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.PosMuhasebeAktarimi;

public sealed class PosMuhasebeAktarimiService(
    MikroDbContext mikroDbContext,
    MikroWriteDbContext mikroWriteDbContext,
    FurpaDbContext furpaDbContext,
    IConfiguration configuration)
    : IPosMuhasebeAktarimiService
{
    private const string CashPaymentType = "Nakit";
    private const string CreditPaymentType = "Kredi";
    private const string ZReportKind = "ZReport";
    private const string InvoiceKind = "Invoice";
    private const string ExpenseKind = "ExpenseNote";
    private const string CashAccountCode = "100.01.02";
    private const string CreditAccountCode = "108.01.02";
    private const string ZReportDescription = "Z RAPORU";
    private const byte CommercialAccountingType = 2;
    private const byte CommercialDocumentType = 63;
    private const byte AccountingLineType = 0;
    private const short AccountingCategory = 1;
    private const byte ZReportOffsetType = 11;
    private const byte DefaultOffsetType = 0;

    public async Task<PosAccountingOverviewDto> GetOverviewAsync(
        PosAccountingFilterRequest request,
        CancellationToken cancellationToken)
    {
        var zReports = ApplyZReportFilter(mikroDbContext.ZReportTotals.AsNoTracking(), request);
        var invoices = ApplyInvoiceFilter(mikroDbContext.Invoices.AsNoTracking(), request);
        var expenseNotes = ApplyExpenseNoteFilter(mikroDbContext.ExpenseNotes.AsNoTracking(), request);

        return new PosAccountingOverviewDto(
            await zReports.CountAsync(cancellationToken),
            await zReports.SumAsync(item => (double?)item.GreatTotal, cancellationToken) ?? 0d,
            await invoices.CountAsync(cancellationToken),
            await invoices.SumAsync(item => (decimal?)item.InvoiceTotal, cancellationToken) ?? 0m,
            await expenseNotes.CountAsync(cancellationToken),
            await expenseNotes.SumAsync(item => (decimal?)item.ExpenseTotal, cancellationToken) ?? 0m,
            await mikroDbContext.CashRegisterBranches.AsNoTracking().CountAsync(cancellationToken));
    }

    public async Task<IReadOnlyCollection<ZReportListItemDto>> ListZReportsAsync(
        PosAccountingFilterRequest request,
        CancellationToken cancellationToken)
    {
        var query =
            from report in ApplyZReportFilter(mikroDbContext.ZReportTotals.AsNoTracking(), request)
            join mapping in mikroDbContext.CashRegisterBranches.AsNoTracking()
                on report.CashRegisterNo equals mapping.CashRegisterNo
            join warehouse in mikroDbContext.DEPOLARs.AsNoTracking()
                on (int?)mapping.BranchNo equals warehouse.dep_no into warehouseJoin
            from warehouse in warehouseJoin.DefaultIfEmpty()
            orderby report.Date, mapping.BranchNo, report.ZNo
            select new ZReportListItemDto(
                report.TotalId,
                report.BillNo,
                report.ZNo,
                report.CashRegisterNo,
                warehouse.dep_adi ?? string.Empty,
                report.Date,
                report.CashPaymentTotal,
                report.CreditCardPaymentTotal,
                report.GreatTotal,
                report.IsSent);

        return await query.ToArrayAsync(cancellationToken);
    }

    public async Task<ZReportDetailDto> GetZReportDetailAsync(
        int totalId,
        CancellationToken cancellationToken)
    {
        var header = await (
            from report in mikroDbContext.ZReportTotals.AsNoTracking()
            join mapping in mikroDbContext.CashRegisterBranches.AsNoTracking()
                on report.CashRegisterNo equals mapping.CashRegisterNo
            join warehouse in mikroDbContext.DEPOLARs.AsNoTracking()
                on (int?)mapping.BranchNo equals warehouse.dep_no into warehouseJoin
            from warehouse in warehouseJoin.DefaultIfEmpty()
            where report.TotalId == totalId
            select new ZReportListItemDto(
                report.TotalId,
                report.BillNo,
                report.ZNo,
                report.CashRegisterNo,
                warehouse.dep_adi ?? string.Empty,
                report.Date,
                report.CashPaymentTotal,
                report.CreditCardPaymentTotal,
                report.GreatTotal,
                report.IsSent))
            .FirstOrDefaultAsync(cancellationToken);

        if (header is null)
        {
            throw new KeyNotFoundException("Z report was not found.");
        }

        var details = await mikroDbContext.ZReportDetails
            .AsNoTracking()
            .Where(item => item.TotalId == totalId)
            .OrderBy(item => item.TaxRate)
            .Select(item => new ZReportDetailLineDto(
                item.DetailId,
                item.TotalId,
                item.TaxRate,
                item.BillTotal,
                item.BillTaxTotal))
            .ToArrayAsync(cancellationToken);

        var bankDetails = await mikroDbContext.ZReportBankDetails
            .AsNoTracking()
            .Where(item => item.TotalId == totalId)
            .OrderBy(item => item.Bank)
            .ThenBy(item => item.BankingNumber)
            .Select(item => new ZReportBankDetailDto(
                item.BankDetailId,
                item.TotalId,
                item.Bank,
                item.BankAmount,
                item.BankingNumber))
            .ToArrayAsync(cancellationToken);

        return new ZReportDetailDto(header, details, bankDetails);
    }

    public Task<PosAccountingImportResultDto> ImportZReportsAsync(
        ImportZReportsRequest request,
        CancellationToken cancellationToken)
    {
        _ = request;
        _ = cancellationToken;

        return Task.FromResult(new PosAccountingImportResultDto(
            ZReportKind,
            request.BusinessDate?.Date ?? DateTime.Today,
            0,
            0,
            1,
            [
                new PosAccountingOperationResultDto(
                    null,
                    null,
                    false,
                    "Z raporu dosya parser'i eski WinUI kaynak kodu olmadan bu API projesinde henuz uygulanmadi.")
            ]));
    }

    public async Task<PosAccountingBatchResultDto> SendZReportsToErpAsync(
        PosAccountingTransferRequest request,
        CancellationToken cancellationToken)
    {
        var results = new List<PosAccountingOperationResultDto>();

        foreach (var totalId in request.DocumentIds.Distinct())
        {
            var result = await ExecuteAccountingTransferAsync(
                totalId,
                documentId => SendZReportToErpAsync(documentId, cancellationToken));

            results.Add(result);

            if (!result.Success && !request.ContinueOnError)
            {
                break;
            }
        }

        return CreateBatchResult(ZReportKind, request.DocumentIds.Count, results);
    }

    public async Task<PosAccountingBatchResultDto> DeleteZReportsAsync(
        PosAccountingDeleteRequest request,
        CancellationToken cancellationToken)
    {
        var results = new List<PosAccountingOperationResultDto>();

        foreach (var totalId in request.DocumentIds.Distinct())
        {
            var result = await ExecuteWriteTransactionAsync(async () =>
            {
                var report = await mikroWriteDbContext.ZReportTotals
                    .FirstOrDefaultAsync(item => item.TotalId == totalId, cancellationToken);

                if (report is null)
                {
                    return new PosAccountingOperationResultDto(totalId, null, false, "Z report was not found.");
                }

                var details = await mikroWriteDbContext.ZReportDetails
                    .Where(item => item.TotalId == totalId)
                    .ToArrayAsync(cancellationToken);
                var bankDetails = await mikroWriteDbContext.ZReportBankDetails
                    .Where(item => item.TotalId == totalId)
                    .ToArrayAsync(cancellationToken);

                mikroWriteDbContext.ZReportDetails.RemoveRange(details);
                mikroWriteDbContext.ZReportBankDetails.RemoveRange(bankDetails);
                mikroWriteDbContext.ZReportTotals.Remove(report);
                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);

                return new PosAccountingOperationResultDto(totalId, null, true, "Z report was deleted.");
            });

            results.Add(result);
        }

        return CreateBatchResult(ZReportKind, request.DocumentIds.Count, results);
    }

    public async Task<IReadOnlyCollection<BranchInvoiceListItemDto>> ListPosInvoicesAsync(
        PosAccountingFilterRequest request,
        CancellationToken cancellationToken)
    {
        var query =
            from invoice in ApplyInvoiceFilter(mikroDbContext.Invoices.AsNoTracking(), request)
            join warehouse in mikroDbContext.DEPOLARs.AsNoTracking()
                on (int?)invoice.BranchNo equals warehouse.dep_no into warehouseJoin
            from warehouse in warehouseJoin.DefaultIfEmpty()
            join customer in mikroDbContext.CARI_HESAPLARs.AsNoTracking()
                on invoice.CustomerTaxNo equals customer.cari_vdaire_no into customerJoin
            from customer in customerJoin.DefaultIfEmpty()
            orderby invoice.InvoiceDate, invoice.BranchNo, invoice.DocumentNo
            select new BranchInvoiceListItemDto(
                invoice.InvoiceId,
                invoice.InvoiceGuid,
                invoice.BranchNo,
                warehouse.dep_adi ?? string.Empty,
                invoice.DocumentNo,
                invoice.CustomerTaxNo,
                ((customer.cari_unvan1 ?? string.Empty) + " " + (customer.cari_unvan2 ?? string.Empty)).Trim(),
                invoice.InvoiceDate,
                invoice.PaymentType,
                invoice.InvoiceTotal,
                invoice.IsSent);

        return await query.ToArrayAsync(cancellationToken);
    }

    public async Task<BranchInvoiceDetailDto> GetPosInvoiceDetailAsync(
        int invoiceId,
        CancellationToken cancellationToken)
    {
        var header = await ListPosInvoicesAsync(
            new PosAccountingFilterRequest(null, null, null, false),
            cancellationToken);
        var invoice = header.FirstOrDefault(item => item.InvoiceId == invoiceId);

        if (invoice is null)
        {
            throw new KeyNotFoundException("POS invoice was not found.");
        }

        var lines = await mikroDbContext.InvoiceLines
            .AsNoTracking()
            .Where(item => item.InvoiceId == invoiceId)
            .OrderBy(item => item.TaxRate)
            .Select(item => new BranchInvoiceLineDto(
                item.LineId,
                item.InvoiceId,
                item.TaxRate,
                item.Amount,
                item.TaxAmount))
            .ToArrayAsync(cancellationToken);

        return new BranchInvoiceDetailDto(invoice, lines);
    }

    public async Task<PosAccountingImportResultDto> ImportPosInvoicesAsync(
        ImportPosDocumentsRequest request,
        CancellationToken cancellationToken)
    {
        ValidateImportRequest(request);

        var businessDate = request.BusinessDate.Date;
        var sourceRows = new List<SourcePosDocumentRow>();
        sourceRows.AddRange(await ListFurpaInvoiceSourceRowsAsync(request, cancellationToken));
        sourceRows.AddRange(await ListVeraInvoiceSourceRowsAsync(request, cancellationToken));

        var results = new List<PosAccountingOperationResultDto>();

        foreach (var sourceRow in sourceRows.OrderBy(item => item.Date).ThenBy(item => item.BranchNo))
        {
            try
            {
                var importResult = await ImportPosInvoiceAsync(sourceRow, request.OverwriteExisting, cancellationToken);
                results.Add(importResult);
            }
            catch (Exception exception)
            {
                results.Add(new PosAccountingOperationResultDto(
                    null,
                    sourceRow.Guid,
                    false,
                    exception.Message));
            }
        }

        return CreateImportResult(InvoiceKind, businessDate, results);
    }

    public async Task<PosAccountingBatchResultDto> SendPosInvoicesToErpAsync(
        PosAccountingTransferRequest request,
        CancellationToken cancellationToken)
    {
        var results = new List<PosAccountingOperationResultDto>();

        foreach (var invoiceId in request.DocumentIds.Distinct())
        {
            var result = await ExecuteAccountingTransferAsync(
                invoiceId,
                documentId => SendPosInvoiceToErpAsync(documentId, cancellationToken));

            results.Add(result);

            if (!result.Success && !request.ContinueOnError)
            {
                break;
            }
        }

        return CreateBatchResult(InvoiceKind, request.DocumentIds.Count, results);
    }

    public async Task<BranchInvoiceDetailDto> UpdatePosInvoiceAsync(
        UpdatePosInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        await ExecuteWriteTransactionAsync(async () =>
        {
            var invoice = await mikroWriteDbContext.Invoices
                .FirstOrDefaultAsync(item => item.InvoiceId == request.InvoiceId, cancellationToken);

            if (invoice is null)
            {
                throw new KeyNotFoundException("POS invoice was not found.");
            }

            if (request.DocumentNo.HasValue)
            {
                invoice.DocumentNo = request.DocumentNo.Value;
            }

            if (request.CustomerTaxNo is not null)
            {
                invoice.CustomerTaxNo = NormalizeText(request.CustomerTaxNo);
            }

            if (request.PaymentType is not null)
            {
                invoice.PaymentType = NormalizePaymentType(request.PaymentType);
            }

            await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
            return true;
        });

        return await GetPosInvoiceDetailAsync(request.InvoiceId, cancellationToken);
    }

    public async Task<PosAccountingBatchResultDto> DeletePosInvoicesAsync(
        PosAccountingDeleteRequest request,
        CancellationToken cancellationToken)
    {
        var results = new List<PosAccountingOperationResultDto>();

        foreach (var invoiceId in request.DocumentIds.Distinct())
        {
            var result = await ExecuteWriteTransactionAsync(async () =>
            {
                var invoice = await mikroWriteDbContext.Invoices
                    .FirstOrDefaultAsync(item => item.InvoiceId == invoiceId, cancellationToken);

                if (invoice is null)
                {
                    return new PosAccountingOperationResultDto(invoiceId, null, false, "POS invoice was not found.");
                }

                var lines = await mikroWriteDbContext.InvoiceLines
                    .Where(item => item.InvoiceId == invoiceId)
                    .ToArrayAsync(cancellationToken);

                mikroWriteDbContext.InvoiceLines.RemoveRange(lines);
                mikroWriteDbContext.Invoices.Remove(invoice);
                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);

                return new PosAccountingOperationResultDto(invoiceId, invoice.InvoiceGuid, true, "POS invoice was deleted.");
            });

            results.Add(result);
        }

        return CreateBatchResult(InvoiceKind, request.DocumentIds.Count, results);
    }

    public async Task<IReadOnlyCollection<ExpenseNoteListItemDto>> ListExpenseNotesAsync(
        PosAccountingFilterRequest request,
        CancellationToken cancellationToken)
    {
        var query =
            from expense in ApplyExpenseNoteFilter(mikroDbContext.ExpenseNotes.AsNoTracking(), request)
            join warehouse in mikroDbContext.DEPOLARs.AsNoTracking()
                on (int?)expense.BranchNo equals warehouse.dep_no into warehouseJoin
            from warehouse in warehouseJoin.DefaultIfEmpty()
            orderby expense.ExpenseDate, expense.BranchNo, expense.DocumentNo
            select new ExpenseNoteListItemDto(
                expense.ExpenseId,
                expense.ExpenseGuid,
                expense.DocumentNo,
                expense.BranchNo,
                warehouse.dep_adi ?? string.Empty,
                expense.ExpenseDate,
                expense.PaymentType,
                expense.ExpenseTotal,
                expense.IsSent);

        return await query.ToArrayAsync(cancellationToken);
    }

    public async Task<ExpenseNoteDetailDto> GetExpenseNoteDetailAsync(
        int expenseId,
        CancellationToken cancellationToken)
    {
        var headers = await ListExpenseNotesAsync(
            new PosAccountingFilterRequest(null, null, null, false),
            cancellationToken);
        var expense = headers.FirstOrDefault(item => item.ExpenseId == expenseId);

        if (expense is null)
        {
            throw new KeyNotFoundException("Expense note was not found.");
        }

        var lines = await mikroDbContext.ExpenseNoteLines
            .AsNoTracking()
            .Where(item => item.ExpenseNoteId == expenseId)
            .OrderBy(item => item.TaxRate)
            .Select(item => new ExpenseNoteLineDto(
                item.LineId,
                item.ExpenseNoteId,
                item.TaxRate,
                item.Amount,
                item.TaxAmount))
            .ToArrayAsync(cancellationToken);

        return new ExpenseNoteDetailDto(expense, lines);
    }

    public async Task<PosAccountingImportResultDto> ImportExpenseNotesAsync(
        ImportPosDocumentsRequest request,
        CancellationToken cancellationToken)
    {
        ValidateImportRequest(request);

        var businessDate = request.BusinessDate.Date;
        var sourceRows = await ListFurpaExpenseSourceRowsAsync(request, cancellationToken);
        var results = new List<PosAccountingOperationResultDto>();

        foreach (var sourceRow in sourceRows.OrderBy(item => item.Date).ThenBy(item => item.BranchNo))
        {
            try
            {
                var importResult = await ImportExpenseNoteAsync(sourceRow, request.OverwriteExisting, cancellationToken);
                results.Add(importResult);
            }
            catch (Exception exception)
            {
                results.Add(new PosAccountingOperationResultDto(
                    null,
                    sourceRow.Guid,
                    false,
                    exception.Message));
            }
        }

        return CreateImportResult(ExpenseKind, businessDate, results);
    }

    public async Task<PosAccountingBatchResultDto> SendExpenseNotesToErpAsync(
        PosAccountingTransferRequest request,
        CancellationToken cancellationToken)
    {
        var results = new List<PosAccountingOperationResultDto>();

        foreach (var expenseId in request.DocumentIds.Distinct())
        {
            var result = await ExecuteAccountingTransferAsync(
                expenseId,
                documentId => SendExpenseNoteToErpAsync(documentId, cancellationToken));

            results.Add(result);

            if (!result.Success && !request.ContinueOnError)
            {
                break;
            }
        }

        return CreateBatchResult(ExpenseKind, request.DocumentIds.Count, results);
    }

    public async Task<ExpenseNoteDetailDto> UpdateExpenseNoteAsync(
        UpdateExpenseNoteRequest request,
        CancellationToken cancellationToken)
    {
        await ExecuteWriteTransactionAsync(async () =>
        {
            var expense = await mikroWriteDbContext.ExpenseNotes
                .FirstOrDefaultAsync(item => item.ExpenseId == request.ExpenseId, cancellationToken);

            if (expense is null)
            {
                throw new KeyNotFoundException("Expense note was not found.");
            }

            if (request.BranchNo.HasValue)
            {
                expense.BranchNo = request.BranchNo.Value;
            }

            if (request.DocumentNo is not null)
            {
                expense.DocumentNo = NormalizeText(request.DocumentNo);
            }

            if (request.PaymentType is not null)
            {
                expense.PaymentType = NormalizePaymentType(request.PaymentType);
            }

            await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
            return true;
        });

        return await GetExpenseNoteDetailAsync(request.ExpenseId, cancellationToken);
    }

    public async Task<PosAccountingBatchResultDto> DeleteExpenseNotesAsync(
        PosAccountingDeleteRequest request,
        CancellationToken cancellationToken)
    {
        var results = new List<PosAccountingOperationResultDto>();

        foreach (var expenseId in request.DocumentIds.Distinct())
        {
            var result = await ExecuteWriteTransactionAsync(async () =>
            {
                var expense = await mikroWriteDbContext.ExpenseNotes
                    .FirstOrDefaultAsync(item => item.ExpenseId == expenseId, cancellationToken);

                if (expense is null)
                {
                    return new PosAccountingOperationResultDto(expenseId, null, false, "Expense note was not found.");
                }

                var lines = await mikroWriteDbContext.ExpenseNoteLines
                    .Where(item => item.ExpenseNoteId == expenseId)
                    .ToArrayAsync(cancellationToken);

                mikroWriteDbContext.ExpenseNoteLines.RemoveRange(lines);
                mikroWriteDbContext.ExpenseNotes.Remove(expense);
                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);

                return new PosAccountingOperationResultDto(expenseId, expense.ExpenseGuid, true, "Expense note was deleted.");
            });

            results.Add(result);
        }

        return CreateBatchResult(ExpenseKind, request.DocumentIds.Count, results);
    }

    public async Task<IReadOnlyCollection<CashRegisterBranchMappingDto>> ListCashRegisterMappingsAsync(
        CashRegisterBranchMappingFilterRequest request,
        CancellationToken cancellationToken)
    {
        var query =
            from mapping in mikroDbContext.CashRegisterBranches.AsNoTracking()
            join warehouse in mikroDbContext.DEPOLARs.AsNoTracking()
                on (int?)mapping.BranchNo equals warehouse.dep_no into warehouseJoin
            from warehouse in warehouseJoin.DefaultIfEmpty()
            where (!request.BranchNo.HasValue || mapping.BranchNo == request.BranchNo.Value) &&
                  (string.IsNullOrWhiteSpace(request.CashRegisterNo) || mapping.CashRegisterNo == request.CashRegisterNo.Trim())
            orderby mapping.BranchNo, mapping.CashRegisterNo
            select new CashRegisterBranchMappingDto(
                mapping.Id,
                mapping.CashRegisterNo,
                mapping.BranchNo,
                warehouse.dep_adi ?? string.Empty);

        return await query.ToArrayAsync(cancellationToken);
    }

    public async Task<CashRegisterBranchMappingDto> CreateCashRegisterMappingAsync(
        UpsertCashRegisterBranchMappingRequest request,
        CancellationToken cancellationToken)
    {
        ValidateMappingRequest(request);
        var cashRegisterNo = NormalizeText(request.CashRegisterNo);

        var id = await ExecuteWriteTransactionAsync(async () =>
        {
            var exists = await mikroWriteDbContext.CashRegisterBranches
                .AnyAsync(item => item.CashRegisterNo == cashRegisterNo, cancellationToken);

            if (exists)
            {
                throw new InvalidOperationException("Cash register mapping already exists.");
            }

            var entity = new CashRegisterBranchEntity
            {
                CashRegisterNo = cashRegisterNo,
                BranchNo = request.BranchNo
            };

            await mikroWriteDbContext.CashRegisterBranches.AddAsync(entity, cancellationToken);
            await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
            return entity.Id;
        });

        return await GetCashRegisterMappingAsync(id, cancellationToken);
    }

    public async Task<CashRegisterBranchMappingDto> UpdateCashRegisterMappingAsync(
        UpsertCashRegisterBranchMappingRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.Id.HasValue)
        {
            throw new ArgumentException("Mapping id is required.", nameof(request.Id));
        }

        ValidateMappingRequest(request);
        var cashRegisterNo = NormalizeText(request.CashRegisterNo);

        await ExecuteWriteTransactionAsync(async () =>
        {
            var mapping = await mikroWriteDbContext.CashRegisterBranches
                .FirstOrDefaultAsync(item => item.Id == request.Id.Value, cancellationToken);

            if (mapping is null)
            {
                throw new KeyNotFoundException("Cash register mapping was not found.");
            }

            var duplicateExists = await mikroWriteDbContext.CashRegisterBranches
                .AnyAsync(item => item.Id != mapping.Id && item.CashRegisterNo == cashRegisterNo, cancellationToken);

            if (duplicateExists)
            {
                throw new InvalidOperationException("Cash register mapping already exists.");
            }

            mapping.CashRegisterNo = cashRegisterNo;
            mapping.BranchNo = request.BranchNo;
            await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
            return true;
        });

        return await GetCashRegisterMappingAsync(request.Id.Value, cancellationToken);
    }

    private async Task<PosAccountingOperationResultDto> ExecuteAccountingTransferAsync(
        int documentId,
        Func<int, Task<PosAccountingOperationResultDto>> transfer)
    {
        try
        {
            return await ExecuteWriteTransactionAsync(() => transfer(documentId));
        }
        catch (Exception exception)
        {
            return new PosAccountingOperationResultDto(documentId, null, false, exception.Message);
        }
    }

    private async Task<PosAccountingOperationResultDto> SendZReportToErpAsync(
        int totalId,
        CancellationToken cancellationToken)
    {
        var report = await mikroWriteDbContext.ZReportTotals
            .FirstOrDefaultAsync(item => item.TotalId == totalId, cancellationToken);

        if (report is null)
        {
            return new PosAccountingOperationResultDto(totalId, null, false, "Z report was not found.");
        }

        if (report.IsSent)
        {
            return new PosAccountingOperationResultDto(totalId, null, false, "Z report was already sent to ERP.");
        }

        var details = await mikroWriteDbContext.ZReportDetails
            .Where(item => item.TotalId == totalId)
            .OrderBy(item => item.TaxRate)
            .ToArrayAsync(cancellationToken);

        if (details.Length == 0)
        {
            return new PosAccountingOperationResultDto(totalId, null, false, "Z report has no detail lines.");
        }

        var mapping = await mikroWriteDbContext.CashRegisterBranches
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.CashRegisterNo == report.CashRegisterNo, cancellationToken);

        if (mapping is null)
        {
            return new PosAccountingOperationResultDto(
                totalId,
                null,
                false,
                $"Cash register mapping was not found for '{report.CashRegisterNo}'.");
        }

        var accountingDocument = CreateZReportAccountingDocument(report, details, mapping.BranchNo);
        var identity = await WriteAccountingSlipAsync(accountingDocument, cancellationToken);

        report.IsSent = true;
        await mikroWriteDbContext.SaveChangesAsync(cancellationToken);

        return new PosAccountingOperationResultDto(
            totalId,
            null,
            true,
            $"Z report was sent to ERP. SlipNo={identity.SlipNo}, JournalNo={identity.JournalNo}.");
    }

    private async Task<PosAccountingOperationResultDto> SendPosInvoiceToErpAsync(
        int invoiceId,
        CancellationToken cancellationToken)
    {
        var invoice = await mikroWriteDbContext.Invoices
            .FirstOrDefaultAsync(item => item.InvoiceId == invoiceId, cancellationToken);

        if (invoice is null)
        {
            return new PosAccountingOperationResultDto(invoiceId, null, false, "POS invoice was not found.");
        }

        if (invoice.IsSent)
        {
            return new PosAccountingOperationResultDto(
                invoiceId,
                invoice.InvoiceGuid,
                false,
                "POS invoice was already sent to ERP.");
        }

        var lines = await mikroWriteDbContext.InvoiceLines
            .Where(item => item.InvoiceId == invoiceId)
            .OrderBy(item => item.TaxRate)
            .ToArrayAsync(cancellationToken);

        if (lines.Length == 0)
        {
            return new PosAccountingOperationResultDto(
                invoiceId,
                invoice.InvoiceGuid,
                false,
                "POS invoice has no detail lines.");
        }

        var customer = await GetCustomerAccountingInfoAsync(invoice.CustomerTaxNo, cancellationToken);
        var accountingDocument = CreatePosInvoiceAccountingDocument(invoice, lines, customer);
        var identity = await WriteAccountingSlipAsync(accountingDocument, cancellationToken);

        invoice.IsSent = true;
        await mikroWriteDbContext.SaveChangesAsync(cancellationToken);

        return new PosAccountingOperationResultDto(
            invoiceId,
            invoice.InvoiceGuid,
            true,
            $"POS invoice was sent to ERP. SlipNo={identity.SlipNo}, JournalNo={identity.JournalNo}.");
    }

    private async Task<PosAccountingOperationResultDto> SendExpenseNoteToErpAsync(
        int expenseId,
        CancellationToken cancellationToken)
    {
        var expense = await mikroWriteDbContext.ExpenseNotes
            .FirstOrDefaultAsync(item => item.ExpenseId == expenseId, cancellationToken);

        if (expense is null)
        {
            return new PosAccountingOperationResultDto(expenseId, null, false, "Expense note was not found.");
        }

        if (expense.IsSent)
        {
            return new PosAccountingOperationResultDto(
                expenseId,
                expense.ExpenseGuid,
                false,
                "Expense note was already sent to ERP.");
        }

        var lines = await mikroWriteDbContext.ExpenseNoteLines
            .Where(item => item.ExpenseNoteId == expenseId)
            .OrderBy(item => item.TaxRate)
            .ToArrayAsync(cancellationToken);

        if (lines.Length == 0)
        {
            return new PosAccountingOperationResultDto(
                expenseId,
                expense.ExpenseGuid,
                false,
                "Expense note has no detail lines.");
        }

        var accountingDocument = CreateExpenseNoteAccountingDocument(expense, lines);
        var identity = await WriteAccountingSlipAsync(accountingDocument, cancellationToken);

        expense.IsSent = true;
        await mikroWriteDbContext.SaveChangesAsync(cancellationToken);

        return new PosAccountingOperationResultDto(
            expenseId,
            expense.ExpenseGuid,
            true,
            $"Expense note was sent to ERP. SlipNo={identity.SlipNo}, JournalNo={identity.JournalNo}.");
    }

    private static AccountingSlipDocument CreateZReportAccountingDocument(
        ZReportTotalEntity report,
        IReadOnlyCollection<ZReportDetailEntity> details,
        int branchNo)
    {
        var lines = new List<AccountingSlipLine>();
        AddAccountingLine(lines, CashAccountCode, ToMoney(report.CashPaymentTotal));
        AddAccountingLine(lines, CreditAccountCode, ToMoney(report.CreditCardPaymentTotal));

        foreach (var detail in details)
        {
            var taxRate = Convert.ToInt32(detail.TaxRate, CultureInfo.InvariantCulture);
            var taxAmount = ToMoney(detail.BillTaxTotal);
            var amountExcludingTax = ToMoney(detail.BillTotal - detail.BillTaxTotal);

            AddAccountingLine(lines, CreateSalesRevenueAccountCode(taxRate), -amountExcludingTax);
            AddAccountingLine(lines, CreateSalesTaxAccountCode(taxRate), -taxAmount, -amountExcludingTax);
        }

        var balancedLines = BalanceAccountingLines(lines);
        var documentNumber = report.BillNo > 0 ? report.BillNo : report.ZNo;

        return new AccountingSlipDocument(
            report.Date.Date,
            ZReportDescription,
            branchNo.ToString(CultureInfo.InvariantCulture),
            null,
            string.Empty,
            documentNumber,
            report.ZNo.ToString(CultureInfo.InvariantCulture),
            ToMoney(report.CashPaymentTotal + report.CreditCardPaymentTotal),
            details.Sum(item => ToMoney(item.BillTaxTotal)),
            CustomerAccountingInfo.Empty,
            5,
            0,
            0,
            ZReportOffsetType,
            balancedLines);
    }

    private static AccountingSlipDocument CreatePosInvoiceAccountingDocument(
        BranchInvoiceEntity invoice,
        IReadOnlyCollection<BranchInvoiceLineEntity> invoiceLines,
        CustomerAccountingInfo customer)
    {
        var lines = new List<AccountingSlipLine>
        {
            new(ResolvePaymentAccountCode(invoice.PaymentType), ToMoney(invoice.InvoiceTotal))
        };

        foreach (var invoiceLine in invoiceLines)
        {
            var taxRate = Convert.ToInt32(invoiceLine.TaxRate, CultureInfo.InvariantCulture);
            var amount = ToMoney(invoiceLine.Amount);
            var taxAmount = ToMoney(invoiceLine.TaxAmount);

            AddAccountingLine(lines, CreateSalesRevenueAccountCode(taxRate), -amount);
            AddAccountingLine(lines, CreateSalesTaxAccountCode(taxRate), -taxAmount, -amount);
        }

        var documentNo = invoice.DocumentNo > 0
            ? invoice.DocumentNo.ToString(CultureInfo.InvariantCulture)
            : invoice.InvoiceId.ToString(CultureInfo.InvariantCulture);

        return new AccountingSlipDocument(
            invoice.InvoiceDate.Date,
            "POS FATURASI",
            invoice.BranchNo.ToString(CultureInfo.InvariantCulture),
            invoice.InvoiceGuid,
            string.Empty,
            invoice.DocumentNo,
            documentNo,
            ToMoney(invoice.InvoiceTotal),
            invoiceLines.Sum(item => ToMoney(item.TaxAmount)),
            customer,
            1,
            0,
            0,
            DefaultOffsetType,
            BalanceAccountingLines(lines));
    }

    private static AccountingSlipDocument CreateExpenseNoteAccountingDocument(
        ExpenseNoteEntity expense,
        IReadOnlyCollection<ExpenseNoteLineEntity> expenseLines)
    {
        var lines = new List<AccountingSlipLine>
        {
            new(ResolvePaymentAccountCode(expense.PaymentType), -ToMoney(expense.ExpenseTotal))
        };

        foreach (var expenseLine in expenseLines)
        {
            var taxRate = Convert.ToInt32(expenseLine.TaxRate, CultureInfo.InvariantCulture);
            var amount = ToMoney(expenseLine.Amount);
            var taxAmount = ToMoney(expenseLine.TaxAmount);

            AddAccountingLine(lines, CreateExpenseRevenueAccountCode(taxRate), amount);
            AddAccountingLine(lines, CreateExpenseTaxAccountCode(taxRate), taxAmount, amount);
        }

        var documentNumber = ParseDocumentNo(expense.DocumentNo);
        var documentNo = string.IsNullOrWhiteSpace(expense.DocumentNo)
            ? expense.ExpenseId.ToString(CultureInfo.InvariantCulture)
            : expense.DocumentNo;

        return new AccountingSlipDocument(
            expense.ExpenseDate.Date,
            "GIDER PUSULASI",
            expense.BranchNo.ToString(CultureInfo.InvariantCulture),
            expense.ExpenseGuid,
            string.Empty,
            documentNumber,
            documentNo,
            ToMoney(expense.ExpenseTotal),
            expenseLines.Sum(item => ToMoney(item.TaxAmount)),
            CustomerAccountingInfo.Empty,
            2,
            0,
            0,
            DefaultOffsetType,
            BalanceAccountingLines(lines));
    }

    private async Task<AccountingSlipIdentity> WriteAccountingSlipAsync(
        AccountingSlipDocument document,
        CancellationToken cancellationToken)
    {
        if (document.Lines.Count == 0)
        {
            throw new InvalidOperationException("Accounting slip has no lines.");
        }

        var slipNo = await GetNextAccountingSlipNoAsync(document.DocumentDate, cancellationToken);
        var journalNo = await GetNextAccountingJournalNoAsync(cancellationToken);

        await InsertAccountingSlipDetailAsync(document, cancellationToken);

        var lineNo = 0;
        foreach (var line in document.Lines)
        {
            await InsertAccountingSlipLineAsync(document, line, slipNo, journalNo, lineNo, cancellationToken);
            lineNo++;
        }

        return new AccountingSlipIdentity(slipNo, journalNo);
    }

    private async Task<int> GetNextAccountingSlipNoAsync(
        DateTime documentDate,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT ISNULL(MAX(fis_sira_no), 0) + 1
            FROM dbo.MUHASEBE_FISLERI WITH (UPDLOCK, HOLDLOCK)
            WHERE fis_tarih >= @date AND fis_tarih < @dateEnd
            """;

        return await ExecuteScalarOnMikroWriteAsync<int>(
            sql,
            command =>
            {
                AddParameter(command, "@date", documentDate.Date);
                AddParameter(command, "@dateEnd", documentDate.Date.AddDays(1));
            },
            cancellationToken);
    }

    private async Task<int> GetNextAccountingJournalNoAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT ISNULL(MAX(fis_yevmiye_no), 0) + 1
            FROM dbo.MUHASEBE_FISLERI WITH (UPDLOCK, HOLDLOCK)
            """;

        return await ExecuteScalarOnMikroWriteAsync<int>(sql, _ => { }, cancellationToken);
    }

    private Task InsertAccountingSlipDetailAsync(
        AccountingSlipDocument document,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.MUHASEBE_FIS_DETAYLARI (
                mfd_ticari_tip,
                mfd_evraktip,
                mfd_evrak_seri,
                mfd_evrak_sira,
                mfd_cariunvan,
                mfd_carivergidaireadi,
                mfd_carivergidaireno,
                mfd_belgetarihi,
                mfd_tutarnereden,
                mfd_caritipi,
                mfd_carikodu,
                mfd_carimuhkodu,
                mfd_belgeno,
                mfd_kdvid,
                mfd_kdvtutar,
                mfd_caritutar,
                mfd_aciklama,
                mfd_kisaevraktipi,
                mfd_satistipi,
                mfd_alistipi,
                mfd_tahtedtipi,
                mfd_evraktur,
                mfd_e_belgemi)
            VALUES (
                @commercialType,
                @documentType,
                @documentSeries,
                @documentNumber,
                @customerName,
                @customerTaxOffice,
                @customerTaxNo,
                @documentDate,
                @amountSource,
                @customerType,
                @customerCode,
                @customerAccountingCode,
                @externalDocumentNo,
                @taxId,
                @taxAmount,
                @totalAmount,
                @description,
                @shortDocumentType,
                @salesType,
                @purchaseType,
                @commitmentType,
                @documentKind,
                @isElectronic)
            """;

        return ExecuteNonQueryOnMikroWriteAsync(
            sql,
            command =>
            {
                AddParameter(command, "@commercialType", CommercialAccountingType);
                AddParameter(command, "@documentType", CommercialDocumentType);
                AddParameter(command, "@documentSeries", LimitText(document.DocumentSeries, 20));
                AddParameter(command, "@documentNumber", document.DocumentNumber);
                AddParameter(command, "@customerName", LimitText(document.Customer.Name, 127));
                AddParameter(command, "@customerTaxOffice", LimitText(document.Customer.TaxOffice, 50));
                AddParameter(command, "@customerTaxNo", LimitText(document.Customer.TaxNo, 15));
                AddParameter(command, "@documentDate", document.DocumentDate);
                AddParameter(command, "@amountSource", string.IsNullOrWhiteSpace(document.Customer.Code) ? 0 : 5);
                AddParameter(command, "@customerType", string.IsNullOrWhiteSpace(document.Customer.Code) ? 0 : 2);
                AddParameter(command, "@customerCode", LimitText(document.Customer.Code, 25));
                AddParameter(command, "@customerAccountingCode", LimitText(document.Customer.AccountingCode, 25));
                AddParameter(command, "@externalDocumentNo", LimitText(document.ExternalDocumentNo, 50));
                AddParameter(command, "@taxId", 0);
                AddParameter(command, "@taxAmount", ToDouble(document.TaxAmount));
                AddParameter(command, "@totalAmount", ToDouble(document.TotalAmount));
                AddParameter(command, "@description", LimitText(document.Description, 127));
                AddParameter(command, "@shortDocumentType", document.ShortDocumentType);
                AddParameter(command, "@salesType", document.SalesType);
                AddParameter(command, "@purchaseType", document.PurchaseType);
                AddParameter(command, "@commitmentType", 0);
                AddParameter(command, "@documentKind", 0);
                AddParameter(command, "@isElectronic", false);
            },
            cancellationToken);
    }

    private Task InsertAccountingSlipLineAsync(
        AccountingSlipDocument document,
        AccountingSlipLine line,
        int slipNo,
        int journalNo,
        int lineNo,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.MUHASEBE_FISLERI (
                fis_firmano,
                fis_subeno,
                fis_maliyil,
                fis_tarih,
                fis_sira_no,
                fis_tur,
                fis_hesap_kod,
                fis_satir_no,
                fis_aciklama1,
                fis_meblag0,
                fis_meblag1,
                fis_meblag2,
                fis_meblag3,
                fis_meblag4,
                fis_meblag5,
                fis_meblag6,
                fis_sorumluluk_kodu,
                fis_ticari_tip,
                fis_ticari_uid,
                fis_kurfarkifl,
                fis_ticari_evraktip,
                fis_tic_evrak_seri,
                fis_tic_evrak_sira,
                fis_tic_belgeno,
                fis_tic_belgetarihi,
                fis_yevmiye_no,
                fis_katagori,
                fis_fmahsup_tipi,
                fis_aktif_pasif)
            VALUES (
                @companyNo,
                @branchNo,
                @fiscalYear,
                @documentDate,
                @slipNo,
                @lineType,
                @accountCode,
                @lineNo,
                @description,
                @amount,
                @alternateAmount,
                @amount,
                @quantity,
                @taxBase,
                @measure1,
                @measure2,
                @responsibilityCode,
                @commercialType,
                @sourceGuid,
                @isExchangeDifference,
                @documentType,
                @documentSeries,
                @documentNumber,
                @externalDocumentNo,
                @documentDate,
                @journalNo,
                @category,
                @offsetType,
                @activePassive)
            """;

        return ExecuteNonQueryOnMikroWriteAsync(
            sql,
            command =>
            {
                AddParameter(command, "@companyNo", 0);
                AddParameter(command, "@branchNo", 0);
                AddParameter(command, "@fiscalYear", document.DocumentDate.Year);
                AddParameter(command, "@documentDate", document.DocumentDate);
                AddParameter(command, "@slipNo", slipNo);
                AddParameter(command, "@lineType", AccountingLineType);
                AddParameter(command, "@accountCode", LimitText(line.AccountCode, 25));
                AddParameter(command, "@lineNo", lineNo);
                AddParameter(command, "@description", LimitText(document.Description, 127));
                AddParameter(command, "@amount", ToDouble(line.Amount));
                AddParameter(command, "@alternateAmount", 0d);
                AddParameter(command, "@quantity", 0d);
                AddParameter(command, "@taxBase", ToDouble(line.TaxBase));
                AddParameter(command, "@measure1", 0d);
                AddParameter(command, "@measure2", 0d);
                AddParameter(command, "@responsibilityCode", LimitText(document.ResponsibilityCode, 25));
                AddParameter(command, "@commercialType", CommercialAccountingType);
                AddParameter(command, "@sourceGuid", document.SourceGuid ?? Guid.Empty);
                AddParameter(command, "@isExchangeDifference", false);
                AddParameter(command, "@documentType", CommercialDocumentType);
                AddParameter(command, "@documentSeries", LimitText(document.DocumentSeries, 20));
                AddParameter(command, "@documentNumber", document.DocumentNumber);
                AddParameter(command, "@externalDocumentNo", LimitText(document.ExternalDocumentNo, 50));
                AddParameter(command, "@journalNo", journalNo);
                AddParameter(command, "@category", AccountingCategory);
                AddParameter(command, "@offsetType", document.OffsetType);
                AddParameter(command, "@activePassive", 0);
            },
            cancellationToken);
    }

    private async Task<CustomerAccountingInfo> GetCustomerAccountingInfoAsync(
        string taxNo,
        CancellationToken cancellationToken)
    {
        var normalizedTaxNo = NormalizeText(taxNo);
        if (string.IsNullOrWhiteSpace(normalizedTaxNo))
        {
            return CustomerAccountingInfo.Empty;
        }

        var customer = await mikroWriteDbContext.CARI_HESAPLARs
            .AsNoTracking()
            .Where(item =>
                item.cari_vdaire_no == normalizedTaxNo ||
                item.cari_VergiKimlikNo == normalizedTaxNo ||
                item.cari_kod == normalizedTaxNo)
            .Select(item => new CustomerAccountingInfo(
                ((item.cari_unvan1 ?? string.Empty) + " " + (item.cari_unvan2 ?? string.Empty)).Trim(),
                item.cari_vdaire_adi ?? string.Empty,
                item.cari_vdaire_no ?? normalizedTaxNo,
                item.cari_kod ?? normalizedTaxNo,
                item.cari_muh_kod ?? string.Empty))
            .FirstOrDefaultAsync(cancellationToken);

        return customer ?? new CustomerAccountingInfo(
            string.Empty,
            string.Empty,
            normalizedTaxNo,
            normalizedTaxNo,
            string.Empty);
    }

    private async Task<PosAccountingOperationResultDto> ImportPosInvoiceAsync(
        SourcePosDocumentRow sourceRow,
        bool overwriteExisting,
        CancellationToken cancellationToken)
    {
        var calculatedLines = sourceRow.Source == SourceSystem.Vera
            ? await GetVeraCalculatedInvoiceLinesAsync(sourceRow.Guid, cancellationToken)
            : await GetFurpaCalculatedLinesAsync(sourceRow.Guid, cancellationToken);

        if (calculatedLines.Count == 0)
        {
            return new PosAccountingOperationResultDto(
                null,
                sourceRow.Guid,
                false,
                "POS invoice line total could not be calculated.");
        }

        return await ExecuteWriteTransactionAsync(async () =>
        {
            var existing = await mikroWriteDbContext.Invoices
                .FirstOrDefaultAsync(item => item.InvoiceGuid == sourceRow.Guid, cancellationToken);

            if (existing is not null && !overwriteExisting)
            {
                return new PosAccountingOperationResultDto(
                    existing.InvoiceId,
                    sourceRow.Guid,
                    false,
                    "POS invoice was already imported.");
            }

            if (existing is not null)
            {
                var existingLines = await mikroWriteDbContext.InvoiceLines
                    .Where(item => item.InvoiceId == existing.InvoiceId)
                    .ToArrayAsync(cancellationToken);

                mikroWriteDbContext.InvoiceLines.RemoveRange(existingLines);
                mikroWriteDbContext.Invoices.Remove(existing);
                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
            }

            var invoice = new BranchInvoiceEntity
            {
                InvoiceGuid = sourceRow.Guid,
                BranchNo = ParseBranchNo(sourceRow.BranchNo),
                DocumentNo = ParseDocumentNo(sourceRow.DocumentNo),
                CustomerTaxNo = NormalizeText(sourceRow.CustomerTaxNo),
                InvoiceDate = sourceRow.Date,
                PaymentType = ResolvePaymentType(sourceRow.CardNumber),
                InvoiceTotal = sourceRow.Total,
                IsSent = false
            };

            await mikroWriteDbContext.Invoices.AddAsync(invoice, cancellationToken);
            await mikroWriteDbContext.SaveChangesAsync(cancellationToken);

            var lines = calculatedLines.Select(line => new BranchInvoiceLineEntity
            {
                InvoiceId = invoice.InvoiceId,
                TaxRate = Convert.ToInt16(line.TaxRate, CultureInfo.InvariantCulture),
                Amount = line.TotalExcludingTax,
                TaxAmount = line.TotalTax
            });

            await mikroWriteDbContext.InvoiceLines.AddRangeAsync(lines, cancellationToken);
            await mikroWriteDbContext.SaveChangesAsync(cancellationToken);

            return new PosAccountingOperationResultDto(
                invoice.InvoiceId,
                invoice.InvoiceGuid,
                true,
                "POS invoice was imported.");
        });
    }

    private async Task<PosAccountingOperationResultDto> ImportExpenseNoteAsync(
        SourcePosDocumentRow sourceRow,
        bool overwriteExisting,
        CancellationToken cancellationToken)
    {
        var calculatedLines = await GetFurpaCalculatedLinesAsync(sourceRow.Guid, cancellationToken);

        if (calculatedLines.Count == 0)
        {
            return new PosAccountingOperationResultDto(
                null,
                sourceRow.Guid,
                false,
                "Expense note line total could not be calculated.");
        }

        return await ExecuteWriteTransactionAsync(async () =>
        {
            var existing = await mikroWriteDbContext.ExpenseNotes
                .FirstOrDefaultAsync(item => item.ExpenseGuid == sourceRow.Guid, cancellationToken);

            if (existing is not null && !overwriteExisting)
            {
                return new PosAccountingOperationResultDto(
                    existing.ExpenseId,
                    sourceRow.Guid,
                    false,
                    "Expense note was already imported.");
            }

            if (existing is not null)
            {
                var existingLines = await mikroWriteDbContext.ExpenseNoteLines
                    .Where(item => item.ExpenseNoteId == existing.ExpenseId)
                    .ToArrayAsync(cancellationToken);

                mikroWriteDbContext.ExpenseNoteLines.RemoveRange(existingLines);
                mikroWriteDbContext.ExpenseNotes.Remove(existing);
                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
            }

            var expenseNote = new ExpenseNoteEntity
            {
                ExpenseGuid = sourceRow.Guid,
                BranchNo = ParseBranchNo(sourceRow.BranchNo),
                DocumentNo = NormalizeText(sourceRow.DocumentNo),
                ExpenseDate = sourceRow.Date,
                PaymentType = ResolvePaymentType(sourceRow.CardNumber),
                ExpenseTotal = sourceRow.Total,
                IsSent = false
            };

            await mikroWriteDbContext.ExpenseNotes.AddAsync(expenseNote, cancellationToken);
            await mikroWriteDbContext.SaveChangesAsync(cancellationToken);

            var lines = calculatedLines.Select(line => new ExpenseNoteLineEntity
            {
                ExpenseNoteId = expenseNote.ExpenseId,
                TaxRate = Convert.ToInt16(line.TaxRate, CultureInfo.InvariantCulture),
                Amount = line.TotalExcludingTax,
                TaxAmount = line.TotalTax
            });

            await mikroWriteDbContext.ExpenseNoteLines.AddRangeAsync(lines, cancellationToken);
            await mikroWriteDbContext.SaveChangesAsync(cancellationToken);

            return new PosAccountingOperationResultDto(
                expenseNote.ExpenseId,
                expenseNote.ExpenseGuid,
                true,
                "Expense note was imported.");
        });
    }

    private async Task<IReadOnlyCollection<SourcePosDocumentRow>> ListFurpaInvoiceSourceRowsAsync(
        ImportPosDocumentsRequest request,
        CancellationToken cancellationToken)
    {
        var targetDatabase = QuoteSqlIdentifier(GetMikroTargetDatabaseName());
        var maydayConnectionString = configuration.GetConnectionString("MaydayConnection");
        var sourceTable = string.IsNullOrWhiteSpace(maydayConnectionString)
            ? "dbo.PosFaturas"
            : $"{QuoteSqlIdentifier(GetFurpaSourceDatabaseName())}.dbo.PosFaturas";
        var sql = $"""
            SELECT
                FaturaGuid,
                Sube,
                Tarih,
                CAST(BelgeTuru AS nvarchar(20)) AS BelgeTuru,
                CAST(BelgeTuru AS nvarchar(20)) AS BelgeTipi,
                CAST('' AS nvarchar(50)) AS FaturaVergiNo,
                CAST(FisNo AS nvarchar(50)) AS BelgeNo,
                KasaNo,
                KartNumarasi,
                CAST(Toplam AS decimal(18,2)) AS Toplam
            FROM {sourceTable} WITH (NOLOCK)
            WHERE (@includePreviouslyImported = 1 OR FaturaGuid NOT IN (
                SELECT InvoiceGuid
                FROM {targetDatabase}.dbo.Invoices WITH (NOLOCK)
            ))
              AND BelgeTuru = 2
              AND Tarih >= @date
              AND (@warehouseNo IS NULL OR TRY_CONVERT(int, Sube) = @warehouseNo)
            """;

        if (string.IsNullOrWhiteSpace(maydayConnectionString))
        {
            return await ExecuteReaderAsync(
                furpaDbContext.Database.GetDbConnection(),
                sql,
                command =>
                {
                    AddParameter(command, "@includePreviouslyImported", request.IncludePreviouslyImported);
                    AddParameter(command, "@date", request.BusinessDate.Date);
                    AddParameter(command, "@warehouseNo", request.WarehouseNo);
                },
                reader => ReadSourceDocumentRow(reader, SourceSystem.Furpa),
                cancellationToken);
        }

        await using var connection = new SqlConnection(maydayConnectionString);
        return await ExecuteReaderAsync(
            connection,
            sql,
            command =>
            {
                AddParameter(command, "@includePreviouslyImported", request.IncludePreviouslyImported);
                AddParameter(command, "@date", request.BusinessDate.Date);
                AddParameter(command, "@warehouseNo", request.WarehouseNo);
            },
            reader => ReadSourceDocumentRow(reader, SourceSystem.Furpa),
            cancellationToken);
    }

    private async Task<IReadOnlyCollection<SourcePosDocumentRow>> ListFurpaExpenseSourceRowsAsync(
        ImportPosDocumentsRequest request,
        CancellationToken cancellationToken)
    {
        var targetDatabase = QuoteSqlIdentifier(GetMikroTargetDatabaseName());
        var sql = $"""
            SELECT
                FaturaGuid,
                Sube,
                Tarih,
                CAST(BelgeTuru AS nvarchar(20)) AS BelgeTuru,
                CAST(BelgeTuru AS nvarchar(20)) AS BelgeTipi,
                CAST('' AS nvarchar(50)) AS FaturaVergiNo,
                CAST(FisNo AS nvarchar(50)) AS BelgeNo,
                KasaNo,
                KartNumarasi,
                CAST((Toplam - FaturaIndirimi + ToplamKdv) AS decimal(18,2)) AS Toplam
            FROM dbo.PosFaturas WITH (NOLOCK)
            WHERE (@includePreviouslyImported = 1 OR FaturaGuid NOT IN (
                SELECT ExpenseGuid
                FROM {targetDatabase}.dbo.ExpenseNotes WITH (NOLOCK)
            ))
              AND BelgeTuru = 4
              AND Tarih >= @date
              AND (@warehouseNo IS NULL OR TRY_CONVERT(int, Sube) = @warehouseNo)
            """;

        return await ExecuteReaderAsync(
            furpaDbContext.Database.GetDbConnection(),
            sql,
            command =>
            {
                AddParameter(command, "@includePreviouslyImported", request.IncludePreviouslyImported);
                AddParameter(command, "@date", request.BusinessDate.Date);
                AddParameter(command, "@warehouseNo", request.WarehouseNo);
            },
            reader => ReadSourceDocumentRow(reader, SourceSystem.Furpa),
            cancellationToken);
    }

    private async Task<IReadOnlyCollection<SourcePosDocumentRow>> ListVeraInvoiceSourceRowsAsync(
        ImportPosDocumentsRequest request,
        CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString("VeraConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return Array.Empty<SourcePosDocumentRow>();
        }

        var targetDatabase = QuoteSqlIdentifier(GetMikroTargetDatabaseName());
        var sql = $"""
            SELECT
                FATURA_GUID AS FaturaGuid,
                SUBE AS Sube,
                TARIH AS Tarih,
                BELGE_TURU AS BelgeTuru,
                BELGE_TIPI AS BelgeTipi,
                FATURA_VN AS FaturaVergiNo,
                BELGE_NO AS BelgeNo,
                KASA_RG_KODU AS KasaNo,
                KART_NUMARASI AS KartNumarasi,
                CAST(TOPLAM AS decimal(18,2)) AS Toplam
            FROM dbo.FATURA WITH (NOLOCK)
            WHERE (@includePreviouslyImported = 1 OR FATURA_GUID NOT IN (
                SELECT InvoiceGuid
                FROM {targetDatabase}.dbo.Invoices WITH (NOLOCK)
            ))
              AND BELGE_TIPI = 'FATURA'
              AND BELGE_TURU = 'FATURA'
              AND TARIH >= @date
              AND (@warehouseNo IS NULL OR TRY_CONVERT(int, SUBE) = @warehouseNo)
            """;

        await using var connection = new SqlConnection(connectionString);
        return await ExecuteReaderAsync(
            connection,
            sql,
            command =>
            {
                AddParameter(command, "@includePreviouslyImported", request.IncludePreviouslyImported);
                AddParameter(command, "@date", request.BusinessDate.Date);
                AddParameter(command, "@warehouseNo", request.WarehouseNo);
            },
            reader => ReadSourceDocumentRow(reader, SourceSystem.Vera),
            cancellationToken);
    }

    private async Task<IReadOnlyCollection<CalculatedPosLine>> GetFurpaCalculatedLinesAsync(
        Guid guid,
        CancellationToken cancellationToken)
    {
        var targetDatabase = QuoteSqlIdentifier(GetMikroTargetDatabaseName());
        var sql = $"""
            SELECT
                CASE stock.sto_perakende_vergi
                    WHEN 1 THEN 18
                    WHEN 2 THEN 8
                    WHEN 3 THEN 1
                    WHEN 4 THEN 0
                    ELSE 0
                END AS TaxRate,
                CAST(SUM(line.NetTutar) AS decimal(18,2)) AS TotalExcludingTax,
                CAST(SUM(line.KdvTutari) AS decimal(18,2)) AS TotalTax
            FROM dbo.PosFaturaSatirs AS line WITH (NOLOCK)
            INNER JOIN {targetDatabase}.dbo.STOKLAR AS stock WITH (NOLOCK)
                ON stock.sto_kod = line.UrunKodu
            WHERE line.FaturaGuid = @guid
            GROUP BY stock.sto_perakende_vergi
            """;

        return await ExecuteReaderAsync(
            furpaDbContext.Database.GetDbConnection(),
            sql,
            command => AddParameter(command, "@guid", guid),
            reader => new CalculatedPosLine(
                ReadInt(reader, "TaxRate"),
                ReadDecimal(reader, "TotalExcludingTax"),
                ReadDecimal(reader, "TotalTax")),
            cancellationToken);
    }

    private async Task<IReadOnlyCollection<CalculatedPosLine>> GetVeraCalculatedInvoiceLinesAsync(
        Guid guid,
        CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString("VeraConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return Array.Empty<CalculatedPosLine>();
        }

        var rows = await ReadSqlRowsAsync(
            connectionString,
            "SELECT * FROM dbo.FATURA_SATIRLAR WITH (NOLOCK) WHERE FATURA_GUID = @guid",
            command => AddParameter(command, "@guid", guid),
            cancellationToken);

        return rows
            .Select(row =>
            {
                var taxRate = ReadInt(row, "TaxRate", "KDV_ORANI", "KDVORANI", "KdvOrani", "VERGI_ORANI", "VERGI");
                var lineTotal = ReadDecimal(row, "LineTotal", "LINE_TOTAL", "SATIR_TOPLAMI", "SATIR_TOPLAM", "TOPLAM", "TUTAR");
                var totalExcludingTax = CalculateVeraTotalExcludingTax(taxRate, lineTotal);

                return new CalculatedPosLine(
                    taxRate,
                    totalExcludingTax,
                    Math.Round(lineTotal - totalExcludingTax, 2, MidpointRounding.AwayFromZero));
            })
            .Where(item => item.TotalExcludingTax != 0m || item.TotalTax != 0m)
            .GroupBy(item => item.TaxRate)
            .Select(grouped => new CalculatedPosLine(
                grouped.Key,
                grouped.Sum(item => item.TotalExcludingTax),
                grouped.Sum(item => item.TotalTax)))
            .OrderBy(item => item.TaxRate)
            .ToArray();
    }

    private async Task<CashRegisterBranchMappingDto> GetCashRegisterMappingAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var item = await ListCashRegisterMappingsAsync(
            new CashRegisterBranchMappingFilterRequest(null, null),
            cancellationToken);

        return item.FirstOrDefault(mapping => mapping.Id == id)
               ?? throw new KeyNotFoundException("Cash register mapping was not found.");
    }

    private async Task<T> ExecuteWriteTransactionAsync<T>(Func<Task<T>> action)
    {
        var strategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                var result = await action();
                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    private async Task<T> ExecuteScalarOnMikroWriteAsync<T>(
        string sql,
        Action<DbCommand> configureCommand,
        CancellationToken cancellationToken)
    {
        var connection = mikroWriteDbContext.Database.GetDbConnection();
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
            command.Transaction = mikroWriteDbContext.Database.CurrentTransaction?.GetDbTransaction();
            configureCommand(command);

            var value = await command.ExecuteScalarAsync(cancellationToken);
            if (value is null or DBNull)
            {
                throw new InvalidOperationException("SQL scalar command returned null.");
            }

            return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture)!;
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task ExecuteNonQueryOnMikroWriteAsync(
        string sql,
        Action<DbCommand> configureCommand,
        CancellationToken cancellationToken)
    {
        var connection = mikroWriteDbContext.Database.GetDbConnection();
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
            command.Transaction = mikroWriteDbContext.Database.CurrentTransaction?.GetDbTransaction();
            configureCommand(command);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static IQueryable<ZReportTotalEntity> ApplyZReportFilter(
        IQueryable<ZReportTotalEntity> query,
        PosAccountingFilterRequest request)
    {
        if (request.OnlyPending)
        {
            query = query.Where(item => !item.IsSent);
        }

        if (request.StartDate.HasValue)
        {
            var startDate = request.StartDate.Value.Date;
            query = query.Where(item => item.Date >= startDate);
        }

        if (request.EndDate.HasValue)
        {
            var endDateExclusive = request.EndDate.Value.Date.AddDays(1);
            query = query.Where(item => item.Date < endDateExclusive);
        }

        return query;
    }

    private IQueryable<BranchInvoiceEntity> ApplyInvoiceFilter(
        IQueryable<BranchInvoiceEntity> query,
        PosAccountingFilterRequest request)
    {
        if (request.OnlyPending)
        {
            query = query.Where(item => !item.IsSent);
        }

        if (request.WarehouseNo.HasValue)
        {
            query = query.Where(item => item.BranchNo == request.WarehouseNo.Value);
        }

        if (request.StartDate.HasValue)
        {
            var startDate = request.StartDate.Value.Date;
            query = query.Where(item => item.InvoiceDate >= startDate);
        }

        if (request.EndDate.HasValue)
        {
            var endDateExclusive = request.EndDate.Value.Date.AddDays(1);
            query = query.Where(item => item.InvoiceDate < endDateExclusive);
        }

        return query;
    }

    private IQueryable<ExpenseNoteEntity> ApplyExpenseNoteFilter(
        IQueryable<ExpenseNoteEntity> query,
        PosAccountingFilterRequest request)
    {
        if (request.OnlyPending)
        {
            query = query.Where(item => !item.IsSent);
        }

        if (request.WarehouseNo.HasValue)
        {
            query = query.Where(item => item.BranchNo == request.WarehouseNo.Value);
        }

        if (request.StartDate.HasValue)
        {
            var startDate = request.StartDate.Value.Date;
            query = query.Where(item => item.ExpenseDate >= startDate);
        }

        if (request.EndDate.HasValue)
        {
            var endDateExclusive = request.EndDate.Value.Date.AddDays(1);
            query = query.Where(item => item.ExpenseDate < endDateExclusive);
        }

        return query;
    }

    private async Task<IReadOnlyCollection<T>> ExecuteReaderAsync<T>(
        DbConnection connection,
        string sql,
        Action<DbCommand> configureCommand,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken)
    {
        var items = new List<T>();
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

    private static async Task<IReadOnlyCollection<IReadOnlyDictionary<string, object?>>> ReadSqlRowsAsync(
        string connectionString,
        string sql,
        Action<DbCommand> configureCommand,
        CancellationToken cancellationToken)
    {
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 180;
        configureCommand(command);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                row[reader.GetName(index)] = await reader.IsDBNullAsync(index, cancellationToken)
                    ? null
                    : reader.GetValue(index);
            }

            rows.Add(row);
        }

        return rows;
    }

    private string GetMikroTargetDatabaseName()
    {
        var databaseName = mikroWriteDbContext.Database.GetDbConnection().Database;
        return string.IsNullOrWhiteSpace(databaseName)
            ? "MikroDB_V16_FURPA_2024"
            : databaseName;
    }

    private string GetFurpaSourceDatabaseName()
    {
        var databaseName = furpaDbContext.Database.GetDbConnection().Database;
        return string.IsNullOrWhiteSpace(databaseName)
            ? "Furpa"
            : databaseName;
    }

    private static SourcePosDocumentRow ReadSourceDocumentRow(DbDataReader reader, SourceSystem source) =>
        new(
            ReadGuid(reader, "FaturaGuid"),
            ReadString(reader, "Sube"),
            ReadDateTime(reader, "Tarih"),
            ReadString(reader, "BelgeTuru"),
            ReadString(reader, "BelgeTipi"),
            ReadString(reader, "FaturaVergiNo"),
            ReadString(reader, "BelgeNo"),
            ReadInt(reader, "KasaNo"),
            ReadString(reader, "KartNumarasi"),
            ReadDecimal(reader, "Toplam"),
            source);

    private static PosAccountingImportResultDto CreateImportResult(
        string documentKind,
        DateTime businessDate,
        IReadOnlyCollection<PosAccountingOperationResultDto> results) =>
        new(
            documentKind,
            businessDate,
            results.Count(item => item.Success),
            results.Count(item => !item.Success && item.DocumentId.HasValue),
            results.Count(item => !item.Success && !item.DocumentId.HasValue),
            results.ToArray());

    private static PosAccountingBatchResultDto CreateBatchResult(
        string documentKind,
        int requestedCount,
        IReadOnlyCollection<PosAccountingOperationResultDto> results) =>
        new(
            documentKind,
            requestedCount,
            results.Count(item => item.Success),
            results.Count(item => !item.Success),
            results.ToArray());

    private static void ValidateImportRequest(ImportPosDocumentsRequest request)
    {
        if (request.BusinessDate == default)
        {
            throw new ArgumentException("Business date is required.", nameof(request.BusinessDate));
        }

        if (request.WarehouseNo is <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }
    }

    private static void ValidateMappingRequest(UpsertCashRegisterBranchMappingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CashRegisterNo))
        {
            throw new ArgumentException("Cash register no is required.", nameof(request.CashRegisterNo));
        }

        if (request.BranchNo <= 0)
        {
            throw new ArgumentException("Branch no must be greater than zero.", nameof(request.BranchNo));
        }
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value switch
        {
            null => DBNull.Value,
            bool boolValue => boolValue ? 1 : 0,
            _ => value
        };
        command.Parameters.Add(parameter);
    }

    private static string QuoteSqlIdentifier(string identifier) =>
        $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";

    private static int ParseBranchNo(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

    private static int ParseDocumentNo(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

    private static string ResolvePaymentType(string cardNumber) =>
        string.IsNullOrWhiteSpace(cardNumber) ? CashPaymentType : CreditPaymentType;

    private static string NormalizePaymentType(string value)
    {
        var normalized = NormalizeText(value);

        if (string.Equals(normalized, CashPaymentType, StringComparison.OrdinalIgnoreCase))
        {
            return CashPaymentType;
        }

        if (string.Equals(normalized, CreditPaymentType, StringComparison.OrdinalIgnoreCase))
        {
            return CreditPaymentType;
        }

        return normalized;
    }

    private static decimal CalculateVeraTotalExcludingTax(int taxRate, decimal lineTotal) =>
        taxRate switch
        {
            18 => Math.Round(lineTotal / 1.18m, 2, MidpointRounding.AwayFromZero),
            8 => Math.Round(lineTotal / 1.08m, 2, MidpointRounding.AwayFromZero),
            1 => Math.Round(lineTotal / 1.01m, 2, MidpointRounding.AwayFromZero),
            _ => lineTotal
        };

    private static void AddAccountingLine(
        ICollection<AccountingSlipLine> lines,
        string accountCode,
        decimal amount,
        decimal taxBase = 0m)
    {
        amount = ToMoney(amount);
        if (amount == 0m)
        {
            return;
        }

        lines.Add(new AccountingSlipLine(accountCode, amount, ToMoney(taxBase)));
    }

    private static IReadOnlyCollection<AccountingSlipLine> BalanceAccountingLines(
        List<AccountingSlipLine> lines)
    {
        if (lines.Count == 0)
        {
            return lines;
        }

        var difference = ToMoney(lines.Sum(item => item.Amount));
        if (difference == 0m)
        {
            return lines;
        }

        if (Math.Abs(difference) > 0.05m)
        {
            throw new InvalidOperationException(
                $"Accounting slip lines are not balanced. Difference={difference.ToString(CultureInfo.InvariantCulture)}");
        }

        var index = lines.FindLastIndex(item => item.Amount < 0m);
        if (index < 0)
        {
            index = lines.Count - 1;
        }

        var line = lines[index];
        lines[index] = line with { Amount = ToMoney(line.Amount - difference) };

        return lines;
    }

    private static string ResolvePaymentAccountCode(string paymentType) =>
        string.Equals(NormalizePaymentType(paymentType), CreditPaymentType, StringComparison.OrdinalIgnoreCase)
            ? CreditAccountCode
            : CashAccountCode;

    private static string CreateSalesRevenueAccountCode(int taxRate) =>
        taxRate switch
        {
            0 => "600.00",
            1 => "600.01",
            8 => "600.08",
            10 => "600.010",
            18 => "600.18",
            20 => "600.20",
            > 0 and < 10 => $"600.0{taxRate.ToString(CultureInfo.InvariantCulture)}",
            _ => $"600.{taxRate.ToString(CultureInfo.InvariantCulture)}"
        };

    private static string CreateSalesTaxAccountCode(int taxRate) =>
        $"391.01.{Math.Max(taxRate, 0).ToString("000", CultureInfo.InvariantCulture)}";

    private static string CreateExpenseRevenueAccountCode(int taxRate) =>
        $"610.01.{Math.Max(taxRate, 0).ToString("00", CultureInfo.InvariantCulture)}";

    private static string CreateExpenseTaxAccountCode(int taxRate) =>
        $"191.{Math.Max(taxRate, 0).ToString("00", CultureInfo.InvariantCulture)}";

    private static decimal ToMoney(double value) =>
        ToMoney((decimal)value);

    private static decimal ToMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static double ToDouble(decimal value) =>
        Convert.ToDouble(value);

    private static string LimitText(string? value, int maxLength)
    {
        var normalized = NormalizeText(value);
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }

    private static string NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string ReadString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal)
            ? string.Empty
            : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
    }

    private static int ReadInt(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal)
            ? 0
            : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static Guid ReadGuid(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            return Guid.Empty;
        }

        var value = reader.GetValue(ordinal);
        return value is Guid guid
            ? guid
            : Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
    }

    private static DateTime ReadDateTime(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal)
            ? default
            : Convert.ToDateTime(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static decimal ReadDecimal(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal)
            ? 0m
            : Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static int ReadInt(IReadOnlyDictionary<string, object?> row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.TryGetValue(name, out var value) || value is null)
            {
                continue;
            }

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        return 0;
    }

    private static decimal ReadDecimal(IReadOnlyDictionary<string, object?> row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.TryGetValue(name, out var value) || value is null)
            {
                continue;
            }

            return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }

        return 0m;
    }

    private sealed record AccountingSlipDocument(
        DateTime DocumentDate,
        string Description,
        string ResponsibilityCode,
        Guid? SourceGuid,
        string DocumentSeries,
        int DocumentNumber,
        string ExternalDocumentNo,
        decimal TotalAmount,
        decimal TaxAmount,
        CustomerAccountingInfo Customer,
        byte ShortDocumentType,
        byte SalesType,
        byte PurchaseType,
        byte OffsetType,
        IReadOnlyCollection<AccountingSlipLine> Lines);

    private sealed record AccountingSlipLine(
        string AccountCode,
        decimal Amount,
        decimal TaxBase = 0m);

    private sealed record AccountingSlipIdentity(
        int SlipNo,
        int JournalNo);

    private sealed record CustomerAccountingInfo(
        string Name,
        string TaxOffice,
        string TaxNo,
        string Code,
        string AccountingCode)
    {
        public static CustomerAccountingInfo Empty { get; } = new(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty);
    }

    private sealed record SourcePosDocumentRow(
        Guid Guid,
        string BranchNo,
        DateTime Date,
        string DocumentKind,
        string DocumentType,
        string CustomerTaxNo,
        string DocumentNo,
        int CashRegisterNo,
        string CardNumber,
        decimal Total,
        SourceSystem Source);

    private sealed record CalculatedPosLine(
        int TaxRate,
        decimal TotalExcludingTax,
        decimal TotalTax);

    private enum SourceSystem
    {
        Furpa = 0,
        Vera = 1
    }
}

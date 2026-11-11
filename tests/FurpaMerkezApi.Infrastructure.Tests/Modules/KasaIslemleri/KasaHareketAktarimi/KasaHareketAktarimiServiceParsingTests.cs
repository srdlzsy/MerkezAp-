using System.Reflection;
using FurpaMerkezApi.Application.Common.Errors;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaHareketAktarimi;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaHareketAktarimi;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.KasaIslemleri.KasaHareketAktarimi;

public sealed class KasaHareketAktarimiServiceParsingTests
{
    [Theory]
    [InlineData("StokHareketYaz")]
    [InlineData("stokhareketyaz")]
    public void ResolveProcedureCommandTimeoutUsesTenMinutesForStockMovementWrite(string procedureName)
    {
        Assert.Equal(600, KasaHareketAktarimiService.ResolveProcedureCommandTimeoutSeconds(procedureName));
    }

    [Fact]
    public void ResolveProcedureCommandTimeoutKeepsDefaultForOtherProcedures()
    {
        Assert.Equal(300, KasaHareketAktarimiService.ResolveProcedureCommandTimeoutSeconds("HareketSil"));
    }

    [Theory]
    [InlineData("HareketSil")]
    [InlineData("StokHareketYaz")]
    [InlineData("StokHareketSil")]
    [InlineData("StokHareketSilTarihAralikli")]
    public void MikroMutationProceduresUseExclusiveApplicationLock(string procedureName)
    {
        Assert.True(KasaHareketAktarimiService.RequiresExclusiveMutationLock(procedureName));
    }

    [Fact]
    public void ReadOnlyProcedureDoesNotUseExclusiveApplicationLock()
    {
        Assert.False(KasaHareketAktarimiService.RequiresExclusiveMutationLock("RaporOku"));
    }

    [Theory]
    [InlineData("129.50", 129.50)]
    [InlineData("730.00", 730.00)]
    [InlineData("4625.17", 4625.17)]
    [InlineData("33420,11", 33420.11)]
    [InlineData("003342011", 33420.11)]
    public void ParseMoneyReadsHrAmountFormat(string value, decimal expected)
    {
        Assert.Equal(expected, InvokeDecimal("ParseMoney", value));
    }

    [Theory]
    [InlineData("01.000", 1)]
    [InlineData("00.642", 0.642)]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("1,234.56", 1234.56)]
    public void ParseDecimalReadsHrQuantityAndFormattedDecimals(string value, decimal expected)
    {
        Assert.Equal(expected, InvokeDecimal("ParseDecimal", value));
    }

    [Fact]
    public void NormalizeImportedBarcodeRemovesNullPaddingWithoutLosingDigits()
    {
        var paddedBarcode = "2" + new string('\0', 10_000) + "102670";

        var result = KasaHareketAktarimiService.NormalizeImportedBarcode(paddedBarcode);

        Assert.Equal("2102670", result);
    }

    [Fact]
    public void SanitizeImportIssueMessageRemovesControlCharactersAndLimitsPayload()
    {
        var malformedMessage = "Sistemde olmayan barkod: 2" + new string('\0', 10_000) + new string('9', 1_000);

        var result = KasaHareketAktarimiService.SanitizeImportIssueMessage(malformedMessage);

        Assert.DoesNotContain('\0', result);
        Assert.EndsWith("...", result, StringComparison.Ordinal);
        Assert.True(result.Length <= 503);
    }

    [Theory]
    [InlineData(1, 18)]
    [InlineData(2, 8)]
    [InlineData(3, 1)]
    [InlineData(4, 0)]
    [InlineData(5, 0)]
    public void MapTaxRateReturnsOldRatesBeforeJuly10_2023(int taxCode, int expectedRate)
    {
        var beforeDate = new DateTime(2023, 7, 9);
        Assert.Equal(expectedRate, InvokeTaxRate(taxCode, beforeDate));
    }

    [Theory]
    [InlineData(1, 20)]
    [InlineData(2, 10)]
    [InlineData(3, 1)]
    [InlineData(4, 0)]
    [InlineData(5, 0)]
    public void MapTaxRateReturnsNewRatesOnOrAfterJuly10_2023(int taxCode, int expectedRate)
    {
        var onDate = new DateTime(2023, 7, 10);
        var afterDate = new DateTime(2024, 1, 1);
        Assert.Equal(expectedRate, InvokeTaxRate(taxCode, onDate));
        Assert.Equal(expectedRate, InvokeTaxRate(taxCode, afterDate));
    }

    [Fact]
    public void MapTaxRateDefaultsToNewRatesWhenDateIsNotProvided()
    {
        Assert.Equal(20, InvokeTaxRate(1, default));
        Assert.Equal(10, InvokeTaxRate(2, default));
    }

    [Fact]
    public void ApplyDiscountDistributesProportionallyWhenTopDiscountLineProvided()
    {
        var parserStateType = typeof(KasaHareketAktarimiService).GetNestedType("ParserState", BindingFlags.NonPublic)!;
        var movementImportKindType = typeof(KasaHareketAktarimiService).GetNestedType("MovementImportKind", BindingFlags.NonPublic)!;
        var parsedReceiptType = typeof(KasaHareketAktarimiService).GetNestedType("ParsedReceipt", BindingFlags.NonPublic)!;
        var parsedLineType = typeof(KasaHareketAktarimiService).GetNestedType("ParsedLine", BindingFlags.NonPublic)!;

        var state = Activator.CreateInstance(parserStateType, 1, Enum.Parse(movementImportKindType, "Normal"), "test.hr")!;
        var receipt = Activator.CreateInstance(parsedReceiptType)!;
        parserStateType.GetMethod("Begin")!.Invoke(state, new[] { receipt });

        var linesList = (System.Collections.IList)parsedReceiptType.GetProperty("Lines")!.GetValue(receipt)!;

        // Line 1: 100 TL
        var line1 = Activator.CreateInstance(parsedLineType)!;
        parsedLineType.GetProperty("GrossAmount")!.SetValue(line1, 100m);
        parsedLineType.GetProperty("Quantity")!.SetValue(line1, 1m);
        parsedLineType.GetProperty("Status")!.SetValue(line1, (byte)0);
        linesList.Add(line1);

        // Line 2: 300 TL
        var line2 = Activator.CreateInstance(parsedLineType)!;
        parsedLineType.GetProperty("GrossAmount")!.SetValue(line2, 300m);
        parsedLineType.GetProperty("Quantity")!.SetValue(line2, 1m);
        parsedLineType.GetProperty("Status")!.SetValue(line2, (byte)0);
        linesList.Add(line2);

        // TOP discount: 40 TL (Line 1 gets 10 TL, Line 2 gets 30 TL)
        var tokens = new[] { "01", "001", "1", "1", "TOPIND", "40.00" };
        var applyDiscountMethod = typeof(KasaHareketAktarimiService).GetMethod("ApplyDiscount", BindingFlags.NonPublic | BindingFlags.Static)!;
        applyDiscountMethod.Invoke(null, new object[] { state, tokens });

        var discount1 = (decimal)parsedLineType.GetProperty("Discount")!.GetValue(line1)!;
        var gross1 = (decimal)parsedLineType.GetProperty("GrossAmount")!.GetValue(line1)!;
        var discount2 = (decimal)parsedLineType.GetProperty("Discount")!.GetValue(line2)!;
        var gross2 = (decimal)parsedLineType.GetProperty("GrossAmount")!.GetValue(line2)!;
        var invoiceDiscount = (decimal)parsedReceiptType.GetProperty("InvoiceDiscount")!.GetValue(receipt)!;

        Assert.Equal(10m, discount1);
        Assert.Equal(90m, gross1);
        Assert.Equal(30m, discount2);
        Assert.Equal(270m, gross2);
        Assert.Equal(40m, invoiceDiscount);
    }

    [Fact]
    public void NormalizeCashRegisterNosCombinesSingleAndMultipleSelections()
    {
        var method = typeof(KasaHareketAktarimiService).GetMethod(
            "NormalizeCashRegisterNos",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        var result = Assert.IsType<int[]>(method!.Invoke(null, new object?[] { 52, new[] { 51, 52, 53 } }));

        Assert.Equal([51, 52, 53], result);
    }

    [Fact]
    public void CreateSalesComparisonRowSeparatesExpenseAndComparableTurnover()
    {
        var row = KasaHareketAktarimiService.CreateSalesComparisonRow(
            new DateTime(2026, 11, 3),
            116,
            "Test Sube",
            retailStockAmount: 1_000m,
            invoiceStockAmount: 200m,
            expenseSlipStockAmount: 30m,
            currentComparableStockAmount: 995m,
            currentAccountAmount: 990m,
            retailTransferredAmount: 990m,
            invoiceTransferredAmount: 200m,
            expenseSlipTransferredAmount: 30m,
            zReportAmount: 1_190m,
            tolerance: 0.01m);

        Assert.Equal(1_200m, row.StockMovementAmount);
        Assert.Equal(1_190m, row.TransferredAmount);
        Assert.Equal(1_160m, row.ExpenseExcludedAmount);
        Assert.Equal(-20m, row.ZReportTransferDifference);
        Assert.Equal(-10m, row.TransferredTurnoverDifference);
        Assert.Equal(5m, row.CurrentStockDifference);
        Assert.Equal(-10m, row.RetailDifference);
        Assert.Equal(0m, row.InvoiceDifference);
        Assert.Equal(0m, row.ExpenseSlipDifference);
        Assert.Equal(1_170m, row.NetStockTurnover);
        Assert.Equal(1_160m, row.NetTransferredTurnover);
        Assert.Equal(-10m, row.NetDifference);
        Assert.True(row.HasDifference);
    }

    [Fact]
    public void CreateSalesComparisonRowKeepsLegacyAuditDifferenceSeparateFromCategoryStatus()
    {
        var row = KasaHareketAktarimiService.CreateSalesComparisonRow(
            new DateTime(2026, 10, 27),
            160,
            "Test Sube",
            retailStockAmount: 965.50m,
            invoiceStockAmount: 34.50m,
            expenseSlipStockAmount: 30m,
            currentComparableStockAmount: 965.50m,
            currentAccountAmount: 965.50m,
            retailTransferredAmount: 965.50m,
            invoiceTransferredAmount: 34.50m,
            expenseSlipTransferredAmount: 30m,
            zReportAmount: 1_000m,
            tolerance: 0.01m);

        Assert.Equal(1_000m, row.StockMovementAmount);
        Assert.Equal(1_000m, row.TransferredAmount);
        Assert.Equal(970m, row.ExpenseExcludedAmount);
        Assert.Equal(-30m, row.ZReportTransferDifference);
        Assert.Equal(0m, row.TransferredTurnoverDifference);
        Assert.Equal(0m, row.CurrentStockDifference);
        Assert.Equal(970m, row.NetStockTurnover);
        Assert.Equal(970m, row.NetTransferredTurnover);
        Assert.Equal(0m, row.NetDifference);
        Assert.False(row.HasDifference);
    }

    [Fact]
    public void EnsureExclusivePendingBranchAllowsOnlyRequestedBranch()
    {
        KasaHareketAktarimiService.EnsureExclusivePendingBranch(
            requestedBranchNo: 160,
            requireExclusiveBranch: true,
            pendingBranchNos: [160]);
    }

    [Fact]
    public void EnsureExclusivePendingBranchRejectsOtherPendingBranches()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            KasaHareketAktarimiService.EnsureExclusivePendingBranch(
                requestedBranchNo: 160,
                requireExclusiveBranch: true,
                pendingBranchNos: [159, 160, 161]));

        Assert.Contains("159, 161", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Mikroya Aktar 2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureExclusivePendingBranchAllowsNoOpWhenRequestedBranchIsNotPending()
    {
        KasaHareketAktarimiService.EnsureExclusivePendingBranch(
            requestedBranchNo: 160,
            requireExclusiveBranch: true,
            pendingBranchNos: [159, 161]);
    }

    [Fact]
    public void FindDocumentDifferenceReturnsOnlyDocumentsCreatedByCurrentOperation()
    {
        var existingReceipt = CreateDocument("PSF159", 29901);
        var existingExpense = CreateDocument("GPS159", 79);
        var createdStockDocument = CreateDocument("PS159", 22278);

        var result = KasaHareketAktarimiService.FindDocumentDifference(
            [existingReceipt, existingExpense, createdStockDocument],
            [existingReceipt, existingExpense]);

        var document = Assert.Single(result);
        Assert.Equal("PS159", document.Key.DocumentSerie);
        Assert.Equal(22278, document.Key.DocumentOrderNo);
    }

    [Fact]
    public void FindDocumentDifferenceSeparatesStockAndCustomerDocumentsWithSameNumber()
    {
        var stockDocument = CreateDocument(
            "PS159",
            22278,
            DocumentFlowType.StockMovementDocument);
        var customerDocument = CreateDocument(
            "PS159",
            22278,
            DocumentFlowType.CustomerMovementDocument);

        var result = KasaHareketAktarimiService.FindDocumentDifference(
            [stockDocument, customerDocument],
            [stockDocument]);

        var document = Assert.Single(result);
        Assert.Equal(DocumentFlowType.CustomerMovementDocument, document.Key.DocumentType);
    }

    [Fact]
    public void EnsureMikroPosDocumentSequencesAreSafeAllowsAlignedSequences()
    {
        KasaHareketAktarimiService.EnsureMikroPosDocumentSequencesAreSafe(
        [
            new(111, 11850, 11850),
            new(126, 4478, 4480)
        ]);
    }

    [Fact]
    public void EnsureMikroPosDocumentSequencesAreSafeRejectsStockSequenceAheadOfCustomer()
    {
        var exception = Assert.Throws<OperationConflictException>(() =>
            KasaHareketAktarimiService.EnsureMikroPosDocumentSequencesAreSafe(
            [
                new(111, 11850, 11849, new DateTime(2026, 11, 4), new DateTime(2026, 10, 29)),
                new(126, 4478, 4478)
            ]));

        Assert.Equal(OperationConflictErrorCodes.MikroPosDocumentSequenceConflict, exception.ErrorCode);
        Assert.False(exception.Retryable);
        Assert.Contains("sube 111", exception.Message, StringComparison.Ordinal);
        Assert.Contains("stok PS111/11850", exception.Message, StringComparison.Ordinal);
        Assert.Contains("cari PS111/11849", exception.Message, StringComparison.Ordinal);
        Assert.Contains("04.11.2026", exception.Message, StringComparison.Ordinal);
        Assert.Contains("29.10.2026", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Mevcut Mikro evraklarina dokunulmadi", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MergeImportResultsCombinesNormalAndCanceledMovements()
    {
        var warning = new KasaHareketImportIssueDto(159, 1, "HR301026.001", null, null, "Warning");
        var error = new KasaHareketImportIssueDto(159, 1, "IP301026.001", null, null, "Error");
        var normal = CreateImportResult("normal", 2, 10, 1, 30, 12, 4, [warning], []);
        var cancel = CreateImportResult("cancel", 1, 3, 2, 8, 0, 0, [], [error]);

        var result = KasaHareketAktarimiService.MergeImportResults(
            "normal",
            new DateTime(2026, 10, 30),
            dryRun: false,
            normal,
            cancel);

        Assert.Equal("normal", result.ImportType);
        Assert.Equal("CompletedWithErrors", result.Status);
        Assert.Equal(3, result.ProcessedFiles);
        Assert.Equal(13, result.ProcessedInvoices);
        Assert.Equal(3, result.SkippedExistingInvoices);
        Assert.Equal(38, result.InsertedLines);
        Assert.Equal(12, result.InsertedPayments);
        Assert.Equal(4, result.InsertedPromotions);
        Assert.Equal(warning, Assert.Single(result.Warnings));
        Assert.Equal(error, Assert.Single(result.Errors));
    }

    private static KasaHareketImportResultDto CreateImportResult(
        string importType,
        int processedFiles,
        int processedInvoices,
        int skippedExistingInvoices,
        int insertedLines,
        int insertedPayments,
        int insertedPromotions,
        IReadOnlyCollection<KasaHareketImportIssueDto> warnings,
        IReadOnlyCollection<KasaHareketImportIssueDto> errors) =>
        new(
            importType,
            importType,
            errors.Count == 0 ? "Completed" : "CompletedWithErrors",
            processedFiles,
            processedInvoices,
            skippedExistingInvoices,
            insertedLines,
            insertedPayments,
            insertedPromotions,
            warnings,
            errors);

    private static KasaHareketAktarimiService.MikroPosDocumentSnapshot CreateDocument(
        string documentSerie,
        int documentOrderNo,
        DocumentFlowType documentType = DocumentFlowType.StockMovementDocument) =>
        new(
            new KasaHareketAktarimiService.MikroPosDocumentKey(
                documentType,
                new DateTime(2026, 10, 30),
                159,
                documentSerie,
                documentOrderNo),
            1);

    private static int InvokeTaxRate(int taxCode, DateTime receiptDate)
    {
        var method = typeof(KasaHareketAktarimiService).GetMethod(
            "MapTaxRate",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        return Assert.IsType<int>(method!.Invoke(null, new object[] { taxCode, receiptDate }));
    }

    [Theory]
    [InlineData("3958", 3958)]
    [InlineData("0003958", 3958)]
    [InlineData("1.0 1", 1)]
    [InlineData("1.0", 1)]
    [InlineData(" 123 ", 123)]
    [InlineData("0", 0)]
    [InlineData("", 0)]
    [InlineData("   ", 0)]
    [InlineData("abc", 0)]
    [InlineData(null, 0)]
    public void ParseZNo_SanitizesMalformedOrPrefixedZNumbers(string? value, int expected)
    {
        var result = KasaHareketAktarimiService.ParseZNo(value);
        Assert.Equal(expected, result);
    }

    private static decimal InvokeDecimal(string methodName, string value)
    {
        var method = typeof(KasaHareketAktarimiService).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        return Assert.IsType<decimal>(method!.Invoke(null, new object[] { value }));
    }
}

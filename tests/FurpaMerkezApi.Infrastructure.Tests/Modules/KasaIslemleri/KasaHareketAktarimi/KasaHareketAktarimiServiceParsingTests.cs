using System.Reflection;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaHareketAktarimi;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaHareketAktarimi;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.KasaIslemleri.KasaHareketAktarimi;

public sealed class KasaHareketAktarimiServiceParsingTests
{
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
            stockMovementAmount: 1_000m,
            currentComparableStockAmount: 995m,
            currentAccountAmount: 990m,
            transferredAmount: 1_020m,
            expenseReceiptAmount: 30m,
            zReportAmount: 960m,
            tolerance: 0.01m);

        Assert.Equal(990m, row.ExpenseExcludedAmount);
        Assert.Equal(0m, row.ZReportTransferDifference);
        Assert.Equal(-10m, row.TransferredTurnoverDifference);
        Assert.Equal(5m, row.CurrentStockDifference);
        Assert.True(row.HasDifference);
    }

    [Fact]
    public void CreateSalesComparisonRowDoesNotTreatCanceledStockAsCurrentAccountDifference()
    {
        var row = KasaHareketAktarimiService.CreateSalesComparisonRow(
            new DateTime(2026, 10, 27),
            160,
            "Test Sube",
            stockMovementAmount: 1_000m,
            currentComparableStockAmount: 965.50m,
            currentAccountAmount: 965.50m,
            transferredAmount: 1_030m,
            expenseReceiptAmount: 30m,
            zReportAmount: 970m,
            tolerance: 0.01m);

        Assert.Equal(1_000m, row.ExpenseExcludedAmount);
        Assert.Equal(0m, row.ZReportTransferDifference);
        Assert.Equal(0m, row.TransferredTurnoverDifference);
        Assert.Equal(0m, row.CurrentStockDifference);
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
    public void EnsurePendingTransferSourceTotalsMatchAllowsMatchingInvoices()
    {
        KasaHareketAktarimiService.EnsurePendingTransferSourceTotalsMatch([]);
    }

    [Fact]
    public void EnsurePendingTransferSourceTotalsMatchRejectsMismatchingInvoice()
    {
        var mismatch = new KasaHareketAktarimiService.PendingTransferSourceMismatch(
            152,
            185,
            143,
            270.22m,
            274.80m,
            4.58m);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            KasaHareketAktarimiService.EnsurePendingTransferSourceTotalsMatch([mismatch]));

        Assert.Contains("sube 152", exception.Message, StringComparison.Ordinal);
        Assert.Contains("kasa 185", exception.Message, StringComparison.Ordinal);
        Assert.Contains("fis 143", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Kaynak fisleri duzeltip tekrar deneyin", exception.Message, StringComparison.Ordinal);
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

    private static decimal InvokeDecimal(string methodName, string value)
    {
        var method = typeof(KasaHareketAktarimiService).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        return Assert.IsType<decimal>(method!.Invoke(null, new object[] { value }));
    }
}

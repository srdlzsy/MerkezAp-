using System.Reflection;
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
    public void CreateSalesComparisonRowUsesLegacyReconciliationFormulas()
    {
        var row = KasaHareketAktarimiService.CreateSalesComparisonRow(
            new DateTime(2026, 11, 3),
            116,
            "Test Sube",
            stockMovementAmount: 1_000m,
            currentAccountAmount: 990m,
            transferredAmount: 1_020m,
            expenseReceiptAmount: 30m,
            zReportAmount: 960m,
            tolerance: 0.01m);

        Assert.Equal(990m, row.ExpenseExcludedAmount);
        Assert.Equal(10m, row.ZReportTransferDifference);
        Assert.Equal(20m, row.TransferredTurnoverDifference);
        Assert.Equal(10m, row.CurrentStockDifference);
        Assert.True(row.HasDifference);
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

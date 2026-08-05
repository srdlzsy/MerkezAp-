using System.Globalization;
using FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGonderimi;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.FaturaIslemleri.FaturaGonderimi;

public sealed class InvoiceSendingAmountFormatterTests
{
    [Theory]
    [InlineData("0.6042", "0.6042")]
    [InlineData("6.037765", "6.0378")]
    [InlineData("1", "1.00")]
    [InlineData("0.6", "0.60")]
    public void FormatAllowanceAmount_PreservesDiscountPrecision(string value, string expected)
    {
        var amount = decimal.Parse(value, CultureInfo.InvariantCulture);

        var result = InvoiceSendingAmountFormatter.FormatAllowanceAmount(amount);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatMoneyAmount_KeepsOfficialTotalsAtTwoDecimals()
    {
        var result = InvoiceSendingAmountFormatter.FormatMoneyAmount(0.6042m);

        Assert.Equal("0.60", result);
    }
}

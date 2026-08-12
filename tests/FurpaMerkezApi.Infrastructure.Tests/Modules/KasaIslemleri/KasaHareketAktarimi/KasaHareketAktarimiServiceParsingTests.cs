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

    private static decimal InvokeDecimal(string methodName, string value)
    {
        var method = typeof(KasaHareketAktarimiService).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        return Assert.IsType<decimal>(method!.Invoke(null, new object[] { value }));
    }
}

using System.Reflection;
using FurpaMerkezApi.Infrastructure.Modules.RaporIslemleri.StokRaporlari;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.RaporIslemleri.StokRaporlari;

public sealed class StockReportsUseCaseNormalizationTests
{
    [Theory]
    [InlineData("stock", "stock")]
    [InlineData("stok", "stock")]
    [InlineData("\u00FCr\u00FCn", "stock")]
    [InlineData("category", "category")]
    [InlineData("kategori", "category")]
    [InlineData("producer", "producer")]
    [InlineData("\u00FCretici", "producer")]
    [InlineData("supplier", "supplier")]
    [InlineData("tedarik\u00E7i", "supplier")]
    [InlineData("product-manager", "product-manager")]
    [InlineData("sat\u0131n-almac\u0131", "product-manager")]
    public void NormalizeFilterType_AcceptsDocumentedAliases(string value, string expected)
    {
        var actual = InvokePrivateStatic<string?>("NormalizeFilterType", value);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("stock", "stock")]
    [InlineData("\u00FCr\u00FCn", "stock")]
    [InlineData("producer", "producer")]
    [InlineData("\u00FCretici", "producer")]
    [InlineData("supplier", "supplier")]
    [InlineData("tedarik\u00E7i", "supplier")]
    [InlineData("sat\u0131n-almac\u0131", "product-manager")]
    public void NormalizeProfitabilityScope_AcceptsDocumentedAliases(string value, string expected)
    {
        var actual = InvokePrivateStatic<string>("NormalizeProfitabilityScope", value);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ValidateFilterPair_RejectsFilterValueWithoutFilterType()
    {
        var exception = Assert.Throws<TargetInvocationException>(() =>
            InvokePrivateStatic<object?>("ValidateFilterPair", null, "ABC"));

        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Fact]
    public void ValidateFilterPair_RejectsFilterTypeWithoutFilterValue()
    {
        var exception = Assert.Throws<TargetInvocationException>(() =>
            InvokePrivateStatic<object?>("ValidateFilterPair", "producer", null));

        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    private static T? InvokePrivateStatic<T>(string methodName, params object?[] arguments)
    {
        var method = typeof(StockReportsUseCase).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        return (T?)method.Invoke(null, arguments);
    }
}

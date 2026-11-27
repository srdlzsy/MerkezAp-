using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.ManavMalKabulVeEtiket;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.KasaIslemleri.ManavMalKabulVeEtiket;

public sealed class GreenGrocerInvoiceIdentityTests
{
    [Theory]
    [InlineData("CFA2026000018914", "CFA26", 18914)]
    [InlineData("eft2026000000678", "EFT26", 678)]
    public void TryParseOfficialDocumentNo_UsesMikroInvoiceSeriesConvention(
        string documentNo,
        string expectedSeries,
        int expectedOrderNo)
    {
        var parsed = GreenGrocerInvoiceIdentity.TryParseOfficialDocumentNo(documentNo, out var identity);

        Assert.True(parsed);
        Assert.Equal(expectedSeries, identity.BaseSeries);
        Assert.Equal(expectedOrderNo, identity.DocumentOrderNo);
        Assert.Equal(documentNo.ToUpperInvariant(), identity.OfficialDocumentNo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("CFA260000018914")]
    [InlineData("CF-2026000018914")]
    [InlineData("CFA2026000000000")]
    public void TryParseOfficialDocumentNo_RejectsAmbiguousValues(string? documentNo)
    {
        Assert.False(GreenGrocerInvoiceIdentity.TryParseOfficialDocumentNo(documentNo, out _));
    }

    [Fact]
    public void NormalizeInvoiceUuid_ReturnsCanonicalUppercaseValue()
    {
        var result = GreenGrocerInvoiceIdentity.NormalizeInvoiceUuid(
            " 9bb38ef4-91b0-4570-87b7-f1ad559bcb95 ");

        Assert.Equal("9BB38EF4-91B0-4570-87B7-F1AD559BCB95", result);
    }

    [Fact]
    public void BuildCandidateSeries_AppendsCollisionSuffix()
    {
        Assert.Equal("EFT26", GreenGrocerInvoiceIdentity.BuildCandidateSeries("EFT26", 0));
        Assert.Equal("EFT261", GreenGrocerInvoiceIdentity.BuildCandidateSeries("EFT26", 1));
    }
}

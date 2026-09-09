using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving;

public sealed class CompanyReceivingDocumentIdentityParserTests
{
    [Fact]
    public void TryParseOfficialDocumentNo_ParsesCanonicalSerieAndNineDigitOrderNo()
    {
        var parsed = CompanyReceivingDocumentIdentityParser.TryParseOfficialDocumentNo(
            "IRS000019105",
            out var identity);

        Assert.True(parsed);
        Assert.Equal("IRS", identity.DocumentSerie);
        Assert.Equal(19105, identity.DocumentOrderNo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("FMK")]
    [InlineData("IRS19105-1")]
    [InlineData("IRS19105")]
    [InlineData("IRS 000019105")]
    public void TryParseOfficialDocumentNo_DoesNotGuessInvalidIdentity(string? value)
    {
        var parsed = CompanyReceivingDocumentIdentityParser.TryParseOfficialDocumentNo(
            value,
            out _);

        Assert.False(parsed);
    }

    [Theory]
    [InlineData("IRS1")]
    [InlineData("fmk120")]
    [InlineData("A1")]
    public void IsValidSerie_AcceptsAsciiLettersAndDigits(string value)
    {
        Assert.True(CompanyReceivingDocumentIdentityParser.IsValidSerie(value));
    }

    [Theory]
    [InlineData("IRS-1")]
    [InlineData("IRS 1")]
    [InlineData("IRS_")]
    [InlineData("")]
    public void IsValidSerie_RejectsAmbiguousValues(string value)
    {
        Assert.False(CompanyReceivingDocumentIdentityParser.IsValidSerie(value));
    }
}

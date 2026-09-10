using FurpaMerkezApi.Infrastructure.Services;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Services;

public sealed class EDespatchServiceTests
{
    [Theory]
    [InlineData("ORHAN BAYRAM", "ORHAN", "BAYRAM")]
    [InlineData("ORHAN ALI BAYRAM", "ORHAN ALI", "BAYRAM")]
    [InlineData("ORHAN", "ORHAN", "ORHAN")]
    public void SplitPersonName_ReturnsSchemaSafeNameParts(
        string value,
        string expectedFirstName,
        string expectedFamilyName)
    {
        var result = EDespatchService.SplitPersonName(value);

        Assert.Equal(expectedFirstName, result.FirstName);
        Assert.Equal(expectedFamilyName, result.FamilyName);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("FRM2026000000001", 1)]
    [InlineData("FRM2026000012345", 12345)]
    public void ParseEDespatchSequence_ReturnsNumericSequence(
        string? documentNo,
        int expected)
    {
        var result = EDespatchService.ParseEDespatchSequence(documentNo, "FRM2026");

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("FRM20260001")]
    [InlineData("FRM2026ABCDEFGHI")]
    [InlineData("ABC2026000000001")]
    public void ParseEDespatchSequence_RejectsInvalidDocumentNumber(string documentNo)
    {
        Assert.Throws<InvalidOperationException>(() =>
            EDespatchService.ParseEDespatchSequence(documentNo, "FRM2026"));
    }

    [Fact]
    public void BuildSentMarkerApiRow_OmitsNullOptionalFields()
    {
        var movementGuid = Guid.Parse("32f4c96c-c5a4-450d-8aac-eb915d7ce8dd");

        var result = EDespatchService.BuildSentMarkerApiRow(
            movementGuid,
            "FRM2026000000123",
            "291b1f58-62b1-4615-8853-c1908e6a1d53",
            "16 ABC 123",
            null,
            "ALI VELI",
            "11111111111");

        Assert.Equal(movementGuid, result["sth_Guid"]);
        Assert.Equal(true, result["sth_kilitli"]);
        Assert.Equal("FRM2026000000123", result["sth_belge_no"]);
        Assert.Equal("291b1f58-62b1-4615-8853-c1908e6a1d53", result["sth_aciklama"]);
        Assert.Equal("16 ABC 123", result["sth_HareketGrupKodu1"]);
        Assert.Equal("ALI VELI", result["sth_HareketGrupKodu3"]);
        Assert.Equal("11111111111", result["sth_ismerkezi_kodu"]);
        Assert.DoesNotContain("sth_HareketGrupKodu2", result.Keys);
        Assert.DoesNotContain(result.Values, value => value is null);
    }

    [Fact]
    public void SelectActiveDespatchReceiverAlias_PreservesActiveMikroAlias()
    {
        var result = EDespatchService.SelectActiveDespatchReceiverAlias(
            "urn:mail:preferred@example.com",
            [
                "urn:mail:first@example.com",
                "URN:MAIL:PREFERRED@EXAMPLE.COM"
            ]);

        Assert.Equal("URN:MAIL:PREFERRED@EXAMPLE.COM", result);
    }

    [Fact]
    public void SelectActiveDespatchReceiverAlias_ReplacesInactiveMikroAliasWithFirstActiveAlias()
    {
        var result = EDespatchService.SelectActiveDespatchReceiverAlias(
            "urn:mail:old@example.com",
            [
                "urn:mail:current@example.com",
                "urn:mail:alternative@example.com"
            ]);

        Assert.Equal("urn:mail:current@example.com", result);
    }

    [Fact]
    public void SelectActiveDespatchReceiverAlias_RejectsMissingActiveAlias()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            EDespatchService.SelectActiveDespatchReceiverAlias(
                "urn:mail:old@example.com",
                [null, " "]));

        Assert.Contains("active e-despatch receiver alias", exception.Message);
    }
}

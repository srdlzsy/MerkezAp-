using FurpaMerkezApi.Infrastructure.Services;
using System.Xml.Linq;
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

    [Fact]
    public void BuildPartyPersonElement_CreatesRequiredPersonForTckn()
    {
        var result = EDespatchService.BuildPartyPersonElement("TCKN", "ILKER BUTUNER");

        Assert.NotNull(result);
        Assert.Equal("Person", result.Name.LocalName);
        Assert.Equal("ILKER", result.Elements().Single(element => element.Name.LocalName == "FirstName").Value);
        Assert.Equal("BUTUNER", result.Elements().Single(element => element.Name.LocalName == "FamilyName").Value);
    }

    [Fact]
    public void BuildPartyPersonElement_OmitsPersonForVkn()
    {
        Assert.Null(EDespatchService.BuildPartyPersonElement("VKN", "FIRMA UNVANI"));
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
    public void ResolveConsistentSentDespatchMarker_ReturnsNullWhenEveryLineIsUnmarked()
    {
        var result = EDespatchService.ResolveConsistentSentDespatchMarker(
        [
            (null, null),
            ("", "")
        ]);

        Assert.Null(result);
    }

    [Fact]
    public void ResolveConsistentSentDespatchMarker_ReturnsSharedMarkerWhenEveryLineMatches()
    {
        const string documentNo = "FRM2026000000123";
        const string uuid = "291b1f58-62b1-4615-8853-c1908e6a1d53";

        var result = EDespatchService.ResolveConsistentSentDespatchMarker(
        [
            (documentNo, uuid),
            (documentNo, uuid)
        ]);

        Assert.NotNull(result);
        Assert.Equal(documentNo, result.Value.DocumentNo);
        Assert.Equal(uuid, result.Value.Uuid);
    }

    [Fact]
    public void ResolveConsistentSentDespatchMarker_RejectsPartiallyMarkedDocument()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            EDespatchService.ResolveConsistentSentDespatchMarker(
            [
                ("FRM2026000000123", "291b1f58-62b1-4615-8853-c1908e6a1d53"),
                (null, null)
            ]));

        Assert.Contains("only present on some document lines", exception.Message);
    }

    [Fact]
    public void ResolveConsistentSentDespatchMarker_RejectsMultipleMarkers()
    {
        Assert.Throws<InvalidOperationException>(() =>
            EDespatchService.ResolveConsistentSentDespatchMarker(
            [
                ("FRM2026000000123", "291b1f58-62b1-4615-8853-c1908e6a1d53"),
                ("FRM2026000000124", "a55de149-3cfa-4e64-9194-da961bdf17c3")
            ]));
    }

    [Fact]
    public void HaveSameMovementGuids_RequiresTheCompleteSet()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        Assert.True(EDespatchService.HaveSameMovementGuids([first, second], [second, first]));
        Assert.False(EDespatchService.HaveSameMovementGuids([first], [first, second]));
        Assert.False(EDespatchService.HaveSameMovementGuids([first, second], [first]));
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

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

    [Fact]
    public void BuildContactElement_CreatesNamedDespatchContact()
    {
        var result = EDespatchService.BuildContactElement(
            "DespatchContact",
            "ZEHRA SAMUK",
            null,
            null,
            null,
            null);

        Assert.NotNull(result);
        Assert.Equal("DespatchContact", result.Name.LocalName);
        Assert.Equal("ZEHRA SAMUK", result.Elements().Single().Value);
        Assert.Equal("Name", result.Elements().Single().Name.LocalName);
    }

    [Theory]
    [InlineData("REQUEST EDEN", "REQUEST ALAN", "MIKRO EDEN", "MIKRO ALAN", "SOFOR ADI", "REQUEST EDEN", "REQUEST ALAN")]
    [InlineData(null, null, "MIKRO EDEN", "MIKRO ALAN", "SOFOR ADI", "MIKRO EDEN", "MIKRO ALAN")]
    [InlineData(null, null, null, null, "SOFOR ADI", "", "SOFOR ADI")]
    public void ResolveDespatchContactNames_UsesRequestThenMikroThenDriverFallback(
        string? requestDeliverer,
        string? requestReceiver,
        string? storedDeliverer,
        string? storedReceiver,
        string? driverNameSurname,
        string expectedDeliverer,
        string expectedReceiver)
    {
        var result = EDespatchService.ResolveDespatchContactNames(
            requestDeliverer,
            requestReceiver,
            storedDeliverer,
            storedReceiver,
            driverNameSurname);

        Assert.Equal(expectedDeliverer, result.Deliverer);
        Assert.Equal(expectedReceiver, result.Receiver);
    }

    [Fact]
    public void BuildDespatchLineElement_SendsBarcodeAndStockCodeSeparately()
    {
        var result = EDespatchService.BuildDespatchLineElement(
            1,
            "015550",
            "MNV SEFTALI KG",
            "2700174",
            "KG",
            4.11);

        var item = result.Elements().Single(element => element.Name.LocalName == "Item");
        Assert.Equal(
            "2700174",
            item.Elements().Single(element => element.Name.LocalName == "Description").Value);
        Assert.Equal(
            "015550",
            item.Descendants().Single(element => element.Name.LocalName == "ID").Value);
    }

    [Fact]
    public void BuildDespatchLineElement_UsesStockCodeWhenBarcodeIsMissing()
    {
        var result = EDespatchService.BuildDespatchLineElement(
            1,
            "015550",
            "MNV SEFTALI KG",
            string.Empty,
            "KG",
            1);

        var item = result.Elements().Single(element => element.Name.LocalName == "Item");
        Assert.Equal(
            "015550",
            item.Elements().Single(element => element.Name.LocalName == "Description").Value);
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
    public void ResolveConsistentSentDespatchMarker_IgnoresNonEDespatchDescriptionValues()
    {
        var result = EDespatchService.ResolveConsistentSentDespatchMarker(
        [
            (null, "D137.2107"),
            ("", "D137.2107")
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
    public void EnsureMovementMarkersMatchTrackedSubmission_AllowsMatchingPartialMarkers()
    {
        EDespatchService.EnsureMovementMarkersMatchTrackedSubmission(
        [
            ("FRM2026600131485", "928d1dc2-f814-48e1-9e62-1cbe980f0096"),
            (null, null),
            ("", "")
        ],
        "FRM2026600131485",
        "928d1dc2-f814-48e1-9e62-1cbe980f0096");
    }

    [Fact]
    public void EnsureMovementMarkersMatchTrackedSubmission_RejectsConflictingMarker()
    {
        Assert.Throws<InvalidOperationException>(() =>
            EDespatchService.EnsureMovementMarkersMatchTrackedSubmission(
            [
                ("FRM2026600131485", "928d1dc2-f814-48e1-9e62-1cbe980f0096"),
                ("FRM2026600131486", "a55de149-3cfa-4e64-9194-da961bdf17c3")
            ],
            "FRM2026600131485",
            "928d1dc2-f814-48e1-9e62-1cbe980f0096"));
    }


    [Fact]
    public void EnsureTrackedSubmissionMatchesDocument_AcceptsCompleteMatchingDocument()
    {
        EDespatchService.EnsureTrackedSubmissionMatchesDocument(
            "FRM2026600131485",
            "928d1dc2-f814-48e1-9e62-1cbe980f0096",
            26,
            "FRM2026600131485",
            "928d1dc2-f814-48e1-9e62-1cbe980f0096",
            26);
    }

    [Fact]
    public void EnsureTrackedSubmissionMatchesDocument_RejectsPartialUyumsoftDocument()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            EDespatchService.EnsureTrackedSubmissionMatchesDocument(
                "FRM2026600131485",
                "928d1dc2-f814-48e1-9e62-1cbe980f0096",
                21,
                "FRM2026600131485",
                "928d1dc2-f814-48e1-9e62-1cbe980f0096",
                26));

        Assert.Contains("Expected 26 lines, Uyumsoft returned 21", exception.Message);
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
    public void TryParseOfflineTraceKey_RestoresClientRequestId()
    {
        var requestId = Guid.NewGuid();
        var traceKey = "FR" + Convert.ToBase64String(requestId.ToByteArray())
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        Assert.True(EDespatchService.TryParseOfflineTraceKey(traceKey, out var parsed));
        Assert.Equal(requestId, parsed);
        Assert.False(EDespatchService.TryParseOfflineTraceKey("FRinvalid", out _));
    }

    [Fact]
    public void MatchesCompletedShipmentCreate_RejectsPartialDocument()
    {
        var response = new FurpaMerkezApi.Application.Modules.SevkIslemleri.DepolarArasiSevkler.Create.CreateInterWarehouseShipmentResponse(
            "F56", 87879, DateTime.Today, DateTime.Today, "", 56, 113, 60, 25, 0, 0, 0, "MikroWriteConnection");

        Assert.True(EDespatchService.MatchesCompletedShipmentCreate(response, "F56", 87879, 56, 25));
        Assert.False(EDespatchService.MatchesCompletedShipmentCreate(response, "F56", 87879, 56, 15));
    }

    [Fact]
    public void MatchesCompletedDocumentCreate_RejectsPartialWarehouseReturn()
    {
        Assert.True(EDespatchService.MatchesCompletedDocumentCreate(
            "F56", 87880, 56, 25, "F56", 87880, 56, 25));
        Assert.False(EDespatchService.MatchesCompletedDocumentCreate(
            "F56", 87880, 56, 25, "F56", 87880, 56, 15));
    }

    [Fact]
    public void ToEndpointOptions_UsesConfiguredDespatchTimeout()
    {
        var config = new EDespatchOptions(
            "https://example.test/despatch", "user", "password", "supplier",
            "profile", "SEVK", "TR", "TURKIYE", 360);

        Assert.Equal(360, EDespatchService.ToEndpointOptions(config).TimeoutSeconds);
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

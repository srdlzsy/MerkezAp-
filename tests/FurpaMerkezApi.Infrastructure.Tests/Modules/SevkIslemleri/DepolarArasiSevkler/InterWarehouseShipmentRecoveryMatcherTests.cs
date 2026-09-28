using FurpaMerkezApi.Application.Modules.SevkIslemleri.DepolarArasiSevkler.Create;
using FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.DepolarArasiSevkler.Create;
using FurpaMerkezApi.Infrastructure.OfflineSync;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.SevkIslemleri.DepolarArasiSevkler;

public sealed class InterWarehouseShipmentRecoveryMatcherTests
{
    [Fact]
    public void Matches_ReturnsTrueWhenDocumentLinesAndClientTraceMatch()
    {
        var clientRequestId = Guid.Parse("a94669b8-b916-474a-9830-bbf87cc9f408");
        var request = CreateRequest(clientRequestId);
        var rows = CreateMatchingRows(MobileOfflineSyncService.ToTraceKey(clientRequestId));

        var matches = InterWarehouseShipmentRecoveryMatcher.Matches(
            request,
            request.Lines.ToArray(),
            rows);

        Assert.True(matches);
    }

    [Fact]
    public void Matches_ReturnsFalseWhenSequenceBelongsToAnotherClientRequest()
    {
        var request = CreateRequest(Guid.Parse("a94669b8-b916-474a-9830-bbf87cc9f408"));
        var rows = CreateMatchingRows("FRDIFFERENTTRACE0000000");

        var matches = InterWarehouseShipmentRecoveryMatcher.Matches(
            request,
            request.Lines.ToArray(),
            rows);

        Assert.False(matches);
    }

    [Fact]
    public void Matches_ReturnsFalseWhenSequenceContainsDifferentMovementLines()
    {
        var request = CreateRequest(null);
        var rows = CreateMatchingRows(string.Empty);
        rows[1] = rows[1] with { Quantity = 99d };

        var matches = InterWarehouseShipmentRecoveryMatcher.Matches(
            request,
            request.Lines.ToArray(),
            rows);

        Assert.False(matches);
    }

    [Fact]
    public void Matches_ReturnsFalseWhenSequenceContainsExtraLines()
    {
        var request = CreateRequest(null);
        var rows = CreateMatchingRows(string.Empty).ToList();
        rows.Add(rows[1] with { RowNo = 2 });

        var matches = InterWarehouseShipmentRecoveryMatcher.Matches(
            request,
            request.Lines.ToArray(),
            rows);

        Assert.False(matches);
    }

    private static CreateInterWarehouseShipmentRequest CreateRequest(Guid? clientRequestId) =>
        new(
            56,
            120,
            60,
            new DateTime(2026, 9, 28),
            new DateTime(2026, 9, 28),
            "TEST-1",
            "Sevk",
            [
                new CreateInterWarehouseShipmentLineRequest(
                    "001082",
                    12.5d,
                    UnitPrice: 20d,
                    UnitPointer: 1,
                    Description: "Satir 1",
                    PartyCode: "P1",
                    LotNo: 3,
                    ProjectCode: "PRJ",
                    CustomerResponsibilityCenter: "CSR",
                    ProductResponsibilityCenter: "SSR"),
                new CreateInterWarehouseShipmentLineRequest(
                    "016167",
                    25d,
                    UnitPrice: 2d,
                    UnitPointer: 2,
                    Description: "Satir 2")
            ],
            ClientRequestId: clientRequestId);

    private static InterWarehouseShipmentRecoveryLine[] CreateMatchingRows(string traceKey) =>
    [
        new InterWarehouseShipmentRecoveryLine(
            0,
            "001082",
            12.5d,
            1,
            250d,
            "Satir 1",
            "P1",
            3,
            "PRJ",
            "CSR",
            "SSR",
            traceKey),
        new InterWarehouseShipmentRecoveryLine(
            1,
            "016167",
            25d,
            2,
            50d,
            "Satir 2",
            null,
            0,
            null,
            null,
            null,
            traceKey)
    ];
}

using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Create;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Create;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.SiparisIslemleri.VerilenDepoSiparisleri;

public sealed class IssuedWarehouseOrderRecoveryMatcherTests
{
    private static readonly DateTime OrderDate = new(2026, 10, 14);

    [Fact]
    public void Match_ReturnsComplete_WhenEveryLineMatches()
    {
        var (request, lines) = CreateRequest();
        var rows = lines.Select((line, index) => CreateRow(line, index)).ToArray();

        var result = IssuedWarehouseOrderRecoveryMatcher.Match(request, lines, rows);

        Assert.Equal(IssuedWarehouseOrderMatch.Complete, result);
    }

    [Fact]
    public void Match_ReturnsIncomplete_WhenMatchingLinesAreStillBeingWritten()
    {
        var (request, lines) = CreateRequest();
        var rows = new[] { CreateRow(lines[0], 0) };

        var result = IssuedWarehouseOrderRecoveryMatcher.Match(request, lines, rows);

        Assert.Equal(IssuedWarehouseOrderMatch.Incomplete, result);
    }

    [Fact]
    public void Match_ReturnsMismatch_WhenExistingDocumentHasDifferentQuantity()
    {
        var (request, lines) = CreateRequest();
        var rows = lines.Select((line, index) => CreateRow(line, index)).ToArray();
        rows[1] = rows[1] with { Quantity = 999d };

        var result = IssuedWarehouseOrderRecoveryMatcher.Match(request, lines, rows);

        Assert.Equal(IssuedWarehouseOrderMatch.Mismatch, result);
    }

    [Fact]
    public void Match_ReturnsMismatch_WhenDocumentContainsDuplicateRowNumber()
    {
        var (request, lines) = CreateRequest();
        var rows = lines.Select(line => CreateRow(line, 0)).ToArray();

        var result = IssuedWarehouseOrderRecoveryMatcher.Match(request, lines, rows);

        Assert.Equal(IssuedWarehouseOrderMatch.Mismatch, result);
    }

    private static (CreateIssuedWarehouseOrderRequest Request, CreateIssuedWarehouseOrderLineRequest[] Lines)
        CreateRequest()
    {
        CreateIssuedWarehouseOrderLineRequest[] lines =
        [
            new("001", 3d, RecommendedQuantity: 4d, UnitPrice: 10d, UnitPointer: 1,
                PackageCode: "PK1", ProjectCode: "PR1", ResponsibilityCenter: "SRM1"),
            new("002", 5d, UnitPrice: 20d, UnitPointer: 2)
        ];
        return (new CreateIssuedWarehouseOrderRequest(119, 56, OrderDate, OrderDate.AddDays(1), null, lines), lines);
    }

    private static IssuedWarehouseOrderRecoveryRow CreateRow(
        CreateIssuedWarehouseOrderLineRequest line,
        int rowNo) =>
        new(
            OrderDate,
            OrderDate.AddDays(1),
            "F119",
            3961,
            rowNo,
            line.StockCode,
            line.Quantity,
            line.UnitPrice,
            line.Quantity * line.UnitPrice,
            119,
            56,
            (byte)line.UnitPointer,
            line.PackageCode,
            line.ProjectCode,
            line.ResponsibilityCenter,
            line.RecommendedQuantity ?? 0d,
            false);
}

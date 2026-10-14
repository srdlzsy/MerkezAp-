using FurpaMerkezApi.Application.Modules.IadeIslemleri.DepoIadeleri.Create;
using FurpaMerkezApi.Infrastructure.Modules.Common;
using FurpaMerkezApi.Infrastructure.Modules.IadeIslemleri.DepoIadeleri.Create;
using FurpaMerkezApi.Infrastructure.OfflineSync;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.Common;

public sealed class WarehouseReturnRecoveryMatcherTests
{
    private static readonly Guid RequestId = Guid.NewGuid();
    private static readonly CreateWarehouseReturnLineRequest[] Lines =
    [
        new("A", 2, 10, 1, "Line A", "P1", 1, "PRJ", "CRM", "SRM"),
        new("B", 3, 20, 2)
    ];
    private static readonly CreateWarehouseReturnRequest Request = new(
        147, 51, 0, DateTime.Today, DateTime.Today, null, "Header", Lines, RequestId);

    [Fact]
    public void Match_RequiresEveryExpectedRow()
    {
        var rows = new[] { Row(0), Row(1) };

        Assert.Equal(StockMovementMatch.Missing, WarehouseReturnRecoveryMatcher.Match(Request, Lines, []));
        Assert.Equal(StockMovementMatch.Incomplete, WarehouseReturnRecoveryMatcher.Match(Request, Lines, [rows[0]]));
        Assert.Equal(StockMovementMatch.Complete, WarehouseReturnRecoveryMatcher.Match(Request, Lines, rows));
    }

    [Theory]
    [InlineData("stock")]
    [InlineData("quantity")]
    [InlineData("unit")]
    [InlineData("amount")]
    [InlineData("trace")]
    [InlineData("row")]
    public void Match_RejectsDifferentContent(string field)
    {
        var rows = new[] { Row(0), Row(1) };
        rows[0] = field switch
        {
            "stock" => rows[0] with { StockCode = "OTHER" },
            "quantity" => rows[0] with { Quantity = 9 },
            "unit" => rows[0] with { UnitPointer = 2 },
            "amount" => rows[0] with { Amount = 1 },
            "trace" => rows[0] with { TraceKey = "OTHER" },
            "row" => rows[0] with { RowNo = 1 },
            _ => rows[0]
        };

        Assert.Equal(StockMovementMatch.Mismatch, WarehouseReturnRecoveryMatcher.Match(Request, Lines, rows));
    }

    private static WarehouseReturnRecoveryLine Row(int rowNo)
    {
        var line = Lines[rowNo];
        return new WarehouseReturnRecoveryLine(
            rowNo,
            line.StockCode,
            line.Quantity,
            line.UnitPointer,
            line.Quantity * line.UnitPrice,
            line.Description ?? Request.Description,
            line.PartyCode,
            line.LotNo,
            line.ProjectCode,
            line.CustomerResponsibilityCenter,
            line.ProductResponsibilityCenter,
            MobileOfflineSyncService.ToTraceKey(RequestId));
    }
}

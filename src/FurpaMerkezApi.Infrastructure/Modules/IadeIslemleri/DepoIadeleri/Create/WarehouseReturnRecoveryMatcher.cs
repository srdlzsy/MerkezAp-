using FurpaMerkezApi.Application.Modules.IadeIslemleri.DepoIadeleri.Create;
using FurpaMerkezApi.Infrastructure.Modules.Common;
using FurpaMerkezApi.Infrastructure.OfflineSync;

namespace FurpaMerkezApi.Infrastructure.Modules.IadeIslemleri.DepoIadeleri.Create;

internal static class WarehouseReturnRecoveryMatcher
{
    internal static StockMovementMatch Match(
        CreateWarehouseReturnRequest request,
        IReadOnlyList<CreateWarehouseReturnLineRequest> expectedLines,
        IReadOnlyCollection<WarehouseReturnRecoveryLine> actualRows)
    {
        if (actualRows.Count == 0)
        {
            return StockMovementMatch.Missing;
        }

        if (actualRows.Count > expectedLines.Count)
        {
            return StockMovementMatch.Mismatch;
        }

        var rowsByRowNo = actualRows
            .Where(row => row.RowNo.HasValue)
            .GroupBy(row => row.RowNo!.Value)
            .ToDictionary(group => group.Key, group => group.ToArray());

        if (rowsByRowNo.Count != actualRows.Count ||
            rowsByRowNo.Values.Any(group => group.Length != 1))
        {
            return StockMovementMatch.Mismatch;
        }

        var expectedTraceKey = request.ClientRequestId.HasValue
            ? MobileOfflineSyncService.ToTraceKey(request.ClientRequestId.Value)
            : string.Empty;

        foreach (var (rowNo, rows) in rowsByRowNo)
        {
            if (rowNo < 0 || rowNo >= expectedLines.Count)
            {
                return StockMovementMatch.Mismatch;
            }

            var expected = expectedLines[rowNo];
            var actual = rows[0];
            var expectedDescription = expected.Description ?? request.Description;

            if (!TextEquals(actual.StockCode, expected.StockCode) ||
                !NearlyEquals(actual.Quantity, expected.Quantity) ||
                actual.UnitPointer != expected.UnitPointer ||
                !NearlyEquals(actual.Amount, expected.Quantity * expected.UnitPrice) ||
                !DescriptionMatches(actual.Description, expectedDescription) ||
                !TextEquals(actual.PartyCode, expected.PartyCode) ||
                actual.LotNo != expected.LotNo ||
                !TextEquals(actual.ProjectCode, expected.ProjectCode) ||
                !TextEquals(actual.CustomerResponsibilityCenter, expected.CustomerResponsibilityCenter) ||
                !TextEquals(actual.ProductResponsibilityCenter, expected.ProductResponsibilityCenter) ||
                (!string.IsNullOrEmpty(expectedTraceKey) && !TextEquals(actual.TraceKey, expectedTraceKey)))
            {
                return StockMovementMatch.Mismatch;
            }
        }

        return actualRows.Count == expectedLines.Count
            ? StockMovementMatch.Complete
            : StockMovementMatch.Incomplete;
    }

    private static bool NearlyEquals(double actual, double expected) =>
        Math.Abs(actual - expected) <= 0.0001d;

    private static bool TextEquals(string? actual, string? expected) =>
        string.Equals(NormalizeText(actual), NormalizeText(expected), StringComparison.OrdinalIgnoreCase);

    private static bool DescriptionMatches(string? actual, string? expected)
    {
        if (TextEquals(actual, expected))
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(expected) &&
               Guid.TryParse(NormalizeText(actual), out var generatedOrderLineGuid) &&
               generatedOrderLineGuid != Guid.Empty;
    }

    private static string NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}

internal sealed record WarehouseReturnRecoveryLine(
    int? RowNo,
    string? StockCode,
    double Quantity,
    int UnitPointer,
    double Amount,
    string? Description,
    string? PartyCode,
    int LotNo,
    string? ProjectCode,
    string? CustomerResponsibilityCenter,
    string? ProductResponsibilityCenter,
    string? TraceKey);

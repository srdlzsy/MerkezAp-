using FurpaMerkezApi.Application.Modules.SevkIslemleri.DepolarArasiSevkler.Create;
using FurpaMerkezApi.Infrastructure.OfflineSync;

namespace FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.DepolarArasiSevkler.Create;

internal static class InterWarehouseShipmentRecoveryMatcher
{
    internal static bool Matches(
        CreateInterWarehouseShipmentRequest request,
        IReadOnlyList<CreateInterWarehouseShipmentLineRequest> expectedLines,
        IReadOnlyCollection<InterWarehouseShipmentRecoveryLine> actualRows)
    {
        var rowsByRowNo = actualRows
            .Where(row => row.RowNo.HasValue)
            .GroupBy(row => row.RowNo!.Value)
            .ToDictionary(group => group.Key, group => group.ToArray());

        if (actualRows.Count != expectedLines.Count ||
            rowsByRowNo.Count != expectedLines.Count ||
            rowsByRowNo.Values.Any(group => group.Length != 1))
        {
            return false;
        }

        var expectedTraceKey = request.ClientRequestId.HasValue
            ? MobileOfflineSyncService.ToTraceKey(request.ClientRequestId.Value)
            : string.Empty;

        for (var rowNo = 0; rowNo < expectedLines.Count; rowNo++)
        {
            if (!rowsByRowNo.TryGetValue(rowNo, out var matchingRows))
            {
                return false;
            }

            var expectedLine = expectedLines[rowNo];
            var actualRow = matchingRows[0];
            var expectedAmount = expectedLine.Quantity * expectedLine.UnitPrice;
            var expectedDescription = expectedLine.Description ?? request.Description;

            if (!TextEquals(actualRow.StockCode, expectedLine.StockCode) ||
                !NearlyEquals(actualRow.Quantity, expectedLine.Quantity) ||
                actualRow.UnitPointer != expectedLine.UnitPointer ||
                !NearlyEquals(actualRow.Amount, expectedAmount) ||
                !TextEquals(actualRow.Description, expectedDescription) ||
                !TextEquals(actualRow.PartyCode, expectedLine.PartyCode) ||
                actualRow.LotNo != expectedLine.LotNo ||
                !TextEquals(actualRow.ProjectCode, expectedLine.ProjectCode) ||
                !TextEquals(actualRow.CustomerResponsibilityCenter, expectedLine.CustomerResponsibilityCenter) ||
                !TextEquals(actualRow.ProductResponsibilityCenter, expectedLine.ProductResponsibilityCenter) ||
                (!string.IsNullOrEmpty(expectedTraceKey) && !TextEquals(actualRow.TraceKey, expectedTraceKey)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool NearlyEquals(double actual, double expected) =>
        Math.Abs(actual - expected) <= 0.0001d;

    private static bool TextEquals(string? actual, string? expected) =>
        string.Equals(NormalizeText(actual), NormalizeText(expected), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}

internal sealed record InterWarehouseShipmentRecoveryLine(
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

using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Create;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Create;

internal enum IssuedWarehouseOrderMatch { Missing, Incomplete, Complete, Mismatch }

internal sealed record IssuedWarehouseOrderRecoveryRow(
    DateTime? OrderDate,
    DateTime? DeliveryDate,
    string? DocumentSerie,
    int? DocumentOrderNo,
    int? RowNo,
    string? StockCode,
    double? Quantity,
    double? UnitPrice,
    double? Amount,
    int? InWarehouseNo,
    int? OutWarehouseNo,
    byte? UnitPointer,
    string? PackageCode,
    string? ProjectCode,
    string? ResponsibilityCenter,
    double? RecommendedQuantity,
    bool? IsCancelled);

internal static class IssuedWarehouseOrderRecoveryMatcher
{
    internal static IssuedWarehouseOrderMatch Match(
        CreateIssuedWarehouseOrderRequest request,
        IReadOnlyList<CreateIssuedWarehouseOrderLineRequest> expectedLines,
        IReadOnlyCollection<IssuedWarehouseOrderRecoveryRow> actualRows)
    {
        if (actualRows.Count == 0)
        {
            return IssuedWarehouseOrderMatch.Missing;
        }

        if (actualRows.Count > expectedLines.Count ||
            actualRows.Any(row => !row.RowNo.HasValue) ||
            actualRows.Select(row => row.RowNo).Distinct().Count() != actualRows.Count ||
            actualRows.Select(row => new
            {
                row.DocumentSerie,
                row.DocumentOrderNo,
                row.OrderDate,
                row.InWarehouseNo,
                row.OutWarehouseNo
            }).Distinct().Count() != 1)
        {
            return IssuedWarehouseOrderMatch.Mismatch;
        }

        var expectedOrderDate = (request.OrderDate ?? DateTime.Today).Date;
        var expectedDeliveryDate = (request.DeliveryDate ?? expectedOrderDate).Date;
        foreach (var actual in actualRows)
        {
            var rowNo = actual.RowNo!.Value;
            if (rowNo < 0 || rowNo >= expectedLines.Count)
            {
                return IssuedWarehouseOrderMatch.Mismatch;
            }

            var expected = expectedLines[rowNo];
            if (actual.IsCancelled == true ||
                actual.OrderDate?.Date != expectedOrderDate ||
                actual.DeliveryDate?.Date != expectedDeliveryDate ||
                actual.InWarehouseNo != request.InWarehouseNo ||
                actual.OutWarehouseNo != request.OutWarehouseNo ||
                !TextEquals(actual.StockCode, expected.StockCode) ||
                !NearlyEquals(actual.Quantity, expected.Quantity) ||
                !NearlyEquals(actual.UnitPrice, expected.UnitPrice) ||
                !NearlyEquals(actual.Amount, expected.Quantity * expected.UnitPrice) ||
                actual.UnitPointer != expected.UnitPointer)
            {
                return IssuedWarehouseOrderMatch.Mismatch;
            }
        }

        return actualRows.Count == expectedLines.Count
            ? IssuedWarehouseOrderMatch.Complete
            : IssuedWarehouseOrderMatch.Incomplete;
    }

    private static bool NearlyEquals(double? actual, double expected) =>
        actual.HasValue && double.IsFinite(actual.Value) && Math.Abs(actual.Value - expected) <= 0.0001d;

    private static bool TextEquals(string? actual, string? expected) =>
        string.Equals(
            actual?.Trim() ?? string.Empty,
            expected?.Trim() ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
}

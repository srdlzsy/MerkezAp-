using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Create;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Create;

internal enum IssuedCompanyOrderMatch { Missing, Incomplete, Complete, Mismatch }

internal sealed record IssuedCompanyOrderRecoveryRow(
    DateTime? OrderDate,
    DateTime? DeliveryDate,
    string? DocumentSerie,
    int? DocumentOrderNo,
    int? RowNo,
    int? WarehouseNo,
    string? CustomerCode,
    string? StockCode,
    double? UnitPrice,
    double? Quantity,
    double? Amount,
    byte? UnitPointer,
    string? Description1,
    string? Description2,
    string? CustomerResponsibilityCenter,
    string? ProductResponsibilityCenter,
    string? ProjectCode,
    string? PackageCode,
    double? RecommendedQuantity,
    bool? IsCancelled);

internal static class IssuedCompanyOrderRecoveryMatcher
{
    internal static IssuedCompanyOrderMatch Match(
        CreateIssuedCompanyOrderRequest request,
        IReadOnlyList<CreateIssuedCompanyOrderLineRequest> expectedLines,
        IReadOnlyCollection<IssuedCompanyOrderRecoveryRow> actualRows)
    {
        if (actualRows.Count == 0)
        {
            return IssuedCompanyOrderMatch.Missing;
        }

        if (actualRows.Count > expectedLines.Count ||
            actualRows.Any(row => !row.RowNo.HasValue) ||
            actualRows.Select(row => row.RowNo).Distinct().Count() != actualRows.Count ||
            actualRows.Select(row => new
            {
                row.DocumentSerie,
                row.DocumentOrderNo,
                row.OrderDate,
                row.WarehouseNo,
                row.CustomerCode
            }).Distinct().Count() != 1)
        {
            return IssuedCompanyOrderMatch.Mismatch;
        }

        var expectedOrderDate = (request.OrderDate ?? DateTime.Today).Date;
        var expectedDeliveryDate = request.DeliveryDate.Date;
        foreach (var actual in actualRows)
        {
            var rowNo = actual.RowNo!.Value;
            if (rowNo < 0 || rowNo >= expectedLines.Count)
            {
                return IssuedCompanyOrderMatch.Mismatch;
            }

            var expected = expectedLines[rowNo];
            if (actual.IsCancelled == true ||
                actual.OrderDate?.Date != expectedOrderDate ||
                actual.DeliveryDate?.Date != expectedDeliveryDate ||
                actual.WarehouseNo != request.WarehouseNo ||
                !TextEquals(actual.CustomerCode, Normalize(request.CustomerCode, 25)) ||
                !TextEquals(actual.StockCode, Normalize(expected.StockCode, 25)) ||
                !NearlyEquals(actual.UnitPrice, expected.UnitPrice) ||
                !NearlyEquals(actual.Quantity, expected.Quantity) ||
                !NearlyEquals(actual.Amount, expected.Quantity * expected.UnitPrice) ||
                actual.UnitPointer != expected.UnitPointer ||
                !TextEquals(actual.Description1, Normalize(expected.Description1 ?? request.Description1, 50)) ||
                !TextEquals(actual.Description2, Normalize(expected.Description2 ?? request.Description2, 50)) ||
                !TextEquals(actual.CustomerResponsibilityCenter, Normalize(expected.CustomerResponsibilityCenter, 25)) ||
                !TextEquals(actual.ProductResponsibilityCenter, Normalize(expected.ProductResponsibilityCenter, 25)) ||
                !TextEquals(actual.ProjectCode, Normalize(expected.ProjectCode, 25)) ||
                !TextEquals(actual.PackageCode, Normalize(expected.PackageCode, 25)) ||
                !NearlyEquals(actual.RecommendedQuantity, expected.RecommendedQuantity ?? 0d))
            {
                return IssuedCompanyOrderMatch.Mismatch;
            }
        }

        return actualRows.Count == expectedLines.Count
            ? IssuedCompanyOrderMatch.Complete
            : IssuedCompanyOrderMatch.Incomplete;
    }

    private static bool NearlyEquals(double? actual, double expected) =>
        actual.HasValue && double.IsFinite(actual.Value) && Math.Abs(actual.Value - expected) <= 0.0001d;

    private static bool TextEquals(string? actual, string? expected) =>
        string.Equals(
            actual?.Trim() ?? string.Empty,
            expected?.Trim() ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}

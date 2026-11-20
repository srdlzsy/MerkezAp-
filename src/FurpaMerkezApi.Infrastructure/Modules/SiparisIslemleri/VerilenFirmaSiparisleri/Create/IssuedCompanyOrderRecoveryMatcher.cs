using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Create;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Create;

using FurpaMerkezApi.Infrastructure.Modules.Common.PurchasePricing;

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
    double? Discount1,
    double? Discount2,
    double? Discount3,
    double? Discount4,
    double? Discount5,
    double? Discount6,
    double? Expense1,
    double? Expense2,
    double? Expense3,
    double? Expense4,
    byte? TaxPointer,
    double? TaxAmount,
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
            var expectedSnapshot = expected.PurchaseFinancial ?? PurchaseFinancialCalculator.FromNetUnitPrice(
                Convert.ToDecimal(expected.UnitPrice),
                0,
                0m);
            var expectedFinancial = PurchaseFinancialCalculator.Calculate(
                expectedSnapshot,
                Convert.ToDecimal(expected.Quantity));
            if (actual.IsCancelled == true ||
                actual.OrderDate?.Date != expectedOrderDate ||
                actual.DeliveryDate?.Date != expectedDeliveryDate ||
                actual.WarehouseNo != request.WarehouseNo ||
                !TextEquals(actual.CustomerCode, Normalize(request.CustomerCode, 25)) ||
                !TextEquals(actual.StockCode, Normalize(expected.StockCode, 25)) ||
                !NearlyEquals(actual.UnitPrice, Convert.ToDouble(expectedSnapshot.GrossUnitPrice)) ||
                !NearlyEquals(actual.Quantity, expected.Quantity) ||
                !NearlyEquals(actual.Amount, Convert.ToDouble(expectedFinancial.GrossAmount)) ||
                !NearlyEquals(actual.Discount1, Convert.ToDouble(expectedFinancial.DiscountAmounts[0])) ||
                !NearlyEquals(actual.Discount2, Convert.ToDouble(expectedFinancial.DiscountAmounts[1])) ||
                !NearlyEquals(actual.Discount3, Convert.ToDouble(expectedFinancial.DiscountAmounts[2])) ||
                !NearlyEquals(actual.Discount4, Convert.ToDouble(expectedFinancial.DiscountAmounts[3])) ||
                !NearlyEquals(actual.Discount5, Convert.ToDouble(expectedFinancial.DiscountAmounts[4])) ||
                !NearlyEquals(actual.Discount6, Convert.ToDouble(expectedFinancial.DiscountAmounts[5])) ||
                !NearlyEquals(actual.Expense1, Convert.ToDouble(expectedFinancial.ExpenseAmounts[0])) ||
                !NearlyEquals(actual.Expense2, Convert.ToDouble(expectedFinancial.ExpenseAmounts[1])) ||
                !NearlyEquals(actual.Expense3, Convert.ToDouble(expectedFinancial.ExpenseAmounts[2])) ||
                !NearlyEquals(actual.Expense4, Convert.ToDouble(expectedFinancial.ExpenseAmounts[3])) ||
                actual.TaxPointer != expectedFinancial.TaxPointer ||
                !NearlyEquals(actual.TaxAmount, Convert.ToDouble(expectedFinancial.TaxAmount)) ||
                actual.UnitPointer != expected.UnitPointer)
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

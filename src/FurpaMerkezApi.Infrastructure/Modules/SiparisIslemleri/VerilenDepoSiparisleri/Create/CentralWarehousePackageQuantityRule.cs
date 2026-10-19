namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Create;

internal static class CentralWarehousePackageQuantityRule
{
    internal const int WarehouseNo = 50;
    private const double Tolerance = 0.000001d;

    internal static CentralWarehousePackageQuantityViolation? Validate(
        string stockCode,
        double quantity,
        double packageFactor)
    {
        var normalizedPackageFactor = Math.Abs(packageFactor);

        if (!double.IsFinite(quantity) ||
            !double.IsFinite(normalizedPackageFactor) ||
            quantity <= 0d ||
            normalizedPackageFactor <= 1d)
        {
            return null;
        }

        var packageCount = quantity / normalizedPackageFactor;
        if (Math.Abs(packageCount - Math.Round(packageCount)) <= Tolerance)
        {
            return null;
        }

        var lowerQuantity = Math.Floor(packageCount) * normalizedPackageFactor;
        var upperQuantity = Math.Ceiling(packageCount) * normalizedPackageFactor;

        return new CentralWarehousePackageQuantityViolation(
            stockCode.Trim(),
            quantity,
            normalizedPackageFactor,
            lowerQuantity > Tolerance ? lowerQuantity : null,
            upperQuantity);
    }
}

internal sealed record CentralWarehousePackageQuantityViolation(
    string StockCode,
    double Quantity,
    double PackageFactor,
    double? LowerQuantity,
    double UpperQuantity);

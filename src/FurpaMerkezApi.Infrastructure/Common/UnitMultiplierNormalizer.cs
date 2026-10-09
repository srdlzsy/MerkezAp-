namespace FurpaMerkezApi.Infrastructure.Common;

internal static class UnitMultiplierNormalizer
{
    internal static double Normalize(double? value, double fallback = 0d)
    {
        if (!value.HasValue || !double.IsFinite(value.Value))
        {
            return fallback;
        }

        var normalized = Math.Abs(value.Value);
        return normalized > 0d ? normalized : fallback;
    }

    internal static double ResolvePackageMultiplier(
        double? primaryUnitMultiplier,
        string? secondaryUnitName,
        double? secondaryUnitMultiplier)
    {
        var normalizedSecondary = Normalize(secondaryUnitMultiplier);
        if (!string.IsNullOrWhiteSpace(secondaryUnitName) && normalizedSecondary > 1d)
        {
            return normalizedSecondary;
        }

        return Normalize(primaryUnitMultiplier, 1d);
    }

    internal static bool IsPackageUnit(
        int? unitPointer,
        string? unitName,
        double? unitMultiplier) =>
        unitPointer.GetValueOrDefault(1) > 1 &&
        !string.IsNullOrWhiteSpace(unitName) &&
        Normalize(unitMultiplier) > 1d;
}

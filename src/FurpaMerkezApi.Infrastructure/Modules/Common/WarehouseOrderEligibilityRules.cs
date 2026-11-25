namespace FurpaMerkezApi.Infrastructure.Modules.Common;

internal static class WarehouseOrderEligibilityRules
{
    public static bool IsWarehouseOrderContext(string? operationType) =>
        string.Equals(operationType?.Trim(), "order", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(operationType?.Trim(), "warehouse-order", StringComparison.OrdinalIgnoreCase);

    public static WarehouseOrderEligibilityDecision Evaluate(
        int targetWarehouseNo,
        int? sourceWarehouseNo,
        bool isPassive,
        bool isDelisted,
        bool isOrderBlocked,
        bool isAllowedForSourceWarehouse)
    {
        if (isPassive)
        {
            return Blocked(
                "ProductPassive",
                $"Urun hedef depo {targetWarehouseNo} icin pasif; depo siparisine eklenemez.");
        }

        if (isDelisted)
        {
            return Blocked("ProductDelisted", "DLS urun depo siparisine eklenemez.");
        }

        if (isOrderBlocked)
        {
            return Blocked(
                "OrderBlocked",
                $"Urun hedef depo {targetWarehouseNo} icin siparise kapali.");
        }

        if (!isAllowedForSourceWarehouse)
        {
            return Blocked(
                "SourceCatalogUnavailable",
                sourceWarehouseNo is > 0
                    ? $"Urun kaynak depo {sourceWarehouseNo} siparis katalogunda bulunmuyor veya kaynak/hedef depoda siparise kapali."
                    : "Urun secilen kaynak depo siparis katalogunda bulunmuyor.");
        }

        return new WarehouseOrderEligibilityDecision(true, null, null);
    }

    public static bool IsDelisted(string? stockName) =>
        stockName?.TrimStart().StartsWith("DLS", StringComparison.OrdinalIgnoreCase) == true;

    private static WarehouseOrderEligibilityDecision Blocked(string code, string reason) =>
        new(false, code, reason);
}

internal sealed record WarehouseOrderEligibilityDecision(
    bool IsEligible,
    string? Code,
    string? Reason);

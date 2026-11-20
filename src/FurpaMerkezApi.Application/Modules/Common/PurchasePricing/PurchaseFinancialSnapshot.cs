namespace FurpaMerkezApi.Application.Modules.Common.PurchasePricing;

public sealed record PurchaseFinancialSnapshot(
    Guid? PurchaseConditionId,
    string Source,
    decimal GrossUnitPrice,
    decimal NetUnitPrice,
    IReadOnlyList<decimal> DiscountRates,
    IReadOnlyList<decimal> DiscountUnitAmounts,
    IReadOnlyList<decimal> ExpenseRates,
    IReadOnlyList<decimal> ExpenseUnitAmounts,
    int TaxPointer,
    decimal TaxRatePercent);

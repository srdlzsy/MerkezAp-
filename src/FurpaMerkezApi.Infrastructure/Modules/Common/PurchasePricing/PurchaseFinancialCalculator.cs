using FurpaMerkezApi.Application.Modules.Common.PurchasePricing;

namespace FurpaMerkezApi.Infrastructure.Modules.Common.PurchasePricing;

internal static class PurchaseFinancialCalculator
{
    private const int DiscountCount = 6;
    private const int ExpenseCount = 4;

    internal static PurchaseFinancialSnapshot Normalize(PurchaseFinancialSnapshot snapshot)
    {
        var grossUnitPrice = NonNegative(snapshot.GrossUnitPrice);
        var discountRates = NormalizeValues(snapshot.DiscountRates, DiscountCount);
        var expenseRates = NormalizeValues(snapshot.ExpenseRates, ExpenseCount);
        var discountAmounts = ResolveDiscountAmounts(
            grossUnitPrice,
            discountRates,
            NormalizeValues(snapshot.DiscountUnitAmounts, DiscountCount));
        var discountedUnitPrice = Math.Max(0m, grossUnitPrice - discountAmounts.Sum());
        var expenseAmounts = ResolveExpenseAmounts(
            discountedUnitPrice,
            expenseRates,
            NormalizeValues(snapshot.ExpenseUnitAmounts, ExpenseCount));
        var netUnitPrice = Math.Max(0m, discountedUnitPrice + expenseAmounts.Sum());

        return snapshot with
        {
            GrossUnitPrice = grossUnitPrice,
            NetUnitPrice = netUnitPrice,
            DiscountRates = discountRates,
            DiscountUnitAmounts = discountAmounts,
            ExpenseRates = expenseRates,
            ExpenseUnitAmounts = expenseAmounts,
            TaxPointer = Math.Max(0, snapshot.TaxPointer),
            TaxRatePercent = NonNegative(snapshot.TaxRatePercent)
        };
    }

    internal static PurchaseFinancialAmounts Calculate(
        PurchaseFinancialSnapshot snapshot,
        decimal quantity)
    {
        var normalized = Normalize(snapshot);
        var safeQuantity = NonNegative(quantity);
        var grossAmount = normalized.GrossUnitPrice * safeQuantity;
        var discounts = normalized.DiscountUnitAmounts
            .Select(amount => amount * safeQuantity)
            .ToArray();
        var expenses = normalized.ExpenseUnitAmounts
            .Select(amount => amount * safeQuantity)
            .ToArray();
        var netAmount = Math.Max(0m, grossAmount - discounts.Sum() + expenses.Sum());
        var taxAmount = netAmount * normalized.TaxRatePercent / 100m;

        return new PurchaseFinancialAmounts(
            grossAmount,
            discounts,
            expenses,
            netAmount,
            taxAmount,
            normalized.TaxPointer,
            normalized.TaxRatePercent);
    }

    internal static PurchaseFinancialSnapshot FromNetUnitPrice(
        decimal unitPrice,
        int taxPointer,
        decimal taxRatePercent,
        string source = "legacy-client-price") =>
        Normalize(new PurchaseFinancialSnapshot(
            null,
            source,
            NonNegative(unitPrice),
            NonNegative(unitPrice),
            new decimal[DiscountCount],
            new decimal[DiscountCount],
            new decimal[ExpenseCount],
            new decimal[ExpenseCount],
            taxPointer,
            taxRatePercent));

    private static decimal[] ResolveDiscountAmounts(
        decimal grossUnitPrice,
        IReadOnlyList<decimal> rates,
        IReadOnlyList<decimal> suppliedAmounts)
    {
        var remaining = grossUnitPrice;
        var result = new decimal[DiscountCount];
        for (var index = 0; index < DiscountCount; index++)
        {
            var amount = suppliedAmounts[index] > 0m
                ? suppliedAmounts[index]
                : remaining * rates[index] / 100m;
            amount = Math.Min(remaining, NonNegative(amount));
            result[index] = amount;
            remaining -= amount;
        }

        return result;
    }

    private static decimal[] ResolveExpenseAmounts(
        decimal discountedUnitPrice,
        IReadOnlyList<decimal> rates,
        IReadOnlyList<decimal> suppliedAmounts)
    {
        var result = new decimal[ExpenseCount];
        for (var index = 0; index < ExpenseCount; index++)
        {
            result[index] = suppliedAmounts[index] > 0m
                ? suppliedAmounts[index]
                : discountedUnitPrice * rates[index] / 100m;
            result[index] = NonNegative(result[index]);
        }

        return result;
    }

    private static decimal[] NormalizeValues(IReadOnlyList<decimal>? values, int count)
    {
        var result = new decimal[count];
        if (values is null)
        {
            return result;
        }

        for (var index = 0; index < Math.Min(values.Count, count); index++)
        {
            result[index] = NonNegative(values[index]);
        }

        return result;
    }

    private static decimal NonNegative(decimal value) => value > 0m ? value : 0m;
}

internal sealed record PurchaseFinancialAmounts(
    decimal GrossAmount,
    IReadOnlyList<decimal> DiscountAmounts,
    IReadOnlyList<decimal> ExpenseAmounts,
    decimal NetAmount,
    decimal TaxAmount,
    int TaxPointer,
    decimal TaxRatePercent);

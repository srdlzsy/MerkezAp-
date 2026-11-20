using FurpaMerkezApi.Application.Modules.Common.PurchasePricing;
using FurpaMerkezApi.Infrastructure.Modules.Common.PurchasePricing;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.Common;

public sealed class PurchaseFinancialCalculatorTests
{
    [Fact]
    public void Calculate_PreservesGrossDiscountAndVatStructure()
    {
        var snapshot = new PurchaseFinancialSnapshot(
            Guid.Parse("56558f6d-033b-42dd-bcf1-8df5d7a0e41c"),
            "purchase-requirement",
            53.25m,
            0m,
            [3m, 1m, 0.01m, 0m, 0m, 0m],
            [1.5975m, 0.516525m, 0.0051135975m, 0m, 0m, 0m],
            [0m, 0m, 0m, 0m],
            [0m, 0m, 0m, 0m],
            3,
            1m);

        var amounts = PurchaseFinancialCalculator.Calculate(snapshot, 12m);

        Assert.Equal(639m, amounts.GrossAmount);
        Assert.Equal(19.17m, amounts.DiscountAmounts[0]);
        Assert.Equal(6.1983m, amounts.DiscountAmounts[1]);
        Assert.Equal(0.06136317m, amounts.DiscountAmounts[2]);
        Assert.Equal(613.57033683m, amounts.NetAmount);
        Assert.Equal(6.1357033683m, amounts.TaxAmount);
        Assert.Equal(3, amounts.TaxPointer);
    }

    [Fact]
    public void Normalize_CalculatesSequentialDiscountsWhenAmountsAreMissing()
    {
        var snapshot = new PurchaseFinancialSnapshot(
            null,
            "test",
            100m,
            0m,
            [10m, 10m, 0m, 0m, 0m, 0m],
            new decimal[6],
            new decimal[4],
            new decimal[4],
            0,
            0m);

        var normalized = PurchaseFinancialCalculator.Normalize(snapshot);

        Assert.Equal(10m, normalized.DiscountUnitAmounts[0]);
        Assert.Equal(9m, normalized.DiscountUnitAmounts[1]);
        Assert.Equal(81m, normalized.NetUnitPrice);
    }
}

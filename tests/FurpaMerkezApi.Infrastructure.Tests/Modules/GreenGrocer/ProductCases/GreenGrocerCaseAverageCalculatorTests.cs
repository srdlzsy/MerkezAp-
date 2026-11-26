using FurpaMerkezApi.Application.Modules.GreenGrocer.ProductCases;
using FurpaMerkezApi.Infrastructure.Modules.GreenGrocer.ProductCases;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.GreenGrocer.ProductCases;

public sealed class GreenGrocerCaseAverageCalculatorTests
{
    private static readonly DateTime OrderDate = new(2026, 11, 25);

    [Fact]
    public void Evaluate_GoldenLuxScenario_ReturnsTier1HighWithDriftWarning()
    {
        var observations = new[]
        {
            // Recent 7 days: 3 labels, 528 cases, weighted avg ~ 3.89 KG/kasa
            new GreenGrocerLabelObservation(OrderDate.AddDays(-1), 200 * 3.85, 200),
            new GreenGrocerLabelObservation(OrderDate.AddDays(-2), 200 * 3.92, 200),
            new GreenGrocerLabelObservation(OrderDate.AddDays(-4), 128 * 3.90, 128),
            // Prior 23 days: 3 labels, 150 cases @ ~7.43 KG/kasa
            new GreenGrocerLabelObservation(OrderDate.AddDays(-10), 100 * 7.40, 100),
            new GreenGrocerLabelObservation(OrderDate.AddDays(-15), 50 * 7.49, 50)
        };

        var result = GreenGrocerCaseAverageCalculator.Evaluate(observations, OrderDate);

        Assert.Equal(GreenGrocerProductCaseModes.Tier1, result.CalculationTier);
        Assert.Equal(GreenGrocerProductCaseModes.ConfidenceHigh, result.Confidence);
        Assert.False(result.RequiresManualApproval);
        Assert.True(result.IsUsable);
        Assert.Equal(7, result.AverageWindowDaysUsed);
        Assert.Equal(3, result.RecordCount);
        Assert.Equal(528, result.CaseCount);
        Assert.InRange(result.AverageKgPerCase!.Value, 3.88, 3.90);
        Assert.True(result.DriftDetected);
        Assert.InRange(result.PriorAverageKgPerCase!.Value, 7.42, 7.44);
        Assert.True(result.DriftPercent < -40d);
        Assert.Contains(result.Warnings, warning => warning.Contains("Kasa agirligi degismis", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_SingleLargeRecentBatchConsistentWithPrior23_ReturnsTier2MediumWithNullCv()
    {
        var observations = new[]
        {
            new GreenGrocerLabelObservation(OrderDate.AddDays(-1), 30 * 10.50, 30),
            new GreenGrocerLabelObservation(OrderDate.AddDays(-10), 20 * 10.00, 20),
            new GreenGrocerLabelObservation(OrderDate.AddDays(-14), 15 * 10.20, 15)
        };

        var result = GreenGrocerCaseAverageCalculator.Evaluate(observations, OrderDate);

        Assert.Equal(GreenGrocerProductCaseModes.Tier2, result.CalculationTier);
        Assert.Equal(GreenGrocerProductCaseModes.ConfidenceMedium, result.Confidence);
        Assert.False(result.RequiresManualApproval);
        Assert.True(result.IsUsable);
        Assert.Null(result.CoefficientOfVariation);
        Assert.Equal(10.50, result.AverageKgPerCase!.Value, 3);
        Assert.False(result.DriftDetected);
    }

    [Fact]
    public void Evaluate_GrannySmithSingleLabelHugeDrift_ReturnsTier3ApprovalRequired()
    {
        var observations = new[]
        {
            new GreenGrocerLabelObservation(OrderDate.AddDays(-1), 56 * 18.57, 56),
            new GreenGrocerLabelObservation(OrderDate.AddDays(-9), 40 * 6.45, 40),
            new GreenGrocerLabelObservation(OrderDate.AddDays(-16), 30 * 6.45, 30)
        };

        var result = GreenGrocerCaseAverageCalculator.Evaluate(observations, OrderDate);

        Assert.Equal(GreenGrocerProductCaseModes.Tier3, result.CalculationTier);
        Assert.Equal(GreenGrocerProductCaseModes.ConfidenceLow, result.Confidence);
        Assert.True(result.RequiresManualApproval);
        Assert.True(result.DriftDetected);
        Assert.Equal(30, result.AverageWindowDaysUsed);
        Assert.Equal(11.84, result.AverageKgPerCase!.Value, 2);
        Assert.Equal(18.57, result.AlternativeAverageKgPerCase!.Value, 2);
        Assert.Equal(6.45, result.PriorAverageKgPerCase!.Value, 2);
        Assert.True(result.DriftPercent > 150d);
    }

    [Fact]
    public void Evaluate_SingleLabelWithNoPriorHistory_ReturnsTier3ApprovalRequired()
    {
        var observations = new[]
        {
            new GreenGrocerLabelObservation(OrderDate.AddDays(-2), 25 * 5.00, 25)
        };

        var result = GreenGrocerCaseAverageCalculator.Evaluate(observations, OrderDate);

        Assert.Equal(GreenGrocerProductCaseModes.Tier3, result.CalculationTier);
        Assert.Equal(GreenGrocerProductCaseModes.ConfidenceLow, result.Confidence);
        Assert.True(result.RequiresManualApproval);
        Assert.Null(result.PriorAverageKgPerCase);
        Assert.Null(result.DriftPercent);
        Assert.Null(result.CoefficientOfVariation);
    }

    [Fact]
    public void Evaluate_HighWeightedCvInRecent7Days_ReturnsTier3ApprovalRequired()
    {
        var observations = new[]
        {
            new GreenGrocerLabelObservation(OrderDate.AddDays(-1), 20 * 5.00, 20),
            new GreenGrocerLabelObservation(OrderDate.AddDays(-2), 20 * 15.00, 20)
        };

        var result = GreenGrocerCaseAverageCalculator.Evaluate(observations, OrderDate);

        Assert.Equal(GreenGrocerProductCaseModes.Tier3, result.CalculationTier);
        Assert.Equal(GreenGrocerProductCaseModes.ConfidenceLow, result.Confidence);
        Assert.True(result.RequiresManualApproval);
        Assert.True(result.CoefficientOfVariation > 0.25d);
    }

    [Fact]
    public void Evaluate_NoRecent7DaysButFreshReliable30Days_ReturnsTier4aMedium()
    {
        var observations = new[]
        {
            new GreenGrocerLabelObservation(OrderDate.AddDays(-8), 10 * 9.80, 10),
            new GreenGrocerLabelObservation(OrderDate.AddDays(-10), 10 * 10.00, 10),
            new GreenGrocerLabelObservation(OrderDate.AddDays(-12), 10 * 10.20, 10)
        };

        var result = GreenGrocerCaseAverageCalculator.Evaluate(observations, OrderDate);

        Assert.Equal(GreenGrocerProductCaseModes.Tier4a, result.CalculationTier);
        Assert.Equal(GreenGrocerProductCaseModes.ConfidenceMedium, result.Confidence);
        Assert.False(result.RequiresManualApproval);
        Assert.Equal(30, result.AverageWindowDaysUsed);
        Assert.Equal(8, result.LabelAgeDays);
        Assert.Equal(10.00, result.AverageKgPerCase!.Value, 2);
    }

    [Fact]
    public void Evaluate_Stale30DayDataOlderThan14Days_ReturnsTier4bApprovalRequired()
    {
        var observations = new[]
        {
            new GreenGrocerLabelObservation(OrderDate.AddDays(-16), 10 * 10.00, 10),
            new GreenGrocerLabelObservation(OrderDate.AddDays(-18), 10 * 10.00, 10),
            new GreenGrocerLabelObservation(OrderDate.AddDays(-20), 10 * 10.00, 10)
        };

        var result = GreenGrocerCaseAverageCalculator.Evaluate(observations, OrderDate);

        Assert.Equal(GreenGrocerProductCaseModes.Tier4b, result.CalculationTier);
        Assert.Equal(GreenGrocerProductCaseModes.ConfidenceLow, result.Confidence);
        Assert.True(result.RequiresManualApproval);
        Assert.True(result.IsUsable);
        Assert.Equal(16, result.LabelAgeDays);
    }

    [Fact]
    public void Evaluate_NoValidLabelsIn30Days_ReturnsTier4bBlocked()
    {
        var observations = new[]
        {
            // Older than exact 30 calendar days (T-30 is outside [T-29, T])
            new GreenGrocerLabelObservation(OrderDate.AddDays(-30), 50 * 10.00, 50),
            // Future label after OrderDate must NEVER leak into backtests
            new GreenGrocerLabelObservation(OrderDate.AddDays(1), 50 * 12.00, 50)
        };

        var result = GreenGrocerCaseAverageCalculator.Evaluate(observations, OrderDate);

        Assert.Equal(GreenGrocerProductCaseModes.Tier4b, result.CalculationTier);
        Assert.Equal(GreenGrocerProductCaseModes.ConfidenceBlocked, result.Confidence);
        Assert.False(result.IsUsable);
        Assert.True(result.RequiresManualApproval);
        Assert.Null(result.AverageKgPerCase);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Evaluate_SackProductAbove40Kg_HasNoGlobalUpperWeightCap()
    {
        var observations = new[]
        {
            new GreenGrocerLabelObservation(OrderDate.AddDays(-1), 20 * 48.0, 20),
            new GreenGrocerLabelObservation(OrderDate.AddDays(-3), 20 * 50.0, 20)
        };

        var result = GreenGrocerCaseAverageCalculator.Evaluate(observations, OrderDate);

        Assert.Equal(GreenGrocerProductCaseModes.Tier1, result.CalculationTier);
        Assert.Equal(GreenGrocerProductCaseModes.ConfidenceHigh, result.Confidence);
        Assert.False(result.RequiresManualApproval);
        Assert.Equal(49.0, result.AverageKgPerCase!.Value, 2);
    }

    [Fact]
    public void Evaluate_ProfileExpectedBoundsViolation_RequiresManualApproval()
    {
        var observations = new[]
        {
            new GreenGrocerLabelObservation(OrderDate.AddDays(-1), 20 * 4.0, 20),
            new GreenGrocerLabelObservation(OrderDate.AddDays(-2), 20 * 4.0, 20)
        };

        var settings = GreenGrocerCaseCalculationSettings.Default with
        {
            MinExpectedKgPerCase = 6.0,
            MaxExpectedKgPerCase = 15.0
        };

        var result = GreenGrocerCaseAverageCalculator.Evaluate(observations, OrderDate, settings);

        Assert.Equal(GreenGrocerProductCaseModes.ConfidenceLow, result.Confidence);
        Assert.True(result.RequiresManualApproval);
        Assert.Contains(result.Warnings, warning => warning.Contains("minimum beklentisinin", StringComparison.Ordinal));
    }
}

using FurpaMerkezApi.Application.Common.Validation;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Common;

public sealed class CreateQuantityLimitsTests
{
    [Theory]
    [InlineData(0d)]
    [InlineData(1d)]
    [InlineData(CreateQuantityLimits.Maximum)]
    public void EnsureWithinMaximum_AcceptsValuesAtOrBelowLimit(double quantity)
    {
        CreateQuantityLimits.EnsureWithinMaximum(quantity, nameof(quantity));
    }

    [Theory]
    [InlineData(100_000.000001d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void EnsureWithinMaximum_RejectsInvalidOrExcessiveValues(double quantity)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => CreateQuantityLimits.EnsureWithinMaximum(quantity, nameof(quantity)));

        Assert.Contains("100000", exception.Message, StringComparison.Ordinal);
    }
}

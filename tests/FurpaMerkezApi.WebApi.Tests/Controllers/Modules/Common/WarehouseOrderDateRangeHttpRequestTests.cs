using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Controllers.Modules.Common;

public sealed class WarehouseOrderDateRangeHttpRequestTests
{
    [Fact]
    public void Validate_RejectsDateBeforeSqlDateTimeMinimum()
    {
        var request = new WarehouseOrderDateRangeHttpRequest
        {
            StartDate = new DateTime(26, 12, 11),
            EndDate = new DateTime(2026, 11, 20)
        };
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, result =>
            result.MemberNames.Contains(nameof(WarehouseOrderDateRangeHttpRequest.StartDate)));
    }

    [Fact]
    public void Validate_RejectsReversedDateRange()
    {
        var request = new WarehouseOrderDateRangeHttpRequest
        {
            StartDate = new DateTime(2026, 11, 21),
            EndDate = new DateTime(2026, 11, 20)
        };
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, result =>
            result.MemberNames.Contains(nameof(WarehouseOrderDateRangeHttpRequest.EndDate)));
    }
}

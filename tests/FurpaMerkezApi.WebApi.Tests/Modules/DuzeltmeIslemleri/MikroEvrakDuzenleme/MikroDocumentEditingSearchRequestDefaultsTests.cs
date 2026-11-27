using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.WebApi.Controllers.Modules.DuzeltmeIslemleri.MikroEvrakDuzenleme;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Modules.DuzeltmeIslemleri.MikroEvrakDuzenleme;

public sealed class MikroDocumentEditingSearchRequestDefaultsTests
{
    [Fact]
    public void CardSearches_IncludePassiveRecordsByDefault()
    {
        Assert.True(new StockCardSearchHttpRequest().IncludePassive);
        Assert.True(new WarehouseCardSearchHttpRequest().IncludePassive);
        Assert.True(new CustomerCardSearchHttpRequest().IncludePassive);
    }

    [Fact]
    public void StockMovementLookup_AllowsZeroForInputAndOutputWarehouseNo()
    {
        var request = new StockMovementDocumentLookupHttpRequest
        {
            DocumentSerie = "A",
            DocumentOrderNo = 1,
            InputWarehouseNo = 0,
            OutputWarehouseNo = 0
        };

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Fact]
    public void StockMovementLookup_RejectsNegativeInputOrOutputWarehouseNo()
    {
        var request = new StockMovementDocumentLookupHttpRequest
        {
            DocumentSerie = "A",
            DocumentOrderNo = 1,
            InputWarehouseNo = -1,
            OutputWarehouseNo = -1
        };

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, item => item.MemberNames.Contains(nameof(StockMovementDocumentLookupHttpRequest.InputWarehouseNo)));
        Assert.Contains(results, item => item.MemberNames.Contains(nameof(StockMovementDocumentLookupHttpRequest.OutputWarehouseNo)));
    }
}

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
}

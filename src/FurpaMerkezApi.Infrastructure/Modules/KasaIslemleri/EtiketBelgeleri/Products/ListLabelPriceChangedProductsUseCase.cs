using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri.Products;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.EtiketBelgeleri.Products;

public sealed class ListLabelPriceChangedProductsUseCase(LabelProductQueryExecutor labelProductQueryExecutor)
    : IListLabelPriceChangedProductsUseCase
{
    public Task<IReadOnlyCollection<LabelPriceChangedProductDto>> ExecuteAsync(
        LabelPriceChangedProductRequest request,
        CancellationToken cancellationToken) =>
        labelProductQueryExecutor.ListPriceChangedProductsAsync(
            request.WarehouseNo,
            request.DateTimeFilter,
            cancellationToken);
}

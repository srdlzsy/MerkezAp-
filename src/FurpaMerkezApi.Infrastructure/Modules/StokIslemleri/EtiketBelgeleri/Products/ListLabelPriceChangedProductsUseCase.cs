using FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri;
using FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri.Products;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.EtiketBelgeleri.Products;

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

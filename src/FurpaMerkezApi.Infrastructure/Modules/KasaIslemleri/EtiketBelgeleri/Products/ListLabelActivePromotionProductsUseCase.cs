using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri.Products;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.EtiketBelgeleri.Products;

public sealed class ListLabelActivePromotionProductsUseCase(LabelProductQueryExecutor labelProductQueryExecutor)
    : IListLabelActivePromotionProductsUseCase
{
    public Task<IReadOnlyCollection<LabelActivePromotionProductDto>> ExecuteAsync(
        LabelActivePromotionProductRequest request,
        CancellationToken cancellationToken) =>
        labelProductQueryExecutor.ListActivePromotionProductsAsync(
            request.WarehouseNo,
            cancellationToken);
}

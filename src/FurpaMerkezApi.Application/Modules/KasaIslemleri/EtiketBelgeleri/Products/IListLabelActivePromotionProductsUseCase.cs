namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri.Products;

public interface IListLabelActivePromotionProductsUseCase
{
    Task<IReadOnlyCollection<LabelActivePromotionProductDto>> ExecuteAsync(
        LabelActivePromotionProductRequest request,
        CancellationToken cancellationToken);
}

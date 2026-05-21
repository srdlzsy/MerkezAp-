namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri.Products;

public interface IListLabelPriceChangedProductsUseCase
{
    Task<IReadOnlyCollection<LabelPriceChangedProductDto>> ExecuteAsync(
        LabelPriceChangedProductRequest request,
        CancellationToken cancellationToken);
}

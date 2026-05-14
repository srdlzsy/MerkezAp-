namespace FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri.Products;

public interface IListLabelPriceChangedProductsUseCase
{
    Task<IReadOnlyCollection<LabelPriceChangedProductDto>> ExecuteAsync(
        LabelPriceChangedProductRequest request,
        CancellationToken cancellationToken);
}

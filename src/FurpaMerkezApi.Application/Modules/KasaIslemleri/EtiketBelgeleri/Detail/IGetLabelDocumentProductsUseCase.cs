namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri.Detail;

public interface IGetLabelDocumentProductsUseCase
{
    Task<IReadOnlyCollection<LabelDocumentProductDto>> ExecuteAsync(
        LabelDocumentDetailRequest request,
        CancellationToken cancellationToken);
}

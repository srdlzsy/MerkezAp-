namespace FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri.Detail;

public interface IGetLabelDocumentProductsUseCase
{
    Task<IReadOnlyCollection<LabelDocumentProductDto>> ExecuteAsync(
        LabelDocumentDetailRequest request,
        CancellationToken cancellationToken);
}

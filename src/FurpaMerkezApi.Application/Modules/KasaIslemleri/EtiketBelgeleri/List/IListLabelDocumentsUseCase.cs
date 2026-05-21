namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri.List;

public interface IListLabelDocumentsUseCase
{
    Task<IReadOnlyCollection<LabelDocumentListItemDto>> ExecuteAsync(
        LabelDocumentListRequest request,
        CancellationToken cancellationToken);
}

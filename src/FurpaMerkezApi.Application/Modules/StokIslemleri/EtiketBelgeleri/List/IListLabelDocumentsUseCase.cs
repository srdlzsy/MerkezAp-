namespace FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri.List;

public interface IListLabelDocumentsUseCase
{
    Task<IReadOnlyCollection<LabelDocumentListItemDto>> ExecuteAsync(
        LabelDocumentListRequest request,
        CancellationToken cancellationToken);
}

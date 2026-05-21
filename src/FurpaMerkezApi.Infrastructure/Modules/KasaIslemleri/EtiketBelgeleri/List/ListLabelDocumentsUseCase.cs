using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri.List;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.EtiketBelgeleri.List;

public sealed class ListLabelDocumentsUseCase(LabelDocumentQueryExecutor queryExecutor)
    : IListLabelDocumentsUseCase
{
    public Task<IReadOnlyCollection<LabelDocumentListItemDto>> ExecuteAsync(
        LabelDocumentListRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.ListAsync(request, cancellationToken);
}

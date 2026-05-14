using FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri;
using FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri.List;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.EtiketBelgeleri.List;

public sealed class ListLabelDocumentsUseCase(LabelDocumentQueryExecutor queryExecutor)
    : IListLabelDocumentsUseCase
{
    public Task<IReadOnlyCollection<LabelDocumentListItemDto>> ExecuteAsync(
        LabelDocumentListRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.ListAsync(request, cancellationToken);
}

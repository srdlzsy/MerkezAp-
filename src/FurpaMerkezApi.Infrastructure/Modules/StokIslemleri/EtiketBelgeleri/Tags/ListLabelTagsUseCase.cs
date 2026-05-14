using FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri;
using FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri.Tags;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.EtiketBelgeleri.Tags;

public sealed class ListLabelTagsUseCase(LabelTagQueryExecutor queryExecutor)
    : IListLabelTagsUseCase
{
    public Task<IReadOnlyCollection<LabelTagDto>> ExecuteAsync(
        LabelTagListRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(request, cancellationToken);
}

using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri.Tags;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.EtiketBelgeleri.Tags;

public sealed class ListLabelTagsUseCase(LabelTagQueryExecutor queryExecutor)
    : IListLabelTagsUseCase
{
    public Task<IReadOnlyCollection<LabelTagDto>> ExecuteAsync(
        LabelTagListRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(request, cancellationToken);
}

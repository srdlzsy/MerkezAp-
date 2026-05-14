using FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar;
using FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar.List;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Virmanlar.List;

public sealed class ListVirmansUseCase(VirmanListQueryExecutor queryExecutor)
    : IListVirmansUseCase
{
    public Task<IReadOnlyCollection<VirmanListItemDto>> ExecuteAsync(
        VirmanListRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(request, cancellationToken);
}

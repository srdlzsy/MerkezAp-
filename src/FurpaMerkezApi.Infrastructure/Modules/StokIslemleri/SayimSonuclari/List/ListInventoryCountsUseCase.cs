using FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari;
using FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari.List;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Common;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.SayimSonuclari.List;

public sealed class ListInventoryCountsUseCase(InventoryCountListQueryExecutor queryExecutor)
    : IListInventoryCountsUseCase
{
    public Task<IReadOnlyCollection<InventoryCountListItemDto>> ExecuteAsync(
        InventoryCountListRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(request, cancellationToken);
}

using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.DepoMalKabulleri.List;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.Common;

namespace FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.DepoMalKabulleri.List;

public sealed class ListPendingWarehouseReceivingsUseCase(WarehouseShippingListQueryExecutor queryExecutor)
    : IListPendingWarehouseReceivingsUseCase
{
    public Task<IReadOnlyCollection<WarehouseShippingListItemDto>> ExecuteAsync(
        WarehouseShippingListRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecutePendingIncomingAsync(request, cancellationToken);
}

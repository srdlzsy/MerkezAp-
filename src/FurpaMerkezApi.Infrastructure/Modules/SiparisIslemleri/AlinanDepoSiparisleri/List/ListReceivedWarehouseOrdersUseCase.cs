using FurpaMerkezApi.Application.Modules.SiparisIslemleri.AlinanDepoSiparisleri.List;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.Common;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.AlinanDepoSiparisleri.List;

public sealed class ListReceivedWarehouseOrdersUseCase(WarehouseOrderListQueryExecutor queryExecutor)
    : IListReceivedWarehouseOrdersUseCase
{
    public Task<IReadOnlyCollection<WarehouseOrderListItemDto>> ExecuteAsync(
        WarehouseOrderListRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(request, WarehouseOrderListDirection.Received, cancellationToken);
}

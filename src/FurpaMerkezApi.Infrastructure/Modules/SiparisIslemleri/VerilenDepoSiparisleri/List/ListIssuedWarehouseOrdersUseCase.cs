using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenDepoSiparisleri.List;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.Common;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenDepoSiparisleri.List;

public sealed class ListIssuedWarehouseOrdersUseCase(WarehouseOrderListQueryExecutor queryExecutor)
    : IListIssuedWarehouseOrdersUseCase
{
    public Task<IReadOnlyCollection<WarehouseOrderListItemDto>> ExecuteAsync(
        WarehouseOrderListRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(request, WarehouseOrderListDirection.Issued, cancellationToken);
}

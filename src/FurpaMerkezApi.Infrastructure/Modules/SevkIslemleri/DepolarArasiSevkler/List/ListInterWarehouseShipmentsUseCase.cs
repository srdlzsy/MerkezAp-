using FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.DepolarArasiSevkler.List;
using FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.Common;

namespace FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.DepolarArasiSevkler.List;

public sealed class ListInterWarehouseShipmentsUseCase(WarehouseShippingListQueryExecutor queryExecutor)
    : IListInterWarehouseShipmentsUseCase
{
    public Task<IReadOnlyCollection<WarehouseShippingListItemDto>> ExecuteAsync(
        WarehouseShippingListRequest request,
        WarehouseShippingDirection direction,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(request, direction, false, cancellationToken);
}

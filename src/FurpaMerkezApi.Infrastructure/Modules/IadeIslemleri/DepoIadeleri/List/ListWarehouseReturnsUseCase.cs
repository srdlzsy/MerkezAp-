using FurpaMerkezApi.Application.Modules.IadeIslemleri.DepoIadeleri.List;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.Common;

namespace FurpaMerkezApi.Infrastructure.Modules.IadeIslemleri.DepoIadeleri.List;

public sealed class ListWarehouseReturnsUseCase(WarehouseShippingListQueryExecutor queryExecutor)
    : IListWarehouseReturnsUseCase
{
    public Task<IReadOnlyCollection<WarehouseShippingListItemDto>> ExecuteAsync(
        WarehouseShippingListRequest request,
        WarehouseShippingDirection direction,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(
            request,
            direction,
            true,
            cancellationToken);
}

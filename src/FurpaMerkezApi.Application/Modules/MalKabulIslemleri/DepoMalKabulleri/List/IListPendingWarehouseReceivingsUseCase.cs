using FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;

namespace FurpaMerkezApi.Application.Modules.MalKabulIslemleri.DepoMalKabulleri.List;

public interface IListPendingWarehouseReceivingsUseCase
{
    Task<IReadOnlyCollection<WarehouseShippingListItemDto>> ExecuteAsync(
        WarehouseShippingListRequest request,
        CancellationToken cancellationToken);
}

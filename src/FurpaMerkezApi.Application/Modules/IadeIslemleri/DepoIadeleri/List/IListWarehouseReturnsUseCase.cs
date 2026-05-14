using FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;

namespace FurpaMerkezApi.Application.Modules.IadeIslemleri.DepoIadeleri.List;

public interface IListWarehouseReturnsUseCase
{
    Task<IReadOnlyCollection<WarehouseShippingListItemDto>> ExecuteAsync(
        WarehouseShippingListRequest request,
        WarehouseShippingDirection direction,
        CancellationToken cancellationToken);
}

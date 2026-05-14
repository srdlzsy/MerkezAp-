using FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;

namespace FurpaMerkezApi.Application.Modules.SevkIslemleri.DepolarArasiSevkler.List;

public interface IListInterWarehouseShipmentsUseCase
{
    Task<IReadOnlyCollection<WarehouseShippingListItemDto>> ExecuteAsync(
        WarehouseShippingListRequest request,
        WarehouseShippingDirection direction,
        CancellationToken cancellationToken);
}

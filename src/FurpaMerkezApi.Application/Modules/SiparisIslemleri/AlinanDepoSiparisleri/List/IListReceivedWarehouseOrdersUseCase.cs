using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;

namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.AlinanDepoSiparisleri.List;

public interface IListReceivedWarehouseOrdersUseCase
{
    Task<IReadOnlyCollection<WarehouseOrderListItemDto>> ExecuteAsync(
        WarehouseOrderListRequest request,
        CancellationToken cancellationToken);
}

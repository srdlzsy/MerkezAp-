using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;

namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenDepoSiparisleri.List;

public interface IListIssuedWarehouseOrdersUseCase
{
    Task<IReadOnlyCollection<WarehouseOrderListItemDto>> ExecuteAsync(
        WarehouseOrderListRequest request,
        CancellationToken cancellationToken);
}

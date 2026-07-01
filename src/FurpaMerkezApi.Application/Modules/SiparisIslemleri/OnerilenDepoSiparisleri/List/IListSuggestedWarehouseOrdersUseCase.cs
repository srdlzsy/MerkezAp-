using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;

namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.OnerilenDepoSiparisleri.List;

public interface IListSuggestedWarehouseOrdersUseCase
{
    Task<IReadOnlyCollection<SuggestedWarehouseOrderListItemDto>> ExecuteAsync(
        SuggestedWarehouseOrderListRequest request,
        CancellationToken cancellationToken);
}

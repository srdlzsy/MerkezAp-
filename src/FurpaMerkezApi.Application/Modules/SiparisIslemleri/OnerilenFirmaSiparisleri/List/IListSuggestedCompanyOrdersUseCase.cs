using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;

namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.OnerilenFirmaSiparisleri.List;

public interface IListSuggestedCompanyOrdersUseCase
{
    Task<IReadOnlyCollection<SuggestedCompanyOrderListItemDto>> ExecuteAsync(
        SuggestedCompanyOrderListRequest request,
        CancellationToken cancellationToken);
}

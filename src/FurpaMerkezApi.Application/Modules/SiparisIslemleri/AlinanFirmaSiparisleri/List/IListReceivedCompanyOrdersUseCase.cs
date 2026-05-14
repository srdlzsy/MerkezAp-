using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;

namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.AlinanFirmaSiparisleri.List;

public interface IListReceivedCompanyOrdersUseCase
{
    Task<IReadOnlyCollection<CompanyOrderListItemDto>> ExecuteAsync(
        CompanyOrderListRequest request,
        CancellationToken cancellationToken);
}

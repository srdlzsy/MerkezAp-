using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;

namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.List;

public interface IListIssuedCompanyOrdersUseCase
{
    Task<IReadOnlyCollection<CompanyOrderListItemDto>> ExecuteAsync(
        CompanyOrderListRequest request,
        CancellationToken cancellationToken);
}

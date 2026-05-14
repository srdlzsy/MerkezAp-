using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.List;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.Common;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.List;

public sealed class ListIssuedCompanyOrdersUseCase(CompanyOrderListQueryExecutor queryExecutor)
    : IListIssuedCompanyOrdersUseCase
{
    public Task<IReadOnlyCollection<CompanyOrderListItemDto>> ExecuteAsync(
        CompanyOrderListRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(request, CompanyOrderListDirection.Issued, cancellationToken);
}

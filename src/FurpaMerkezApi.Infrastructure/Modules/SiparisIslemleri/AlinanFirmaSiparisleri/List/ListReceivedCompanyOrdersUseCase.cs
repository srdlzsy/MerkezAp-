using FurpaMerkezApi.Application.Modules.SiparisIslemleri.AlinanFirmaSiparisleri.List;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.Common;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.AlinanFirmaSiparisleri.List;

public sealed class ListReceivedCompanyOrdersUseCase(CompanyOrderListQueryExecutor queryExecutor)
    : IListReceivedCompanyOrdersUseCase
{
    public Task<IReadOnlyCollection<CompanyOrderListItemDto>> ExecuteAsync(
        CompanyOrderListRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(request, CompanyOrderListDirection.Received, cancellationToken);
}

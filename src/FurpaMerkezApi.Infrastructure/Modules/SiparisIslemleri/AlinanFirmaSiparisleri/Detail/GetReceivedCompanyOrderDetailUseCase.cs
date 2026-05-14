using FurpaMerkezApi.Application.Modules.SiparisIslemleri.AlinanFirmaSiparisleri.Detail;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.Common;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.AlinanFirmaSiparisleri.Detail;

public sealed class GetReceivedCompanyOrderDetailUseCase(CompanyOrderDetailQueryExecutor queryExecutor)
    : IGetReceivedCompanyOrderDetailUseCase
{
    public Task<CompanyOrderDetailDto> ExecuteAsync(
        CompanyOrderDetailRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(request, CompanyOrderListDirection.Received, cancellationToken);
}

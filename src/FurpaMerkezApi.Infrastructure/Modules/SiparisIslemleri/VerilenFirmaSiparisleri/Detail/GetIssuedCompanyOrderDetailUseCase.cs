using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Detail;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.Common;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Detail;

public sealed class GetIssuedCompanyOrderDetailUseCase(CompanyOrderDetailQueryExecutor queryExecutor)
    : IGetIssuedCompanyOrderDetailUseCase
{
    public Task<CompanyOrderDetailDto> ExecuteAsync(
        CompanyOrderDetailRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(request, CompanyOrderListDirection.Issued, cancellationToken);
}

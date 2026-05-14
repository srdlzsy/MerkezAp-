using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.FirmaSevkleri.Detail;
using FurpaMerkezApi.Infrastructure.Modules.Common.CompanyMovements;

namespace FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.FirmaSevkleri.Detail;

public sealed class GetCompanyShipmentDetailUseCase(CompanyMovementDetailQueryExecutor queryExecutor)
    : IGetCompanyShipmentDetailUseCase
{
    public Task<CompanyMovementDetailDto> ExecuteAsync(
        CompanyMovementDetailRequest request,
        CompanyMovementKind kind,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(request, kind, cancellationToken);
}

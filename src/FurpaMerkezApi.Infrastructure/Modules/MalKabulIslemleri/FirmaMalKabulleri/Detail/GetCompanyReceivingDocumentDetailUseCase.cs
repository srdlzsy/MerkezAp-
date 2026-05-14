using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.FirmaMalKabulleri.Detail;
using FurpaMerkezApi.Infrastructure.Modules.Common.CompanyMovements;

namespace FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.FirmaMalKabulleri.Detail;

public sealed class GetCompanyReceivingDocumentDetailUseCase(CompanyMovementDetailQueryExecutor queryExecutor)
    : IGetCompanyReceivingDocumentDetailUseCase
{
    public Task<CompanyMovementDetailDto> ExecuteAsync(
        CompanyMovementDetailRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(request, CompanyMovementKind.IncomingShipment, cancellationToken);
}

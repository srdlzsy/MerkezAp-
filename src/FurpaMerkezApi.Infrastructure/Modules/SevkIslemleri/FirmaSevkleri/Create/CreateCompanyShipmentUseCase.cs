using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.FirmaSevkleri.Create;
using FurpaMerkezApi.Infrastructure.Modules.Common.CompanyMovements;

namespace FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.FirmaSevkleri.Create;

public sealed class CreateCompanyShipmentUseCase(CompanyMovementWriteService companyMovementWriteService)
    : ICreateCompanyShipmentUseCase
{
    public Task<CreateCompanyMovementResponse> ExecuteAsync(
        CreateCompanyMovementRequest request,
        CancellationToken cancellationToken) =>
        companyMovementWriteService.ExecuteAsync(request, CompanyMovementKind.OutgoingShipment, cancellationToken);
}

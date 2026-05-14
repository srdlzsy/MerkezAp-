using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;

namespace FurpaMerkezApi.Application.Modules.SevkIslemleri.FirmaSevkleri.Create;

public interface ICreateCompanyShipmentUseCase
{
    Task<CreateCompanyMovementResponse> ExecuteAsync(
        CreateCompanyMovementRequest request,
        CancellationToken cancellationToken);
}

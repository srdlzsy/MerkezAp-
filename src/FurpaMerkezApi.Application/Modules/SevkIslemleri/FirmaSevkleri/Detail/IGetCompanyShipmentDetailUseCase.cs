using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;

namespace FurpaMerkezApi.Application.Modules.SevkIslemleri.FirmaSevkleri.Detail;

public interface IGetCompanyShipmentDetailUseCase
{
    Task<CompanyMovementDetailDto> ExecuteAsync(
        CompanyMovementDetailRequest request,
        CompanyMovementKind kind,
        CancellationToken cancellationToken);
}

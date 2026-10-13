using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Application.Modules.Common.OfflineSync;

namespace FurpaMerkezApi.Application.Modules.IadeIslemleri.FirmaIadeleri.Create;

public interface ICreateCompanyReturnUseCase
{
    Task<CreateCompanyMovementResponse> ExecuteAsync(
        CreateCompanyMovementRequest request,
        CancellationToken cancellationToken);

    Task<OfflineSyncStatusDto<CreateCompanyMovementResponse>> GetOfflineSyncStatusAsync(
        Guid requestedByUserId,
        Guid clientRequestId,
        CancellationToken cancellationToken);
}

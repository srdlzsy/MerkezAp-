using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;

namespace FurpaMerkezApi.Application.Modules.IadeIslemleri.FirmaIadeleri.Create;

public interface ICreateCompanyReturnUseCase
{
    Task<CreateCompanyMovementResponse> ExecuteAsync(
        CreateCompanyMovementRequest request,
        CancellationToken cancellationToken);
}

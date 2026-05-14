using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Application.Modules.IadeIslemleri.FirmaIadeleri.Create;
using FurpaMerkezApi.Infrastructure.Modules.Common.CompanyMovements;

namespace FurpaMerkezApi.Infrastructure.Modules.IadeIslemleri.FirmaIadeleri.Create;

public sealed class CreateCompanyReturnUseCase(CompanyMovementWriteService companyMovementWriteService)
    : ICreateCompanyReturnUseCase
{
    public Task<CreateCompanyMovementResponse> ExecuteAsync(
        CreateCompanyMovementRequest request,
        CancellationToken cancellationToken) =>
        companyMovementWriteService.ExecuteAsync(request, CompanyMovementKind.PurchaseReturn, cancellationToken);
}

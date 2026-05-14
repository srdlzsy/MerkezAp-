using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Application.Modules.IadeIslemleri.FirmaIadeleri.Detail;
using FurpaMerkezApi.Infrastructure.Modules.Common.CompanyMovements;

namespace FurpaMerkezApi.Infrastructure.Modules.IadeIslemleri.FirmaIadeleri.Detail;

public sealed class GetCompanyReturnDetailUseCase(CompanyMovementDetailQueryExecutor queryExecutor)
    : IGetCompanyReturnDetailUseCase
{
    public Task<CompanyMovementDetailDto> ExecuteAsync(
        CompanyMovementDetailRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(request, CompanyMovementKind.PurchaseReturn, cancellationToken);
}

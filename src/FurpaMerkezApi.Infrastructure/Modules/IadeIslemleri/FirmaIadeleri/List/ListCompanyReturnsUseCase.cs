using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Application.Modules.IadeIslemleri.FirmaIadeleri.List;
using FurpaMerkezApi.Infrastructure.Modules.Common.CompanyMovements;

namespace FurpaMerkezApi.Infrastructure.Modules.IadeIslemleri.FirmaIadeleri.List;

public sealed class ListCompanyReturnsUseCase(CompanyMovementListQueryExecutor queryExecutor)
    : IListCompanyReturnsUseCase
{
    public Task<IReadOnlyCollection<CompanyMovementListItemDto>> ExecuteAsync(
        CompanyMovementListRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(request, CompanyMovementKind.PurchaseReturn, cancellationToken);
}

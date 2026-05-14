using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;

namespace FurpaMerkezApi.Application.Modules.IadeIslemleri.FirmaIadeleri.List;

public interface IListCompanyReturnsUseCase
{
    Task<IReadOnlyCollection<CompanyMovementListItemDto>> ExecuteAsync(
        CompanyMovementListRequest request,
        CancellationToken cancellationToken);
}

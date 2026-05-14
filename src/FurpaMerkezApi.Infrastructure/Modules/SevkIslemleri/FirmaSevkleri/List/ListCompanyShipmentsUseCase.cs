using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.FirmaSevkleri.List;
using FurpaMerkezApi.Infrastructure.Modules.Common.CompanyMovements;

namespace FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.FirmaSevkleri.List;

public sealed class ListCompanyShipmentsUseCase(CompanyMovementListQueryExecutor queryExecutor)
    : IListCompanyShipmentsUseCase
{
    public Task<IReadOnlyCollection<CompanyMovementListItemDto>> ExecuteAsync(
        CompanyMovementListRequest request,
        CompanyMovementKind kind,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(request, kind, cancellationToken);
}

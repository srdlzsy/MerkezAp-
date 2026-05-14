using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.FirmaMalKabulleri.List;
using FurpaMerkezApi.Infrastructure.Modules.Common.CompanyMovements;

namespace FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.FirmaMalKabulleri.List;

public sealed class ListCompanyReceivingDocumentsUseCase(CompanyMovementListQueryExecutor queryExecutor)
    : IListCompanyReceivingDocumentsUseCase
{
    public Task<IReadOnlyCollection<CompanyMovementListItemDto>> ExecuteAsync(
        CompanyMovementListRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(request, CompanyMovementKind.IncomingShipment, cancellationToken);
}

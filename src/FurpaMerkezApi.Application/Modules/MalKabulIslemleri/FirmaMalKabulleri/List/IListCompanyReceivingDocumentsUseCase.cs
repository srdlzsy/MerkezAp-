using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;

namespace FurpaMerkezApi.Application.Modules.MalKabulIslemleri.FirmaMalKabulleri.List;

public interface IListCompanyReceivingDocumentsUseCase
{
    Task<IReadOnlyCollection<CompanyMovementListItemDto>> ExecuteAsync(
        CompanyMovementListRequest request,
        CancellationToken cancellationToken);
}

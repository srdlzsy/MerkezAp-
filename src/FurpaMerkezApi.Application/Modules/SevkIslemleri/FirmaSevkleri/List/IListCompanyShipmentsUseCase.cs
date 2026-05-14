using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;

namespace FurpaMerkezApi.Application.Modules.SevkIslemleri.FirmaSevkleri.List;

public interface IListCompanyShipmentsUseCase
{
    Task<IReadOnlyCollection<CompanyMovementListItemDto>> ExecuteAsync(
        CompanyMovementListRequest request,
        CompanyMovementKind kind,
        CancellationToken cancellationToken);
}

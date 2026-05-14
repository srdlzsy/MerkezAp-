using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;

namespace FurpaMerkezApi.Application.Modules.IadeIslemleri.FirmaIadeleri.Detail;

public interface IGetCompanyReturnDetailUseCase
{
    Task<CompanyMovementDetailDto> ExecuteAsync(
        CompanyMovementDetailRequest request,
        CancellationToken cancellationToken);
}

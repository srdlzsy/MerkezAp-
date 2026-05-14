using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;

namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Detail;

public interface IGetIssuedCompanyOrderDetailUseCase
{
    Task<CompanyOrderDetailDto> ExecuteAsync(
        CompanyOrderDetailRequest request,
        CancellationToken cancellationToken);
}

using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;

namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.AlinanFirmaSiparisleri.Detail;

public interface IGetReceivedCompanyOrderDetailUseCase
{
    Task<CompanyOrderDetailDto> ExecuteAsync(
        CompanyOrderDetailRequest request,
        CancellationToken cancellationToken);
}

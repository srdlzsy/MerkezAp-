namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Create;

public interface ICreateIssuedCompanyOrderUseCase
{
    Task<CreateIssuedCompanyOrderResponse> ExecuteAsync(
        CreateIssuedCompanyOrderRequest request,
        CancellationToken cancellationToken);
}

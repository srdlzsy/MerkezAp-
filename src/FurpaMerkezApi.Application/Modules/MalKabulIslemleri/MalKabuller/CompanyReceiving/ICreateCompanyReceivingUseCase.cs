namespace FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving;

public interface ICreateCompanyReceivingUseCase
{
    Task<CreateCompanyReceivingResponse> ExecuteAsync(
        CreateCompanyReceivingRequest request,
        CancellationToken cancellationToken);
}

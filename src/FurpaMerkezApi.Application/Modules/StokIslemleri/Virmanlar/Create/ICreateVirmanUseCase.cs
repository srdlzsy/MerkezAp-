namespace FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar.Create;

public interface ICreateVirmanUseCase
{
    Task<CreateVirmanResponse> ExecuteAsync(
        CreateVirmanRequest request,
        CancellationToken cancellationToken);
}

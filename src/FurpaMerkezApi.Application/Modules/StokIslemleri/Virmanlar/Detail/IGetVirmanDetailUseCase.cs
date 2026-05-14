namespace FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar.Detail;

public interface IGetVirmanDetailUseCase
{
    Task<VirmanDetailDto> ExecuteAsync(
        VirmanDetailRequest request,
        CancellationToken cancellationToken);
}

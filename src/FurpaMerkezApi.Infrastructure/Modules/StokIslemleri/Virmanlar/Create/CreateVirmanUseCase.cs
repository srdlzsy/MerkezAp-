using FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar;
using FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar.Create;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Virmanlar.Create;

public sealed class CreateVirmanUseCase(VirmanWriteService virmanWriteService)
    : ICreateVirmanUseCase
{
    public Task<CreateVirmanResponse> ExecuteAsync(
        CreateVirmanRequest request,
        CancellationToken cancellationToken) =>
        virmanWriteService.ExecuteAsync(request, cancellationToken);
}

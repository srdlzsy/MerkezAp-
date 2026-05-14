using FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar;
using FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar.Detail;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Virmanlar.Detail;

public sealed class GetVirmanDetailUseCase(VirmanDetailQueryExecutor queryExecutor)
    : IGetVirmanDetailUseCase
{
    public Task<VirmanDetailDto> ExecuteAsync(
        VirmanDetailRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(request, cancellationToken);
}

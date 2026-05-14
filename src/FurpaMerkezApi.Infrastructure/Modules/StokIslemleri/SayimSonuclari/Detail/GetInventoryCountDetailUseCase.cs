using FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari;
using FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari.Detail;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Common;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.SayimSonuclari.Detail;

public sealed class GetInventoryCountDetailUseCase(InventoryCountDetailQueryExecutor queryExecutor)
    : IGetInventoryCountDetailUseCase
{
    public Task<InventoryCountDetailDto> ExecuteAsync(
        InventoryCountDetailRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(request, cancellationToken);
}

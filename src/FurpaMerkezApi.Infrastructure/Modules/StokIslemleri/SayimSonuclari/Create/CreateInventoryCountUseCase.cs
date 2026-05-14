using FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari;
using FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari.Create;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Common;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.SayimSonuclari.Create;

public sealed class CreateInventoryCountUseCase(InventoryCountWriteService inventoryCountWriteService)
    : ICreateInventoryCountUseCase
{
    public Task<CreateInventoryCountResponse> ExecuteAsync(
        CreateInventoryCountRequest request,
        CancellationToken cancellationToken) =>
        inventoryCountWriteService.ExecuteAsync(request, cancellationToken);
}

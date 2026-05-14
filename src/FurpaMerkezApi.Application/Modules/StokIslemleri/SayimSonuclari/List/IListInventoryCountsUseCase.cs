namespace FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari.List;

public interface IListInventoryCountsUseCase
{
    Task<IReadOnlyCollection<InventoryCountListItemDto>> ExecuteAsync(
        InventoryCountListRequest request,
        CancellationToken cancellationToken);
}

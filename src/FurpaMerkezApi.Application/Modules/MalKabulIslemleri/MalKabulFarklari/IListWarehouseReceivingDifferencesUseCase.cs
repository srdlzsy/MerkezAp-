namespace FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabulFarklari;

public interface IListWarehouseReceivingDifferencesUseCase
{
    Task<IReadOnlyCollection<WarehouseReceivingDifferenceDto>> ExecuteAsync(
        WarehouseReceivingDifferenceListRequest request,
        CancellationToken cancellationToken);
}

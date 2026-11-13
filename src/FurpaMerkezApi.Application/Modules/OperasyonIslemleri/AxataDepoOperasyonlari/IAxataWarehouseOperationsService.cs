namespace FurpaMerkezApi.Application.Modules.OperasyonIslemleri.AxataDepoOperasyonlari;

public interface IAxataWarehouseOperationsService
{
    Task<AxataWarehouseOverviewDto> GetOverviewAsync(
        int? warehouseNo,
        DateOnly? date,
        CancellationToken cancellationToken);

    Task<AxataDocumentTraceDto> GetTraceAsync(
        string searchTerm,
        CancellationToken cancellationToken);

    Task<AxataPickingPerformanceDto> GetPickingPerformanceAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        int? warehouseNo,
        int take,
        CancellationToken cancellationToken);

    Task<AxataFillRateDto> GetFillRateAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        string? targetStoreCode,
        int? warehouseNo,
        int take,
        CancellationToken cancellationToken);

    Task<AxataCapacityDto> GetCapacityAsync(
        int? warehouseNo,
        CancellationToken cancellationToken);
}

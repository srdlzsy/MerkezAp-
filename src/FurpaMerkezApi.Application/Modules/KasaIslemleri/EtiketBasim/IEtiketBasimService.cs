namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBasim;

public interface IEtiketBasimService
{
    Task<IReadOnlyCollection<EtiketBasimSupplierSuggestionDto>> SearchSuppliersAsync(
        EtiketBasimReferenceSearchRequest request,
        CancellationToken cancellationToken);

    Task<EtiketBasimSupplierSuggestionDto> GetSupplierByNameAsync(
        string supplierName,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<EtiketBasimStockSuggestionDto>> SearchStocksAsync(
        EtiketBasimStockSearchRequest request,
        CancellationToken cancellationToken);

    Task<EtiketBasimStockSuggestionDto> GetStockByCodeAsync(
        string stockCode,
        CancellationToken cancellationToken);

    Task<EtiketBasimStockSuggestionDto> GetStockByNameAsync(
        string stockName,
        CancellationToken cancellationToken);

    EtiketBasimCalculationDto Calculate(EtiketBasimCalculationRequest request);

    Task<IReadOnlyCollection<EtiketBasimAcceptanceRecordDto>> ListAcceptanceRecordsAsync(
        DateTime date,
        CancellationToken cancellationToken);

    Task<EtiketBasimAcceptanceRecordDto> GetAcceptanceRecordAsync(
        int id,
        CancellationToken cancellationToken);

    Task<EtiketBasimAcceptanceRecordDto> CreateAcceptanceRecordAsync(
        SaveEtiketBasimAcceptanceRecordRequest request,
        CancellationToken cancellationToken);

    Task<EtiketBasimAcceptanceRecordDto> UpdateAcceptanceRecordAsync(
        int id,
        SaveEtiketBasimAcceptanceRecordRequest request,
        CancellationToken cancellationToken);

    Task DeleteAcceptanceRecordAsync(
        int id,
        CancellationToken cancellationToken);

    Task<EtiketBasimLabelDto> GetLabelAsync(
        int id,
        CancellationToken cancellationToken);

    EtiketBasimLabelDto PreviewLabel(SaveEtiketBasimAcceptanceRecordRequest request);

    Task<IReadOnlyCollection<EtiketBasimReceivedProductReportItemDto>> GetReceivedProductsReportAsync(
        DateTime date,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<EtiketBasimDepotStockReportItemDto>> GetDepotStockReportAsync(
        int warehouseNo,
        DateTime date,
        CancellationToken cancellationToken);

    EtiketBasimMicroTransferUnavailableDto ExplainMicroTransferAvailability(
        EtiketBasimMicroTransferRequest request);
}

namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.ManavMalKabulVeEtiket;

public interface IManavMalKabulVeEtiketService
{
    Task<IReadOnlyCollection<ManavMalKabulVeEtiketSupplierSuggestionDto>> SearchSuppliersAsync(
        ManavMalKabulVeEtiketReferenceSearchRequest request,
        CancellationToken cancellationToken);

    Task<ManavMalKabulVeEtiketSupplierSuggestionDto> GetSupplierByNameAsync(
        string supplierName,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ManavMalKabulVeEtiketStockSuggestionDto>> SearchStocksAsync(
        ManavMalKabulVeEtiketStockSearchRequest request,
        CancellationToken cancellationToken);

    Task<ManavMalKabulVeEtiketStockSuggestionDto> GetStockByCodeAsync(
        string stockCode,
        CancellationToken cancellationToken);

    Task<ManavMalKabulVeEtiketStockSuggestionDto> GetStockByNameAsync(
        string stockName,
        CancellationToken cancellationToken);

    ManavMalKabulVeEtiketCalculationDto Calculate(ManavMalKabulVeEtiketCalculationRequest request);

    Task<IReadOnlyCollection<ManavMalKabulVeEtiketAcceptanceRecordDto>> ListAcceptanceRecordsAsync(
        DateTime date,
        CancellationToken cancellationToken);

    Task<ManavMalKabulVeEtiketAcceptanceRecordDto> GetAcceptanceRecordAsync(
        int id,
        CancellationToken cancellationToken);

    Task<ManavMalKabulVeEtiketAcceptanceRecordDto> CreateAcceptanceRecordAsync(
        SaveManavMalKabulVeEtiketAcceptanceRecordRequest request,
        CancellationToken cancellationToken);

    Task<ManavMalKabulVeEtiketAcceptanceRecordDto> UpdateAcceptanceRecordAsync(
        int id,
        SaveManavMalKabulVeEtiketAcceptanceRecordRequest request,
        CancellationToken cancellationToken);

    Task DeleteAcceptanceRecordAsync(
        int id,
        CancellationToken cancellationToken);

    Task<ManavMalKabulVeEtiketLabelDto> GetLabelAsync(
        int id,
        CancellationToken cancellationToken);

    ManavMalKabulVeEtiketLabelDto PreviewLabel(SaveManavMalKabulVeEtiketAcceptanceRecordRequest request);

    Task<IReadOnlyCollection<ManavMalKabulVeEtiketReceivedProductReportItemDto>> GetReceivedProductsReportAsync(
        DateTime date,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ManavMalKabulVeEtiketDepotStockReportItemDto>> GetDepotStockReportAsync(
        int warehouseNo,
        DateTime date,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ManavMalKabulVeEtiketMicroGoodsReceiptDocumentDto>> GetMicroGoodsReceiptsAsync(
        ManavMalKabulVeEtiketMicroGoodsReceiptQuery request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ManavMalKabulVeEtiketGoodsReceiptComparisonItemDto>> CompareGoodsReceiptsAsync(
        ManavMalKabulVeEtiketMicroGoodsReceiptQuery request,
        CancellationToken cancellationToken);

    Task<ManavMalKabulVeEtiketCreateMicroGoodsReceiptResultDto> CreateMicroGoodsReceiptAsync(
        ManavMalKabulVeEtiketCreateMicroGoodsReceiptRequest request,
        CancellationToken cancellationToken);

    ManavMalKabulVeEtiketMicroTransferUnavailableDto ExplainMicroTransferAvailability(
        ManavMalKabulVeEtiketMicroTransferRequest request);
}

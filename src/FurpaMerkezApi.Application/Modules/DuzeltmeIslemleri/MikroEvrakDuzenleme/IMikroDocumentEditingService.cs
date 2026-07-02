namespace FurpaMerkezApi.Application.Modules.DuzeltmeIslemleri.MikroEvrakDuzenleme;

public interface IMikroDocumentEditingService
{
    Task<IReadOnlyCollection<StockCardListItemDto>> SearchStockCardsAsync(
        StockCardSearchRequest request,
        CancellationToken cancellationToken);

    Task<StockCardDetailDto> GetStockCardAsync(
        string stockCode,
        CancellationToken cancellationToken);

    Task<StockCardUpdateResponse> UpdateStockCardAsync(
        UpdateStockCardRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<StockCardWarehouseSettingsDto>> GetStockCardWarehouseSettingsAsync(
        string stockCode,
        int? warehouseNo,
        CancellationToken cancellationToken);

    Task<StockCardWarehouseUpdateResponse> UpdateStockCardWarehouseSettingsAsync(
        UpdateStockCardWarehouseSettingsRequest request,
        CancellationToken cancellationToken);

    Task<MikroDocumentDeleteResponse> DeleteStockCardWarehouseSettingsAsync(
        DeleteStockCardWarehouseSettingsRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<WarehouseCardListItemDto>> SearchWarehouseCardsAsync(
        WarehouseCardSearchRequest request,
        CancellationToken cancellationToken);

    Task<WarehouseCardDetailDto> GetWarehouseCardAsync(
        int warehouseNo,
        CancellationToken cancellationToken);

    Task<WarehouseCardUpdateResponse> UpdateWarehouseCardAsync(
        UpdateWarehouseCardRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CustomerCardListItemDto>> SearchCustomerCardsAsync(
        CustomerCardSearchRequest request,
        CancellationToken cancellationToken);

    Task<CustomerCardDetailDto> GetCustomerCardAsync(
        string customerCode,
        CancellationToken cancellationToken);

    Task<CustomerCardUpdateResponse> UpdateCustomerCardAsync(
        UpdateCustomerCardRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<StockSalesPriceDto>> GetStockSalesPricesAsync(
        string stockCode,
        int? warehouseNo,
        CancellationToken cancellationToken);

    Task<StockSalesPriceUpsertResponse> UpsertStockSalesPriceAsync(
        UpsertStockSalesPriceRequest request,
        CancellationToken cancellationToken);

    Task<MikroDocumentDeleteResponse> DeleteStockSalesPriceAsync(
        DeleteStockSalesPriceRequest request,
        CancellationToken cancellationToken);

    Task<StockMovementDocumentDto> GetStockMovementDocumentAsync(
        StockMovementDocumentLookupRequest request,
        CancellationToken cancellationToken);

    Task<StockMovementDocumentUpdateResponse> UpdateStockMovementDocumentAsync(
        UpdateStockMovementDocumentRequest request,
        CancellationToken cancellationToken);

    Task<MikroDocumentDeleteResponse> DeleteStockMovementDocumentAsync(
        DeleteStockMovementDocumentRequest request,
        CancellationToken cancellationToken);

    Task<CustomerMovementDocumentDto> GetCustomerMovementDocumentAsync(
        CustomerMovementDocumentLookupRequest request,
        CancellationToken cancellationToken);

    Task<CustomerMovementDocumentUpdateResponse> UpdateCustomerMovementDocumentAsync(
        UpdateCustomerMovementDocumentRequest request,
        CancellationToken cancellationToken);

    Task<MikroDocumentDeleteResponse> DeleteCustomerMovementDocumentAsync(
        DeleteCustomerMovementDocumentRequest request,
        CancellationToken cancellationToken);
}

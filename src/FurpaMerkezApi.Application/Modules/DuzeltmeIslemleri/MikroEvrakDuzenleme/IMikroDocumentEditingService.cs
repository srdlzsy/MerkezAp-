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

    Task<StockMovementDocumentDto> GetStockMovementDocumentAsync(
        StockMovementDocumentLookupRequest request,
        CancellationToken cancellationToken);

    Task<StockMovementDocumentUpdateResponse> UpdateStockMovementDocumentAsync(
        UpdateStockMovementDocumentRequest request,
        CancellationToken cancellationToken);

    Task<CustomerMovementDocumentDto> GetCustomerMovementDocumentAsync(
        CustomerMovementDocumentLookupRequest request,
        CancellationToken cancellationToken);

    Task<CustomerMovementDocumentUpdateResponse> UpdateCustomerMovementDocumentAsync(
        UpdateCustomerMovementDocumentRequest request,
        CancellationToken cancellationToken);
}

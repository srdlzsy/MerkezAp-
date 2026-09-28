using System.Text.Json;

namespace FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.TrendyolGo;

public interface ITrendyolGoIntegrationService
{
    Task<TrendyolGoConnectionStatusDto> GetStatusAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TrendyolGoStoreMappingDto>> ListStoresAsync(CancellationToken cancellationToken);

    Task<TrendyolGoConnectionStatusDto> TestConnectionAsync(
        long storeId,
        CancellationToken cancellationToken);

    Task<JsonElement> ListOrdersAsync(
        TrendyolGoOrderListRequest request,
        CancellationToken cancellationToken);

    Task<JsonElement> GetOrderByNumberAsync(
        string orderNumber,
        CancellationToken cancellationToken);

    Task<JsonElement?> AcceptOrderAsync(
        string packageId,
        CancellationToken cancellationToken);

    Task<JsonElement> GetInvoiceAmountRangeAsync(
        string orderId,
        CancellationToken cancellationToken);

    Task<JsonElement?> MarkOrderInvoicedAsync(
        string packageId,
        TrendyolGoInvoiceRequest request,
        CancellationToken cancellationToken);

    Task<JsonElement> ListBrandsAsync(int page, int size, string? name, CancellationToken cancellationToken);

    Task<JsonElement?> CreateProductsAsync(JsonElement payload, CancellationToken cancellationToken);

    Task<JsonElement?> UpdateProductsAsync(JsonElement payload, CancellationToken cancellationToken);

    Task<JsonElement?> UpdatePriceAndInventoryAsync(JsonElement payload, CancellationToken cancellationToken);

    Task<JsonElement> GetProductBatchResultAsync(string batchRequestId, CancellationToken cancellationToken);

    Task<JsonElement> ListProductsAsync(
        TrendyolGoProductListRequest request,
        CancellationToken cancellationToken);

    Task<JsonElement?> SetProductsSaleStatusAsync(
        bool enabled,
        JsonElement payload,
        CancellationToken cancellationToken);

    Task<JsonElement?> UpdateProductAttributesAsync(JsonElement payload, CancellationToken cancellationToken);

    Task<JsonElement> GetOrdersByPackageIdsAsync(
        IReadOnlyCollection<string> packageIds,
        CancellationToken cancellationToken);

    Task<JsonElement?> MarkItemsUnsuppliedAsync(
        string packageId,
        JsonElement payload,
        CancellationToken cancellationToken);

    Task<JsonElement?> MarkAlternativeItemsAsync(
        string packageId,
        JsonElement payload,
        CancellationToken cancellationToken);

    Task<JsonElement?> MarkManualShipmentAsync(
        string packageId,
        bool delivered,
        CancellationToken cancellationToken);

    Task<JsonElement?> SendInvoiceLinkAsync(JsonElement payload, CancellationToken cancellationToken);

    Task<JsonElement> ListClaimsAsync(
        TrendyolGoClaimListRequest request,
        CancellationToken cancellationToken);

    Task<JsonElement?> AcceptClaimAsync(
        string claimId,
        JsonElement payload,
        CancellationToken cancellationToken);

    Task<JsonElement?> RejectClaimAsync(
        string claimId,
        JsonElement payload,
        CancellationToken cancellationToken);

    Task<JsonElement> GetObjectionableClaimItemsAsync(string claimId, CancellationToken cancellationToken);

    Task<JsonElement?> CreateClaimObjectionAsync(
        string claimId,
        JsonElement payload,
        CancellationToken cancellationToken);
}

public sealed record TrendyolGoConnectionStatusDto(
    bool Enabled,
    string Environment,
    string BaseUrl,
    long SupplierId,
    string IntegrationReferenceCode,
    bool CredentialsConfigured,
    bool? Reachable,
    int? UpstreamStatusCode,
    string Message,
    DateTime CheckedAtUtc);

public sealed record TrendyolGoStoreMappingDto(
    long StoreId,
    int WarehouseNo,
    string StoreName);

public sealed record TrendyolGoOrderListRequest(
    long StoreId,
    long? StartDate,
    long? EndDate,
    int Page,
    int Size,
    IReadOnlyCollection<string> Statuses,
    string SortDirection);

public sealed record TrendyolGoInvoiceRequest(
    decimal? InvoiceAmount,
    int? BagCount,
    string? ReceiptLink,
    decimal? InvoiceTaxAmount);

public sealed record TrendyolGoProductListRequest(
    long StoreId,
    string? ListType,
    string? Barcode,
    string? StockCode,
    long? StartDate,
    long? EndDate,
    IReadOnlyCollection<long> BrandIds,
    int Page,
    int Size);

public sealed record TrendyolGoClaimListRequest(
    string? ClaimItemStatus,
    long? StartDate,
    long? EndDate,
    int Page,
    int Size);

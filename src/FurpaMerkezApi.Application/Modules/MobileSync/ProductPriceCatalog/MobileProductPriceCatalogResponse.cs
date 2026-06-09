namespace FurpaMerkezApi.Application.Modules.MobileSync.ProductPriceCatalog;

public sealed record MobileProductPriceCatalogResponse(
    int WarehouseNo,
    DateTime GeneratedAt,
    DateTime? Since,
    DateTime? SyncToken,
    string? NextCursor,
    bool HasMore,
    int PageSize,
    IReadOnlyCollection<MobileProductPriceCatalogItemDto> Items,
    IReadOnlyCollection<string> DeletedBarcodes);

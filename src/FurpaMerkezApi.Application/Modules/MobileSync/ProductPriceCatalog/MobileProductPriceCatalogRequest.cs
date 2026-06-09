namespace FurpaMerkezApi.Application.Modules.MobileSync.ProductPriceCatalog;

public sealed record MobileProductPriceCatalogRequest(
    int WarehouseNo,
    DateTime? Since,
    string? Cursor,
    int PageSize);

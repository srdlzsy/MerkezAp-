namespace FurpaMerkezApi.Application.Modules.MobileSync.WarehouseCatalog;

public sealed record MobileWarehouseCatalogRequest(
    DateTime? Since,
    string? Cursor,
    int PageSize);

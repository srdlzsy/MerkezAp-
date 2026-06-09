namespace FurpaMerkezApi.Application.Modules.MobileSync.WarehouseCatalog;

public sealed record MobileWarehouseCatalogResponse(
    DateTime GeneratedAt,
    DateTime? Since,
    DateTime? SyncToken,
    string? NextCursor,
    bool HasMore,
    int PageSize,
    IReadOnlyCollection<MobileWarehouseCatalogItemDto> Items,
    IReadOnlyCollection<int> DeletedWarehouseNos);

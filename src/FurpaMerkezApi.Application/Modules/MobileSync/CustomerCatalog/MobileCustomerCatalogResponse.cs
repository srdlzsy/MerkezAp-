namespace FurpaMerkezApi.Application.Modules.MobileSync.CustomerCatalog;

public sealed record MobileCustomerCatalogResponse(
    DateTime GeneratedAt,
    DateTime? Since,
    DateTime? SyncToken,
    string? NextCursor,
    bool HasMore,
    int PageSize,
    IReadOnlyCollection<MobileCustomerCatalogItemDto> Items,
    IReadOnlyCollection<string> DeletedCustomerCodes);

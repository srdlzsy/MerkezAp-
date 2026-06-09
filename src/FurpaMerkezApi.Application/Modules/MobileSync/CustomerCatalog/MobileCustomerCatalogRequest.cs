namespace FurpaMerkezApi.Application.Modules.MobileSync.CustomerCatalog;

public sealed record MobileCustomerCatalogRequest(
    DateTime? Since,
    string? Cursor,
    int PageSize);

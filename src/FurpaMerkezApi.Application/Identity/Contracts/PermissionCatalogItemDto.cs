namespace FurpaMerkezApi.Application.Identity.Contracts;

public sealed record PermissionCatalogItemDto(
    string Code,
    string Name,
    string Description,
    string Module,
    string SubModule,
    string Action);

namespace FurpaMerkezApi.Application.Identity.Contracts;

public sealed record PermissionActionDto(
    string Code,
    string Name,
    string PermissionCode,
    string? Description);

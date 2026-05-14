namespace FurpaMerkezApi.Application.Identity.Contracts;

public sealed record PermissionModuleDto(
    string Code,
    string Name,
    IReadOnlyCollection<PermissionMenuDto> Menus);

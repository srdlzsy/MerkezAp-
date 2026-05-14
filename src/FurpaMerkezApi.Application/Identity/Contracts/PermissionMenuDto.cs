namespace FurpaMerkezApi.Application.Identity.Contracts;

public sealed record PermissionMenuDto(
    string Code,
    string Name,
    IReadOnlyCollection<PermissionActionDto> Actions);

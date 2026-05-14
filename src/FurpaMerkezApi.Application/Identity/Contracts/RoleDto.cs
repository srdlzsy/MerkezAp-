namespace FurpaMerkezApi.Application.Identity.Contracts;

public sealed record RoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    IReadOnlyCollection<PermissionDto> Permissions,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

namespace FurpaMerkezApi.Application.Identity.Contracts;

public sealed record PermissionDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string ModuleCode,
    string ModuleName,
    string MenuCode,
    string MenuName,
    string ActionCode,
    string ActionName,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

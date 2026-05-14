namespace FurpaMerkezApi.Application.Identity.Contracts;

public sealed record UserDto(
    Guid Id,
    string Username,
    string Email,
    string FirstName,
    string LastName,
    string WarehouseNo,
    string WarehouseName,
    bool IsActive,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<PermissionModuleDto> Modules,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

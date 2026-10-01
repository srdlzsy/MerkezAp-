namespace FurpaMerkezApi.Application.Identity.Contracts;

public sealed record UserClientRoleDto(
    string ClientType,
    Guid RoleId,
    string RoleName);

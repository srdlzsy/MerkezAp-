namespace FurpaMerkezApi.Application.Identity.Contracts;

public sealed record AssignRolePermissionsRequest(IReadOnlyCollection<Guid> PermissionIds);

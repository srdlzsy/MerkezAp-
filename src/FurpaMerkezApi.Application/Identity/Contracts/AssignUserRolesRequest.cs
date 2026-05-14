namespace FurpaMerkezApi.Application.Identity.Contracts;

public sealed record AssignUserRolesRequest(IReadOnlyCollection<Guid> RoleIds);

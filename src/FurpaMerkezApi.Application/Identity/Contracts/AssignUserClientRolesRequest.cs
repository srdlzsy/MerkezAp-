namespace FurpaMerkezApi.Application.Identity.Contracts;

public sealed record AssignUserClientRolesRequest(
    string ClientType,
    IReadOnlyCollection<Guid> RoleIds);

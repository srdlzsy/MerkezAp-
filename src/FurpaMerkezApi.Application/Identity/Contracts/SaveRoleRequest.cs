namespace FurpaMerkezApi.Application.Identity.Contracts;

public sealed record SaveRoleRequest(
    string Name,
    string? Description,
    bool IsActive);

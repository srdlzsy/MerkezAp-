namespace FurpaMerkezApi.Application.Identity.Contracts;

public sealed record SavePermissionRequest(
    string Code,
    string Name,
    string? Description);

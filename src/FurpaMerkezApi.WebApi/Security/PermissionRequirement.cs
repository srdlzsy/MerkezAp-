using Microsoft.AspNetCore.Authorization;

namespace FurpaMerkezApi.WebApi.Security;

public sealed class PermissionRequirement(string permissionCode) : IAuthorizationRequirement
{
    public string PermissionCode { get; } = string.IsNullOrWhiteSpace(permissionCode)
        ? throw new ArgumentException("Permission code is required.", nameof(permissionCode))
        : permissionCode;
}

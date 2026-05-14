using FurpaMerkezApi.Application.Identity.Contracts;
using FurpaMerkezApi.Application.Security;
using FurpaMerkezApi.Domain.Entities;

namespace FurpaMerkezApi.Infrastructure.Services;

internal static class AuthMappingExtensions
{
    public static UserDto ToDto(this AppUser user)
    {
        var roles = user.UserRoles
            .Select(userRole => userRole.Role.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => role)
            .ToArray();

        var permissionDtos = user.UserRoles
            .SelectMany(userRole => userRole.Role.RolePermissions)
            .Select(rolePermission => rolePermission.Permission)
            .DistinctBy(permission => permission.Id)
            .OrderBy(permission => permission.Code)
            .Select(permission => permission.ToDto())
            .ToArray();

        return new UserDto(
            user.Id,
            user.Username,
            user.Email,
            user.FirstName,
            user.LastName,
            user.WarehouseNo,
            user.WarehouseName,
            user.IsActive,
            roles,
            permissionDtos.Select(permission => permission.Code).ToArray(),
            PermissionTreeBuilder.BuildFromPermissions(permissionDtos),
            user.CreatedAtUtc,
            user.UpdatedAtUtc);
    }

    public static RoleDto ToDto(this AppRole role)
    {
        var permissions = role.RolePermissions
            .Select(rolePermission => rolePermission.Permission)
            .DistinctBy(permission => permission.Id)
            .OrderBy(permission => permission.Code)
            .Select(permission => permission.ToDto())
            .ToArray();

        return new RoleDto(
            role.Id,
            role.Name,
            role.Description,
            role.IsActive,
            permissions,
            role.CreatedAtUtc,
            role.UpdatedAtUtc);
    }

    public static PermissionDto ToDto(this AppPermission permission) =>
        CreatePermissionDto(permission);

    private static PermissionDto CreatePermissionDto(AppPermission permission)
    {
        var definition = PermissionCatalog.Find(permission.Code);

        if (definition is not null)
        {
            return new PermissionDto(
                permission.Id,
                permission.Code,
                permission.Name,
                permission.Description,
                definition.ModuleCode,
                definition.ModuleName,
                definition.MenuCode,
                definition.MenuName,
                definition.ActionCode,
                definition.ActionName,
                permission.CreatedAtUtc,
                permission.UpdatedAtUtc);
        }

        var parts = permission.Code.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var moduleCode = parts.Length > 0 ? parts[0] : "custom";
        var actionCode = parts.Length > 1 ? parts[^1] : "custom";
        var menuCode = parts.Length > 2 ? string.Join('.', parts.Skip(1).Take(parts.Length - 2)) : "custom";

        return new PermissionDto(
            permission.Id,
            permission.Code,
            permission.Name,
            permission.Description,
            moduleCode,
            ToDisplayName(moduleCode),
            menuCode,
            ToDisplayName(menuCode),
            actionCode,
            ToActionDisplayName(actionCode),
            permission.CreatedAtUtc,
            permission.UpdatedAtUtc);
    }

    private static string ToDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Custom";
        }

        var parts = value
            .Split(['-', '.'], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant());

        return string.Concat(parts);
    }

    private static string ToActionDisplayName(string actionCode) =>
        actionCode.ToLowerInvariant() switch
        {
            "list" => "Listele",
            "detail" => "Detay",
            "create" => "Ekle",
            "update" => "Guncelle",
            "delete" => "Sil",
            "manage" => "Yonet",
            _ => ToDisplayName(actionCode)
        };
}

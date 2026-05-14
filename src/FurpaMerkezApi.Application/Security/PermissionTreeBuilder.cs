using FurpaMerkezApi.Application.Identity.Contracts;

namespace FurpaMerkezApi.Application.Security;

public static class PermissionTreeBuilder
{
    public static IReadOnlyCollection<PermissionModuleDto> BuildFromDefinitions(IEnumerable<PermissionDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        return definitions
            .OrderBy(definition => definition.ModuleName)
            .ThenBy(definition => definition.MenuName)
            .ThenBy(definition => definition.ActionName)
            .GroupBy(definition => new ModuleKey(definition.ModuleCode, definition.ModuleName))
            .Select(moduleGroup => new PermissionModuleDto(
                moduleGroup.Key.Code,
                moduleGroup.Key.Name,
                moduleGroup
                    .GroupBy(definition => new MenuKey(definition.MenuCode, definition.MenuName))
                    .Select(menuGroup => new PermissionMenuDto(
                        menuGroup.Key.Code,
                        menuGroup.Key.Name,
                        menuGroup
                            .Select(definition => new PermissionActionDto(
                                definition.ActionCode,
                                definition.ActionName,
                                definition.Code,
                                definition.Description))
                            .ToArray()))
                    .ToArray()))
            .ToArray();
    }

    public static IReadOnlyCollection<PermissionModuleDto> BuildFromPermissions(IEnumerable<PermissionDto> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        return permissions
            .OrderBy(permission => permission.ModuleName)
            .ThenBy(permission => permission.MenuName)
            .ThenBy(permission => permission.ActionName)
            .GroupBy(permission => new ModuleKey(permission.ModuleCode, permission.ModuleName))
            .Select(moduleGroup => new PermissionModuleDto(
                moduleGroup.Key.Code,
                moduleGroup.Key.Name,
                moduleGroup
                    .GroupBy(permission => new MenuKey(permission.MenuCode, permission.MenuName))
                    .Select(menuGroup => new PermissionMenuDto(
                        menuGroup.Key.Code,
                        menuGroup.Key.Name,
                        menuGroup
                            .Select(permission => new PermissionActionDto(
                                permission.ActionCode,
                                permission.ActionName,
                                permission.Code,
                                permission.Description))
                            .ToArray()))
                    .ToArray()))
            .ToArray();
    }

    private sealed record ModuleKey(string Code, string Name);

    private sealed record MenuKey(string Code, string Name);
}

namespace FurpaMerkezApi.Application.Security;

public sealed record PermissionDefinition(
    string Code,
    string Name,
    string Description,
    string ModuleCode,
    string ModuleName,
    string MenuCode,
    string MenuName,
    string ActionCode,
    string ActionName);

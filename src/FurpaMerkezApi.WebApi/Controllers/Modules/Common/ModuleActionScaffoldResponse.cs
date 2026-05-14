namespace FurpaMerkezApi.WebApi.Controllers.Modules.Common;

public sealed record ModuleActionScaffoldResponse(
    string ModuleCode,
    string ModuleName,
    string MenuCode,
    string MenuName,
    string ActionCode,
    string ActionName,
    string HttpMethod,
    string PermissionCode,
    string Route,
    string? ResourceId,
    bool IsImplemented,
    string Message);

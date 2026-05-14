using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.Common;

public abstract class ModuleMenuControllerBase(
    string moduleCode,
    string moduleName,
    string menuCode,
    string menuName) : ControllerBase
{
    protected ActionResult<ModuleActionScaffoldResponse> ListNotImplemented(string permissionCode) =>
        CreateNotImplementedResponse("list", permissionCode);

    protected ActionResult<ModuleActionScaffoldResponse> DetailNotImplemented(string permissionCode, string resourceId) =>
        CreateNotImplementedResponse("detail", permissionCode, resourceId);

    protected ActionResult<ModuleActionScaffoldResponse> CreateNotImplemented(string permissionCode) =>
        CreateNotImplementedResponse("create", permissionCode);

    protected ActionResult<ModuleActionScaffoldResponse> UpdateNotImplemented(string permissionCode, string resourceId) =>
        CreateNotImplementedResponse("update", permissionCode, resourceId);

    private ActionResult<ModuleActionScaffoldResponse> CreateNotImplementedResponse(
        string actionCode,
        string permissionCode,
        string? resourceId = null)
    {
        var response = new ModuleActionScaffoldResponse(
            moduleCode,
            moduleName,
            menuCode,
            menuName,
            actionCode,
            GetActionName(actionCode),
            HttpContext.Request.Method,
            permissionCode,
            HttpContext.Request.Path.Value ?? string.Empty,
            resourceId,
            false,
            "Bu endpoint iskelet olarak acildi. Is kurali ve Mikro veritabani entegrasyonu sonraki adimda baglanacak.");

        return StatusCode(StatusCodes.Status501NotImplemented, response);
    }

    private static string GetActionName(string actionCode) =>
        actionCode switch
        {
            "list" => "Listele",
            "detail" => "Detay",
            "create" => "Ekle",
            "update" => "Guncelle",
            _ => actionCode
        };
}

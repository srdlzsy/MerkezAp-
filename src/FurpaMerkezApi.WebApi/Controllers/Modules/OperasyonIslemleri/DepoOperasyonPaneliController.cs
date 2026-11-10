using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.DepoOperasyonPaneli;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.OperasyonIslemleri;

[ApiController]
[Route("api/operasyon-islemleri/depo-operasyon-paneli")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class DepoOperasyonPaneliController(
    IWarehouseOperationsDashboardService dashboardService)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "operasyon-islemleri";
    private const string ModuleName = "OperasyonIslemleri";
    private const string MenuCode = "depo-operasyon-paneli";
    private const string MenuName = "DepoOperasyonPaneli";
    private const string ListPolicy = "operasyon-islemleri.depo-operasyon-paneli.list";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(WarehouseOperationsDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<WarehouseOperationsDashboardDto>> Get(
        [FromQuery] DateOnly? date,
        [FromQuery] int? warehouseNo,
        CancellationToken cancellationToken)
    {
        var resolvedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        var scopedWarehouseNo = User.ResolveWarehouseScopeForPolicy(warehouseNo, ListPolicy);

        return Ok(await dashboardService.GetAsync(resolvedDate, cancellationToken, scopedWarehouseNo));
    }
}

using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.FirmaEvrakTakibi;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.OperasyonIslemleri;

[ApiController]
[Route("api/operasyon-islemleri/firma-evrak-takibi")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class FirmaEvrakTakibiController(
    ICompanyDocumentTrackingService trackingService)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "operasyon-islemleri";
    private const string ModuleName = "OperasyonIslemleri";
    private const string MenuCode = "firma-evrak-takibi";
    private const string MenuName = "FirmaEvrakTakibi";
    private const string ListPolicy = "operasyon-islemleri.firma-evrak-takibi.list";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(CompanyDocumentTrackingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CompanyDocumentTrackingDto>> Get(
        [FromQuery] CompanyDocumentTrackingHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.ResolveWarehouseScopeForPolicy(request.WarehouseNo, ListPolicy);

        return Ok(await trackingService.GetAsync(
            new CompanyDocumentTrackingRequest(request.Date!.Value, warehouseNo),
            cancellationToken));
    }
}

public sealed class CompanyDocumentTrackingHttpRequest
{
    [Required]
    public DateOnly? Date { get; init; }

    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }
}

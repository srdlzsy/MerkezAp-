using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.RaporIslemleri.CiroHedefRaporlari;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.RaporIslemleri.CiroHedefRaporlari;

[ApiController]
[Route("api/rapor-islemleri/ciro-hedef")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class CiroHedefRaporlariController(ICiroHedefRaporlariUseCase ciroHedefRaporlariUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "rapor-islemleri";
    private const string ModuleName = "RaporIslemleri";
    private const string MenuCode = "ciro-hedef-raporlari";
    private const string MenuName = "CiroHedefRaporlari";
    private const string ListPolicy = "rapor-islemleri.ciro-hedef-raporlari.list";
    private const string AllWarehousesPolicy = "rapor-islemleri.ciro-hedef-raporlari.all-warehouses";

    [HttpGet("karsilastirma")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(CiroHedefKarsilastirmaRaporDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CiroHedefKarsilastirmaRaporDto>> Comparison(
        [FromQuery] CiroHedefKarsilastirmaHttpRequest request,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = User.ResolveWarehouseScope(request.WarehouseNo, AllWarehousesPolicy);

        return Ok(await ciroHedefRaporlariUseCase.GetComparisonReportAsync(
            new CiroHedefKarsilastirmaRequest(
                request.StartDate!.Value,
                request.EndDate!.Value,
                resolvedWarehouseNo,
                request.Region),
            cancellationToken));
    }

    [HttpGet("hedef-tanimlari")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<HedefTanimDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<HedefTanimDto>>> Goals(
        [FromQuery] int? year,
        CancellationToken cancellationToken) =>
        Ok(await ciroHedefRaporlariUseCase.GetGoalsAsync(year, cancellationToken));
}

public sealed class CiroHedefKarsilastirmaHttpRequest
{
    [Required]
    public DateOnly? StartDate { get; init; }

    [Required]
    public DateOnly? EndDate { get; init; }

    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public string? Region { get; init; }
}

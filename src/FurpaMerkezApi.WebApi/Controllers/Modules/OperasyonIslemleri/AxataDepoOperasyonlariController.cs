using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.AxataDepoOperasyonlari;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.OperasyonIslemleri;

[ApiController]
[Route("api/operasyon-islemleri/axata-depo-operasyonlari")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class AxataDepoOperasyonlariController(
    IAxataWarehouseOperationsService service)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "operasyon-islemleri";
    private const string ModuleName = "OperasyonIslemleri";
    private const string MenuCode = "axata-depo-kokpiti";
    private const string MenuName = "AxataDepoKokpiti";
    private const string ListPolicy = "operasyon-islemleri.axata-depo-kokpiti.list";
    private const string DetailPolicy = "operasyon-islemleri.axata-depo-kokpiti.detail";

    [HttpGet("overview")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(AxataWarehouseOverviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AxataWarehouseOverviewDto>> GetOverview(
        [FromQuery] int? warehouseNo,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        return Ok(await service.GetOverviewAsync(warehouseNo, date, cancellationToken));
    }

    [HttpGet("trace")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(AxataDocumentTraceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataDocumentTraceDto>> GetTrace(
        [FromQuery] string searchTerm,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Geçersiz arama",
                Detail = "Arama yapmak için sipariş, irsaliye veya sevk numarası giriniz."
            });
        }

        return Ok(await service.GetTraceAsync(searchTerm, cancellationToken));
    }

    [HttpGet("picking-performance")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(AxataPickingPerformanceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AxataPickingPerformanceDto>> GetPickingPerformance(
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        [FromQuery] int? warehouseNo,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        return Ok(await service.GetPickingPerformanceAsync(startDate, endDate, warehouseNo, take, cancellationToken));
    }

    [HttpGet("fill-rate")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(AxataFillRateDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AxataFillRateDto>> GetFillRate(
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        [FromQuery] string? targetStoreCode,
        [FromQuery] int? warehouseNo,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        return Ok(await service.GetFillRateAsync(startDate, endDate, targetStoreCode, warehouseNo, take, cancellationToken));
    }

    [HttpGet("capacity")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(AxataCapacityDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AxataCapacityDto>> GetCapacity(
        [FromQuery] int? warehouseNo,
        CancellationToken cancellationToken)
    {
        return Ok(await service.GetCapacityAsync(warehouseNo, cancellationToken));
    }
}

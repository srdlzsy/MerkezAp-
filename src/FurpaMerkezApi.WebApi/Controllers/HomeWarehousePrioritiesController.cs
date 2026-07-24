using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FurpaMerkezApi.Application.Modules.Home.DepoOncelikleri;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/home/depo-oncelikleri")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class HomeWarehousePrioritiesController(
    IHomeWarehousePrioritiesService prioritiesService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(HomeWarehousePrioritiesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<HomeWarehousePrioritiesDto>> Get(
        [FromQuery] HomeWarehousePrioritiesHttpRequest request,
        CancellationToken cancellationToken)
    {
        var date = request.Date ?? DateOnly.FromDateTime(DateTime.Today);
        var warehouseNo = User.ResolveWarehouseScope(request.WarehouseNo);

        return Ok(await prioritiesService.GetAsync(
            new HomeWarehousePrioritiesRequest(
                date,
                warehouseNo,
                ResolveWarehouseName(warehouseNo),
                User.GetRequiredUserId()),
            cancellationToken));
    }

    private string? ResolveWarehouseName(int? warehouseNo)
    {
        if (!warehouseNo.HasValue || warehouseNo.Value != User.GetRequiredWarehouseNo())
        {
            return null;
        }

        return User.FindFirstValue("warehouse_name");
    }
}

public sealed class HomeWarehousePrioritiesHttpRequest
{
    public DateOnly? Date { get; init; }

    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }
}

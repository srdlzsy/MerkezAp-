using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.GreenGrocer.Reports;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.GreenGrocer.Orders;

[ApiController]
[Route("api/green-grocer/orders")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class GreenGrocerOrdersController(
    IDeleteGreenGrocerOrderUseCase deleteGreenGrocerOrderUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "green-grocer";
    private const string ModuleName = "GreenGrocer";
    private const string MenuCode = "reports";
    private const string MenuName = "Reports";
    private const string UpdatePolicy = "green-grocer.reports.update";

    [HttpDelete]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(DeleteGreenGrocerOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeleteGreenGrocerOrderResponse>> Delete(
        [FromQuery] DeleteGreenGrocerOrderHttpRequest request,
        CancellationToken cancellationToken)
    {
        var response = await deleteGreenGrocerOrderUseCase.ExecuteAsync(
            new DeleteGreenGrocerOrderRequest(
                request.DocumentSerie!,
                request.DocumentOrderNo!.Value,
                request.WarehouseNo),
            cancellationToken);

        return Ok(response);
    }
}

public sealed class DeleteGreenGrocerOrderHttpRequest
{
    [Required]
    [StringLength(20, MinimumLength = 1)]
    public string? DocumentSerie { get; init; }

    [Required]
    [Range(0, int.MaxValue)]
    public int? DocumentOrderNo { get; init; }

    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }
}

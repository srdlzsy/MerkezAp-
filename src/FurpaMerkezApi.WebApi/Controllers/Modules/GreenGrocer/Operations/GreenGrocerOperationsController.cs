using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.GreenGrocer.Operations;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.GreenGrocer.Operations;

[ApiController]
[Route("api/green-grocer/operations")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class GreenGrocerOperationsController(
    IGreenGrocerOperationsUseCase greenGrocerOperationsUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "green-grocer";
    private const string ModuleName = "GreenGrocer";
    private const string MenuCode = "operations";
    private const string MenuName = "Operations";
    private const string ListPolicy = "green-grocer.operations.list";
    private const string CreatePolicy = "green-grocer.operations.create";

    [HttpGet("overview")]
    [HttpGet("ozet")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(GreenGrocerOperationsOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GreenGrocerOperationsOverviewDto>> Overview(
        [FromQuery] GreenGrocerOperationsOverviewHttpRequest request,
        CancellationToken cancellationToken)
    {
        var endDate = (request.EndDate ?? DateTime.Today).Date;
        var startDate = (request.StartDate ?? endDate.AddDays(-7)).Date;
        var warehouseNo = User.ResolveWarehouseNoForPolicy(request.WarehouseNo, ListPolicy);

        return Ok(await greenGrocerOperationsUseCase.GetOverviewAsync(
            new GreenGrocerOperationsOverviewRequest(
                startDate,
                endDate,
                warehouseNo,
                request.TypeCode,
                request.Search,
                request.OnlyWithActivity,
                request.Take),
            cancellationToken));
    }

    [HttpPost("adjustments/preview")]
    [HttpPost("duzeltmeler/onizleme")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(GreenGrocerOperationsAdjustmentPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<GreenGrocerOperationsAdjustmentPreviewDto> PreviewAdjustment(
        [FromBody] GreenGrocerOperationsAdjustmentPreviewHttpRequest request)
    {
        var warehouseNo = User.ResolveWarehouseNoForPolicy(request.WarehouseNo, ListPolicy);

        return Ok(greenGrocerOperationsUseCase.PreviewAdjustment(
            new GreenGrocerOperationsAdjustmentPreviewRequest(
                warehouseNo,
                request.Direction!,
                request.MovementDate,
                request.DocumentSerie,
                request.ReasonCode,
                request.Lines.Select(MapLine).ToArray())));
    }

    [HttpPost("adjustments")]
    [HttpPost("duzeltmeler")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(GreenGrocerOperationsAdjustmentApplyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(GreenGrocerOperationsAdjustmentApplyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GreenGrocerOperationsAdjustmentApplyResponse>> ApplyAdjustment(
        [FromBody] GreenGrocerOperationsAdjustmentApplyHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.ResolveWarehouseNoForPolicy(request.WarehouseNo, CreatePolicy);
        var response = await greenGrocerOperationsUseCase.ApplyAdjustmentAsync(
            new GreenGrocerOperationsAdjustmentApplyRequest(
                User.GetRequiredUserId(),
                request.ClientRequestId,
                warehouseNo,
                request.Direction!,
                request.MovementDate,
                request.DocumentDate,
                request.DocumentNo,
                request.DocumentSerie,
                request.CounterWarehouseNo,
                request.ReasonCode,
                request.Description,
                request.Creator,
                request.Acceptor,
                request.Lines.Select(MapLine).ToArray()),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    private static GreenGrocerOperationsAdjustmentLineRequest MapLine(
        GreenGrocerOperationsAdjustmentLineHttpRequest line) =>
        new(
            line.StockCode!,
            line.Quantity,
            line.UnitPointer,
            line.UnitPrice,
            line.Description,
            line.PartyCode,
            line.LotNo,
            line.ProjectCode);
}

public sealed class GreenGrocerOperationsOverviewHttpRequest
{
    public DateTime? StartDate { get; init; }

    public DateTime? EndDate { get; init; }

    [Range(1, int.MaxValue)]
    public int WarehouseNo { get; init; } = 56;

    [StringLength(20)]
    public string? TypeCode { get; init; }

    [StringLength(100)]
    public string? Search { get; init; }

    public bool OnlyWithActivity { get; init; } = true;

    [Range(1, 2000)]
    public int Take { get; init; } = 500;
}

public sealed class GreenGrocerOperationsAdjustmentPreviewHttpRequest
{
    [Range(1, int.MaxValue)]
    public int WarehouseNo { get; init; } = 56;

    [Required]
    [StringLength(30)]
    public string? Direction { get; init; }

    public DateTime? MovementDate { get; init; }

    [StringLength(20)]
    public string? DocumentSerie { get; init; }

    [StringLength(25)]
    public string? ReasonCode { get; init; }

    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<GreenGrocerOperationsAdjustmentLineHttpRequest> Lines { get; init; } =
        Array.Empty<GreenGrocerOperationsAdjustmentLineHttpRequest>();
}

public sealed class GreenGrocerOperationsAdjustmentApplyHttpRequest
{
    [Required]
    public Guid ClientRequestId { get; init; }

    [Range(1, int.MaxValue)]
    public int WarehouseNo { get; init; } = 56;

    [Required]
    [StringLength(30)]
    public string? Direction { get; init; }

    public DateTime? MovementDate { get; init; }

    public DateTime? DocumentDate { get; init; }

    [StringLength(50)]
    public string? DocumentNo { get; init; }

    [StringLength(20)]
    public string? DocumentSerie { get; init; }

    [Range(1, int.MaxValue)]
    public int CounterWarehouseNo { get; init; } = 1;

    [StringLength(25)]
    public string? ReasonCode { get; init; }

    [StringLength(50)]
    public string? Description { get; init; }

    [StringLength(25)]
    public string? Creator { get; init; }

    [StringLength(25)]
    public string? Acceptor { get; init; }

    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<GreenGrocerOperationsAdjustmentLineHttpRequest> Lines { get; init; } =
        Array.Empty<GreenGrocerOperationsAdjustmentLineHttpRequest>();
}

public sealed class GreenGrocerOperationsAdjustmentLineHttpRequest
{
    [Required]
    [StringLength(25)]
    public string? StockCode { get; init; }

    [Range(0.0001d, double.MaxValue)]
    public double Quantity { get; init; }

    [Range(1, byte.MaxValue)]
    public int UnitPointer { get; init; } = 1;

    [Range(0d, double.MaxValue)]
    public double UnitPrice { get; init; }

    [StringLength(50)]
    public string? Description { get; init; }

    [StringLength(25)]
    public string? PartyCode { get; init; }

    [Range(0, int.MaxValue)]
    public int LotNo { get; init; }

    [StringLength(25)]
    public string? ProjectCode { get; init; }
}

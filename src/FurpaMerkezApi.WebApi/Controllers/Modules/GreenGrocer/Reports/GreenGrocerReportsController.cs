using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.GreenGrocer.Reports;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.GreenGrocer.Reports;

[ApiController]
[Route("api/green-grocer/reports")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class GreenGrocerReportsController(
    IGreenGrocerReportsUseCase greenGrocerReportsUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "green-grocer";
    private const string ModuleName = "GreenGrocer";
    private const string MenuCode = "reports";
    private const string MenuName = "Reports";
    private const string ListPolicy = "green-grocer.reports.list";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<GreenGrocerProductReportItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<GreenGrocerProductReportItemDto>>> SummaryDefault(
        [FromQuery] GreenGrocerReportHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await greenGrocerReportsUseCase.GetSummaryAsync(ToApplicationRequest(request), cancellationToken));

    [HttpGet("dashboard")]
    [HttpGet("ozet")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(GreenGrocerDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GreenGrocerDashboardDto>> Dashboard(
        [FromQuery] GreenGrocerReportHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await greenGrocerReportsUseCase.GetDashboardAsync(ToApplicationRequest(request), cancellationToken));

    [HttpGet("type-options")]
    [HttpGet("tip-secenekleri")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<GreenGrocerTypeOptionDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyCollection<GreenGrocerTypeOptionDto>> TypeOptions() =>
        Ok(greenGrocerReportsUseCase.GetTypeOptions());

    [HttpGet("summary")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<GreenGrocerProductReportItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<GreenGrocerProductReportItemDto>>> Summary(
        [FromQuery] GreenGrocerReportHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await greenGrocerReportsUseCase.GetSummaryAsync(ToApplicationRequest(request), cancellationToken));

    [HttpGet("by-branch")]
    [HttpGet("sube")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(GreenGrocerBranchReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GreenGrocerBranchReportDto>> ByBranch(
        [FromQuery] GreenGrocerReportHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await greenGrocerReportsUseCase.GetByBranchAsync(ToApplicationRequest(request), cancellationToken));

    [HttpGet("by-product")]
    [HttpGet("urun")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<GreenGrocerProductReportGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<GreenGrocerProductReportGroupDto>>> ByProduct(
        [FromQuery] GreenGrocerReportHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await greenGrocerReportsUseCase.GetByProductAsync(ToApplicationRequest(request), cancellationToken));

    [HttpGet("greens")]
    [HttpGet("yesillik")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<GreenGrocerGreenReportItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<GreenGrocerGreenReportItemDto>>> Greens(
        [FromQuery] GreenGrocerReportHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await greenGrocerReportsUseCase.GetGreensAsync(ToApplicationRequest(request), cancellationToken));

    private GreenGrocerReportDateRequest ToApplicationRequest(GreenGrocerReportHttpRequest request)
    {
        var date = request.Date ?? request.DateToGet ?? DateTime.Today;

        return new GreenGrocerReportDateRequest(
            date,
            User.ResolveWarehouseScope(request.WarehouseNo),
            request.TypeCode,
            request.Search,
            request.IncludeLazyBranches,
            request.Take);
    }
}

public sealed class GreenGrocerReportHttpRequest
{
    public DateTime? Date { get; init; }

    public DateTime? DateToGet { get; init; }

    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [StringLength(20)]
    public string? TypeCode { get; init; }

    [StringLength(100)]
    public string? Search { get; init; }

    public bool IncludeLazyBranches { get; init; } = true;

    [Range(1, 5000)]
    public int Take { get; init; } = 1000;
}

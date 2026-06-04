using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.GreenGrocer.Reports;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
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
        [FromQuery] GreenGrocerReportDateHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await greenGrocerReportsUseCase.GetSummaryAsync(ToApplicationRequest(request), cancellationToken));

    [HttpGet("summary")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<GreenGrocerProductReportItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<GreenGrocerProductReportItemDto>>> Summary(
        [FromQuery] GreenGrocerReportDateHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await greenGrocerReportsUseCase.GetSummaryAsync(ToApplicationRequest(request), cancellationToken));

    [HttpGet("by-branch")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(GreenGrocerBranchReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GreenGrocerBranchReportDto>> ByBranch(
        [FromQuery] GreenGrocerReportDateHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await greenGrocerReportsUseCase.GetByBranchAsync(ToApplicationRequest(request), cancellationToken));

    [HttpGet("by-product")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<GreenGrocerProductReportGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<GreenGrocerProductReportGroupDto>>> ByProduct(
        [FromQuery] GreenGrocerReportDateHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await greenGrocerReportsUseCase.GetByProductAsync(ToApplicationRequest(request), cancellationToken));

    [HttpGet("greens")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<GreenGrocerGreenReportItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<GreenGrocerGreenReportItemDto>>> Greens(
        [FromQuery] GreenGrocerReportDateHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await greenGrocerReportsUseCase.GetGreensAsync(ToApplicationRequest(request), cancellationToken));

    private static GreenGrocerReportDateRequest ToApplicationRequest(GreenGrocerReportDateHttpRequest request)
    {
        var date = request.Date ?? request.DateToGet;

        if (!date.HasValue)
        {
            throw new ArgumentException("Date is required.", nameof(request.Date));
        }

        return new GreenGrocerReportDateRequest(date.Value);
    }
}

public sealed class GreenGrocerReportDateHttpRequest
{
    public DateTime? Date { get; init; }

    public DateTime? DateToGet { get; init; }
}

using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.GreenGrocer.ProductCases;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.GreenGrocer.ProductCases;

[ApiController]
[Route("api/green-grocer/product-case-profiles")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class GreenGrocerProductCasesController(
    IGreenGrocerProductCaseService productCaseService)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "green-grocer";
    private const string ModuleName = "GreenGrocer";
    private const string MenuCode = "product-case-profiles";
    private const string MenuName = "ProductCaseProfiles";
    private const string ListPolicy = "green-grocer.product-case-profiles.list";
    private const string DetailPolicy = "green-grocer.product-case-profiles.detail";
    private const string UpdatePolicy = "green-grocer.product-case-profiles.update";
    private const string DeletePolicy = "green-grocer.product-case-profiles.delete";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<GreenGrocerProductCaseProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IReadOnlyCollection<GreenGrocerProductCaseProfileDto>>> List(
        [FromQuery] GreenGrocerProductCaseProfileListHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await productCaseService.ListProfilesAsync(
            new GreenGrocerProductCaseProfileListRequest(
                request.Search,
                request.IncludeInactive,
                request.Take),
            cancellationToken));

    [HttpGet("{stockCode}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(GreenGrocerProductCaseProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GreenGrocerProductCaseProfileDto>> Detail(
        [FromRoute] string stockCode,
        CancellationToken cancellationToken) =>
        Ok(await productCaseService.GetProfileAsync(stockCode, cancellationToken));

    [HttpPut("{stockCode}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(GreenGrocerProductCaseProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GreenGrocerProductCaseProfileDto>> Save(
        [FromRoute] string stockCode,
        [FromBody] SaveGreenGrocerProductCaseProfileHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await productCaseService.SaveProfileAsync(
            stockCode,
            ToApplicationRequest(request),
            User.GetRequiredUserId(),
            cancellationToken));

    [HttpDelete("{stockCode}")]
    [Authorize(Policy = DeletePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        [FromRoute] string stockCode,
        CancellationToken cancellationToken)
    {
        await productCaseService.DeleteProfileAsync(stockCode, User.GetRequiredUserId(), cancellationToken);
        return NoContent();
    }

    [HttpPost("resolution-preview")]
    [HttpPost("cozumleme-onizleme")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(GreenGrocerProductCaseResolutionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GreenGrocerProductCaseResolutionDto>> ResolutionPreview(
        [FromBody] GreenGrocerProductCaseResolutionHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await productCaseService.PreviewResolutionAsync(
            new GreenGrocerProductCaseResolutionRequest(
                request.StockCode!,
                request.InputQuantity,
                request.SourceWarehouseNo,
                request.TargetWarehouseNo,
                request.OrderDate),
            cancellationToken));

    private static SaveGreenGrocerProductCaseProfileRequest ToApplicationRequest(
        SaveGreenGrocerProductCaseProfileHttpRequest request) =>
        new(
            request.IsActive,
            request.InputMode!,
            request.ConversionMode!,
            request.ManualKgPerCase,
            request.ManualUnitsPerCase,
            request.MinExpectedKgPerCase,
            request.MaxExpectedKgPerCase,
            request.AverageWindowDays,
            request.MinAverageRecordCount,
            request.MinAverageCaseCount,
            request.MaxCoefficientOfVariation,
            request.RequiresManualApproval,
            request.AllowOrderLinking,
            request.OverDeliveryTolerancePercent,
            request.Notes);
}

public sealed class GreenGrocerProductCaseProfileListHttpRequest
{
    [StringLength(100)]
    public string? Search { get; init; }

    public bool IncludeInactive { get; init; }

    [Range(1, 500)]
    public int Take { get; init; } = 100;
}

public sealed class SaveGreenGrocerProductCaseProfileHttpRequest
{
    public bool IsActive { get; init; } = true;

    [Required]
    [StringLength(40)]
    public string? InputMode { get; init; } = GreenGrocerProductCaseModes.InputCase;

    [Required]
    [StringLength(60)]
    public string? ConversionMode { get; init; } = GreenGrocerProductCaseModes.ConversionLabelAverageKgPerCase;

    [Range(0.0001d, double.MaxValue)]
    public double? ManualKgPerCase { get; init; }

    [Range(0.0001d, double.MaxValue)]
    public double? ManualUnitsPerCase { get; init; }

    [Range(0d, double.MaxValue)]
    public double? MinExpectedKgPerCase { get; init; }

    [Range(0d, double.MaxValue)]
    public double? MaxExpectedKgPerCase { get; init; }

    [Range(1, 365)]
    public int AverageWindowDays { get; init; } = 30;

    [Range(0, 100000)]
    public int MinAverageRecordCount { get; init; } = 5;

    [Range(0, 100000)]
    public int MinAverageCaseCount { get; init; } = 20;

    [Range(0d, 10d)]
    public double MaxCoefficientOfVariation { get; init; } = 0.25d;

    public bool RequiresManualApproval { get; init; }

    public bool AllowOrderLinking { get; init; } = true;

    [Range(0d, 1000d)]
    public double OverDeliveryTolerancePercent { get; init; } = 20d;

    [StringLength(1000)]
    public string? Notes { get; init; }
}

public sealed class GreenGrocerProductCaseResolutionHttpRequest
{
    [Required]
    [StringLength(25)]
    public string? StockCode { get; init; }

    [Range(0.0001d, double.MaxValue)]
    public double InputQuantity { get; init; }

    [Range(1, int.MaxValue)]
    public int SourceWarehouseNo { get; init; } = 56;

    [Range(1, int.MaxValue)]
    public int? TargetWarehouseNo { get; init; }

    public DateTime? OrderDate { get; init; }
}

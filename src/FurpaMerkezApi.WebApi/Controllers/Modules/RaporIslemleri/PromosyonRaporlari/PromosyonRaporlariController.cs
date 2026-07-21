using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.RaporIslemleri.PromosyonRaporlari;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.RaporIslemleri.PromosyonRaporlari;

[ApiController]
[Route("api/rapor-islemleri/promosyon-raporlari")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class PromosyonRaporlariController(IPromotionReportsUseCase promotionReportsUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "rapor-islemleri";
    private const string ModuleName = "RaporIslemleri";
    private const string MenuCode = "promosyon-raporlari";
    private const string MenuName = "PromosyonRaporlari";
    private const string ListPolicy = "rapor-islemleri.promosyon-raporlari.list";

    [HttpGet("bultenler")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<PromotionBulletinListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<PromotionBulletinListItemDto>>> Bulletins(
        [FromQuery] PromotionBulletinListHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await promotionReportsUseCase.GetBulletinsAsync(
            new PromotionBulletinListRequest(
                User.ResolveWarehouseScope(request.WarehouseNo),
                request.ActiveOn,
                request.OnlyActive,
                request.Search,
                request.Take),
            cancellationToken));

    [HttpGet("performans")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(PromotionPerformanceReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PromotionPerformanceReportDto>> Performance(
        [FromQuery] PromotionPerformanceHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await promotionReportsUseCase.GetPerformanceAsync(
            ToPerformanceRequest(request),
            cancellationToken));

    [HttpGet("satis-marj-etkisi")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(PromotionPerformanceReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<ActionResult<PromotionPerformanceReportDto>> SalesMarginImpact(
        [FromQuery] PromotionPerformanceHttpRequest request,
        CancellationToken cancellationToken) =>
        Performance(request, cancellationToken);

    [HttpGet("performans/sube")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<PromotionBranchPerformanceItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<PromotionBranchPerformanceItemDto>>> BranchPerformance(
        [FromQuery] PromotionPerformanceHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await promotionReportsUseCase.GetBranchPerformanceAsync(
            ToPerformanceRequest(request),
            cancellationToken));

    private PromotionPerformanceRequest ToPerformanceRequest(PromotionPerformanceHttpRequest request) =>
        new(
            User.ResolveWarehouseScope(request.WarehouseNo),
            request.StartDate!.Value,
            request.EndDate!.Value,
            request.PromotionCode,
            request.Search,
            request.Take);
}

public sealed class PromotionBulletinListHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public DateTime? ActiveOn { get; init; }

    public bool OnlyActive { get; init; }

    [StringLength(100)]
    public string? Search { get; init; }

    [Range(1, 1000)]
    public int Take { get; init; } = 100;
}

public sealed class PromotionPerformanceHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Required]
    public DateTime? StartDate { get; init; }

    [Required]
    public DateTime? EndDate { get; init; }

    [StringLength(25)]
    public string? PromotionCode { get; init; }

    [StringLength(100)]
    public string? Search { get; init; }

    [Range(1, 1000)]
    public int Take { get; init; } = 250;
}

using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaCirolari;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaCirolari.Detail;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaCirolari.List;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaCirolari.Overview;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.KasaIslemleri.KasaCirolari;

[ApiController]
[Route("api/kasa-islemleri/kasa-cirolari")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class KasaCirolariController(
    IListCashTurnoversUseCase listCashTurnoversUseCase,
    IGetCashTurnoverDetailUseCase getCashTurnoverDetailUseCase,
    IGetCashTurnoverOverviewUseCase getCashTurnoverOverviewUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "kasa-islemleri";
    private const string ModuleName = "KasaIslemleri";
    private const string MenuCode = "kasa-cirolari";
    private const string MenuName = "KasaCirolari";
    private const string ListPolicy = "kasa-islemleri.kasa-cirolari.list";
    private const string DetailPolicy = "kasa-islemleri.kasa-cirolari.detail";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CashTurnoverListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<CashTurnoverListItemDto>>> List(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken)
        => Ok(await ExecuteListAsync(request, CashTurnoverSource.New, cancellationToken));

    [HttpGet("yeni")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CashTurnoverListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<CashTurnoverListItemDto>>> ListNew(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken)
        => Ok(await ExecuteListAsync(request, CashTurnoverSource.New, cancellationToken));

    [HttpGet("eski")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CashTurnoverListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<CashTurnoverListItemDto>>> ListOld(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken)
        => Ok(await ExecuteListAsync(request, CashTurnoverSource.Old, cancellationToken));

    [HttpGet("toplam")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CashTurnoverListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<CashTurnoverListItemDto>>> ListCombined(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken)
        => Ok(await ExecuteListAsync(request, CashTurnoverSource.All, cancellationToken));

    [HttpGet("ozet")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(CashTurnoverOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CashTurnoverOverviewDto>> Overview(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken)
        => Ok(await ExecuteOverviewAsync(request, CashTurnoverSource.New, cancellationToken));

    [HttpGet("yeni/ozet")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(CashTurnoverOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CashTurnoverOverviewDto>> OverviewNew(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken)
        => Ok(await ExecuteOverviewAsync(request, CashTurnoverSource.New, cancellationToken));

    [HttpGet("eski/ozet")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(CashTurnoverOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CashTurnoverOverviewDto>> OverviewOld(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken)
        => Ok(await ExecuteOverviewAsync(request, CashTurnoverSource.Old, cancellationToken));

    [HttpGet("toplam/ozet")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(CashTurnoverOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CashTurnoverOverviewDto>> OverviewCombined(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken)
        => Ok(await ExecuteOverviewAsync(request, CashTurnoverSource.All, cancellationToken));

    [HttpGet("detay")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(CashTurnoverDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CashTurnoverDetailDto>> Detail(
        [FromQuery] CashTurnoverDetailHttpRequest request,
        CancellationToken cancellationToken)
        => Ok(await ExecuteDetailAsync(request, CashTurnoverSource.New, cancellationToken));

    [HttpGet("yeni/detay")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(CashTurnoverDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CashTurnoverDetailDto>> DetailNew(
        [FromQuery] CashTurnoverDetailHttpRequest request,
        CancellationToken cancellationToken)
        => Ok(await ExecuteDetailAsync(request, CashTurnoverSource.New, cancellationToken));

    [HttpGet("eski/detay")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(CashTurnoverDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CashTurnoverDetailDto>> DetailOld(
        [FromQuery] CashTurnoverDetailHttpRequest request,
        CancellationToken cancellationToken)
        => Ok(await ExecuteDetailAsync(request, CashTurnoverSource.Old, cancellationToken));

    [HttpGet("toplam/detay")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(CashTurnoverDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CashTurnoverDetailDto>> DetailCombined(
        [FromQuery] CashTurnoverDetailHttpRequest request,
        CancellationToken cancellationToken)
        => Ok(await ExecuteDetailAsync(request, CashTurnoverSource.All, cancellationToken));

    private async Task<IReadOnlyCollection<CashTurnoverListItemDto>> ExecuteListAsync(
        WarehouseOrderDateRangeHttpRequest request,
        CashTurnoverSource source,
        CancellationToken cancellationToken)
    {
        var warehouseNo = request.WarehouseNo ?? User.GetRequiredWarehouseNo();
        return await listCashTurnoversUseCase.ExecuteAsync(
            new CashTurnoverListRequest(
                warehouseNo,
                request.StartDate!.Value,
                request.EndDate!.Value,
                source),
            cancellationToken);
    }

    private Task<CashTurnoverOverviewDto> ExecuteOverviewAsync(
        WarehouseOrderDateRangeHttpRequest request,
        CashTurnoverSource source,
        CancellationToken cancellationToken) =>
        getCashTurnoverOverviewUseCase.ExecuteAsync(
            new CashTurnoverOverviewRequest(
                request.WarehouseNo,
                request.StartDate!.Value,
                request.EndDate!.Value,
                source),
            cancellationToken);

    private async Task<CashTurnoverDetailDto> ExecuteDetailAsync(
        CashTurnoverDetailHttpRequest request,
        CashTurnoverSource source,
        CancellationToken cancellationToken)
    {
        var warehouseNo = request.WarehouseNo ?? User.GetRequiredWarehouseNo();
        return await getCashTurnoverDetailUseCase.ExecuteAsync(
            new CashTurnoverDetailRequest(
                warehouseNo,
                request.BusinessDate!.Value,
                request.ShiftNo!.Value,
                request.CashierCode!,
                source),
            cancellationToken);
    }
}

public sealed class CashTurnoverDetailHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Required]
    public DateTime? BusinessDate { get; init; }

    [Required]
    [Range(0, int.MaxValue)]
    public int? ShiftNo { get; init; }

    [Required]
    [StringLength(25, MinimumLength = 1)]
    public string? CashierCode { get; init; }
}

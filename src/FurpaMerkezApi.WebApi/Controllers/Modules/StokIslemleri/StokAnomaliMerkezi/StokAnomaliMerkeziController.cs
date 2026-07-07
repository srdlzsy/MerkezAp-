using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FurpaMerkezApi.Application.Modules.StokIslemleri.StokAnomaliMerkezi;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.StokIslemleri.StokAnomaliMerkezi;

[ApiController]
[Route("api/stok-islemleri/stok-anomali-merkezi")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class StokAnomaliMerkeziController(IStockAnomalyCenterService stockAnomalyCenterService)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "stok-islemleri";
    private const string ModuleName = "StokIslemleri";
    private const string MenuCode = "stok-anomali-merkezi";
    private const string MenuName = "StokAnomaliMerkezi";
    private const string ListPolicy = "stok-islemleri.stok-anomali-merkezi.list";
    private const string DetailPolicy = "stok-islemleri.stok-anomali-merkezi.detail";
    private const string UpdatePolicy = "stok-islemleri.stok-anomali-merkezi.update";
    private const string ScanPolicy = "stok-islemleri.stok-anomali-merkezi.scan";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(StockAnomalyListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StockAnomalyListResponse>> List(
        [FromQuery] StockAnomalyListHttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.StartDate.HasValue &&
            request.EndDate.HasValue &&
            request.StartDate.Value.Date > request.EndDate.Value.Date)
        {
            throw new ArgumentException("Start date can not be later than end date.");
        }

        var warehouseNo = ResolveWarehouseScope(request.WarehouseNo);

        return Ok(await stockAnomalyCenterService.ListAsync(
            new StockAnomalyListRequest(
                warehouseNo,
                request.Type,
                request.Status,
                request.Severity,
                request.ProductManagerCode,
                request.HasProductManager,
                request.StartDate,
                request.EndDate,
                request.Search,
                request.Take),
            cancellationToken));
    }

    [HttpGet("satin-almacilar")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<StockAnomalyProductManagerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<StockAnomalyProductManagerDto>>> ProductManagers(
        [FromQuery] StockAnomalyProductManagerHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await stockAnomalyCenterService.ListProductManagersAsync(
            new StockAnomalyProductManagerListRequest(
                ResolveWarehouseScope(request.WarehouseNo),
                request.Status),
            cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(StockAnomalyDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockAnomalyDetailDto>> Detail(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await stockAnomalyCenterService.GetAsync(
            id,
            CanViewAllWarehouses(User) ? null : User.GetRequiredWarehouseNo(),
            cancellationToken));
    }

    [HttpPost("tara")]
    [Authorize(Policy = ScanPolicy)]
    [ProducesResponseType(typeof(StockAnomalyScanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StockAnomalyScanResponse>> Scan(
        [FromBody] StockAnomalyScanHttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.StartDate.HasValue &&
            request.EndDate.HasValue &&
            request.StartDate.Value.Date > request.EndDate.Value.Date)
        {
            throw new ArgumentException("Start date can not be later than end date.");
        }

        var warehouseNo = ResolveWarehouseScope(request.WarehouseNo);

        return Ok(await stockAnomalyCenterService.ScanAsync(
            new StockAnomalyScanRequest(
                warehouseNo,
                request.StartDate,
                request.EndDate,
                request.DormantDays,
                request.PendingTransferHours,
                request.HighQuantityLookbackDays,
                request.HighQuantityMultiplier,
                request.HighQuantityMinimum,
                request.TakePerRule),
            cancellationToken));
    }

    [HttpPost("{id:guid}/durum")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(StockAnomalyDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StockAnomalyDetailDto>> ChangeStatus(
        Guid id,
        [FromBody] ChangeStockAnomalyStatusHttpRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await stockAnomalyCenterService.ChangeStatusAsync(
            new ChangeStockAnomalyStatusRequest(
                id,
                request.Status,
                request.Note,
                User.GetRequiredUserId(),
                CanViewAllWarehouses(User) ? null : User.GetRequiredWarehouseNo()),
            cancellationToken));
    }

    private int? ResolveWarehouseScope(int? requestedWarehouseNo)
    {
        if (CanViewAllWarehouses(User))
        {
            return requestedWarehouseNo;
        }

        return User.GetRequiredWarehouseNo();
    }

    private static bool CanViewAllWarehouses(ClaimsPrincipal user) =>
        user.IsInRole("Administrator") || user.IsInRole("Admin");
}

public sealed class StockAnomalyListHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public StockAnomalyType? Type { get; init; }

    public StockAnomalyStatus? Status { get; init; }

    public StockAnomalySeverity? Severity { get; init; }

    [StringLength(25)]
    public string? ProductManagerCode { get; init; }

    public bool? HasProductManager { get; init; }

    public DateTime? StartDate { get; init; }

    public DateTime? EndDate { get; init; }

    [StringLength(100)]
    public string? Search { get; init; }

    [Range(1, 500)]
    public int Take { get; init; } = 100;
}

public sealed class StockAnomalyProductManagerHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public StockAnomalyStatus? Status { get; init; } = StockAnomalyStatus.Open;
}

public sealed class StockAnomalyScanHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public DateTime? StartDate { get; init; }

    public DateTime? EndDate { get; init; }

    [Range(1, 3650)]
    public int DormantDays { get; init; } = 90;

    [Range(1, 720)]
    public int PendingTransferHours { get; init; } = 24;

    [Range(1, 365)]
    public int HighQuantityLookbackDays { get; init; } = 30;

    [Range(1.01d, 100d)]
    public double HighQuantityMultiplier { get; init; } = 3d;

    [Range(0d, double.MaxValue)]
    public double HighQuantityMinimum { get; init; } = 100d;

    [Range(1, 1000)]
    public int TakePerRule { get; init; } = 250;
}

public sealed class ChangeStockAnomalyStatusHttpRequest
{
    public StockAnomalyStatus Status { get; init; }

    [StringLength(500)]
    public string? Note { get; init; }
}

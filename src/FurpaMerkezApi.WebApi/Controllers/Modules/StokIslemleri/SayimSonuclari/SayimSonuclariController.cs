using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.Common.OfflineSync;
using FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari;
using FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari.Create;
using FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari.Detail;
using FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari.List;
using FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari.Offline;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.StokIslemleri.SayimSonuclari;

[ApiController]
[Route("api/stok-islemleri/sayim-sonuclari")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class SayimSonuclariController(
    IListInventoryCountsUseCase listInventoryCountsUseCase,
    IGetInventoryCountDetailUseCase getInventoryCountDetailUseCase,
    ICreateInventoryCountUseCase createInventoryCountUseCase,
    IGetInventoryCountOfflineSyncStatusUseCase getInventoryCountOfflineSyncStatusUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "stok-islemleri";
    private const string ModuleName = "StokIslemleri";
    private const string MenuCode = "sayim-sonuclari";
    private const string MenuName = "SayimSonuclari";
    private const string ListPolicy = "stok-islemleri.sayim-sonuclari.list";
    private const string DetailPolicy = "stok-islemleri.sayim-sonuclari.detail";
    private const string CreatePolicy = "stok-islemleri.sayim-sonuclari.create";
    private const string UpdatePolicy = "stok-islemleri.sayim-sonuclari.update";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<InventoryCountListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<InventoryCountListItemDto>>> List(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = request.WarehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await listInventoryCountsUseCase.ExecuteAsync(
            new InventoryCountListRequest(
                warehouseNo,
                request.StartDate!.Value,
                request.EndDate!.Value),
            cancellationToken));
    }

    [HttpGet("{documentNo:int}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(InventoryCountDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InventoryCountDetailDto>> Detail(
        int documentNo,
        [FromQuery, Required] DateTime? documentDate,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = warehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await getInventoryCountDetailUseCase.ExecuteAsync(
            new InventoryCountDetailRequest(
                resolvedWarehouseNo,
                documentNo,
                documentDate!.Value),
            cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(CreateInventoryCountResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(CreateInventoryCountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateInventoryCountResponse>> Create(
        [FromBody] CreateInventoryCountHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.ResolveWarehouseNo(request.WarehouseNo);
        var response = await createInventoryCountUseCase.ExecuteAsync(
            new CreateInventoryCountRequest(
                warehouseNo,
                User.GetRequiredUserId(),
                request.ClientRequestId,
                request.Name,
                request.DocumentDate,
                request.Lines
                    .Select(line => new CreateInventoryCountLineRequest(
                        line.StockCode,
                        line.Quantity,
                        line.Barcode,
                        line.UnitPointer))
                    .ToArray()),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet("offline-sync/{clientRequestId:guid}")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(OfflineSyncStatusDto<CreateInventoryCountResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OfflineSyncStatusDto<CreateInventoryCountResponse>>> GetOfflineSyncStatus(
        Guid clientRequestId,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.GetRequiredWarehouseNo();
        var requestedByUserId = User.GetRequiredUserId();

        return Ok(await getInventoryCountOfflineSyncStatusUseCase.ExecuteAsync(
            warehouseNo,
            requestedByUserId,
            clientRequestId,
            cancellationToken));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> Update(string id, [FromBody] ModuleActionRequest request) =>
        UpdateNotImplemented(UpdatePolicy, id);
}

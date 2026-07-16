using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.StokIslemleri.Common;
using FurpaMerkezApi.Application.Modules.StokIslemleri.ZayiatFisleri.Create;
using FurpaMerkezApi.Application.Modules.StokIslemleri.ZayiatFisleri.Detail;
using FurpaMerkezApi.Application.Modules.StokIslemleri.ZayiatFisleri.List;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.StokIslemleri.ZayiatFisleri;

[ApiController]
[Route("api/stok-islemleri/zayiat-fisleri")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class ZayiatFisleriController(
    IListOutageReceiptsUseCase listOutageReceiptsUseCase,
    IGetOutageReceiptDetailUseCase getOutageReceiptDetailUseCase,
    ICreateOutageReceiptUseCase createOutageReceiptUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "stok-islemleri";
    private const string ModuleName = "StokIslemleri";
    private const string MenuCode = "zayiat-fisleri";
    private const string MenuName = "ZayiatFisleri";
    private const string ListPolicy = "stok-islemleri.zayiat-fisleri.list";
    private const string DetailPolicy = "stok-islemleri.zayiat-fisleri.detail";
    private const string CreatePolicy = "stok-islemleri.zayiat-fisleri.create";
    private const string UpdatePolicy = "stok-islemleri.zayiat-fisleri.update";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<StockReceiptListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<StockReceiptListItemDto>>> List(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.ResolveWarehouseScope(request.WarehouseNo);

        return Ok(await listOutageReceiptsUseCase.ExecuteAsync(
            new StockReceiptListRequest(
                warehouseNo,
                request.StartDate!.Value,
                request.EndDate!.Value),
            cancellationToken));
    }

    [HttpGet("{documentSerie}/{documentOrderNo:int}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(StockReceiptDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockReceiptDetailDto>> Detail(
        string documentSerie,
        int documentOrderNo,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = warehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await getOutageReceiptDetailUseCase.ExecuteAsync(
            new StockReceiptDetailRequest(
                resolvedWarehouseNo,
                documentSerie,
                documentOrderNo),
            cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(CreateStockReceiptResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateStockReceiptResponse>> Create(
        [FromBody] CreateStockReceiptHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.ResolveWarehouseNo(request.WarehouseNo);
        var response = await createOutageReceiptUseCase.ExecuteAsync(
            new CreateStockReceiptRequest(
                warehouseNo,
                request.Creator,
                request.Acceptor,
                request.MovementDate,
                request.DocumentDate,
                request.DocumentNo,
                request.Description,
                request.Lines
                    .Select(line => new CreateStockReceiptLineRequest(
                        line.StockCode,
                        line.Quantity,
                        line.UnitPointer,
                        line.Description,
                        line.PartyCode,
                        line.LotNo,
                        line.ProjectCode))
                    .ToArray()),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> Update(string id, [FromBody] ModuleActionRequest request) =>
        UpdateNotImplemented(UpdatePolicy, id);
}

using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar;
using FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar.Create;
using FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar.Detail;
using FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar.List;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.StokIslemleri.Virmanlar;

[ApiController]
[Route("api/stok-islemleri/virmanlar")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class VirmanlarController(
    IListVirmansUseCase listVirmansUseCase,
    IGetVirmanDetailUseCase getVirmanDetailUseCase,
    ICreateVirmanUseCase createVirmanUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "stok-islemleri";
    private const string ModuleName = "StokIslemleri";
    private const string MenuCode = "virmanlar";
    private const string MenuName = "Virmanlar";
    private const string ListPolicy = "stok-islemleri.virmanlar.list";
    private const string DetailPolicy = "stok-islemleri.virmanlar.detail";
    private const string CreatePolicy = "stok-islemleri.virmanlar.create";
    private const string UpdatePolicy = "stok-islemleri.virmanlar.update";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<VirmanListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<VirmanListItemDto>>> List(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.ResolveWarehouseScope(request.WarehouseNo);

        return Ok(await listVirmansUseCase.ExecuteAsync(
            new VirmanListRequest(
                warehouseNo,
                request.StartDate!.Value,
                request.EndDate!.Value),
            cancellationToken));
    }

    [HttpGet("{documentSerie}/{documentOrderNo:int}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(VirmanDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VirmanDetailDto>> Detail(
        string documentSerie,
        int documentOrderNo,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = warehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await getVirmanDetailUseCase.ExecuteAsync(
            new VirmanDetailRequest(
                resolvedWarehouseNo,
                documentSerie,
                documentOrderNo),
            cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(CreateVirmanResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateVirmanResponse>> Create(
        [FromBody] CreateVirmanHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.ResolveWarehouseNo(request.WarehouseNo);
        var response = await createVirmanUseCase.ExecuteAsync(
            new CreateVirmanRequest(
                warehouseNo,
                request.MovementDate,
                request.DocumentDate,
                request.DocumentNo,
                request.Description,
                request.Lines
                    .Select(line => new CreateVirmanLineRequest(
                        line.StockCode,
                        Convert.ToByte(line.MovementType),
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

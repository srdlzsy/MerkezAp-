using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.AlinanDepoSiparisleri.Detail;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.AlinanDepoSiparisleri.List;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.SiparisIslemleri.AlinanDepoSiparisleri;

[ApiController]
[Route("api/siparis-islemleri/alinan-depo-siparisleri")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class AlinanDepoSiparisleriController(
    IListReceivedWarehouseOrdersUseCase listReceivedWarehouseOrdersUseCase,
    IGetReceivedWarehouseOrderDetailUseCase getReceivedWarehouseOrderDetailUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "siparis-islemleri";
    private const string ModuleName = "SiparisIslemleri";
    private const string MenuCode = "alinan-depo-siparisleri";
    private const string MenuName = "AlinanDepoSiparisleri";
    private const string ListPolicy = "siparis-islemleri.alinan-depo-siparisleri.list";
    private const string DetailPolicy = "siparis-islemleri.alinan-depo-siparisleri.detail";
    private const string CreatePolicy = "siparis-islemleri.alinan-depo-siparisleri.create";
    private const string UpdatePolicy = "siparis-islemleri.alinan-depo-siparisleri.update";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<WarehouseOrderListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<WarehouseOrderListItemDto>>> List(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.ResolveWarehouseScope(request.WarehouseNo);

        return Ok(await listReceivedWarehouseOrdersUseCase.ExecuteAsync(
            new WarehouseOrderListRequest(
                warehouseNo,
                request.StartDate!.Value,
                request.EndDate!.Value),
            cancellationToken));
    }

    [HttpGet("{documentSerie}/{documentOrderNo:int}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(WarehouseOrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseOrderDetailDto>> Detail(
        string documentSerie,
        int documentOrderNo,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = warehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await getReceivedWarehouseOrderDetailUseCase.ExecuteAsync(
            new WarehouseOrderDetailRequest(
                resolvedWarehouseNo,
                documentSerie,
                documentOrderNo),
            cancellationToken));
    }

    [HttpGet("key/{documentKey}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(WarehouseOrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseOrderDetailDto>> DetailByKey(
        string documentKey,
        CancellationToken cancellationToken) =>
        Ok(await getReceivedWarehouseOrderDetailUseCase.ExecuteAsync(
            WarehouseOrderDocumentKey.Parse(documentKey),
            cancellationToken));

    [HttpPost]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> Create([FromBody] ModuleActionRequest request) =>
        CreateNotImplemented(CreatePolicy);

    [HttpPut("{id}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> Update(string id, [FromBody] ModuleActionRequest request) =>
        UpdateNotImplemented(UpdatePolicy, id);
}

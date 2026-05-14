using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.AlinanFirmaSiparisleri.Detail;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.AlinanFirmaSiparisleri.List;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.SiparisIslemleri.AlinanFirmaSiparisleri;

[ApiController]
[Route("api/siparis-islemleri/alinan-firma-siparisleri")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class AlinanFirmaSiparisleriController(
    IListReceivedCompanyOrdersUseCase listReceivedCompanyOrdersUseCase,
    IGetReceivedCompanyOrderDetailUseCase getReceivedCompanyOrderDetailUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "siparis-islemleri";
    private const string ModuleName = "SiparisIslemleri";
    private const string MenuCode = "alinan-firma-siparisleri";
    private const string MenuName = "AlinanFirmaSiparisleri";
    private const string ListPolicy = "siparis-islemleri.alinan-firma-siparisleri.list";
    private const string DetailPolicy = "siparis-islemleri.alinan-firma-siparisleri.detail";
    private const string CreatePolicy = "siparis-islemleri.alinan-firma-siparisleri.create";
    private const string UpdatePolicy = "siparis-islemleri.alinan-firma-siparisleri.update";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CompanyOrderListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<CompanyOrderListItemDto>>> List(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = request.WarehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await listReceivedCompanyOrdersUseCase.ExecuteAsync(
            new CompanyOrderListRequest(
                warehouseNo,
                request.StartDate!.Value,
                request.EndDate!.Value),
            cancellationToken));
    }

    [HttpGet("{documentSerie}/{documentOrderNo:int}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(CompanyOrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyOrderDetailDto>> Detail(
        string documentSerie,
        int documentOrderNo,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = warehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await getReceivedCompanyOrderDetailUseCase.ExecuteAsync(
            new CompanyOrderDetailRequest(
                resolvedWarehouseNo,
                documentSerie,
                documentOrderNo),
            cancellationToken));
    }

    [HttpGet("key/{documentKey}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(CompanyOrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyOrderDetailDto>> DetailByKey(
        string documentKey,
        CancellationToken cancellationToken) =>
        Ok(await getReceivedCompanyOrderDetailUseCase.ExecuteAsync(
            CompanyOrderDocumentKey.Parse(documentKey),
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

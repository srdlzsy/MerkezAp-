using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.AlinanDepoSiparisleri.Detail;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.AlinanDepoSiparisleri.List;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.AlinanDepoSiparisleri.Print;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using FurpaMerkezApi.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.SiparisIslemleri.AlinanDepoSiparisleri;

[ApiController]
[Route("api/siparis-islemleri/alinan-depo-siparisleri")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class AlinanDepoSiparisleriController(
    IListReceivedWarehouseOrdersUseCase listReceivedWarehouseOrdersUseCase,
    IGetReceivedWarehouseOrderDetailUseCase getReceivedWarehouseOrderDetailUseCase,
    IGetReceivedWarehouseOrdersForPrintUseCase getReceivedWarehouseOrdersForPrintUseCase,
    IReceivedWarehouseOrderPdfRenderer receivedWarehouseOrderPdfRenderer)
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
    private const string PrintPolicy = "siparis-islemleri.alinan-depo-siparisleri.print";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<WarehouseOrderListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<WarehouseOrderListItemDto>>> List(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.ResolveWarehouseScopeForPolicy(request.WarehouseNo, ListPolicy);

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
        var resolvedWarehouseNo = User.ResolveWarehouseNoForPolicy(warehouseNo, DetailPolicy);

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

    [HttpPost("toplu-yazdir")]
    [Authorize(Policy = PrintPolicy)]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BulkPrint(
        [FromBody] BulkPrintReceivedWarehouseOrdersHttpRequest request,
        CancellationToken cancellationToken)
    {
        var detailRequests = request.DocumentKeys
            .Select(WarehouseOrderDocumentKey.Parse)
            .DistinctBy(
                item => $"{item.WarehouseNo}\u001f{item.DocumentSerie}\u001f{item.DocumentOrderNo}",
                StringComparer.OrdinalIgnoreCase)
            .Select(item => item with
            {
                WarehouseNo = User.ResolveWarehouseNoForPolicy(item.WarehouseNo, PrintPolicy)
            })
            .ToArray();

        var documents = await getReceivedWarehouseOrdersForPrintUseCase.ExecuteAsync(
            detailRequests,
            cancellationToken);
        var pdf = receivedWarehouseOrderPdfRenderer.Render(documents);
        var fileName = $"alinan-depo-siparisleri-{DateTime.Now:yyyyMMdd-HHmm}.pdf";

        Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";
        return File(pdf, "application/pdf");
    }

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

public sealed class BulkPrintReceivedWarehouseOrdersHttpRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    public required IReadOnlyCollection<string> DocumentKeys { get; init; }
}

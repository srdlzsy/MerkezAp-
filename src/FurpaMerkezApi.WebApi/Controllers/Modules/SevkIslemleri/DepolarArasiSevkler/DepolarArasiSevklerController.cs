using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.DepolarArasiSevkler.Create;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.DepolarArasiSevkler.Detail;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.DepolarArasiSevkler.List;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.SevkIslemleri.DepolarArasiSevkler;

[ApiController]
[Route("api/sevk-islemleri/depolar-arasi-sevkler")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class DepolarArasiSevklerController(
    IListInterWarehouseShipmentsUseCase listInterWarehouseShipmentsUseCase,
    IGetInterWarehouseShipmentDetailUseCase getInterWarehouseShipmentDetailUseCase,
    ICreateInterWarehouseShipmentUseCase createInterWarehouseShipmentUseCase,
    IDocumentFlowService documentFlowService,
    IEDespatchService eDespatchService)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "sevk-islemleri";
    private const string ModuleName = "SevkIslemleri";
    private const string MenuCode = "depolar-arasi-sevkler";
    private const string MenuName = "DepolarArasiSevkler";
    private const string OutgoingListPolicy = "sevk-islemleri.giden-depolar-arasi-sevkler.list";
    private const string OutgoingDetailPolicy = "sevk-islemleri.giden-depolar-arasi-sevkler.detail";
    private const string OutgoingCreatePolicy = "sevk-islemleri.giden-depolar-arasi-sevkler.create";
    private const string OutgoingUpdatePolicy = "sevk-islemleri.giden-depolar-arasi-sevkler.update";
    private const string IncomingListPolicy = "sevk-islemleri.gelen-depolar-arasi-sevkler.list";
    private const string IncomingDetailPolicy = "sevk-islemleri.gelen-depolar-arasi-sevkler.detail";

    [HttpGet]
    [Authorize(Policy = OutgoingListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<WarehouseShippingListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<WarehouseShippingListItemDto>>> List(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        await ListByDirection(request, WarehouseShippingDirection.Outgoing, cancellationToken);

    [HttpGet("giden")]
    [Authorize(Policy = OutgoingListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<WarehouseShippingListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<WarehouseShippingListItemDto>>> ListOutgoing(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        await ListByDirection(request, WarehouseShippingDirection.Outgoing, cancellationToken);

    [HttpGet("gelen")]
    [Authorize(Policy = IncomingListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<WarehouseShippingListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<WarehouseShippingListItemDto>>> ListIncoming(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        await ListByDirection(request, WarehouseShippingDirection.Incoming, cancellationToken);

    [HttpGet("{documentSerie}/{documentOrderNo:int}")]
    [Authorize(Policy = OutgoingDetailPolicy)]
    [ProducesResponseType(typeof(WarehouseShippingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseShippingDetailDto>> Detail(
        string documentSerie,
        int documentOrderNo,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        CancellationToken cancellationToken) =>
        await DetailByDirection(documentSerie, documentOrderNo, warehouseNo, WarehouseShippingDirection.Outgoing, cancellationToken);

    [HttpGet("giden/{documentSerie}/{documentOrderNo:int}")]
    [Authorize(Policy = OutgoingDetailPolicy)]
    [ProducesResponseType(typeof(WarehouseShippingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseShippingDetailDto>> DetailOutgoing(
        string documentSerie,
        int documentOrderNo,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        CancellationToken cancellationToken) =>
        await DetailByDirection(documentSerie, documentOrderNo, warehouseNo, WarehouseShippingDirection.Outgoing, cancellationToken);

    [HttpGet("gelen/{documentSerie}/{documentOrderNo:int}")]
    [Authorize(Policy = IncomingDetailPolicy)]
    [ProducesResponseType(typeof(WarehouseShippingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseShippingDetailDto>> DetailIncoming(
        string documentSerie,
        int documentOrderNo,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        CancellationToken cancellationToken) =>
        await DetailByDirection(documentSerie, documentOrderNo, warehouseNo, WarehouseShippingDirection.Incoming, cancellationToken);

    [HttpPost]
    [Authorize(Policy = OutgoingCreatePolicy)]
    [ProducesResponseType(typeof(CreateInterWarehouseShipmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreateInterWarehouseShipmentResponse>> Create(
        [FromBody] CreateInterWarehouseShipmentHttpRequest request,
        CancellationToken cancellationToken) =>
        await CreateOutgoing(request, cancellationToken);

    [HttpPost("{documentSerie}/{documentOrderNo:int}/e-irsaliye")]
    [Authorize(Policy = OutgoingDetailPolicy)]
    [ProducesResponseType(typeof(SendEDespatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SendEDespatchResponse>> SendEDespatch(
        string documentSerie,
        int documentOrderNo,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        [FromBody, Required] SendEDespatchHttpRequest request,
        CancellationToken cancellationToken) =>
        await SendOutgoingEDespatch(documentSerie, documentOrderNo, warehouseNo, request, cancellationToken);

    [HttpPost("giden/{documentSerie}/{documentOrderNo:int}/e-irsaliye")]
    [Authorize(Policy = OutgoingDetailPolicy)]
    [ProducesResponseType(typeof(SendEDespatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SendEDespatchResponse>> SendOutgoingEDespatch(
        string documentSerie,
        int documentOrderNo,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        [FromBody, Required] SendEDespatchHttpRequest request,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = warehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await eDespatchService.SendAsync(
            new SendEDespatchRequest(
                EDespatchDocumentType.InterWarehouseShipment,
                resolvedWarehouseNo,
                documentSerie,
                documentOrderNo,
                request.Plaque,
                request.DriverNameSurname,
                request.DriverTckn),
            cancellationToken));
    }

    [HttpGet("{documentSerie}/{documentOrderNo:int}/e-irsaliye/pdf")]
    [Authorize(Policy = OutgoingDetailPolicy)]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetEDespatchPdf(
        string documentSerie,
        int documentOrderNo,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        CancellationToken cancellationToken) =>
        await GetOutgoingEDespatchPdf(documentSerie, documentOrderNo, warehouseNo, cancellationToken);

    [HttpGet("giden/{documentSerie}/{documentOrderNo:int}/e-irsaliye/pdf")]
    [Authorize(Policy = OutgoingDetailPolicy)]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetOutgoingEDespatchPdf(
        string documentSerie,
        int documentOrderNo,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = warehouseNo ?? User.GetRequiredWarehouseNo();
        var response = await eDespatchService.GetPdfAsync(
            new GetEDespatchPdfRequest(
                EDespatchDocumentType.InterWarehouseShipment,
                resolvedWarehouseNo,
                documentSerie,
                documentOrderNo),
            cancellationToken);

        Response.Headers.ContentDisposition = $"inline; filename=\"{response.FileName}\"";

        return File(response.Content, "application/pdf");
    }

    [HttpPost("giden")]
    [Authorize(Policy = OutgoingCreatePolicy)]
    [ProducesResponseType(typeof(CreateInterWarehouseShipmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreateInterWarehouseShipmentResponse>> CreateOutgoing(
        [FromBody] CreateInterWarehouseShipmentHttpRequest request,
        CancellationToken cancellationToken)
    {
        var sourceWarehouseNo = User.ResolveWarehouseNo(request.SourceWarehouseNo);
        var response = await createInterWarehouseShipmentUseCase.ExecuteAsync(
            new CreateInterWarehouseShipmentRequest(
                sourceWarehouseNo,
                request.TargetWarehouseNo,
                request.TransitWarehouseNo ?? 60,
                request.MovementDate,
                request.DocumentDate,
                request.DocumentNo,
                request.Description,
                request.Lines
                    .Select(line => new CreateInterWarehouseShipmentLineRequest(
                        line.StockCode,
                        line.Quantity,
                        line.WarehouseOrderLineGuid,
                        line.UnitPrice,
                        line.UnitPointer,
                        line.Description,
                        line.PartyCode,
                        line.LotNo,
                        line.ProjectCode,
                        line.CustomerResponsibilityCenter,
                        line.ProductResponsibilityCenter))
                    .ToArray()),
            cancellationToken);

        await documentFlowService.RecordAsync(
            new RecordDocumentFlowRequest(
                DocumentFlowKeys.Create(
                    DocumentFlowType.InterWarehouseShipment,
                    response.SourceWarehouseNo,
                    response.DocumentSerie,
                    response.DocumentOrderNo),
                DocumentFlowType.InterWarehouseShipment,
                response.SourceWarehouseNo,
                response.TargetWarehouseNo,
                response.DocumentSerie,
                response.DocumentOrderNo,
                DocumentFlowStep.DocumentCreated,
                DocumentFlowStatus.Succeeded,
                "Depolar arasi sevk olusturuldu.",
                ChangedByUserId: User.GetRequiredUserId(),
                DocumentNo: response.DocumentNo),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = OutgoingUpdatePolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> Update(string id, [FromBody] ModuleActionRequest request) =>
        UpdateNotImplemented(OutgoingUpdatePolicy, id);

    private async Task<ActionResult<IReadOnlyCollection<WarehouseShippingListItemDto>>> ListByDirection(
        WarehouseOrderDateRangeHttpRequest request,
        WarehouseShippingDirection direction,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.ResolveWarehouseScope(request.WarehouseNo);

        return Ok(await listInterWarehouseShipmentsUseCase.ExecuteAsync(
            new WarehouseShippingListRequest(
                warehouseNo,
                request.StartDate!.Value,
                request.EndDate!.Value),
            direction,
            cancellationToken));
    }

    private async Task<ActionResult<WarehouseShippingDetailDto>> DetailByDirection(
        string documentSerie,
        int documentOrderNo,
        int? warehouseNo,
        WarehouseShippingDirection direction,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = warehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await getInterWarehouseShipmentDetailUseCase.ExecuteAsync(
            new WarehouseShippingDetailRequest(
                resolvedWarehouseNo,
                documentSerie,
                documentOrderNo),
            direction,
            cancellationToken));
    }
}

public sealed class CreateInterWarehouseShipmentHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? SourceWarehouseNo { get; init; }

    [Range(1, int.MaxValue)]
    public int TargetWarehouseNo { get; init; }

    [Range(1, int.MaxValue)]
    public int? TransitWarehouseNo { get; init; }

    public DateTime? MovementDate { get; init; }

    public DateTime? DocumentDate { get; init; }

    [StringLength(50)]
    public string? DocumentNo { get; init; }

    [StringLength(50)]
    public string? Description { get; init; }

    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<CreateInterWarehouseShipmentLineHttpRequest> Lines { get; init; } =
        Array.Empty<CreateInterWarehouseShipmentLineHttpRequest>();
}

public sealed class CreateInterWarehouseShipmentLineHttpRequest
{
    [Required]
    [StringLength(25)]
    public string StockCode { get; init; } = string.Empty;

    [Range(0.000001, double.MaxValue)]
    public double Quantity { get; init; }

    public Guid? WarehouseOrderLineGuid { get; init; }

    [Range(0, double.MaxValue)]
    public double UnitPrice { get; init; }

    [Range(1, byte.MaxValue)]
    public int UnitPointer { get; init; } = 1;

    [StringLength(50)]
    public string? Description { get; init; }

    [StringLength(25)]
    public string? PartyCode { get; init; }

    [Range(0, int.MaxValue)]
    public int LotNo { get; init; }

    [StringLength(25)]
    public string? ProjectCode { get; init; }

    [StringLength(25)]
    public string? CustomerResponsibilityCenter { get; init; }

    [StringLength(25)]
    public string? ProductResponsibilityCenter { get; init; }
}

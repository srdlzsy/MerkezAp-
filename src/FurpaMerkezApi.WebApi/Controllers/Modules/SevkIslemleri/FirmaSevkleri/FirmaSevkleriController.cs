using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.FirmaSevkleri.Create;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.FirmaSevkleri.Detail;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.FirmaSevkleri.List;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.SevkIslemleri.FirmaSevkleri;

[ApiController]
[Route("api/sevk-islemleri/firma-sevkleri")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class FirmaSevkleriController(
    IListCompanyShipmentsUseCase listCompanyShipmentsUseCase,
    IGetCompanyShipmentDetailUseCase getCompanyShipmentDetailUseCase,
    ICreateCompanyShipmentUseCase createCompanyShipmentUseCase,
    IEDespatchService eDespatchService)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "sevk-islemleri";
    private const string ModuleName = "SevkIslemleri";
    private const string MenuCode = "firma-sevkleri";
    private const string MenuName = "FirmaSevkleri";
    private const string OutgoingListPolicy = "sevk-islemleri.giden-firma-sevkleri.list";
    private const string OutgoingDetailPolicy = "sevk-islemleri.giden-firma-sevkleri.detail";
    private const string OutgoingCreatePolicy = "sevk-islemleri.giden-firma-sevkleri.create";
    private const string OutgoingUpdatePolicy = "sevk-islemleri.giden-firma-sevkleri.update";
    private const string IncomingListPolicy = "sevk-islemleri.gelen-firma-sevkleri.list";
    private const string IncomingDetailPolicy = "sevk-islemleri.gelen-firma-sevkleri.detail";

    [HttpGet]
    [Authorize(Policy = OutgoingListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CompanyMovementListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<CompanyMovementListItemDto>>> List(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        await ListByKind(request, CompanyMovementKind.OutgoingShipment, cancellationToken);

    [HttpGet("giden")]
    [Authorize(Policy = OutgoingListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CompanyMovementListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<CompanyMovementListItemDto>>> ListOutgoing(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        await ListByKind(request, CompanyMovementKind.OutgoingShipment, cancellationToken);

    [HttpGet("gelen")]
    [Authorize(Policy = IncomingListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CompanyMovementListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<CompanyMovementListItemDto>>> ListIncoming(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        await ListByKind(request, CompanyMovementKind.IncomingShipment, cancellationToken);

    [HttpGet("{documentSerie}/{documentOrderNo:int}")]
    [Authorize(Policy = OutgoingDetailPolicy)]
    [ProducesResponseType(typeof(CompanyMovementDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyMovementDetailDto>> Detail(
        string documentSerie,
        int documentOrderNo,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        CancellationToken cancellationToken) =>
        await DetailByKind(documentSerie, documentOrderNo, warehouseNo, CompanyMovementKind.OutgoingShipment, cancellationToken);

    [HttpGet("giden/{documentSerie}/{documentOrderNo:int}")]
    [Authorize(Policy = OutgoingDetailPolicy)]
    [ProducesResponseType(typeof(CompanyMovementDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyMovementDetailDto>> DetailOutgoing(
        string documentSerie,
        int documentOrderNo,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        CancellationToken cancellationToken) =>
        await DetailByKind(documentSerie, documentOrderNo, warehouseNo, CompanyMovementKind.OutgoingShipment, cancellationToken);

    [HttpGet("gelen/{documentSerie}/{documentOrderNo:int}")]
    [Authorize(Policy = IncomingDetailPolicy)]
    [ProducesResponseType(typeof(CompanyMovementDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyMovementDetailDto>> DetailIncoming(
        string documentSerie,
        int documentOrderNo,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        CancellationToken cancellationToken) =>
        await DetailByKind(documentSerie, documentOrderNo, warehouseNo, CompanyMovementKind.IncomingShipment, cancellationToken);

    [HttpPost]
    [Authorize(Policy = OutgoingCreatePolicy)]
    [ProducesResponseType(typeof(CreateCompanyMovementResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreateCompanyMovementResponse>> Create(
        [FromBody] CreateCompanyMovementHttpRequest request,
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
                EDespatchDocumentType.OutgoingCompanyShipment,
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
                EDespatchDocumentType.OutgoingCompanyShipment,
                resolvedWarehouseNo,
                documentSerie,
                documentOrderNo),
            cancellationToken);

        Response.Headers.ContentDisposition = $"inline; filename=\"{response.FileName}\"";

        return File(response.Content, "application/pdf");
    }

    [HttpPost("giden")]
    [Authorize(Policy = OutgoingCreatePolicy)]
    [ProducesResponseType(typeof(CreateCompanyMovementResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreateCompanyMovementResponse>> CreateOutgoing(
        [FromBody] CreateCompanyMovementHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.GetRequiredWarehouseNo();
        var response = await createCompanyShipmentUseCase.ExecuteAsync(
            new CreateCompanyMovementRequest(
                warehouseNo,
                request.CustomerCode,
                request.MovementDate,
                request.DocumentDate,
                request.DocumentNo,
                request.Description,
                request.Lines
                    .Select(line => new CreateCompanyMovementLineRequest(
                        line.StockCode,
                        line.Quantity,
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

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = OutgoingUpdatePolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> Update(string id, [FromBody] ModuleActionRequest request) =>
        UpdateNotImplemented(OutgoingUpdatePolicy, id);

    private async Task<ActionResult<IReadOnlyCollection<CompanyMovementListItemDto>>> ListByKind(
        WarehouseOrderDateRangeHttpRequest request,
        CompanyMovementKind kind,
        CancellationToken cancellationToken)
    {
        var warehouseNo = request.WarehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await listCompanyShipmentsUseCase.ExecuteAsync(
            new CompanyMovementListRequest(
                warehouseNo,
                request.StartDate!.Value,
                request.EndDate!.Value),
            kind,
            cancellationToken));
    }

    private async Task<ActionResult<CompanyMovementDetailDto>> DetailByKind(
        string documentSerie,
        int documentOrderNo,
        int? warehouseNo,
        CompanyMovementKind kind,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = warehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await getCompanyShipmentDetailUseCase.ExecuteAsync(
            new CompanyMovementDetailRequest(
                resolvedWarehouseNo,
                documentSerie,
                documentOrderNo),
            kind,
            cancellationToken));
    }

}

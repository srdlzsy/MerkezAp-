using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.Common.EIrsaliyeLookup;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.DepoMalKabulleri.Detail;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.DepoMalKabulleri.List;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.Accept;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.MalKabulIslemleri.DepoMalKabulleri;

[ApiController]
[Route("api/mal-kabul-islemleri/depo-mal-kabulleri")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class DepoMalKabulleriController(
    IListPendingWarehouseReceivingsUseCase listPendingWarehouseReceivingsUseCase,
    IGetPendingWarehouseReceivingDetailUseCase getPendingWarehouseReceivingDetailUseCase,
    IAcceptWarehouseReceivingUseCase acceptWarehouseReceivingUseCase,
    IDocumentFlowService documentFlowService,
    IGetInboundDespatchLookupUseCase getInboundDespatchLookupUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "mal-kabul-islemleri";
    private const string ModuleName = "MalKabulIslemleri";
    private const string MenuCode = "depo-mal-kabulleri";
    private const string MenuName = "DepoMalKabulleri";
    private const string ListPolicy = "mal-kabul-islemleri.depo-mal-kabulleri.list";
    private const string DetailPolicy = "mal-kabul-islemleri.depo-mal-kabulleri.detail";
    private const string CreatePolicy = "mal-kabul-islemleri.depo-mal-kabulleri.create";
    private const string UpdatePolicy = "mal-kabul-islemleri.depo-mal-kabulleri.update";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<WarehouseShippingListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<WarehouseShippingListItemDto>>> List(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = request.WarehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await listPendingWarehouseReceivingsUseCase.ExecuteAsync(
            new WarehouseShippingListRequest(
                warehouseNo,
                request.StartDate!.Value,
                request.EndDate!.Value),
            cancellationToken));
    }

    [HttpGet("{documentSerie}/{documentOrderNo:int}")]
    [HttpGet("/api/mal-kabul-islemleri/mal-kabuller/depo-sevkleri/{documentSerie}/{documentOrderNo:int}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(WarehouseShippingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WarehouseShippingDetailDto>> Detail(
        string documentSerie,
        int documentOrderNo,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = warehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await getPendingWarehouseReceivingDetailUseCase.ExecuteAsync(
            new WarehouseShippingDetailRequest(
                resolvedWarehouseNo,
                documentSerie,
                documentOrderNo),
            cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> Create([FromBody] ModuleActionRequest request) =>
        CreateNotImplemented(CreatePolicy);

    [HttpPost("{documentSerie}/{documentOrderNo:int}/kabul")]
    [HttpPost("/api/mal-kabul-islemleri/mal-kabuller/depo-sevkleri/{documentSerie}/{documentOrderNo:int}/kabul")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(AcceptWarehouseReceivingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AcceptWarehouseReceivingResponse>> AcceptWarehouseReceiving(
        string documentSerie,
        int documentOrderNo,
        [FromBody] AcceptWarehouseReceivingHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.ResolveWarehouseNo(request.WarehouseNo);

        var response = await acceptWarehouseReceivingUseCase.ExecuteAsync(
            new AcceptWarehouseReceivingRequest(
                warehouseNo,
                documentSerie,
                documentOrderNo,
                request.AllowDiscrepancy,
                request.Lines
                    .Select(line => new AcceptWarehouseReceivingLineRequest(
                        line.MovementGuid,
                line.ReceivedQuantity))
                    .ToArray()),
            cancellationToken);

        var documentType = response.IsReturn
            ? DocumentFlowType.WarehouseReturn
            : DocumentFlowType.InterWarehouseShipment;

        await documentFlowService.RecordAsync(
            new RecordDocumentFlowRequest(
                DocumentFlowKeys.Create(
                    documentType,
                    response.SourceWarehouseNo,
                    response.DocumentSerie,
                    response.DocumentOrderNo),
                documentType,
                response.SourceWarehouseNo,
                response.WarehouseNo,
                response.DocumentSerie,
                response.DocumentOrderNo,
                DocumentFlowStep.WarehouseReceivingAccepted,
                DocumentFlowStatus.Succeeded,
                "Depo mal kabulu tamamlandi.",
                ChangedByUserId: User.GetRequiredUserId()),
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("e-irsaliye/ettn/{ettn}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(InboundDespatchLookupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InboundDespatchLookupResponse>> GetInboundDespatchByEttn(
        string ettn,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = warehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await getInboundDespatchLookupUseCase.ExecuteAsync(
            new InboundDespatchLookupRequest(
                resolvedWarehouseNo,
                MenuCode,
                ettn),
            cancellationToken));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> Update(string id, [FromBody] ModuleActionRequest request) =>
        UpdateNotImplemented(UpdatePolicy, id);
}

public sealed class AcceptWarehouseReceivingHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public bool AllowDiscrepancy { get; init; }

    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<AcceptWarehouseReceivingLineHttpRequest> Lines { get; init; } =
        Array.Empty<AcceptWarehouseReceivingLineHttpRequest>();
}

public sealed class AcceptWarehouseReceivingLineHttpRequest
{
    public Guid MovementGuid { get; init; }

    [Range(0, double.MaxValue)]
    public double ReceivedQuantity { get; init; }
}

using System.ComponentModel.DataAnnotations;
using System.Globalization;
using FurpaMerkezApi.Application.Modules.Common.OfflineSync;
using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.Common.EIrsaliyeLookup;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.FirmaMalKabulleri.Detail;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.FirmaMalKabulleri.List;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving.Offline;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.MalKabulIslemleri.FirmaMalKabulleri;

[ApiController]
[Route("api/mal-kabul-islemleri/firma-mal-kabulleri")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class FirmaMalKabulleriController(
    IListCompanyReceivingDocumentsUseCase listCompanyReceivingDocumentsUseCase,
    IGetCompanyReceivingDocumentDetailUseCase getCompanyReceivingDocumentDetailUseCase,
    ICreateCompanyReceivingUseCase createCompanyReceivingUseCase,
    IGetCompanyReceivingOfflineSyncStatusUseCase getCompanyReceivingOfflineSyncStatusUseCase,
    IDocumentFlowService documentFlowService,
    IGetInboundDespatchLookupUseCase getInboundDespatchLookupUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "mal-kabul-islemleri";
    private const string ModuleName = "MalKabulIslemleri";
    private const string MenuCode = "firma-mal-kabulleri";
    private const string MenuName = "FirmaMalKabulleri";
    private const string ListPolicy = "mal-kabul-islemleri.firma-mal-kabulleri.list";
    private const string DetailPolicy = "mal-kabul-islemleri.firma-mal-kabulleri.detail";
    private const string CreatePolicy = "mal-kabul-islemleri.firma-mal-kabulleri.create";
    private const string UpdatePolicy = "mal-kabul-islemleri.firma-mal-kabulleri.update";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CompanyMovementListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<CompanyMovementListItemDto>>> List(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.ResolveWarehouseScopeForPolicy(request.WarehouseNo, ListPolicy);

        return Ok(await listCompanyReceivingDocumentsUseCase.ExecuteAsync(
            new CompanyMovementListRequest(
                warehouseNo,
                request.StartDate!.Value,
                request.EndDate!.Value),
            cancellationToken));
    }

    [HttpGet("{documentSerie}/{documentOrderNo:int}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(CompanyMovementDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyMovementDetailDto>> Detail(
        string documentSerie,
        int documentOrderNo,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = User.ResolveWarehouseNoForPolicy(warehouseNo, DetailPolicy);

        return Ok(await getCompanyReceivingDocumentDetailUseCase.ExecuteAsync(
            new CompanyMovementDetailRequest(
                resolvedWarehouseNo,
                documentSerie,
                documentOrderNo),
            cancellationToken));
    }

    [HttpPost]
    [HttpPost("/api/mal-kabul-islemleri/mal-kabuller/firma")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(CreateCompanyReceivingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(CreateCompanyReceivingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateCompanyReceivingResponse>> Create(
        [FromBody] CreateCompanyReceivingHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.ResolveWarehouseNoForPolicy(request.WarehouseNo, CreatePolicy);
        var response = await createCompanyReceivingUseCase.ExecuteAsync(
            new CreateCompanyReceivingRequest(
                warehouseNo,
                User.GetRequiredUserId(),
                request.ClientRequestId,
                request.CustomerCode,
                request.MovementDate,
                request.DocumentDate,
                request.DocumentNo,
                request.Deliverer,
                request.Receiver,
                request.Description,
                request.AllowOrderOverReceiving,
                request.AutoCreateReturnForPartialAcceptance,
                request.Lines
                    .Select(MapLine)
                    .ToArray()),
            cancellationToken);

        await documentFlowService.RecordAsync(
            new RecordDocumentFlowRequest(
                DocumentFlowKeys.Create(
                    DocumentFlowType.CompanyReceiving,
                    response.WarehouseNo,
                    response.DocumentSerie,
                    response.DocumentOrderNo),
                DocumentFlowType.CompanyReceiving,
                response.WarehouseNo,
                null,
                response.DocumentSerie,
                response.DocumentOrderNo,
                DocumentFlowStep.DocumentCreated,
                DocumentFlowStatus.Succeeded,
                BuildDocumentFlowMessage(request),
                ChangedByUserId: User.GetRequiredUserId(),
                DocumentNo: response.DocumentNo,
                ExternalDocumentNo: ResolveOfficialDocumentNo(request),
                ExternalUuid: ResolveOfficialDocumentEttn(request)),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    private static string BuildDocumentFlowMessage(CreateCompanyReceivingHttpRequest request)
    {
        var officialDocumentNo = ResolveOfficialDocumentNo(request);
        var officialDocumentKind = ResolveOfficialDocumentKind(request);
        var officialDocumentDate = ResolveOfficialDocumentDate(request);

        if (officialDocumentNo is null && ResolveOfficialDocumentEttn(request) is null)
        {
            return "Firma mal kabulu olusturuldu.";
        }

        var label = officialDocumentKind switch
        {
            "e-invoice" => "E-Fatura",
            "e-despatch" => "E-Irsaliye",
            _ => "E-Belge"
        };

        var documentText = officialDocumentNo is null
            ? label
            : $"{label} {officialDocumentNo}";

        if (officialDocumentDate.HasValue)
        {
            documentText += $" ({officialDocumentDate.Value:yyyy-MM-dd})";
        }

        return $"Firma mal kabulu olusturuldu. Resmi belge: {documentText}.";
    }

    private static string? ResolveOfficialDocumentKind(CreateCompanyReceivingHttpRequest request) =>
        NormalizeOfficialDocumentKind(FirstNonEmpty(request.OfficialDocumentKind, request.SourceDocumentKind));

    private static string? ResolveOfficialDocumentNo(CreateCompanyReceivingHttpRequest request) =>
        FirstNonEmpty(
            request.OfficialDocumentNo,
            request.SourceDocumentNumber,
            request.DespatchNumber,
            request.InvoiceNumber);

    private static DateTime? ResolveOfficialDocumentDate(CreateCompanyReceivingHttpRequest request) =>
        request.OfficialDocumentDate
        ?? request.SourceDocumentDate
        ?? request.InvoiceDate
        ?? request.IssueDate;

    private static string? ResolveOfficialDocumentEttn(CreateCompanyReceivingHttpRequest request) =>
        FirstNonEmpty(request.OfficialDocumentEttn, request.Ettn);

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string? NormalizeOfficialDocumentKind(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var normalized = value.Trim().ToLower(CultureInfo.InvariantCulture);

        return normalized switch
        {
            "e-fatura" or "efatura" or "einvoice" or "e_invoice" or "e-invoice" => "e-invoice",
            "e-irsaliye" or "eirsaliye" or "edespatch" or "e_despatch" or "e-despatch" => "e-despatch",
            _ => normalized
        };
    }

    private static CreateCompanyReceivingLineRequest MapLine(CreateCompanyReceivingLineHttpRequest line)
    {
        var dispatchQuantity = line.DispatchQuantity ?? line.Quantity ?? line.AcceptedQuantity ?? 0d;
        var acceptedQuantity = line.AcceptedQuantity ?? line.Quantity ?? dispatchQuantity;

        return new CreateCompanyReceivingLineRequest(
            line.StockCode,
            dispatchQuantity,
            dispatchQuantity,
            acceptedQuantity,
            line.UnitPrice,
            line.UnitPointer,
            line.LastConsumingDate,
            line.OrderGuid,
            line.Description,
            line.PartyCode,
            line.LotNo,
            line.ProjectCode,
            line.CustomerResponsibilityCenter,
            line.ProductResponsibilityCenter);
    }

    [HttpGet("offline-sync/{clientRequestId:guid}")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(OfflineSyncStatusDto<CreateCompanyReceivingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OfflineSyncStatusDto<CreateCompanyReceivingResponse>>> GetOfflineSyncStatus(
        Guid clientRequestId,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.GetRequiredWarehouseNo();
        var requestedByUserId = User.GetRequiredUserId();

        return Ok(await getCompanyReceivingOfflineSyncStatusUseCase.ExecuteAsync(
            warehouseNo,
            requestedByUserId,
            clientRequestId,
            cancellationToken));
    }

    [HttpGet("e-irsaliye/ettn/{ettn}")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(InboundDespatchLookupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InboundDespatchLookupResponse>> GetInboundDespatchByEttn(
        string ettn,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = User.ResolveWarehouseNoForPolicy(warehouseNo, CreatePolicy);

        return Ok(await getInboundDespatchLookupUseCase.ExecuteAsync(
            new InboundDespatchLookupRequest(
                resolvedWarehouseNo,
                MenuCode,
                ettn),
            cancellationToken));
    }

    [HttpGet("resmi-belge/ettn/{ettn}")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(InboundDespatchLookupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InboundDespatchLookupResponse>> ResolveOfficialDocumentByEttn(
        string ettn,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        [FromQuery] string? documentKind,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = User.ResolveWarehouseNoForPolicy(warehouseNo, CreatePolicy);

        return Ok(await getInboundDespatchLookupUseCase.ExecuteAsync(
            new InboundDespatchLookupRequest(
                resolvedWarehouseNo,
                MenuCode,
                ettn,
                documentKind),
            cancellationToken));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> Update(string id, [FromBody] ModuleActionRequest request) =>
        UpdateNotImplemented(UpdatePolicy, id);
}

public sealed class CreateCompanyReceivingHttpRequest
{
    private const int MaxCompanyReceivingDocumentNoLength = 29;

    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public Guid? ClientRequestId { get; init; }

    [Required]
    [StringLength(25)]
    public string CustomerCode { get; init; } = string.Empty;

    public DateTime? MovementDate { get; init; }

    public DateTime? DocumentDate { get; init; }

    [StringLength(
        MaxCompanyReceivingDocumentNoLength,
        ErrorMessage = "DocumentNo can not be longer than 29 characters.")]
    public string? DocumentNo { get; init; }

    [StringLength(30)]
    public string? OfficialDocumentKind { get; init; }

    [StringLength(50)]
    public string? OfficialDocumentNo { get; init; }

    public DateTime? OfficialDocumentDate { get; init; }

    [StringLength(50)]
    public string? OfficialDocumentEttn { get; init; }

    [StringLength(30)]
    public string? SourceDocumentKind { get; init; }

    [StringLength(50)]
    public string? SourceDocumentNumber { get; init; }

    public DateTime? SourceDocumentDate { get; init; }

    [StringLength(50)]
    public string? DespatchNumber { get; init; }

    public DateTime? IssueDate { get; init; }

    [StringLength(50)]
    public string? InvoiceNumber { get; init; }

    public DateTime? InvoiceDate { get; init; }

    [StringLength(50)]
    public string? Ettn { get; init; }

    [StringLength(25)]
    public string? Deliverer { get; init; }

    [StringLength(25)]
    public string? Receiver { get; init; }

    [StringLength(50)]
    public string? Description { get; init; }

    public bool AllowOrderOverReceiving { get; init; }

    public bool AutoCreateReturnForPartialAcceptance { get; init; } = true;

    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<CreateCompanyReceivingLineHttpRequest> Lines { get; init; } =
        Array.Empty<CreateCompanyReceivingLineHttpRequest>();
}

public sealed class CreateCompanyReceivingLineHttpRequest
{
    [Required]
    [StringLength(25)]
    public string StockCode { get; init; } = string.Empty;

    [Range(0.000001, double.MaxValue)]
    public double? Quantity { get; init; }

    [Range(0.000001, double.MaxValue)]
    public double? DispatchQuantity { get; init; }

    [Range(0, double.MaxValue)]
    public double? AcceptedQuantity { get; init; }

    [Range(0, double.MaxValue)]
    public double UnitPrice { get; init; }

    [Range(1, byte.MaxValue)]
    public int UnitPointer { get; init; } = 1;

    public DateTime? LastConsumingDate { get; init; }

    public Guid? OrderGuid { get; init; }

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

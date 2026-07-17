using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.UyumsoftServisleri;
using FurpaMerkezApi.Application.Modules.FaturaIslemleri.Common;
using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGoruntuleme;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.FaturaIslemleri.FaturaGoruntuleme;

[ApiController]
[Route("api/fatura-islemleri/fatura-goruntuleme")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class FaturaGoruntulemeController(
    IListInvoiceViewingDocumentsUseCase listInvoiceViewingDocumentsUseCase,
    ISynchronizeInvoiceViewingDocumentsUseCase synchronizeInvoiceViewingDocumentsUseCase,
    IGetInvoiceViewingSynchronizationProgressUseCase getInvoiceViewingSynchronizationProgressUseCase,
    IGetInvoiceViewingDocumentUseCase getInvoiceViewingDocumentUseCase,
    IRenderInvoiceViewingDocumentUseCase renderInvoiceViewingDocumentUseCase,
    ISetInvoiceViewingPrintedStateUseCase setInvoiceViewingPrintedStateUseCase,
    IUyumsoftConnectedQueryService uyumsoftConnectedQueryService)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "fatura-islemleri";
    private const string ModuleName = "FaturaIslemleri";
    private const string MenuCode = "fatura-goruntuleme";
    private const string MenuName = "FaturaGoruntuleme";
    private const string ListPolicy = "fatura-islemleri.fatura-goruntuleme.list";
    private const string DetailPolicy = "fatura-islemleri.fatura-goruntuleme.detail";
    private const string UpdatePolicy = "fatura-islemleri.fatura-goruntuleme.update";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(InvoiceViewingListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InvoiceViewingListResponse>> List(
        [FromQuery] InvoiceViewingListHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await listInvoiceViewingDocumentsUseCase.ExecuteAsync(
            new InvoiceViewingListRequest(
                request.StartDate!.Value,
                request.EndDate!.Value,
                MapState(request.ResolveProcessedState()),
                MapState(request.ResolvePrintedState()),
                request.ResolveInvoiceId(),
                request.ResolveDespatchId(),
                request.CustomerTitle,
                request.ResolveCustomerTcknVkn(),
                request.ResolveDocumentId(),
                request.OrderDocumentId,
                request.Status,
                request.InvoiceType,
                request.MinInvoiceTotal,
                request.MaxInvoiceTotal,
                request.HasDespatchId,
                request.SearchField,
                request.SearchText,
                request.ResolvePageNumber(),
                request.PageSize),
            cancellationToken));

    [HttpPost("senkronize")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(InvoiceViewingSynchronizationProgressResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InvoiceViewingSynchronizationProgressResponse>> Synchronize(
        [FromBody] InvoiceViewingSynchronizationHttpRequest request,
        CancellationToken cancellationToken)
    {
        var response = await synchronizeInvoiceViewingDocumentsUseCase.ExecuteAsync(
            new InvoiceViewingSynchronizationRequest(
                request.StartDate!.Value,
                request.EndDate!.Value,
                request.IncludeStatuses ?? false),
            cancellationToken);

        return AcceptedAtAction(nameof(SynchronizationProgress), routeValues: null, value: response);
    }

    [HttpGet("senkronize/progress")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(InvoiceViewingSynchronizationProgressResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<InvoiceViewingSynchronizationProgressResponse>> SynchronizationProgress(
        CancellationToken cancellationToken) =>
        Ok(await getInvoiceViewingSynchronizationProgressUseCase.ExecuteAsync(cancellationToken));

    [HttpGet("{documentId}")]
    [HttpGet("{documentId}/pdf")]
    [Authorize(Policy = DetailPolicy)]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPdf(
        string documentId,
        CancellationToken cancellationToken)
    {
        var pdfBytes = await uyumsoftConnectedQueryService.GetInboxInvoicePdfFileAsync(
            documentId,
            cancellationToken);

        return File(pdfBytes, "application/pdf");
    }

    [HttpGet("{documentId}/detail")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(InvoiceViewingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceViewingDetailDto>> Detail(
        string documentId,
        CancellationToken cancellationToken) =>
        Ok(await getInvoiceViewingDocumentUseCase.ExecuteAsync(
            new InvoiceViewingDetailRequest(documentId),
            cancellationToken));

    [HttpPost("{documentId}/render")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(InvoiceViewingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceViewingDetailDto>> Render(
        string documentId,
        [FromBody] InvoiceViewingRenderHttpRequest? request,
        CancellationToken cancellationToken) =>
        Ok(await renderInvoiceViewingDocumentUseCase.ExecuteAsync(
            new InvoiceViewingRenderRequest(
                documentId,
                request?.Profile ?? InvoiceDocumentProfile.Auto,
                request?.PreferEmbeddedXslt,
                request?.FallbackToDefaultXslt ?? true),
            cancellationToken));

    [HttpPatch("{documentId}/printed")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(InvoiceViewingPrintedStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceViewingPrintedStateResponse>> SetPrintedState(
        string documentId,
        [FromBody] InvoiceViewingPrintedStateHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await setInvoiceViewingPrintedStateUseCase.ExecuteAsync(
            new InvoiceViewingPrintedStateRequest(
                documentId,
                request.IsPrinted!.Value,
                string.IsNullOrWhiteSpace(request.Source) ? "manual-update" : request.Source.Trim()),
            cancellationToken));

    private static bool? MapState(int state) =>
        state switch
        {
            0 => false,
            1 => true,
            _ => null
        };
}

public sealed class InvoiceViewingListHttpRequest
{
    [Required]
    public DateTime? StartDate { get; init; }

    [Required]
    public DateTime? EndDate { get; init; }

    [Range(-1, 1)]
    public int ProcessedState { get; init; } = -1;

    [FromQuery(Name = "isProcessed")]
    [Range(-1, 1)]
    public int? IsProcessed { get; init; }

    [Range(-1, 1)]
    public int PrintedState { get; init; } = -1;

    [FromQuery(Name = "isPrinted")]
    [Range(-1, 1)]
    public int? IsPrinted { get; init; }

    public InvoiceViewingSearchField? SearchField { get; init; }

    public string? SearchText { get; init; }

    public string? InvoiceId { get; init; }

    [FromQuery(Name = "invoiceNo")]
    public string? InvoiceNo { get; init; }

    public string? DespatchId { get; init; }

    [FromQuery(Name = "despatchNo")]
    public string? DespatchNo { get; init; }

    public string? CustomerTitle { get; init; }

    public string? CustomerTcknVkn { get; init; }

    [FromQuery(Name = "tcknVkn")]
    public string? TcknVkn { get; init; }

    public string? DocumentId { get; init; }

    [FromQuery(Name = "ettn")]
    public string? Ettn { get; init; }

    public string? OrderDocumentId { get; init; }

    public string? Status { get; init; }

    public string? InvoiceType { get; init; }

    public decimal? MinInvoiceTotal { get; init; }

    public decimal? MaxInvoiceTotal { get; init; }

    public bool? HasDespatchId { get; init; }

    [Range(1, int.MaxValue)]
    public int PageNumber { get; init; } = 1;

    [FromQuery(Name = "page")]
    [Range(1, int.MaxValue)]
    public int? Page { get; init; }

    [Range(1, int.MaxValue)]
    public int PageSize { get; init; } = 50;

    public int ResolveProcessedState() => IsProcessed ?? ProcessedState;

    public int ResolvePrintedState() => IsPrinted ?? PrintedState;

    public int ResolvePageNumber() => Page ?? PageNumber;

    public string? ResolveInvoiceId() => string.IsNullOrWhiteSpace(InvoiceId) ? InvoiceNo : InvoiceId;

    public string? ResolveDespatchId() => string.IsNullOrWhiteSpace(DespatchId) ? DespatchNo : DespatchId;

    public string? ResolveCustomerTcknVkn() => string.IsNullOrWhiteSpace(CustomerTcknVkn) ? TcknVkn : CustomerTcknVkn;

    public string? ResolveDocumentId() => string.IsNullOrWhiteSpace(DocumentId) ? Ettn : DocumentId;
}

public sealed class InvoiceViewingPrintedStateHttpRequest
{
    [Required]
    public bool? IsPrinted { get; init; }

    public string? Source { get; init; }
}

public sealed class InvoiceViewingSynchronizationHttpRequest
{
    [Required]
    public DateTime? StartDate { get; init; }

    [Required]
    public DateTime? EndDate { get; init; }

    public bool? IncludeStatuses { get; init; }
}

public sealed class InvoiceViewingRenderHttpRequest
{
    public InvoiceDocumentProfile Profile { get; init; } = InvoiceDocumentProfile.Auto;

    public bool? PreferEmbeddedXslt { get; init; }

    [JsonPropertyName("fallbackToGeneral")]
    public bool FallbackToDefaultXslt { get; init; } = true;
}

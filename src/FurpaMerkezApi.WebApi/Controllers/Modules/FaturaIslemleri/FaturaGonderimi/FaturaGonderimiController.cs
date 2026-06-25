using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Application.Modules.FaturaIslemleri.Common;
using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGonderimi;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.FaturaIslemleri.FaturaGonderimi;

[ApiController]
[Route("api/fatura-islemleri/fatura-gonderimi")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class FaturaGonderimiController(
    IListInvoiceSendingDocumentsUseCase listInvoiceSendingDocumentsUseCase,
    IGetInvoiceSendingDocumentUseCase getInvoiceSendingDocumentUseCase,
    IRenderInvoiceSendingDocumentUseCase renderInvoiceSendingDocumentUseCase,
    IValidateInvoiceSendingDocumentsUseCase validateInvoiceSendingDocumentsUseCase,
    ISendInvoiceSendingDocumentsUseCase sendInvoiceSendingDocumentsUseCase,
    IListInvoiceReturnReferenceCandidatesUseCase listInvoiceReturnReferenceCandidatesUseCase,
    IUpdateInvoiceReturnReferenceUseCase updateInvoiceReturnReferenceUseCase,
    IEInvoiceDocumentRenderer invoiceDocumentRenderer)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "fatura-islemleri";
    private const string ModuleName = "FaturaIslemleri";
    private const string MenuCode = "fatura-gonderimi";
    private const string MenuName = "FaturaGonderimi";
    private const string ListPolicy = "fatura-islemleri.fatura-gonderimi.list";
    private const string DetailPolicy = "fatura-islemleri.fatura-gonderimi.detail";
    private const string CreatePolicy = "fatura-islemleri.fatura-gonderimi.create";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(InvoiceSendingListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InvoiceSendingListResponse>> List(
        [FromQuery] InvoiceSendingListHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await listInvoiceSendingDocumentsUseCase.ExecuteAsync(
            new InvoiceSendingListRequest(
                request.StartDate!.Value,
                request.EndDate!.Value,
                request.Scenario,
                request.ResolveSentState()),
            cancellationToken));

    [HttpGet("{documentSerie}/{documentOrderNo:int}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(InvoiceSendingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceSendingDetailDto>> Detail(
        string documentSerie,
        int documentOrderNo,
        [FromQuery] InvoiceSendingScenario scenario = InvoiceSendingScenario.EFatura,
        CancellationToken cancellationToken = default) =>
        Ok(await getInvoiceSendingDocumentUseCase.ExecuteAsync(
            new InvoiceSendingDocumentRequest(
                documentSerie,
                documentOrderNo,
                scenario),
            cancellationToken));

    [HttpPost("{documentSerie}/{documentOrderNo:int}/render")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(InvoiceSendingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceSendingDetailDto>> Render(
        string documentSerie,
        int documentOrderNo,
        [FromBody] InvoiceSendingRenderHttpRequest? request,
        CancellationToken cancellationToken) =>
        Ok(await renderInvoiceSendingDocumentUseCase.ExecuteAsync(
            new InvoiceSendingRenderRequest(
                documentSerie,
                documentOrderNo,
                request?.Scenario ?? InvoiceSendingScenario.EFatura,
                request?.Profile ?? InvoiceDocumentProfile.Auto,
                request?.PreferEmbeddedXslt,
                request?.FallbackToDefaultXslt ?? true),
            cancellationToken));

    [HttpGet("{documentSerie}/{documentOrderNo:int}/return-reference-candidates")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(InvoiceReturnReferenceCandidatesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceReturnReferenceCandidatesResponse>> ReturnReferenceCandidates(
        string documentSerie,
        int documentOrderNo,
        [FromQuery] InvoiceSendingScenario scenario = InvoiceSendingScenario.EFatura,
        CancellationToken cancellationToken = default) =>
        Ok(await listInvoiceReturnReferenceCandidatesUseCase.ExecuteAsync(
            new InvoiceReturnReferenceCandidatesRequest(
                documentSerie,
                documentOrderNo,
                scenario),
            cancellationToken));

    [HttpPut("{documentSerie}/{documentOrderNo:int}/return-reference")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(UpdateInvoiceReturnReferenceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UpdateInvoiceReturnReferenceResponse>> UpdateReturnReference(
        string documentSerie,
        int documentOrderNo,
        [FromBody, Required] UpdateInvoiceReturnReferenceHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await updateInvoiceReturnReferenceUseCase.ExecuteAsync(
            new UpdateInvoiceReturnReferenceRequest(
                documentSerie,
                documentOrderNo,
                request.Scenario,
                request.SourceDocumentSerie,
                request.SourceDocumentOrderNo,
                request.UseFallbackWhenNotSelected),
            cancellationToken));

    [HttpPost("send")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(SendInvoiceDocumentsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SendInvoiceDocumentsResponse>> Send(
        [FromBody] InvoiceSendingBatchHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await sendInvoiceSendingDocumentsUseCase.ExecuteAsync(
            new SendInvoiceDocumentsRequest(
                request.Scenario,
                request.Documents
                    .Select(document => new SendInvoiceDocumentSelection(
                        document.DocumentSerie,
                        document.DocumentOrderNo!.Value))
                    .ToArray()),
            cancellationToken));

    [HttpPost("validate")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(ValidateInvoiceDocumentsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ValidateInvoiceDocumentsResponse>> Validate(
        [FromBody] InvoiceSendingBatchHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await validateInvoiceSendingDocumentsUseCase.ExecuteAsync(
            new ValidateInvoiceDocumentsRequest(
                request.Scenario,
                request.Documents
                    .Select(document => new SendInvoiceDocumentSelection(
                        document.DocumentSerie,
                        document.DocumentOrderNo!.Value))
                    .ToArray()),
            cancellationToken));

    [HttpPost("preview")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(InvoiceRenderedDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InvoiceRenderedDocumentDto>> Preview(
        [FromBody] InvoicePreviewHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await invoiceDocumentRenderer.RenderXmlAsync(
            "preview",
            string.IsNullOrWhiteSpace(request.InvoiceId) ? "preview" : request.InvoiceId.Trim(),
            request.XmlContent,
            request.Profile,
            request.PreferEmbeddedXslt,
            cancellationToken));
}

public sealed class InvoicePreviewHttpRequest
{
    public string? InvoiceId { get; init; }

    [Required(AllowEmptyStrings = false)]
    public string XmlContent { get; init; } = string.Empty;

    public InvoiceDocumentProfile Profile { get; init; } = InvoiceDocumentProfile.Auto;

    public bool PreferEmbeddedXslt { get; init; } = true;
}

public sealed class InvoiceSendingListHttpRequest
{
    [Required]
    public DateTime? StartDate { get; init; }

    [Required]
    public DateTime? EndDate { get; init; }

    public InvoiceSendingScenario Scenario { get; init; } = InvoiceSendingScenario.EFatura;

    [Range(-1, 1)]
    public int SentState { get; init; } = 0;

    [FromQuery(Name = "isSent")]
    [Range(-1, 1)]
    public int? IsSent { get; init; }

    public int ResolveSentState() => IsSent ?? SentState;
}

public sealed class InvoiceSendingRenderHttpRequest
{
    public InvoiceSendingScenario Scenario { get; init; } = InvoiceSendingScenario.EFatura;

    public InvoiceDocumentProfile Profile { get; init; } = InvoiceDocumentProfile.Auto;

    public bool? PreferEmbeddedXslt { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("fallbackToGeneral")]
    public bool FallbackToDefaultXslt { get; init; } = true;
}

public sealed class UpdateInvoiceReturnReferenceHttpRequest
{
    public InvoiceSendingScenario Scenario { get; init; } = InvoiceSendingScenario.EFatura;

    [StringLength(20)]
    public string? SourceDocumentSerie { get; init; }

    [Range(1, int.MaxValue)]
    public int? SourceDocumentOrderNo { get; init; }

    public bool UseFallbackWhenNotSelected { get; init; }
}

public sealed class InvoiceSendingBatchHttpRequest
{
    public InvoiceSendingScenario Scenario { get; init; } = InvoiceSendingScenario.EFatura;

    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<InvoiceSendingBatchDocumentHttpRequest> Documents { get; init; } =
        Array.Empty<InvoiceSendingBatchDocumentHttpRequest>();
}

public sealed class InvoiceSendingBatchDocumentHttpRequest
{
    [Required(AllowEmptyStrings = false)]
    public string DocumentSerie { get; init; } = string.Empty;

    [Required]
    [Range(1, int.MaxValue)]
    public int? DocumentOrderNo { get; init; }
}

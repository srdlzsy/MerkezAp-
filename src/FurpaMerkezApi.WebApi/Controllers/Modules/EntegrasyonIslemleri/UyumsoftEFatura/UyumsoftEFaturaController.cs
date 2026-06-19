using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.UyumsoftServisleri;
using FurpaMerkezApi.WebApi.Controllers.Modules.EntegrasyonIslemleri.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.EntegrasyonIslemleri.UyumsoftEFatura;

[ApiController]
[Route("api/entegrasyon-islemleri/uyumsoft/e-fatura")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class UyumsoftEFaturaController(IUyumsoftConnectedQueryService queryService)
    : UyumsoftConnectedControllerBase(
        queryService,
        UyumsoftConnectedServiceKind.EInvoice,
        ModuleCode,
        ModuleName,
        MenuCode,
        MenuName)
{
    private const string ModuleCode = "entegrasyon-islemleri";
    private const string ModuleName = "EntegrasyonIslemleri";
    private const string MenuCode = "uyumsoft-e-fatura";
    private const string MenuName = "UyumsoftEFatura";
    private const string ListPolicy = "entegrasyon-islemleri.uyumsoft-e-fatura.list";
    private const string DetailPolicy = "entegrasyon-islemleri.uyumsoft-e-fatura.detail";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(UyumsoftConnectedServiceOverviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftConnectedServiceOverviewDto>> GetOverview(
        CancellationToken cancellationToken) =>
        Ok(await GetOverviewAsync(cancellationToken));

    [HttpGet("operations")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<UyumsoftOperationDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<UyumsoftOperationDefinitionDto>>> GetOperations(
        CancellationToken cancellationToken) =>
        Ok(await GetOperationsAsync(cancellationToken));

    [HttpGet("get/{operationName}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> InvokeGetOperationByQuery(
        string operationName,
        [FromQuery(Name = "parameter")] string[]? parameters,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            operationName,
            ParseParameters(parameters),
            cancellationToken));

    [HttpGet("system/date")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetSystemDate(
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync("GetSystemDate", cancellationToken));

    [HttpGet("system/date/formatted")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetFormattedSystemDate(
        [FromQuery] string? format,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetSystemDateWithFormat",
            cancellationToken,
            Parameter("format", RequireQueryValue(format, nameof(format)))));

    [HttpGet("inbox/invoices/{invoiceUuid}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetInboxInvoice(
        string invoiceUuid,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetInboxInvoice",
            cancellationToken,
            Parameter("invoiceId", invoiceUuid)));

    [HttpGet("inbox/invoices/{invoiceUuid}/data")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetInboxInvoiceData(
        string invoiceUuid,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetInboxInvoiceData",
            cancellationToken,
            Parameter("invoiceId", invoiceUuid)));

    [HttpGet("inbox/invoices/{invoiceUuid}/view")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetInboxInvoiceView(
        string invoiceUuid,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetInboxInvoiceView",
            cancellationToken,
            Parameter("invoiceId", invoiceUuid)));

    [HttpGet("inbox/invoices/{invoiceUuid}/pdf")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetInboxInvoicePdf(
        string invoiceUuid,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetInboxInvoicePdf",
            cancellationToken,
            Parameter("invoiceId", invoiceUuid)));

    [HttpGet("inbox/invoices/{invoiceUuid}/pdf-file")]
    [Authorize(Policy = DetailPolicy)]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInboxInvoicePdfFile(
        string invoiceUuid,
        CancellationToken cancellationToken)
    {
        var pdfBytes = await GetInboxInvoicePdfFileAsync(
            invoiceUuid,
            cancellationToken);

        return CreatePdfFileResult(pdfBytes, invoiceUuid);
    }

    [HttpGet("inbox/invoices/{invoiceUuid}/status-with-logs")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetInboxInvoiceStatusWithLogs(
        string invoiceUuid,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetInboxInvoiceStatusWithLogs",
            cancellationToken,
            Parameter("invoiceId", invoiceUuid)));

    [HttpGet("outbox/invoices/{invoiceUuid}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetOutboxInvoice(
        string invoiceUuid,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetOutboxInvoice",
            cancellationToken,
            Parameter("invoiceId", invoiceUuid)));

    [HttpGet("outbox/invoices/{invoiceUuid}/data")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetOutboxInvoiceData(
        string invoiceUuid,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetOutboxInvoiceData",
            cancellationToken,
            Parameter("invoiceId", invoiceUuid)));

    [HttpGet("outbox/invoices/{invoiceUuid}/view")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetOutboxInvoiceView(
        string invoiceUuid,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetOutboxInvoiceView",
            cancellationToken,
            Parameter("invoiceId", invoiceUuid)));

    [HttpGet("outbox/invoices/{invoiceUuid}/pdf")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetOutboxInvoicePdf(
        string invoiceUuid,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetOutboxInvoicePdf",
            cancellationToken,
            Parameter("invoiceId", invoiceUuid)));

    [HttpGet("outbox/invoices/{invoiceUuid}/pdf-file")]
    [Authorize(Policy = DetailPolicy)]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOutboxInvoicePdfFile(
        string invoiceUuid,
        CancellationToken cancellationToken)
    {
        var pdfBytes = await GetOutboxInvoicePdfFileAsync(
            invoiceUuid,
            cancellationToken);

        return CreatePdfFileResult(pdfBytes, invoiceUuid);
    }

    [HttpGet("outbox/invoices/{invoiceUuid}/status-with-logs")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetOutboxInvoiceStatusWithLogs(
        string invoiceUuid,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetOutboxInvoiceStatusWithLogs",
            cancellationToken,
            Parameter("invoiceId", invoiceUuid)));

    [HttpGet("outbox/invoices/{invoiceUuid}/response-view")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetOutboxInvoiceResponseView(
        string invoiceUuid,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetOutboxInvoiceResponseView",
            cancellationToken,
            Parameter("invoiceId", invoiceUuid)));

    [HttpGet("invoices/{invoiceUuid}/envelope")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetInvoiceEnvelope(
        string invoiceUuid,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetInvoiceEnvelope",
            cancellationToken,
            Parameter("invoiceId", invoiceUuid)));

    [HttpPost("get/{operationName}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> InvokeGetOperation(
        string operationName,
        [FromBody] UyumsoftOperationHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            operationName,
            request.Parameters
                .Select(parameter => new UyumsoftOperationParameterRequest(parameter.Name, parameter.Value))
                .ToArray(),
            cancellationToken));

    private FileContentResult CreatePdfFileResult(
        byte[] pdfBytes,
        string invoiceUuid)
    {
        var fileName = $"{SanitizeFileName(invoiceUuid)}.pdf";

        Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";

        return new FileContentResult(pdfBytes, "application/pdf")
        {
            EnableRangeProcessing = true
        };
    }

    private static string SanitizeFileName(string value)
    {
        var safe = string.IsNullOrWhiteSpace(value) ? "invoice" : value.Trim();

        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            safe = safe.Replace(invalidChar, '_');
        }

        return safe;
    }

}

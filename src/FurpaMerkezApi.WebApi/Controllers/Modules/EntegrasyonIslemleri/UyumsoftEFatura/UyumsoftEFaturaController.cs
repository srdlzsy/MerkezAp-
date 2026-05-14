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
        [FromQuery] string? payloadXml,
        [FromQuery(Name = "parameter")] string[]? parameters,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            operationName,
            payloadXml,
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

    [HttpGet("inbox/invoices/{invoiceId}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetInboxInvoice(
        string invoiceId,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetInboxInvoice",
            cancellationToken,
            Parameter("invoiceId", invoiceId)));

    [HttpGet("inbox/invoices/{invoiceId}/data")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetInboxInvoiceData(
        string invoiceId,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetInboxInvoiceData",
            cancellationToken,
            Parameter("invoiceId", invoiceId)));

    [HttpGet("inbox/invoices/{invoiceId}/view")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetInboxInvoiceView(
        string invoiceId,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetInboxInvoiceView",
            cancellationToken,
            Parameter("invoiceId", invoiceId)));

    [HttpGet("inbox/invoices/{invoiceId}/pdf")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetInboxInvoicePdf(
        string invoiceId,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetInboxInvoicePdf",
            cancellationToken,
            Parameter("invoiceId", invoiceId)));

    [HttpGet("inbox/invoices/{invoiceId}/status-with-logs")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetInboxInvoiceStatusWithLogs(
        string invoiceId,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetInboxInvoiceStatusWithLogs",
            cancellationToken,
            Parameter("invoiceId", invoiceId)));

    [HttpGet("outbox/invoices/{invoiceId}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetOutboxInvoice(
        string invoiceId,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetOutboxInvoice",
            cancellationToken,
            Parameter("invoiceId", invoiceId)));

    [HttpGet("outbox/invoices/{invoiceId}/data")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetOutboxInvoiceData(
        string invoiceId,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetOutboxInvoiceData",
            cancellationToken,
            Parameter("invoiceId", invoiceId)));

    [HttpGet("outbox/invoices/{invoiceId}/view")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetOutboxInvoiceView(
        string invoiceId,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetOutboxInvoiceView",
            cancellationToken,
            Parameter("invoiceId", invoiceId)));

    [HttpGet("outbox/invoices/{invoiceId}/pdf")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetOutboxInvoicePdf(
        string invoiceId,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetOutboxInvoicePdf",
            cancellationToken,
            Parameter("invoiceId", invoiceId)));

    [HttpGet("outbox/invoices/{invoiceId}/status-with-logs")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetOutboxInvoiceStatusWithLogs(
        string invoiceId,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetOutboxInvoiceStatusWithLogs",
            cancellationToken,
            Parameter("invoiceId", invoiceId)));

    [HttpGet("outbox/invoices/{invoiceId}/response-view")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetOutboxInvoiceResponseView(
        string invoiceId,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetOutboxInvoiceResponseView",
            cancellationToken,
            Parameter("invoiceId", invoiceId)));

    [HttpGet("invoices/{invoiceId}/envelope")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetInvoiceEnvelope(
        string invoiceId,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetInvoiceEnvelope",
            cancellationToken,
            Parameter("invoiceId", invoiceId)));

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
            request.PayloadXml,
            request.Parameters
                .Select(parameter => new UyumsoftOperationParameterRequest(parameter.Name, parameter.Value))
                .ToArray(),
            cancellationToken));
}

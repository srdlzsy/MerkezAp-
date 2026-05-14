using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.UyumsoftServisleri;
using FurpaMerkezApi.WebApi.Controllers.Modules.EntegrasyonIslemleri.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.EntegrasyonIslemleri.UyumsoftEIrsaliye;

[ApiController]
[Route("api/entegrasyon-islemleri/uyumsoft/e-irsaliye")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class UyumsoftEIrsaliyeController(IUyumsoftConnectedQueryService queryService)
    : UyumsoftConnectedControllerBase(
        queryService,
        UyumsoftConnectedServiceKind.EDespatch,
        ModuleCode,
        ModuleName,
        MenuCode,
        MenuName)
{
    private const string ModuleCode = "entegrasyon-islemleri";
    private const string ModuleName = "EntegrasyonIslemleri";
    private const string MenuCode = "uyumsoft-e-irsaliye";
    private const string MenuName = "UyumsoftEIrsaliye";
    private const string ListPolicy = "entegrasyon-islemleri.uyumsoft-e-irsaliye.list";
    private const string DetailPolicy = "entegrasyon-islemleri.uyumsoft-e-irsaliye.detail";

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

    [HttpGet("inbox/despatches/{despatchId}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetInboxDespatch(
        string despatchId,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetInboxDespatch",
            cancellationToken,
            Parameter("despatchId", despatchId)));

    [HttpGet("inbox/despatches/{despatchId}/view")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetInboxDespatchView(
        string despatchId,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetInboxDespatchView",
            cancellationToken,
            Parameter("despatchId", despatchId)));

    [HttpGet("inbox/despatches/{despatchId}/pdf")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetInboxDespatchPdf(
        string despatchId,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetInboxDespatchPdf",
            cancellationToken,
            Parameter("despatchId", despatchId)));

    [HttpGet("inbox/despatches/{despatchId}/status-with-logs")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetInboxDespatchStatusWithLogs(
        string despatchId,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetInboxDespatchStatusWithLogs",
            cancellationToken,
            Parameter("despatchId", despatchId)));

    [HttpGet("outbox/despatches/{despatchId}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetOutboxDespatch(
        string despatchId,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetOutboxDespatch",
            cancellationToken,
            Parameter("despatchId", despatchId)));

    [HttpGet("outbox/despatches/{despatchId}/view")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetOutboxDespatchView(
        string despatchId,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetOutboxDespatchView",
            cancellationToken,
            Parameter("despatchId", despatchId)));

    [HttpGet("outbox/despatches/{despatchId}/pdf")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetOutboxDespatchPdf(
        string despatchId,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetOutboxDespatchPdf",
            cancellationToken,
            Parameter("despatchId", despatchId)));

    [HttpGet("outbox/despatches/{despatchId}/status-with-logs")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetOutboxDespatchStatusWithLogs(
        string despatchId,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetOutboxDespatchStatusWithLogs",
            cancellationToken,
            Parameter("despatchId", despatchId)));

    [HttpGet("receipt-advices/{despatchId}/view")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetReceiptAdviceView(
        string despatchId,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetReceiptAdviceView",
            cancellationToken,
            Parameter("despatchId", despatchId)));

    [HttpGet("receipt-advices/{despatchId}/pdf")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetReceiptAdvicePdf(
        string despatchId,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetReceiptAdvicePdf",
            cancellationToken,
            Parameter("despatchId", despatchId)));

    [HttpGet("despatches/{despatchId}/envelope")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(UyumsoftOperationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UyumsoftOperationResponseDto>> GetDespatchEnvelope(
        string despatchId,
        [FromQuery] bool? isInbox,
        CancellationToken cancellationToken) =>
        Ok(await InvokeOperationAsync(
            "GetDespatchEnvelope",
            cancellationToken,
            Parameter("despatchId", despatchId),
            Parameter("isInbox", RequireQueryValue(isInbox, nameof(isInbox)).ToString().ToLowerInvariant())));

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

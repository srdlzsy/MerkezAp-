using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.UyumsoftServisleri;
using FurpaMerkezApi.WebApi.Controllers.Modules.EntegrasyonIslemleri.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

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

    [HttpGet("inbox/invoices/{invoiceId}/pdf-file")]
    [Authorize(Policy = DetailPolicy)]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInboxInvoicePdfFile(
        string invoiceId,
        CancellationToken cancellationToken)
    {
        var response = await InvokeOperationAsync(
            "GetInboxInvoicePdf",
            cancellationToken,
            Parameter("invoiceId", invoiceId));

        return CreatePdfFileResult(response, invoiceId);
    }

    [HttpGet("inbox/invoices/by-number/{invoiceNumber}/pdf-file")]
    [Authorize(Policy = DetailPolicy)]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInboxInvoicePdfFileByNumber(
        string invoiceNumber,
        CancellationToken cancellationToken)
    {
        var invoiceId = await ResolveInvoiceIdByNumberAsync(
            "GetInboxInvoiceList",
            invoiceNumber,
            cancellationToken);

        var response = await InvokeOperationAsync(
            "GetInboxInvoicePdf",
            cancellationToken,
            Parameter("invoiceId", invoiceId));

        return CreatePdfFileResult(response, invoiceNumber);
    }

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

    [HttpGet("outbox/invoices/{invoiceId}/pdf-file")]
    [Authorize(Policy = DetailPolicy)]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOutboxInvoicePdfFile(
        string invoiceId,
        CancellationToken cancellationToken)
    {
        var response = await InvokeOperationAsync(
            "GetOutboxInvoicePdf",
            cancellationToken,
            Parameter("invoiceId", invoiceId));

        return CreatePdfFileResult(response, invoiceId);
    }

    [HttpGet("outbox/invoices/by-number/{invoiceNumber}/pdf-file")]
    [Authorize(Policy = DetailPolicy)]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOutboxInvoicePdfFileByNumber(
        string invoiceNumber,
        CancellationToken cancellationToken)
    {
        var invoiceId = await ResolveInvoiceIdByNumberAsync(
            "GetOutboxInvoiceList",
            invoiceNumber,
            cancellationToken);

        var response = await InvokeOperationAsync(
            "GetOutboxInvoicePdf",
            cancellationToken,
            Parameter("invoiceId", invoiceId));

        return CreatePdfFileResult(response, invoiceNumber);
    }

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
            request.Parameters
                .Select(parameter => new UyumsoftOperationParameterRequest(parameter.Name, parameter.Value))
                .ToArray(),
            cancellationToken));

    private FileContentResult CreatePdfFileResult(
        UyumsoftOperationResponseDto response,
        string invoiceId)
    {
        var pdfBytes = ExtractPdfBytes(response);
        var fileName = $"{SanitizeFileName(invoiceId)}.pdf";

        Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";

        return File(pdfBytes, "application/pdf");
    }

    private async Task<string> ResolveInvoiceIdByNumberAsync(
        string listOperationName,
        string invoiceNumber,
        CancellationToken cancellationToken)
    {
        var trimmedInvoiceNumber = RequireQueryValue(invoiceNumber, nameof(invoiceNumber)).Trim();
        var listResponse = await InvokeOperationAsync(
            listOperationName,
            cancellationToken,
            Parameter("InvoiceNumbers", trimmedInvoiceNumber),
            Parameter("PageIndex", "0"),
            Parameter("PageSize", "5"));

        var invoiceId = FindInvoiceIdByDocumentId(listResponse, trimmedInvoiceNumber)
                        ?? FindFirstInvoiceId(listResponse);

        return string.IsNullOrWhiteSpace(invoiceId)
            ? throw new KeyNotFoundException(
                $"Uyumsoft fatura numarasina gore teknik invoiceId bulunamadi. Fatura No: {trimmedInvoiceNumber}")
            : invoiceId;
    }

    private static string? FindInvoiceIdByDocumentId(
        UyumsoftOperationResponseDto response,
        string documentId)
    {
        if (TryFindInvoiceIdByDocumentIdFromJson(response.ResponsePayloadJson, documentId, out var jsonInvoiceId))
        {
            return jsonInvoiceId;
        }

        return response.Nodes
            .SelectMany(FlattenNodes)
            .Select(node => new
            {
                InvoiceId = FindChildValue(node, "InvoiceId"),
                DocumentId = FindChildValue(node, "DocumentId")
            })
            .FirstOrDefault(candidate =>
                !string.IsNullOrWhiteSpace(candidate.InvoiceId) &&
                string.Equals(candidate.DocumentId, documentId, StringComparison.OrdinalIgnoreCase))
            ?.InvoiceId;
    }

    private static string? FindFirstInvoiceId(UyumsoftOperationResponseDto response)
    {
        if (TryFindFirstInvoiceIdFromJson(response.ResponsePayloadJson, out var jsonInvoiceId))
        {
            return jsonInvoiceId;
        }

        return response.Nodes
            .SelectMany(FlattenNodes)
            .FirstOrDefault(node =>
                string.Equals(node.Name, "InvoiceId", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(node.Value))
            ?.Value;
    }

    private static byte[] ExtractPdfBytes(UyumsoftOperationResponseDto response)
    {
        var dataNodeValue = response.Nodes
            .SelectMany(FlattenNodes)
            .FirstOrDefault(node =>
                string.Equals(node.Name, "Data", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(node.Value))
            ?.Value;

        if (TryDecodeBase64(dataNodeValue, out var nodeBytes))
        {
            return nodeBytes;
        }

        if (TryExtractPdfBytesFromJson(response.ResponsePayloadJson, out var jsonBytes))
        {
            return jsonBytes;
        }

        if (TryDecodeBase64(response.ScalarValue, out var scalarBytes))
        {
            return scalarBytes;
        }

        throw new InvalidOperationException("Uyumsoft PDF cevabinda okunabilir PDF verisi bulunamadi.");
    }

    private static IEnumerable<UyumsoftResponseNodeDto> FlattenNodes(UyumsoftResponseNodeDto node)
    {
        yield return node;

        foreach (var child in node.Children.SelectMany(FlattenNodes))
        {
            yield return child;
        }
    }

    private static bool TryExtractPdfBytesFromJson(string? payloadJson, out byte[] pdfBytes)
    {
        pdfBytes = [];

        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            return TryFindBase64Property(document.RootElement, "data", out pdfBytes);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryFindBase64Property(
        JsonElement element,
        string propertyName,
        out byte[] bytes)
    {
        bytes = [];

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.String &&
                    TryDecodeBase64(property.Value.GetString(), out bytes))
                {
                    return true;
                }

                if (TryFindBase64Property(property.Value, propertyName, out bytes))
                {
                    return true;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryFindBase64Property(item, propertyName, out bytes))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryFindInvoiceIdByDocumentIdFromJson(
        string? payloadJson,
        string documentId,
        out string? invoiceId)
    {
        invoiceId = null;

        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            invoiceId = FindInvoiceIdByDocumentId(document.RootElement, documentId);
            return !string.IsNullOrWhiteSpace(invoiceId);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryFindFirstInvoiceIdFromJson(
        string? payloadJson,
        out string? invoiceId)
    {
        invoiceId = null;

        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            invoiceId = FindFirstInvoiceId(document.RootElement);
            return !string.IsNullOrWhiteSpace(invoiceId);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? FindInvoiceIdByDocumentId(JsonElement element, string documentId)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var invoiceId = GetStringProperty(element, "invoiceId");
            var candidateDocumentId = GetStringProperty(element, "documentId");

            if (!string.IsNullOrWhiteSpace(invoiceId) &&
                string.Equals(candidateDocumentId, documentId, StringComparison.OrdinalIgnoreCase))
            {
                return invoiceId;
            }

            foreach (var property in element.EnumerateObject())
            {
                var found = FindInvoiceIdByDocumentId(property.Value, documentId);

                if (!string.IsNullOrWhiteSpace(found))
                {
                    return found;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var found = FindInvoiceIdByDocumentId(item, documentId);

                if (!string.IsNullOrWhiteSpace(found))
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static string? FindFirstInvoiceId(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var invoiceId = GetStringProperty(element, "invoiceId");

            if (!string.IsNullOrWhiteSpace(invoiceId))
            {
                return invoiceId;
            }

            foreach (var property in element.EnumerateObject())
            {
                var found = FindFirstInvoiceId(property.Value);

                if (!string.IsNullOrWhiteSpace(found))
                {
                    return found;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var found = FindFirstInvoiceId(item);

                if (!string.IsNullOrWhiteSpace(found))
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }
        }

        return null;
    }

    private static string? FindChildValue(UyumsoftResponseNodeDto node, string childName) =>
        node.Children
            .FirstOrDefault(child =>
                string.Equals(child.Name, childName, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(child.Value))
            ?.Value;

    private static bool TryDecodeBase64(string? value, out byte[] bytes)
    {
        bytes = [];

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(value);
            return bytes.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
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

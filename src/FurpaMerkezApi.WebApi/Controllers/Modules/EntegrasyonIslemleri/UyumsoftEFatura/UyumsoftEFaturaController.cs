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
        var response = await InvokeOperationAsync(
            "GetInboxInvoicePdf",
            cancellationToken,
            Parameter("invoiceId", invoiceUuid));

        return CreatePdfFileResult(response, invoiceUuid);
    }

    [HttpGet("inbox/invoices/by-number/{invoiceNumber}/pdf-file")]
    [Authorize(Policy = DetailPolicy)]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInboxInvoicePdfFileByNumber(
        string invoiceNumber,
        [FromQuery] string[]? alternateDocumentReference,
        CancellationToken cancellationToken)
    {
        var invoiceUuid = await ResolveInvoiceIdByNumberAsync(
            "GetInboxInvoiceList",
            invoiceNumber,
            alternateDocumentReference,
            cancellationToken);

        var response = await InvokeOperationAsync(
            "GetInboxInvoicePdf",
            cancellationToken,
            Parameter("invoiceId", invoiceUuid));

        return CreatePdfFileResult(response, invoiceNumber);
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
        var response = await InvokeOperationAsync(
            "GetOutboxInvoicePdf",
            cancellationToken,
            Parameter("invoiceId", invoiceUuid));

        return CreatePdfFileResult(response, invoiceUuid);
    }

    [HttpGet("outbox/invoices/by-number/{invoiceNumber}/pdf-file")]
    [Authorize(Policy = DetailPolicy)]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOutboxInvoicePdfFileByNumber(
        string invoiceNumber,
        [FromQuery] string[]? alternateDocumentReference,
        CancellationToken cancellationToken)
    {
        var invoiceUuid = await ResolveInvoiceIdByNumberAsync(
            "GetOutboxInvoiceList",
            invoiceNumber,
            alternateDocumentReference,
            cancellationToken);

        var response = await InvokeOperationAsync(
            "GetOutboxInvoicePdf",
            cancellationToken,
            Parameter("invoiceId", invoiceUuid));

        return CreatePdfFileResult(response, invoiceNumber);
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
        UyumsoftOperationResponseDto response,
        string invoiceUuid)
    {
        var pdfBytes = ExtractPdfBytes(response);
        var fileName = $"{SanitizeFileName(invoiceUuid)}.pdf";

        Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";

        return File(pdfBytes, "application/pdf");
    }

    private async Task<string> ResolveInvoiceIdByNumberAsync(
        string listOperationName,
        string invoiceNumber,
        IReadOnlyCollection<string>? alternateDocumentReferences,
        CancellationToken cancellationToken)
    {
        var trimmedInvoiceNumber = RequireQueryValue(invoiceNumber, nameof(invoiceNumber)).Trim();
        var documentReferences = new[] { trimmedInvoiceNumber }
            .Concat(alternateDocumentReferences ?? [])
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Select(reference => reference.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var directTechnicalId = documentReferences.FirstOrDefault(LooksLikeTechnicalInvoiceId);
        if (!string.IsNullOrWhiteSpace(directTechnicalId))
        {
            return directTechnicalId;
        }

        foreach (var lookupAttempt in documentReferences.SelectMany(BuildInvoiceLookupAttempts))
        {
            var listResponse = await InvokeOperationAsync(
                listOperationName,
                lookupAttempt.Parameters,
                cancellationToken);

            var invoiceId = FindInvoiceIdByDocumentReference(listResponse, documentReferences);

            if (!string.IsNullOrWhiteSpace(invoiceId))
            {
                return invoiceId;
            }
        }

        throw new KeyNotFoundException(
            $"Uyumsoft fatura numarasina gore teknik invoiceId bulunamadi. Fatura No: {trimmedInvoiceNumber}");
    }

    private static IReadOnlyCollection<InvoiceLookupAttempt> BuildInvoiceLookupAttempts(string invoiceNumber)
    {
        bool?[] archiveStates = [null, false, true];
        var attempts = new List<InvoiceLookupAttempt>(archiveStates.Length * 2);

        foreach (var isArchived in archiveStates)
        {
            attempts.Add(new InvoiceLookupAttempt(
                $"InvoiceNumbers/IsArchived={FormatArchiveState(isArchived)}",
                BuildInvoiceLookupParameters("InvoiceNumbers", invoiceNumber, isArchived)));
        }

        foreach (var isArchived in archiveStates)
        {
            attempts.Add(new InvoiceLookupAttempt(
                $"InvoiceIds/IsArchived={FormatArchiveState(isArchived)}",
                BuildInvoiceLookupParameters("InvoiceIds", invoiceNumber, isArchived)));
        }

        return attempts;
    }

    private static IReadOnlyCollection<UyumsoftOperationParameterRequest> BuildInvoiceLookupParameters(
        string lookupParameterName,
        string invoiceNumber,
        bool? isArchived)
    {
        var parameters = new List<UyumsoftOperationParameterRequest>
        {
            Parameter(lookupParameterName, invoiceNumber),
            Parameter("PageIndex", "0"),
            Parameter("PageSize", "10")
        };

        if (isArchived.HasValue)
        {
            parameters.Add(Parameter("IsArchived", isArchived.Value ? "true" : "false"));
        }

        return parameters;
    }

    private static string FormatArchiveState(bool? isArchived) =>
        isArchived.HasValue
            ? isArchived.Value.ToString()
            : "null";

    private static bool LooksLikeTechnicalInvoiceId(string value) =>
        Guid.TryParse(value, out _);

    private static string? FindInvoiceIdByDocumentReference(
        UyumsoftOperationResponseDto response,
        IReadOnlyCollection<string> documentReferences)
    {
        if (TryFindInvoiceIdByDocumentReferenceFromJson(response.ResponsePayloadJson, documentReferences, out var jsonInvoiceId))
        {
            return jsonInvoiceId;
        }

        return response.Nodes
            .SelectMany(FlattenNodes)
            .Select(node => new
            {
                InvoiceId = FindChildValue(node, "InvoiceId"),
                DocumentId = FindChildValue(node, "DocumentId") ?? FindChildValue(node, "InvoiceNumber"),
                LocalDocumentId = FindChildValue(node, "LocalDocumentId")
            })
            .FirstOrDefault(candidate =>
                !string.IsNullOrWhiteSpace(candidate.InvoiceId) &&
                (MatchesAnyDocumentReference(candidate.DocumentId, documentReferences) ||
                 MatchesAnyDocumentReference(candidate.LocalDocumentId, documentReferences)))
            ?.InvoiceId;
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

    private static bool TryFindInvoiceIdByDocumentReferenceFromJson(
        string? payloadJson,
        IReadOnlyCollection<string> documentReferences,
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
            invoiceId = FindInvoiceIdByDocumentReference(document.RootElement, documentReferences);
            return !string.IsNullOrWhiteSpace(invoiceId);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? FindInvoiceIdByDocumentReference(
        JsonElement element,
        IReadOnlyCollection<string> documentReferences)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var invoiceId = GetStringProperty(element, "invoiceId");
            var candidateDocumentId = GetStringProperty(element, "documentId", "invoiceNumber");
            var candidateLocalDocumentId = GetStringProperty(element, "localDocumentId");

            if (!string.IsNullOrWhiteSpace(invoiceId) &&
                (MatchesAnyDocumentReference(candidateDocumentId, documentReferences) ||
                 MatchesAnyDocumentReference(candidateLocalDocumentId, documentReferences)))
            {
                return invoiceId;
            }

            foreach (var property in element.EnumerateObject())
            {
                var found = FindInvoiceIdByDocumentReference(property.Value, documentReferences);

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
                var found = FindInvoiceIdByDocumentReference(item, documentReferences);

                if (!string.IsNullOrWhiteSpace(found))
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static bool MatchesAnyDocumentReference(
        string? candidate,
        IReadOnlyCollection<string> documentReferences) =>
        !string.IsNullOrWhiteSpace(candidate) &&
        documentReferences.Any(documentReference =>
            string.Equals(candidate.Trim(), documentReference, StringComparison.OrdinalIgnoreCase));

    private static string? GetStringProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (propertyNames.Any(propertyName => string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)) &&
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

    private sealed record InvoiceLookupAttempt(
        string Name,
        IReadOnlyCollection<UyumsoftOperationParameterRequest> Parameters);
}

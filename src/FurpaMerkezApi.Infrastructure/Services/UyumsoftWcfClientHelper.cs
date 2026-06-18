using System.Collections;
using System.Globalization;
using System.Reflection;
using System.ServiceModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Serialization;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.UyumsoftServisleri;
using UyumsoftDespatch = FurpaMerkezApi.Infrastructure.Services.ServiceReferences.Uyumsoft.Despatch;
using UyumsoftInvoice = FurpaMerkezApi.Infrastructure.Services.ServiceReferences.Uyumsoft.Invoice;

namespace FurpaMerkezApi.Infrastructure.Services;

internal static class UyumsoftWcfClientHelper
{
    private const string InvoiceNamespace = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private const string DespatchNamespace = "urn:oasis:names:specification:ubl:schema:xsd:DespatchAdvice-2";
    private const int MaxNodeDepth = 8;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        WriteIndented = false
    };

    public static UyumsoftInvoice.BasicIntegrationClient CreateInvoiceClient(string endpointUrl) =>
        new(
            UyumsoftInvoice.BasicIntegrationClient.EndpointConfiguration.BasicHttpBinding_IBasicIntegration,
            endpointUrl);

    public static UyumsoftDespatch.BasicDespatchIntegrationClient CreateDespatchClient(string endpointUrl) =>
        new(
            UyumsoftDespatch.BasicDespatchIntegrationClient.EndpointConfiguration.BasicHttpBinding_IBasicDespatchIntegration,
            endpointUrl);

    public static UyumsoftInvoice.UserInformation CreateInvoiceUserInfo(UyumsoftServiceEndpointOptions options) =>
        new()
        {
            Username = options.Username,
            Password = options.Password
        };

    public static UyumsoftDespatch.UserInformation CreateDespatchUserInfo(UyumsoftServiceEndpointOptions options) =>
        new()
        {
            Username = options.Username,
            Password = options.Password
        };

    public static async Task CloseAsync(ICommunicationObject client)
    {
        try
        {
            if (client.State == CommunicationState.Faulted)
            {
                client.Abort();
                return;
            }

            await Task.Run(client.Close);
        }
        catch
        {
            client.Abort();
        }
    }

    public static void Abort(ICommunicationObject client) => client.Abort();

    public static UyumsoftOperationResponseDto ToOperationResponse(
        UyumsoftServiceCatalogEntry catalog,
        string operationName,
        object? response)
    {
        if (response is null)
        {
            throw new InvalidOperationException($"{catalog.ServiceName} response is empty for {operationName}.");
        }

        var isSucceeded = ReadBoolProperty(response, "IsSucceded") ?? true;
        var message = ReadStringProperty(response, "Message");

        if (!isSucceeded)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(message)
                    ? $"{catalog.ServiceName} request was rejected by {operationName}."
                    : message);
        }

        var value = response.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(response);
        var attributes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["IsSucceded"] = isSucceeded.ToString(CultureInfo.InvariantCulture),
            ["Message"] = NormalizeValue(message)
        };
        var scalarValue = ToScalarValue(value);
        var resultNode = MapNode($"{operationName}Result", response, 0);
        var invoiceList = MapInvoiceList(response);

        return new UyumsoftOperationResponseDto(
            catalog.ServiceKey,
            catalog.ServiceName,
            operationName,
            $"{operationName}Result",
            isSucceeded,
            NormalizeValue(message),
            scalarValue,
            attributes,
            resultNode.Children,
            invoiceList,
            JsonSerializer.Serialize(response, JsonOptions));
    }

    public static T DeserializeUbl<T>(string xml, string rootName, string rootNamespace)
    {
        var serializer = new XmlSerializer(
            typeof(T),
            new XmlRootAttribute(rootName)
            {
                Namespace = rootNamespace
            });
        using var reader = new StringReader(xml);

        return (T)(serializer.Deserialize(reader)
                   ?? throw new InvalidOperationException($"{rootName} UBL content could not be deserialized."));
    }

    public static string SerializeInvoice(UyumsoftInvoice.InvoiceType invoice) =>
        SerializeUbl(invoice, "Invoice", InvoiceNamespace);

    public static string SerializeDespatchAdvice(UyumsoftDespatch.DespatchAdviceType despatchAdvice) =>
        SerializeUbl(despatchAdvice, "DespatchAdvice", DespatchNamespace);

    private static string SerializeUbl<T>(T value, string rootName, string rootNamespace)
    {
        var serializer = new XmlSerializer(
            typeof(T),
            new XmlRootAttribute(rootName)
            {
                Namespace = rootNamespace
            });
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        serializer.Serialize(writer, value);

        return writer.ToString();
    }

    private static UyumsoftResponseNodeDto MapNode(string name, object? value, int depth)
    {
        if (value is null)
        {
            return new UyumsoftResponseNodeDto(name, null, EmptyAttributes(), Array.Empty<UyumsoftResponseNodeDto>());
        }

        if (TrySerializeKnownUbl(value, out var ublXml))
        {
            return new UyumsoftResponseNodeDto(name, ublXml, EmptyAttributes(), Array.Empty<UyumsoftResponseNodeDto>());
        }

        var scalar = ToScalarValue(value);
        if (scalar is not null || value is string)
        {
            return new UyumsoftResponseNodeDto(name, scalar, EmptyAttributes(), Array.Empty<UyumsoftResponseNodeDto>());
        }

        if (depth >= MaxNodeDepth)
        {
            return new UyumsoftResponseNodeDto(name, value.GetType().Name, EmptyAttributes(), Array.Empty<UyumsoftResponseNodeDto>());
        }

        if (value is IEnumerable enumerable and not string)
        {
            var children = enumerable
                .Cast<object?>()
                .Select(item => MapNode("Items", item, depth + 1))
                .ToArray();

            return new UyumsoftResponseNodeDto(name, null, EmptyAttributes(), children);
        }

        var propertyNodes = value.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetIndexParameters().Length == 0)
            .Select(property => MapNode(property.Name, property.GetValue(value), depth + 1))
            .ToArray();

        return new UyumsoftResponseNodeDto(name, null, EmptyAttributes(), propertyNodes);
    }

    private static bool TrySerializeKnownUbl(object value, out string xml)
    {
        switch (value)
        {
            case UyumsoftInvoice.InvoiceType invoice:
                xml = SerializeInvoice(invoice);
                return true;
            case UyumsoftDespatch.DespatchAdviceType despatchAdvice:
                xml = SerializeDespatchAdvice(despatchAdvice);
                return true;
            default:
                xml = string.Empty;
                return false;
        }
    }

    private static UyumsoftInvoiceListDto? MapInvoiceList(object response) =>
        response switch
        {
            UyumsoftInvoice.OutboxInvoiceListResponse outboxResponse when outboxResponse.Value is not null =>
                new UyumsoftInvoiceListDto(
                    outboxResponse.Value.PageIndex,
                    outboxResponse.Value.PageSize,
                    outboxResponse.Value.TotalCount,
                    outboxResponse.Value.TotalPages,
                    (outboxResponse.Value.Items ?? [])
                    .Select(MapInvoiceListItem)
                    .ToArray()),

            UyumsoftInvoice.InboxInvoiceListResponse inboxResponse when inboxResponse.Value is not null =>
                new UyumsoftInvoiceListDto(
                    inboxResponse.Value.PageIndex,
                    inboxResponse.Value.PageSize,
                    inboxResponse.Value.TotalCount,
                    inboxResponse.Value.TotalPages,
                    (inboxResponse.Value.Items ?? [])
                    .Select(MapInvoiceListItem)
                    .ToArray()),

            _ => null
        };

    private static UyumsoftInvoiceListItemDto MapInvoiceListItem(
        UyumsoftInvoice.InvoiceListItemBase item)
    {
        var outboxItem = item as UyumsoftInvoice.OutboxInvoiceListItem;
        var inboxItem = item as UyumsoftInvoice.InboxInvoiceListItem;
        var invoiceUuid = NormalizeValue(item.InvoiceId);
        var direction = outboxItem is null ? "inbox" : "outbox";
        var pdfFilePath = invoiceUuid is null
            ? null
            : $"/api/entegrasyon-islemleri/uyumsoft/e-fatura/{direction}/invoices/{Uri.EscapeDataString(invoiceUuid)}/pdf-file";

        return new UyumsoftInvoiceListItemDto(
            invoiceUuid,
            NormalizeValue(item.DocumentId),
            direction,
            pdfFilePath,
            outboxItem?.LocalDocumentId,
            outboxItem?.Scenario.ToString(),
            outboxItem?.ScenarioCode,
            item.Type.ToString(),
            item.TypeCode,
            NormalizeValue(item.TargetTcknVkn),
            NormalizeValue(item.TargetTitle),
            NormalizeValue(item.EnvelopeIdentifier),
            item.Status.ToString(),
            item.StatusCode,
            item.EnvelopeStatus.ToString(),
            item.EnvelopeStatusCode,
            NormalizeValue(item.Message),
            item.CreateDateUtc,
            item.ExecutionDate,
            item.PayableAmount,
            item.TaxTotal,
            item.TaxExclusiveAmount,
            NormalizeValue(item.DocumentCurrencyCode),
            item.ExchangeRate,
            item.Vat1,
            item.Vat8,
            item.Vat10,
            item.Vat18,
            item.Vat20,
            item.Vat0TaxableAmount,
            item.Vat1TaxableAmount,
            item.Vat8TaxableAmount,
            item.Vat10TaxableAmount,
            item.Vat18TaxableAmount,
            item.Vat20TaxableAmount,
            NormalizeValue(item.OrderDocumentId),
            item.IsArchived,
            item.InvoiceTipType.ToString(),
            item.InvoiceTipTypeCode,
            inboxItem?.IsNew,
            inboxItem?.IsSeen);
    }

    private static string? ToScalarValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return value switch
        {
            string text => NormalizeValue(text),
            byte[] bytes => Convert.ToBase64String(bytes),
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            DateOnly dateOnly => dateOnly.ToString("O", CultureInfo.InvariantCulture),
            TimeOnly timeOnly => timeOnly.ToString("O", CultureInfo.InvariantCulture),
            bool boolean => boolean.ToString(CultureInfo.InvariantCulture),
            Enum enumValue => enumValue.ToString(),
            IFormattable formattable when IsPrimitiveLike(value.GetType()) =>
                formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => null
        };
    }

    private static bool IsPrimitiveLike(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        return type.IsPrimitive ||
               type == typeof(decimal) ||
               type == typeof(Guid);
    }

    private static bool? ReadBoolProperty(object response, string propertyName)
    {
        var value = response.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(response);

        return value switch
        {
            bool boolean => boolean,
            null => null,
            _ when bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed) => parsed,
            _ => null
        };
    }

    private static string? ReadStringProperty(object response, string propertyName) =>
        response.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(response) is { } value
            ? Convert.ToString(value, CultureInfo.InvariantCulture)
            : null;

    private static string? NormalizeValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyDictionary<string, string?> EmptyAttributes() =>
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
}

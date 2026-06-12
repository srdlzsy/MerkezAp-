using System.Globalization;
using System.Text;
using System.Xml.Linq;
using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal sealed class AxataSynchronizationLiveTransportService(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<AxataSynchronizationOptions> options)
{
    private const string SoapEnvelopeNamespace = "http://schemas.xmlsoap.org/soap/envelope/";
    private const string ServiceNamespace = "http://tempuri.org/";
    private const string ServiceContractName = "IAxataServicePool";
    private const string DefaultBranchCode = "01";
    private const string DefaultExternalChannel = "01";
    private const string DefaultActionCode = "01";
    private const string DefaultFormType = "0";
    private const string DefaultAddressCode = "01";
    private const string DefaultOutboundOperationName = "addOutboundOrder";
    private const string DefaultInboundOperationName = "addInboundOrder";
    private const string DefaultOutboundMovementCode = "C01";
    private const string DefaultInboundMovementCode = "G01";

    public async Task<AxataLiveDispatchResult> DispatchWarehouseOrderAsync(
        AxataSynchronizationTaskExecutionContext context,
        WarehouseOrderDetailDto detail,
        CancellationToken cancellationToken)
    {
        var configuration = GetRequiredConfiguration();
        var operationName = ResolveOperationName(context.Definition.Code, DefaultOutboundOperationName);
        var payload = BuildOutboundOrderPayload(detail);
        var envelope = BuildOutboundOrderEnvelope(payload, configuration, operationName);
        var responseXml = await SendSoapAsync(
            envelope,
            configuration.MainEndpointUrl,
            operationName,
            cancellationToken);
        var serviceResponse = ParseServiceResponse(responseXml, operationName);

        return new AxataLiveDispatchResult(
            operationName,
            configuration.MainEndpointUrl,
            serviceResponse.IsSuccess,
            serviceResponse.State,
            serviceResponse.Message,
            AxataSynchronizationPayloadFactory.Serialize(payload),
            RedactSensitiveXml(envelope),
            responseXml,
            [
                $"Task icin yapilandirilmis AXATA operasyonu `{operationName}` ile SOAP envelope gonderildi.",
                $"Hareket kodu {payload.MovementCode} ve belge {payload.DocumentNumber} kullanildi."
            ]);
    }

    public async Task<AxataLiveDispatchResult> DispatchCompanyReceivingAsync(
        AxataSynchronizationTaskExecutionContext context,
        CompanyMovementDetailDto detail,
        CancellationToken cancellationToken)
    {
        var configuration = GetRequiredConfiguration();
        var operationName = ResolveOperationName(context.Definition.Code, DefaultInboundOperationName);
        var payload = BuildInboundOrderPayload(detail);
        var envelope = BuildInboundOrderEnvelope(payload, configuration, operationName);
        var responseXml = await SendSoapAsync(
            envelope,
            configuration.MainEndpointUrl,
            operationName,
            cancellationToken);
        var serviceResponse = ParseServiceResponse(responseXml, operationName);

        return new AxataLiveDispatchResult(
            operationName,
            configuration.MainEndpointUrl,
            serviceResponse.IsSuccess,
            serviceResponse.State,
            serviceResponse.Message,
            AxataSynchronizationPayloadFactory.Serialize(payload),
            RedactSensitiveXml(envelope),
            responseXml,
            [
                $"Task icin yapilandirilmis AXATA operasyonu `{operationName}` ile SOAP envelope gonderildi.",
                $"Hareket kodu {payload.MovementCode} ve belge {payload.DocumentNumber} kullanildi."
            ]);
    }

    private AxataSynchronizationLiveTransportConfiguration GetRequiredConfiguration()
    {
        var currentOptions = options.CurrentValue;

        if (string.IsNullOrWhiteSpace(currentOptions.MainEndpointUrl))
        {
            throw new InvalidOperationException("AXATA main endpoint url is not configured.");
        }

        if (string.IsNullOrWhiteSpace(currentOptions.Username))
        {
            throw new InvalidOperationException("AXATA username is not configured.");
        }

        if (string.IsNullOrWhiteSpace(currentOptions.Password))
        {
            throw new InvalidOperationException(
                "AXATA password is not configured. Live dispatch requires AxataSynchronization:Password.");
        }

        return new AxataSynchronizationLiveTransportConfiguration(
            currentOptions.MainEndpointUrl,
            currentOptions.Username,
            currentOptions.Password);
    }

    private static AxataLegacyOutboundOrderPayload BuildOutboundOrderPayload(WarehouseOrderDetailDto detail)
    {
        var documentNumber = BuildDocumentNumber(detail.Header.DocumentSerie, detail.Header.DocumentOrderNo);
        var movementCode = DefaultOutboundMovementCode;
        var depotCode = detail.Header.OutWarehouseNo.ToString(CultureInfo.InvariantCulture);

        return new AxataLegacyOutboundOrderPayload(
            documentNumber,
            movementCode,
            new AxataLegacyOutboundOrderMaster(
                DefaultBranchCode,
                documentNumber,
                DefaultExternalChannel,
                detail.Header.InWarehouseNo.ToString(CultureInfo.InvariantCulture),
                detail.Header.OutWarehouseNo.ToString(CultureInfo.InvariantCulture),
                DefaultFormType,
                movementCode,
                movementCode,
                DefaultAddressCode,
                depotCode),
            detail.Items
                .OrderBy(item => item.LineNo)
                .Select(item => new AxataLegacyOutboundOrderLine(
                    DefaultBranchCode,
                    documentNumber,
                    item.LineNo,
                    item.StockCode,
                    item.RemainingQuantity > 0d ? item.RemainingQuantity : item.Quantity,
                    depotCode))
                .ToArray());
    }

    private static AxataLegacyInboundOrderPayload BuildInboundOrderPayload(CompanyMovementDetailDto detail)
    {
        var documentNumber = BuildDocumentNumber(detail.Header.DocumentSerie, detail.Header.DocumentOrderNo);
        var movementCode = DefaultInboundMovementCode;
        var orderDate = (detail.Header.DocumentDate ?? detail.Header.MovementCreateDate).Date;
        var deliveryDate = (detail.Header.MovementDate ?? detail.Header.DocumentDate ?? detail.Header.MovementCreateDate).Date;

        return new AxataLegacyInboundOrderPayload(
            documentNumber,
            movementCode,
            new AxataLegacyInboundOrderMaster(
                DefaultBranchCode,
                movementCode,
                documentNumber,
                DefaultActionCode,
                detail.Header.CustomerCode,
                orderDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                deliveryDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture)),
            detail.Items
                .OrderBy(item => item.LineNo)
                .Select(item => new AxataLegacyInboundOrderLine(
                    DefaultBranchCode,
                    movementCode,
                    documentNumber,
                    DefaultActionCode,
                    item.LineNo,
                    item.StockCode,
                    detail.Header.CustomerCode,
                    item.Quantity,
                    orderDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                    deliveryDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture)))
                .ToArray());
    }

    private static string BuildOutboundOrderEnvelope(
        AxataLegacyOutboundOrderPayload payload,
        AxataSynchronizationLiveTransportConfiguration configuration,
        string operationName) =>
        BuildSoapEnvelope(operationName, configuration, service =>
        [
            new XElement(service + "ENT000List",
                new XElement(service + "ENT000", CreateElements(payload.Master, service))),
            new XElement(service + "ENT001List",
                payload.Lines.Select(line => new XElement(service + "ENT001", CreateElements(line, service))))
        ]);

    private static string BuildInboundOrderEnvelope(
        AxataLegacyInboundOrderPayload payload,
        AxataSynchronizationLiveTransportConfiguration configuration,
        string operationName) =>
        BuildSoapEnvelope(operationName, configuration, service =>
        [
            new XElement(service + "ENT013_MSTList",
                new XElement(service + "ENT013_MST", CreateElements(payload.Master, service))),
            new XElement(service + "ENT013List",
                payload.Lines.Select(line => new XElement(service + "ENT013", CreateElements(line, service))))
        ]);

    private static string BuildSoapEnvelope(
        string operationName,
        AxataSynchronizationLiveTransportConfiguration configuration,
        Func<XNamespace, IReadOnlyCollection<XElement>> payloadFactory)
    {
        var soap = XNamespace.Get(SoapEnvelopeNamespace);
        var service = XNamespace.Get(ServiceNamespace);
        var payloadElements = payloadFactory(service);

        var document = new XDocument(
            new XElement(
                soap + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soapenv", soap),
                new XAttribute(XNamespace.Xmlns + "tem", service),
                new XElement(
                    soap + "Body",
                    new XElement(
                        service + operationName,
                        new XElement(service + "UserName", configuration.Username),
                        new XElement(service + "Password", configuration.Password),
                        payloadElements))));

        return document.ToString(SaveOptions.DisableFormatting);
    }

    private async Task<string> SendSoapAsync(
        string envelope,
        string endpointUrl,
        string operationName,
        CancellationToken cancellationToken)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpointUrl)
        {
            Content = new StringContent(envelope, Encoding.UTF8, "text/xml")
        };

        requestMessage.Headers.TryAddWithoutValidation(
            "SOAPAction",
            $"{ServiceNamespace}{ServiceContractName}/{operationName}");

        using var client = httpClientFactory.CreateClient();
        using var response = await client.SendAsync(requestMessage, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ExtractSoapFaultOrDefault(
                    responseContent,
                    $"AXATA service returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}."));
        }

        return responseContent;
    }

    private static AxataServiceResponse ParseServiceResponse(string responseXml, string operationName)
    {
        var document = XDocument.Parse(responseXml);
        var faultMessage = ExtractSoapFaultOrDefault(responseXml, null);

        if (!string.IsNullOrWhiteSpace(faultMessage))
        {
            return new AxataServiceResponse(false, null, faultMessage);
        }

        var stateText = document.Descendants()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, "state", StringComparison.OrdinalIgnoreCase))
            ?.Value
            ?.Trim();
        var messageText = document.Descendants()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, "message", StringComparison.OrdinalIgnoreCase))
            ?.Value
            ?.Trim();
        int? state = int.TryParse(stateText, out var parsedState) ? parsedState : null;
        var isSuccess = !state.HasValue || state.Value == 0;

        return new AxataServiceResponse(
            isSuccess,
            state,
            string.IsNullOrWhiteSpace(messageText)
                ? $"{operationName} response received."
                : messageText);
    }

    private static string ExtractSoapFaultOrDefault(string responseContent, string? fallbackMessage)
    {
        try
        {
            var document = XDocument.Parse(responseContent);
            var fault = document.Descendants()
                .FirstOrDefault(element => string.Equals(element.Name.LocalName, "Fault", StringComparison.OrdinalIgnoreCase));

            if (fault is null)
            {
                return fallbackMessage ?? string.Empty;
            }

            var faultString = fault.Descendants()
                .FirstOrDefault(element => string.Equals(element.Name.LocalName, "faultstring", StringComparison.OrdinalIgnoreCase))
                ?.Value
                ?.Trim();

            return string.IsNullOrWhiteSpace(faultString)
                ? fallbackMessage ?? "AXATA SOAP fault was returned."
                : faultString;
        }
        catch
        {
            return fallbackMessage ?? string.Empty;
        }
    }

    private static string RedactSensitiveXml(string xml)
    {
        try
        {
            var document = XDocument.Parse(xml);
            foreach (var element in document
                         .Descendants()
                         .Where(element => string.Equals(
                             element.Name.LocalName,
                             "Password",
                             StringComparison.OrdinalIgnoreCase)))
            {
                element.Value = "***";
            }

            return document.ToString(SaveOptions.DisableFormatting);
        }
        catch
        {
            return xml;
        }
    }

    private static IEnumerable<XElement> CreateElements(object value, XNamespace elementNamespace) =>
        value.GetType()
            .GetProperties()
            .Select(property => new XElement(elementNamespace + property.Name, property.GetValue(value) ?? string.Empty));

    private static string BuildDocumentNumber(string documentSerie, int documentOrderNo) =>
        $"{documentSerie.Trim()}.{documentOrderNo.ToString(CultureInfo.InvariantCulture)}";

    private string ResolveOperationName(string taskCode, string fallbackOperationName)
    {
        if (options.CurrentValue.Tasks.TryGetValue(taskCode, out var taskOptions) &&
            !string.IsNullOrWhiteSpace(taskOptions.LiveOperationName))
        {
            return taskOptions.LiveOperationName.Trim();
        }

        return fallbackOperationName;
    }
}

internal sealed record AxataLiveDispatchResult(
    string OperationName,
    string EndpointUrl,
    bool IsSuccess,
    int? ServiceState,
    string ServiceMessage,
    string PayloadJson,
    string RequestXml,
    string ResponseXml,
    IReadOnlyCollection<string> Notes);

internal sealed record AxataSynchronizationLiveTransportConfiguration(
    string MainEndpointUrl,
    string Username,
    string Password);

internal sealed record AxataServiceResponse(
    bool IsSuccess,
    int? State,
    string Message);

internal sealed record AxataLegacyOutboundOrderPayload(
    string DocumentNumber,
    string MovementCode,
    AxataLegacyOutboundOrderMaster Master,
    IReadOnlyCollection<AxataLegacyOutboundOrderLine> Lines);

internal sealed record AxataLegacyOutboundOrderMaster(
    string S00SKOD,
    string S00TESN,
    string S00DKAN,
    string S00TMUS,
    string S00SMUS,
    string S00FDRM,
    string S00HTP1,
    string S00HTP2,
    string S00TADR,
    string S00FBLK);

internal sealed record AxataLegacyOutboundOrderLine(
    string S01SKOD,
    string S01TESL,
    int S01KALN,
    string S01SKU,
    double S01MIKT,
    string S01DEPO);

internal sealed record AxataLegacyInboundOrderPayload(
    string DocumentNumber,
    string MovementCode,
    AxataLegacyInboundOrderMaster Master,
    IReadOnlyCollection<AxataLegacyInboundOrderLine> Lines);

internal sealed record AxataLegacyInboundOrderMaster(
    string S13SKOD,
    string S13HKOD,
    string S13BNUM,
    string S13AKOD,
    string S13FIRM,
    string S13SIPT,
    string S13TEST);

internal sealed record AxataLegacyInboundOrderLine(
    string S13SKOD,
    string S13HKOD,
    string S13BNUM,
    string S13AKOD,
    int S13KALN,
    string S13SKU,
    string S13FIRM,
    double S13MIKT,
    string S13SIPT,
    string S13TEST);

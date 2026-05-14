using System.Net.Http;
using System.Text;
using System.Xml.Linq;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.UyumsoftServisleri;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Services;

public sealed class UyumsoftConnectedQueryService(
    IHttpClientFactory httpClientFactory,
    IOptions<UyumsoftConnectedServicesOptions> options)
    : IUyumsoftConnectedQueryService
{
    private const string SoapEnvelopeNamespace = "http://schemas.xmlsoap.org/soap/envelope/";
    private const string ServiceNamespace = "http://tempuri.org/";
    private const int SendAttemptCount = 3;

    public Task<UyumsoftConnectedServiceOverviewDto> GetOverviewAsync(
        UyumsoftConnectedServiceKind serviceKind,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var catalog = UyumsoftConnectedServiceCatalog.GetService(serviceKind);
        var config = ResolveServiceOptions(serviceKind, catalog);

        return Task.FromResult(new UyumsoftConnectedServiceOverviewDto(
            catalog.ServiceKey,
            catalog.ServiceName,
            config.EndpointUrl,
            config.WsdlUrl,
            config.ContractName,
            catalog.Operations
                .Select(operation => operation with
                {
                    SoapAction = BuildSoapAction(config.ContractName, operation.OperationName)
                })
                .ToArray()));
    }

    public Task<IReadOnlyCollection<UyumsoftOperationDefinitionDto>> GetOperationsAsync(
        UyumsoftConnectedServiceKind serviceKind,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var catalog = UyumsoftConnectedServiceCatalog.GetService(serviceKind);
        var config = ResolveServiceOptions(serviceKind, catalog);

        IReadOnlyCollection<UyumsoftOperationDefinitionDto> operations = catalog.Operations
            .Select(operation => operation with
            {
                SoapAction = BuildSoapAction(config.ContractName, operation.OperationName)
            })
            .ToArray();

        return Task.FromResult(operations);
    }

    public async Task<UyumsoftOperationResponseDto> InvokeGetOperationAsync(
        UyumsoftConnectedServiceKind serviceKind,
        UyumsoftOperationInvocationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OperationName))
        {
            throw new ArgumentException("Operation name is required.", nameof(request));
        }

        var catalog = UyumsoftConnectedServiceCatalog.GetService(serviceKind);
        var config = ResolveServiceOptions(serviceKind, catalog);
        var operation = UyumsoftConnectedServiceCatalog.GetGetOperation(serviceKind, request.OperationName);
        var soapAction = BuildSoapAction(config.ContractName, operation.OperationName);
        var envelope = BuildEnvelope(config, operation.OperationName, request);
        var responseContent = await SendSoapRequestAsync(
            config.EndpointUrl,
            soapAction,
            envelope,
            cancellationToken);

        return ParseResponse(
            catalog,
            operation.OperationName,
            responseContent);
    }

    private UyumsoftServiceEndpointOptions ResolveServiceOptions(
        UyumsoftConnectedServiceKind serviceKind,
        UyumsoftServiceCatalogEntry catalog)
    {
        var configured = serviceKind switch
        {
            UyumsoftConnectedServiceKind.EInvoice => options.Value.EInvoice,
            UyumsoftConnectedServiceKind.EDespatch => options.Value.EDespatch,
            _ => throw new ArgumentOutOfRangeException(nameof(serviceKind), serviceKind, "Unsupported Uyumsoft service.")
        };

        var resolved = configured with
        {
            EndpointUrl = string.IsNullOrWhiteSpace(configured.EndpointUrl)
                ? catalog.DefaultEndpointUrl
                : configured.EndpointUrl,
            WsdlUrl = string.IsNullOrWhiteSpace(configured.WsdlUrl)
                ? catalog.DefaultWsdlUrl
                : configured.WsdlUrl,
            ContractName = string.IsNullOrWhiteSpace(configured.ContractName)
                ? catalog.ContractName
                : configured.ContractName
        };

        if (string.IsNullOrWhiteSpace(resolved.EndpointUrl))
        {
            throw new InvalidOperationException($"{catalog.ServiceName} endpoint configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(resolved.Username))
        {
            throw new InvalidOperationException($"{catalog.ServiceName} username configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(resolved.Password))
        {
            throw new InvalidOperationException($"{catalog.ServiceName} password configuration is required.");
        }

        return resolved;
    }

    private static string BuildSoapAction(string contractName, string operationName) =>
        $"{ServiceNamespace}{contractName}/{operationName}";

    private static string BuildEnvelope(
        UyumsoftServiceEndpointOptions options,
        string operationName,
        UyumsoftOperationInvocationRequest request)
    {
        var soapNamespace = XNamespace.Get(SoapEnvelopeNamespace);
        var serviceNamespace = XNamespace.Get(ServiceNamespace);
        var operationElement = new XElement(
            serviceNamespace + operationName,
            new XElement(
                serviceNamespace + "userInfo",
                new XAttribute("Username", options.Username),
                new XAttribute("Password", options.Password)));

        foreach (var parameter in request.Parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.Name))
            {
                continue;
            }

            operationElement.Add(new XElement(serviceNamespace + parameter.Name.Trim(), parameter.Value ?? string.Empty));
        }

        foreach (var fragment in ParsePayloadFragments(request.PayloadXml))
        {
            QualifyElementNamespace(fragment, serviceNamespace);
            operationElement.Add(fragment);
        }

        var envelope = new XDocument(
            new XElement(
                soapNamespace + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soapenv", soapNamespace),
                new XAttribute(XNamespace.Xmlns + "tem", serviceNamespace),
                new XElement(
                    soapNamespace + "Body",
                    operationElement)));

        return envelope.ToString(SaveOptions.DisableFormatting);
    }

    private static IReadOnlyCollection<XElement> ParsePayloadFragments(string? payloadXml)
    {
        if (string.IsNullOrWhiteSpace(payloadXml))
        {
            return Array.Empty<XElement>();
        }

        try
        {
            var wrapped = XDocument.Parse($"<root>{payloadXml}</root>");

            return wrapped.Root?.Elements().ToArray() ?? Array.Empty<XElement>();
        }
        catch (Exception exception)
        {
            throw new ArgumentException("payloadXml is not a valid XML fragment.", nameof(payloadXml), exception);
        }
    }

    private static void QualifyElementNamespace(
        XElement element,
        XNamespace serviceNamespace)
    {
        if (element.Name.Namespace == XNamespace.None)
        {
            element.Name = serviceNamespace + element.Name.LocalName;
        }

        foreach (var child in element.Elements())
        {
            QualifyElementNamespace(child, serviceNamespace);
        }
    }

    private async Task<string> SendSoapRequestAsync(
        string endpointUrl,
        string soapAction,
        string envelope,
        CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient(nameof(UyumsoftConnectedQueryService));
        Exception? lastTransportException = null;

        for (var attempt = 1; attempt <= SendAttemptCount; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
            request.Headers.Add("SOAPAction", soapAction);
            request.Content = new StringContent(envelope, Encoding.UTF8, "text/xml");

            try
            {
                using var response = await client.SendAsync(request, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var faultMessage = ExtractSoapFaultOrDefault(responseContent, null);

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(faultMessage)
                            ? $"Uyumsoft request failed with HTTP {(int)response.StatusCode}."
                            : faultMessage);
                }

                if (!string.IsNullOrWhiteSpace(faultMessage))
                {
                    throw new InvalidOperationException(faultMessage);
                }

                return responseContent;
            }
            catch (HttpRequestException exception) when (attempt < SendAttemptCount)
            {
                lastTransportException = exception;
                await Task.Delay(TimeSpan.FromMilliseconds(400 * attempt), cancellationToken);
            }
            catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested && attempt < SendAttemptCount)
            {
                lastTransportException = exception;
                await Task.Delay(TimeSpan.FromMilliseconds(400 * attempt), cancellationToken);
            }
        }

        throw new HttpRequestException(
            $"Uyumsoft request could not be completed after {SendAttemptCount} attempts.",
            lastTransportException);
    }

    private static UyumsoftOperationResponseDto ParseResponse(
        UyumsoftServiceCatalogEntry catalog,
        string operationName,
        string responseContent)
    {
        var document = XDocument.Parse(responseContent);
        var resultElementName = $"{operationName}Result";
        var resultElement = document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == resultElementName);

        if (resultElement is null)
        {
            throw new InvalidOperationException(
                $"{catalog.ServiceName} response could not be parsed for {operationName}.");
        }

        var attributes = resultElement.Attributes()
            .ToDictionary(
                attribute => attribute.Name.LocalName,
                attribute => NormalizeValue(attribute.Value),
                StringComparer.OrdinalIgnoreCase);
        var isSucceeded = !attributes.TryGetValue("IsSucceded", out var succeededValue) ||
                          !bool.TryParse(succeededValue, out var parsedSucceeded) ||
                          parsedSucceeded;
        var message = attributes.TryGetValue("Message", out var parsedMessage)
            ? parsedMessage
            : null;

        if (!isSucceeded)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(message)
                    ? $"{catalog.ServiceName} request was rejected by {operationName}."
                    : message);
        }

        var nodes = resultElement.Elements().Select(MapNode).ToArray();
        var scalarValue = attributes.TryGetValue("Value", out var valueAttribute)
            ? valueAttribute
            : nodes.Length == 0
                ? NormalizeValue(resultElement.Value)
                : null;

        return new UyumsoftOperationResponseDto(
            catalog.ServiceKey,
            catalog.ServiceName,
            operationName,
            resultElementName,
            isSucceeded,
            message,
            scalarValue,
            attributes,
            nodes,
            resultElement.ToString(SaveOptions.DisableFormatting));
    }

    private static UyumsoftResponseNodeDto MapNode(XElement element)
    {
        var children = element.Elements().Select(MapNode).ToArray();

        return new UyumsoftResponseNodeDto(
            element.Name.LocalName,
            children.Length == 0 ? NormalizeValue(element.Value) : null,
            element.Attributes()
                .ToDictionary(
                    attribute => attribute.Name.LocalName,
                    attribute => NormalizeValue(attribute.Value),
                    StringComparer.OrdinalIgnoreCase),
            children);
    }

    private static string? NormalizeValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? ExtractSoapFaultOrDefault(string responseContent, string? fallbackMessage)
    {
        try
        {
            var document = XDocument.Parse(responseContent);
            var faultElement = document.Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "Fault");

            if (faultElement is null)
            {
                return fallbackMessage;
            }

            return faultElement.Descendants()
                       .FirstOrDefault(element =>
                           element.Name.LocalName is "faultstring" or "Reason" or "Text")
                       ?.Value
                       ?.Trim()
                   ?? fallbackMessage;
        }
        catch
        {
            return fallbackMessage;
        }
    }
}

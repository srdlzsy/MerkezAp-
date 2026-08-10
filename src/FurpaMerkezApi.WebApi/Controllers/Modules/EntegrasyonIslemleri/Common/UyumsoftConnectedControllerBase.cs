using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.UyumsoftServisleri;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using Microsoft.AspNetCore.Http;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.EntegrasyonIslemleri.Common;

public abstract class UyumsoftConnectedControllerBase(
    IUyumsoftConnectedQueryService queryService,
    UyumsoftConnectedServiceKind serviceKind,
    string moduleCode,
    string moduleName,
    string menuCode,
    string menuName)
    : ModuleMenuControllerBase(moduleCode, moduleName, menuCode, menuName)
{
    protected Task<UyumsoftConnectedServiceOverviewDto> GetOverviewAsync(CancellationToken cancellationToken) =>
        queryService.GetOverviewAsync(serviceKind, cancellationToken);

    protected Task<IReadOnlyCollection<UyumsoftOperationDefinitionDto>> GetOperationsAsync(
        CancellationToken cancellationToken) =>
        queryService.GetOperationsAsync(serviceKind, cancellationToken);

    protected Task<UyumsoftOperationResponseDto> InvokeOperationAsync(
        string operationName,
        CancellationToken cancellationToken,
        params UyumsoftOperationParameterRequest[] parameters) =>
        queryService.InvokeGetOperationAsync(
            serviceKind,
            new UyumsoftOperationInvocationRequest(operationName, parameters),
            cancellationToken);

    protected Task<UyumsoftOperationResponseDto> InvokeOperationAsync(
        string operationName,
        IReadOnlyCollection<UyumsoftOperationParameterRequest> parameters,
        CancellationToken cancellationToken) =>
        queryService.InvokeGetOperationAsync(
            serviceKind,
            new UyumsoftOperationInvocationRequest(operationName, parameters.ToArray()),
            cancellationToken);

    protected Task<byte[]> GetInboxInvoicePdfFileAsync(
        string invoiceUuid,
        CancellationToken cancellationToken) =>
        queryService.GetInboxInvoicePdfFileAsync(invoiceUuid, cancellationToken);

    protected Task<byte[]> GetOutboxInvoicePdfFileAsync(
        string invoiceUuid,
        CancellationToken cancellationToken) =>
        queryService.GetOutboxInvoicePdfFileAsync(invoiceUuid, cancellationToken);

    protected static UyumsoftOperationParameterRequest Parameter(string name, string? value) =>
        new(name, value);

    protected static string RequireQueryValue(string? value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{parameterName} query parameter is required.", parameterName)
            : value;

    protected static bool RequireQueryValue(bool? value, string parameterName) =>
        value ?? throw new ArgumentException($"{parameterName} query parameter is required.", parameterName);

    protected static IReadOnlyCollection<UyumsoftOperationParameterRequest> ParseParameters(string[]? parameterPairs)
    {
        if (parameterPairs is null || parameterPairs.Length == 0)
        {
            return Array.Empty<UyumsoftOperationParameterRequest>();
        }

        return parameterPairs
            .Select(ParseParameter)
            .ToArray();
    }

    protected static IReadOnlyCollection<UyumsoftOperationParameterRequest> ParseParameters(
        IQueryCollection query,
        string[]? parameterPairs)
    {
        var parsedParameters = ParseParameters(parameterPairs).ToList();

        foreach (var queryParameter in query)
        {
            if (string.Equals(queryParameter.Key, "parameter", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var value in queryParameter.Value)
            {
                parsedParameters.Add(new UyumsoftOperationParameterRequest(queryParameter.Key, value));
            }
        }

        return parsedParameters;
    }

    private static UyumsoftOperationParameterRequest ParseParameter(string parameterPair)
    {
        if (string.IsNullOrWhiteSpace(parameterPair))
        {
            throw new ArgumentException("Query string parameter entries cannot be empty.", nameof(parameterPair));
        }

        var separatorIndex = parameterPair.IndexOf('=');

        if (separatorIndex <= 0)
        {
            throw new ArgumentException(
                "Query string parameter entries must use the name=value format.",
                nameof(parameterPair));
        }

        var name = parameterPair[..separatorIndex].Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Query string parameter names cannot be empty.", nameof(parameterPair));
        }

        var value = separatorIndex == parameterPair.Length - 1
            ? null
            : parameterPair[(separatorIndex + 1)..];

        return new UyumsoftOperationParameterRequest(name, value);
    }
}

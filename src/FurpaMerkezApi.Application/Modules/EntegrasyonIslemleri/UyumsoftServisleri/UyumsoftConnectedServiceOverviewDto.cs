namespace FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.UyumsoftServisleri;

public sealed record UyumsoftConnectedServiceOverviewDto(
    string ServiceKey,
    string ServiceName,
    string EndpointUrl,
    string WsdlUrl,
    string ContractName,
    IReadOnlyCollection<UyumsoftOperationDefinitionDto> SupportedGetOperations);

public sealed record UyumsoftOperationDefinitionDto(
    string OperationName,
    string GroupName,
    string SoapAction,
    string RequestHint,
    IReadOnlyCollection<UyumsoftOperationParameterDefinitionDto> Parameters)
{
    public UyumsoftOperationDefinitionDto(
        string operationName,
        string groupName,
        string soapAction,
        string requestHint)
        : this(
            operationName,
            groupName,
            soapAction,
            requestHint,
            Array.Empty<UyumsoftOperationParameterDefinitionDto>())
    {
    }
}

public sealed record UyumsoftOperationParameterDefinitionDto(
    string Name,
    string Type,
    bool IsArray,
    bool IsRequired,
    string? Description,
    IReadOnlyCollection<string> AllowedValues);

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
    string RequestHint);

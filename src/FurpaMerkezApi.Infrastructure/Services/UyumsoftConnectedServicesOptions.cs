namespace FurpaMerkezApi.Infrastructure.Services;

public sealed record UyumsoftConnectedServicesOptions(
    UyumsoftServiceEndpointOptions EInvoice,
    UyumsoftServiceEndpointOptions EDespatch);

public sealed record UyumsoftServiceEndpointOptions(
    string EndpointUrl,
    string WsdlUrl,
    string Username,
    string Password,
    string ContractName,
    int? TimeoutSeconds = null);

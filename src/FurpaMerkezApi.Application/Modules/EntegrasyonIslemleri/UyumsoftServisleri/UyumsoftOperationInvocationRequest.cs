namespace FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.UyumsoftServisleri;

public sealed record UyumsoftOperationInvocationRequest(
    string OperationName,
    IReadOnlyCollection<UyumsoftOperationParameterRequest> Parameters);

public sealed record UyumsoftOperationParameterRequest(
    string Name,
    string? Value);

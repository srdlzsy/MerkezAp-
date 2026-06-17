namespace FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.UyumsoftServisleri;

public sealed record UyumsoftOperationResponseDto(
    string ServiceKey,
    string ServiceName,
    string OperationName,
    string ResultElementName,
    bool IsSucceeded,
    string? Message,
    string? ScalarValue,
    IReadOnlyDictionary<string, string?> ResultAttributes,
    IReadOnlyCollection<UyumsoftResponseNodeDto> Nodes,
    string ResponsePayloadJson);

public sealed record UyumsoftResponseNodeDto(
    string Name,
    string? Value,
    IReadOnlyDictionary<string, string?> Attributes,
    IReadOnlyCollection<UyumsoftResponseNodeDto> Children);

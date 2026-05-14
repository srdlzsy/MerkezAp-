namespace FurpaMerkezApi.Application.Modules.OperasyonIslemleri.Operations;

public sealed record SaveAuthorizationFileItemRequest(
    int Id,
    string Name,
    bool Z,
    bool R,
    bool X);

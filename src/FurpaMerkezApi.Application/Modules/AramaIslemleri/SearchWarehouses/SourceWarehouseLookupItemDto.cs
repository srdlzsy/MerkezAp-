namespace FurpaMerkezApi.Application.Modules.AramaIslemleri.SearchWarehouses;

public sealed record SourceWarehouseLookupItemDto(
    int SourceWarehouseNo,
    string SourceWarehouseName,
    IReadOnlyCollection<string> ModelCodes,
    IReadOnlyCollection<string> ModelNames,
    string DisplayName);

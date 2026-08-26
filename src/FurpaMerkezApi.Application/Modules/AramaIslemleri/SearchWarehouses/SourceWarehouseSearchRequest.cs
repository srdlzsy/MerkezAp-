namespace FurpaMerkezApi.Application.Modules.AramaIslemleri.SearchWarehouses;

public sealed record SourceWarehouseSearchRequest(
    string? SearchText,
    int Take);

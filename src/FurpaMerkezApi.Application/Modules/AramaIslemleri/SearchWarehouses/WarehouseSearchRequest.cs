namespace FurpaMerkezApi.Application.Modules.AramaIslemleri.SearchWarehouses;

public sealed record WarehouseSearchRequest(
    string? SearchText,
    int? WarehouseNo,
    int Take);

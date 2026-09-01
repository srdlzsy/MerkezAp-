namespace FurpaMerkezApi.Application.Modules.AramaIslemleri.ProductAvailability;

public sealed record ProductAvailabilityRequest(
    int WarehouseNo,
    string? Barcode,
    string? StockCode,
    string? StockName,
    int Take);

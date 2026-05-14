namespace FurpaMerkezApi.Application.Modules.AramaIslemleri.SearchProducts;

public sealed record ProductSearchRequest(
    int WarehouseNo,
    string? Barcode,
    string? StockCode,
    string? StockName,
    string? SupplierCode,
    int Take);

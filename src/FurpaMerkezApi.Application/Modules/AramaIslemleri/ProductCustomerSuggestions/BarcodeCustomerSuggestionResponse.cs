namespace FurpaMerkezApi.Application.Modules.AramaIslemleri.ProductCustomerSuggestions;

public sealed record BarcodeCustomerSuggestionResponse(
    bool IsFound,
    string Barcode,
    int WarehouseNo,
    string? ResolutionSource,
    string? StockCode,
    string? StockName,
    string? MatchedBarcode,
    string? PrimaryBarcode,
    string? CaseBarcode,
    double? UnitsPerCase,
    string? DefaultSupplierCode,
    string? DefaultSupplierName,
    IReadOnlyCollection<ProductCustomerSuggestionDto> Suggestions);

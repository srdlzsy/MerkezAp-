namespace FurpaMerkezApi.Application.Modules.AramaIslemleri.ProductCustomerSuggestions;

public sealed record ProductCustomerSuggestionResponse(
    bool IsProductFound,
    string StockCode,
    string? StockName,
    string? DefaultSupplierCode,
    string? DefaultSupplierName,
    IReadOnlyCollection<ProductCustomerSuggestionDto> Suggestions);

public sealed record ProductCustomerSuggestionDto(
    string CustomerCode,
    string CustomerName,
    string? TaxNoOrTckn,
    bool IsDefaultSupplier,
    int MovementCount,
    DateTime? LastMovementDate,
    string? LastDocumentNo,
    IReadOnlyCollection<string> Sources);

namespace FurpaMerkezApi.Application.Modules.AramaIslemleri.ProductCustomerSuggestions;

public sealed record ProductCustomerSuggestionRequest(
    string StockCode,
    int Take);

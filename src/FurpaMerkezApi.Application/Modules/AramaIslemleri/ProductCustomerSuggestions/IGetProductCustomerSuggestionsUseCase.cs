namespace FurpaMerkezApi.Application.Modules.AramaIslemleri.ProductCustomerSuggestions;

public interface IGetProductCustomerSuggestionsUseCase
{
    Task<ProductCustomerSuggestionResponse> ExecuteAsync(
        ProductCustomerSuggestionRequest request,
        CancellationToken cancellationToken);
}

namespace FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar.ConversionSuggestion;

public interface IVirmanConversionSuggestionUseCase
{
    Task<VirmanConversionSuggestionDto> ExecuteAsync(
        VirmanConversionSuggestionRequest request,
        CancellationToken cancellationToken);
}

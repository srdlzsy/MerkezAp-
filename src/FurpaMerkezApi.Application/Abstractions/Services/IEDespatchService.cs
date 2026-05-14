namespace FurpaMerkezApi.Application.Abstractions.Services;

public interface IEDespatchService
{
    Task<SendEDespatchResponse> SendAsync(
        SendEDespatchRequest request,
        CancellationToken cancellationToken = default);

    Task<GetEDespatchPdfResponse> GetPdfAsync(
        GetEDespatchPdfRequest request,
        CancellationToken cancellationToken = default);
}

namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari.Commands;

public interface ICashSummaryCommandsUseCase
{
    Task<CreateBanknoteTrackResponse> CreateBanknoteTrackAsync(
        CreateBanknoteTrackRequest request,
        CancellationToken cancellationToken);

    Task<CreateCashSummaryResponse> CreateAsync(
        CreateCashSummaryRequest request,
        CancellationToken cancellationToken);

    Task<UpdateCashSummaryDetailsResponse> UpdateDetailsAsync(
        UpdateCashSummaryDetailsRequest request,
        CancellationToken cancellationToken);

    Task<UpdateCashSummaryBanknotesResponse> UpdateBanknotesAsync(
        UpdateCashSummaryBanknotesRequest request,
        CancellationToken cancellationToken);

    Task<DeleteCashSummaryResponse> DeleteAsync(
        DeleteCashSummaryRequest request,
        CancellationToken cancellationToken);
}

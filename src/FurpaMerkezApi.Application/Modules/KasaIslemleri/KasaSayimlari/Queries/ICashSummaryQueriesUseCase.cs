namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari.Queries;

public interface ICashSummaryQueriesUseCase
{
    Task<IReadOnlyCollection<CashSummaryReportItemDto>> GetReportAsync(
        CashSummaryDateRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CashSummaryListItemDto>> ListAsync(
        CashSummaryDateRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CashSummaryDetailItemDto>> GetDetailsAsync(
        CashSummaryDocumentRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<BanknoteMovementItemDto>> GetBanknoteMovementsAsync(
        CashSummaryDocumentRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<GiftCheckMovementItemDto>> GetGiftCheckMovementsAsync(
        CashSummaryDocumentRequest request,
        CancellationToken cancellationToken);
}

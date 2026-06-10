namespace FurpaMerkezApi.Application.Modules.RaporIslemleri.SatisAnalizleri;

public interface ISalesAnalysisReportsUseCase
{
    Task<IReadOnlyCollection<BankMovementAnalysisItemDto>> GetBankMovementsAsync(
        SalesAnalysisDateRangeRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<BranchBankMovementSummaryItemDto>> GetBankMovementsByBranchAsync(
        SalesAnalysisDateRangeRequest request,
        CancellationToken cancellationToken);

    Task<BankPaymentSummaryReportDto> GetBankPaymentSummaryAsync(
        SalesAnalysisDateRangeRequest request,
        CancellationToken cancellationToken);

    Task<MerchantPaymentSummaryReportDto> GetMerchantPaymentSummaryAsync(
        SalesAnalysisDateRangeRequest request,
        CancellationToken cancellationToken);

    Task<ValorPaymentSummaryReportDto> GetValorPaymentSummaryAsync(
        SalesAnalysisDateRangeRequest request,
        CancellationToken cancellationToken);

    Task<FoodCheckReportDto> GetFoodCheckReportAsync(
        SalesAnalysisDateRangeRequest request,
        CancellationToken cancellationToken);

    Task<SalesAnalysisAmountDto> GetFoodCheckTotalAsync(
        SalesAnalysisDateRangeRequest request,
        FoodCheckTotalKind totalKind,
        CancellationToken cancellationToken);

    Task<MyoSalesReportDto> GetMyoSalesReportAsync(
        SalesAnalysisDateRangeRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MyoSalesByBranchItemDto>> GetMyoSalesByBranchAsync(
        SalesAnalysisDateRangeRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ZReportBankAnalysisItemDto>> GetZReportBankAnalysisAsync(
        SalesAnalysisDateRangeRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<DiscountCardDetailItemDto>> GetDiscountCardDetailsAsync(
        SalesAnalysisDateRangeRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MissingTurnoverBranchItemDto>> GetMissingTurnoverBranchesAsync(
        SalesAnalysisDateRangeRequest request,
        CancellationToken cancellationToken);
}

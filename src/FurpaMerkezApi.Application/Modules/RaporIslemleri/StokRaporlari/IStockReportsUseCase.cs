namespace FurpaMerkezApi.Application.Modules.RaporIslemleri.StokRaporlari;

public interface IStockReportsUseCase
{
    Task<StockOnHandReportDto> GetStockOnHandAsync(
        StockOnHandReportRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ProductWarehouseStockDto>> GetProductWarehouseStockAsync(
        ProductWarehouseStockRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<StockCardDetailDto>> GetStockCardDetailsAsync(
        StockCardDetailRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<WarehouseMissingStockDto>> GetWarehouseHasBranchMissingAsync(
        WarehouseMissingStockRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<WarehouseZeroStockDto>> GetWarehouseZeroStocksAsync(
        WarehouseZeroStockRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<StockMovementReportItemDto>> GetStockMovementsAsync(
        StockMovementReportRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MovementInOutComparisonDto>> GetInOutComparisonAsync(
        MovementInOutComparisonRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<BranchSalesReportItemDto>> GetBranchSalesAsync(
        BranchSalesReportRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<YearSalesComparisonItemDto>> GetYearSalesComparisonAsync(
        YearSalesComparisonRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ReturnBranchReportItemDto>> GetReturnBranchesAsync(
        ReturnBranchReportRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<NotSoldProductReportItemDto>> GetNotSoldProductsAsync(
        NotSoldProductReportRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ProfitabilityReportItemDto>> GetProfitabilityAsync(
        ProfitabilityReportRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CountingComparisonReportItemDto>> GetCountingComparisonAsync(
        CountingComparisonReportRequest request,
        CancellationToken cancellationToken);
}

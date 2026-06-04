namespace FurpaMerkezApi.Application.Modules.GreenGrocer.Reports;

public interface IGreenGrocerReportsUseCase
{
    Task<GreenGrocerBranchReportDto> GetByBranchAsync(
        GreenGrocerReportDateRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<GreenGrocerGreenReportItemDto>> GetGreensAsync(
        GreenGrocerReportDateRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<GreenGrocerProductReportItemDto>> GetSummaryAsync(
        GreenGrocerReportDateRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<GreenGrocerProductReportGroupDto>> GetByProductAsync(
        GreenGrocerReportDateRequest request,
        CancellationToken cancellationToken);
}

namespace FurpaMerkezApi.Application.Modules.RaporIslemleri.PromosyonRaporlari;

public interface IPromotionReportsUseCase
{
    Task<IReadOnlyCollection<PromotionBulletinListItemDto>> GetBulletinsAsync(
        PromotionBulletinListRequest request,
        CancellationToken cancellationToken);

    Task<PromotionPerformanceReportDto> GetPerformanceAsync(
        PromotionPerformanceRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PromotionBranchPerformanceItemDto>> GetBranchPerformanceAsync(
        PromotionPerformanceRequest request,
        CancellationToken cancellationToken);
}

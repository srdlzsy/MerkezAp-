namespace FurpaMerkezApi.Application.Modules.RaporIslemleri.KasaRaporlari;

public interface IKasaRaporlariUseCase
{
    Task<IReadOnlyCollection<ExpenseSlipZoneSummaryDto>> GetExpenseSlipHierarchyReportAsync(
        ExpenseSlipReportRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ExpenseSlipDetailItemDto>> GetExpenseSlipDetailsAsync(
        ExpenseSlipReportRequest request,
        CancellationToken cancellationToken);
}

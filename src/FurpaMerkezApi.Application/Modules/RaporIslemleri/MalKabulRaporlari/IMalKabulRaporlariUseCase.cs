namespace FurpaMerkezApi.Application.Modules.RaporIslemleri.MalKabulRaporlari;

public interface IMalKabulRaporlariUseCase
{
    Task<IReadOnlyCollection<LazyBranchReceivingItemDto>> GetLazyBranchesAsync(
        LazyBranchesWarehouseReceivingRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LazyBranchDocumentDetailDto>> GetLazyBranchDocumentsAsync(
        LazyBranchesWarehouseReceivingRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CompanyDocumentByCustomerItemDto>> GetCompanyDocumentByCustomerAsync(
        CompanyDocumentByCustomerRequest request,
        CancellationToken cancellationToken);
}

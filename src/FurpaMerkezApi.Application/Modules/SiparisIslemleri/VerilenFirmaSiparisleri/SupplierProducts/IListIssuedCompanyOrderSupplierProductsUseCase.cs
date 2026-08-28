namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.SupplierProducts;

public interface IListIssuedCompanyOrderSupplierProductsUseCase
{
    Task<IReadOnlyCollection<IssuedCompanyOrderSupplierProductDto>> ExecuteAsync(
        IssuedCompanyOrderSupplierProductsRequest request,
        CancellationToken cancellationToken);
}

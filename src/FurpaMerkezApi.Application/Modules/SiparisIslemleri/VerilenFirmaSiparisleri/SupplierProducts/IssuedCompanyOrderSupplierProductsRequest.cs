namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.SupplierProducts;

public sealed record IssuedCompanyOrderSupplierProductsRequest(
    int WarehouseNo,
    string CustomerCode,
    string? Search,
    int Take);

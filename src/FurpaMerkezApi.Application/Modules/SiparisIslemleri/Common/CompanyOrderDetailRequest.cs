namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;

public sealed record CompanyOrderDetailRequest(
    int WarehouseNo,
    string DocumentSerie,
    int DocumentOrderNo);

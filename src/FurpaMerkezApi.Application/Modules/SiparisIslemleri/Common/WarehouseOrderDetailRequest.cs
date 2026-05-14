namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;

public sealed record WarehouseOrderDetailRequest(
    int WarehouseNo,
    string DocumentSerie,
    int DocumentOrderNo);

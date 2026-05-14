namespace FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;

public sealed record WarehouseShippingDetailRequest(
    int WarehouseNo,
    string DocumentSerie,
    int DocumentOrderNo);

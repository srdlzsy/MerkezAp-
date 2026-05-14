namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;

public sealed record WarehouseOrderListRequest(
    int WarehouseNo,
    DateTime StartDate,
    DateTime EndDate);

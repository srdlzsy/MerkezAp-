namespace FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;

public sealed record WarehouseShippingListRequest(
    int? WarehouseNo,
    DateTime StartDate,
    DateTime EndDate);

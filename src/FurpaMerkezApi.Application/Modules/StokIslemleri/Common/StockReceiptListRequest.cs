namespace FurpaMerkezApi.Application.Modules.StokIslemleri.Common;

public sealed record StockReceiptListRequest(
    int? WarehouseNo,
    DateTime StartDate,
    DateTime EndDate);

namespace FurpaMerkezApi.Application.Modules.StokIslemleri.Common;

public sealed record StockReceiptDetailRequest(
    int WarehouseNo,
    string DocumentSerie,
    int DocumentOrderNo);

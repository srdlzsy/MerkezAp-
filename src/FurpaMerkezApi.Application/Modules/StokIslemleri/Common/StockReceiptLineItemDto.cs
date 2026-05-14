namespace FurpaMerkezApi.Application.Modules.StokIslemleri.Common;

public sealed record StockReceiptLineItemDto(
    int RowNo,
    string StockCode,
    string StockName,
    string UnitName,
    byte UnitPointer,
    double Quantity,
    double Quantity2,
    double UnitPrice,
    double LineAmount,
    string Description,
    string PartyCode,
    int LotNo,
    string ProjectCode);

namespace FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;

public sealed record WarehouseShippingLineItemDto(
    Guid MovementGuid,
    int LineNo,
    string StockCode,
    string StockName,
    string UnitName,
    byte UnitPointer,
    double Quantity,
    double UnitPrice,
    double LineAmount,
    string Description,
    string PartyCode,
    int LotNo,
    string ProjectCode,
    string WarehouseOrderNo);

namespace FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari;

public sealed record InventoryCountLineItemDto(
    int RowNo,
    string StockCode,
    string StockName,
    string Barcode,
    string UnitName,
    byte UnitPointer,
    double Quantity1,
    double Quantity2,
    double Quantity3,
    double Quantity4,
    double Quantity5);

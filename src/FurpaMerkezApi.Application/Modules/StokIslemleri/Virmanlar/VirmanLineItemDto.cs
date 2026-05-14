namespace FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar;

public sealed record VirmanLineItemDto(
    int RowNo,
    string StockCode,
    string StockName,
    string UnitName,
    byte UnitPointer,
    byte MovementType,
    double Quantity,
    double Quantity2,
    double UnitPrice,
    double LineAmount,
    string Description,
    string PartyCode,
    int LotNo,
    string ProjectCode);

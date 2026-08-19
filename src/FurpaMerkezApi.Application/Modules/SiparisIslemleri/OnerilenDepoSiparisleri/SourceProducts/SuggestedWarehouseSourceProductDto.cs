namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.OnerilenDepoSiparisleri.SourceProducts;

public sealed record SuggestedWarehouseSourceProductDto(
    int SourceWarehouseNo,
    string SourceWarehouseName,
    string StockCode,
    string StockName,
    string ModelCode,
    string ModelName,
    string UnitName,
    string Barcode,
    double Quantity,
    double RecommendedQuantity,
    double UnitPrice,
    int UnitPointer);

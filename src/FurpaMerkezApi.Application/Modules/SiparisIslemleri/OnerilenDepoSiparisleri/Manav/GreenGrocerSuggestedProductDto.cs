namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.OnerilenDepoSiparisleri.Manav;

public sealed record GreenGrocerSuggestedProductDto(
    string StockCode,
    string StockName,
    string ModelCode,
    string ModelName,
    string UnitName,
    double Quantity,
    double RecommendedQuantity,
    double UnitPrice,
    int UnitPointer);

namespace FurpaMerkezApi.Application.Modules.AramaIslemleri.SearchProducts;

public sealed record ProductLookupItemDto(
    int WarehouseNo,
    string Barcode,
    string StockCode,
    string StockName,
    double Price,
    int PriceTypeCode,
    string UnitName,
    double UnitMultiplier,
    string SecondaryUnitName,
    double SecondaryUnitMultiplier,
    int? SalesBlockCode,
    int? OrderBlockCode,
    int? GoodsAcceptanceBlockCode,
    bool IsSalesBlocked,
    bool IsOrderBlocked,
    bool IsGoodsAcceptanceBlocked,
    string ProductManagerCode);

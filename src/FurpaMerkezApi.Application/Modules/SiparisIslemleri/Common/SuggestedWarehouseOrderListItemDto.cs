namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;

public sealed record SuggestedWarehouseOrderListItemDto(
    string StockCode,
    string StockName,
    string ModelCode,
    string Barcode,
    double TargetOnHand,
    double SourceOnHand,
    double SalesQuantity,
    double OpenIncomingOrderQuantity,
    double PackageFactor,
    double MinDay,
    double RecommendedDay,
    double MaxDay,
    double RecommendedStockQuantity,
    double NeedQuantity,
    double SuggestedOrderQuantity);

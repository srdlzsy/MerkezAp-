namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;

public sealed record SuggestedCompanyOrderListItemDto(
    string SupplierCode,
    string SupplierName,
    string StockCode,
    string StockName,
    string ModelCode,
    string Barcode,
    double TargetOnHand,
    double SalesQuantity,
    double OpenCompanyOrderQuantity,
    double PackageFactor,
    double MinDay,
    double RecommendedDay,
    double MaxDay,
    double RecommendedStockQuantity,
    double NeedQuantity,
    double SuggestedOrderQuantity,
    double PurchasePrice,
    double MinimumPurchaseQuantity,
    int? DeliveryDay);

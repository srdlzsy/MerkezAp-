namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.SupplierProducts;

public sealed record IssuedCompanyOrderSupplierProductDto(
    int WarehouseNo,
    string CustomerCode,
    string CustomerName,
    string StockCode,
    string StockName,
    string ModelCode,
    string ModelName,
    string UnitName,
    string SecondaryUnitName,
    double UnitMultiplier,
    string Barcode,
    string CaseBarcode,
    double Quantity,
    double RecommendedQuantity,
    double UnitPrice,
    double MinimumPurchaseQuantity,
    int? DeliveryDay,
    int UnitPointer);

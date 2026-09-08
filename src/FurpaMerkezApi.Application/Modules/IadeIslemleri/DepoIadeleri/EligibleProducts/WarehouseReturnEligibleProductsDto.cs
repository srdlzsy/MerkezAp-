namespace FurpaMerkezApi.Application.Modules.IadeIslemleri.DepoIadeleri.EligibleProducts;

public sealed record WarehouseReturnEligibleProductsDto(
    int SourceWarehouseNo,
    string SourceWarehouseName,
    int TotalCount,
    IReadOnlyCollection<WarehouseReturnEligibleProductDto> Items);

public sealed record WarehouseReturnEligibleProductDto(
    string StockCode,
    string StockName,
    string Barcode,
    string CaseBarcode,
    string ModelCode,
    string ModelName,
    string UnitName,
    string SecondaryUnitName,
    double UnitMultiplier,
    int ProductSourceWarehouseNo,
    string ProductSourceWarehouseName,
    int ReturnWarehouseNo,
    string ReturnWarehouseName,
    double CurrentStockQuantity,
    double ReturnableQuantity,
    string ProcurementType,
    bool HasPurchaseRequirement,
    bool IsReturnable,
    string Decision,
    IReadOnlyCollection<string> Warnings);

namespace FurpaMerkezApi.Application.Modules.RaporIslemleri.StokRaporlari;

public sealed record StockOnHandReportRequest(
    int WarehouseNo,
    DateTime ReportDate,
    string? Search,
    string? SupplierCode,
    string? CategoryCode,
    string? ProducerCode,
    string? ProductManagerCode,
    string? ModelCode,
    bool OnlyWithStock,
    int Take);

public sealed record ProductWarehouseStockRequest(
    int? WarehouseNo,
    DateTime ReportDate,
    string StockCodeOrBarcode,
    bool OnlyWithStock,
    int Take);

public sealed record StockCardDetailRequest(
    int? WarehouseNo,
    string? Barcode,
    string? StockCode,
    string? StockName,
    string? SupplierCode,
    string? ProductManagerCode,
    int Take);

public sealed record WarehouseMissingStockRequest(
    int SourceWarehouseNo,
    int TargetWarehouseNo,
    DateTime ReportDate,
    string? Search,
    string? ModelCode,
    int Take);

public sealed record WarehouseZeroStockRequest(
    int WarehouseNo,
    DateTime ReportDate,
    string? ModelCode,
    int Take);

public sealed record StockMovementReportRequest(
    int? WarehouseNo,
    DateTime StartDate,
    DateTime EndDate,
    string? StockCode,
    int Take);

public sealed record MovementInOutComparisonRequest(
    int? WarehouseNo,
    DateTime StartDate,
    DateTime EndDate,
    string? FilterType,
    string? FilterValue,
    int Take);

public sealed record BranchSalesReportRequest(
    int? WarehouseNo,
    DateTime StartDate,
    DateTime EndDate,
    string? FilterType,
    string? FilterValue,
    int Take);

public sealed record YearSalesComparisonRequest(
    int? WarehouseNo,
    DateTime StartDate,
    DateTime EndDate,
    string? FilterType,
    string? FilterValue,
    int Take);

public sealed record ReturnBranchReportRequest(
    int? WarehouseNo,
    DateTime StartDate,
    DateTime EndDate,
    string StockCode,
    int Take);

public sealed record NotSoldProductReportRequest(
    int? WarehouseNo,
    DateTime StartDate,
    DateTime EndDate,
    string? ProductManagerCode,
    bool IncludeDls,
    int Take);

public sealed record ProfitabilityReportRequest(
    int? WarehouseNo,
    DateTime StartDate,
    DateTime EndDate,
    string? Scope,
    string? FilterValue,
    int Take);

public sealed record CountingComparisonReportRequest(
    int WarehouseNo,
    DateTime CountDate,
    int? DocumentNo,
    string? PackageCode,
    int Take);

public sealed record StockOnHandReportDto(
    int WarehouseNo,
    string WarehouseName,
    DateTime ReportDate,
    int ReturnedCount,
    double TotalQuantity,
    double TotalSalesValue,
    IReadOnlyCollection<StockOnHandReportItemDto> Items);

public sealed record StockOnHandReportItemDto(
    int WarehouseNo,
    string WarehouseName,
    string StockCode,
    string StockName,
    string Barcode,
    string UnitName,
    double Quantity,
    double SalesPrice,
    double SalesValue,
    string SupplierCode,
    string SupplierName,
    string ProductManagerCode,
    string ProductManagerName,
    string CategoryCode,
    string RayonCode,
    string ProducerCode,
    string ModelCode,
    int? SalesBlockCode,
    int? OrderBlockCode,
    int? GoodsAcceptanceBlockCode,
    bool IsPassive);

public sealed record ProductWarehouseStockDto(
    int WarehouseNo,
    string WarehouseName,
    string StockCode,
    string StockName,
    string Barcode,
    string UnitName,
    double Quantity,
    double SalesPrice,
    double SalesValue,
    int? SalesBlockCode,
    int? OrderBlockCode,
    int? GoodsAcceptanceBlockCode,
    bool IsPassive);

public sealed record StockCardDetailDto(
    int? WarehouseNo,
    string WarehouseName,
    string StockCode,
    string StockName,
    string Barcode,
    string Unit1Name,
    double Unit1Multiplier,
    string Unit2Name,
    double Unit2Multiplier,
    string SupplierCode,
    string SupplierName,
    string ProductManagerCode,
    string ProductManagerName,
    string CategoryCode,
    string RayonCode,
    string ProducerCode,
    string ModelCode,
    string BrandCode,
    double SalesPrice,
    int? SalesBlockCode,
    int? OrderBlockCode,
    int? GoodsAcceptanceBlockCode,
    bool IsPassive,
    bool IsDeleted);

public sealed record WarehouseMissingStockDto(
    int SourceWarehouseNo,
    string SourceWarehouseName,
    int TargetWarehouseNo,
    string TargetWarehouseName,
    string StockCode,
    string StockName,
    string Barcode,
    string UnitName,
    double SourceQuantity,
    double TargetQuantity,
    double SalesPrice,
    string SupplierCode,
    string SupplierName,
    string ProductManagerCode,
    string ProductManagerName,
    string ModelCode);

public sealed record WarehouseZeroStockDto(
    int WarehouseNo,
    string WarehouseName,
    string StockCode,
    string StockName,
    string Barcode,
    string UnitName,
    double Quantity,
    double SalesPrice,
    string SupplierCode,
    string SupplierName,
    string ProductManagerCode,
    string ProductManagerName,
    string ModelCode);

public sealed record StockMovementReportItemDto(
    Guid MovementGuid,
    DateTime MovementDate,
    int? InputWarehouseNo,
    string InputWarehouseName,
    int? OutputWarehouseNo,
    string OutputWarehouseName,
    string StockCode,
    string StockName,
    string DocumentSerie,
    int DocumentOrderNo,
    string DocumentNo,
    int MovementType,
    int MovementKind,
    int DocumentType,
    int NormalReturn,
    double Quantity,
    double Amount,
    string CustomerCode,
    string Description);

public sealed record MovementInOutComparisonDto(
    string StockCode,
    string StockName,
    string Barcode,
    string SupplierCode,
    string SupplierName,
    string CategoryCode,
    string ProducerCode,
    string ProductManagerCode,
    string ProductManagerName,
    double PurchaseQuantity,
    double PurchaseAmount,
    double SalesQuantity,
    double SalesAmount,
    double ReturnQuantity,
    double ReturnAmount,
    double NetQuantity);

public sealed record BranchSalesReportItemDto(
    int WarehouseNo,
    string WarehouseName,
    string StockCode,
    string StockName,
    string Barcode,
    double Quantity,
    double Amount,
    double TaxAmount,
    double CurrentStock);

public sealed record YearSalesComparisonItemDto(
    string StockCode,
    string StockName,
    string Barcode,
    double CurrentQuantity,
    double CurrentAmount,
    double PreviousQuantity,
    double PreviousAmount,
    double QuantityDifference,
    double AmountDifference,
    double QuantityChangePercent,
    double AmountChangePercent);

public sealed record ReturnBranchReportItemDto(
    int WarehouseNo,
    string WarehouseName,
    string StockCode,
    string StockName,
    DateTime ReturnDate,
    string DocumentSerie,
    int DocumentOrderNo,
    string DocumentNo,
    double Quantity,
    double Amount,
    string CustomerCode);

public sealed record NotSoldProductReportItemDto(
    int? WarehouseNo,
    string WarehouseName,
    string StockCode,
    string StockName,
    string Barcode,
    string SupplierCode,
    string SupplierName,
    string ProductManagerCode,
    string ProductManagerName,
    double CurrentStock,
    DateTime? LastSaleDate);

public sealed record ProfitabilityReportItemDto(
    string GroupCode,
    string GroupName,
    double SalesQuantity,
    double SalesAmount,
    double CostAmount,
    double ProfitAmount,
    double ProfitPercent);

public sealed record CountingComparisonReportItemDto(
    int WarehouseNo,
    string WarehouseName,
    DateTime CountDate,
    int DocumentNo,
    string StockCode,
    string StockName,
    string Barcode,
    string UnitName,
    double CountQuantity,
    double SystemQuantity,
    double DifferenceQuantity,
    double SalesPrice,
    double DifferenceSalesValue);

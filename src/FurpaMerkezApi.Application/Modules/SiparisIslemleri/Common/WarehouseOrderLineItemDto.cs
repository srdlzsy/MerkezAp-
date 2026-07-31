namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;

public sealed record WarehouseOrderLineItemDto(
    Guid? LineGuid,
    int LineNo,
    string StockCode,
    string StockName,
    string UnitName,
    byte UnitPointer,
    double Quantity,
    double DeliveredQuantity,
    double RemainingQuantity,
    double UnitPrice,
    double LineAmount,
    bool IsClosed,
    string Description,
    string PackageCode,
    string ProjectCode,
    WarehouseOrderLineGreenGrocerCaseDto? GreenGrocerCase = null);

public sealed record WarehouseOrderLineGreenGrocerCaseDto(
    double InputQuantity,
    string InputMode,
    string ConversionMode,
    double EstimatedQuantity,
    string MicroUnit,
    double? AverageKgPerCase,
    double? UnitsPerCase,
    string AverageSource,
    int? AverageRecordCount,
    int? AverageCaseCount,
    double? CoefficientOfVariation,
    string Confidence,
    double? ActualShippedQuantity,
    double? ActualShippedCaseCount,
    string Status);

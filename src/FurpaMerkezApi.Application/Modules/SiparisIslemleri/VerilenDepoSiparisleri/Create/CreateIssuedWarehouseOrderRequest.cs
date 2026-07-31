namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Create;

public sealed record CreateIssuedWarehouseOrderRequest(
    int InWarehouseNo,
    int OutWarehouseNo,
    DateTime? OrderDate,
    DateTime? DeliveryDate,
    string? Description,
    IReadOnlyCollection<CreateIssuedWarehouseOrderLineRequest> Lines,
    Guid? CreatedByUserId = null);

public sealed record CreateIssuedWarehouseOrderLineRequest(
    string StockCode,
    double Quantity,
    double? RecommendedQuantity = null,
    double UnitPrice = 0d,
    int UnitPointer = 1,
    string? Description = null,
    string? PackageCode = null,
    string? ProjectCode = null,
    string? ResponsibilityCenter = null,
    GreenGrocerOrderLineSnapshotRequest? GreenGrocerCase = null);

public sealed record GreenGrocerOrderLineSnapshotRequest(
    double InputQuantity,
    string InputMode,
    string ConversionMode,
    string MicroUnit,
    double EstimatedQuantity,
    double? AverageKgPerCase,
    double? UnitsPerCase,
    string AverageSource,
    int? AverageRecordCount,
    int? AverageCaseCount,
    double? CoefficientOfVariation,
    string Confidence);

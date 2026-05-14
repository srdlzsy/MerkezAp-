namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Create;

public sealed record CreateIssuedWarehouseOrderRequest(
    int InWarehouseNo,
    int OutWarehouseNo,
    DateTime? OrderDate,
    DateTime? DeliveryDate,
    string? Description,
    IReadOnlyCollection<CreateIssuedWarehouseOrderLineRequest> Lines);

public sealed record CreateIssuedWarehouseOrderLineRequest(
    string StockCode,
    double Quantity,
    double? RecommendedQuantity = null,
    double UnitPrice = 0d,
    int UnitPointer = 1,
    string? Description = null,
    string? PackageCode = null,
    string? ProjectCode = null,
    string? ResponsibilityCenter = null);

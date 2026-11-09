namespace FurpaMerkezApi.Application.Modules.RaporIslemleri.MalKabulRaporlari;

public sealed record LazyBranchesWarehouseReceivingRequest(
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? Region,
    int? WarehouseNo);

public sealed record LazyBranchReceivingItemDto(
    string Region,
    int WarehouseNo,
    string WarehouseName,
    int NotAcceptedCount,
    DateTime? OldestShipmentDate,
    DateTime? LatestShipmentDate,
    double TotalQuantity);

public sealed record LazyBranchDocumentDetailDto(
    string Region,
    int WarehouseNo,
    string WarehouseName,
    string DocumentSerie,
    int DocumentOrderNo,
    string DocumentNo,
    DateTime DocumentDate,
    int LineCount,
    double TotalQuantity);

public sealed record CompanyDocumentByCustomerRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    int? WarehouseNo,
    int Take);

public sealed record CompanyDocumentByCustomerItemDto(
    string CustomerCode,
    string CustomerName,
    string CustomerTitle,
    int DocumentCount,
    double TotalAmount,
    double TotalTaxAmount,
    double TotalDiscountAmount,
    double NetAmount);

namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Create;

public sealed record CreateIssuedCompanyOrderRequest(
    int WarehouseNo,
    string CustomerCode,
    DateTime? OrderDate,
    DateTime DeliveryDate,
    string? Description1,
    string? Description2,
    string? Deliverer,
    string? Receiver,
    IReadOnlyCollection<CreateIssuedCompanyOrderLineRequest> Lines);

public sealed record CreateIssuedCompanyOrderLineRequest(
    string StockCode,
    double Quantity,
    double? RecommendedQuantity = null,
    double UnitPrice = 0d,
    int UnitPointer = 1,
    string? Description1 = null,
    string? Description2 = null,
    string? PackageCode = null,
    string? ProjectCode = null,
    string? CustomerResponsibilityCenter = null,
    string? ProductResponsibilityCenter = null);

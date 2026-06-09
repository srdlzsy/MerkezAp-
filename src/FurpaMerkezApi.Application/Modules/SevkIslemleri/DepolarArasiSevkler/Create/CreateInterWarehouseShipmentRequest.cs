namespace FurpaMerkezApi.Application.Modules.SevkIslemleri.DepolarArasiSevkler.Create;

public sealed record CreateInterWarehouseShipmentRequest(
    int SourceWarehouseNo,
    int TargetWarehouseNo,
    int TransitWarehouseNo,
    DateTime? MovementDate,
    DateTime? DocumentDate,
    string? DocumentNo,
    string? Description,
    IReadOnlyCollection<CreateInterWarehouseShipmentLineRequest> Lines,
    bool UpdateLinkedOrderDeliveredQuantities = false);

public sealed record CreateInterWarehouseShipmentLineRequest(
    string StockCode,
    double Quantity,
    Guid? WarehouseOrderLineGuid = null,
    double UnitPrice = 0d,
    int UnitPointer = 1,
    string? Description = null,
    string? PartyCode = null,
    int LotNo = 0,
    string? ProjectCode = null,
    string? CustomerResponsibilityCenter = null,
    string? ProductResponsibilityCenter = null);

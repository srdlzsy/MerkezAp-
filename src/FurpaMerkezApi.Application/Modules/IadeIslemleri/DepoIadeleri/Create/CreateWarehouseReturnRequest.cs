namespace FurpaMerkezApi.Application.Modules.IadeIslemleri.DepoIadeleri.Create;

public sealed record CreateWarehouseReturnRequest(
    int SourceWarehouseNo,
    int TargetWarehouseNo,
    int TransitWarehouseNo,
    DateTime? MovementDate,
    DateTime? DocumentDate,
    string? DocumentNo,
    string? Description,
    IReadOnlyCollection<CreateWarehouseReturnLineRequest> Lines);

public sealed record CreateWarehouseReturnLineRequest(
    string StockCode,
    double Quantity,
    double UnitPrice = 0d,
    int UnitPointer = 1,
    string? Description = null,
    string? PartyCode = null,
    int LotNo = 0,
    string? ProjectCode = null,
    string? CustomerResponsibilityCenter = null,
    string? ProductResponsibilityCenter = null);

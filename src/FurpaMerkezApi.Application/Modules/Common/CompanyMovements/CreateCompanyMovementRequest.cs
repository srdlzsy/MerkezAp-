namespace FurpaMerkezApi.Application.Modules.Common.CompanyMovements;

public sealed record CreateCompanyMovementRequest(
    int WarehouseNo,
    string CustomerCode,
    DateTime? MovementDate,
    DateTime? DocumentDate,
    string? DocumentNo,
    string? Description,
    IReadOnlyCollection<CreateCompanyMovementLineRequest> Lines);

public sealed record CreateCompanyMovementLineRequest(
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

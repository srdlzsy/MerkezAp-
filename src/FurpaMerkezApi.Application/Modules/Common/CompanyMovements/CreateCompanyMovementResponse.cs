namespace FurpaMerkezApi.Application.Modules.Common.CompanyMovements;

public sealed record CreateCompanyMovementResponse(
    string DocumentSerie,
    int DocumentOrderNo,
    DateTime MovementDate,
    DateTime DocumentDate,
    string DocumentNo,
    int WarehouseNo,
    string CustomerCode,
    int LineCount,
    double TotalQuantity,
    double TotalAmount,
    string WriteConnectionName);

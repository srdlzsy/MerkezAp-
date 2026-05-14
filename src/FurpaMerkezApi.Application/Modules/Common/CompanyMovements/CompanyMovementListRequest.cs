namespace FurpaMerkezApi.Application.Modules.Common.CompanyMovements;

public sealed record CompanyMovementListRequest(
    int WarehouseNo,
    DateTime StartDate,
    DateTime EndDate);

namespace FurpaMerkezApi.Application.Modules.Common.CompanyMovements;

public sealed record CompanyMovementDetailRequest(
    int WarehouseNo,
    string DocumentSerie,
    int DocumentOrderNo);

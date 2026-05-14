namespace FurpaMerkezApi.Application.Modules.IadeIslemleri.DepoIadeleri.Create;

public sealed record CreateWarehouseReturnResponse(
    string DocumentSerie,
    int DocumentOrderNo,
    DateTime MovementDate,
    DateTime DocumentDate,
    string DocumentNo,
    int SourceWarehouseNo,
    int TargetWarehouseNo,
    int TransitWarehouseNo,
    int LineCount,
    double TotalQuantity,
    double TotalAmount,
    string WriteConnectionName);

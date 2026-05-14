namespace FurpaMerkezApi.Application.Modules.SevkIslemleri.DepolarArasiSevkler.Create;

public sealed record CreateInterWarehouseShipmentResponse(
    string DocumentSerie,
    int DocumentOrderNo,
    DateTime MovementDate,
    DateTime DocumentDate,
    string DocumentNo,
    int SourceWarehouseNo,
    int TargetWarehouseNo,
    int TransitWarehouseNo,
    int LineCount,
    int LinkedWarehouseOrderLineCount,
    double TotalQuantity,
    double TotalAmount,
    string WriteConnectionName);

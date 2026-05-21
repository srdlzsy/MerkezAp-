namespace FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabulFarklari;

public sealed record WarehouseReceivingDifferenceDto(
    DateTime? DocumentDate,
    DateTime? MovementDate,
    string DocumentNo,
    string DocumentSerie,
    int DocumentOrderNo,
    int LineNo,
    Guid MovementGuid,
    bool IsReturn,
    int SourceWarehouseNo,
    string SourceWarehouse,
    int TargetWarehouseNo,
    string TargetWarehouse,
    string ProductCode,
    string ProductName,
    string UnitName,
    byte UnitPointer,
    double Quantity,
    double ReceivedQuantity,
    double DifferenceQuantity,
    string DifferenceType,
    string Description);

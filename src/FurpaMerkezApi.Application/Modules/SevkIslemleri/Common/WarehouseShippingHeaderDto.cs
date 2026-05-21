namespace FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;

public sealed record WarehouseShippingHeaderDto(
    DateTime? DocumentDate,
    DateTime? MovementDate,
    string DocumentNo,
    string DocumentSerie,
    int DocumentOrderNo,
    int SourceWarehouseNo,
    string SourceWarehouse,
    int TargetWarehouseNo,
    string TargetWarehouse,
    int ShippingWarehouseNo,
    byte ShippingState,
    bool IsReturn,
    string Plaque,
    string DriverNameSurname,
    string DriverTckn,
    string DescriptionEttn,
    string WarehouseOrderNo,
    IReadOnlyCollection<string> WarehouseOrderNos,
    int LineCount,
    double TotalQuantity,
    double TotalAmount);

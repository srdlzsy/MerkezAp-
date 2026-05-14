namespace FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;

public sealed record WarehouseShippingListItemDto(
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
    string Plaque,
    string DriverNameSurname,
    string DriverTckn,
    string DescriptionEttn,
    string WarehouseOrderNo,
    int LineCount,
    double TotalQuantity);

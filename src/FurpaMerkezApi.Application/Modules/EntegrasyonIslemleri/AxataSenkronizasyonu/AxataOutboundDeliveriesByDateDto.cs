namespace FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

public sealed record AxataOutboundDeliveriesByDateDto(
    DateTime Date,
    decimal AxataDateNumber,
    DateTime GeneratedAtUtc,
    int TotalDocumentCount,
    int TotalLineCount,
    double TotalQuantity,
    IReadOnlyCollection<AxataOutboundDeliveryByDateItemDto> Items);

public sealed record AxataOutboundDeliveryByDateItemDto(
    long AxataSequenceNo,
    string AxataDeliveryNo,
    string DocumentSerie,
    int? DocumentOrderNo,
    string Status,
    string? MovementType,
    string? SourceWarehouseCode,
    string? TargetWarehouseCode,
    DateTime? AxataDate,
    DateTime? TransferDate,
    int LineCount,
    double Quantity,
    string? VehiclePlate,
    string? DriverName);

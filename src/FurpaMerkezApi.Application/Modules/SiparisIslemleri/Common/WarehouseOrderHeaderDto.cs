namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;

public sealed record WarehouseOrderHeaderDto(
    string? DocumentKey,
    DateTime DocumentDate,
    DateTime? DeliveryDate,
    string DocumentSerie,
    int DocumentOrderNo,
    string DocumentNumber,
    int WarehouseNo,
    string WarehouseName,
    int RelatedWarehouseNo,
    string RelatedWarehouseName,
    int InWarehouseNo,
    string InWarehouseName,
    int OutWarehouseNo,
    string OutWarehouseName,
    int LineCount,
    double TotalQuantity,
    double TotalDeliveredQuantity,
    double TotalRemainingQuantity,
    double TotalAmount,
    bool IsClosed);

namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;

public sealed record WarehouseOrderListItemDto(
    string? DocumentKey,
    DateTime DocumentDate,
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
    double TotalAmount,
    DateTime? DeliveryDate);

namespace FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.Accept;

public sealed record AcceptWarehouseReceivingResponse(
    string DocumentSerie,
    int DocumentOrderNo,
    int WarehouseNo,
    int SourceWarehouseNo,
    int TransitWarehouseNo,
    byte ShippingState,
    int LineCount,
    double TotalShippedQuantity,
    double TotalReceivedQuantity,
    double TotalMissingQuantity,
    double TotalExcessQuantity,
    bool HasDiscrepancy,
    string DifferenceResolutionStatus,
    string WriteConnectionName,
    IReadOnlyCollection<AcceptWarehouseReceivingLineResultDto> Lines);

public sealed record AcceptWarehouseReceivingLineResultDto(
    Guid MovementGuid,
    int LineNo,
    string StockCode,
    double ShippedQuantity,
    double ReceivedQuantity,
    double DifferenceQuantity,
    string DifferenceType);

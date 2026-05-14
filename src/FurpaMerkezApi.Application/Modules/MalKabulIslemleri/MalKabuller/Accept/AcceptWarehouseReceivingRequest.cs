namespace FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.Accept;

public sealed record AcceptWarehouseReceivingRequest(
    int WarehouseNo,
    string DocumentSerie,
    int DocumentOrderNo,
    bool AllowDiscrepancy,
    IReadOnlyCollection<AcceptWarehouseReceivingLineRequest> Lines);

public sealed record AcceptWarehouseReceivingLineRequest(
    Guid MovementGuid,
    double ReceivedQuantity);

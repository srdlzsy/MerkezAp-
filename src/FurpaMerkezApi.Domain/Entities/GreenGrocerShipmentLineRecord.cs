namespace FurpaMerkezApi.Domain.Entities;

public sealed class GreenGrocerShipmentLineRecord
{
    private GreenGrocerShipmentLineRecord()
    {
        DocumentSerie = string.Empty;
    }

    public GreenGrocerShipmentLineRecord(
        Guid id,
        Guid clientRequestId,
        Guid orderLineSnapshotId,
        Guid warehouseOrderLineGuid,
        string documentSerie,
        int documentOrderNo,
        double actualQuantity,
        DateTime createdAtUtc,
        double? actualCaseCount = null)
    {
        if (clientRequestId == Guid.Empty)
        {
            throw new ArgumentException("Client request id is required.", nameof(clientRequestId));
        }

        if (orderLineSnapshotId == Guid.Empty || warehouseOrderLineGuid == Guid.Empty)
        {
            throw new ArgumentException("Green grocer order line reference is required.");
        }

        if (actualQuantity <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(actualQuantity));
        }

        if (actualCaseCount.HasValue && actualCaseCount.Value <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(actualCaseCount));
        }

        Id = id;
        ClientRequestId = clientRequestId;
        OrderLineSnapshotId = orderLineSnapshotId;
        WarehouseOrderLineGuid = warehouseOrderLineGuid;
        DocumentSerie = documentSerie.Trim();
        DocumentOrderNo = documentOrderNo;
        ActualQuantity = actualQuantity;
        ActualCaseCount = actualCaseCount;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid ClientRequestId { get; private set; }

    public Guid OrderLineSnapshotId { get; private set; }

    public Guid WarehouseOrderLineGuid { get; private set; }

    public string DocumentSerie { get; private set; }

    public int DocumentOrderNo { get; private set; }

    public double ActualQuantity { get; private set; }

    public double? ActualCaseCount { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
}

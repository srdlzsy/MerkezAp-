namespace FurpaMerkezApi.Domain.Entities;

public sealed class DocumentFlow
{
    private readonly List<DocumentFlowEvent> events = [];

    private DocumentFlow()
    {
        FlowKey = string.Empty;
        DocumentSerie = string.Empty;
    }

    public Guid Id { get; private set; }

    public string FlowKey { get; private set; }

    public DocumentFlowType DocumentType { get; private set; }

    public int SourceWarehouseNo { get; private set; }

    public int? TargetWarehouseNo { get; private set; }

    public string DocumentSerie { get; private set; }

    public int DocumentOrderNo { get; private set; }

    public string? DocumentNo { get; private set; }

    public string? ExternalDocumentNo { get; private set; }

    public string? ExternalUuid { get; private set; }

    public DocumentFlowStatus Status { get; private set; }

    public DocumentFlowStep CurrentStep { get; private set; }

    public string? LastError { get; private set; }

    public Guid? LastChangedByUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<DocumentFlowEvent> Events => events;

    public DocumentFlow(
        Guid id,
        string flowKey,
        DocumentFlowType documentType,
        int sourceWarehouseNo,
        int? targetWarehouseNo,
        string documentSerie,
        int documentOrderNo,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Document flow id can not be empty.", nameof(id));
        }

        Id = id;
        FlowKey = NormalizeRequired(flowKey, nameof(flowKey), 180);
        DocumentType = documentType;
        SourceWarehouseNo = ValidateWarehouseNo(sourceWarehouseNo, nameof(sourceWarehouseNo));
        TargetWarehouseNo = targetWarehouseNo.HasValue
            ? ValidateWarehouseNo(targetWarehouseNo.Value, nameof(targetWarehouseNo))
            : null;
        DocumentSerie = NormalizeRequired(documentSerie, nameof(documentSerie), 20);
        DocumentOrderNo = documentOrderNo;
        Status = DocumentFlowStatus.Succeeded;
        CurrentStep = DocumentFlowStep.DocumentCreated;
        CreatedAtUtc = NormalizeUtc(createdAtUtc);
        UpdatedAtUtc = CreatedAtUtc;
    }

    public void Record(
        DocumentFlowStep step,
        DocumentFlowStatus status,
        string message,
        string? error,
        Guid? changedByUserId,
        DateTime occurredAtUtc,
        string? documentNo = null,
        string? externalDocumentNo = null,
        string? externalUuid = null,
        int? targetWarehouseNo = null)
    {
        var occurredAt = NormalizeUtc(occurredAtUtc);

        if (targetWarehouseNo.HasValue)
        {
            TargetWarehouseNo = ValidateWarehouseNo(targetWarehouseNo.Value, nameof(targetWarehouseNo));
        }

        DocumentNo = NormalizeOptional(documentNo, 50) ?? DocumentNo;
        ExternalDocumentNo = NormalizeOptional(externalDocumentNo, 50) ?? ExternalDocumentNo;
        ExternalUuid = NormalizeOptional(externalUuid, 50) ?? ExternalUuid;
        Status = status;
        CurrentStep = step;
        LastError = status == DocumentFlowStatus.Failed
            ? NormalizeOptional(error, 2000) ?? NormalizeRequired(message, nameof(message), 500)
            : null;
        LastChangedByUserId = changedByUserId;
        UpdatedAtUtc = occurredAt;

        events.Add(new DocumentFlowEvent(
            Guid.NewGuid(),
            Id,
            step,
            status,
            message,
            error,
            changedByUserId,
            occurredAt));
    }

    private static int ValidateWarehouseNo(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Warehouse no can not be negative.");
        }

        return value;
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();

    private static string NormalizeRequired(string value, string parameterName, int maxLength)
    {
        var normalized = NormalizeOptional(value, maxLength);

        return normalized ?? throw new ArgumentException($"{parameterName} is required.", parameterName);
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}

public enum DocumentFlowType
{
    CompanyShipment = 1,
    InterWarehouseShipment = 2,
    CompanyReturn = 3,
    WarehouseReturn = 4,
    CompanyReceiving = 5,
    IssuedCompanyOrder = 6,
    IssuedWarehouseOrder = 7
}

public enum DocumentFlowStatus
{
    Succeeded = 1,
    Failed = 2
}

public enum DocumentFlowStep
{
    DocumentCreated = 1,
    EDespatchSubmission = 2,
    WarehouseReceivingAccepted = 3,
    OrderCreated = 4
}

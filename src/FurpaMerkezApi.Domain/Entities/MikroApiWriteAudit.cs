namespace FurpaMerkezApi.Domain.Entities;

public sealed class MikroApiWriteAudit
{
    private MikroApiWriteAudit()
    {
        Endpoint = string.Empty;
        PayloadHash = string.Empty;
        CorrelationId = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid RequestId { get; private set; }

    public Guid? DocumentFlowId { get; private set; }

    public string CorrelationId { get; private set; }

    public string Endpoint { get; private set; }

    public string PayloadHash { get; private set; }

    public MikroApiWriteAuditStatus Status { get; private set; }

    public int? HttpStatusCode { get; private set; }

    public int? MikroStatusCode { get; private set; }

    public string? Response { get; private set; }

    public string? Error { get; private set; }

    public int AttemptCount { get; private set; }

    public long? ElapsedMilliseconds { get; private set; }

    public string? RecoveredDocumentNo { get; private set; }

    public Guid? RecoveredGuid { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public DateTime? RecoveredAtUtc { get; private set; }

    public MikroApiWriteAudit(
        Guid id,
        Guid requestId,
        string correlationId,
        string endpoint,
        string payloadHash,
        DateTime createdAtUtc,
        Guid? documentFlowId = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Audit id can not be empty.", nameof(id));
        if (requestId == Guid.Empty) throw new ArgumentException("Request id can not be empty.", nameof(requestId));

        Id = id;
        RequestId = requestId;
        DocumentFlowId = documentFlowId;
        CorrelationId = NormalizeRequired(correlationId, nameof(correlationId), 128);
        Endpoint = NormalizeRequired(endpoint, nameof(endpoint), 500);
        PayloadHash = NormalizeRequired(payloadHash, nameof(payloadHash), 64);
        Status = MikroApiWriteAuditStatus.Pending;
        CreatedAtUtc = NormalizeUtc(createdAtUtc);
    }

    public void Complete(
        bool isError,
        bool isUnknown,
        int? httpStatusCode,
        int? mikroStatusCode,
        string? response,
        string? error,
        int attemptCount,
        long elapsedMilliseconds,
        DateTime completedAtUtc)
    {
        Status = isUnknown
            ? MikroApiWriteAuditStatus.Unknown
            : isError
                ? MikroApiWriteAuditStatus.Failed
                : MikroApiWriteAuditStatus.Succeeded;
        HttpStatusCode = httpStatusCode;
        MikroStatusCode = mikroStatusCode;
        Response = NormalizeOptional(response, 8000);
        Error = NormalizeOptional(error, 2000);
        AttemptCount = Math.Max(0, attemptCount);
        ElapsedMilliseconds = Math.Max(0, elapsedMilliseconds);
        CompletedAtUtc = NormalizeUtc(completedAtUtc);
    }

    public void MarkRecovered(
        string? documentNo,
        Guid? recoveredGuid,
        Guid? documentFlowId,
        DateTime recoveredAtUtc)
    {
        RecoveredDocumentNo = NormalizeOptional(documentNo, 100) ?? RecoveredDocumentNo;
        RecoveredGuid = recoveredGuid ?? RecoveredGuid;
        DocumentFlowId = documentFlowId ?? DocumentFlowId;
        Status = MikroApiWriteAuditStatus.Recovered;
        RecoveredAtUtc = NormalizeUtc(recoveredAtUtc);
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();

    private static string NormalizeRequired(string value, string parameterName, int maxLength) =>
        NormalizeOptional(value, maxLength)
        ?? throw new ArgumentException($"{parameterName} is required.", parameterName);

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}

public enum MikroApiWriteAuditStatus
{
    Pending = 1,
    Succeeded = 2,
    Failed = 3,
    Unknown = 4,
    Recovered = 5
}

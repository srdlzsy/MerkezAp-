namespace FurpaMerkezApi.Domain.Entities;

public sealed class MobileOfflineSyncRequest
{
    private MobileOfflineSyncRequest()
    {
        OperationCode = string.Empty;
        ClientRequestId = string.Empty;
        RequestFingerprint = string.Empty;
    }

    public Guid Id { get; private set; }

    public string OperationCode { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public int WarehouseNo { get; private set; }

    public string ClientRequestId { get; private set; }

    public string RequestFingerprint { get; private set; }

    public string? RequestPayload { get; private set; }

    public MobileOfflineSyncRequestStatus Status { get; private set; }

    public string? ResponsePayload { get; private set; }

    public string? ErrorMessage { get; private set; }

    public string? ErrorCode { get; private set; }

    public bool? Retryable { get; private set; }

    public Guid Revision { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public MobileOfflineSyncRequest(
        Guid id,
        string operationCode,
        Guid requestedByUserId,
        int warehouseNo,
        string clientRequestId,
        string requestFingerprint,
        string? requestPayload,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Offline sync request id can not be empty.", nameof(id));
        }

        if (requestedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Requested by user id can not be empty.", nameof(requestedByUserId));
        }

        if (warehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(warehouseNo));
        }

        Id = id;
        OperationCode = NormalizeRequired(operationCode, nameof(operationCode), 100);
        RequestedByUserId = requestedByUserId;
        WarehouseNo = warehouseNo;
        ClientRequestId = NormalizeRequired(clientRequestId, nameof(clientRequestId), 50).ToLowerInvariant();
        RequestFingerprint = NormalizeRequired(requestFingerprint, nameof(requestFingerprint), 64);
        RequestPayload = NormalizeOptional(requestPayload);
        Status = MobileOfflineSyncRequestStatus.Processing;
        CreatedAtUtc = NormalizeUtc(createdAtUtc);
        UpdatedAtUtc = CreatedAtUtc;
        Revision = Guid.NewGuid();
    }

    public void EnsureRequestFingerprintMatches(string requestFingerprint)
    {
        var normalized = NormalizeRequired(requestFingerprint, nameof(requestFingerprint), 64);

        if (!string.Equals(RequestFingerprint, normalized, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The same clientRequestId was already used with a different request payload.");
        }
    }

    public void RestartProcessing(string requestFingerprint, string? requestPayload, DateTime updatedAtUtc)
    {
        if (Retryable == false)
        {
            throw new InvalidOperationException("Manual review is required before restarting this request.");
        }

        RequestFingerprint = NormalizeRequired(requestFingerprint, nameof(requestFingerprint), 64);
        RequestPayload = NormalizeOptional(requestPayload);
        Status = MobileOfflineSyncRequestStatus.Processing;
        ResponsePayload = null;
        ErrorMessage = null;
        ErrorCode = null;
        Retryable = null;
        CompletedAtUtc = null;
        UpdatedAtUtc = NormalizeUtc(updatedAtUtc);
        Revision = Guid.NewGuid();
    }

    public void MarkCompleted(string responsePayload, DateTime completedAtUtc)
    {
        if (Retryable == false)
        {
            throw new InvalidOperationException("A request awaiting manual review cannot be completed automatically.");
        }

        ResponsePayload = NormalizeRequired(responsePayload, nameof(responsePayload), int.MaxValue);
        ErrorMessage = null;
        ErrorCode = null;
        Retryable = null;
        Status = MobileOfflineSyncRequestStatus.Completed;
        CompletedAtUtc = NormalizeUtc(completedAtUtc);
        UpdatedAtUtc = CompletedAtUtc;
        Revision = Guid.NewGuid();
    }

    public void MarkFailed(string errorMessage, DateTime completedAtUtc, string? errorCode = null, bool? retryable = null)
    {
        if (Status == MobileOfflineSyncRequestStatus.Completed || Retryable == false)
        {
            return;
        }

        ErrorMessage = NormalizeRequired(errorMessage, nameof(errorMessage), 1000);
        ErrorCode = NormalizeOptional(errorCode);
        Retryable = retryable;
        ResponsePayload = null;
        Status = MobileOfflineSyncRequestStatus.Failed;
        CompletedAtUtc = NormalizeUtc(completedAtUtc);
        UpdatedAtUtc = CompletedAtUtc;
        Revision = Guid.NewGuid();
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static string NormalizeRequired(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"{parameterName} can not exceed {maxLength} characters.", parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public enum MobileOfflineSyncRequestStatus
{
    Processing = 1,
    Completed = 2,
    Failed = 3
}

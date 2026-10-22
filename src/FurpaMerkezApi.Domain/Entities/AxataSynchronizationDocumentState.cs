namespace FurpaMerkezApi.Domain.Entities;

public sealed class AxataSynchronizationDocumentState
{
    private AxataSynchronizationDocumentState()
    {
        TaskCode = string.Empty;
        DocumentKey = string.Empty;
        LastError = string.Empty;
    }

    public AxataSynchronizationDocumentState(
        Guid id,
        string taskCode,
        string documentKey,
        string error,
        DateTime failedAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("AXATA document state id can not be empty.", nameof(id));
        }

        Id = id;
        TaskCode = NormalizeRequired(taskCode, nameof(taskCode), 100);
        DocumentKey = NormalizeRequired(documentKey, nameof(documentKey), 120);
        FirstFailedAtUtc = NormalizeUtc(failedAtUtc);
        RegisterFailure(error, failedAtUtc);
    }

    public Guid Id { get; private set; }

    public string TaskCode { get; private set; }

    public string DocumentKey { get; private set; }

    public int FailureCount { get; private set; }

    public string LastError { get; private set; } = string.Empty;

    public DateTime FirstFailedAtUtc { get; private set; }

    public DateTime LastFailedAtUtc { get; private set; }

    public DateTime NextAttemptAtUtc { get; private set; }

    public void RegisterFailure(string error, DateTime failedAtUtc)
    {
        var failedAt = NormalizeUtc(failedAtUtc);
        FailureCount++;
        LastError = NormalizeRequired(error, nameof(error), 2000);
        LastFailedAtUtc = failedAt;
        NextAttemptAtUtc = failedAt.Add(GetRetryDelay(FailureCount));
    }

    private static TimeSpan GetRetryDelay(int failureCount) => failureCount switch
    {
        1 => TimeSpan.FromMinutes(15),
        2 => TimeSpan.FromHours(1),
        _ => TimeSpan.FromHours(6)
    };

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();

    private static string NormalizeRequired(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}

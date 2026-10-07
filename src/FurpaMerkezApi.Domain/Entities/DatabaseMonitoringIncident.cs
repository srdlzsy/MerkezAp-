namespace FurpaMerkezApi.Domain.Entities;

public sealed class DatabaseMonitoringIncident
{
    private DatabaseMonitoringIncident()
    {
        Fingerprint = string.Empty;
        Type = string.Empty;
        Severity = string.Empty;
        Recommendation = string.Empty;
    }

    public Guid Id { get; private set; }
    public string Fingerprint { get; private set; }
    public string Type { get; private set; }
    public string Severity { get; private set; }
    public int SessionId { get; private set; }
    public int? BlockingSessionId { get; private set; }
    public string? DatabaseName { get; private set; }
    public string? LoginName { get; private set; }
    public string? HostName { get; private set; }
    public string? ProgramName { get; private set; }
    public string? WaitType { get; private set; }
    public long ElapsedMilliseconds { get; private set; }
    public string? SqlText { get; private set; }
    public string Recommendation { get; private set; }
    public DateTime FirstSeenAtUtc { get; private set; }
    public DateTime LastSeenAtUtc { get; private set; }
    public int OccurrenceCount { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }

    public DatabaseMonitoringIncident(
        Guid id,
        string fingerprint,
        string type,
        string severity,
        int sessionId,
        int? blockingSessionId,
        string? databaseName,
        string? loginName,
        string? hostName,
        string? programName,
        string? waitType,
        long elapsedMilliseconds,
        string? sqlText,
        string recommendation,
        DateTime observedAtUtc)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Incident id is required.", nameof(id)) : id;
        Fingerprint = Required(fingerprint, 64);
        Type = Required(type, 40);
        Severity = Required(severity, 20);
        SessionId = sessionId;
        BlockingSessionId = blockingSessionId;
        DatabaseName = Optional(databaseName, 128);
        LoginName = Optional(loginName, 128);
        HostName = Optional(hostName, 128);
        ProgramName = Optional(programName, 256);
        WaitType = Optional(waitType, 120);
        ElapsedMilliseconds = Math.Max(0, elapsedMilliseconds);
        SqlText = Optional(sqlText, 4000);
        Recommendation = Required(recommendation, 1000);
        FirstSeenAtUtc = Utc(observedAtUtc);
        LastSeenAtUtc = Utc(observedAtUtc);
        OccurrenceCount = 1;
    }

    public void Observe(
        string severity,
        int? blockingSessionId,
        string? waitType,
        long elapsedMilliseconds,
        string? sqlText,
        string recommendation,
        DateTime observedAtUtc)
    {
        Severity = Required(severity, 20);
        BlockingSessionId = blockingSessionId;
        WaitType = Optional(waitType, 120);
        ElapsedMilliseconds = Math.Max(0, elapsedMilliseconds);
        SqlText = Optional(sqlText, 4000);
        Recommendation = Required(recommendation, 1000);
        LastSeenAtUtc = Utc(observedAtUtc);
        OccurrenceCount++;
        ResolvedAtUtc = null;
    }

    public void Resolve(DateTime resolvedAtUtc) => ResolvedAtUtc = Utc(resolvedAtUtc);

    private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    private static string Required(string value, int length) => Optional(value, length) ?? throw new ArgumentException("Value is required.");
    private static string? Optional(string? value, int length)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        return normalized.Length <= length ? normalized : normalized[..length];
    }
}

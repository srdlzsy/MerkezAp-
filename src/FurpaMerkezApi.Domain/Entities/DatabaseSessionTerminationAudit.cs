namespace FurpaMerkezApi.Domain.Entities;

public sealed class DatabaseSessionTerminationAudit
{
    private DatabaseSessionTerminationAudit()
    {
        Reason = string.Empty;
    }

    public Guid Id { get; private set; }
    public int SessionId { get; private set; }
    public DateTime LoginTime { get; private set; }
    public int? HostProcessId { get; private set; }
    public string? LoginName { get; private set; }
    public string? HostName { get; private set; }
    public string? ProgramName { get; private set; }
    public string? DatabaseName { get; private set; }
    public string? SqlText { get; private set; }
    public string Reason { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public DateTime RequestedAtUtc { get; private set; }
    public bool IsSucceeded { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? Error { get; private set; }

    public DatabaseSessionTerminationAudit(
        Guid id,
        int sessionId,
        DateTime loginTime,
        int? hostProcessId,
        string? loginName,
        string? hostName,
        string? programName,
        string? databaseName,
        string? sqlText,
        string reason,
        Guid requestedByUserId,
        DateTime requestedAtUtc)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Audit id is required.", nameof(id)) : id;
        SessionId = sessionId;
        LoginTime = loginTime;
        HostProcessId = hostProcessId;
        LoginName = Optional(loginName, 128);
        HostName = Optional(hostName, 128);
        ProgramName = Optional(programName, 256);
        DatabaseName = Optional(databaseName, 128);
        SqlText = Optional(sqlText, 4000);
        Reason = Optional(reason, 500) ?? throw new ArgumentException("Termination reason is required.", nameof(reason));
        RequestedByUserId = requestedByUserId == Guid.Empty
            ? throw new ArgumentException("User id is required.", nameof(requestedByUserId))
            : requestedByUserId;
        RequestedAtUtc = requestedAtUtc.Kind == DateTimeKind.Utc ? requestedAtUtc : requestedAtUtc.ToUniversalTime();
    }

    public void Complete(bool succeeded, string? error, DateTime completedAtUtc)
    {
        IsSucceeded = succeeded;
        Error = Optional(error, 2000);
        CompletedAtUtc = completedAtUtc.Kind == DateTimeKind.Utc ? completedAtUtc : completedAtUtc.ToUniversalTime();
    }

    private static string? Optional(string? value, int length)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        return normalized.Length <= length ? normalized : normalized[..length];
    }
}

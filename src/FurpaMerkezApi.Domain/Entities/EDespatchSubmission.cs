namespace FurpaMerkezApi.Domain.Entities;

public sealed class EDespatchSubmission
{
    private EDespatchSubmission() { }

    public EDespatchSubmission(string documentKey, string documentNo, string uuid, string payloadJson, DateTime now)
    {
        Id = Guid.NewGuid();
        DocumentKey = documentKey;
        DocumentNo = documentNo;
        Uuid = uuid;
        PayloadJson = payloadJson;
        CreatedAtUtc = now;
        NextAttemptAtUtc = now.AddMinutes(2);
        Status = EDespatchSubmissionStatus.Unknown;
    }

    public Guid Id { get; private set; }
    public string DocumentKey { get; private set; } = string.Empty;
    public string DocumentNo { get; private set; } = string.Empty;
    public string Uuid { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public EDespatchSubmissionStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime NextAttemptAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? LastError { get; private set; }

    public void ConfirmSubmission(DateTime now)
    {
        Status = EDespatchSubmissionStatus.PendingMetadata;
        NextAttemptAtUtc = now;
        LastError = null;
    }

    public void Complete(DateTime now)
    {
        Status = EDespatchSubmissionStatus.Completed;
        CompletedAtUtc = now;
        LastError = null;
    }

    public void ScheduleRetry(string error, DateTime now)
    {
        AttemptCount++;
        LastError = error[..Math.Min(error.Length, 2000)];
        NextAttemptAtUtc = now.AddSeconds(Math.Min(900, 5 * Math.Pow(2, Math.Min(AttemptCount, 8))));
    }

    public void RequireReview(string error)
    {
        Status = EDespatchSubmissionStatus.NeedsReview;
        LastError = error[..Math.Min(error.Length, 2000)];
    }

    public void ReopenMetadataReview(DateTime now)
    {
        if (Status != EDespatchSubmissionStatus.NeedsReview)
            throw new InvalidOperationException("Only e-despatch submissions awaiting review can be reopened.");

        Status = EDespatchSubmissionStatus.PendingMetadata;
        AttemptCount++;
        NextAttemptAtUtc = now;
        LastError = null;
    }
}

public enum EDespatchSubmissionStatus
{
    Unknown = 1,
    PendingMetadata = 2,
    Completed = 3,
    NeedsReview = 4
}

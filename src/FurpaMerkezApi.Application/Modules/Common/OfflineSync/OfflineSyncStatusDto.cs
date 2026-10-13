namespace FurpaMerkezApi.Application.Modules.Common.OfflineSync;

public sealed record OfflineSyncStatusDto<TResponse>(
    Guid ClientRequestId,
    string OperationCode,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    string? ErrorMessage,
    string? ErrorCode,
    bool? Retryable,
    int? RetryAfterSeconds,
    TResponse? Result);

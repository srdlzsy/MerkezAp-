namespace FurpaMerkezApi.Application.Modules.Common.OfflineSync;

public sealed record OfflineSyncStatusDto<TResponse>(
    Guid ClientRequestId,
    string OperationCode,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    string? ErrorMessage,
    TResponse? Result);

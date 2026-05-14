namespace FurpaMerkezApi.Application.Modules.OperasyonIslemleri.Operations;

public sealed record OperationJobDetailDto(
    Guid JobId,
    string Operation,
    string Status,
    int WarehouseNo,
    Guid RequestedByUserId,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    string? Message,
    string? ErrorMessage,
    IReadOnlyCollection<GeneratedOperationFileDto> Files);

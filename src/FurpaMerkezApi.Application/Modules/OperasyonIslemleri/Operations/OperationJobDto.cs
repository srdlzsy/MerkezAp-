namespace FurpaMerkezApi.Application.Modules.OperasyonIslemleri.Operations;

public sealed record OperationJobDto(
    Guid JobId,
    string Operation,
    string Status,
    int WarehouseNo,
    DateTime CreatedAtUtc);

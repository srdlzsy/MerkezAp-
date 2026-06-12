namespace FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

public sealed record AxataSynchronizationManualDocumentRequest(
    string TaskCode,
    int? WarehouseNo,
    string? DocumentSerie,
    int? DocumentOrderNo,
    int? DocumentNo,
    DateTime? DocumentDate);

public sealed record AxataSynchronizationManualDocumentCandidatesRequest(
    string TaskCode,
    int? WarehouseNo,
    DateTime? StartDate,
    DateTime? EndDate,
    int? Skip,
    int? Take);

public sealed record AxataSynchronizationManualDocumentExecuteRequest(
    string TaskCode,
    string ExecutionMode,
    int? WarehouseNo,
    string? DocumentSerie,
    int? DocumentOrderNo,
    int? DocumentNo,
    DateTime? DocumentDate);

public sealed record AxataSynchronizationManualDocumentBatchRequest(
    string TaskCode,
    int? WarehouseNo,
    IReadOnlyCollection<AxataSynchronizationManualDocumentRequestItem> Documents,
    bool ContinueOnError);

public sealed record AxataSynchronizationManualDocumentBatchExecuteRequest(
    string TaskCode,
    string ExecutionMode,
    int? WarehouseNo,
    IReadOnlyCollection<AxataSynchronizationManualDocumentRequestItem> Documents,
    bool ContinueOnError);

public sealed record AxataSynchronizationManualDocumentRequestItem(
    string? DocumentSerie,
    int? DocumentOrderNo,
    int? DocumentNo,
    DateTime? DocumentDate);

public sealed record AxataSynchronizationManualDocumentDto(
    string TaskCode,
    string TaskName,
    string Flow,
    string ExecutionMode,
    int? WarehouseNo,
    string DocumentReference,
    DateTime GeneratedAtUtc,
    int AffectedRecordCount,
    string PayloadJson,
    IReadOnlyCollection<string> Notes,
    IReadOnlyCollection<AxataSynchronizationJobArtifactDto> Artifacts);

public sealed record AxataSynchronizationManualDocumentCandidatesDto(
    string TaskCode,
    string TaskName,
    string Flow,
    int? WarehouseNo,
    DateTime StartDate,
    DateTime EndDate,
    int TotalRecordCount,
    int SkippedRecordCount,
    int ReturnedRecordCount,
    DateTime GeneratedAtUtc,
    IReadOnlyCollection<AxataSynchronizationManualDocumentCandidateItemDto> Items,
    IReadOnlyCollection<string> Notes);

public sealed record AxataSynchronizationManualDocumentCandidateItemDto(
    string DocumentReference,
    string Summary,
    string? DocumentSerie,
    int? DocumentOrderNo,
    int? DocumentNo,
    DateTime? DocumentDate,
    string? DocumentIdentifier,
    int LineCount,
    double TotalQuantity);

public sealed record AxataSynchronizationManualDocumentBatchDto(
    string TaskCode,
    string TaskName,
    string Flow,
    string ExecutionMode,
    int? WarehouseNo,
    DateTime GeneratedAtUtc,
    int RequestedDocumentCount,
    int SucceededDocumentCount,
    int FailedDocumentCount,
    IReadOnlyCollection<AxataSynchronizationManualDocumentDto> Documents,
    IReadOnlyCollection<AxataSynchronizationManualDocumentBatchFailureDto> Failures,
    IReadOnlyCollection<string> Notes);

public sealed record AxataSynchronizationManualDocumentBatchFailureDto(
    string DocumentReference,
    string ErrorMessage);

public sealed record AxataSynchronizationManualDispatchDto(
    string TaskCode,
    string TaskName,
    string Flow,
    int? WarehouseNo,
    string DocumentReference,
    string OperationName,
    string EndpointUrl,
    DateTime DispatchedAtUtc,
    bool IsSuccess,
    int? ServiceState,
    string ServiceMessage,
    string PayloadJson,
    string RequestXml,
    string ResponseXml,
    IReadOnlyCollection<string> Notes);

public sealed record AxataSynchronizationManualDispatchBatchDto(
    string TaskCode,
    string TaskName,
    string Flow,
    int? WarehouseNo,
    DateTime DispatchedAtUtc,
    int RequestedDocumentCount,
    int SucceededDocumentCount,
    int FailedDocumentCount,
    IReadOnlyCollection<AxataSynchronizationManualDispatchDto> Documents,
    IReadOnlyCollection<AxataSynchronizationManualDocumentBatchFailureDto> Failures,
    IReadOnlyCollection<string> Notes);

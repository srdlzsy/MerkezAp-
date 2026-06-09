namespace FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

public interface IAxataOutboundDeliveryImportService
{
    Task<AxataOutboundDeliveryImportPreviewDto> PreviewC01Async(
        AxataOutboundDeliveryImportPreviewRequest request,
        CancellationToken cancellationToken);

    Task<AxataOutboundDeliveryImportExecuteDto> ExecuteC01Async(
        AxataOutboundDeliveryImportExecuteRequest request,
        Guid requestedByUserId,
        CancellationToken cancellationToken);
}

public sealed record AxataOutboundDeliveryImportPreviewRequest(
    int? Take);

public sealed record AxataOutboundDeliveryImportExecuteRequest(
    int? Take,
    bool ContinueOnError,
    bool Acknowledge);

public sealed record AxataOutboundDeliveryImportPreviewDto(
    string MovementType,
    string PendingStatus,
    DateTime GeneratedAtUtc,
    int TotalFetchedDocumentCount,
    int ReturnedDocumentCount,
    int TotalLineCount,
    double TotalQuantity,
    IReadOnlyCollection<AxataOutboundDeliveryImportDocumentDto> Documents,
    IReadOnlyCollection<string> Notes);

public sealed record AxataOutboundDeliveryImportExecuteDto(
    string MovementType,
    string PendingStatus,
    DateTime GeneratedAtUtc,
    int RequestedDocumentCount,
    int SucceededDocumentCount,
    int FailedDocumentCount,
    int SkippedDocumentCount,
    int CreatedMovementLineCount,
    double CreatedMovementQuantity,
    IReadOnlyCollection<AxataOutboundDeliveryImportResultDto> Results,
    IReadOnlyCollection<AxataOutboundDeliveryImportFailureDto> Failures,
    IReadOnlyCollection<string> Notes);

public sealed record AxataOutboundDeliveryImportDocumentDto(
    long AxataSequenceNo,
    string AxataDeliveryNo,
    string DocumentSerie,
    int DocumentOrderNo,
    string MovementType,
    string Status,
    int SourceWarehouseNo,
    int TargetWarehouseNo,
    DateTime? AxataDate,
    int AxataLineCount,
    double AxataQuantity,
    int MikroOrderLineCount,
    double MikroOrderQuantity,
    double MikroDeliveredQuantity,
    int ExistingLinkedMovementLineCount,
    bool CanImport,
    string? Warning);

public sealed record AxataOutboundDeliveryImportResultDto(
    long AxataSequenceNo,
    string AxataDeliveryNo,
    string DocumentSerie,
    int DocumentOrderNo,
    string MovementSerie,
    int MovementOrderNo,
    int CreatedMovementLineCount,
    double CreatedMovementQuantity,
    bool Acknowledged,
    string Message);

public sealed record AxataOutboundDeliveryImportFailureDto(
    long? AxataSequenceNo,
    string? AxataDeliveryNo,
    string ErrorMessage);

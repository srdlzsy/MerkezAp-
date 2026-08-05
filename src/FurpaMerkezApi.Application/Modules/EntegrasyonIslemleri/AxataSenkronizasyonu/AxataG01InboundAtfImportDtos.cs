namespace FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

public interface IAxataG01InboundAtfImportService
{
    Task<AxataG01InboundAtfPreviewDto> PreviewAsync(
        AxataG01InboundAtfPreviewRequest request,
        CancellationToken cancellationToken);

    Task<AxataG01InboundAtfExecuteDto> ExecuteAsync(
        AxataG01InboundAtfExecuteRequest request,
        Guid requestedByUserId,
        CancellationToken cancellationToken);
}

public sealed record AxataG01InboundAtfPreviewRequest(
    int? Take);

public sealed record AxataG01InboundAtfExecuteRequest(
    int? Take,
    bool ContinueOnError,
    bool Acknowledge);

public sealed record AxataG01InboundAtfPreviewDto(
    string MovementType,
    string PendingStatus,
    DateTime GeneratedAtUtc,
    int TotalFetchedLineCount,
    int ReturnedDocumentCount,
    int ImportableDocumentCount,
    int TotalLineCount,
    double TotalQuantity,
    IReadOnlyCollection<AxataG01InboundAtfDocumentDto> Documents,
    IReadOnlyCollection<string> Notes);

public sealed record AxataG01InboundAtfExecuteDto(
    string MovementType,
    string PendingStatus,
    DateTime GeneratedAtUtc,
    int RequestedDocumentCount,
    int SucceededDocumentCount,
    int FailedDocumentCount,
    int SkippedDocumentCount,
    int CreatedMovementLineCount,
    double CreatedMovementQuantity,
    IReadOnlyCollection<AxataG01InboundAtfResultDto> Results,
    IReadOnlyCollection<AxataG01InboundAtfFailureDto> Failures,
    IReadOnlyCollection<string> Notes);

public sealed record AxataG01InboundAtfDocumentDto(
    string OrderDocumentNo,
    string DocumentSerie,
    int DocumentOrderNo,
    string CustomerCode,
    string DespatchNo,
    int WarehouseNo,
    int AxataLineCount,
    double AxataQuantity,
    int MikroOrderLineCount,
    double MikroOrderQuantity,
    double MikroDeliveredQuantity,
    int ExistingMovementLineCount,
    bool CanImport,
    string? Warning);

public sealed record AxataG01InboundAtfResultDto(
    string OrderDocumentNo,
    string MovementSerie,
    int MovementOrderNo,
    int CreatedMovementLineCount,
    double CreatedMovementQuantity,
    bool Acknowledged,
    string Message);

public sealed record AxataG01InboundAtfFailureDto(
    string? OrderDocumentNo,
    string ErrorMessage);

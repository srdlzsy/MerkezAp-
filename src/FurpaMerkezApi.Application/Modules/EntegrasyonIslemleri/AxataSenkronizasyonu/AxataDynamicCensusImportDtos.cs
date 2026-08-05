namespace FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

public interface IAxataDynamicCensusImportService
{
    Task<AxataDynamicCensusPreviewDto> PreviewAsync(
        AxataDynamicCensusPreviewRequest request,
        CancellationToken cancellationToken);

    Task<AxataDynamicCensusExecuteDto> ExecuteAsync(
        AxataDynamicCensusExecuteRequest request,
        Guid requestedByUserId,
        CancellationToken cancellationToken);
}

public sealed record AxataDynamicCensusPreviewRequest(
    int? Take);

public sealed record AxataDynamicCensusExecuteRequest(
    int? Take,
    bool ContinueOnError,
    bool Acknowledge);

public sealed record AxataDynamicCensusPreviewDto(
    string ViewName,
    string PendingStatus,
    DateTime GeneratedAtUtc,
    int TotalFetchedLineCount,
    int ReturnedLineCount,
    int ImportableLineCount,
    int ExistingMovementLineCount,
    double TotalQuantity,
    IReadOnlyCollection<AxataDynamicCensusLineDto> Lines,
    IReadOnlyCollection<string> Notes);

public sealed record AxataDynamicCensusExecuteDto(
    string ViewName,
    string PendingStatus,
    DateTime GeneratedAtUtc,
    int RequestedLineCount,
    int SucceededLineCount,
    int FailedLineCount,
    int SkippedLineCount,
    int CreatedDocumentCount,
    int CreatedMovementLineCount,
    double CreatedMovementQuantity,
    IReadOnlyCollection<AxataDynamicCensusResultDto> Results,
    IReadOnlyCollection<AxataDynamicCensusFailureDto> Failures,
    IReadOnlyCollection<string> Notes);

public sealed record AxataDynamicCensusLineDto(
    string RowNo,
    string StockCode,
    double Quantity,
    string AxataStockType,
    byte MovementType,
    byte MovementGenre,
    byte DocumentType,
    string DocumentSerie,
    int InputWarehouseNo,
    int OutputWarehouseNo,
    bool CanImport,
    bool ExistingMovementExists,
    string? Warning);

public sealed record AxataDynamicCensusResultDto(
    string RowNo,
    string StockCode,
    string MovementSerie,
    int MovementOrderNo,
    int MovementLineNo,
    double Quantity,
    bool Acknowledged,
    string Message);

public sealed record AxataDynamicCensusFailureDto(
    string? RowNo,
    string? StockCode,
    string ErrorMessage);

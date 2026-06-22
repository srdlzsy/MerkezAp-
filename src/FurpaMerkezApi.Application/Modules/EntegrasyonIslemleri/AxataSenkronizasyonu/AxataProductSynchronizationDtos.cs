namespace FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

public interface IAxataProductSynchronizationService
{
    Task<AxataProductSynchronizationPreviewDto> PreviewAsync(
        string? productCode,
        int? take,
        CancellationToken cancellationToken);

    Task<AxataProductSynchronizationExecuteDto> DispatchAsync(
        AxataProductSynchronizationDispatchRequest request,
        CancellationToken cancellationToken);
}

public sealed record AxataProductSynchronizationDispatchRequest(
    IReadOnlyCollection<string> ProductCodes,
    int? Take,
    bool ContinueOnError);

public sealed record AxataProductSynchronizationPreviewDto(
    DateTime GeneratedAtUtc,
    string? ProductCode,
    int TotalRecordCount,
    int ReturnedRecordCount,
    IReadOnlyCollection<AxataProductSynchronizationItemDto> Products,
    IReadOnlyCollection<string> Notes);

public sealed record AxataProductSynchronizationItemDto(
    string ProductCode,
    string ProductName,
    string MainUnit,
    int BarcodeCount,
    IReadOnlyCollection<string> Barcodes,
    int UnitCount,
    string PayloadJson);

public sealed record AxataProductSynchronizationExecuteDto(
    DateTime DispatchedAtUtc,
    string OperationName,
    string EndpointUrl,
    int RequestedProductCount,
    int SucceededProductCount,
    int FailedProductCount,
    IReadOnlyCollection<AxataProductSynchronizationResultDto> Results,
    IReadOnlyCollection<string> Notes);

public sealed record AxataProductSynchronizationResultDto(
    string ProductCode,
    bool IsSuccess,
    int? ServiceState,
    string ServiceMessage,
    int BarcodeCount,
    int UnitCount);

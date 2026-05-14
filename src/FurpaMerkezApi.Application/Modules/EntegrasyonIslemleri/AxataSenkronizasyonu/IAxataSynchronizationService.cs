namespace FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

public interface IAxataSynchronizationService
{
    Task<AxataSynchronizationOverviewDto> GetOverviewAsync(CancellationToken cancellationToken);

    Task<AxataSynchronizationFetchProfilesOverviewDto> GetFetchProfilesAsync(CancellationToken cancellationToken);

    Task<AxataSynchronizationPreviewDto> PreviewAsync(
        AxataSynchronizationPreviewRequest request,
        int defaultWarehouseNo,
        CancellationToken cancellationToken);

    Task<AxataSynchronizationJobDto> QueueAsync(
        AxataSynchronizationExecuteRequest request,
        Guid requestedByUserId,
        int defaultWarehouseNo,
        CancellationToken cancellationToken);

    Task<AxataSynchronizationJobDetailDto> GetJobAsync(Guid jobId, CancellationToken cancellationToken);

    Task<AxataSynchronizationConnectionTestDto> TestConnectionsAsync(CancellationToken cancellationToken);

    Task<AxataSynchronizationManualDocumentCandidatesDto> ListDocumentCandidatesAsync(
        AxataSynchronizationManualDocumentCandidatesRequest request,
        int defaultWarehouseNo,
        CancellationToken cancellationToken);

    Task<AxataSynchronizationManualDocumentDto> PreviewDocumentAsync(
        AxataSynchronizationManualDocumentRequest request,
        int defaultWarehouseNo,
        CancellationToken cancellationToken);

    Task<AxataSynchronizationManualDocumentDto> ExecuteDocumentAsync(
        AxataSynchronizationManualDocumentExecuteRequest request,
        Guid requestedByUserId,
        int defaultWarehouseNo,
        CancellationToken cancellationToken);

    Task<AxataSynchronizationManualDocumentBatchDto> PreviewDocumentsAsync(
        AxataSynchronizationManualDocumentBatchRequest request,
        int defaultWarehouseNo,
        CancellationToken cancellationToken);

    Task<AxataSynchronizationManualDocumentBatchDto> ExecuteDocumentsAsync(
        AxataSynchronizationManualDocumentBatchExecuteRequest request,
        Guid requestedByUserId,
        int defaultWarehouseNo,
        CancellationToken cancellationToken);

    Task<AxataSynchronizationManualDispatchDto> DispatchDocumentLiveAsync(
        AxataSynchronizationManualDocumentRequest request,
        int defaultWarehouseNo,
        CancellationToken cancellationToken);

    Task<AxataSynchronizationManualDispatchBatchDto> DispatchDocumentsLiveAsync(
        AxataSynchronizationManualDocumentBatchRequest request,
        int defaultWarehouseNo,
        CancellationToken cancellationToken);
}

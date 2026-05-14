namespace FurpaMerkezApi.Application.Modules.OperasyonIslemleri.Operations;

public interface IOperationsService
{
    Task<OperationJobDto> QueueScalesFileAsync(
        int warehouseNo,
        Guid requestedByUserId,
        CancellationToken cancellationToken);

    Task<OperationJobDto> QueueProductBarcodePluNoFileAsync(
        int warehouseNo,
        Guid requestedByUserId,
        CancellationToken cancellationToken);

    Task<OperationJobDto> QueueCashierFileAsync(
        int warehouseNo,
        Guid requestedByUserId,
        CancellationToken cancellationToken);

    Task<OperationJobDto> QueuePromoFileAsync(
        int warehouseNo,
        Guid requestedByUserId,
        CancellationToken cancellationToken);

    Task<OperationJobDetailDto> GetJobAsync(Guid jobId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AuthorizationFileDto>> GetAuthorizationFilesAsync(CancellationToken cancellationToken);

    Task SaveAuthorizationFilesAsync(
        IReadOnlyCollection<SaveAuthorizationFileItemRequest> fileList,
        CancellationToken cancellationToken);
}

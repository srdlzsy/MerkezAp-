using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.Operations;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.Operations;

internal sealed class OperationsService(
    FurpaDbContext furpaDbContext,
    OperationsJobQueue jobQueue)
    : IOperationsService
{
    public Task<OperationJobDto> QueueScalesFileAsync(
        int warehouseNo,
        Guid requestedByUserId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Enqueue(OperationFileKind.ScalesFile, warehouseNo, requestedByUserId));

    public Task<OperationJobDto> QueueProductBarcodePluNoFileAsync(
        int warehouseNo,
        Guid requestedByUserId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Enqueue(OperationFileKind.ProductBarcodePluNoFile, warehouseNo, requestedByUserId));

    public Task<OperationJobDto> QueueCashierFileAsync(
        int warehouseNo,
        Guid requestedByUserId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Enqueue(OperationFileKind.CashierFile, warehouseNo, requestedByUserId));

    public Task<OperationJobDto> QueuePromoFileAsync(
        int warehouseNo,
        Guid requestedByUserId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Enqueue(OperationFileKind.PromoFile, warehouseNo, requestedByUserId));

    public Task<OperationJobDetailDto> GetJobAsync(Guid jobId, CancellationToken cancellationToken) =>
        Task.FromResult(jobQueue.Get(jobId));

    public async Task<IReadOnlyCollection<AuthorizationFileDto>> GetAuthorizationFilesAsync(CancellationToken cancellationToken) =>
        await furpaDbContext.AuthorizationFiles
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .Select(item => new AuthorizationFileDto
            {
                Id = item.Id,
                UpdateDate = item.UpdateDate,
                Name = item.Name,
                Z = item.Z,
                R = item.R,
                X = item.X
            })
            .ToArrayAsync(cancellationToken);

    public async Task SaveAuthorizationFilesAsync(
        IReadOnlyCollection<SaveAuthorizationFileItemRequest> fileList,
        CancellationToken cancellationToken)
    {
        if (fileList.Count == 0)
        {
            throw new ArgumentException("Authorization file list cannot be empty.", nameof(fileList));
        }

        var duplicateIds = fileList
            .GroupBy(item => item.Id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicateIds.Length > 0)
        {
            throw new ArgumentException(
                $"Authorization file list contains duplicate ids: {string.Join(", ", duplicateIds)}.",
                nameof(fileList));
        }

        var fileIds = fileList.Select(item => item.Id).ToArray();
        var existingFiles = await furpaDbContext.AuthorizationFiles
            .Where(item => fileIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var missingIds = fileIds
            .Except(existingFiles.Keys)
            .ToArray();

        if (missingIds.Length > 0)
        {
            throw new KeyNotFoundException(
                $"Authorization file records were not found for ids: {string.Join(", ", missingIds)}.");
        }

        var updatedAt = DateTime.Now;

        foreach (var request in fileList)
        {
            var entity = existingFiles[request.Id];
            entity.Name = request.Name.Trim();
            entity.Z = request.Z;
            entity.R = request.R;
            entity.X = request.X;
            entity.UpdateDate = updatedAt;
        }

        await furpaDbContext.SaveChangesAsync(cancellationToken);
    }

    private OperationJobDto Enqueue(OperationFileKind kind, int warehouseNo, Guid requestedByUserId)
    {
        ValidatePositive(warehouseNo, nameof(warehouseNo));

        if (requestedByUserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Current user id was not found.");
        }

        return jobQueue.Enqueue(kind, warehouseNo, requestedByUserId);
    }

    private static void ValidatePositive(int value, string paramName)
    {
        if (value <= 0)
        {
            throw new ArgumentException("Value must be greater than zero.", paramName);
        }
    }
}

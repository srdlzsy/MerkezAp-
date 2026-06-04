using System.Data;
using FurpaMerkezApi.Application.Modules.GreenGrocer.Reports;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.GreenGrocer.Reports;

public sealed class DeleteGreenGrocerOrderUseCase(MikroWriteDbContext mikroWriteDbContext)
    : IDeleteGreenGrocerOrderUseCase
{
    private const double DeleteWindowHours = 24d;

    public async Task<DeleteGreenGrocerOrderResponse> ExecuteAsync(
        DeleteGreenGrocerOrderRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            try
            {
                var documentSerie = request.DocumentSerie.Trim();
                var query = mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs
                    .Where(order =>
                        order.ssip_evrakno_seri == documentSerie &&
                        order.ssip_evrakno_sira == request.DocumentOrderNo);

                if (request.WarehouseNo.HasValue)
                {
                    query = query.Where(order => order.ssip_girdepo == request.WarehouseNo.Value);
                }

                var lines = await query.ToListAsync(cancellationToken);

                if (lines.Count == 0)
                {
                    throw new KeyNotFoundException("Green grocer order was not found.");
                }

                var now = DateTime.Now;
                var latestCreateDate = lines.Max(line => line.ssip_create_date);
                var elapsedHours = (now - latestCreateDate).TotalHours;

                if (elapsedHours >= DeleteWindowHours)
                {
                    throw new InvalidOperationException(
                        "Green grocer order can only be deleted within 24 hours after creation.");
                }

                mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs.RemoveRange(lines);
                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new DeleteGreenGrocerOrderResponse(
                    documentSerie,
                    request.DocumentOrderNo,
                    request.WarehouseNo,
                    lines.Count,
                    latestCreateDate,
                    now);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private static void Validate(DeleteGreenGrocerOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DocumentSerie))
        {
            throw new ArgumentException("Document serie is required.", nameof(request.DocumentSerie));
        }

        if (request.DocumentOrderNo < 0)
        {
            throw new ArgumentException("Document order no can not be negative.", nameof(request.DocumentOrderNo));
        }

        if (request.WarehouseNo is <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }
    }
}

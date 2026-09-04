using System.Data;
using System.Text.Json;
using FurpaMerkezApi.Application.Modules.GreenGrocer.Reports;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using FurpaMerkezApi.Infrastructure.Services.MikroApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.GreenGrocer.Reports;

public sealed class DeleteGreenGrocerOrderUseCase(
    MikroWriteDbContext mikroWriteDbContext,
    IOptionsMonitor<MikroWriteRoutingOptions> mikroWriteRoutingOptions,
    MikroApiClient mikroApiClient)
    : IDeleteGreenGrocerOrderUseCase
{
    private const double DeleteWindowHours = 24d;
    private const string DepolarArasiSiparisGuidSilPath = "/Api/apiMethods/DepolarArasiSiparisGuidSilV2";

    public async Task<DeleteGreenGrocerOrderResponse> ExecuteAsync(
        DeleteGreenGrocerOrderRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        return mikroWriteRoutingOptions.CurrentValue.GreenGrocerOrderDelete switch
        {
            MikroWriteMode.Database => await ExecuteDatabaseAsync(request, cancellationToken),
            MikroWriteMode.MikroApi => await ExecuteMikroApiAsync(request, cancellationToken),
            MikroWriteMode.DualShadow => await ExecuteDatabaseAsync(request, cancellationToken),
            var mode => throw new InvalidOperationException(
                $"Unsupported MikroWriteRouting:GreenGrocerOrderDelete mode '{mode}'.")
        };
    }

    private async Task<DeleteGreenGrocerOrderResponse> ExecuteDatabaseAsync(
        DeleteGreenGrocerOrderRequest request,
        CancellationToken cancellationToken)
    {
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

    private async Task<DeleteGreenGrocerOrderResponse> ExecuteMikroApiAsync(
        DeleteGreenGrocerOrderRequest request,
        CancellationToken cancellationToken)
    {
        var documentSerie = request.DocumentSerie.Trim();
        var lines = await QueryLinesAsync(documentSerie, request, asNoTracking: true, cancellationToken);
        EnsureDeletable(lines);

        var now = DateTime.Now;
        var latestCreateDate = lines.Max(line => line.ssip_create_date);
        var payload = new
        {
            evraklar = new[]
            {
                new
                {
                    satirlar = lines
                        .Select(line => new { line.ssip_Guid })
                        .ToArray()
                }
            }
        };

        var result = await mikroApiClient.PostWithMikroPayloadAsync<JsonElement>(
            DepolarArasiSiparisGuidSilPath,
            payload,
            cancellationToken);

        if (result.IsError)
        {
            throw new InvalidOperationException(
                result.ErrorMessage ?? "Mikro API green grocer order delete failed.");
        }

        return new DeleteGreenGrocerOrderResponse(
            documentSerie,
            request.DocumentOrderNo,
            request.WarehouseNo,
            lines.Count,
            latestCreateDate,
            now);
    }

    private async Task<List<DEPOLAR_ARASI_SIPARISLER>> QueryLinesAsync(
        string documentSerie,
        DeleteGreenGrocerOrderRequest request,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        var query = mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs
            .Where(order =>
                order.ssip_evrakno_seri == documentSerie &&
                order.ssip_evrakno_sira == request.DocumentOrderNo);

        if (request.WarehouseNo.HasValue)
        {
            query = query.Where(order => order.ssip_girdepo == request.WarehouseNo.Value);
        }

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.ToListAsync(cancellationToken);
    }

    private static void EnsureDeletable(IReadOnlyCollection<DEPOLAR_ARASI_SIPARISLER> lines)
    {
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

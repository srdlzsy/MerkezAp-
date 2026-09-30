using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FurpaMerkezApi.Infrastructure.Services;

internal sealed record EDespatchMetadataUpdateWorkItem(
    SendEDespatchRequest Request,
    string EDespatchDocumentNo,
    string EDespatchUuid,
    EDespatchMovementSnapshot[] Movements);

internal sealed record EDespatchMovementSnapshot(
    Guid Id, int? RowNo, string? StockCode, double? Quantity, byte? Unit,
    double? Amount, int? SourceWarehouse, int? TargetWarehouse, int? TransitWarehouse,
    byte? Type, byte? Kind, byte? ReturnType, byte? DocumentType,
    DateTime? Date, string? Customer, string? Trace, bool? Cancelled)
{
    private const double NumericComparisonTolerance = 0.000001d;

    internal static EDespatchMovementSnapshot From(STOK_HAREKETLERI row) => new(
        row.sth_Guid, row.sth_satirno, row.sth_stok_kod, row.sth_miktar, row.sth_birim_pntr,
        row.sth_tutar, row.sth_cikis_depo_no, row.sth_giris_depo_no, row.sth_nakliyedeposu,
        row.sth_tip, row.sth_cins, row.sth_normal_iade, row.sth_evraktip,
        row.sth_tarih, row.sth_cari_kodu, row.sth_eticaret_kanal_kodu, row.sth_iptal);

    internal static EDespatchMovementSnapshot From(STOK_HAREKETLERI row, EDespatchDocumentType documentType) =>
        NormalizeForDocumentType(From(row), documentType);

    internal static bool Matches(
        IEnumerable<EDespatchMovementSnapshot> expected,
        IEnumerable<STOK_HAREKETLERI> current,
        EDespatchDocumentType documentType) =>
        expected.Select(x => NormalizeForDocumentType(x, documentType)).OrderBy(x => x.Id)
            .Zip(current.Select(x => From(x, documentType)).OrderBy(x => x.Id), IsEquivalent)
            .All(x => x) &&
        expected.Count() == current.Count();

    private static EDespatchMovementSnapshot NormalizeForDocumentType(
        EDespatchMovementSnapshot snapshot,
        EDespatchDocumentType documentType) =>
        // Mikro records the technical incoming warehouse for purchase returns as 1.
        // Older snapshots can contain 0 before that normalization; neither value changes the submitted return.
        documentType == EDespatchDocumentType.CompanyReturn && snapshot.TargetWarehouse == 0
            ? snapshot with { TargetWarehouse = 1 }
            : snapshot;

    private static bool IsEquivalent(EDespatchMovementSnapshot expected, EDespatchMovementSnapshot actual) =>
        expected with { Quantity = null, Amount = null } == actual with { Quantity = null, Amount = null } &&
        IsEquivalentNumber(expected.Quantity, actual.Quantity) &&
        IsEquivalentNumber(expected.Amount, actual.Amount);

    private static bool IsEquivalentNumber(double? expected, double? actual) =>
        expected.HasValue == actual.HasValue &&
        (!expected.HasValue || Math.Abs(expected.Value - actual!.Value) <= NumericComparisonTolerance);
}

internal sealed class EDespatchMetadataConflictException(string message) : InvalidOperationException(message);

internal interface IEDespatchMetadataUpdateProcessor
{
    Task ProcessPendingAsync(Guid id, CancellationToken cancellationToken);
    Task ImportLegacyAsync(Guid flowId, CancellationToken cancellationToken);
}

// The database is the queue. Polling resumes after restarts without an age or total-count cutoff.
internal sealed class EDespatchMetadataUpdateWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<EDespatchMetadataUpdateWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
                var now = DateTime.UtcNow;
                var ids = await db.EDespatchSubmissions.AsNoTracking()
                    .Where(x =>
                        ((x.Status == EDespatchSubmissionStatus.Unknown ||
                          x.Status == EDespatchSubmissionStatus.PendingMetadata) && x.NextAttemptAtUtc <= now) ||
                        (x.Status == EDespatchSubmissionStatus.NeedsReview &&
                         x.AttemptCount == 0 &&
                         x.DocumentKey.StartsWith("CompanyReturn:")))
                    .OrderBy(x => x.NextAttemptAtUtc).ThenBy(x => x.Id)
                    .Select(x => x.Id).Take(25).ToArrayAsync(stoppingToken);

                foreach (var id in ids)
                {
                    try
                    {
                        await using var itemScope = scopeFactory.CreateAsyncScope();
                        await itemScope.ServiceProvider.GetRequiredService<IEDespatchMetadataUpdateProcessor>()
                            .ProcessPendingAsync(id, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "E-despatch durable work could not be processed. SubmissionId={SubmissionId}", id);
                    }
                }

                // Gradually adopt work accepted by the old in-memory queue before deployment.
                var legacyCutoff = now.AddDays(-2);
                var legacyId = await db.DocumentFlows.AsNoTracking()
                    .Where(x => x.ExternalDocumentNo != null && x.ExternalUuid != null && x.UpdatedAtUtc >= legacyCutoff &&
                        (x.DocumentType == DocumentFlowType.InterWarehouseShipment || x.DocumentType == DocumentFlowType.WarehouseReturn ||
                         x.DocumentType == DocumentFlowType.CompanyShipment || x.DocumentType == DocumentFlowType.CompanyReturn) &&
                        !db.EDespatchSubmissions.Any(s => s.DocumentKey == x.FlowKey))
                    .OrderByDescending(x => x.UpdatedAtUtc).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(stoppingToken);
                if (legacyId.HasValue)
                {
                    await using var legacyScope = scopeFactory.CreateAsyncScope();
                    await legacyScope.ServiceProvider.GetRequiredService<IEDespatchMetadataUpdateProcessor>()
                        .ImportLegacyAsync(legacyId.Value, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogError(exception, "E-despatch pending work could not be read; polling will retry.");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}

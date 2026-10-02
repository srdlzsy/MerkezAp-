using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Application.Modules.IadeIslemleri.DepoIadeleri.Create;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.DepolarArasiSevkler.Create;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.DepolarArasiSevkler.Create;
using FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using FurpaMerkezApi.Infrastructure.Services.MikroApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UyumsoftDespatch = FurpaMerkezApi.Infrastructure.Services.ServiceReferences.Uyumsoft.Despatch;

namespace FurpaMerkezApi.Infrastructure.Services;

public sealed class EDespatchService(
    AuthDbContext authDbContext,
    MikroDbContext mikroDbContext,
    MikroWriteDbContext mikroWriteDbContext,
    IDocumentFlowService documentFlowService,
    IOptions<EDespatchOptions> options,
    IOptionsMonitor<MikroWriteRoutingOptions> mikroWriteRoutingOptions,
    MikroApiClient mikroApiClient,
    ILogger<EDespatchService> logger)
    : IEDespatchService, IEDespatchMetadataUpdateProcessor
{
    private const string DespatchNamespace = "urn:oasis:names:specification:ubl:schema:xsd:DespatchAdvice-2";
    private const string AggregateNamespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private const string BasicNamespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private const short MikroUserNo = 39;
    private const byte CompanyDispatchDocumentType = 1;
    private const byte ReceivingReceiptDocumentType = 13;
    private const byte OutgoingMovementType = 1;
    private const byte IncomingMovementType = 0;
    private const byte NormalMovement = 0;
    private const byte ReturnMovement = 1;
    private const byte InterWarehouseShipmentDocumentType = 17;
    private const byte InterWarehouseMovementType = 2;
    private const byte InterWarehouseMovementGenre = 6;
    private const string CommonEDespatchDocumentPrefix = "FRM";
    private const string DocumentNumberLockResource = "FurpaMerkezApi:EDespatchDocumentNumber";
    private const int DocumentNumberLockTimeoutMilliseconds = 120_000;
    private const int DocumentNumberAvailabilityAttemptCount = 100;
    private const int PostSubmissionCompletionTimeoutSeconds = 120;
    private const int LocalMetadataUpdateAttemptCount = 2;
    private const string StockMovementUpdatePath = "/Api/apiMethods/DahiliStokHareketDuzeltV2";
    private const string InterWarehouseCreateOperationCode = "sevk-islemleri.giden-depolar-arasi-sevkler.create";
    private const string WarehouseReturnCreateOperationCode = "iade-islemleri.giden-depo-iadeleri.create";
    private static readonly SemaphoreSlim LocalDocumentNumberLock = new(1, 1);

    public async Task<SendEDespatchResponse> SendAsync(
        SendEDespatchRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        request = await ResolveDriverAsync(request, cancellationToken);
        Validate(request);

        var config = options.Value;
        ValidateConfiguration(config);

        var requestTimer = Stopwatch.StartNew();
        try
        {
            await using var documentLock = await EDespatchDocumentLock.TryAcquireAsync(
                mikroWriteDbContext.Database.GetConnectionString()!, SubmissionKey(request), cancellationToken)
                ?? throw new InvalidOperationException("This e-despatch is already being processed. Refresh its status; do not submit again.");
            var existing = await authDbContext.EDespatchSubmissions.AsNoTracking()
                .SingleOrDefaultAsync(x => x.DocumentKey == SubmissionKey(request), cancellationToken);
            if (existing is not null)
            {
                if (existing.Status == EDespatchSubmissionStatus.Unknown)
                    throw new InvalidOperationException("Uyumsoft submission result is being verified. No new e-despatch was sent.");
                return SubmissionResponse(existing);
            }
            var response = request.DocumentType switch
            {
                EDespatchDocumentType.OutgoingCompanyShipment => await SendCompanyMovementAsync(
                    request,
                    CompanyMovementKind.OutgoingShipment,
                    cancellationToken),

                EDespatchDocumentType.CompanyReturn => await SendCompanyMovementAsync(
                    request,
                    CompanyMovementKind.PurchaseReturn,
                    cancellationToken),

                EDespatchDocumentType.InterWarehouseShipment => await SendInterWarehouseDocumentAsync(
                    request,
                    false,
                    cancellationToken),

                EDespatchDocumentType.WarehouseReturn => await SendInterWarehouseDocumentAsync(
                    request,
                    true,
                    cancellationToken),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(request.DocumentType),
                    request.DocumentType,
                    "Unsupported e-despatch document type.")
            };

            return response;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "E-despatch request did not complete. Document={Document}", SubmissionKey(request));
            // A rejected duplicate or uncertain network response must not overwrite a confirmed send.
            await RecordKnownFailureAsync(request, exception);
            throw;
        }
        finally
        {
            logger.LogInformation("E-despatch HTTP processing finished. Document={Document}, ElapsedMs={ElapsedMs}",
                SubmissionKey(request), requestTimer.ElapsedMilliseconds);
        }
    }

    private async Task RecordKnownFailureAsync(SendEDespatchRequest request, Exception error)
    {
        try
        {
            authDbContext.ChangeTracker.Clear();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await using var lease = await EDespatchDocumentLock.TryAcquireAsync(
                mikroWriteDbContext.Database.GetConnectionString()!, SubmissionKey(request), timeout.Token);
            if (lease is null || await authDbContext.EDespatchSubmissions.AsNoTracking()
                    .AnyAsync(x => x.DocumentKey == SubmissionKey(request), timeout.Token) ||
                await GetTrackedSubmittedDespatchAsync(request, timeout.Token) is not null) return;
            await RecordEDespatchFlowAsync(request, DocumentFlowStatus.Failed,
                "E-irsaliye gonderimi tamamlanamadi.", error.Message, null, timeout.Token);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "E-despatch failure timeline could not be recorded.");
        }
    }

    async Task IEDespatchMetadataUpdateProcessor.ImportLegacyAsync(Guid flowId, CancellationToken cancellationToken)
    {
        var flow = await authDbContext.DocumentFlows.AsNoTracking().SingleAsync(x => x.Id == flowId, cancellationToken);
        var type = flow.DocumentType switch
        {
            DocumentFlowType.InterWarehouseShipment => EDespatchDocumentType.InterWarehouseShipment,
            DocumentFlowType.WarehouseReturn => EDespatchDocumentType.WarehouseReturn,
            DocumentFlowType.CompanyShipment => EDespatchDocumentType.OutgoingCompanyShipment,
            DocumentFlowType.CompanyReturn => EDespatchDocumentType.CompanyReturn,
            _ => throw new InvalidOperationException("Unsupported legacy e-despatch flow.")
        };
        var request = new SendEDespatchRequest(type, flow.SourceWarehouseNo, flow.DocumentSerie,
            flow.DocumentOrderNo, string.Empty, string.Empty, string.Empty);
        await using var lease = await EDespatchDocumentLock.TryAcquireAsync(
            mikroWriteDbContext.Database.GetConnectionString()!, SubmissionKey(request), cancellationToken);
        if (lease is null || await authDbContext.EDespatchSubmissions.AnyAsync(x => x.DocumentKey == flow.FlowKey, cancellationToken)) return;

        IReadOnlyCollection<STOK_HAREKETLERI> rows;
        if (type is EDespatchDocumentType.InterWarehouseShipment or EDespatchDocumentType.WarehouseReturn)
        {
            var detailRequest = new WarehouseShippingDetailRequest(
                request.WarehouseNo,
                request.DocumentSerie,
                request.DocumentOrderNo);
            try
            {
                rows = await LoadInterWarehouseRowsAsync(
                    mikroDbContext,
                    detailRequest,
                    type == EDespatchDocumentType.WarehouseReturn,
                    cancellationToken);
            }
            catch (KeyNotFoundException)
            {
                rows = await LoadInterWarehouseRowsAsync(
                    mikroWriteDbContext,
                    detailRequest,
                    type == EDespatchDocumentType.WarehouseReturn,
                    cancellationToken);
            }
        }
        else
        {
            var detailRequest = new CompanyMovementDetailRequest(
                request.WarehouseNo,
                request.DocumentSerie,
                request.DocumentOrderNo);
            var movementKind = type == EDespatchDocumentType.CompanyReturn
                ? CompanyMovementKind.PurchaseReturn
                : CompanyMovementKind.OutgoingShipment;
            try
            {
                rows = await LoadCompanyMovementRowsAsync(
                    mikroDbContext,
                    detailRequest,
                    movementKind,
                    cancellationToken);
            }
            catch (KeyNotFoundException)
            {
                rows = await LoadCompanyMovementRowsAsync(
                    mikroWriteDbContext,
                    detailRequest,
                    movementKind,
                    cancellationToken);
            }
        }
        var contacts = ResolveDespatchContacts(request, NormalizeText(rows.Select(x => x.sth_HareketGrupKodu2).ToArray()),
            NormalizeText(rows.Select(x => x.sth_HareketGrupKodu3).ToArray()));
        var work = new EDespatchMetadataUpdateWorkItem(
            request with
            {
                Deliverer = contacts.Deliverer, Receiver = contacts.Receiver,
                Plaque = NormalizeText(rows.Select(x => x.sth_HareketGrupKodu1).ToArray()),
                DriverTckn = NormalizeText(rows.Select(x => x.sth_ismerkezi_kodu).ToArray())
            }, flow.ExternalDocumentNo!, flow.ExternalUuid!, rows.Select(x => EDespatchMovementSnapshot.From(x, type)).ToArray());
        authDbContext.EDespatchSubmissions.Add(new EDespatchSubmission(flow.FlowKey, work.EDespatchDocumentNo,
            work.EDespatchUuid, JsonSerializer.Serialize(work), DateTime.UtcNow));
        await authDbContext.SaveChangesAsync(cancellationToken);
    }

    private static string SubmissionKey(SendEDespatchRequest request) => DocumentFlowKeys.Create(
        ToDocumentFlowType(request.DocumentType), request.WarehouseNo, request.DocumentSerie.Trim(), request.DocumentOrderNo);

    internal static void EnsureSnapshotMatches(
        IReadOnlyCollection<EDespatchMovementSnapshot> snapshot,
        IReadOnlyCollection<STOK_HAREKETLERI> current,
        EDespatchDocumentType documentType = EDespatchDocumentType.InterWarehouseShipment)
    {
        if (snapshot.Count == 0 || !EDespatchMovementSnapshot.Matches(snapshot, current, documentType))
            throw new EDespatchMetadataConflictException(
                "Mikro document content differs from the submitted e-despatch. No metadata was overwritten; manual review is required.");
    }

    private SendEDespatchResponse SubmissionResponse(EDespatchSubmission submission)
    {
        var work = JsonSerializer.Deserialize<EDespatchMetadataUpdateWorkItem>(submission.PayloadJson)
            ?? throw new InvalidOperationException("Stored e-despatch work is invalid.");
        var complete = submission.Status == EDespatchSubmissionStatus.Completed;
        return new SendEDespatchResponse(work.Request.DocumentType, work.Request.DocumentSerie,
            work.Request.DocumentOrderNo, submission.DocumentNo, submission.Uuid, string.Empty, submission.DocumentNo,
            submission.CreatedAtUtc, options.Value.EndpointUrl, complete,
            complete ? null : submission.Status == EDespatchSubmissionStatus.NeedsReview
                ? "Uyumsoft submission succeeded; Mikro metadata requires manual review. Do not resend."
                : BuildQueuedMikroMetadataWarning(),
            submission.Status == EDespatchSubmissionStatus.PendingMetadata);
    }

    private async Task<SendEDespatchResponse> SendDurablyAsync(
        SendEDespatchRequest request, MikroDbContext context, IReadOnlyCollection<STOK_HAREKETLERI> movements,
        ResolvedDespatchContacts contacts, Func<string, string, UyumsoftDespatch.DespatchInfo> build,
        CancellationToken cancellationToken)
    {
        EnsureExpectedLineCountMatches(request, movements.Count);

        EDespatchSubmission submission;
        UyumsoftDespatch.DespatchInfo despatch;
        var timer = Stopwatch.StartNew();
        await using (var numberLock = await AcquireDocumentNumberLockAsync(cancellationToken))
        {
            var number = await BuildEDespatchDocumentNoAsync(DateTime.Now.Year, options.Value, cancellationToken);
            var uuid = Guid.NewGuid().ToString();
            despatch = build(number, uuid);
            await EnsureDocumentMovementSetUnchangedAsync(request, context, movements, cancellationToken);
            var work = new EDespatchMetadataUpdateWorkItem(
                request with { Deliverer = contacts.Deliverer, Receiver = contacts.Receiver }, number, uuid,
                movements.Select(x => EDespatchMovementSnapshot.From(x, request.DocumentType)).ToArray());
            EnsureUyumsoftLinesMatch(despatch.DespatchAdvice.DespatchLine, work.Movements);
            submission = new EDespatchSubmission(SubmissionKey(request), number, uuid,
                JsonSerializer.Serialize(work), DateTime.UtcNow);
            authDbContext.EDespatchSubmissions.Add(submission);
            // Persist identity before the network call. An interrupted request can only be reconciled, never blindly resent.
            await authDbContext.SaveChangesAsync(cancellationToken);
        }
        logger.LogInformation("E-despatch reservation completed. Document={Document}, ElapsedMs={ElapsedMs}",
            submission.DocumentKey, timer.ElapsedMilliseconds);
        timer.Restart();
        var serviceResult = await SendToUyumsoftAsync(despatch, options.Value, cancellationToken);
        logger.LogInformation("Uyumsoft submission completed. Document={Document}, ElapsedMs={ElapsedMs}",
            submission.DocumentKey, timer.ElapsedMilliseconds);
        using var completion = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await ConfirmSubmissionAsync(submission, completion.Token);
        return SubmissionResponse(submission) with
        {
            ServiceDocumentId = serviceResult.ServiceDocumentId,
            ServiceDocumentNumber = serviceResult.ServiceDocumentNumber
        };
    }

    private async Task ConfirmSubmissionAsync(EDespatchSubmission submission, CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        var work = JsonSerializer.Deserialize<EDespatchMetadataUpdateWorkItem>(submission.PayloadJson)!;
        var request = work.Request;
        var target = work.Movements.FirstOrDefault()?.TransitWarehouse;
        var flow = await authDbContext.DocumentFlows.SingleOrDefaultAsync(x => x.FlowKey == submission.DocumentKey, cancellationToken);
        if (flow is null)
        {
            flow = new DocumentFlow(Guid.NewGuid(), submission.DocumentKey, ToDocumentFlowType(request.DocumentType),
                request.WarehouseNo, target > 0 ? target : null, request.DocumentSerie, request.DocumentOrderNo, DateTime.UtcNow);
            authDbContext.DocumentFlows.Add(flow);
        }
        var flowEvent = flow.Record(DocumentFlowStep.EDespatchSubmission, DocumentFlowStatus.Succeeded,
            "E-irsaliye Uyumsoft'a gonderildi; Mikro isaretleme kalici olarak kaydedildi.", null, null, DateTime.UtcNow,
            externalDocumentNo: submission.DocumentNo, externalUuid: submission.Uuid);
        authDbContext.DocumentFlowEvents.Add(flowEvent);
        submission.ConfirmSubmission(DateTime.UtcNow);
        // One Auth transaction persists both confirmation and the pending metadata work; exceptions are not swallowed.
        await authDbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("E-despatch confirmation persisted. Document={Document}, ElapsedMs={ElapsedMs}",
            submission.DocumentKey, timer.ElapsedMilliseconds);
    }

    async Task IEDespatchMetadataUpdateProcessor.ProcessPendingAsync(Guid id, CancellationToken cancellationToken)
    {
        var submission = await authDbContext.EDespatchSubmissions.SingleAsync(x => x.Id == id, cancellationToken);
        await using var documentLock = await EDespatchDocumentLock.TryAcquireAsync(
            mikroWriteDbContext.Database.GetConnectionString()!, submission.DocumentKey, cancellationToken);
        if (documentLock is null) return;
        await authDbContext.Entry(submission).ReloadAsync(cancellationToken);
        if (submission.Status is not (EDespatchSubmissionStatus.Unknown or EDespatchSubmissionStatus.PendingMetadata or EDespatchSubmissionStatus.NeedsReview) ||
            submission.Status != EDespatchSubmissionStatus.NeedsReview && submission.NextAttemptAtUtc > DateTime.UtcNow) return;
        var timer = Stopwatch.StartNew();
        try
        {
            var work = JsonSerializer.Deserialize<EDespatchMetadataUpdateWorkItem>(submission.PayloadJson)
                ?? throw new EDespatchMetadataConflictException("Stored e-despatch work is invalid.");
            if (submission.Status == EDespatchSubmissionStatus.NeedsReview)
            {
                if (work.Request.DocumentType != EDespatchDocumentType.CompanyReturn || submission.AttemptCount != 0)
                    return;

                // A prior version treated Mikro's 0 -> 1 technical return warehouse normalization as a content conflict.
                submission.ReopenMetadataReview(DateTime.UtcNow);
                await authDbContext.SaveChangesAsync(cancellationToken);
            }
            if (submission.Status == EDespatchSubmissionStatus.Unknown)
            {
                await EnsureTrackedSubmissionContainsCompleteDocumentAsync(options.Value,
                    new SentDespatchInfo(submission.DocumentNo, submission.Uuid), work.Movements.Length, cancellationToken, work.Movements);
                await ConfirmSubmissionAsync(submission, cancellationToken);
            }
            if (!await UpdateLocalMetadataAsync(work, cancellationToken))
                throw new InvalidOperationException("Mikro metadata update could not be verified; it will be retried.");
            submission.Complete(DateTime.UtcNow);
            await authDbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Mikro metadata completed. Document={Document}, ElapsedMs={ElapsedMs}",
                submission.DocumentKey, timer.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            // Discard failed timeline changes before persisting the retry in the same scope.
            authDbContext.ChangeTracker.Clear();
            submission = await authDbContext.EDespatchSubmissions.SingleAsync(x => x.Id == id, cancellationToken);
            ApplyMetadataProcessingFailure(submission, exception, DateTime.UtcNow);
            await authDbContext.SaveChangesAsync(cancellationToken);
            logger.LogWarning(exception, "E-despatch reconciliation deferred. Document={Document}, Status={Status}, Attempt={Attempt}",
                submission.DocumentKey, submission.Status, submission.AttemptCount);
        }
    }

    internal static void ApplyMetadataProcessingFailure(
        EDespatchSubmission submission,
        Exception exception,
        DateTime now)
    {
        if (exception is EDespatchMetadataConflictException &&
            submission.Status is EDespatchSubmissionStatus.Unknown or EDespatchSubmissionStatus.PendingMetadata)
        {
            submission.RequireReview(exception.Message);
            return;
        }

        submission.ScheduleRetry(exception.Message, now);
    }

    private async Task<bool> UpdateLocalMetadataAsync(
        EDespatchMetadataUpdateWorkItem workItem,
        CancellationToken cancellationToken)
    {
        var request = workItem.Request;

        if (request.DocumentType is EDespatchDocumentType.InterWarehouseShipment or EDespatchDocumentType.WarehouseReturn)
        {
            var document = await ResolveInterWarehouseDocumentAsync(
                request.WarehouseNo,
                request.DocumentSerie,
                request.DocumentOrderNo,
                request.DocumentType == EDespatchDocumentType.WarehouseReturn,
                cancellationToken);
            var contacts = ResolveDespatchContacts(
                request,
                NormalizeText(document.TrackedMovements.Select(movement => movement.sth_HareketGrupKodu2).ToArray()),
                NormalizeText(document.TrackedMovements.Select(movement => movement.sth_HareketGrupKodu3).ToArray()));

            EnsureSnapshotMatches(workItem.Movements, document.TrackedMovements, request.DocumentType);

            if (HasMatchingSentMarker(
                    document.TrackedMovements,
                    workItem.EDespatchDocumentNo,
                    workItem.EDespatchUuid))
            {
                return true;
            }

            var updated = await TryMarkAsSentAsync(
                document.Context,
                document.TrackedMovements,
                workItem.EDespatchDocumentNo,
                workItem.EDespatchUuid,
                new SentMovementMetadata(
                    NormalizeText(request.Plaque),
                    contacts.Deliverer,
                    contacts.Receiver,
                    NormalizeText(request.DriverTckn)), request.DocumentType, cancellationToken);

            return updated && await TryDocumentMovementSetMatchesAfterSubmissionAsync(
                request,
                document.Context,
                document.TrackedMovements);
        }

        var movementKind = request.DocumentType switch
        {
            EDespatchDocumentType.OutgoingCompanyShipment => CompanyMovementKind.OutgoingShipment,
            EDespatchDocumentType.CompanyReturn => CompanyMovementKind.PurchaseReturn,
            _ => throw new ArgumentOutOfRangeException(
                nameof(request.DocumentType),
                request.DocumentType,
                "Unsupported e-despatch document type.")
        };
        var companyDocument = await ResolveCompanyMovementAsync(
            request.WarehouseNo,
            request.DocumentSerie,
            request.DocumentOrderNo,
            movementKind,
            cancellationToken);
        var companyContacts = ResolveDespatchContacts(
            request,
            companyDocument.Detail.Header.Deliverer,
            companyDocument.Detail.Header.Receiver);
        EnsureSnapshotMatches(workItem.Movements, companyDocument.TrackedMovements, request.DocumentType);
        if (HasMatchingSentMarker(
                companyDocument.TrackedMovements,
                workItem.EDespatchDocumentNo,
                workItem.EDespatchUuid))
        {
            return true;
        }

        var companyUpdated = await TryMarkAsSentAsync(
            companyDocument.Context,
            companyDocument.TrackedMovements,
            workItem.EDespatchDocumentNo,
            workItem.EDespatchUuid,
            new SentMovementMetadata(
                NormalizeText(request.Plaque),
                companyContacts.Deliverer,
                companyContacts.Receiver,
                NormalizeText(request.DriverTckn)), request.DocumentType, cancellationToken);

        return companyUpdated && await TryDocumentMovementSetMatchesAfterSubmissionAsync(
            request,
            companyDocument.Context,
            companyDocument.TrackedMovements);
    }

    private async Task<(string DocumentNo, string Uuid)?> FindCompleteLocalMarkerAsync(
        SendEDespatchRequest request,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<STOK_HAREKETLERI> movements;
        if (request.DocumentType is EDespatchDocumentType.InterWarehouseShipment or EDespatchDocumentType.WarehouseReturn)
        {
            var document = await ResolveInterWarehouseDocumentAsync(
                request.WarehouseNo,
                request.DocumentSerie,
                request.DocumentOrderNo,
                request.DocumentType == EDespatchDocumentType.WarehouseReturn,
                cancellationToken);
            movements = document.TrackedMovements;
        }
        else
        {
            var movementKind = request.DocumentType switch
            {
                EDespatchDocumentType.OutgoingCompanyShipment => CompanyMovementKind.OutgoingShipment,
                EDespatchDocumentType.CompanyReturn => CompanyMovementKind.PurchaseReturn,
                _ => throw new ArgumentOutOfRangeException(nameof(request.DocumentType), request.DocumentType, null)
            };
            var document = await ResolveCompanyMovementAsync(
                request.WarehouseNo,
                request.DocumentSerie,
                request.DocumentOrderNo,
                movementKind,
                cancellationToken);
            movements = document.TrackedMovements;
        }

        return ResolveConsistentSentDespatchMarker(
            movements.Select(movement => (movement.sth_belge_no, movement.sth_aciklama)).ToArray());
    }

    internal static bool HasMatchingSentMarker(
        IReadOnlyCollection<STOK_HAREKETLERI> movements,
        string expectedDocumentNo,
        string expectedUuid)
    {
        try
        {
            EnsureMovementMarkersMatchTrackedSubmission(
                movements.Select(x => (x.sth_belge_no, x.sth_aciklama)).ToArray(), expectedDocumentNo, expectedUuid);
        }
        catch (InvalidOperationException exception) { throw new EDespatchMetadataConflictException(exception.Message); }

        return movements.Count > 0 && movements.All(x => x.sth_kilitli == true &&
            string.Equals(x.sth_belge_no?.Trim(), expectedDocumentNo, StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParse(x.sth_aciklama, out var uuid) && uuid == Guid.Parse(expectedUuid));
    }

    private async Task<SendEDespatchRequest> ResolveDriverAsync(
        SendEDespatchRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedRequest = NormalizeDriverFields(request);

        if (!normalizedRequest.DriverId.HasValue || normalizedRequest.DriverId.Value == Guid.Empty)
        {
            return normalizedRequest;
        }

        var driver = await authDbContext.DespatchDrivers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == normalizedRequest.DriverId.Value && item.IsActive,
                cancellationToken)
            ?? throw new KeyNotFoundException("Despatch driver was not found or inactive.");

        var driverNameSurname = JoinNonEmpty(driver.FirstName, driver.LastName);

        return normalizedRequest with
        {
            Plaque = ResolveDriverField(normalizedRequest.Plaque, driver.PlateNumber),
            DriverNameSurname = ResolveDriverField(normalizedRequest.DriverNameSurname, driverNameSurname),
            DriverTckn = ResolveDriverField(normalizedRequest.DriverTckn, driver.Tckn)
        };
    }

    private async Task RecordEDespatchFlowAsync(
        SendEDespatchRequest request,
        DocumentFlowStatus status,
        string message,
        string? error,
        SendEDespatchResponse? response,
        CancellationToken cancellationToken)
    {
        var documentType = ToDocumentFlowType(request.DocumentType);

        var targetWarehouseNo = await ResolveDocumentFlowTargetWarehouseNoAsync(request, cancellationToken);

        await documentFlowService.RecordAsync(
            new RecordDocumentFlowRequest(
                DocumentFlowKeys.Create(
                    documentType,
                    request.WarehouseNo,
                    request.DocumentSerie,
                    request.DocumentOrderNo),
                documentType,
                request.WarehouseNo,
                targetWarehouseNo,
                request.DocumentSerie,
                request.DocumentOrderNo,
                DocumentFlowStep.EDespatchSubmission,
                status,
                message,
                error,
                ExternalDocumentNo: response?.EDespatchDocumentNo,
                ExternalUuid: response?.EDespatchUuid),
            cancellationToken);
    }

    private async Task<SentDespatchInfo?> GetTrackedSubmittedDespatchAsync(
        SendEDespatchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await FindConfirmedSubmissionAsync(authDbContext, request, cancellationToken);
        return result is null ? null : new SentDespatchInfo(result.Value.DocumentNo, result.Value.Uuid);
    }

    internal static async Task<(string DocumentNo, string Uuid)?> FindConfirmedSubmissionAsync(
        AuthDbContext db, SendEDespatchRequest request, CancellationToken cancellationToken)
    {
        var key = SubmissionKey(request);
        var submission = await db.EDespatchSubmissions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.DocumentKey == key, cancellationToken);
        if (submission is not null && submission.Status is EDespatchSubmissionStatus.PendingMetadata or
            EDespatchSubmissionStatus.Completed or EDespatchSubmissionStatus.NeedsReview)
            return (submission.DocumentNo, submission.Uuid);
        var documentType = ToDocumentFlowType(request.DocumentType);
        var flowKey = DocumentFlowKeys.Create(
            documentType,
            request.WarehouseNo,
            request.DocumentSerie,
            request.DocumentOrderNo);

        var sentFlow = await db.DocumentFlows
            .AsNoTracking()
            .Where(flow =>
                flow.FlowKey == flowKey &&
                flow.ExternalDocumentNo != null &&
                flow.ExternalUuid != null)
            .Select(flow => new
            {
                flow.ExternalDocumentNo,
                flow.ExternalUuid
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (sentFlow is null || !Guid.TryParse(sentFlow.ExternalUuid, out _))
        {
            return null;
        }

        return (
            sentFlow.ExternalDocumentNo!.Trim(),
            sentFlow.ExternalUuid!.Trim());
    }

    private async Task<SendEDespatchResponse?> TryRecoverExistingSubmissionAsync(
        SendEDespatchRequest request,
        MikroDbContext context,
        IReadOnlyCollection<STOK_HAREKETLERI> trackedMovements,
        string documentSerie,
        int documentOrderNo,
        ResolvedDespatchContacts contacts,
        EDespatchOptions config,
        CancellationToken cancellationToken)
    {
        var movementMarkers = trackedMovements
            .Select(movement => (movement.sth_belge_no, movement.sth_aciklama))
            .ToArray();
        (string DocumentNo, string Uuid)? localMarker = null;
        InvalidOperationException? localMarkerError = null;
        try
        {
            localMarker = ResolveConsistentSentDespatchMarker(movementMarkers);
        }
        catch (InvalidOperationException exception)
        {
            localMarkerError = exception;
        }

        SentDespatchInfo? sentDespatch;
        if (localMarker is not null)
        {
            sentDespatch = new SentDespatchInfo(localMarker.Value.DocumentNo, localMarker.Value.Uuid);
        }
        else
        {
            sentDespatch = await GetTrackedSubmittedDespatchAsync(request, cancellationToken);
            if (sentDespatch is null)
            {
                if (localMarkerError is not null)
                {
                    throw localMarkerError;
                }

                return null;
            }

            EnsureMovementMarkersMatchTrackedSubmission(
                movementMarkers,
                sentDespatch.EDespatchDocumentNo,
                sentDespatch.EDespatchUuid);

            await EnsureTrackedSubmissionContainsCompleteDocumentAsync(
                config, sentDespatch, trackedMovements.Count, cancellationToken,
                trackedMovements.Select(x => EDespatchMovementSnapshot.From(x, request.DocumentType)).ToArray());
        }

        if (sentDespatch is null)
        {
            return null;
        }

        logger.LogInformation(
            "Existing Uyumsoft e-despatch submission was recovered without resending. Document={DocumentSerie}/{DocumentOrderNo}, EDespatchDocumentNo={EDespatchDocumentNo}",
            documentSerie,
            documentOrderNo,
            sentDespatch.EDespatchDocumentNo);

        var work = new EDespatchMetadataUpdateWorkItem(
            request with { Deliverer = contacts.Deliverer, Receiver = contacts.Receiver },
            sentDespatch.EDespatchDocumentNo, sentDespatch.EDespatchUuid,
            trackedMovements.Select(x => EDespatchMovementSnapshot.From(x, request.DocumentType)).ToArray());
        var submission = new EDespatchSubmission(SubmissionKey(request), work.EDespatchDocumentNo,
            work.EDespatchUuid, JsonSerializer.Serialize(work), DateTime.UtcNow);
        authDbContext.EDespatchSubmissions.Add(submission);
        await ConfirmSubmissionAsync(submission, cancellationToken);
        if (HasMatchingSentMarker(trackedMovements, work.EDespatchDocumentNo, work.EDespatchUuid))
        {
            submission.Complete(DateTime.UtcNow);
            await authDbContext.SaveChangesAsync(cancellationToken);
        }
        return SubmissionResponse(submission);
    }

    internal static (string DocumentNo, string Uuid)? ResolveConsistentSentDespatchMarker(
        IReadOnlyCollection<(string? DocumentNo, string? Uuid)> movements)
    {
        var normalizedMarkers = movements
            .Select(movement =>
            {
                var documentNo = movement.DocumentNo?.Trim();
                var uuid = movement.Uuid?.Trim();
                var hasEDespatchDocumentNo =
                    !string.IsNullOrWhiteSpace(documentNo) &&
                    documentNo.StartsWith(CommonEDespatchDocumentPrefix, StringComparison.OrdinalIgnoreCase);
                var hasEDespatchUuid = Guid.TryParse(uuid, out _);

                return new
                {
                    DocumentNo = documentNo,
                    Uuid = uuid,
                    HasEDespatchMarker = hasEDespatchDocumentNo || hasEDespatchUuid
                };
            })
            .ToArray();

        var eDespatchMarkers = normalizedMarkers
            .Where(movement => movement.HasEDespatchMarker)
            .ToArray();

        if (eDespatchMarkers.Length == 0)
        {
            return null;
        }

        if (eDespatchMarkers.Length != movements.Count)
        {
            throw new InvalidOperationException(
                "E-despatch metadata is only present on some document lines. Automatic recovery was blocked to prevent unsent lines from being attached to an existing e-despatch.");
        }

        var markers = eDespatchMarkers
            .Select(movement =>
            {
                if (string.IsNullOrWhiteSpace(movement.DocumentNo) ||
                    !movement.DocumentNo.StartsWith(CommonEDespatchDocumentPrefix, StringComparison.OrdinalIgnoreCase) ||
                    !Guid.TryParse(movement.Uuid, out _))
                {
                    throw new InvalidOperationException(
                        "Document lines contain invalid or incomplete e-despatch metadata. Automatic recovery was blocked.");
                }

                return (DocumentNo: movement.DocumentNo, Uuid: movement.Uuid!);
            })
            .Distinct()
            .ToArray();

        if (markers.Length != 1)
        {
            throw new InvalidOperationException(
                "Document lines reference more than one e-despatch. Automatic recovery was blocked.");
        }

        return markers[0];
    }

    internal static void EnsureMovementMarkersMatchTrackedSubmission(
        IReadOnlyCollection<(string? DocumentNo, string? Uuid)> movements,
        string trackedDocumentNo,
        string trackedUuid)
    {
        if (string.IsNullOrWhiteSpace(trackedDocumentNo) ||
            !trackedDocumentNo.StartsWith(CommonEDespatchDocumentPrefix, StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParse(trackedUuid, out var parsedTrackedUuid))
        {
            throw new InvalidOperationException(
                "Tracked e-despatch metadata is invalid. Automatic recovery was blocked.");
        }

        foreach (var movement in movements)
        {
            var documentNo = movement.DocumentNo?.Trim();
            var uuid = movement.Uuid?.Trim();
            var hasEDespatchDocumentNo =
                !string.IsNullOrWhiteSpace(documentNo) &&
                documentNo.StartsWith(CommonEDespatchDocumentPrefix, StringComparison.OrdinalIgnoreCase);
            var hasEDespatchUuid = Guid.TryParse(uuid, out var parsedMovementUuid);

            if (!hasEDespatchDocumentNo && !hasEDespatchUuid)
            {
                continue;
            }

            if (!hasEDespatchDocumentNo ||
                !hasEDespatchUuid ||
                !string.Equals(documentNo, trackedDocumentNo.Trim(), StringComparison.OrdinalIgnoreCase) ||
                parsedMovementUuid != parsedTrackedUuid)
            {
                throw new InvalidOperationException(
                    "Mikro document lines conflict with the tracked e-despatch. Automatic recovery was blocked.");
            }
        }
    }


    internal static void EnsureTrackedSubmissionMatchesDocument(
        string? actualDocumentNo,
        string? actualUuid,
        int actualLineCount,
        string trackedDocumentNo,
        string trackedUuid,
        int expectedLineCount)
    {
        if (!string.Equals(
                actualDocumentNo?.Trim(),
                trackedDocumentNo.Trim(),
                StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParse(actualUuid, out var parsedActualUuid) ||
            !Guid.TryParse(trackedUuid, out var parsedTrackedUuid) ||
            parsedActualUuid != parsedTrackedUuid ||
            actualLineCount != expectedLineCount)
        {
            throw new InvalidOperationException(
                $"Tracked Uyumsoft e-despatch does not match the complete Mikro document. " +
                $"Expected {expectedLineCount} lines, Uyumsoft returned {actualLineCount}. " +
                "Automatic metadata recovery was blocked.");
        }
    }

    private static async Task EnsureTrackedSubmissionContainsCompleteDocumentAsync(
        EDespatchOptions config,
        SentDespatchInfo trackedSubmission,
        int expectedLineCount,
        CancellationToken cancellationToken,
        EDespatchMovementSnapshot[]? expectedMovements = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var endpointOptions = ToEndpointOptions(config);
        var client = UyumsoftWcfClientHelper.CreateDespatchClient(endpointOptions);

        try
        {
            var response = await client.GetOutboxDespatchAsync(
                UyumsoftWcfClientHelper.CreateDespatchUserInfo(endpointOptions),
                trackedSubmission.EDespatchUuid);

            EnsureSucceeded(response, "e-despatch recovery verification");
            cancellationToken.ThrowIfCancellationRequested();

            var despatch = response.Value?.DespatchAdvice
                ?? throw new InvalidOperationException(
                    "Uyumsoft e-despatch recovery response does not contain a despatch document.");

            EnsureTrackedSubmissionMatchesDocument(
                despatch.ID?.Value,
                despatch.UUID?.Value,
                despatch.DespatchLine?.Length ?? 0,
                trackedSubmission.EDespatchDocumentNo,
                trackedSubmission.EDespatchUuid,
                expectedLineCount);
            if (expectedMovements is not null) EnsureUyumsoftLinesMatch(despatch.DespatchLine, expectedMovements);
        }
        catch
        {
            UyumsoftWcfClientHelper.Abort(client);
            throw;
        }
        finally
        {
            await UyumsoftWcfClientHelper.CloseAsync(client);
        }
    }
    internal static void EnsureUyumsoftLinesMatch(
        UyumsoftDespatch.DespatchLineType[]? lines, EDespatchMovementSnapshot[] expectedMovements)
    {
        if (lines is null || lines.Length != expectedMovements.Length ||
            expectedMovements.Any(x => x.RowNo is null) ||
            expectedMovements.Select(x => x.RowNo).Distinct().Count() != expectedMovements.Length ||
            lines.Select(x => x.ID?.Value).Distinct().Count() != lines.Length)
            throw new EDespatchMetadataConflictException("E-despatch line identities/count do not match Mikro.");
        foreach (var expected in expectedMovements)
        {
            var line = lines.SingleOrDefault(x => x.ID?.Value == (expected.RowNo!.Value + 1).ToString(CultureInfo.InvariantCulture));
            var expectedLineNo = expected.RowNo.GetValueOrDefault() + 1;
            var actualStockCode = line?.Item?.SellersItemIdentification?.ID?.Value;
            var actualQuantity = line?.DeliveredQuantity?.Value;
            var actualQuantityValue = actualQuantity.GetValueOrDefault();
            // Uyumsoft's outbox response can omit SellersItemIdentification even though it exists in
            // the submitted UBL. A stock code returned by Uyumsoft must still match exactly.
            if (line is null ||
                !string.IsNullOrWhiteSpace(actualStockCode) && actualStockCode != expected.StockCode ||
                actualQuantity is null ||
                Math.Abs((double)actualQuantityValue - (expected.Quantity ?? 0)) > 0.000001)
                throw new EDespatchMetadataConflictException(
                    $"E-despatch line {expectedLineNo} differs from Mikro. Expected stock '{expected.StockCode}' quantity " +
                    $"{expected.Quantity ?? 0}; Uyumsoft returned stock '{actualStockCode ?? "<missing>"}' quantity " +
                    $"{(actualQuantity?.ToString(CultureInfo.InvariantCulture) ?? "<missing>")}. Submission or metadata recovery was blocked.");
        }
    }

    private async Task EnsureDocumentMovementSetUnchangedAsync(
        SendEDespatchRequest request,
        MikroDbContext context,
        IReadOnlyCollection<STOK_HAREKETLERI> trackedMovements,
        CancellationToken cancellationToken)
    {
        if (await DocumentMovementSetMatchesAsync(
                request,
                context,
                trackedMovements,
                cancellationToken))
        {
            return;
        }

        throw new InvalidOperationException(
            "Document lines changed while the e-despatch was being prepared. Refresh the document and try again; no e-despatch was sent.");
    }

    private async Task<bool> DocumentMovementSetMatchesAsync(
        SendEDespatchRequest request,
        MikroDbContext context,
        IReadOnlyCollection<STOK_HAREKETLERI> trackedMovements,
        CancellationToken cancellationToken)
    {
        var currentMovements = await BuildDocumentMovementQuery(context, request)
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);
        var matches = MatchesExpectedLineCount(request.ExpectedLineCount, currentMovements.Length) &&
            EDespatchMovementSnapshot.Matches(
            trackedMovements.Select(x => EDespatchMovementSnapshot.From(x, request.DocumentType)), currentMovements, request.DocumentType);

        if (!matches)
        {
            logger.LogWarning(
                "E-despatch document movement set changed. DocumentType={DocumentType}, WarehouseNo={WarehouseNo}, Document={DocumentSerie}/{DocumentOrderNo}, PreparedCount={PreparedCount}, CurrentCount={CurrentCount}",
                request.DocumentType,
                request.WarehouseNo,
                request.DocumentSerie,
                request.DocumentOrderNo,
                trackedMovements.Count,
                currentMovements.Length);
        }

        return matches;
    }

    private static void EnsureExpectedLineCountMatches(SendEDespatchRequest request, int actualLineCount)
    {
        if (MatchesExpectedLineCount(request.ExpectedLineCount, actualLineCount))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Document line count does not match the legacy request. Expected {request.ExpectedLineCount}, " +
            $"but Mikro currently contains {actualLineCount} lines; no e-despatch was sent.");
    }

    internal static bool MatchesExpectedLineCount(int? expectedLineCount, int actualLineCount) =>
        !expectedLineCount.HasValue || expectedLineCount.Value == actualLineCount;

    private async Task<bool> TryDocumentMovementSetMatchesAfterSubmissionAsync(
        SendEDespatchRequest request,
        MikroDbContext context,
        IReadOnlyCollection<STOK_HAREKETLERI> trackedMovements)
    {
        using var completionCancellation = new CancellationTokenSource(
            TimeSpan.FromSeconds(PostSubmissionCompletionTimeoutSeconds));

        try
        {
            return await DocumentMovementSetMatchesAsync(
                request,
                context,
                trackedMovements,
                completionCancellation.Token);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "E-despatch was sent but the complete Mikro document movement set could not be verified. DocumentType={DocumentType}, WarehouseNo={WarehouseNo}, Document={DocumentSerie}/{DocumentOrderNo}",
                request.DocumentType,
                request.WarehouseNo,
                request.DocumentSerie,
                request.DocumentOrderNo);
            return false;
        }
    }

    private static IQueryable<STOK_HAREKETLERI> BuildDocumentMovementQuery(
        MikroDbContext context,
        SendEDespatchRequest request)
    {
        var query = context.STOK_HAREKETLERIs.Where(movement =>
            movement.sth_evrakno_seri == request.DocumentSerie &&
            movement.sth_evrakno_sira == request.DocumentOrderNo);

        return request.DocumentType switch
        {
            EDespatchDocumentType.OutgoingCompanyShipment => query.Where(movement =>
                movement.sth_evraktip == CompanyDispatchDocumentType &&
                movement.sth_tip == OutgoingMovementType &&
                movement.sth_normal_iade == NormalMovement &&
                movement.sth_cikis_depo_no == request.WarehouseNo),
            EDespatchDocumentType.CompanyReturn => query.Where(movement =>
                movement.sth_evraktip == CompanyDispatchDocumentType &&
                movement.sth_tip == OutgoingMovementType &&
                movement.sth_normal_iade == ReturnMovement &&
                movement.sth_cikis_depo_no == request.WarehouseNo),
            EDespatchDocumentType.InterWarehouseShipment => query.Where(movement =>
                movement.sth_evraktip == InterWarehouseShipmentDocumentType &&
                movement.sth_normal_iade == NormalMovement &&
                movement.sth_cikis_depo_no == request.WarehouseNo),
            EDespatchDocumentType.WarehouseReturn => query.Where(movement =>
                movement.sth_evraktip == InterWarehouseShipmentDocumentType &&
                movement.sth_normal_iade == ReturnMovement &&
                movement.sth_cikis_depo_no == request.WarehouseNo),
            _ => throw new ArgumentOutOfRangeException(nameof(request.DocumentType))
        };
    }

    internal static bool HaveSameMovementGuids(
        IEnumerable<Guid> preparedMovementGuids,
        IEnumerable<Guid> currentMovementGuids)
    {
        var prepared = preparedMovementGuids.ToHashSet();
        var current = currentMovementGuids.ToHashSet();
        return prepared.Count == current.Count && prepared.SetEquals(current);
    }

    private static DocumentFlowType ToDocumentFlowType(EDespatchDocumentType documentType) =>
        documentType switch
        {
            EDespatchDocumentType.OutgoingCompanyShipment => DocumentFlowType.CompanyShipment,
            EDespatchDocumentType.CompanyReturn => DocumentFlowType.CompanyReturn,
            EDespatchDocumentType.InterWarehouseShipment => DocumentFlowType.InterWarehouseShipment,
            EDespatchDocumentType.WarehouseReturn => DocumentFlowType.WarehouseReturn,
            _ => throw new ArgumentOutOfRangeException(nameof(documentType))
        };

    private async Task<int?> ResolveDocumentFlowTargetWarehouseNoAsync(
        SendEDespatchRequest request,
        CancellationToken cancellationToken)
    {
        if (request.DocumentType is not (EDespatchDocumentType.InterWarehouseShipment or EDespatchDocumentType.WarehouseReturn))
        {
            return null;
        }

        try
        {
            var returnType = request.DocumentType == EDespatchDocumentType.WarehouseReturn
                ? ReturnMovement
                : NormalMovement;

            return await TryLoadInterWarehouseTargetWarehouseNoAsync(
                    mikroDbContext,
                    request,
                    returnType,
                    cancellationToken)
                ?? await TryLoadInterWarehouseTargetWarehouseNoAsync(
                    mikroWriteDbContext,
                    request,
                    returnType,
                    cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "E-despatch document flow target warehouse could not be resolved. DocumentType={DocumentType}, WarehouseNo={WarehouseNo}, DocumentSerie={DocumentSerie}, DocumentOrderNo={DocumentOrderNo}",
                request.DocumentType,
                request.WarehouseNo,
                request.DocumentSerie,
                request.DocumentOrderNo);

            return null;
        }
    }

    private static async Task<int?> TryLoadInterWarehouseTargetWarehouseNoAsync(
        MikroDbContext dbContext,
        SendEDespatchRequest request,
        byte returnType,
        CancellationToken cancellationToken) =>
        await dbContext.STOK_HAREKETLERIs
            .AsNoTracking()
            .Where(movement =>
                movement.sth_evraktip == InterWarehouseShipmentDocumentType &&
                movement.sth_normal_iade == returnType &&
                movement.sth_evrakno_seri == request.DocumentSerie &&
                movement.sth_evrakno_sira == request.DocumentOrderNo &&
                movement.sth_cikis_depo_no == request.WarehouseNo &&
                movement.sth_nakliyedeposu > 0)
            .OrderBy(movement => movement.sth_satirno)
            .Select(movement => movement.sth_nakliyedeposu)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<GetEDespatchPdfResponse> GetPdfAsync(
        GetEDespatchPdfRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);

        var config = options.Value;
        ValidateConfiguration(config);

        var lookupRequest = new SendEDespatchRequest(request.DocumentType, request.WarehouseNo,
            request.DocumentSerie, request.DocumentOrderNo, string.Empty, string.Empty, string.Empty);
        var confirmed = await GetTrackedSubmittedDespatchAsync(lookupRequest, cancellationToken);
        // Uyumsoft owns the PDF; Mikro metadata can legitimately still be pending.
        var sentDespatch = confirmed ?? (request.DocumentType switch
        {
            EDespatchDocumentType.OutgoingCompanyShipment => ExtractSentDespatchInfo(
                (await ResolveCompanyMovementAsync(
                    request.WarehouseNo,
                    request.DocumentSerie,
                    request.DocumentOrderNo,
                    CompanyMovementKind.OutgoingShipment,
                    cancellationToken)).TrackedMovements),

            EDespatchDocumentType.CompanyReturn => ExtractSentDespatchInfo(
                (await ResolveCompanyMovementAsync(
                    request.WarehouseNo,
                    request.DocumentSerie,
                    request.DocumentOrderNo,
                    CompanyMovementKind.PurchaseReturn,
                    cancellationToken)).TrackedMovements),

            EDespatchDocumentType.InterWarehouseShipment => ExtractSentDespatchInfo(
                (await ResolveInterWarehouseDocumentAsync(
                    request.WarehouseNo,
                    request.DocumentSerie,
                    request.DocumentOrderNo,
                    false,
                    cancellationToken)).TrackedMovements),

            EDespatchDocumentType.WarehouseReturn => ExtractSentDespatchInfo(
                (await ResolveInterWarehouseDocumentAsync(
                    request.WarehouseNo,
                    request.DocumentSerie,
                    request.DocumentOrderNo,
                    true,
                    cancellationToken)).TrackedMovements),

            _ => throw new ArgumentOutOfRangeException(
                nameof(request.DocumentType),
                request.DocumentType,
                "Unsupported e-despatch document type.")
        });

        var pdfTimer = Stopwatch.StartNew();
        var pdfContent = await GetOutboxDespatchPdfAsync(
            config,
            sentDespatch.EDespatchUuid,
            cancellationToken);
        logger.LogInformation("Uyumsoft PDF received. DocumentNo={DocumentNo}, ElapsedMs={ElapsedMs}",
            sentDespatch.EDespatchDocumentNo, pdfTimer.ElapsedMilliseconds);

        return new GetEDespatchPdfResponse(
            $"{sentDespatch.EDespatchDocumentNo}.pdf",
            pdfContent);
    }

    public async Task<GetEDespatchStatusResponse> GetStatusAsync(
        GetEDespatchStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        var sendRequest = new SendEDespatchRequest(
            request.DocumentType,
            request.WarehouseNo,
            request.DocumentSerie,
            request.DocumentOrderNo,
            string.Empty,
            string.Empty,
            string.Empty);
        var key = SubmissionKey(sendRequest);
        var submission = await authDbContext.EDespatchSubmissions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.DocumentKey == key, cancellationToken);

        if (submission is not null)
        {
            var isSent = submission.Status is EDespatchSubmissionStatus.PendingMetadata or
                EDespatchSubmissionStatus.Completed or EDespatchSubmissionStatus.NeedsReview;
            return new GetEDespatchStatusResponse(
                request.DocumentType,
                request.DocumentSerie,
                request.DocumentOrderNo,
                isSent,
                submission.Status.ToString(),
                isSent ? submission.DocumentNo : null,
                isSent ? submission.Uuid : null,
                isSent ? submission.CreatedAtUtc : null,
                submission.Status == EDespatchSubmissionStatus.Completed,
                submission.Status == EDespatchSubmissionStatus.PendingMetadata,
                submission.Status switch
                {
                    EDespatchSubmissionStatus.Unknown => "E-irsaliye sonucu Uyumsoft'ta dogrulaniyor. Yeni gonderim yapmayin.",
                    EDespatchSubmissionStatus.PendingMetadata => BuildQueuedMikroMetadataWarning(),
                    EDespatchSubmissionStatus.NeedsReview => "E-irsaliye Uyumsoft'a gonderildi; Mikro isaretleme manuel inceleme bekliyor.",
                    _ => null
                });
        }

        var flow = await authDbContext.DocumentFlows.AsNoTracking()
            .Where(x => x.FlowKey == key && x.ExternalDocumentNo != null && x.ExternalUuid != null)
            .Select(x => new { x.ExternalDocumentNo, x.ExternalUuid, x.UpdatedAtUtc })
            .SingleOrDefaultAsync(cancellationToken);
        var isLegacySent = flow is not null && Guid.TryParse(flow.ExternalUuid, out _);
        return new GetEDespatchStatusResponse(
            request.DocumentType,
            request.DocumentSerie,
            request.DocumentOrderNo,
            isLegacySent,
            isLegacySent ? "Sent" : "NotSent",
            isLegacySent ? flow!.ExternalDocumentNo!.Trim() : null,
            isLegacySent ? flow!.ExternalUuid!.Trim() : null,
            isLegacySent ? flow!.UpdatedAtUtc : null,
            false,
            false,
            isLegacySent
                ? "E-irsaliye Uyumsoft'a gonderilmis. Eski kayitta Mikro isaretleme durumu izlenemiyor."
                : null);
    }

    private async Task<SendEDespatchResponse> SendCompanyMovementAsync(
        SendEDespatchRequest request,
        CompanyMovementKind movementKind,
        CancellationToken cancellationToken)
    {
        var document = await ResolveCompanyMovementAsync(
            request.WarehouseNo,
            request.DocumentSerie,
            request.DocumentOrderNo,
            movementKind,
            cancellationToken);
        var contacts = ResolveDespatchContacts(
            request,
            document.Detail.Header.Deliverer,
            document.Detail.Header.Receiver);
        var primaryBarcodes = await LoadPrimaryBarcodesAsync(
            document.Context,
            document.Detail.Items.Select(item => item.StockCode),
            cancellationToken);
        var config = options.Value;
        var recoveredResponse = await TryRecoverExistingSubmissionAsync(
            request,
            document.Context,
            document.TrackedMovements,
            document.Detail.Header.DocumentSerie,
            document.Detail.Header.DocumentOrderNo,
            contacts,
            config,
            cancellationToken);
        if (recoveredResponse is not null)
        {
            return recoveredResponse;
        }

        await CompanyEDespatchCreateGuard.EnsureCompleteAsync(
            authDbContext, request, document.TrackedMovements, cancellationToken);

        var now = DateTime.Now;
        var sourceWarehouse = await LoadWarehouseAsync(
            document.Context,
            document.Detail.Header.WarehouseNo,
            cancellationToken);
        var supplierCustomer = await LoadCustomerAsync(
            document.Context,
            config.SupplierCustomerCode,
            null,
            cancellationToken);
        var deliveryCustomer = await LoadCustomerAsync(
            document.Context,
            document.Detail.Header.CustomerCode,
            document.Metadata.AddressNo,
            cancellationToken);
        EnsureDeliveryAddressPostalCode(
            deliveryCustomer.PostalCode,
            $"customer {deliveryCustomer.CustomerCode} ({deliveryCustomer.DisplayName}) address {document.Metadata.AddressNo}");
        var resolvedDeliveryAlias = await ResolveTargetCustomerAliasAsync(
            deliveryCustomer,
            config,
            cancellationToken);
        return await SendDurablyAsync(request, document.Context, document.TrackedMovements, contacts,
            (eDespatchDocumentNo, eDespatchUuid) => BuildDespatchInfo(
            BuildCompanyMovementDespatchAdvice(
                document.Detail,
                sourceWarehouse,
                supplierCustomer,
                deliveryCustomer,
                request,
                contacts,
                primaryBarcodes,
                now,
                eDespatchDocumentNo,
                eDespatchUuid,
                config),
            BuildLocalDocumentId(request),
            resolvedDeliveryAlias,
            resolvedDeliveryAlias is null ? null : deliveryCustomer.TaxNumber,
            resolvedDeliveryAlias is null ? null : deliveryCustomer.DisplayName), cancellationToken);
    }

    private async Task<SendEDespatchResponse> SendInterWarehouseDocumentAsync(
        SendEDespatchRequest request,
        bool isReturn,
        CancellationToken cancellationToken)
    {
        var document = await ResolveInterWarehouseDocumentAsync(
            request.WarehouseNo,
            request.DocumentSerie,
            request.DocumentOrderNo,
            isReturn,
            cancellationToken);
        var contacts = ResolveDespatchContacts(
            request,
            NormalizeText(document.TrackedMovements.Select(movement => movement.sth_HareketGrupKodu2).ToArray()),
            NormalizeText(document.TrackedMovements.Select(movement => movement.sth_HareketGrupKodu3).ToArray()));
        var primaryBarcodes = await LoadPrimaryBarcodesAsync(
            document.Context,
            document.Detail.Items.Select(item => item.StockCode),
            cancellationToken);
        var config = options.Value;
        var recoveredResponse = await TryRecoverExistingSubmissionAsync(
            request,
            document.Context,
            document.TrackedMovements,
            document.Detail.Header.DocumentSerie,
            document.Detail.Header.DocumentOrderNo,
            contacts,
            config,
            cancellationToken);
        if (recoveredResponse is not null)
        {
            return recoveredResponse;
        }

        var now = DateTime.Now;
        await EnsureInterWarehouseDocumentCompleteAsync(request, document, cancellationToken);
        var supplierCustomer = await LoadCustomerAsync(
            document.Context,
            config.SupplierCustomerCode,
            null,
            cancellationToken);
        var sourceWarehouse = await LoadWarehouseAsync(
            document.Context,
            document.Detail.Header.SourceWarehouseNo,
            cancellationToken);
        var targetWarehouse = await LoadWarehouseAsync(
            document.Context,
            document.Detail.Header.TargetWarehouseNo,
            cancellationToken);
        EnsureDeliveryAddressPostalCode(
            targetWarehouse.PostalCode,
            $"warehouse {targetWarehouse.WarehouseNo} ({targetWarehouse.Name})");
        return await SendDurablyAsync(request, document.Context, document.TrackedMovements, contacts,
            (eDespatchDocumentNo, eDespatchUuid) => BuildDespatchInfo(
            BuildInterWarehouseDespatchAdvice(
                document.Detail,
                supplierCustomer,
                sourceWarehouse,
                targetWarehouse,
                request,
                contacts,
                primaryBarcodes,
                now,
                eDespatchDocumentNo,
                eDespatchUuid,
                config),
            BuildLocalDocumentId(request),
            null,
            null,
            null), cancellationToken);
    }

    private async Task<ResolvedCompanyMovementDocument> ResolveCompanyMovementAsync(
        int warehouseNo,
        string documentSerie,
        int documentOrderNo,
        CompanyMovementKind movementKind,
        CancellationToken cancellationToken)
    {
        var detailRequest = new CompanyMovementDetailRequest(
            warehouseNo,
            documentSerie.Trim(),
            documentOrderNo);
        var readExecutor = new CompanyMovementDetailQueryExecutor(mikroDbContext);

        try
        {
            var detail = await readExecutor.ExecuteAsync(detailRequest, movementKind, cancellationToken);
            var trackedMovements = await LoadCompanyMovementRowsAsync(
                mikroDbContext,
                detailRequest,
                movementKind,
                cancellationToken);

            return new ResolvedCompanyMovementDocument(
                detail,
                trackedMovements,
                mikroDbContext,
                BuildCompanyMovementMetadata(trackedMovements));
        }
        catch (KeyNotFoundException)
        {
            var writeExecutor = new CompanyMovementDetailQueryExecutor(mikroWriteDbContext);
            var detail = await writeExecutor.ExecuteAsync(detailRequest, movementKind, cancellationToken);
            var trackedMovements = await LoadCompanyMovementRowsAsync(
                mikroWriteDbContext,
                detailRequest,
                movementKind,
                cancellationToken);

            return new ResolvedCompanyMovementDocument(
                detail,
                trackedMovements,
                mikroWriteDbContext,
                BuildCompanyMovementMetadata(trackedMovements));
        }
    }

    private async Task EnsureInterWarehouseDocumentCompleteAsync(
        SendEDespatchRequest request,
        ResolvedInterWarehouseDocument document,
        CancellationToken cancellationToken)
    {
        var itemGuids = document.Detail.Items.Select(item => item.MovementGuid);
        if (!HaveSameMovementGuids(itemGuids, document.TrackedMovements.Select(movement => movement.sth_Guid)) ||
            document.Detail.Items.Count != document.TrackedMovements.Count)
        {
            throw new InvalidOperationException(
                "E-despatch detail and Mikro document lines differ. Refresh the document; no e-despatch was sent.");
        }

        var traceKeys = document.TrackedMovements
            .Select(movement => movement.sth_eticaret_kanal_kodu)
            .Where(value => !string.IsNullOrWhiteSpace(value) && value.StartsWith("FR", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (traceKeys.Length == 0)
        {
            return;
        }

        if (traceKeys.Length != 1 ||
            document.TrackedMovements.Any(movement => movement.sth_eticaret_kanal_kodu != traceKeys[0]) ||
            !TryParseOfflineTraceKey(traceKeys[0]!, out var clientRequestId))
        {
            throw new InvalidOperationException(
                "E-despatch document lines do not share a valid create request trace; no e-despatch was sent.");
        }

        var operationCode = request.DocumentType == EDespatchDocumentType.WarehouseReturn
            ? WarehouseReturnCreateOperationCode
            : InterWarehouseCreateOperationCode;
        var create = await authDbContext.MobileOfflineSyncRequests
            .SingleOrDefaultAsync(record =>
                record.OperationCode == operationCode &&
                record.ClientRequestId == clientRequestId.ToString("D") &&
                record.WarehouseNo == request.WarehouseNo,
                cancellationToken);

        if (request.DocumentType == EDespatchDocumentType.InterWarehouseShipment &&
            (create?.Status != MobileOfflineSyncRequestStatus.Completed ||
             string.IsNullOrWhiteSpace(create.ResponsePayload)))
        {
            await TryRecoverInterWarehouseCreateAsync(
                request,
                document,
                traceKeys[0]!,
                create,
                cancellationToken);
        }

        if (create?.Status != MobileOfflineSyncRequestStatus.Completed ||
            string.IsNullOrWhiteSpace(create.ResponsePayload))
        {
            throw new InvalidOperationException(
                "Shipment creation has not completed; wait for the create response before sending the e-despatch.");
        }

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        int? expectedLineCount;
        bool matches;
        try
        {
            if (request.DocumentType == EDespatchDocumentType.WarehouseReturn)
            {
                var response = JsonSerializer.Deserialize<CreateWarehouseReturnResponse>(create.ResponsePayload, jsonOptions);
                var originalRequest = DeserializeCreateRequest<CreateWarehouseReturnRequest>(
                    create.RequestPayload,
                    jsonOptions);
                expectedLineCount = originalRequest?.Lines.Count ?? response?.LineCount;
                matches = response is not null && MatchesCompletedDocumentCreate(
                    response.DocumentSerie, response.DocumentOrderNo, response.SourceWarehouseNo,
                    response.LineCount, request.DocumentSerie, request.DocumentOrderNo,
                    request.WarehouseNo, document.Detail.Items.Count,
                    originalRequest?.Lines.Count);
            }
            else
            {
                var response = JsonSerializer.Deserialize<CreateInterWarehouseShipmentResponse>(create.ResponsePayload, jsonOptions);
                var originalRequest = DeserializeCreateRequest<CreateInterWarehouseShipmentRequest>(
                    create.RequestPayload,
                    jsonOptions);
                expectedLineCount = originalRequest?.Lines.Count ?? response?.LineCount;
                matches = MatchesCompletedShipmentCreate(
                    response, request.DocumentSerie, request.DocumentOrderNo,
                    request.WarehouseNo, document.Detail.Items.Count,
                    originalRequest?.Lines.Count);
            }
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "Shipment create response could not be verified; no e-despatch was sent.", exception);
        }

        if (!matches)
        {
            logger.LogWarning(
                "E-despatch create/detail line count mismatch. Document={DocumentSerie}/{DocumentOrderNo}, ExpectedCount={ExpectedCount}, ActualCount={ActualCount}",
                request.DocumentSerie,
                request.DocumentOrderNo,
                expectedLineCount,
                document.Detail.Items.Count);
            throw new InvalidOperationException(
                "Shipment create response and current document lines differ; no e-despatch was sent.");
        }
    }

    private async Task TryRecoverInterWarehouseCreateAsync(
        SendEDespatchRequest request,
        ResolvedInterWarehouseDocument document,
        string traceKey,
        MobileOfflineSyncRequest? create,
        CancellationToken cancellationToken)
    {
        if (create is null || string.IsNullOrWhiteSpace(create.RequestPayload))
        {
            return;
        }

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        CreateInterWarehouseShipmentRequest? originalRequest;
        try
        {
            originalRequest = JsonSerializer.Deserialize<CreateInterWarehouseShipmentRequest>(
                create.RequestPayload,
                jsonOptions);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(
                exception,
                "Inter warehouse shipment create request could not be deserialized during e-despatch recovery. Document={DocumentSerie}/{DocumentOrderNo}",
                request.DocumentSerie,
                request.DocumentOrderNo);
            return;
        }

        if (originalRequest is null ||
            !InterWarehouseShipmentRecoveryMatcher.Matches(
                originalRequest,
                originalRequest.Lines.ToArray(),
                document.TrackedMovements.Select(movement => new InterWarehouseShipmentRecoveryLine(
                    movement.sth_satirno,
                    movement.sth_stok_kod,
                    movement.sth_miktar ?? 0d,
                    movement.sth_birim_pntr ?? 0,
                    movement.sth_tutar ?? 0d,
                    movement.sth_aciklama,
                    movement.sth_parti_kodu,
                    movement.sth_lot_no ?? 0,
                    movement.sth_proje_kodu,
                    movement.sth_cari_srm_merkezi,
                    movement.sth_stok_srm_merkezi,
                    movement.sth_eticaret_kanal_kodu)).ToArray()))
        {
            return;
        }

        var tracedDocuments = await document.Context.STOK_HAREKETLERIs
            .AsNoTracking()
            .Where(movement =>
                movement.sth_evraktip == InterWarehouseShipmentDocumentType &&
                movement.sth_tip == InterWarehouseMovementType &&
                movement.sth_cins == InterWarehouseMovementGenre &&
                movement.sth_normal_iade == NormalMovement &&
                movement.sth_cikis_depo_no == request.WarehouseNo &&
                movement.sth_eticaret_kanal_kodu == traceKey)
            .Select(movement => new
            {
                movement.sth_evrakno_seri,
                movement.sth_evrakno_sira
            })
            .Distinct()
            .ToListAsync(cancellationToken);

        if (tracedDocuments.Count != 1 ||
            tracedDocuments[0].sth_evrakno_seri != request.DocumentSerie ||
            tracedDocuments[0].sth_evrakno_sira != request.DocumentOrderNo)
        {
            var documentNumbers = string.Join(", ", tracedDocuments
                .OrderBy(item => item.sth_evrakno_sira)
                .Select(item => $"{item.sth_evrakno_seri}/{item.sth_evrakno_sira}"));
            throw new InvalidOperationException(
                $"The same shipment request created multiple Mikro documents ({documentNumbers}); no e-despatch was sent.");
        }

        var header = document.Detail.Header;
        var recoveredResponse = new CreateInterWarehouseShipmentResponse(
            header.DocumentSerie,
            header.DocumentOrderNo,
            header.MovementDate?.Date ?? DateTime.Today,
            header.DocumentDate?.Date ?? header.MovementDate?.Date ?? DateTime.Today,
            header.DocumentNo,
            header.SourceWarehouseNo,
            header.TargetWarehouseNo,
            header.ShippingWarehouseNo,
            document.Detail.Items.Count,
            0,
            document.Detail.Items.Sum(item => item.Quantity),
            document.Detail.Items.Sum(item => item.LineAmount),
            "MikroWriteConnection");

        create.MarkCompleted(JsonSerializer.Serialize(recoveredResponse, jsonOptions), DateTime.UtcNow);
        await authDbContext.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Recovered completed inter warehouse shipment create while preparing e-despatch. Document={DocumentSerie}/{DocumentOrderNo}, ClientRequestId={ClientRequestId}, LineCount={LineCount}",
            request.DocumentSerie,
            request.DocumentOrderNo,
            create.ClientRequestId,
            document.Detail.Items.Count);
    }

    internal static bool MatchesCompletedShipmentCreate(
        CreateInterWarehouseShipmentResponse? response,
        string documentSerie,
        int documentOrderNo,
        int warehouseNo,
        int lineCount,
        int? originalRequestLineCount = null) =>
        response is not null && MatchesCompletedDocumentCreate(
            response.DocumentSerie, response.DocumentOrderNo, response.SourceWarehouseNo,
            response.LineCount, documentSerie, documentOrderNo, warehouseNo, lineCount,
            originalRequestLineCount);

    internal static bool MatchesCompletedDocumentCreate(
        string createdSerie,
        int createdOrderNo,
        int createdWarehouseNo,
        int createdLineCount,
        string documentSerie,
        int documentOrderNo,
        int warehouseNo,
        int lineCount,
        int? originalRequestLineCount = null) =>
        createdSerie == documentSerie &&
        createdOrderNo == documentOrderNo &&
        createdWarehouseNo == warehouseNo &&
        (originalRequestLineCount ?? createdLineCount) == lineCount;

    private static TRequest? DeserializeCreateRequest<TRequest>(
        string? requestPayload,
        JsonSerializerOptions jsonOptions)
    {
        if (string.IsNullOrWhiteSpace(requestPayload))
        {
            return default;
        }

        return JsonSerializer.Deserialize<TRequest>(requestPayload, jsonOptions);
    }

    internal static bool TryParseOfflineTraceKey(string traceKey, out Guid clientRequestId)
    {
        clientRequestId = Guid.Empty;
        if (traceKey.Length != 24 || !traceKey.StartsWith("FR", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var encoded = traceKey[2..].Replace('-', '+').Replace('_', '/') + "==";
            var bytes = Convert.FromBase64String(encoded);
            if (bytes.Length != 16)
            {
                return false;
            }

            clientRequestId = new Guid(bytes);
            return clientRequestId != Guid.Empty;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private async Task<ResolvedInterWarehouseDocument> ResolveInterWarehouseDocumentAsync(
        int warehouseNo,
        string documentSerie,
        int documentOrderNo,
        bool isReturn,
        CancellationToken cancellationToken)
    {
        var detailRequest = new WarehouseShippingDetailRequest(
            warehouseNo,
            documentSerie.Trim(),
            documentOrderNo);
        var readExecutor = new WarehouseShippingDetailQueryExecutor(mikroDbContext);

        try
        {
            var detail = await readExecutor.ExecuteAsync(
                detailRequest,
                WarehouseShippingDirection.Outgoing,
                isReturn,
                cancellationToken);
            var trackedMovements = await LoadInterWarehouseRowsAsync(
                mikroDbContext,
                detailRequest,
                isReturn,
                cancellationToken);

            return new ResolvedInterWarehouseDocument(
                detail,
                trackedMovements,
                mikroDbContext);
        }
        catch (KeyNotFoundException)
        {
            var writeExecutor = new WarehouseShippingDetailQueryExecutor(mikroWriteDbContext);
            var detail = await writeExecutor.ExecuteAsync(
                detailRequest,
                WarehouseShippingDirection.Outgoing,
                isReturn,
                cancellationToken);
            var trackedMovements = await LoadInterWarehouseRowsAsync(
                mikroWriteDbContext,
                detailRequest,
                isReturn,
                cancellationToken);

            return new ResolvedInterWarehouseDocument(
                detail,
                trackedMovements,
                mikroWriteDbContext);
        }
    }

    private static async Task<List<STOK_HAREKETLERI>> LoadCompanyMovementRowsAsync(
        MikroDbContext dbContext,
        CompanyMovementDetailRequest request,
        CompanyMovementKind movementKind,
        CancellationToken cancellationToken)
    {
        var query = dbContext.STOK_HAREKETLERIs.Where(movement =>
            movement.sth_evrakno_seri == request.DocumentSerie &&
            movement.sth_evrakno_sira == request.DocumentOrderNo);

        query = movementKind switch
        {
            CompanyMovementKind.OutgoingShipment => query.Where(movement =>
                movement.sth_evraktip == CompanyDispatchDocumentType &&
                movement.sth_tip == OutgoingMovementType &&
                movement.sth_normal_iade == NormalMovement &&
                movement.sth_cikis_depo_no == request.WarehouseNo),

            CompanyMovementKind.PurchaseReturn => query.Where(movement =>
                movement.sth_evraktip == CompanyDispatchDocumentType &&
                movement.sth_tip == OutgoingMovementType &&
                movement.sth_normal_iade == ReturnMovement &&
                movement.sth_cikis_depo_no == request.WarehouseNo),

            CompanyMovementKind.IncomingShipment => query.Where(movement =>
                movement.sth_evraktip == ReceivingReceiptDocumentType &&
                movement.sth_tip == IncomingMovementType &&
                movement.sth_normal_iade == NormalMovement &&
                movement.sth_giris_depo_no == request.WarehouseNo),

            _ => throw new ArgumentOutOfRangeException(
                nameof(movementKind),
                movementKind,
                "Unsupported company movement kind.")
        };

        var movements = await query
            .OrderBy(movement => movement.sth_satirno)
            .ThenBy(movement => movement.sth_stok_kod)
            .ToListAsync(cancellationToken);

        if (movements.Count == 0)
        {
            throw new KeyNotFoundException("Company movement detail was not found.");
        }

        return movements;
    }

    private static async Task<List<STOK_HAREKETLERI>> LoadInterWarehouseRowsAsync(
        MikroDbContext dbContext,
        WarehouseShippingDetailRequest request,
        bool isReturn,
        CancellationToken cancellationToken)
    {
        var returnType = isReturn ? ReturnMovement : NormalMovement;
        var movements = await dbContext.STOK_HAREKETLERIs
            .Where(movement =>
                movement.sth_evraktip == InterWarehouseShipmentDocumentType &&
                movement.sth_normal_iade == returnType &&
                movement.sth_evrakno_seri == request.DocumentSerie &&
                movement.sth_evrakno_sira == request.DocumentOrderNo &&
                movement.sth_cikis_depo_no == request.WarehouseNo)
            .OrderBy(movement => movement.sth_satirno)
            .ThenBy(movement => movement.sth_stok_kod)
            .ToListAsync(cancellationToken);

        if (movements.Count == 0)
        {
            throw new KeyNotFoundException(
                isReturn
                    ? "Warehouse return detail was not found."
                    : "Inter warehouse shipment detail was not found.");
        }

        return movements;
    }

    private static CompanyMovementMetadata BuildCompanyMovementMetadata(
        IReadOnlyList<STOK_HAREKETLERI> trackedMovements)
    {
        var firstMovement = trackedMovements[0];

        return new CompanyMovementMetadata(
            firstMovement.sth_adres_no ?? 1);
    }

    private async Task<EDespatchCustomerInfo> LoadCustomerAsync(
        MikroDbContext dbContext,
        string customerCode,
        int? preferredAddressNo,
        CancellationToken cancellationToken)
    {
        var customer = await dbContext.CARI_HESAPLARs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.cari_kod == customerCode,
                cancellationToken);

        if (customer is null)
        {
            throw new KeyNotFoundException($"Customer was not found for e-despatch: {customerCode}");
        }

        var addressNo = preferredAddressNo
            ?? customer.cari_sevk_adres_no
            ?? customer.cari_fatura_adres_no
            ?? 1;
        var address = await dbContext.CARI_HESAP_ADRESLERIs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item =>
                    item.adr_cari_kod == customerCode &&
                    item.adr_adres_no == addressNo,
                cancellationToken);
        var taxNumber = NormalizeText(customer.cari_VergiKimlikNo, customer.cari_vdaire_no);

        if (string.IsNullOrWhiteSpace(taxNumber))
        {
            throw new InvalidOperationException(
                $"Customer tax number is required for e-despatch: {customerCode}");
        }

        return new EDespatchCustomerInfo(
            customerCode,
            JoinNonEmpty(customer.cari_unvan1, customer.cari_unvan2),
            taxNumber,
            ResolveTaxSchemeId(taxNumber),
            NormalizeText(customer.cari_unvan1),
            NormalizeText(customer.cari_vdaire_adi),
            BuildStreet(
                address?.adr_cadde,
                address?.adr_mahalle,
                address?.adr_sokak,
                address?.adr_Apt_No,
                address?.adr_Daire_No),
            NormalizeText(address?.adr_ilce),
            NormalizeText(address?.adr_il),
            NormalizeText(address?.adr_posta_kodu),
            NormalizeText(address?.adr_ulke),
            NormalizeText(address?.adr_tel_no1, customer.cari_CepTel),
            NormalizeText(address?.adr_tel_faxno),
            NormalizeText(customer.cari_EMail),
            NormalizeText(customer.cari_wwwadresi),
            NormalizeText(address?.adr_eirsaliye_alias));
    }

    private static async Task<EDespatchWarehouseInfo> LoadWarehouseAsync(
        MikroDbContext dbContext,
        int warehouseNo,
        CancellationToken cancellationToken)
    {
        var warehouse = await dbContext.DEPOLARs
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.dep_no == warehouseNo, cancellationToken);

        if (warehouse is null)
        {
            throw new KeyNotFoundException($"Warehouse was not found for e-despatch: {warehouseNo}");
        }

        return new EDespatchWarehouseInfo(
            warehouseNo,
            NormalizeText(warehouse.dep_adi),
            BuildStreet(
                warehouse.dep_cadde,
                warehouse.dep_mahalle,
                warehouse.dep_sokak,
                warehouse.dep_Apt_No,
                warehouse.dep_Daire_No),
            NormalizeText(warehouse.dep_Ilce),
            NormalizeText(warehouse.dep_Il),
            NormalizeText(warehouse.dep_posta_Kodu),
            NormalizeText(warehouse.dep_Ulke),
            NormalizeText(warehouse.dep_tel_no1),
            NormalizeText(warehouse.dep_tel_faxno),
            NormalizeText(warehouse.dep_yetkili_email));
    }

    private static UyumsoftDespatch.DespatchInfo BuildDespatchInfo(
        XElement despatchAdvice,
        string localDocumentId,
        string? targetCustomerAlias,
        string? targetCustomerTaxNumber,
        string? targetCustomerTitle)
    {
        var despatchInfo = new UyumsoftDespatch.DespatchInfo
        {
            DespatchAdvice = UyumsoftWcfClientHelper.DeserializeUbl<UyumsoftDespatch.DespatchAdviceType>(
                despatchAdvice.ToString(SaveOptions.DisableFormatting),
                "DespatchAdvice",
                DespatchNamespace),
            LocalDocumentId = localDocumentId,
            ExtraInformation = string.Empty
        };

        if (!string.IsNullOrWhiteSpace(targetCustomerTaxNumber) ||
            !string.IsNullOrWhiteSpace(targetCustomerAlias) ||
            !string.IsNullOrWhiteSpace(targetCustomerTitle))
        {
            despatchInfo.TargetCustomer = new UyumsoftDespatch.CustomerInfo
            {
                VknTckn = targetCustomerTaxNumber?.Trim(),
                Alias = targetCustomerAlias?.Trim(),
                Title = targetCustomerTitle?.Trim()
            };
        }

        return despatchInfo;
    }

    private async Task<string?> ResolveTargetCustomerAliasAsync(
        EDespatchCustomerInfo customer,
        EDespatchOptions config,
        CancellationToken cancellationToken)
    {
        var endpointOptions = ToEndpointOptions(config);
        var client = UyumsoftWcfClientHelper.CreateDespatchClient(endpointOptions);

        try
        {
            var response = await client.GetUserAliassesAsync(
                    UyumsoftWcfClientHelper.CreateDespatchUserInfo(endpointOptions),
                    customer.TaxNumber)
                .WaitAsync(cancellationToken);

            EnsureSucceeded(response, "e-despatch receiver alias lookup");

            var activeAliases = (response.Value?.DespatchReceiverboxAliases ?? [])
                .Where(alias => alias.Enabled && !string.IsNullOrWhiteSpace(alias.Alias))
                .Select(alias => alias.Alias)
                .ToArray();
            var resolvedAlias = SelectActiveDespatchReceiverAlias(customer.Alias, activeAliases);

            if (!string.Equals(resolvedAlias, customer.Alias, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "Mikro e-despatch alias was replaced with an active Uyumsoft receiver alias. CustomerCode={CustomerCode}, TaxNumber={TaxNumber}, MikroAlias={MikroAlias}, ResolvedAlias={ResolvedAlias}, ActiveAliasCount={ActiveAliasCount}",
                    customer.CustomerCode,
                    customer.TaxNumber,
                    customer.Alias,
                    resolvedAlias,
                    activeAliases.Length);
            }

            return resolvedAlias;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            UyumsoftWcfClientHelper.Abort(client);
            throw;
        }
        catch (Exception exception)
        {
            UyumsoftWcfClientHelper.Abort(client);
            logger.LogWarning(
                exception,
                "Uyumsoft e-despatch receiver alias lookup failed. TargetCustomer will be omitted so Uyumsoft can resolve the receiver from the UBL tax number. CustomerCode={CustomerCode}, TaxNumber={TaxNumber}",
                customer.CustomerCode,
                customer.TaxNumber);
            return null;
        }
        finally
        {
            await UyumsoftWcfClientHelper.CloseAsync(client);
        }
    }

    internal static string SelectActiveDespatchReceiverAlias(
        string? preferredAlias,
        IEnumerable<string?> activeAliases)
    {
        var aliases = activeAliases
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Select(alias => alias!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (aliases.Length == 0)
        {
            throw new InvalidOperationException(
                "Uyumsoft did not return an active e-despatch receiver alias for the customer.");
        }

        var matchingPreferredAlias = aliases.FirstOrDefault(alias =>
            string.Equals(alias, preferredAlias?.Trim(), StringComparison.OrdinalIgnoreCase));

        return matchingPreferredAlias ?? aliases[0];
    }

    private static XElement BuildCompanyMovementDespatchAdvice(
        CompanyMovementDetailDto detail,
        EDespatchWarehouseInfo warehouse,
        EDespatchCustomerInfo supplierCustomer,
        EDespatchCustomerInfo deliveryCustomer,
        SendEDespatchRequest request,
        ResolvedDespatchContacts contacts,
        IReadOnlyDictionary<string, string> primaryBarcodes,
        DateTime issueDateTime,
        string eDespatchDocumentNo,
        string eDespatchUuid,
        EDespatchOptions config)
    {
        var sellerName = JoinNonEmpty(supplierCustomer.DisplayName, warehouse.Name);

        return BuildDespatchAdviceCore(
            issueDateTime,
            eDespatchDocumentNo,
            eDespatchUuid,
            config,
            $"{detail.Header.WarehouseName} / {detail.Header.DocumentSerie} / {detail.Header.DocumentOrderNo}",
            detail.Items.Count,
            BuildSupplierPartyElement(
                "DespatchSupplierParty",
                supplierCustomer,
                contacts.Deliverer),
            BuildCustomerPartyElement(
                "DeliveryCustomerParty",
                deliveryCustomer,
                contacts.Receiver),
            BuildCustomerPartyElement(
                "BuyerCustomerParty",
                deliveryCustomer,
                null),
            BuildSupplierPartyElement(
                "SellerSupplierParty",
                supplierCustomer with { DisplayName = sellerName },
                null),
            BuildCustomerPartyElement(
                "OriginatorCustomerParty",
                deliveryCustomer,
                null),
            BuildShipmentElement(
                issueDateTime,
                deliveryCustomer.ToAddressInfo(config),
                request.Plaque,
                request.DriverNameSurname,
                request.DriverTckn),
            detail.Items.Select(
                item => BuildDespatchLineElement(
                    item.LineNo + 1,
                    item.StockCode,
                    item.StockName,
                    primaryBarcodes.GetValueOrDefault(item.StockCode) ?? string.Empty,
                    item.UnitName,
                    item.Quantity))
                .ToArray());
    }

    private static XElement BuildInterWarehouseDespatchAdvice(
        WarehouseShippingDetailDto detail,
        EDespatchCustomerInfo supplierCustomer,
        EDespatchWarehouseInfo sourceWarehouse,
        EDespatchWarehouseInfo targetWarehouse,
        SendEDespatchRequest request,
        ResolvedDespatchContacts contacts,
        IReadOnlyDictionary<string, string> primaryBarcodes,
        DateTime issueDateTime,
        string eDespatchDocumentNo,
        string eDespatchUuid,
        EDespatchOptions config)
    {
        var sourceParty = supplierCustomer with
        {
            DisplayName = JoinNonEmpty(supplierCustomer.DisplayName, sourceWarehouse.Name),
            Street = sourceWarehouse.Street,
            District = sourceWarehouse.District,
            Province = sourceWarehouse.Province,
            PostalCode = sourceWarehouse.PostalCode,
            CountryName = sourceWarehouse.CountryName,
            Telephone = sourceWarehouse.Telephone,
            Fax = sourceWarehouse.Fax,
            Email = sourceWarehouse.Email
        };
        var targetParty = supplierCustomer with
        {
            DisplayName = JoinNonEmpty(supplierCustomer.DisplayName, targetWarehouse.Name),
            Street = targetWarehouse.Street,
            District = targetWarehouse.District,
            Province = targetWarehouse.Province,
            PostalCode = targetWarehouse.PostalCode,
            CountryName = targetWarehouse.CountryName,
            Telephone = targetWarehouse.Telephone,
            Fax = targetWarehouse.Fax,
            Email = targetWarehouse.Email
        };

        return BuildDespatchAdviceCore(
            issueDateTime,
            eDespatchDocumentNo,
            eDespatchUuid,
            config,
            $"{detail.Header.SourceWarehouse} / {detail.Header.DocumentSerie} / {detail.Header.DocumentOrderNo}",
            detail.Items.Count,
            BuildSupplierPartyElement(
                "DespatchSupplierParty",
                sourceParty,
                contacts.Deliverer),
            BuildCustomerPartyElement(
                "DeliveryCustomerParty",
                targetParty,
                contacts.Receiver),
            BuildCustomerPartyElement(
                "BuyerCustomerParty",
                targetParty,
                null),
            BuildSupplierPartyElement(
                "SellerSupplierParty",
                sourceParty with { DisplayName = sourceWarehouse.Name },
                null),
            BuildCustomerPartyElement(
                "OriginatorCustomerParty",
                targetParty with { DisplayName = targetWarehouse.Name },
                null),
            BuildShipmentElement(
                issueDateTime,
                targetWarehouse.ToAddressInfo(config),
                request.Plaque,
                request.DriverNameSurname,
                request.DriverTckn),
            detail.Items.Select(
                item => BuildDespatchLineElement(
                    item.LineNo + 1,
                    item.StockCode,
                    item.StockName,
                    primaryBarcodes.GetValueOrDefault(item.StockCode) ?? string.Empty,
                    item.UnitName,
                    item.Quantity))
                .ToArray());
    }

    private static XElement BuildDespatchAdviceCore(
        DateTime issueDateTime,
        string eDespatchDocumentNo,
        string eDespatchUuid,
        EDespatchOptions config,
        string note,
        int lineCount,
        XElement despatchSupplierParty,
        XElement deliveryCustomerParty,
        XElement buyerCustomerParty,
        XElement sellerSupplierParty,
        XElement originatorCustomerParty,
        XElement shipment,
        IReadOnlyCollection<XElement> despatchLines)
    {
        var despatch = XNamespace.Get(DespatchNamespace);
        var aggregate = XNamespace.Get(AggregateNamespace);
        var basic = XNamespace.Get(BasicNamespace);

        return new XElement(
            despatch + "DespatchAdvice",
            new XAttribute(XNamespace.Xmlns + "cac", aggregate.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "cbc", basic.NamespaceName),
            new XElement(basic + "ProfileID", config.ProfileId),
            new XElement(basic + "ID", eDespatchDocumentNo),
            new XElement(basic + "CopyIndicator", "false"),
            new XElement(basic + "UUID", eDespatchUuid),
            new XElement(basic + "IssueDate", issueDateTime.ToString("yyyy-MM-dd")),
            new XElement(basic + "IssueTime", issueDateTime.ToString("HH:mm:ss")),
            new XElement(basic + "DespatchAdviceTypeCode", config.DespatchAdviceTypeCode),
            new XElement(basic + "Note", note),
            new XElement(basic + "LineCountNumeric", lineCount),
            new XElement(
                aggregate + "OrderReference",
                new XElement(basic + "ID", eDespatchDocumentNo),
                new XElement(basic + "IssueDate", issueDateTime.ToString("yyyy-MM-dd"))),
            despatchSupplierParty,
            deliveryCustomerParty,
            buyerCustomerParty,
            sellerSupplierParty,
            originatorCustomerParty,
            shipment,
            despatchLines);
    }

    private static XElement BuildSupplierPartyElement(
        string elementName,
        EDespatchCustomerInfo partyInfo,
        string? contactName)
    {
        var aggregate = XNamespace.Get(AggregateNamespace);
        var elements = new List<object>
        {
            BuildPartyElement(partyInfo)
        };

        if (!string.IsNullOrWhiteSpace(contactName))
        {
            var contactElement = BuildContactElement("DespatchContact", contactName, null, null, null, null);
            if (contactElement is not null)
            {
                elements.Add(contactElement!);
            }
        }

        return new XElement(aggregate + elementName, elements);
    }

    private static XElement BuildCustomerPartyElement(
        string elementName,
        EDespatchCustomerInfo partyInfo,
        string? contactName)
    {
        var aggregate = XNamespace.Get(AggregateNamespace);
        var elements = new List<object>
        {
            BuildPartyElement(partyInfo)
        };

        if (!string.IsNullOrWhiteSpace(contactName))
        {
            var contactElement = BuildContactElement("DeliveryContact", contactName, null, null, null, null);
            if (contactElement is not null)
            {
                elements.Add(contactElement!);
            }
        }

        return new XElement(aggregate + elementName, elements);
    }

    private static XElement BuildPartyElement(EDespatchCustomerInfo partyInfo)
    {
        var aggregate = XNamespace.Get(AggregateNamespace);
        var basic = XNamespace.Get(BasicNamespace);
        var partyElements = new List<object>();

        if (!string.IsNullOrWhiteSpace(partyInfo.Website))
        {
            partyElements.Add(new XElement(basic + "WebsiteURI", partyInfo.Website));
        }

        partyElements.Add(
            new XElement(
                aggregate + "PartyIdentification",
                new XElement(
                    basic + "ID",
                    new XAttribute("schemeID", partyInfo.TaxSchemeId),
                    partyInfo.TaxNumber)));
        partyElements.Add(
            new XElement(
                aggregate + "PartyName",
                new XElement(basic + "Name", partyInfo.DisplayName)));

        partyElements.Add(BuildAddressElement("PostalAddress", partyInfo.ToAddressInfo()));

        if (!string.IsNullOrWhiteSpace(partyInfo.TaxOffice))
        {
            partyElements.Add(
                new XElement(
                    aggregate + "PartyTaxScheme",
                    new XElement(
                        aggregate + "TaxScheme",
                        new XElement(basic + "Name", partyInfo.TaxOffice))));
        }

        var contactElement = BuildContactElement(
            "Contact",
            null,
            partyInfo.Telephone,
            partyInfo.Fax,
            partyInfo.Email,
            null);
        if (contactElement is not null)
        {
            partyElements.Add(contactElement!);
        }

        var personElement = BuildPartyPersonElement(partyInfo.TaxSchemeId, partyInfo.PersonName);
        if (personElement is not null)
        {
            partyElements.Add(personElement);
        }

        return new XElement(aggregate + "Party", partyElements);
    }

    internal static XElement? BuildPartyPersonElement(string taxSchemeId, string personName)
    {
        if (!string.Equals(taxSchemeId, "TCKN", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var aggregate = XNamespace.Get(AggregateNamespace);
        var basic = XNamespace.Get(BasicNamespace);
        var (firstName, familyName) = SplitPersonName(personName);

        return new XElement(
            aggregate + "Person",
            new XElement(basic + "FirstName", firstName),
            new XElement(basic + "FamilyName", familyName));
    }

    private static XElement BuildShipmentElement(
        DateTime issueDateTime,
        EDespatchAddressInfo deliveryAddress,
        string? plaque,
        string? driverNameSurname,
        string? driverTckn)
    {
        var aggregate = XNamespace.Get(AggregateNamespace);
        var basic = XNamespace.Get(BasicNamespace);
        var shipmentElements = new List<object>
        {
            new XElement(basic + "ID", "1")
        };
        var shipmentStage = BuildShipmentStageElement(plaque, driverNameSurname, driverTckn);
        if (shipmentStage is not null)
        {
            shipmentElements.Add(shipmentStage!);
        }

        shipmentElements.Add(
            new XElement(
                aggregate + "Delivery",
                new XElement(basic + "ID", "1"),
                BuildAddressElement("DeliveryAddress", deliveryAddress),
                new XElement(
                    aggregate + "Despatch",
                    new XElement(basic + "ID", "1"),
                    new XElement(basic + "ActualDespatchDate", issueDateTime.ToString("yyyy-MM-dd")),
                    new XElement(basic + "ActualDespatchTime", issueDateTime.ToString("HH:mm:ss")))));

        return new XElement(aggregate + "Shipment", shipmentElements);
    }

    private static XElement? BuildShipmentStageElement(
        string? plaque,
        string? driverNameSurname,
        string? driverTckn)
    {
        var aggregate = XNamespace.Get(AggregateNamespace);
        var basic = XNamespace.Get(BasicNamespace);
        var hasPlaque = !string.IsNullOrWhiteSpace(plaque);
        var hasDriver = !string.IsNullOrWhiteSpace(driverNameSurname);

        if (!hasPlaque && !hasDriver)
        {
            return null;
        }

        var elements = new List<object>();

        if (hasPlaque)
        {
            elements.Add(
                new XElement(
                    aggregate + "TransportMeans",
                    new XElement(
                        aggregate + "RoadTransport",
                        new XElement(
                            basic + "LicensePlateID",
                            new XAttribute("schemeID", "PLAKA"),
                            plaque!.Trim()))));
        }

        if (hasDriver)
        {
            var (firstName, familyName) = SplitPersonName(driverNameSurname!);
            var driverElements = new List<object>
            {
                new XElement(basic + "FirstName", firstName),
                new XElement(basic + "FamilyName", familyName)
            };

            if (!string.IsNullOrWhiteSpace(driverTckn))
            {
                driverElements.Add(
                    new XElement(
                        basic + "NationalityID",
                        new XAttribute("schemeID", "TCKN"),
                        driverTckn!.Trim()));
            }

            elements.Add(new XElement(aggregate + "DriverPerson", driverElements));
        }

        return new XElement(aggregate + "ShipmentStage", elements);
    }

    internal static XElement BuildDespatchLineElement(
        int lineNo,
        string stockCode,
        string stockName,
        string barcode,
        string unitName,
        double quantity)
    {
        var aggregate = XNamespace.Get(AggregateNamespace);
        var basic = XNamespace.Get(BasicNamespace);
        var unitCode = ResolveUnitCode(unitName);

        return new XElement(
            aggregate + "DespatchLine",
            new XElement(basic + "ID", lineNo),
            new XElement(basic + "Note", lineNo.ToString()),
            new XElement(
                basic + "DeliveredQuantity",
                new XAttribute("unitCode", unitCode),
                quantity),
            new XElement(
                basic + "OutstandingQuantity",
                new XAttribute("unitCode", unitCode),
                quantity),
            new XElement(basic + "OutstandingReason", "Stok Yok"),
            new XElement(
                basic + "OversupplyQuantity",
                new XAttribute("unitCode", unitCode),
                quantity),
            new XElement(
                aggregate + "OrderLineReference",
                new XElement(basic + "LineID", lineNo)),
            new XElement(
                aggregate + "Item",
                new XElement(
                    basic + "Description",
                    string.IsNullOrWhiteSpace(barcode) ? stockCode : barcode.Trim()),
                new XElement(basic + "Name", stockName),
                new XElement(
                    aggregate + "SellersItemIdentification",
                    new XElement(basic + "ID", stockCode))));
    }

    private static XElement BuildAddressElement(
        string elementName,
        EDespatchAddressInfo addressInfo)
    {
        var aggregate = XNamespace.Get(AggregateNamespace);
        var basic = XNamespace.Get(BasicNamespace);
        var elements = new List<object>();

        if (!string.IsNullOrWhiteSpace(addressInfo.Street))
        {
            elements.Add(new XElement(basic + "StreetName", addressInfo.Street));
        }

        if (!string.IsNullOrWhiteSpace(addressInfo.District))
        {
            elements.Add(new XElement(basic + "CitySubdivisionName", addressInfo.District));
        }

        if (!string.IsNullOrWhiteSpace(addressInfo.Province))
        {
            elements.Add(new XElement(basic + "CityName", addressInfo.Province));
        }

        if (!string.IsNullOrWhiteSpace(addressInfo.PostalCode))
        {
            elements.Add(new XElement(basic + "PostalZone", addressInfo.PostalCode));
        }

        elements.Add(
            new XElement(
                aggregate + "Country",
                new XElement(basic + "IdentificationCode", addressInfo.CountryCode),
                new XElement(basic + "Name", addressInfo.CountryName)));

        return new XElement(aggregate + elementName, elements);
    }

    internal static XElement? BuildContactElement(
        string elementName,
        string? name,
        string? telephone,
        string? telefax,
        string? email,
        string? note)
    {
        var aggregate = XNamespace.Get(AggregateNamespace);
        var basic = XNamespace.Get(BasicNamespace);
        var elements = new List<object>();

        if (!string.IsNullOrWhiteSpace(name))
        {
            elements.Add(new XElement(basic + "Name", name.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(telephone))
        {
            elements.Add(new XElement(basic + "Telephone", telephone.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(telefax))
        {
            elements.Add(new XElement(basic + "Telefax", telefax.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            elements.Add(new XElement(basic + "ElectronicMail", email.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(note))
        {
            elements.Add(new XElement(basic + "Note", note.Trim()));
        }

        return elements.Count == 0 ? null : new XElement(aggregate + elementName, elements);
    }

    private static async Task<string> ResolveOutboxDespatchIdAsync(
        EDespatchOptions config,
        string eDespatchDocumentNo,
        CancellationToken cancellationToken)
    {
        var client = UyumsoftWcfClientHelper.CreateDespatchClient(ToEndpointOptions(config));

        try
        {
            var response = await client.GetOutboxDespatchListAsync(
                UyumsoftWcfClientHelper.CreateDespatchUserInfo(ToEndpointOptions(config)),
                new UyumsoftDespatch.OutboxDespatchListQueryModel
                {
                    PageIndex = 0,
                    PageSize = 10,
                    IsOnlyNewReceiptAdvice = false,
                    DespatchNumbers = [eDespatchDocumentNo]
                });

            EnsureSucceeded(response, "e-despatch outbox list");

            var items = response.Value?.Items ?? [];
            var matchedItem = items.FirstOrDefault(item =>
                string.Equals(
                    item.DespatchNumber?.Trim(),
                    eDespatchDocumentNo,
                    StringComparison.OrdinalIgnoreCase));

            if (matchedItem is null && items.Length == 1)
            {
                matchedItem = items[0];
            }

            if (matchedItem is null)
            {
                throw new InvalidOperationException(
                    $"Could not find the sent e-despatch in Uyumsoft for document number {eDespatchDocumentNo}.");
            }

            if (string.IsNullOrWhiteSpace(matchedItem.DespatchId))
            {
                throw new InvalidOperationException(
                    $"Uyumsoft e-despatch list response does not contain a document id for {eDespatchDocumentNo}.");
            }

            return matchedItem.DespatchId.Trim();
        }
        catch
        {
            UyumsoftWcfClientHelper.Abort(client);
            throw;
        }
        finally
        {
            await UyumsoftWcfClientHelper.CloseAsync(client);
        }
    }

    private static async Task<bool> IsOutboxDocumentNumberUsedAsync(
        EDespatchOptions config,
        string eDespatchDocumentNo,
        CancellationToken cancellationToken)
    {
        var client = UyumsoftWcfClientHelper.CreateDespatchClient(ToEndpointOptions(config));

        try
        {
            var response = await client.GetOutboxDespatchListAsync(
                UyumsoftWcfClientHelper.CreateDespatchUserInfo(ToEndpointOptions(config)),
                new UyumsoftDespatch.OutboxDespatchListQueryModel
                {
                    PageIndex = 0,
                    PageSize = 10,
                    IsOnlyNewReceiptAdvice = false,
                    DespatchNumbers = [eDespatchDocumentNo]
                });

            EnsureSucceeded(response, "e-despatch document number availability");

            return (response.Value?.Items ?? []).Any(item =>
                string.Equals(
                    item.DespatchNumber?.Trim(),
                    eDespatchDocumentNo,
                    StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            UyumsoftWcfClientHelper.Abort(client);
            throw;
        }
        finally
        {
            await UyumsoftWcfClientHelper.CloseAsync(client);
        }
    }

    private static async Task<ServiceSendResult> SendToUyumsoftAsync(
        UyumsoftDespatch.DespatchInfo despatchInfo,
        EDespatchOptions config,
        CancellationToken cancellationToken)
    {
        var client = UyumsoftWcfClientHelper.CreateDespatchClient(ToEndpointOptions(config));

        try
        {
            var response = await client.SendDespatchAsync(
                UyumsoftWcfClientHelper.CreateDespatchUserInfo(ToEndpointOptions(config)),
                [despatchInfo]);

            EnsureSucceeded(response, "e-despatch");

            var identity = response.Value?.FirstOrDefault()
                           ?? throw new InvalidOperationException(
                               "Uyumsoft e-despatch response does not contain a document identity.");

            return new ServiceSendResult(
                identity.Id?.Trim() ?? string.Empty,
                identity.Number?.Trim() ?? string.Empty);
        }
        catch
        {
            UyumsoftWcfClientHelper.Abort(client);
            throw;
        }
        finally
        {
            await UyumsoftWcfClientHelper.CloseAsync(client);
        }
    }

    private static async Task<byte[]> GetOutboxDespatchPdfAsync(
        EDespatchOptions config,
        string despatchId,
        CancellationToken cancellationToken)
    {
        var client = UyumsoftWcfClientHelper.CreateDespatchClient(ToEndpointOptions(config));

        try
        {
            var response = await client.GetOutboxDespatchPdfAsync(
                UyumsoftWcfClientHelper.CreateDespatchUserInfo(ToEndpointOptions(config)),
                despatchId);

            EnsureSucceeded(response, "e-despatch PDF");

            var matchedItem = response.Value?.Items?.FirstOrDefault(item =>
            string.Equals(
                item.DespatchId?.Trim(),
                despatchId,
                StringComparison.OrdinalIgnoreCase));

            if (matchedItem is null && response.Value?.Items?.Length == 1)
            {
                matchedItem = response.Value.Items[0];
            }

            if (matchedItem?.Data is null || matchedItem.Data.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Uyumsoft e-despatch PDF response returned empty data for document id {despatchId}.");
            }

            return matchedItem.Data;
        }
        catch
        {
            UyumsoftWcfClientHelper.Abort(client);
            throw;
        }
        finally
        {
            await UyumsoftWcfClientHelper.CloseAsync(client);
        }
    }

    private static void EnsureSucceeded(UyumsoftDespatch.Response response, string operationName)
    {
        if (!response.IsSucceded)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(response.Message)
                    ? $"Uyumsoft {operationName} service rejected the request."
                    : response.Message);
        }
    }

    internal static UyumsoftServiceEndpointOptions ToEndpointOptions(EDespatchOptions config) =>
        new(
            config.EndpointUrl,
            string.Empty,
            config.Username,
            config.Password,
            "IBasicDespatchIntegration",
            config.TimeoutSeconds);

    private async Task<bool> TryMarkAsSentAsync(
        MikroDbContext context,
        IReadOnlyCollection<STOK_HAREKETLERI> trackedMovements,
        string eDespatchDocumentNo,
        string eDespatchUuid,
        SentMovementMetadata metadata,
        EDespatchDocumentType documentType,
        CancellationToken stoppingToken)
    {
        using var completionCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        completionCancellation.CancelAfter(TimeSpan.FromSeconds(PostSubmissionCompletionTimeoutSeconds));
        var cancellationToken = completionCancellation.Token;

        try
        {
            var now = DateTime.Now;
            var movementGuids = trackedMovements
                .Select(movement => movement.sth_Guid)
                .Distinct()
                .ToArray();

            if (movementGuids.Length == 0)
            {
                logger.LogWarning(
                    "E-despatch was sent but no Mikro movement row was available for local metadata update.");

                return false;
            }

            var documentNo = Truncate(eDespatchDocumentNo, 50);
            var uuid = Truncate(eDespatchUuid, 50);
            var plaque = string.IsNullOrWhiteSpace(metadata.Plaque)
                ? null
                : Truncate(metadata.Plaque.Trim(), 25);
            var deliverer = string.IsNullOrWhiteSpace(metadata.Deliverer)
                ? null
                : Truncate(metadata.Deliverer.Trim(), 25);
            var receiver = string.IsNullOrWhiteSpace(metadata.Receiver)
                ? null
                : Truncate(metadata.Receiver.Trim(), 25);
            var driverTckn = string.IsNullOrWhiteSpace(metadata.DriverTckn)
                ? null
                : Truncate(metadata.DriverTckn.Trim(), 25);

            for (var attempt = 1; attempt <= LocalMetadataUpdateAttemptCount; attempt++)
            {
                var currentRows = await context.STOK_HAREKETLERIs.AsNoTracking()
                    .Where(x => movementGuids.Contains(x.sth_Guid)).ToArrayAsync(cancellationToken);
                EnsureSnapshotMatches(
                    trackedMovements.Select(x => EDespatchMovementSnapshot.From(x, documentType)).ToArray(),
                    currentRows,
                    documentType);
                if (HasMatchingSentMarker(currentRows, documentNo, uuid)) return true;
                var pendingRows = currentRows.Where(x => !HasMatchingSentMarker([x], documentNo, uuid)).ToArray();
                int updatedCount;
                if (mikroWriteRoutingOptions.CurrentValue.EDespatchMarkAsSent == MikroWriteMode.MikroApi)
                {
                    var payload = new
                    {
                        evraklar = new[]
                        {
                            new
                            {
                                satirlar = pendingRows.Select(movement =>
                                    BuildSentMarkerApiRow(
                                        movement.sth_Guid,
                                        documentNo,
                                        uuid,
                                        plaque,
                                        deliverer,
                                        receiver,
                                        driverTckn)).ToArray()
                            }
                        }
                    };
                    var result = await mikroApiClient.PostWithMikroPayloadAsync<JsonElement>(
                        StockMovementUpdatePath,
                        payload,
                        cancellationToken);
                    if (result.IsError)
                    {
                        throw new InvalidOperationException(
                            result.ErrorMessage ?? "Mikro API e-despatch marker update failed.");
                    }

                    updatedCount = movementGuids.Length;
                }
                else
                {
                    updatedCount = await context.STOK_HAREKETLERIs
                        .Where(movement => movementGuids.Contains(movement.sth_Guid))
                        .ExecuteUpdateAsync(
                            setters => setters
                                .SetProperty(movement => movement.sth_kilitli, true)
                                .SetProperty(movement => movement.sth_lastup_user, MikroUserNo)
                                .SetProperty(movement => movement.sth_lastup_date, now)
                                .SetProperty(movement => movement.sth_belge_no, documentNo)
                                .SetProperty(movement => movement.sth_aciklama, uuid)
                                .SetProperty(movement => movement.sth_HareketGrupKodu1, movement => plaque ?? movement.sth_HareketGrupKodu1)
                                .SetProperty(movement => movement.sth_HareketGrupKodu2, movement => deliverer ?? movement.sth_HareketGrupKodu2)
                                .SetProperty(movement => movement.sth_HareketGrupKodu3, movement => receiver ?? movement.sth_HareketGrupKodu3)
                                .SetProperty(movement => movement.sth_ismerkezi_kodu, movement => driverTckn ?? movement.sth_ismerkezi_kodu),
                            cancellationToken);
                }

                var verifiedCount = await context.STOK_HAREKETLERIs
                    .AsNoTracking()
                    .Where(movement =>
                        movementGuids.Contains(movement.sth_Guid) &&
                        movement.sth_kilitli == true &&
                        movement.sth_belge_no == documentNo &&
                        movement.sth_aciklama == uuid)
                    .CountAsync(cancellationToken);

                if (verifiedCount == movementGuids.Length)
                {
                    foreach (var movement in trackedMovements)
                    {
                        movement.sth_kilitli = true;
                        movement.sth_lastup_user = MikroUserNo;
                        movement.sth_lastup_date = now;
                        movement.sth_belge_no = documentNo;
                        movement.sth_aciklama = uuid;

                        if (plaque is not null)
                        {
                            movement.sth_HareketGrupKodu1 = plaque;
                        }

                        if (deliverer is not null)
                        {
                            movement.sth_HareketGrupKodu2 = deliverer;
                        }

                        if (receiver is not null)
                        {
                            movement.sth_HareketGrupKodu3 = receiver;
                        }

                        if (driverTckn is not null)
                        {
                            movement.sth_ismerkezi_kodu = driverTckn;
                        }
                    }

                    return true;
                }

                logger.LogWarning(
                    "E-despatch local Mikro metadata update attempt {Attempt} affected {UpdatedCount} rows and verified {VerifiedCount} rows, expected {ExpectedCount}. EDespatchDocumentNo={EDespatchDocumentNo}, EDespatchUuid={EDespatchUuid}",
                    attempt,
                    updatedCount,
                    verifiedCount,
                    movementGuids.Length,
                    eDespatchDocumentNo,
                    eDespatchUuid);

                if (attempt < LocalMetadataUpdateAttemptCount)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
                }
            }

            return false;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
        catch (EDespatchMetadataConflictException) { throw; }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "E-despatch was sent but local Mikro metadata could not be updated.");

            return false;
        }
    }

    internal static Dictionary<string, object?> BuildSentMarkerApiRow(
        Guid movementGuid,
        string documentNo,
        string uuid,
        string? plaque,
        string? deliverer,
        string? receiver,
        string? driverTckn)
    {
        var row = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["sth_Guid"] = movementGuid,
            ["sth_kilitli"] = true,
            ["sth_belge_no"] = documentNo,
            ["sth_aciklama"] = uuid
        };

        AddIfNotNull(row, "sth_HareketGrupKodu1", plaque);
        AddIfNotNull(row, "sth_HareketGrupKodu2", deliverer);
        AddIfNotNull(row, "sth_HareketGrupKodu3", receiver);
        AddIfNotNull(row, "sth_ismerkezi_kodu", driverTckn);
        return row;
    }

    private static void AddIfNotNull(
        IDictionary<string, object?> row,
        string name,
        string? value)
    {
        if (value is not null)
        {
            row[name] = value;
        }
    }

    private static string BuildQueuedMikroMetadataWarning() =>
        "E-despatch was sent to Uyumsoft. Mikro metadata update was queued and will continue in the background; do not resend.";

    private async Task<string> BuildEDespatchDocumentNoAsync(
        int year,
        EDespatchOptions config,
        CancellationToken cancellationToken)
    {
        var nextSequence = await GetNextDocumentSequenceAsync(year, cancellationToken);

        for (var attempt = 0; attempt < DocumentNumberAvailabilityAttemptCount; attempt++)
        {
            if (nextSequence > 999_999_999)
            {
                break;
            }

            var candidate = $"{CommonEDespatchDocumentPrefix}{year}{nextSequence:D9}";
            if (!await IsOutboxDocumentNumberUsedAsync(config, candidate, cancellationToken))
            {
                return candidate;
            }

            logger.LogWarning(
                "E-despatch document number {EDespatchDocumentNo} already exists in Uyumsoft; trying the next sequence.",
                candidate);
            nextSequence++;
        }

        throw new InvalidOperationException(
            $"Could not allocate an unused e-despatch document number after {DocumentNumberAvailabilityAttemptCount} attempts.");
    }

    private async Task<IAsyncDisposable> AcquireDocumentNumberLockAsync(
        CancellationToken cancellationToken)
    {
        await LocalDocumentNumberLock.WaitAsync(cancellationToken);

        var connection = mikroWriteDbContext.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;

        try
        {
            if (closeConnection)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = """
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock
                    @Resource = @resource,
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Session',
                    @LockTimeout = @lockTimeout;
                SELECT @result;
                """;
            command.CommandTimeout = (DocumentNumberLockTimeoutMilliseconds / 1000) + 10;
            AddParameter(command, "@resource", DbType.String, DocumentNumberLockResource);
            AddParameter(
                command,
                "@lockTimeout",
                DbType.Int32,
                DocumentNumberLockTimeoutMilliseconds);

            var result = Convert.ToInt32(
                await command.ExecuteScalarAsync(cancellationToken));

            if (result < 0)
            {
                throw new TimeoutException(
                    $"E-despatch document number lock could not be acquired. SQL result: {result}.");
            }

            return new DocumentNumberLockLease(
                connection,
                closeConnection,
                LocalDocumentNumberLock,
                logger);
        }
        catch
        {
            if (closeConnection && connection.State != ConnectionState.Closed)
            {
                await connection.CloseAsync();
            }

            LocalDocumentNumberLock.Release();
            throw;
        }
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        DbType type,
        object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static void EnsureDeliveryAddressPostalCode(
        string postalCode,
        string targetDescription)
    {
        if (!string.IsNullOrWhiteSpace(postalCode))
        {
            return;
        }

        throw new ArgumentException(
            $"E-despatch delivery address postal code is required. Target={targetDescription}.");
    }

    private async Task<int> GetNextDocumentSequenceAsync(
        int year,
        CancellationToken cancellationToken)
    {
        var prefixWithYear = $"{CommonEDespatchDocumentPrefix}{year}";
        var latestMikroDocumentNo = await mikroWriteDbContext.STOK_HAREKETLERIs
            .AsNoTracking()
            .Where(movement =>
                movement.sth_belge_no != null &&
                movement.sth_belge_no.StartsWith(prefixWithYear) &&
                movement.sth_belge_no.Length == prefixWithYear.Length + 9)
            .OrderByDescending(movement => movement.sth_belge_no)
            .Select(movement => movement.sth_belge_no!)
            .FirstOrDefaultAsync(cancellationToken);
        var latestTrackedDocumentNo = await authDbContext.DocumentFlows
            .AsNoTracking()
            .Where(flow =>
                flow.ExternalDocumentNo != null &&
                flow.ExternalDocumentNo.StartsWith(prefixWithYear) &&
                flow.ExternalDocumentNo.Length == prefixWithYear.Length + 9)
            .OrderByDescending(flow => flow.ExternalDocumentNo)
            .Select(flow => flow.ExternalDocumentNo!)
            .FirstOrDefaultAsync(cancellationToken);

        var latestReservedDocumentNo = await authDbContext.EDespatchSubmissions.AsNoTracking()
            .Where(x => x.DocumentNo.StartsWith(prefixWithYear))
            .OrderByDescending(x => x.DocumentNo).Select(x => x.DocumentNo).FirstOrDefaultAsync(cancellationToken);
        var nextSequence = Math.Max(ParseEDespatchSequence(latestReservedDocumentNo, prefixWithYear), Math.Max(
            ParseEDespatchSequence(latestMikroDocumentNo, prefixWithYear),
            ParseEDespatchSequence(latestTrackedDocumentNo, prefixWithYear))) + 1;

        if (nextSequence > 999_999_999)
        {
            throw new InvalidOperationException(
                $"E-despatch document sequence exceeded the supported limit for {prefixWithYear}.");
        }

        return nextSequence;
    }

    internal static int ParseEDespatchSequence(string? documentNo, string prefixWithYear)
    {
        if (string.IsNullOrWhiteSpace(documentNo))
        {
            return 0;
        }

        if (!documentNo.StartsWith(prefixWithYear, StringComparison.OrdinalIgnoreCase) ||
            documentNo.Length != prefixWithYear.Length + 9)
        {
            throw new InvalidOperationException(
                $"Could not parse the latest e-despatch document number: {documentNo}");
        }

        var sequenceText = documentNo[prefixWithYear.Length..];
        if (!int.TryParse(sequenceText, out var sequence))
        {
            throw new InvalidOperationException(
                $"Could not parse the latest e-despatch document number: {documentNo}");
        }

        return sequence;
    }

    private static string BuildLocalDocumentId(SendEDespatchRequest request) =>
        $"{request.DocumentType}:{request.WarehouseNo}:{request.DocumentSerie.Trim()}:{request.DocumentOrderNo}";

    private static SendEDespatchRequest NormalizeDriverFields(SendEDespatchRequest request) =>
        request with
        {
            Plaque = request.Plaque?.Trim() ?? string.Empty,
            DriverNameSurname = request.DriverNameSurname?.Trim() ?? string.Empty,
            DriverTckn = request.DriverTckn?.Trim() ?? string.Empty,
            Deliverer = NormalizeNullableText(request.Deliverer),
            Receiver = NormalizeNullableText(request.Receiver)
        };

    internal static (string Deliverer, string Receiver) ResolveDespatchContactNames(
        string? requestDeliverer,
        string? requestReceiver,
        string? storedDeliverer,
        string? storedReceiver,
        string? driverNameSurname) =>
        (
            NormalizeText(requestDeliverer, storedDeliverer),
            NormalizeText(requestReceiver, storedReceiver, driverNameSurname));

    private static ResolvedDespatchContacts ResolveDespatchContacts(
        SendEDespatchRequest request,
        string? storedDeliverer,
        string? storedReceiver)
    {
        var (deliverer, receiver) = ResolveDespatchContactNames(
            request.Deliverer,
            request.Receiver,
            storedDeliverer,
            storedReceiver,
            request.DriverNameSurname);

        return new ResolvedDespatchContacts(deliverer, receiver);
    }

    private static string ResolveDriverField(string? overrideValue, string fallbackValue) =>
        string.IsNullOrWhiteSpace(overrideValue)
            ? fallbackValue.Trim()
            : overrideValue.Trim();

    private static async Task<IReadOnlyDictionary<string, string>> LoadPrimaryBarcodesAsync(
        MikroDbContext context,
        IEnumerable<string?> stockCodes,
        CancellationToken cancellationToken)
    {
        var normalizedStockCodes = stockCodes
            .Where(stockCode => !string.IsNullOrWhiteSpace(stockCode))
            .Select(stockCode => stockCode!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedStockCodes.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var rows = await context.BARKOD_TANIMLARIs
            .AsNoTracking()
            .Where(barcode =>
                barcode.bar_iptal != true &&
                barcode.bar_stokkodu != null &&
                normalizedStockCodes.Contains(barcode.bar_stokkodu) &&
                barcode.bar_kodu != null &&
                barcode.bar_kodu != string.Empty)
            .OrderByDescending(barcode => barcode.bar_master == true)
            .ThenBy(barcode => barcode.bar_birimpntr ?? byte.MaxValue)
            .ThenBy(barcode => barcode.bar_kodu)
            .Select(barcode => new
            {
                StockCode = barcode.bar_stokkodu!,
                Barcode = barcode.bar_kodu!
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.StockCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().Barcode.Trim(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static void Validate(SendEDespatchRequest request)
    {
        ValidateDocumentIdentity(
            request.WarehouseNo,
            request.DocumentSerie,
            request.DocumentOrderNo);

        if (request.DriverId == Guid.Empty)
        {
            throw new ArgumentException(
                "Driver id can not be empty.",
                nameof(request.DriverId));
        }

        if (request.ExpectedLineCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.ExpectedLineCount),
                "Expected line count must be greater than zero when provided.");
        }

        var requiresManualDriver = !request.DriverId.HasValue;

        if (requiresManualDriver && string.IsNullOrWhiteSpace(request.Plaque))
        {
            throw new ArgumentException(
                "Plaque is required for e-despatch.",
                nameof(request.Plaque));
        }

        if (requiresManualDriver && string.IsNullOrWhiteSpace(request.DriverNameSurname))
        {
            throw new ArgumentException(
                "Driver name surname is required for e-despatch.",
                nameof(request.DriverNameSurname));
        }

        if (requiresManualDriver && string.IsNullOrWhiteSpace(request.DriverTckn))
        {
            throw new ArgumentException(
                "Driver TCKN is required for e-despatch.",
                nameof(request.DriverTckn));
        }

        if (!string.IsNullOrWhiteSpace(request.DriverNameSurname) &&
            request.DriverNameSurname.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length < 2)
        {
            throw new ArgumentException(
                "Driver name surname must contain both name and surname.",
                nameof(request.DriverNameSurname));
        }

        if (!string.IsNullOrWhiteSpace(request.DriverTckn) &&
            (request.DriverTckn.Trim().Length != 11 || request.DriverTckn.Trim().Any(character => !char.IsDigit(character))))
        {
            throw new ArgumentException(
                "Driver TCKN must be 11 digits.",
                nameof(request.DriverTckn));
        }
    }

    private static void Validate(GetEDespatchPdfRequest request) =>
        ValidateDocumentIdentity(
            request.WarehouseNo,
            request.DocumentSerie,
            request.DocumentOrderNo);

    private static void Validate(GetEDespatchStatusRequest request) =>
        ValidateDocumentIdentity(
            request.WarehouseNo,
            request.DocumentSerie,
            request.DocumentOrderNo);

    private static void ValidateDocumentIdentity(
        int warehouseNo,
        string documentSerie,
        int documentOrderNo)
    {
        if (warehouseNo <= 0)
        {
            throw new ArgumentException(
                "Warehouse no must be greater than zero.",
                nameof(warehouseNo));
        }

        if (string.IsNullOrWhiteSpace(documentSerie))
        {
            throw new ArgumentException(
                "Document serie is required.",
                nameof(documentSerie));
        }

        if (documentOrderNo < 0)
        {
            throw new ArgumentException(
                "Document order no can not be negative.",
                nameof(documentOrderNo));
        }
    }

    private static void ValidateConfiguration(EDespatchOptions options)
    {
        if (options.TimeoutSeconds is < 1 or > 600)
        {
            throw new InvalidOperationException(
                "EDespatch:TimeoutSeconds must be between 1 and 600.");
        }

        if (string.IsNullOrWhiteSpace(options.EndpointUrl))
        {
            throw new InvalidOperationException(
                "EDespatch:EndpointUrl configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Username))
        {
            throw new InvalidOperationException(
                "EDespatch:Username configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Password))
        {
            throw new InvalidOperationException(
                "EDespatch:Password configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(options.SupplierCustomerCode))
        {
            throw new InvalidOperationException(
                "EDespatch:SupplierCustomerCode configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(options.ProfileId))
        {
            throw new InvalidOperationException(
                "EDespatch:ProfileId configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(options.DespatchAdviceTypeCode))
        {
            throw new InvalidOperationException(
                "EDespatch:DespatchAdviceTypeCode configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(options.CountryCode))
        {
            throw new InvalidOperationException(
                "EDespatch:CountryCode configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(options.CountryName))
        {
            throw new InvalidOperationException(
                "EDespatch:CountryName configuration is required.");
        }
    }

    private static string ResolveTaxSchemeId(string taxNumber) =>
        taxNumber.Length == 11 ? "TCKN" : "VKN";

    private static string ResolveUnitCode(string unitName)
    {
        var normalizedUnitName = unitName.Trim().ToUpperInvariant();

        return normalizedUnitName switch
        {
            "ADET" => "NIU",
            "AD" => "NIU",
            "KG" => "KGM",
            "KILOGRAM" => "KGM",
            "PAKET" => "PA",
            "PK" => "PA",
            "LT" => "LTR",
            "LITRE" => "LTR",
            _ => "NIU"
        };
    }

    internal static (string FirstName, string FamilyName) SplitPersonName(string value)
    {
        var parts = value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length switch
        {
            0 => (string.Empty, string.Empty),
            1 => (parts[0], parts[0]),
            _ => (string.Join(" ", parts[..^1]), parts[^1])
        };
    }

    private static string BuildStreet(params string?[] values) =>
        JoinNonEmpty(values);

    private static string JoinNonEmpty(params string?[] values) =>
        string.Join(
            " ",
            values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));

    private static string NormalizeText(params string?[] values) =>
        values
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?.Trim()
        ?? string.Empty;

    private static string? NormalizeNullableText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static SentDespatchInfo ExtractSentDespatchInfo(
        IReadOnlyCollection<STOK_HAREKETLERI> trackedMovements)
    {
        var firstMovement = trackedMovements.First();
        var eDespatchDocumentNo = firstMovement.sth_belge_no?.Trim() ?? string.Empty;
        var eDespatchUuid = firstMovement.sth_aciklama?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(eDespatchDocumentNo) ||
            !Guid.TryParse(eDespatchUuid, out _))
        {
            throw new InvalidOperationException(
                "E-despatch PDF is not available because the document has not been sent yet.");
        }

        return new SentDespatchInfo(
            eDespatchDocumentNo,
            eDespatchUuid);
    }

    private sealed record CompanyMovementMetadata(int AddressNo);

    private sealed record ResolvedCompanyMovementDocument(
        CompanyMovementDetailDto Detail,
        IReadOnlyCollection<STOK_HAREKETLERI> TrackedMovements,
        MikroDbContext Context,
        CompanyMovementMetadata Metadata);

    private sealed record ResolvedInterWarehouseDocument(
        WarehouseShippingDetailDto Detail,
        IReadOnlyCollection<STOK_HAREKETLERI> TrackedMovements,
        MikroDbContext Context);

    private sealed record EDespatchAddressInfo(
        string Street,
        string District,
        string Province,
        string PostalCode,
        string CountryCode,
        string CountryName);

    private sealed record EDespatchCustomerInfo(
        string CustomerCode,
        string DisplayName,
        string TaxNumber,
        string TaxSchemeId,
        string PersonName,
        string TaxOffice,
        string Street,
        string District,
        string Province,
        string PostalCode,
        string CountryName,
        string Telephone,
        string Fax,
        string Email,
        string Website,
        string Alias)
    {
        public EDespatchAddressInfo ToAddressInfo() =>
            new(
                Street,
                District,
                Province,
                PostalCode,
                "TR",
                string.IsNullOrWhiteSpace(CountryName) ? "TURKIYE" : CountryName);

        public EDespatchAddressInfo ToAddressInfo(EDespatchOptions options) =>
            new(
                Street,
                District,
                Province,
                PostalCode,
                options.CountryCode,
                string.IsNullOrWhiteSpace(CountryName) ? options.CountryName : CountryName);
    }

    private sealed record EDespatchWarehouseInfo(
        int WarehouseNo,
        string Name,
        string Street,
        string District,
        string Province,
        string PostalCode,
        string CountryName,
        string Telephone,
        string Fax,
        string Email)
    {
        public EDespatchAddressInfo ToAddressInfo(EDespatchOptions options) =>
            new(
                Street,
                District,
                Province,
                PostalCode,
                options.CountryCode,
                string.IsNullOrWhiteSpace(CountryName) ? options.CountryName : CountryName);
    }

    private sealed record ServiceSendResult(
        string ServiceDocumentId,
        string ServiceDocumentNumber);

    private sealed record SentDespatchInfo(
        string EDespatchDocumentNo,
        string EDespatchUuid);

    private sealed record SentMovementMetadata(
        string? Plaque,
        string? Deliverer,
        string? Receiver,
        string? DriverTckn);

    private sealed record ResolvedDespatchContacts(
        string Deliverer,
        string Receiver);

    private sealed class DocumentNumberLockLease(
        DbConnection connection,
        bool closeConnection,
        SemaphoreSlim localLock,
        ILogger<EDespatchService> leaseLogger)
        : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                if (connection.State == ConnectionState.Open)
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = """
                        EXEC sys.sp_releaseapplock
                            @Resource = @resource,
                            @LockOwner = 'Session';
                        """;
                    AddParameter(command, "@resource", DbType.String, DocumentNumberLockResource);
                    await command.ExecuteNonQueryAsync(CancellationToken.None);
                }
            }
            catch (Exception exception)
            {
                leaseLogger.LogWarning(
                    exception,
                    "E-despatch document number SQL application lock could not be released explicitly.");
            }
            finally
            {
                if (closeConnection && connection.State != ConnectionState.Closed)
                {
                    await connection.CloseAsync();
                }

                localLock.Release();
            }
        }
    }
}

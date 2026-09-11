using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Modules.Common.CompanyMovements;
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
    : IEDespatchService
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
    private const string CommonEDespatchDocumentPrefix = "FRM";
    private const string DocumentNumberLockResource = "FurpaMerkezApi:EDespatchDocumentNumber";
    private const int DocumentNumberLockTimeoutMilliseconds = 120_000;
    private const int DocumentNumberAvailabilityAttemptCount = 100;
    private const int PostSubmissionCompletionTimeoutSeconds = 120;
    private const int LocalMetadataUpdateAttemptCount = 2;
    private const string StockMovementUpdatePath = "/Api/apiMethods/DahiliStokHareketDuzeltV2";
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

        try
        {
            await using var documentNumberLock = await AcquireDocumentNumberLockAsync(cancellationToken);
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

            using var completionCancellation = new CancellationTokenSource(
                TimeSpan.FromSeconds(PostSubmissionCompletionTimeoutSeconds));
            await RecordEDespatchFlowAsync(
                request,
                DocumentFlowStatus.Succeeded,
                response.LocalMikroMetadataUpdated
                    ? "E-irsaliye Uyumsoft'a basariyla gonderildi."
                    : "E-irsaliye Uyumsoft'a gonderildi ancak Mikro gonderim bilgisi isaretlenemedi.",
                response.Warning,
                response,
                completionCancellation.Token);

            return response;
        }
        catch (Exception exception)
        {
            await RecordEDespatchFlowAsync(
                request,
                DocumentFlowStatus.Failed,
                "E-irsaliye gonderimi basarisiz oldu.",
                exception.Message,
                null,
                CancellationToken.None);
            throw;
        }
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
        var documentType = ToDocumentFlowType(request.DocumentType);
        var flowKey = DocumentFlowKeys.Create(
            documentType,
            request.WarehouseNo,
            request.DocumentSerie,
            request.DocumentOrderNo);

        var sentFlow = await authDbContext.DocumentFlows
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

        return new SentDespatchInfo(
            sentFlow.ExternalDocumentNo!.Trim(),
            sentFlow.ExternalUuid!.Trim());
    }

    private async Task<SendEDespatchResponse?> TryRecoverExistingSubmissionAsync(
        SendEDespatchRequest request,
        MikroDbContext context,
        IReadOnlyCollection<STOK_HAREKETLERI> trackedMovements,
        string documentSerie,
        int documentOrderNo,
        EDespatchOptions config,
        CancellationToken cancellationToken)
    {
        var localMarker = ResolveConsistentSentDespatchMarker(
            trackedMovements
                .Select(movement => (movement.sth_belge_no, movement.sth_aciklama))
                .ToArray());
        var sentDespatch = localMarker is null
            ? await GetTrackedSubmittedDespatchAsync(request, cancellationToken)
            : new SentDespatchInfo(localMarker.Value.DocumentNo, localMarker.Value.Uuid);
        if (sentDespatch is null)
        {
            return null;
        }

        var localMikroMetadataUpdated = await TryMarkAsSentAsync(
            context,
            trackedMovements,
            sentDespatch.EDespatchDocumentNo,
            sentDespatch.EDespatchUuid,
            new SentMovementMetadata(
                request.Plaque,
                null,
                request.DriverNameSurname,
                request.DriverTckn));

        logger.LogInformation(
            "Existing Uyumsoft e-despatch submission was recovered without resending. Document={DocumentSerie}/{DocumentOrderNo}, EDespatchDocumentNo={EDespatchDocumentNo}, LocalMikroMetadataUpdated={LocalMikroMetadataUpdated}",
            documentSerie,
            documentOrderNo,
            sentDespatch.EDespatchDocumentNo,
            localMikroMetadataUpdated);

        return new SendEDespatchResponse(
            request.DocumentType,
            documentSerie,
            documentOrderNo,
            sentDespatch.EDespatchDocumentNo,
            sentDespatch.EDespatchUuid,
            string.Empty,
            sentDespatch.EDespatchDocumentNo,
            DateTime.Now,
            config.EndpointUrl,
            localMikroMetadataUpdated,
            BuildLocalMikroMetadataWarning(localMikroMetadataUpdated));
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
        var currentMovementGuids = await BuildDocumentMovementQuery(context, request)
            .AsNoTracking()
            .Select(movement => movement.sth_Guid)
            .ToArrayAsync(cancellationToken);
        var matches = HaveSameMovementGuids(
            trackedMovements.Select(movement => movement.sth_Guid),
            currentMovementGuids);

        if (!matches)
        {
            logger.LogWarning(
                "E-despatch document movement set changed. DocumentType={DocumentType}, WarehouseNo={WarehouseNo}, Document={DocumentSerie}/{DocumentOrderNo}, PreparedCount={PreparedCount}, CurrentCount={CurrentCount}",
                request.DocumentType,
                request.WarehouseNo,
                request.DocumentSerie,
                request.DocumentOrderNo,
                trackedMovements.Count,
                currentMovementGuids.Length);
        }

        return matches;
    }

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

        var sentDespatch = request.DocumentType switch
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
        };

        var serviceDocumentId = await ResolveOutboxDespatchIdAsync(
            config,
            sentDespatch.EDespatchDocumentNo,
            cancellationToken);
        var pdfContent = await GetOutboxDespatchPdfAsync(
            config,
            serviceDocumentId,
            cancellationToken);

        return new GetEDespatchPdfResponse(
            $"{sentDespatch.EDespatchDocumentNo}.pdf",
            pdfContent);
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
        var config = options.Value;
        var recoveredResponse = await TryRecoverExistingSubmissionAsync(
            request,
            document.Context,
            document.TrackedMovements,
            document.Detail.Header.DocumentSerie,
            document.Detail.Header.DocumentOrderNo,
            config,
            cancellationToken);
        if (recoveredResponse is not null)
        {
            return recoveredResponse;
        }

        var now = DateTime.Now;
        var eDespatchDocumentNo = await BuildEDespatchDocumentNoAsync(
            now.Year,
            config,
            cancellationToken);
        var eDespatchUuid = Guid.NewGuid().ToString();
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
        var despatchInfo = BuildDespatchInfo(
            BuildCompanyMovementDespatchAdvice(
                document.Detail,
                sourceWarehouse,
                supplierCustomer,
                deliveryCustomer,
                request,
                now,
                eDespatchDocumentNo,
                eDespatchUuid,
                config),
            BuildLocalDocumentId(request),
            resolvedDeliveryAlias,
            resolvedDeliveryAlias is null ? null : deliveryCustomer.TaxNumber,
            resolvedDeliveryAlias is null ? null : deliveryCustomer.DisplayName);
        await EnsureDocumentMovementSetUnchangedAsync(
            request,
            document.Context,
            document.TrackedMovements,
            cancellationToken);
        var serviceResult = await SendToUyumsoftAsync(
            despatchInfo,
            config,
            cancellationToken);

        var trackedMetadataUpdated = await TryMarkAsSentAsync(
            document.Context,
            document.TrackedMovements,
            eDespatchDocumentNo,
            eDespatchUuid,
            new SentMovementMetadata(
                request.Plaque,
                null,
                request.DriverNameSurname,
                request.DriverTckn));
        var localMikroMetadataUpdated = trackedMetadataUpdated &&
            await TryDocumentMovementSetMatchesAfterSubmissionAsync(
                request,
                document.Context,
                document.TrackedMovements);

        return new SendEDespatchResponse(
            request.DocumentType,
            document.Detail.Header.DocumentSerie,
            document.Detail.Header.DocumentOrderNo,
            eDespatchDocumentNo,
            eDespatchUuid,
            serviceResult.ServiceDocumentId,
            serviceResult.ServiceDocumentNumber,
            now,
            config.EndpointUrl,
            localMikroMetadataUpdated,
            BuildLocalMikroMetadataWarning(localMikroMetadataUpdated));
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
        var config = options.Value;
        var recoveredResponse = await TryRecoverExistingSubmissionAsync(
            request,
            document.Context,
            document.TrackedMovements,
            document.Detail.Header.DocumentSerie,
            document.Detail.Header.DocumentOrderNo,
            config,
            cancellationToken);
        if (recoveredResponse is not null)
        {
            return recoveredResponse;
        }

        var now = DateTime.Now;
        var eDespatchDocumentNo = await BuildEDespatchDocumentNoAsync(
            now.Year,
            config,
            cancellationToken);
        var eDespatchUuid = Guid.NewGuid().ToString();
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
        var despatchInfo = BuildDespatchInfo(
            BuildInterWarehouseDespatchAdvice(
                document.Detail,
                supplierCustomer,
                sourceWarehouse,
                targetWarehouse,
                request,
                now,
                eDespatchDocumentNo,
                eDespatchUuid,
                config),
            BuildLocalDocumentId(request),
            null,
            null,
            null);
        await EnsureDocumentMovementSetUnchangedAsync(
            request,
            document.Context,
            document.TrackedMovements,
            cancellationToken);
        var serviceResult = await SendToUyumsoftAsync(
            despatchInfo,
            config,
            cancellationToken);

        var trackedMetadataUpdated = await TryMarkAsSentAsync(
            document.Context,
            document.TrackedMovements,
            eDespatchDocumentNo,
            eDespatchUuid,
            new SentMovementMetadata(
                request.Plaque,
                null,
                request.DriverNameSurname,
                request.DriverTckn));
        var localMikroMetadataUpdated = trackedMetadataUpdated &&
            await TryDocumentMovementSetMatchesAfterSubmissionAsync(
                request,
                document.Context,
                document.TrackedMovements);

        return new SendEDespatchResponse(
            request.DocumentType,
            document.Detail.Header.DocumentSerie,
            document.Detail.Header.DocumentOrderNo,
            eDespatchDocumentNo,
            eDespatchUuid,
            serviceResult.ServiceDocumentId,
            serviceResult.ServiceDocumentNumber,
            now,
            config.EndpointUrl,
            localMikroMetadataUpdated,
            BuildLocalMikroMetadataWarning(localMikroMetadataUpdated));
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
                null),
            BuildCustomerPartyElement(
                "DeliveryCustomerParty",
                deliveryCustomer,
                null),
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
                null),
            BuildCustomerPartyElement(
                "DeliveryCustomerParty",
                targetParty,
                null),
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

    private static XElement BuildDespatchLineElement(
        int lineNo,
        string stockCode,
        string stockName,
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
                new XElement(basic + "Description", stockCode),
                new XElement(basic + "Name", stockName)));
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

    private static XElement? BuildContactElement(
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

    private static UyumsoftServiceEndpointOptions ToEndpointOptions(EDespatchOptions config) =>
        new(
            config.EndpointUrl,
            string.Empty,
            config.Username,
            config.Password,
            "IBasicDespatchIntegration");

    private async Task<bool> TryMarkAsSentAsync(
        MikroDbContext context,
        IReadOnlyCollection<STOK_HAREKETLERI> trackedMovements,
        string eDespatchDocumentNo,
        string eDespatchUuid,
        SentMovementMetadata metadata)
    {
        using var completionCancellation = new CancellationTokenSource(
            TimeSpan.FromSeconds(PostSubmissionCompletionTimeoutSeconds));
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
                int updatedCount;
                if (mikroWriteRoutingOptions.CurrentValue.EDespatchMarkAsSent == MikroWriteMode.MikroApi)
                {
                    var payload = new
                    {
                        evraklar = new[]
                        {
                            new
                            {
                                satirlar = trackedMovements.Select(movement =>
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

    private static string? BuildLocalMikroMetadataWarning(bool localMikroMetadataUpdated) =>
        localMikroMetadataUpdated
            ? null
            : "E-despatch was sent to Uyumsoft, but Mikro STOK_HAREKETLERI metadata could not be updated. Do not resend; use the returned FRM document number and ETTN to repair the local document metadata.";

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

        var nextSequence = Math.Max(
            ParseEDespatchSequence(latestMikroDocumentNo, prefixWithYear),
            ParseEDespatchSequence(latestTrackedDocumentNo, prefixWithYear)) + 1;

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
            DriverTckn = request.DriverTckn?.Trim() ?? string.Empty
        };

    private static string ResolveDriverField(string? overrideValue, string fallbackValue) =>
        string.IsNullOrWhiteSpace(overrideValue)
            ? fallbackValue.Trim()
            : overrideValue.Trim();

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

using System.Globalization;
using System.ServiceModel;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.DepolarArasiSevkler.Create;
using FurpaMerkezApi.Infrastructure.Persistence.Axata;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AxataExt = FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu.ServiceReferences.Ext;
using AxataMain = FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu.ServiceReferences.Main;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal sealed class AxataOutboundDeliveryImportService(
    IOptionsMonitor<AxataSynchronizationOptions> options,
    MikroWriteDbContext mikroWriteDbContext,
    ICreateInterWarehouseShipmentUseCase createInterWarehouseShipmentUseCase,
    AxataDbContext? axataDbContext = null)
    : IAxataOutboundDeliveryImportService,
        IAxataIntegrationAuditService
{
    private const string PendingStatus = "0";
    private const string CompletedStatus = "1";
    private const string C01MovementType = "C01";
    private const string C02MovementType = "C02";
    private const string C03MovementType = "C03";
    private const string C04LegacyMovementType = "C4";
    private const string CompanyCode = "01";
    private const string WarehouseCode = "01";
    private const int TransitWarehouseNo = 60;
    private const int DefaultTake = 20;
    private const int MaxTake = 200;
    private const string FetchOperationName = "getOutBoundDeliveryList";
    private const string AckOperationName = "updIntegrationTable";
    private const int DefaultWarehouseOrderSourceWarehouseNo = 50;
    private const double QuantityTolerance = 0.000001d;

    private static readonly IReadOnlyCollection<string> AuditMovementTypes =
    [
        C01MovementType,
        C02MovementType,
        C03MovementType,
        C04LegacyMovementType
    ];

    public async Task<AxataOutboundDeliveryQueuePreviewDto> PreviewOutboundDeliveriesAsync(
        AxataOutboundDeliveryQueuePreviewRequest request,
        CancellationToken cancellationToken)
    {
        var movementType = ResolveOutboundDeliveryMovementType(request.MovementType);
        var take = NormalizeTake(request.Take);
        var documents = await FetchPendingOutboundDeliveriesAsync(movementType, cancellationToken);
        var selectedDocuments = documents
            .Take(take)
            .ToArray();
        var hasLiveImport = movementType.Equals(C01MovementType, StringComparison.OrdinalIgnoreCase);

        return new AxataOutboundDeliveryQueuePreviewDto(
            movementType,
            PendingStatus,
            DateTime.UtcNow,
            documents.Count,
            selectedDocuments.Length,
            selectedDocuments.Sum(document => document.Lines.Count),
            selectedDocuments.Sum(document => document.Lines.Sum(line => line.Quantity)),
            selectedDocuments.Select(document => ToQueuePreviewDocumentDto(document, hasLiveImport)).ToArray(),
            hasLiveImport
                ? [
                    "AXATA outbound delivery kuyrugu canli servisten okundu.",
                    "C01 icin detayli Mikro eslesme ve import kontrolu live/axata/outbound-deliveries/c01/preview endpoint'inde yapilir."
                ]
                : [
                    "AXATA outbound delivery kuyrugu canli servisten okundu.",
                    "Bu hareket tipi icin su an sadece kuyruk preview vardir; Mikro'ya yazma ve AXATA ack akisi acik degildir."
                ]);
    }

    public async Task<AxataIntegrationAuditDto> GetOverviewAsync(
        AxataIntegrationAuditRequest request,
        CancellationToken cancellationToken)
    {
        var (startDate, endDate) = ResolveDateRange(request.StartDate, request.EndDate);
        var take = NormalizeTake(request.Take);
        var outboundDeliveryStatuses = ResolveAuditOutboundDeliveryStatuses(request.Statuses);
        var outboundDeliveryStatusLabel = FormatStatuses(outboundDeliveryStatuses);
        var warehouseOrderSourceWarehouseNos = ResolveWarehouseOrderSourceWarehouseNos(request.WarehouseNo);
        var warehouseAudit = await GetWarehouseOrderAuditAsync(
            startDate,
            endDate,
            warehouseOrderSourceWarehouseNos,
            request.DocumentSerie,
            request.DocumentOrderNo,
            take,
            cancellationToken);
        var orderWorkflowAudit = await GetOrderWorkflowAuditAsync(
            startDate,
            endDate,
            warehouseOrderSourceWarehouseNos,
            request.DocumentSerie,
            request.DocumentOrderNo,
            take,
            cancellationToken);

        var movementDocuments = new Dictionary<string, IReadOnlyCollection<AxataOutboundDeliveryDocument>>(
            StringComparer.OrdinalIgnoreCase);
        var movementFetchErrors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var movementType in AuditMovementTypes)
        {
            var fetchResult = await TryFetchOutboundDeliveriesForAuditAsync(
                movementType,
                outboundDeliveryStatuses,
                startDate,
                endDate,
                warehouseOrderSourceWarehouseNos,
                request.DocumentSerie,
                request.DocumentOrderNo,
                cancellationToken);
            if (!fetchResult.IsSuccess)
            {
                movementFetchErrors[movementType] = fetchResult.ErrorMessage
                    ?? "AXATA fetch failed.";
                if (fetchResult.Documents.Count == 0)
                {
                    movementDocuments[movementType] = Array.Empty<AxataOutboundDeliveryDocument>();
                    continue;
                }
            }

            movementDocuments[movementType] = fetchResult.Documents;
        }

        var c01Analyses = await AnalyzeC01DocumentsAsync(
            movementDocuments.TryGetValue(C01MovementType, out var c01Documents)
                ? c01Documents
                : Array.Empty<AxataOutboundDeliveryDocument>(),
            cancellationToken);

        var c01OutboundDeliveryDocuments = c01Analyses
            .Select(ToPendingOutboundDeliveryDto)
            .ToArray();

        var axataOutboundDeliveryDtos = new List<AxataPendingOutboundDeliveryDto>();
        axataOutboundDeliveryDtos.AddRange(c01OutboundDeliveryDocuments);

        foreach (var movementGroup in movementDocuments.Where(item => item.Key != C01MovementType))
        {
            axataOutboundDeliveryDtos.AddRange(movementGroup.Value.Select(document => ToQueueOnlyPendingDto(document)));
        }

        var pendingDeliveryDtos = axataOutboundDeliveryDtos
            .Where(document => IsPendingStatus(document.Status))
            .ToArray();
        var sentMissingMikroShipmentDocuments = c01Analyses
            .Where(IsAxataCompletedC01ShipmentMissingMikro)
            .Select(ToAxataSourceMissingShipmentDto)
            .ToArray();
        var sentShipmentDifferenceDocuments = warehouseAudit.SentShipmentDifferenceDocuments.ToArray();
        var mikroMarkedSentMissingAxataOutboundDeliveryDocuments = warehouseAudit.SentMissingMikroShipmentDocuments
            .Where(document => !HasMatchingAxataOutboundDelivery(document, axataOutboundDeliveryDtos))
            .ToArray();
        var cancelledOutboundDeliveryDtos = axataOutboundDeliveryDtos
            .Where(document => document.IsCancelled)
            .ToArray();

        var movementSummaries = AuditMovementTypes
            .Select(movementType =>
            {
                var movementDocumentsForSummary = axataOutboundDeliveryDtos
                    .Where(document => document.MovementType.Equals(movementType, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                var hasFetchError = movementFetchErrors.TryGetValue(movementType, out var fetchError);
                var isC01 = movementType == C01MovementType;

                return new AxataOutboundDeliveryMovementSummaryDto(
                    movementType,
                    hasFetchError ? $"FetchFailed: {outboundDeliveryStatusLabel}" : outboundDeliveryStatusLabel,
                    movementDocumentsForSummary.Length,
                    movementDocumentsForSummary.Sum(document => document.LineCount),
                    movementDocumentsForSummary.Sum(document => document.Quantity),
                    isC01
                        ? movementDocumentsForSummary.Count(document =>
                            IsCompletedStatus(document.Status) &&
                            !document.IsCancelled &&
                            document.Quantity > QuantityTolerance &&
                            document.ExistingLinkedMovementLineCount == 0)
                        : 0,
                    isC01
                        ? movementDocumentsForSummary.Count(document => document.MikroCheckState == "MikroShipmentExistsPendingAck")
                        : 0,
                    hasFetchError
                        ? $"AXATA fetch failed: {fetchError}"
                        : isC01
                        ? "AXATA SQL ENT006/ENT007 + C01 order and shipment link check"
                        : "AXATA SQL ENT006/ENT007 queue check");
            })
            .ToArray();

        var c01MissingInMikroDocumentCount = c01OutboundDeliveryDocuments.Count(document =>
            IsCompletedStatus(document.Status) &&
            !document.IsCancelled &&
            document.Quantity > QuantityTolerance &&
            document.ExistingLinkedMovementLineCount == 0);
        var c01MikroExistsPendingAckDocumentCount = c01OutboundDeliveryDocuments.Count(document =>
            document.MikroCheckState == "MikroShipmentExistsPendingAck");
        var summary = new AxataIntegrationAuditSummaryDto(
            warehouseAudit.DocumentCount,
            warehouseAudit.SentDocumentCount,
            warehouseAudit.PartiallySentDocumentCount,
            warehouseAudit.UnsentDocumentCount,
            sentMissingMikroShipmentDocuments.Length,
            sentMissingMikroShipmentDocuments.Sum(document => document.MissingMovementLinkLineCount),
            sentMissingMikroShipmentDocuments.Sum(document => document.MissingMovementLinkQuantity),
            sentMissingMikroShipmentDocuments.Length,
            sentMissingMikroShipmentDocuments.Sum(document => document.MissingMovementLinkLineCount),
            sentMissingMikroShipmentDocuments.Sum(document => document.MissingMovementLinkQuantity),
            mikroMarkedSentMissingAxataOutboundDeliveryDocuments.Length,
            mikroMarkedSentMissingAxataOutboundDeliveryDocuments.Sum(document => document.MissingMovementLinkLineCount),
            mikroMarkedSentMissingAxataOutboundDeliveryDocuments.Sum(document => document.MissingMovementLinkQuantity),
            sentShipmentDifferenceDocuments.Length,
            sentShipmentDifferenceDocuments.Sum(document => document.DifferenceLineCount),
            sentShipmentDifferenceDocuments.Sum(document => document.DifferenceQuantity),
            pendingDeliveryDtos.Length,
            pendingDeliveryDtos.Sum(document => document.LineCount),
            pendingDeliveryDtos.Sum(document => document.Quantity),
            c01OutboundDeliveryDocuments.Count(document => IsPendingStatus(document.Status)),
            c01MissingInMikroDocumentCount,
            c01MikroExistsPendingAckDocumentCount,
            axataOutboundDeliveryDtos.Count,
            axataOutboundDeliveryDtos.Sum(document => document.LineCount),
            axataOutboundDeliveryDtos.Sum(document => document.Quantity),
            axataOutboundDeliveryDtos.Count(document => IsCompletedStatus(document.Status)),
            cancelledOutboundDeliveryDtos.Length,
            cancelledOutboundDeliveryDtos.Sum(document => document.LineCount),
            cancelledOutboundDeliveryDtos.Sum(document => document.Quantity),
            axataOutboundDeliveryDtos.Count(document => document.LineCount == 0));

        var interventionCandidates = axataOutboundDeliveryDtos
            .Where(document => document.CanIntervene)
            .OrderBy(document => document.AxataSequenceNo)
            .Take(take)
            .ToArray();
        var returnedPendingDocuments = pendingDeliveryDtos
            .OrderBy(document => document.MovementType)
            .ThenBy(document => document.AxataSequenceNo)
            .Take(take)
            .ToArray();
        var returnedOutboundDeliveries = axataOutboundDeliveryDtos
            .OrderBy(document => document.MovementType)
            .ThenBy(document => document.Status)
            .ThenBy(document => document.AxataSequenceNo)
            .Take(take)
            .ToArray();
        var isInSync = summary.UnsentWarehouseOrderDocumentCount == 0 &&
                       summary.PartiallySentWarehouseOrderDocumentCount == 0 &&
                       summary.SentWarehouseOrderMissingMikroShipmentDocumentCount == 0 &&
                       summary.SentWarehouseOrderShipmentDifferenceDocumentCount == 0 &&
                       summary.PendingOutboundDeliveryDocumentCount == 0 &&
                       summary.AxataEmptyOutboundDeliveryDocumentCount == 0 &&
                       orderWorkflowAudit.Summary.AxataOrderMissingDocumentCount == 0 &&
                       orderWorkflowAudit.Summary.AxataOrderUnknownDocumentCount == 0 &&
                       orderWorkflowAudit.Summary.WaitingForMikroTransferDocumentCount == 0 &&
                       orderWorkflowAudit.Summary.PartiallyLinkedInMikroDocumentCount == 0 &&
                       orderWorkflowAudit.Summary.ManualActionRequiredDocumentCount == 0 &&
                       orderWorkflowAudit.Summary.FullySynchronizedDocumentCount ==
                       orderWorkflowAudit.Summary.MikroOrderDocumentCount &&
                       movementFetchErrors.Count == 0;
        var operations = BuildAuditOperations(summary);
        var notes = new List<string>
        {
            $"Siparis kontrolu sadece AXATA kaynak/cikis depo(lar)i ({FormatWarehouseNos(warehouseOrderSourceWarehouseNos)}) icin ssip_cikdepo uzerinden yapilir.",
            "Mikro depolar arasi siparislerdeki ssip_special1 worker basari bayragi kontrol edilir.",
            "Audit tarih filtresi ssip_tarih uzerindedir; ssip_lastup_date problem listelerinde en yeni guncellenen belgeyi one almak icin kullanilir.",
            "Ana sevk donus kontrolu AXATA C01 ENT006/ENT007 kaynaklidir; pozitif miktarli, iptal olmayan AXATA sevki Mikro STOK_HAREKETLERI_EK.sth_subesip_uid linki yoksa eksik kabul edilir.",
            axataDbContext is null
                ? $"Sevk kontrolu AXATA getOutBoundDeliveryListAsync status {outboundDeliveryStatusLabel} kuyrugundan okunur; AxataConnection tanimli degilse WCF fallback kullanilir."
                : $"Sevk kontrolu AxataConnection uzerinden AXATA SQL ENT006/ENT007 status {outboundDeliveryStatusLabel} kayitlarindan okunur; WCF canli servis import/ack icin kullanilir.",
            "AXATA S06IPTKOD dolu olan veya S06STTU=3 ve toplam sevk miktari 0 olan belgeler iptal/zero-quantity sevk olarak ayrilir; Mikro'ya sevk fisi beklenmez.",
            "Mikro ssip_special1=1 olup AXATA outbound delivery kaydi bulunmayan belgeler ikincil tutarsizlik olarak ozetlenir; ana eksik sevk alarmi sayilmaz.",
            "pendingOutboundDeliveries yalnizca Status=0 bekleyen AXATA sevklerini dondurur; axataOutboundDeliveries secili status kaynak evrenini dondurur.",
            "C01 mikroCheckState=Synchronized ise AXATA Status=1 ve Mikro sevk linki mevcut demektir; UI bunu tamamlanmis kabul etmeli ve manuel islem gostermemelidir.",
            "C01 mikroCheckState=MikroShipmentExistsPendingAck ise AXATA Status=0 iken Mikro sevk linki zaten vardir; duplicate fis olusturulmadan sadece AXATA ack aksiyonu dusunulebilir.",
            "C01 icin Mikro siparis satiri ve STOK_HAREKETLERI_EK linki kontrol edilir; diger hareket tipleri bu raporda kuyruk seviyesinde izlenir.",
            "workflowSummary ve orderLifecycles Mikro siparisini baslangic kabul eder; AXATA ENT000/ENT001 siparisi ve bu siparise ait tum C01 ENT006/ENT007 sevkleri, sevk tarihinden bagimsiz olarak izlenir."
        };
        notes.AddRange(movementFetchErrors.Select(error =>
            $"AXATA {error.Key} status {outboundDeliveryStatusLabel} sevk kuyrugu okunamadi: {error.Value}"));

        return new AxataIntegrationAuditDto(
            isInSync,
            DateTime.UtcNow,
            startDate,
            endDate,
            request.WarehouseNo,
            summary,
            orderWorkflowAudit.Summary,
            orderWorkflowAudit.Documents,
            movementSummaries,
            warehouseAudit.UnsyncedDocuments,
            sentMissingMikroShipmentDocuments.Take(take).ToArray(),
            sentShipmentDifferenceDocuments.Take(take).ToArray(),
            returnedPendingDocuments,
            returnedOutboundDeliveries,
            interventionCandidates,
            operations,
            notes);
    }

    public async Task<AxataOutboundDeliveryImportPreviewDto> PreviewC01Async(
        AxataOutboundDeliveryImportPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var take = NormalizeTake(request.Take);
        var documents = await FetchPendingOutboundDeliveriesAsync(C01MovementType, cancellationToken);
        var selectedDocuments = documents.Take(take).ToArray();
        var analyses = await AnalyzeC01DocumentsAsync(selectedDocuments, cancellationToken);

        return new AxataOutboundDeliveryImportPreviewDto(
            C01MovementType,
            PendingStatus,
            DateTime.UtcNow,
            documents.Count,
            analyses.Count,
            selectedDocuments.Sum(document => document.Lines.Count),
            selectedDocuments.Sum(document => document.Lines.Sum(line => line.Quantity)),
            analyses.Select(analysis => analysis.ImportDto).ToArray(),
            [
                "AXATA C01 teslimatlar getOutBoundDeliveryListAsync ile Status=0 olarak okunur.",
                "CanImport=true olan belgelerde AXATA teslimat satirlari Mikro siparis satirlariyla guvenli eslesmistir ve henuz fis linki yoktur.",
                "Mikro fis linki zaten varsa tekrar fis uretilmez; execute sirasinda uygun durumda sadece AXATA ack yapilabilir."
            ]);
    }

    public async Task<AxataOutboundDeliveryImportExecuteDto> ExecuteC01Async(
        AxataOutboundDeliveryImportExecuteRequest request,
        Guid requestedByUserId,
        CancellationToken cancellationToken)
    {
        var take = NormalizeTake(request.Take);
        var documents = await FetchPendingOutboundDeliveriesAsync(C01MovementType, cancellationToken);
        var selectedDocuments = documents.Take(take).ToArray();
        var analyses = await AnalyzeC01DocumentsAsync(selectedDocuments, cancellationToken);
        var results = new List<AxataOutboundDeliveryImportResultDto>(analyses.Count);
        var failures = new List<AxataOutboundDeliveryImportFailureDto>();
        var skippedDocumentCount = 0;

        foreach (var analysis in analyses)
        {
            try
            {
                if (analysis.ImportDto.CanImport)
                {
                    var shipmentResponse = await createInterWarehouseShipmentUseCase.ExecuteAsync(
                        BuildCreateShipmentRequest(analysis),
                        cancellationToken);
                    var acknowledged = false;

                    if (request.Acknowledge)
                    {
                        await AcknowledgeOutboundDeliveryAsync(analysis.Document.AxataSequenceNo, cancellationToken);
                        acknowledged = true;
                    }

                    results.Add(new AxataOutboundDeliveryImportResultDto(
                        analysis.Document.AxataSequenceNo,
                        analysis.Document.AxataDeliveryNo,
                        analysis.Document.DocumentSerie,
                        analysis.Document.DocumentOrderNo ?? 0,
                        shipmentResponse.DocumentSerie,
                        shipmentResponse.DocumentOrderNo,
                        shipmentResponse.LineCount,
                        shipmentResponse.TotalQuantity,
                        acknowledged,
                        acknowledged
                            ? "Mikro sevk fisi olusturuldu ve AXATA ENT006.S06STAT=1 yapildi."
                            : "Mikro sevk fisi olusturuldu; AXATA ack istenmedigi icin status degistirilmedi."));

                    continue;
                }

                if (request.Acknowledge && CanAcknowledgeExistingMikroShipment(analysis))
                {
                    await AcknowledgeOutboundDeliveryAsync(analysis.Document.AxataSequenceNo, cancellationToken);
                    skippedDocumentCount++;
                    results.Add(new AxataOutboundDeliveryImportResultDto(
                        analysis.Document.AxataSequenceNo,
                        analysis.Document.AxataDeliveryNo,
                        analysis.Document.DocumentSerie,
                        analysis.Document.DocumentOrderNo ?? 0,
                        string.Empty,
                        0,
                        0,
                        0d,
                        true,
                        "Mikro sevk linki zaten vardi; duplicate fis olusturulmadan AXATA ENT006.S06STAT=1 yapildi."));

                    continue;
                }

                skippedDocumentCount++;
                failures.Add(new AxataOutboundDeliveryImportFailureDto(
                    analysis.Document.AxataSequenceNo,
                    analysis.Document.AxataDeliveryNo,
                    analysis.ImportDto.Warning ?? "C01 delivery can not be imported safely."));

                if (!request.ContinueOnError)
                {
                    break;
                }
            }
            catch (Exception exception)
            {
                failures.Add(new AxataOutboundDeliveryImportFailureDto(
                    analysis.Document.AxataSequenceNo,
                    analysis.Document.AxataDeliveryNo,
                    exception.Message));

                if (!request.ContinueOnError)
                {
                    break;
                }
            }
        }

        return new AxataOutboundDeliveryImportExecuteDto(
            C01MovementType,
            PendingStatus,
            DateTime.UtcNow,
            analyses.Count,
            results.Count(result => result.CreatedMovementLineCount > 0 || result.Acknowledged),
            failures.Count,
            skippedDocumentCount,
            results.Sum(result => result.CreatedMovementLineCount),
            results.Sum(result => result.CreatedMovementQuantity),
            results,
            failures,
            [
                "AXATA ack islemi Mikro sevk fisi basariyla olustuktan sonra yapilir.",
                "Mikro fis linki mevcutsa duplicate fis olusturulmaz.",
                $"Talep eden kullanici: {requestedByUserId}"
            ]);
    }

    public async Task<AxataOutboundDeliveryImportPreviewDto> PreviewC01DocumentAsync(
        AxataOutboundDeliveryDocumentImportPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var analysis = await GetC01DocumentAnalysisAsync(
            request.DocumentSerie,
            request.DocumentOrderNo,
            request.Status,
            cancellationToken);

        return new AxataOutboundDeliveryImportPreviewDto(
            C01MovementType,
            analysis.Document.Status,
            DateTime.UtcNow,
            1,
            1,
            analysis.Document.Lines.Count,
            analysis.Document.Lines.Sum(line => line.Quantity),
            [analysis.ImportDto],
            [
                $"AXATA C01 teslimat belge bazinda arandi: {FormatAxataDocumentNo(request.DocumentSerie, request.DocumentOrderNo)}.",
                string.IsNullOrWhiteSpace(request.Status)
                    ? "Status belirtilmedigi icin once 0, sonra 1 denendi."
                    : $"AXATA status {request.Status.Trim()} ile arandi.",
                "CanImport=true ise Mikro sevk fisi linki eksiktir ve AXATA satirlari Mikro siparis satirlariyla guvenli eslesmistir."
            ]);
    }

    public async Task<AxataOutboundDeliveryImportExecuteDto> ExecuteC01DocumentAsync(
        AxataOutboundDeliveryDocumentImportExecuteRequest request,
        Guid requestedByUserId,
        CancellationToken cancellationToken)
    {
        var analysis = await GetC01DocumentAnalysisAsync(
            request.DocumentSerie,
            request.DocumentOrderNo,
            request.Status,
            cancellationToken);
        var results = new List<AxataOutboundDeliveryImportResultDto>();
        var failures = new List<AxataOutboundDeliveryImportFailureDto>();
        var skippedDocumentCount = 0;

        try
        {
            if (analysis.ImportDto.CanImport)
            {
                var shipmentResponse = await createInterWarehouseShipmentUseCase.ExecuteAsync(
                    BuildCreateShipmentRequest(analysis),
                    cancellationToken);
                var acknowledged = false;

                if (request.Acknowledge)
                {
                    await AcknowledgeOutboundDeliveryAsync(analysis.Document.AxataSequenceNo, cancellationToken);
                    acknowledged = true;
                }

                results.Add(new AxataOutboundDeliveryImportResultDto(
                    analysis.Document.AxataSequenceNo,
                    analysis.Document.AxataDeliveryNo,
                    analysis.Document.DocumentSerie,
                    analysis.Document.DocumentOrderNo ?? 0,
                    shipmentResponse.DocumentSerie,
                    shipmentResponse.DocumentOrderNo,
                    shipmentResponse.LineCount,
                    shipmentResponse.TotalQuantity,
                    acknowledged,
                    acknowledged
                        ? "Belge bazli Mikro sevk fisi olusturuldu ve AXATA ENT006.S06STAT=1 yapildi."
                        : "Belge bazli Mikro sevk fisi olusturuldu; AXATA status degistirilmedi."));
            }
            else if (request.Acknowledge && CanAcknowledgeExistingMikroShipment(analysis))
            {
                await AcknowledgeOutboundDeliveryAsync(analysis.Document.AxataSequenceNo, cancellationToken);
                skippedDocumentCount++;
                results.Add(new AxataOutboundDeliveryImportResultDto(
                    analysis.Document.AxataSequenceNo,
                    analysis.Document.AxataDeliveryNo,
                    analysis.Document.DocumentSerie,
                    analysis.Document.DocumentOrderNo ?? 0,
                    string.Empty,
                    0,
                    0,
                    0d,
                    true,
                    "Mikro sevk linki zaten vardi; duplicate fis olusturulmadan AXATA ENT006.S06STAT=1 yapildi."));
            }
            else
            {
                skippedDocumentCount++;
                failures.Add(new AxataOutboundDeliveryImportFailureDto(
                    analysis.Document.AxataSequenceNo,
                    analysis.Document.AxataDeliveryNo,
                    analysis.ImportDto.Warning ?? "C01 delivery can not be imported safely."));
            }
        }
        catch (Exception exception)
        {
            failures.Add(new AxataOutboundDeliveryImportFailureDto(
                analysis.Document.AxataSequenceNo,
                analysis.Document.AxataDeliveryNo,
                exception.Message));
        }

        return new AxataOutboundDeliveryImportExecuteDto(
            C01MovementType,
            analysis.Document.Status,
            DateTime.UtcNow,
            1,
            results.Count(result => result.CreatedMovementLineCount > 0 || result.Acknowledged),
            failures.Count,
            skippedDocumentCount,
            results.Sum(result => result.CreatedMovementLineCount),
            results.Sum(result => result.CreatedMovementQuantity),
            results,
            failures,
            [
                $"Belge bazli rescue: {FormatAxataDocumentNo(request.DocumentSerie, request.DocumentOrderNo)}.",
                "Mikro'ya yazim sadece AXATA teslimat detaylari Mikro siparis satirlariyla guvenli eslesirse ve teslim miktari kalan siparisi asmazsa yapilir.",
                $"Talep eden kullanici: {requestedByUserId}"
            ]);
    }

    private int[] ResolveWarehouseOrderSourceWarehouseNos(int? requestedWarehouseNo)
    {
        var configuredWarehouseNos = options.CurrentValue.WarehouseOrderAutomation.WarehouseNos
            .Where(warehouseNo => warehouseNo > 0)
            .Distinct()
            .OrderBy(warehouseNo => warehouseNo)
            .ToArray();

        if (configuredWarehouseNos.Length == 0)
        {
            configuredWarehouseNos = [DefaultWarehouseOrderSourceWarehouseNo];
        }

        if (requestedWarehouseNo is > 0)
        {
            return configuredWarehouseNos.Contains(requestedWarehouseNo.Value)
                ? [requestedWarehouseNo.Value]
                : Array.Empty<int>();
        }

        return configuredWarehouseNos;
    }

    private async Task<OrderWorkflowAuditResult> GetOrderWorkflowAuditAsync(
        DateTime startDate,
        DateTime endDate,
        IReadOnlyCollection<int> sourceWarehouseNos,
        string? documentSerie,
        int? documentOrderNo,
        int take,
        CancellationToken cancellationToken)
    {
        var sourceWarehouseNoArray = sourceWarehouseNos
            .Where(warehouseNo => warehouseNo > 0)
            .Distinct()
            .ToArray();
        if (sourceWarehouseNoArray.Length == 0)
        {
            return OrderWorkflowAuditResult.Empty;
        }

        var endDateExclusive = endDate.Date.AddDays(1);
        var normalizedDocumentSerie = NormalizeQueryValue(documentSerie);
        var mikroQuery = mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs
            .AsNoTracking()
            .Where(order =>
                order.ssip_iptal != true &&
                order.ssip_tarih.HasValue &&
                order.ssip_tarih.Value >= startDate.Date &&
                order.ssip_tarih.Value < endDateExclusive &&
                order.ssip_evrakno_seri != null &&
                order.ssip_evrakno_sira.HasValue &&
                sourceWarehouseNoArray.Contains(order.ssip_cikdepo ?? 0));

        if (!string.IsNullOrWhiteSpace(normalizedDocumentSerie))
        {
            mikroQuery = mikroQuery.Where(order => order.ssip_evrakno_seri == normalizedDocumentSerie);
        }

        if (documentOrderNo.HasValue)
        {
            mikroQuery = mikroQuery.Where(order => order.ssip_evrakno_sira == documentOrderNo.Value);
        }

        var mikroRows = await mikroQuery
            .Select(order => new OrderWorkflowMikroRow(
                order.ssip_evrakno_seri ?? string.Empty,
                order.ssip_evrakno_sira ?? 0,
                order.ssip_tarih ?? DateTime.MinValue,
                order.ssip_cikdepo ?? 0,
                order.ssip_girdepo ?? 0,
                order.ssip_Guid,
                order.ssip_miktar ?? 0d,
                order.ssip_teslim_miktar ?? 0d,
                order.ssip_special1 ?? string.Empty))
            .ToListAsync(cancellationToken);

        if (mikroRows.Count == 0)
        {
            return OrderWorkflowAuditResult.Empty;
        }

        var mikroDocuments = mikroRows
            .GroupBy(row => new MikroDocumentKey(row.DocumentSerie, row.DocumentOrderNo))
            .Select(group => new OrderWorkflowMikroDocument(
                group.Key,
                group.Min(row => row.DocumentDate).Date,
                group.First().SourceWarehouseNo,
                group.First().TargetWarehouseNo,
                group.Count(),
                group.Sum(row => row.Quantity),
                group.Count(row => IsAxataSentFlag(row.Special1)),
                group.ToArray()))
            .OrderBy(document => document.DocumentDate)
            .ThenBy(document => document.Key.DocumentSerie)
            .ThenBy(document => document.Key.DocumentOrderNo)
            .ToArray();
        var documentNos = mikroDocuments
            .Select(document => FormatAxataDocumentNo(
                document.Key.DocumentSerie,
                document.Key.DocumentOrderNo))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var orderLineGuids = mikroRows
            .Select(row => row.OrderLineGuid)
            .Distinct()
            .ToArray();
        var movementLinkCounts = await GetWarehouseOrderMovementLinkCountsAsync(
            orderLineGuids,
            cancellationToken);
        var linkedMovementQuantities = await GetWarehouseOrderLinkedMovementQuantitiesAsync(
            orderLineGuids,
            cancellationToken);

        Dictionary<string, OrderWorkflowAxataOrder> axataOrders;
        Dictionary<string, IReadOnlyCollection<OrderWorkflowShipment>> shipmentsByDocument;
        if (axataDbContext is null)
        {
            axataOrders = new Dictionary<string, OrderWorkflowAxataOrder>(StringComparer.OrdinalIgnoreCase);
            shipmentsByDocument =
                new Dictionary<string, IReadOnlyCollection<OrderWorkflowShipment>>(StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            axataOrders = await GetAxataOrdersForWorkflowAsync(documentNos, cancellationToken);
            shipmentsByDocument = await GetAxataShipmentsForWorkflowAsync(documentNos, cancellationToken);
        }

        var documents = mikroDocuments
            .Select(mikroDocument =>
            {
                var documentNo = FormatAxataDocumentNo(
                    mikroDocument.Key.DocumentSerie,
                    mikroDocument.Key.DocumentOrderNo);
                var axataOrderExists = axataDbContext is null
                    ? (bool?)null
                    : axataOrders.ContainsKey(documentNo);
                var axataOrder = axataOrders.GetValueOrDefault(documentNo);
                var shipments = shipmentsByDocument.GetValueOrDefault(documentNo)
                    ?? Array.Empty<OrderWorkflowShipment>();
                var activeShipments = shipments
                    .Where(shipment => !shipment.IsCancelled && shipment.Quantity > QuantityTolerance)
                    .ToArray();
                var shipmentQuantity = activeShipments.Sum(shipment => shipment.Quantity);
                var linkedLineCount = mikroDocument.Rows.Sum(row =>
                    movementLinkCounts.GetValueOrDefault(row.OrderLineGuid));
                var linkedMovementQuantity = mikroDocument.Rows.Sum(row =>
                    linkedMovementQuantities.GetValueOrDefault(row.OrderLineGuid));
                var linkedDeliveredQuantity = mikroDocument.Rows
                    .Where(row => movementLinkCounts.GetValueOrDefault(row.OrderLineGuid) > 0)
                    .Sum(row => row.DeliveredQuantity);
                var linkedQuantity = linkedMovementQuantity > QuantityTolerance
                    ? linkedMovementQuantity
                    : linkedDeliveredQuantity;
                var dispatchFlagState = mikroDocument.SentFlagLineCount == mikroDocument.LineCount
                    ? "Sent"
                    : mikroDocument.SentFlagLineCount > 0
                        ? "PartiallySent"
                        : "NotSent";
                var axataOrderState = ResolveAxataOrderState(
                    axataOrderExists,
                    mikroDocument.Quantity,
                    axataOrder?.Quantity ?? 0d);
                var shipmentState = ResolveOrderShipmentState(
                    axataOrderExists,
                    mikroDocument.Quantity,
                    shipmentQuantity,
                    shipments.Count(shipment => shipment.IsCancelled));
                var mikroTransferState = ResolveMikroTransferState(shipmentQuantity, linkedQuantity);
                var pendingShipmentCount = activeShipments.Count(shipment =>
                    IsPendingStatus(shipment.Status));
                var synchronizationState = ResolveOrderSynchronizationState(
                    axataOrderState,
                    shipmentState,
                    mikroTransferState,
                    pendingShipmentCount);
                var action = BuildOrderRecommendedAction(
                    mikroDocument.Key,
                    axataOrderState,
                    shipmentState,
                    mikroTransferState,
                    pendingShipmentCount,
                    shipmentQuantity,
                    linkedQuantity);

                return new AxataOrderLifecycleDto(
                    mikroDocument.Key.DocumentSerie,
                    mikroDocument.Key.DocumentOrderNo,
                    documentNo,
                    mikroDocument.DocumentDate,
                    mikroDocument.SourceWarehouseNo,
                    mikroDocument.TargetWarehouseNo,
                    mikroDocument.LineCount,
                    mikroDocument.Quantity,
                    mikroDocument.SentFlagLineCount,
                    dispatchFlagState,
                    axataOrderExists,
                    axataOrder?.LineCount ?? 0,
                    axataOrder?.Quantity ?? 0d,
                    axataOrderState,
                    activeShipments.Length,
                    pendingShipmentCount,
                    activeShipments.Count(shipment => IsCompletedStatus(shipment.Status)),
                    shipments.Count(shipment => shipment.IsCancelled),
                    activeShipments.Sum(shipment => shipment.LineCount),
                    shipmentQuantity,
                    shipmentState,
                    linkedLineCount,
                    linkedQuantity,
                    mikroTransferState,
                    synchronizationState,
                    action,
                    shipments
                        .OrderBy(shipment => shipment.ShipmentDate)
                        .ThenBy(shipment => shipment.AxataSequenceNo)
                        .Select(shipment => new AxataOrderShipmentReferenceDto(
                            shipment.AxataSequenceNo,
                            shipment.AxataDeliveryNo,
                            shipment.Status,
                            shipment.ShipmentDate,
                            shipment.LineCount,
                            shipment.Quantity,
                            shipment.IsCancelled,
                            NormalizeNullableValue(shipment.CancellationCode)))
                        .ToArray());
            })
            .ToArray();

        var summary = new AxataOrderWorkflowSummaryDto(
            documents.Length,
            documents.Count(document => document.AxataOrderExists == true),
            documents.Count(document => document.AxataOrderExists == false),
            documents.Count(document => !document.AxataOrderExists.HasValue),
            documents.Count(document => document.AxataOrderState == "QuantityMismatch"),
            documents.Sum(document => document.Shipments.Count),
            documents.Count(document => document.ShipmentState == "WaitingForAxataShipment"),
            documents.Count(document => document.ShipmentState == "PartiallyShipped"),
            documents.Count(document => document.ShipmentState == "FullyShipped"),
            documents.Count(document => document.ShipmentState == "OverShipped"),
            documents.Count(document => document.MikroLinkedShipmentLineCount > 0),
            documents.Count(document => document.MikroTransferState == "WaitingForMikroTransfer"),
            documents.Count(document => document.MikroTransferState == "PartiallyLinked"),
            documents.Count(document => document.MikroTransferState == "FullyLinked"),
            documents.Count(document => document.SynchronizationState == "FullySynchronized"),
            documents.Count(document => document.RecommendedAction.RequiresManualAction));

        return new OrderWorkflowAuditResult(
            summary,
            documents
                .OrderByDescending(document => document.RecommendedAction.RequiresManualAction)
                .ThenBy(document => document.DocumentDate)
                .ThenBy(document => document.DocumentSerie)
                .ThenBy(document => document.DocumentOrderNo)
                .Take(take)
                .ToArray());
    }

    private async Task<Dictionary<Guid, double>> GetWarehouseOrderLinkedMovementQuantitiesAsync(
        IReadOnlyCollection<Guid> orderLineGuids,
        CancellationToken cancellationToken)
    {
        var distinctOrderLineGuids = orderLineGuids
            .Where(guid => guid != Guid.Empty)
            .Distinct()
            .ToArray();
        if (distinctOrderLineGuids.Length == 0)
        {
            return new Dictionary<Guid, double>();
        }

        var rows = await (
                from movementExtra in mikroWriteDbContext.STOK_HAREKETLERI_EKs.AsNoTracking()
                join movement in mikroWriteDbContext.STOK_HAREKETLERIs.AsNoTracking()
                    on movementExtra.sthek_related_uid equals movement.sth_Guid
                where movementExtra.sth_subesip_uid.HasValue &&
                      distinctOrderLineGuids.Contains(movementExtra.sth_subesip_uid.Value) &&
                      movement.sth_iptal != true
                select new
                {
                    OrderLineGuid = movementExtra.sth_subesip_uid!.Value,
                    Quantity = movement.sth_miktar ?? 0d
                })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.OrderLineGuid)
            .ToDictionary(group => group.Key, group => group.Sum(row => row.Quantity));
    }

    private async Task<Dictionary<string, OrderWorkflowAxataOrder>> GetAxataOrdersForWorkflowAsync(
        IReadOnlyCollection<string> documentNos,
        CancellationToken cancellationToken)
    {
        if (axataDbContext is null || documentNos.Count == 0)
        {
            return new Dictionary<string, OrderWorkflowAxataOrder>(StringComparer.OrdinalIgnoreCase);
        }

        var normalizedDocumentNos = documentNos
            .Select(documentNo => documentNo.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var headers = await axataDbContext.ENT000s
            .AsNoTracking()
            .Where(header =>
                header.S00TESN != null &&
                normalizedDocumentNos.Contains(header.S00TESN.Trim()) &&
                ((header.S00HTP1 != null && header.S00HTP1.Trim() == C01MovementType) ||
                 (header.S00HTP2 != null && header.S00HTP2.Trim() == C01MovementType)))
            .Select(header => header.S00TESN ?? string.Empty)
            .ToListAsync(cancellationToken);
        var lines = await axataDbContext.ENT001s
            .AsNoTracking()
            .Where(line =>
                line.S01TESL != null &&
                normalizedDocumentNos.Contains(line.S01TESL.Trim()))
            .Select(line => new
            {
                DocumentNo = line.S01TESL ?? string.Empty,
                Quantity = line.S01MIKT
            })
            .ToListAsync(cancellationToken);
        var lineGroups = lines
            .GroupBy(line => line.DocumentNo.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => new OrderWorkflowAxataOrder(
                    group.Count(),
                    group.Sum(line => line.Quantity.HasValue ? (double)line.Quantity.Value : 0d)),
                StringComparer.OrdinalIgnoreCase);

        return headers
            .Select(documentNo => documentNo.Trim())
            .Where(documentNo => !string.IsNullOrWhiteSpace(documentNo))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                documentNo => documentNo,
                documentNo => lineGroups.GetValueOrDefault(documentNo)
                              ?? new OrderWorkflowAxataOrder(0, 0d),
                StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, IReadOnlyCollection<OrderWorkflowShipment>>> GetAxataShipmentsForWorkflowAsync(
        IReadOnlyCollection<string> documentNos,
        CancellationToken cancellationToken)
    {
        if (axataDbContext is null || documentNos.Count == 0)
        {
            return new Dictionary<string, IReadOnlyCollection<OrderWorkflowShipment>>(
                StringComparer.OrdinalIgnoreCase);
        }

        var normalizedDocumentNos = documentNos
            .Select(documentNo => documentNo.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var headers = await axataDbContext.ENT006s
            .AsNoTracking()
            .Where(header =>
                header.S06TESL != null &&
                normalizedDocumentNos.Contains(header.S06TESL.Trim()) &&
                header.S06OHTP != null &&
                header.S06OHTP.Trim() == C01MovementType &&
                header.S06STAT.HasValue &&
                (header.S06STAT.Value == 0m || header.S06STAT.Value == 1m))
            .Select(header => new AxataAuditOutboundDeliveryHeaderRow(
                header.S06SIRA,
                header.S06TESL ?? string.Empty,
                header.S06OHTP ?? C01MovementType,
                header.S06STAT,
                header.S06STTU,
                header.S06IPTKOD,
                header.S06FIRM,
                header.S06TFIR,
                header.S06ITAR))
            .ToListAsync(cancellationToken);
        var deliveryNos = headers
            .Select(header => header.AxataDeliveryNo.Trim())
            .Where(deliveryNo => !string.IsNullOrWhiteSpace(deliveryNo))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var lines = deliveryNos.Length == 0
            ? []
            : await axataDbContext.ENT007s
                .AsNoTracking()
                .Where(line => line.S07TESL != null && deliveryNos.Contains(line.S07TESL.Trim()))
                .Select(line => new AxataAuditOutboundDeliveryLineRow(
                    line.S07TESL ?? string.Empty,
                    line.S07KALN,
                    line.S07SKOD,
                    line.S07MIKT))
                .ToListAsync(cancellationToken);
        var lineGroups = lines
            .GroupBy(line => line.AxataDeliveryNo.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    LineCount = group.Count(),
                    Quantity = group.Sum(line => line.Quantity.HasValue ? (double)line.Quantity.Value : 0d)
                },
                StringComparer.OrdinalIgnoreCase);

        return headers
            .Select(header =>
            {
                var documentNo = header.AxataDeliveryNo.Trim();
                var lineGroup = lineGroups.GetValueOrDefault(documentNo);
                var quantity = lineGroup?.Quantity ?? 0d;
                var cancellationCode = NormalizeQueryValue(header.CancellationCode);
                var isCancelled = !string.IsNullOrWhiteSpace(cancellationCode) ||
                                  (string.Equals(
                                       NormalizeQueryValue(header.ShipmentState),
                                       "3",
                                       StringComparison.OrdinalIgnoreCase) &&
                                   quantity <= QuantityTolerance);

                return new OrderWorkflowShipment(
                    documentNo,
                    header.AxataSequenceNo,
                    documentNo,
                    FirstNonEmpty(FormatDecimal(header.Status), PendingStatus),
                    ParseDate(FormatDecimal(header.AxataDateKey)),
                    lineGroup?.LineCount ?? 0,
                    quantity,
                    isCancelled,
                    cancellationCode);
            })
            .GroupBy(shipment => shipment.DocumentNo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.ToArray() as IReadOnlyCollection<OrderWorkflowShipment>,
                StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveAxataOrderState(
        bool? axataOrderExists,
        double mikroOrderQuantity,
        double axataOrderQuantity)
    {
        if (!axataOrderExists.HasValue)
        {
            return "Unknown";
        }

        if (!axataOrderExists.Value)
        {
            return "NotFound";
        }

        return HasQuantityDifference(mikroOrderQuantity, axataOrderQuantity)
            ? "QuantityMismatch"
            : "Confirmed";
    }

    private static string ResolveOrderShipmentState(
        bool? axataOrderExists,
        double orderQuantity,
        double shipmentQuantity,
        int cancelledShipmentCount)
    {
        if (!axataOrderExists.HasValue)
        {
            return "Unknown";
        }

        if (!axataOrderExists.Value)
        {
            return shipmentQuantity > QuantityTolerance
                ? "ShipmentExistsWithoutAxataOrder"
                : "WaitingForAxataOrder";
        }

        if (shipmentQuantity <= QuantityTolerance)
        {
            return cancelledShipmentCount > 0
                ? "CancelledInAxata"
                : "WaitingForAxataShipment";
        }

        if (shipmentQuantity + QuantityTolerance < orderQuantity)
        {
            return "PartiallyShipped";
        }

        return shipmentQuantity > orderQuantity + QuantityTolerance
            ? "OverShipped"
            : "FullyShipped";
    }

    private static string ResolveMikroTransferState(double shipmentQuantity, double linkedQuantity)
    {
        if (shipmentQuantity <= QuantityTolerance)
        {
            return "NotApplicable";
        }

        if (linkedQuantity <= QuantityTolerance)
        {
            return "WaitingForMikroTransfer";
        }

        if (linkedQuantity + QuantityTolerance < shipmentQuantity)
        {
            return "PartiallyLinked";
        }

        return linkedQuantity > shipmentQuantity + QuantityTolerance
            ? "QuantityMismatch"
            : "FullyLinked";
    }

    private static string ResolveOrderSynchronizationState(
        string axataOrderState,
        string shipmentState,
        string mikroTransferState,
        int pendingShipmentCount)
    {
        if (axataOrderState == "Unknown")
        {
            return "UnableToVerify";
        }

        if (axataOrderState == "NotFound")
        {
            return shipmentState == "ShipmentExistsWithoutAxataOrder"
                ? "InconsistentAxataData"
                : "WaitingForAxataTransfer";
        }

        if (axataOrderState == "QuantityMismatch" ||
            shipmentState == "OverShipped" ||
            mikroTransferState == "QuantityMismatch")
        {
            return "QuantityMismatch";
        }

        if (shipmentState == "CancelledInAxata")
        {
            return "CancelledInAxata";
        }

        if (shipmentState == "WaitingForAxataShipment")
        {
            return "WaitingForAxataShipment";
        }

        if (mikroTransferState == "WaitingForMikroTransfer")
        {
            return "WaitingForMikroTransfer";
        }

        if (mikroTransferState == "PartiallyLinked")
        {
            return "ManualReviewRequired";
        }

        if (pendingShipmentCount > 0 && mikroTransferState == "FullyLinked")
        {
            return "WaitingForAxataAck";
        }

        if (shipmentState == "PartiallyShipped" && mikroTransferState == "FullyLinked")
        {
            return "WaitingForRemainingAxataShipment";
        }

        return shipmentState == "FullyShipped" && mikroTransferState == "FullyLinked"
            ? "FullySynchronized"
            : "ReviewRequired";
    }

    private static AxataOrderRecommendedActionDto BuildOrderRecommendedAction(
        MikroDocumentKey documentKey,
        string axataOrderState,
        string shipmentState,
        string mikroTransferState,
        int pendingShipmentCount,
        double shipmentQuantity,
        double linkedQuantity)
    {
        var documentPath =
            $"/api/integrations/axata-sync/live/axata/outbound-deliveries/c01/documents/{Uri.EscapeDataString(documentKey.DocumentSerie)}/{documentKey.DocumentOrderNo}";

        if (axataOrderState == "Unknown")
        {
            return new AxataOrderRecommendedActionDto(
                "VERIFY_AXATA_CONNECTION",
                "AXATA siparis kaydini dogrula",
                "Warning",
                true,
                false,
                "/api/integrations/axata-sync/health",
                null,
                "AXATA SQL baglantisi olmadigi icin siparisin AXATA'ya gercekten dustugu dogrulanamadi.");
        }

        if (axataOrderState == "NotFound")
        {
            if (shipmentQuantity > QuantityTolerance)
            {
                return new AxataOrderRecommendedActionDto(
                    "REVIEW_SHIPMENT_WITHOUT_AXATA_ORDER",
                    "AXATA kayit tutarsizligini incele",
                    "Critical",
                    true,
                    false,
                    documentPath + "/preview",
                    null,
                    "AXATA siparis basligi bulunamadi ancak bu evrak numarasina ait SEV var; siparisi yeniden gondermek duplicate riski tasir.");
            }

            return new AxataOrderRecommendedActionDto(
                "RESEND_ORDER_TO_AXATA",
                "Siparisi AXATA'ya yeniden gonder",
                "Critical",
                true,
                true,
                "/api/integrations/axata-sync/manual/tasks/issued-warehouse-order-sync/documents/preview",
                "/api/integrations/axata-sync/manual/tasks/issued-warehouse-order-sync/documents/dispatch",
                "Siparis Mikro'da var ancak AXATA ENT000/ENT001 kaydi bulunamadi.");
        }

        if (axataOrderState == "QuantityMismatch" ||
            shipmentState == "OverShipped" ||
            mikroTransferState == "QuantityMismatch")
        {
            return new AxataOrderRecommendedActionDto(
                "REVIEW_QUANTITY_MISMATCH",
                "Miktar farkini manuel incele",
                "Critical",
                true,
                false,
                documentPath + "/preview",
                null,
                "Mikro siparis, AXATA siparis/sevk veya Mikro bagli sevk miktarlari birbiriyle uyusmuyor.");
        }

        if (shipmentState == "CancelledInAxata")
        {
            return new AxataOrderRecommendedActionDto(
                "NO_ACTION_CANCELLED",
                "Islem yapma",
                "Info",
                false,
                false,
                null,
                null,
                "AXATA sevki iptal veya sifir miktarli; Mikro sevk fisi beklenmez.");
        }

        if (shipmentState == "WaitingForAxataShipment")
        {
            return new AxataOrderRecommendedActionDto(
                "WAIT_FOR_AXATA_SHIPMENT",
                "AXATA sevkini bekle",
                "Info",
                false,
                false,
                null,
                null,
                "Siparis AXATA'da mevcut ancak henuz pozitif miktarli SEV olusmamis.");
        }

        if (mikroTransferState == "WaitingForMikroTransfer")
        {
            var pending = pendingShipmentCount > 0;
            return new AxataOrderRecommendedActionDto(
                pending ? "IMPORT_PENDING_C01" : "RESCUE_COMPLETED_C01",
                pending ? "Bekleyen SEV'i Mikro'ya aktar" : "Tamamlanmis SEV icin rescue yap",
                "Critical",
                true,
                true,
                documentPath + $"/preview?status={(pending ? "0" : "1")}",
                documentPath + "/import",
                $"AXATA'da {shipmentQuantity:0.###} miktar SEV var, Mikro siparisine bagli sevk bulunamadi.");
        }

        if (mikroTransferState == "PartiallyLinked")
        {
            return new AxataOrderRecommendedActionDto(
                "REVIEW_PARTIAL_MIKRO_LINK",
                "Eksik Mikro baglantisini incele",
                "Warning",
                true,
                false,
                documentPath + "/preview",
                null,
                $"AXATA sevk miktari {shipmentQuantity:0.###}, Mikro'ya bagli miktar {linkedQuantity:0.###}.");
        }

        if (pendingShipmentCount > 0 && mikroTransferState == "FullyLinked")
        {
            return new AxataOrderRecommendedActionDto(
                "ACK_AXATA_ONLY",
                "Yalnizca AXATA durumunu onayla",
                "Warning",
                true,
                true,
                documentPath + "/preview?status=0",
                documentPath + "/import",
                "Mikro sevk baglantisi tamam ancak AXATA SEV hala Status=0; duplicate fis olusturmadan ACK gerekir.");
        }

        if (shipmentState == "PartiallyShipped")
        {
            return new AxataOrderRecommendedActionDto(
                "WAIT_FOR_REMAINING_SHIPMENT",
                "Kalan AXATA sevkini bekle",
                "Info",
                false,
                false,
                null,
                null,
                "Olusan AXATA sevkleri Mikro'ya tam baglanmis, siparisin kalan miktari henuz sevk edilmemis.");
        }

        return new AxataOrderRecommendedActionDto(
            "NO_ACTION_FULLY_SYNCHRONIZED",
            "Islem gerekmiyor",
            "Success",
            false,
            false,
            null,
            null,
            "Siparis AXATA'da mevcut, sevk tamam ve Mikro siparis baglantisi miktar olarak uyumlu.");
    }

    private async Task<WarehouseOrderAuditResult> GetWarehouseOrderAuditAsync(
        DateTime startDate,
        DateTime endDate,
        IReadOnlyCollection<int> sourceWarehouseNos,
        string? documentSerie,
        int? documentOrderNo,
        int take,
        CancellationToken cancellationToken)
    {
        var endDateExclusive = endDate.Date.AddDays(1);
        var normalizedDocumentSerie = NormalizeQueryValue(documentSerie);
        var sourceWarehouseNoArray = sourceWarehouseNos
            .Where(warehouseNo => warehouseNo > 0)
            .Distinct()
            .ToArray();

        if (sourceWarehouseNoArray.Length == 0)
        {
            return new WarehouseOrderAuditResult(
                0,
                0,
                0,
                0,
                0,
                0,
                0d,
                Array.Empty<AxataSentWarehouseOrderMissingShipmentDto>(),
                0,
                0,
                0d,
                Array.Empty<AxataSentWarehouseOrderMissingShipmentDto>(),
                Array.Empty<AxataUnsyncedWarehouseOrderDto>());
        }

        var query = mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs
            .AsNoTracking()
            .Where(order =>
                order.ssip_iptal != true &&
                order.ssip_tarih.HasValue &&
                order.ssip_tarih.Value >= startDate.Date &&
                order.ssip_tarih.Value < endDateExclusive &&
                order.ssip_evrakno_seri != null &&
                order.ssip_evrakno_sira.HasValue &&
                sourceWarehouseNoArray.Contains(order.ssip_cikdepo ?? 0));

        if (!string.IsNullOrWhiteSpace(normalizedDocumentSerie))
        {
            query = query.Where(order => order.ssip_evrakno_seri == normalizedDocumentSerie);
        }

        if (documentOrderNo.HasValue)
        {
            query = query.Where(order => order.ssip_evrakno_sira == documentOrderNo.Value);
        }

        var rows = await query
            .Select(order => new WarehouseOrderAuditRow(
                order.ssip_evrakno_seri ?? string.Empty,
                order.ssip_evrakno_sira ?? 0,
                order.ssip_tarih ?? DateTime.MinValue,
                order.ssip_girdepo ?? 0,
                order.ssip_cikdepo ?? 0,
                order.ssip_satirno ?? 0,
                order.ssip_Guid,
                order.ssip_miktar ?? 0d,
                order.ssip_teslim_miktar ?? 0d,
                order.ssip_special1 ?? string.Empty,
                order.ssip_lastup_date))
            .ToListAsync(cancellationToken);

        var movementLinkCounts = await GetWarehouseOrderMovementLinkCountsAsync(
            rows.Select(row => row.OrderLineGuid).Distinct().ToArray(),
            cancellationToken);

        var documents = rows
            .GroupBy(row => new
            {
                row.DocumentSerie,
                row.DocumentOrderNo,
                DocumentDate = row.DocumentDate.Date,
                row.InWarehouseNo,
                row.OutWarehouseNo
            })
            .Select(group =>
            {
                var sentLines = group
                    .Where(row => IsAxataSentFlag(row.Special1))
                    .ToArray();
                var lineCount = group.Count();
                var sentLineCount = sentLines.Length;
                var state = sentLineCount == lineCount
                    ? "Sent"
                    : sentLineCount > 0
                        ? "PartiallySent"
                        : "NotSent";

                return new AxataUnsyncedWarehouseOrderDto(
                    group.Key.DocumentSerie,
                    group.Key.DocumentOrderNo,
                    group.Key.DocumentDate,
                    group.Key.InWarehouseNo,
                    group.Key.OutWarehouseNo,
                    lineCount,
                    sentLineCount,
                    lineCount - sentLineCount,
                    group.Sum(row => row.Quantity),
                    sentLines.Sum(row => row.Quantity),
                    group.Where(row => !IsAxataSentFlag(row.Special1)).Sum(row => row.Quantity),
                    state,
                    group.Max(row => row.LastUpdateDate),
                    state == "Sent"
                        ? string.Empty
                        : "Mikro ssip_special1 worker basari bayragi tum satirlarda 1 degil.");
            })
            .OrderBy(document => document.DocumentDate)
            .ThenBy(document => document.DocumentSerie)
            .ThenBy(document => document.DocumentOrderNo)
            .ToArray();

        var sentShipmentLinkDocuments = rows
            .GroupBy(row => new
            {
                row.DocumentSerie,
                row.DocumentOrderNo,
                DocumentDate = row.DocumentDate.Date,
                row.InWarehouseNo,
                row.OutWarehouseNo
            })
            .Select(group =>
            {
                var sentRows = group
                    .Where(row => IsAxataSentFlag(row.Special1))
                    .ToArray();
                var missingRows = sentRows
                    .Where(row => movementLinkCounts.GetValueOrDefault(row.OrderLineGuid) == 0)
                    .ToArray();
                var quantityDifferenceRows = sentRows
                    .Where(row => HasQuantityDifference(row.Quantity, row.DeliveredQuantity))
                    .ToArray();
                var linkedMovementLineCount = sentRows
                    .Sum(row => movementLinkCounts.GetValueOrDefault(row.OrderLineGuid));
                var hasAnyMikroShipmentLink = linkedMovementLineCount > 0;
                var sentQuantity = sentRows.Sum(row => row.Quantity);
                var deliveredQuantity = sentRows.Sum(row => row.DeliveredQuantity);
                var differenceRows = sentRows
                    .Where(row =>
                        movementLinkCounts.GetValueOrDefault(row.OrderLineGuid) == 0 ||
                        HasQuantityDifference(row.Quantity, row.DeliveredQuantity))
                    .ToArray();
                var hasMissingMovementLink = missingRows.Length > 0;
                var hasQuantityDifference = quantityDifferenceRows.Length > 0 ||
                                            HasQuantityDifference(sentQuantity, deliveredQuantity);

                return new AxataSentWarehouseOrderMissingShipmentDto(
                    group.Key.DocumentSerie,
                    group.Key.DocumentOrderNo,
                    group.Key.DocumentDate,
                    group.Key.InWarehouseNo,
                    group.Key.OutWarehouseNo,
                    group.Count(),
                    sentRows.Length,
                    missingRows.Length,
                    group.Sum(row => row.Quantity),
                    sentQuantity,
                    missingRows.Sum(row => row.Quantity),
                    deliveredQuantity,
                    linkedMovementLineCount,
                    differenceRows.Length,
                    differenceRows.Sum(row => Math.Abs(row.Quantity - row.DeliveredQuantity)),
                    BuildShipmentDifferenceReason(hasMissingMovementLink, hasQuantityDifference),
                    hasAnyMikroShipmentLink
                        ? "SentToAxataShipmentDifference"
                        : "SentToAxataMissingMikroShipment",
                    group.Max(row => row.LastUpdateDate),
                    hasAnyMikroShipmentLink
                        ? "Belgede en az bir Mikro sevk linki var; eksik link veya siparis-teslim miktar farki bulundugu icin kismi sevk/satir farki olarak incelenmelidir."
                        : "Siparis AXATA'ya gonderildi olarak isaretli, ancak belge genelinde Mikro sevk fisi linki STOK_HAREKETLERI_EK.sth_subesip_uid uzerinden bulunamadi.",
                    false,
                    null,
                    null,
                    0,
                    0d);
            })
            .Where(document => document.SentLineCount > 0 && document.DifferenceLineCount > 0)
            .OrderByDescending(document => document.LastUpdateDate ?? document.DocumentDate)
            .ThenByDescending(document => document.DocumentDate)
            .ThenBy(document => document.DocumentSerie)
            .ThenBy(document => document.DocumentOrderNo)
            .ToArray();
        var sentMissingMikroShipmentDocuments = sentShipmentLinkDocuments
            .Where(document => document.LinkedMovementLineCount == 0)
            .ToArray();
        var sentShipmentDifferenceDocuments = sentShipmentLinkDocuments
            .Where(document => document.LinkedMovementLineCount > 0)
            .ToArray();

        var unsyncedDocuments = documents
            .Where(document => document.State != "Sent")
            .Take(take)
            .ToArray();

        return new WarehouseOrderAuditResult(
            documents.Length,
            documents.Count(document => document.State == "Sent"),
            documents.Count(document => document.State == "PartiallySent"),
            documents.Count(document => document.State == "NotSent"),
            sentMissingMikroShipmentDocuments.Length,
            sentMissingMikroShipmentDocuments.Sum(document => document.MissingMovementLinkLineCount),
            sentMissingMikroShipmentDocuments.Sum(document => document.MissingMovementLinkQuantity),
            sentMissingMikroShipmentDocuments,
            sentShipmentDifferenceDocuments.Length,
            sentShipmentDifferenceDocuments.Sum(document => document.DifferenceLineCount),
            sentShipmentDifferenceDocuments.Sum(document => document.DifferenceQuantity),
            sentShipmentDifferenceDocuments,
            unsyncedDocuments);
    }

    private static string FormatWarehouseNos(IReadOnlyCollection<int> warehouseNos) =>
        warehouseNos.Count == 0
            ? "AXATA kaynak/cikis depo yok"
            : string.Join(", ", warehouseNos.OrderBy(warehouseNo => warehouseNo));

    private static IReadOnlyCollection<AxataIntegrationAuditOperationDto> BuildAuditOperations(
        AxataIntegrationAuditSummaryDto summary)
    {
        var unsentDocumentCount = summary.UnsentWarehouseOrderDocumentCount +
                                  summary.PartiallySentWarehouseOrderDocumentCount;
        var pendingOutboundDocumentCount = summary.PendingOutboundDeliveryDocumentCount;
        var cancelledOutboundDocumentCount = summary.AxataCancelledOutboundDeliveryDocumentCount;
        var missingMikroShipmentDocumentCount =
            summary.SentWarehouseOrderMissingMikroShipmentWithAxataDeliveryDocumentCount;
        var missingAxataOutboundDeliveryDocumentCount =
            summary.SentWarehouseOrderMissingAxataOutboundDeliveryDocumentCount;
        var shipmentDifferenceDocumentCount = summary.SentWarehouseOrderShipmentDifferenceDocumentCount;

        return
        [
            new AxataIntegrationAuditOperationDto(
                "warehouse-orders-not-sent-to-axata",
                "Mikro siparis AXATA gonderim kontrolu",
                unsentDocumentCount == 0 ? "Ok" : "ActionRequired",
                unsentDocumentCount == 0 ? "Success" : "Warning",
                unsentDocumentCount,
                0,
                0d,
                "/api/integrations/axata-sync/manual/tasks/issued-warehouse-order-sync/documents/candidates",
                "/api/integrations/axata-sync/manual/tasks/issued-warehouse-order-sync/documents/preview-batch",
                "/api/integrations/axata-sync/manual/tasks/issued-warehouse-order-sync/documents/dispatch-batch",
                true,
                true,
                "ssip_special1 basari bayragi eksik olan depolar arasi siparisler AXATA'ya manuel tekrar gonderilebilir."),
            new AxataIntegrationAuditOperationDto(
                "axata-pending-outbound-deliveries",
                "AXATA bekleyen sevk kuyrugu",
                pendingOutboundDocumentCount == 0 ? "Ok" : "ActionRequired",
                pendingOutboundDocumentCount == 0 ? "Success" : "Warning",
                pendingOutboundDocumentCount,
                summary.PendingOutboundDeliveryLineCount,
                summary.PendingOutboundDeliveryQuantity,
                "/api/integrations/axata-sync/live/axata/outbound-deliveries/preview?movementType={C01|C02|C03|C4}",
                "/api/integrations/axata-sync/live/axata/outbound-deliveries/c01/preview",
                "/api/integrations/axata-sync/live/axata/outbound-deliveries/c01/import",
                pendingOutboundDocumentCount > 0,
                true,
                "AXATA Status=0 sevk kuyrugu izlenir; canli Mikro import/ack su an C01 icin aciktir."),
            new AxataIntegrationAuditOperationDto(
                "axata-cancelled-outbound-deliveries",
                "AXATA iptal/zero sevkler",
                cancelledOutboundDocumentCount == 0 ? "Ok" : "Review",
                cancelledOutboundDocumentCount == 0 ? "Success" : "Info",
                cancelledOutboundDocumentCount,
                summary.AxataCancelledOutboundDeliveryLineCount,
                summary.AxataCancelledOutboundDeliveryQuantity,
                "/api/integrations/axata-sync/live/audit/overview#axataOutboundDeliveries",
                null,
                null,
                false,
                false,
                "AXATA tarafinda iptal kodu olan veya S06STTU=3 ve miktari 0 olan sevkler ayrica izlenir; Mikro sevk fisi beklenmez."),
            new AxataIntegrationAuditOperationDto(
                "sent-to-axata-missing-mikro-shipment",
                "AXATA sevk kesilmis Mikro donus eksik",
                missingMikroShipmentDocumentCount == 0 ? "Ok" : "ActionRequired",
                missingMikroShipmentDocumentCount == 0 ? "Success" : "Critical",
                missingMikroShipmentDocumentCount,
                summary.SentWarehouseOrderMissingMikroShipmentWithAxataDeliveryLineCount,
                summary.SentWarehouseOrderMissingMikroShipmentWithAxataDeliveryQuantity,
                "/api/integrations/axata-sync/live/audit/overview#sentWarehouseOrdersMissingMikroShipments",
                "/api/integrations/axata-sync/live/axata/outbound-deliveries/c01/documents/{documentSerie}/{documentOrderNo}/preview",
                "/api/integrations/axata-sync/live/axata/outbound-deliveries/c01/documents/{documentSerie}/{documentOrderNo}/import",
                true,
                true,
                "AXATA C01 outbound delivery kaydi bulunan, pozitif miktarli ve iptal olmayan sevklerde Mikro STOK_HAREKETLERI_EK sevk linki yoksa listelenir; belge bazli C01 rescue Mikro sevk fisini olusturabilir."),
            new AxataIntegrationAuditOperationDto(
                "sent-to-axata-missing-axata-outbound-delivery",
                "Mikro gonderildi isaretli AXATA sevk yok",
                missingAxataOutboundDeliveryDocumentCount == 0 ? "Ok" : "Review",
                missingAxataOutboundDeliveryDocumentCount == 0 ? "Success" : "Warning",
                missingAxataOutboundDeliveryDocumentCount,
                summary.SentWarehouseOrderMissingAxataOutboundDeliveryLineCount,
                summary.SentWarehouseOrderMissingAxataOutboundDeliveryQuantity,
                "/api/integrations/axata-sync/live/audit/overview#sentWarehouseOrdersMissingMikroShipments",
                null,
                null,
                false,
                false,
                "Mikro siparis AXATA'ya gonderildi olarak isaretli fakat AXATA ENT006/ENT007 C01 outbound delivery kaydi bulunamadi; once AXATA tarafinda sevk olusup olusmadigi incelenmelidir."),
            new AxataIntegrationAuditOperationDto(
                "sent-to-axata-shipment-differences",
                "Kismi sevk / satir farki izleme",
                shipmentDifferenceDocumentCount == 0 ? "Ok" : "Review",
                shipmentDifferenceDocumentCount == 0 ? "Success" : "Warning",
                shipmentDifferenceDocumentCount,
                summary.SentWarehouseOrderShipmentDifferenceLineCount,
                summary.SentWarehouseOrderShipmentDifferenceQuantity,
                "/api/integrations/axata-sync/live/audit/overview#sentWarehouseOrdersWithShipmentDifferences",
                null,
                null,
                false,
                false,
                "Belgede en az bir Mikro sevk linki vardir, ancak eksik link veya siparis-teslim miktar farki bulunur; kismi sevk veya satir farki olarak incelenir.")
        ];
    }

    private async Task<C01DeliveryAnalysis> GetC01DocumentAnalysisAsync(
        string documentSerie,
        int documentOrderNo,
        string? status,
        CancellationToken cancellationToken)
    {
        var normalizedDocumentSerie = NormalizeRequiredDocumentSerie(documentSerie);
        if (documentOrderNo <= 0)
        {
            throw new ArgumentException("Document order no must be greater than zero.", nameof(documentOrderNo));
        }

        var axataDocumentNo = FormatAxataDocumentNo(normalizedDocumentSerie, documentOrderNo);
        var statuses = ResolveOutboundDeliveryStatuses(status);
        var fetchedDocuments = new List<AxataOutboundDeliveryDocument>();

        foreach (var candidateStatus in statuses)
        {
            var documents = await FetchOutboundDeliveriesAsync(
                C01MovementType,
                candidateStatus,
                axataDocumentNo,
                cancellationToken);
            var matches = documents
                .Where(document => MatchesAxataDocument(document, normalizedDocumentSerie, documentOrderNo))
                .ToArray();

            fetchedDocuments.AddRange(matches);

            if (matches.Length > 0)
            {
                break;
            }
        }

        var selectedDocument = fetchedDocuments
            .OrderBy(document => document.Status == PendingStatus ? 0 : 1)
            .ThenBy(document => document.AxataSequenceNo)
            .FirstOrDefault();

        if (selectedDocument is null)
        {
            throw new KeyNotFoundException(
                $"AXATA C01 delivery was not found for document {axataDocumentNo} with status {FormatStatuses(statuses)}.");
        }

        var analyses = await AnalyzeC01DocumentsAsync([selectedDocument], cancellationToken);
        return analyses.Single();
    }

    private async Task<IReadOnlyCollection<C01DeliveryAnalysis>> AnalyzeC01DocumentsAsync(
        IReadOnlyCollection<AxataOutboundDeliveryDocument> documents,
        CancellationToken cancellationToken)
    {
        if (documents.Count == 0)
        {
            return Array.Empty<C01DeliveryAnalysis>();
        }

        var parsedDocuments = documents
            .Where(document => document.DocumentOrderNo.HasValue && !string.IsNullOrWhiteSpace(document.DocumentSerie))
            .ToArray();
        var orderLines = Array.Empty<DEPOLAR_ARASI_SIPARISLER>();

        if (parsedDocuments.Length > 0)
        {
            var documentSeries = parsedDocuments
                .Select(document => document.DocumentSerie)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var documentOrderNos = parsedDocuments
                .Select(document => document.DocumentOrderNo!.Value)
                .Distinct()
                .ToArray();

            orderLines = await mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs
                .AsNoTracking()
                .Where(order =>
                    order.ssip_iptal != true &&
                    order.ssip_evrakno_seri != null &&
                    documentSeries.Contains(order.ssip_evrakno_seri) &&
                    order.ssip_evrakno_sira.HasValue &&
                    documentOrderNos.Contains(order.ssip_evrakno_sira.Value))
                .ToArrayAsync(cancellationToken);
        }

        var movementLinkCounts = await GetMovementLinkCountsAsync(orderLines, cancellationToken);
        var orderLinesByDocument = orderLines
            .GroupBy(order => new MikroDocumentKey(order.ssip_evrakno_seri ?? string.Empty, order.ssip_evrakno_sira ?? 0))
            .ToDictionary(
                group => group.Key,
                group => group.ToArray());

        return documents
            .Select(document =>
            {
                var documentOrderLines = document.DocumentOrderNo.HasValue
                    ? orderLinesByDocument.GetValueOrDefault(new MikroDocumentKey(document.DocumentSerie, document.DocumentOrderNo.Value))
                      ?? Array.Empty<DEPOLAR_ARASI_SIPARISLER>()
                    : Array.Empty<DEPOLAR_ARASI_SIPARISLER>();
                var positiveLines = document.Lines
                    .Where(line => line.Quantity > 0d)
                    .ToArray();
                var matchedLines = MatchC01Lines(document, documentOrderLines, positiveLines);
                var existingLinkedMovementLineCount = documentOrderLines
                    .Sum(order => movementLinkCounts.GetValueOrDefault(order.ssip_Guid));
                var warning = BuildC01Warning(
                    document,
                    documentOrderLines,
                    positiveLines,
                    matchedLines,
                    existingLinkedMovementLineCount);
                var canImport = string.IsNullOrWhiteSpace(warning);
                var mikroCheckState = ResolveC01MikroCheckState(
                    document,
                    documentOrderLines,
                    positiveLines,
                    matchedLines,
                    existingLinkedMovementLineCount,
                    canImport);

                return new C01DeliveryAnalysis(
                    document,
                    new AxataOutboundDeliveryImportDocumentDto(
                        document.AxataSequenceNo,
                        document.AxataDeliveryNo,
                        document.DocumentSerie,
                        document.DocumentOrderNo ?? 0,
                        document.MovementType,
                        document.Status,
                        document.SourceWarehouseNo,
                        document.TargetWarehouseNo,
                        document.AxataDate,
                        document.Lines.Count,
                        document.Lines.Sum(line => line.Quantity),
                        documentOrderLines.Length,
                        documentOrderLines.Sum(order => order.ssip_miktar ?? 0d),
                        documentOrderLines.Sum(order => order.ssip_teslim_miktar ?? 0d),
                        existingLinkedMovementLineCount,
                        canImport,
                        warning),
                    matchedLines,
                    mikroCheckState);
            })
            .ToArray();
    }

    private async Task<Dictionary<Guid, int>> GetWarehouseOrderMovementLinkCountsAsync(
        IReadOnlyCollection<Guid> orderLineGuids,
        CancellationToken cancellationToken)
    {
        var distinctOrderLineGuids = orderLineGuids
            .Where(guid => guid != Guid.Empty)
            .Distinct()
            .ToArray();

        if (distinctOrderLineGuids.Length == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var linkedOrderLineGuids = await mikroWriteDbContext.STOK_HAREKETLERI_EKs
            .AsNoTracking()
            .Where(extra =>
                extra.sth_subesip_uid.HasValue &&
                distinctOrderLineGuids.Contains(extra.sth_subesip_uid.Value))
            .Select(extra => extra.sth_subesip_uid!.Value)
            .ToListAsync(cancellationToken);

        return linkedOrderLineGuids
            .GroupBy(guid => guid)
            .ToDictionary(group => group.Key, group => group.Count());
    }

    private async Task<Dictionary<Guid, int>> GetMovementLinkCountsAsync(
        IReadOnlyCollection<DEPOLAR_ARASI_SIPARISLER> orderLines,
        CancellationToken cancellationToken)
    {
        var orderLineGuids = orderLines
            .Select(order => order.ssip_Guid)
            .Distinct()
            .ToArray();

        if (orderLineGuids.Length == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var linkedOrderLineGuids = await mikroWriteDbContext.STOK_HAREKETLERI_EKs
            .AsNoTracking()
            .Where(extra =>
                extra.sth_subesip_uid.HasValue &&
                orderLineGuids.Contains(extra.sth_subesip_uid.Value))
            .Select(extra => extra.sth_subesip_uid!.Value)
            .ToListAsync(cancellationToken);

        return linkedOrderLineGuids
            .GroupBy(guid => guid)
            .ToDictionary(group => group.Key, group => group.Count());
    }

    private static IReadOnlyCollection<MatchedC01Line> MatchC01Lines(
        AxataOutboundDeliveryDocument document,
        IReadOnlyCollection<DEPOLAR_ARASI_SIPARISLER> orderLines,
        IReadOnlyCollection<AxataOutboundDeliveryLine> positiveLines)
    {
        var result = new List<MatchedC01Line>(positiveLines.Count);
        var matchedQuantitiesByOrderLine = new Dictionary<Guid, double>();

        foreach (var axataLine in positiveLines)
        {
            var orderLine = FindC01OrderLine(
                orderLines,
                axataLine,
                matchedQuantitiesByOrderLine);

            if (orderLine is not null)
            {
                result.Add(new MatchedC01Line(document, axataLine, orderLine));
                matchedQuantitiesByOrderLine[orderLine.ssip_Guid] =
                    matchedQuantitiesByOrderLine.GetValueOrDefault(orderLine.ssip_Guid) + axataLine.Quantity;
            }
        }

        return result;
    }

    private static DEPOLAR_ARASI_SIPARISLER? FindC01OrderLine(
        IReadOnlyCollection<DEPOLAR_ARASI_SIPARISLER> orderLines,
        AxataOutboundDeliveryLine axataLine,
        IReadOnlyDictionary<Guid, double> matchedQuantitiesByOrderLine)
    {
        var sameStockLines = orderLines
            .Where(order => string.Equals(
                NormalizeCode(order.ssip_stok_kod),
                NormalizeCode(axataLine.StockCode),
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var exactLineNoCandidate = sameStockLines.FirstOrDefault(order =>
            (order.ssip_satirno ?? -1) == axataLine.LineNo);

        if (exactLineNoCandidate is not null)
        {
            return exactLineNoCandidate;
        }

        var oneBasedLineNoCandidate = sameStockLines.FirstOrDefault(order =>
            (order.ssip_satirno ?? -1) + 1 == axataLine.LineNo);

        if (oneBasedLineNoCandidate is not null)
        {
            return oneBasedLineNoCandidate;
        }

        var safeStockCandidates = sameStockLines
            .Where(order =>
                order.ssip_kapat_fl != true &&
                GetRemainingOrderQuantity(order) -
                matchedQuantitiesByOrderLine.GetValueOrDefault(order.ssip_Guid) + QuantityTolerance >= axataLine.Quantity)
            .ToArray();

        if (safeStockCandidates.Length == 1)
        {
            return safeStockCandidates[0];
        }

        var exactQuantityCandidates = safeStockCandidates
            .Where(order =>
                !HasQuantityDifference(GetRemainingOrderQuantity(order), axataLine.Quantity) ||
                !HasQuantityDifference(order.ssip_miktar ?? 0d, axataLine.Quantity))
            .ToArray();

        return exactQuantityCandidates.Length == 1
            ? exactQuantityCandidates[0]
            : null;
    }

    private static string? BuildC01Warning(
        AxataOutboundDeliveryDocument document,
        IReadOnlyCollection<DEPOLAR_ARASI_SIPARISLER> orderLines,
        IReadOnlyCollection<AxataOutboundDeliveryLine> positiveLines,
        IReadOnlyCollection<MatchedC01Line> matchedLines,
        int existingLinkedMovementLineCount)
    {
        if (document.AxataSequenceNo <= 0)
        {
            return "AXATA S06SIRA bulunamadi; ENT006 ack guvenli degil.";
        }

        if (!document.DocumentOrderNo.HasValue || string.IsNullOrWhiteSpace(document.DocumentSerie))
        {
            return "AXATA S06TESL seri.sira formatinda degil.";
        }

        if (document.SourceWarehouseNo <= 0 || document.TargetWarehouseNo <= 0)
        {
            return "AXATA kaynak/hedef depo bilgisi okunamadi.";
        }

        if (IsCancelledOutboundDelivery(document))
        {
            return "AXATA sevki iptal/zero-quantity olarak gorunuyor; Mikro sevk fisi beklenmez.";
        }

        if (positiveLines.Count == 0)
        {
            return "AXATA teslimat satiri yok veya miktarlar sifir.";
        }

        if (orderLines.Count == 0)
        {
            return "Mikro depolar arasi siparis bulunamadi.";
        }

        if (matchedLines.Count != positiveLines.Count)
        {
            return "AXATA teslimat satirlari Mikro siparis satirlariyla guvenli eslesmedi.";
        }

        if (existingLinkedMovementLineCount > 0)
        {
            return IsCompletedStatus(document.Status)
                ? "Mikro sevk linki mevcut ve AXATA status tamamlandi; islem gerekmiyor."
                : "Mikro sevk linki zaten mevcut; duplicate fis uretilmeden AXATA ack yapilabilir.";
        }

        if (matchedLines.Any(line =>
                line.OrderLine.ssip_girdepo != document.TargetWarehouseNo ||
                line.OrderLine.ssip_cikdepo != document.SourceWarehouseNo))
        {
            return "Mikro siparis depo bilgisi AXATA teslimat deposuyla uyusmuyor.";
        }

        if (matchedLines.Any(line => line.OrderLine.ssip_kapat_fl == true))
        {
            return "Mikro siparis satiri kapali.";
        }

        if (matchedLines
            .GroupBy(line => line.OrderLine.ssip_Guid)
            .Any(group =>
            {
                var orderLine = group.First().OrderLine;
                return group.Sum(line => line.AxataLine.Quantity) >
                       GetRemainingOrderQuantity(orderLine) + QuantityTolerance;
            }))
        {
            return "AXATA teslim miktari Mikro siparis kalan miktarindan buyuk.";
        }

        return null;
    }

    private static string ResolveC01MikroCheckState(
        AxataOutboundDeliveryDocument document,
        IReadOnlyCollection<DEPOLAR_ARASI_SIPARISLER> orderLines,
        IReadOnlyCollection<AxataOutboundDeliveryLine> positiveLines,
        IReadOnlyCollection<MatchedC01Line> matchedLines,
        int existingLinkedMovementLineCount,
        bool canImport)
    {
        if (IsCancelledOutboundDelivery(document))
        {
            return "CancelledInAxata";
        }

        if (positiveLines.Count == 0)
        {
            return "EmptyAxataDelivery";
        }

        if (existingLinkedMovementLineCount > 0)
        {
            return IsCompletedStatus(document.Status)
                ? "Synchronized"
                : "MikroShipmentExistsPendingAck";
        }

        if (canImport)
        {
            return "ReadyForImport";
        }

        if (orderLines.Count == 0)
        {
            return "OrderNotFound";
        }

        if (matchedLines.Count != positiveLines.Count)
        {
            return "OrderLineMismatch";
        }

        return "Blocked";
    }

    private static bool CanAcknowledgeExistingMikroShipment(C01DeliveryAnalysis analysis) =>
        !IsCancelledOutboundDelivery(analysis.Document) &&
        HasPositiveAxataQuantity(analysis.Document) &&
        IsPendingStatus(analysis.Document.Status) &&
        analysis.ImportDto.ExistingLinkedMovementLineCount > 0 &&
        analysis.ImportDto.MikroDeliveredQuantity + QuantityTolerance >= analysis.ImportDto.AxataQuantity;

    private static AxataPendingOutboundDeliveryDto ToPendingOutboundDeliveryDto(C01DeliveryAnalysis analysis) =>
        new(
            analysis.Document.MovementType,
            analysis.Document.Status,
            analysis.Document.AxataSequenceNo,
            analysis.Document.AxataDeliveryNo,
            analysis.Document.DocumentSerie,
            analysis.Document.DocumentOrderNo,
            analysis.Document.SourceWarehouseNo,
            analysis.Document.TargetWarehouseNo,
            analysis.Document.AxataDate,
            analysis.Document.Lines.Count,
            analysis.Document.Lines.Sum(line => line.Quantity),
            analysis.ImportDto.MikroOrderLineCount,
            analysis.ImportDto.MikroOrderQuantity,
            analysis.ImportDto.MikroDeliveredQuantity,
            analysis.ImportDto.ExistingLinkedMovementLineCount,
            analysis.MikroCheckState,
            analysis.ImportDto.CanImport || CanAcknowledgeExistingMikroShipment(analysis),
            analysis.ImportDto.Warning,
            analysis.Document.ShipmentState,
            IsCancelledOutboundDelivery(analysis.Document),
            NormalizeNullableValue(analysis.Document.CancellationCode));

    private static AxataPendingOutboundDeliveryDto ToQueueOnlyPendingDto(AxataOutboundDeliveryDocument document) =>
        new(
            document.MovementType,
            document.Status,
            document.AxataSequenceNo,
            document.AxataDeliveryNo,
            document.DocumentSerie,
            document.DocumentOrderNo,
            document.SourceWarehouseNo,
            document.TargetWarehouseNo,
            document.AxataDate,
            document.Lines.Count,
            document.Lines.Sum(line => line.Quantity),
            0,
            0d,
            0d,
            0,
            "PendingInAxataQueue",
            false,
            "Bu hareket tipi icin Mikro fis eslesmesi bu endpointte kontrol edilmiyor; AXATA status 0 oldugu icin worker tamamlamamis kabul edilir.",
            document.ShipmentState,
            IsCancelledOutboundDelivery(document),
            NormalizeNullableValue(document.CancellationCode));

    private static AxataOutboundDeliveryQueueDocumentDto ToQueuePreviewDocumentDto(
        AxataOutboundDeliveryDocument document,
        bool hasLiveImport) =>
        new(
            document.AxataSequenceNo,
            document.AxataDeliveryNo,
            document.DocumentSerie,
            document.DocumentOrderNo,
            document.MovementType,
            document.Status,
            document.SourceWarehouseNo,
            document.TargetWarehouseNo,
            document.AxataDate,
            document.Lines.Count,
            document.Lines.Sum(line => line.Quantity),
            hasLiveImport,
            hasLiveImport
                ? "LiveImportAvailableViaC01Endpoint"
                : "LiveQueuePreviewOnly",
            hasLiveImport
                ? "Detayli import uygunlugu icin C01 preview endpoint'i kullanilmalidir."
                : "Bu hareket tipi icin Mikro import/ack endpoint'i henuz acik degildir.");

    private static CreateInterWarehouseShipmentRequest BuildCreateShipmentRequest(C01DeliveryAnalysis analysis) =>
        new(
            analysis.Document.SourceWarehouseNo,
            analysis.Document.TargetWarehouseNo,
            TransitWarehouseNo,
            analysis.Document.AxataDate ?? DateTime.Today,
            analysis.Document.AxataDate ?? DateTime.Today,
            "",
            analysis.Document.AxataDeliveryNo,
            analysis.MatchedLines
                .OrderBy(line => line.AxataLine.LineNo)
                .Select(line => new CreateInterWarehouseShipmentLineRequest(
                    line.AxataLine.StockCode,
                    line.AxataLine.Quantity,
                    line.OrderLine.ssip_Guid,
                    line.OrderLine.ssip_b_fiyat ?? 0d,
                    line.OrderLine.ssip_birim_pntr ?? 1,
                    analysis.Document.AxataDeliveryNo,
                    null,
                    0,
                    line.OrderLine.ssip_projekodu,
                    null,
                    line.OrderLine.ssip_sormerkezi))
                .ToArray(),
            true);

    private async Task<IReadOnlyCollection<AxataOutboundDeliveryDocument>> FetchPendingOutboundDeliveriesAsync(
        string movementType,
        CancellationToken cancellationToken) =>
        await FetchOutboundDeliveriesAsync(movementType, PendingStatus, null, cancellationToken);

    private async Task<IReadOnlyCollection<AxataOutboundDeliveryDocument>> FetchOutboundDeliveriesAsync(
        string movementType,
        string status,
        string? orderNumber,
        CancellationToken cancellationToken)
    {
        var configuration = GetRequiredConfiguration(requireExtendedEndpoint: false);
        var client = CreateMainClient(configuration.MainEndpointUrl);
        AxataMain.getOutboundDelivery_Res response;

        try
        {
            response = await client
                .getOutBoundDeliveryListAsync(
                    new AxataMain.getOutboundDelivery_Req(
                        configuration.Username,
                        configuration.Password,
                        new AxataMain.OutboundDeliveryQuery
                        {
                            CompanyCode = CompanyCode,
                            WarehouseCode = WarehouseCode,
                            OrderNumber = NormalizeQueryValue(orderNumber),
                            Firma = string.Empty,
                            MovementType = movementType,
                            Status = status,
                            YuklemeNo = string.Empty,
                            Type = string.Empty
                        }))
                .WaitAsync(cancellationToken);

            CloseWcfClient(client);
        }
        catch
        {
            AbortWcfClient(client);
            throw;
        }

        var serviceResponse = ToAxataServiceResponse(response.state, response.message);

        if (!serviceResponse.IsSuccess)
        {
            throw new InvalidOperationException(
                $"AXATA {FetchOperationName} failed: {serviceResponse.Message}");
        }

        return MapOutboundDeliveryDocuments(response.OutboundDeliveryList, movementType);
    }

    private async Task<OutboundDeliveryAuditFetchResult> TryFetchOutboundDeliveriesForAuditAsync(
        string movementType,
        IReadOnlyCollection<string> statuses,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyCollection<int> sourceWarehouseNos,
        string? documentSerie,
        int? documentOrderNo,
        CancellationToken cancellationToken)
    {
        if (axataDbContext is not null)
        {
            try
            {
                return new OutboundDeliveryAuditFetchResult(
                    true,
                    await FetchOutboundDeliveriesFromAxataDbForAuditAsync(
                        movementType,
                        statuses,
                        startDate,
                        endDate,
                        sourceWarehouseNos,
                        documentSerie,
                        documentOrderNo,
                        cancellationToken),
                    null);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var fallback = await TryFetchOutboundDeliveriesFromWcfForAuditAsync(
                    movementType,
                    statuses,
                    startDate,
                    endDate,
                    sourceWarehouseNos,
                    documentSerie,
                    documentOrderNo,
                    cancellationToken);

                return fallback with
                {
                    IsSuccess = false,
                    ErrorMessage = fallback.ErrorMessage is null
                        ? $"AXATA SQL fetch failed and WCF fallback was used: {exception.Message}"
                        : $"AXATA SQL fetch failed: {exception.Message}; WCF fallback also reported: {fallback.ErrorMessage}"
                };
            }
        }

        return await TryFetchOutboundDeliveriesFromWcfForAuditAsync(
            movementType,
            statuses,
            startDate,
            endDate,
            sourceWarehouseNos,
            documentSerie,
            documentOrderNo,
            cancellationToken);
    }

    private async Task<IReadOnlyCollection<AxataOutboundDeliveryDocument>> FetchOutboundDeliveriesFromAxataDbForAuditAsync(
        string movementType,
        IReadOnlyCollection<string> statuses,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyCollection<int> sourceWarehouseNos,
        string? documentSerie,
        int? documentOrderNo,
        CancellationToken cancellationToken)
    {
        if (axataDbContext is null)
        {
            return Array.Empty<AxataOutboundDeliveryDocument>();
        }

        if (sourceWarehouseNos.Count == 0)
        {
            return Array.Empty<AxataOutboundDeliveryDocument>();
        }

        var statusValues = statuses
            .Select(status => decimal.Parse(status, NumberStyles.Integer, CultureInfo.InvariantCulture))
            .Distinct()
            .ToArray();
        var startDateKey = ToAxataDateKey(startDate);
        var endDateKey = ToAxataDateKey(endDate);
        var sourceWarehouseCodes = sourceWarehouseNos
            .Where(warehouseNo => warehouseNo > 0)
            .Select(warehouseNo => warehouseNo.ToString(CultureInfo.InvariantCulture))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var normalizedDocumentSerie = NormalizeQueryValue(documentSerie);
        var exactDocumentNo = !string.IsNullOrWhiteSpace(normalizedDocumentSerie) && documentOrderNo.HasValue
            ? FormatAxataDocumentNo(normalizedDocumentSerie, documentOrderNo.Value)
            : string.Empty;
        var documentSeriePrefix = string.IsNullOrWhiteSpace(normalizedDocumentSerie)
            ? string.Empty
            : $"{normalizedDocumentSerie}.";

        var query = axataDbContext.ENT006s
            .AsNoTracking()
            .Where(header =>
                header.S06OHTP == movementType &&
                header.S06ITAR.HasValue &&
                header.S06ITAR.Value >= startDateKey &&
                header.S06ITAR.Value <= endDateKey &&
                header.S06STAT.HasValue &&
                statusValues.Contains(header.S06STAT.Value));

        if (sourceWarehouseCodes.Length > 0)
        {
            query = query.Where(header =>
                header.S06FIRM != null &&
                sourceWarehouseCodes.Contains(header.S06FIRM.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(exactDocumentNo))
        {
            query = query.Where(header => header.S06TESL != null && header.S06TESL.Trim() == exactDocumentNo);
        }
        else if (!string.IsNullOrWhiteSpace(documentSeriePrefix))
        {
            query = query.Where(header => header.S06TESL != null && header.S06TESL.Trim().StartsWith(documentSeriePrefix));
        }

        var headers = await query
            .OrderBy(header => header.S06SIRA)
            .Select(header => new AxataAuditOutboundDeliveryHeaderRow(
                header.S06SIRA,
                header.S06TESL ?? string.Empty,
                header.S06OHTP ?? movementType,
                header.S06STAT,
                header.S06STTU,
                header.S06IPTKOD,
                header.S06FIRM,
                header.S06TFIR,
                header.S06ITAR))
            .ToListAsync(cancellationToken);

        if (headers.Count == 0)
        {
            return Array.Empty<AxataOutboundDeliveryDocument>();
        }

        var deliveryNos = headers
            .Select(header => header.AxataDeliveryNo.Trim())
            .Where(deliveryNo => !string.IsNullOrWhiteSpace(deliveryNo))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        List<AxataAuditOutboundDeliveryLineRow> lineRows;
        if (deliveryNos.Length == 0)
        {
            lineRows = [];
        }
        else
        {
            lineRows = await axataDbContext.ENT007s
                .AsNoTracking()
                .Where(line =>
                    line.S07TESL != null &&
                    deliveryNos.Contains(line.S07TESL.Trim()))
                .Select(line => new AxataAuditOutboundDeliveryLineRow(
                    line.S07TESL ?? string.Empty,
                    line.S07KALN,
                    line.S07SKOD,
                    line.S07MIKT))
                .ToListAsync(cancellationToken);
        }

        var linesByDeliveryNo = lineRows
            .Where(line => !string.IsNullOrWhiteSpace(line.AxataDeliveryNo))
            .GroupBy(line => line.AxataDeliveryNo.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Where(line => !string.IsNullOrWhiteSpace(line.LineNo) && !string.IsNullOrWhiteSpace(line.StockCode))
                    .Select(line => new AxataOutboundDeliveryLine(
                        ParseInt(line.LineNo ?? string.Empty) ?? 0,
                        line.StockCode!.Trim(),
                        line.Quantity.HasValue ? (double)line.Quantity.Value : 0d))
                    .ToArray() as IReadOnlyCollection<AxataOutboundDeliveryLine>,
                StringComparer.OrdinalIgnoreCase);

        return headers
            .Select(header =>
            {
                var axataDeliveryNo = header.AxataDeliveryNo.Trim();
                var (serie, orderNo) = ParseAxataDeliveryNo(axataDeliveryNo);

                return new AxataOutboundDeliveryDocument(
                    header.AxataSequenceNo,
                    axataDeliveryNo,
                    serie,
                    orderNo,
                    FirstNonEmpty(header.MovementType, movementType),
                    FirstNonEmpty(FormatDecimal(header.Status), PendingStatus),
                    NormalizeQueryValue(header.ShipmentState),
                    NormalizeQueryValue(header.CancellationCode),
                    ParseInt(header.SourceWarehouseNo ?? string.Empty) ?? 0,
                    ParseInt(header.TargetWarehouseNo ?? string.Empty) ?? 0,
                    ParseDate(FormatDecimal(header.AxataDateKey)),
                    linesByDeliveryNo.GetValueOrDefault(axataDeliveryNo) ?? Array.Empty<AxataOutboundDeliveryLine>());
            })
            .OrderBy(document => document.AxataSequenceNo)
            .ToArray();
    }

    private async Task<OutboundDeliveryAuditFetchResult> TryFetchOutboundDeliveriesFromWcfForAuditAsync(
        string movementType,
        IReadOnlyCollection<string> statuses,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyCollection<int> sourceWarehouseNos,
        string? documentSerie,
        int? documentOrderNo,
        CancellationToken cancellationToken)
    {
        if (sourceWarehouseNos.Count == 0)
        {
            return new OutboundDeliveryAuditFetchResult(
                true,
                Array.Empty<AxataOutboundDeliveryDocument>(),
                null);
        }

        var documents = new List<AxataOutboundDeliveryDocument>();
        var errors = new List<string>();
        var sourceWarehouseNoSet = sourceWarehouseNos
            .Where(warehouseNo => warehouseNo > 0)
            .ToHashSet();
        var normalizedDocumentSerie = NormalizeQueryValue(documentSerie);

        foreach (var status in statuses)
        {
            try
            {
                documents.AddRange(await FetchOutboundDeliveriesAsync(
                    movementType,
                    status,
                    null,
                    cancellationToken));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                errors.Add($"status {status}: {exception.Message}");
            }
        }

        return new OutboundDeliveryAuditFetchResult(
            errors.Count == 0,
            documents
                .Where(document => IsInsideDateRange(document.AxataDate, startDate, endDate))
                .Where(document => sourceWarehouseNoSet.Count == 0 || sourceWarehouseNoSet.Contains(document.SourceWarehouseNo))
                .Where(document => MatchesAuditDocumentFilter(document, normalizedDocumentSerie, documentOrderNo))
                .OrderBy(document => document.Status)
                .ThenBy(document => document.AxataSequenceNo)
                .ToArray(),
            errors.Count == 0 ? null : string.Join("; ", errors));
    }

    private async Task AcknowledgeOutboundDeliveryAsync(
        long axataSequenceNo,
        CancellationToken cancellationToken)
    {
        var configuration = GetRequiredConfiguration(requireExtendedEndpoint: true);
        var client = CreateExtClient(configuration.ExtendedEndpointUrl);
        AxataExt.updIntegrationTable_Res response;

        try
        {
            response = await client
                .updIntegrationTableAsync(
                    new AxataExt.updIntegrationTable_Req(
                        configuration.Username,
                        configuration.Password,
                        new AxataExt.IntegrationTable
                        {
                            TableName = "ENT006",
                            UpdateField = "S06STAT",
                            UpdateValue = CompletedStatus,
                            IDField = "S06SIRA",
                            IDValues = new AxataExt.IDList
                            {
                                axataSequenceNo.ToString(CultureInfo.InvariantCulture)
                            }
                        }))
                .WaitAsync(cancellationToken);

            CloseWcfClient(client);
        }
        catch
        {
            AbortWcfClient(client);
            throw;
        }

        var serviceResponse = ToAxataServiceResponse(response.state, response.message);

        if (!serviceResponse.IsSuccess)
        {
            throw new InvalidOperationException(
                $"AXATA {AckOperationName} failed: {serviceResponse.Message}");
        }
    }

    private AxataOutboundDeliveryConfiguration GetRequiredConfiguration(bool requireExtendedEndpoint)
    {
        var currentOptions = options.CurrentValue;

        if (string.IsNullOrWhiteSpace(currentOptions.MainEndpointUrl))
        {
            throw new InvalidOperationException("AXATA main endpoint url is not configured.");
        }

        if (requireExtendedEndpoint && string.IsNullOrWhiteSpace(currentOptions.ExtendedEndpointUrl))
        {
            throw new InvalidOperationException("AXATA extended endpoint url is not configured.");
        }

        if (string.IsNullOrWhiteSpace(currentOptions.Username))
        {
            throw new InvalidOperationException("AXATA username is not configured.");
        }

        if (string.IsNullOrWhiteSpace(currentOptions.Password))
        {
            throw new InvalidOperationException("AXATA password is not configured.");
        }

        return new AxataOutboundDeliveryConfiguration(
            currentOptions.MainEndpointUrl,
            currentOptions.ExtendedEndpointUrl,
            currentOptions.Username,
            currentOptions.Password);
    }

    private static AxataMain.AxataServicePoolClient CreateMainClient(string endpointUrl) =>
        new(
            AxataMain.AxataServicePoolClient.EndpointConfiguration.BasicHttpBinding_IAxataServicePool,
            endpointUrl);

    private static AxataExt.AxataServicePoolEXTClient CreateExtClient(string endpointUrl) =>
        new(
            AxataExt.AxataServicePoolEXTClient.EndpointConfiguration.BasicHttpBinding_IAxataServicePoolEXT,
            endpointUrl);

    private static void CloseWcfClient(ICommunicationObject client)
    {
        if (client.State == CommunicationState.Faulted)
        {
            client.Abort();
            return;
        }

        client.Close();
    }

    private static void AbortWcfClient(ICommunicationObject client)
    {
        if (client.State != CommunicationState.Closed)
        {
            client.Abort();
        }
    }

    private static AxataWcfServiceResponse ToAxataServiceResponse(int? state, string? message)
    {
        var isSuccess = !state.HasValue || state.Value == 0;

        return new AxataWcfServiceResponse(
            isSuccess,
            state,
            string.IsNullOrWhiteSpace(message) ? "AXATA response received." : message.Trim());
    }

    private static IReadOnlyCollection<AxataOutboundDeliveryDocument> MapOutboundDeliveryDocuments(
        AxataMain.OutboundDelivery[]? deliveryList,
        string requestedMovementType)
    {
        if (deliveryList is null || deliveryList.Length == 0)
        {
            return Array.Empty<AxataOutboundDeliveryDocument>();
        }

        var result = new List<AxataOutboundDeliveryDocument>(deliveryList.Length);

        foreach (var delivery in deliveryList)
        {
            var header = delivery.ENT006;
            if (header is null)
            {
                continue;
            }

            var axataDeliveryNo = header.S06TESL?.Trim() ?? string.Empty;
            var (documentSerie, documentOrderNo) = ParseAxataDeliveryNo(axataDeliveryNo);
            var sourceWarehouseNo = ParseInt(header.S06FIRM ?? string.Empty) ?? 0;
            var targetWarehouseNo = ParseInt(header.S06TFIR ?? string.Empty) ?? 0;
            var status = FirstNonEmpty(FormatDecimal(header.S06STAT), PendingStatus);
            var axataDate = ParseDate(FormatDecimal(header.S06ITAR));
            var lines = MapOutboundDeliveryLines(delivery.ENT007_List);

            result.Add(new AxataOutboundDeliveryDocument(
                header.S06SIRA,
                axataDeliveryNo,
                documentSerie,
                documentOrderNo,
                requestedMovementType,
                status,
                NormalizeQueryValue(header.S06STTU),
                NormalizeQueryValue(header.S06IPTKOD),
                sourceWarehouseNo,
                targetWarehouseNo,
                axataDate,
                lines));
        }

        return result
            .OrderBy(item => item.AxataSequenceNo)
            .ToArray();
    }

    private static IReadOnlyCollection<AxataOutboundDeliveryLine> MapOutboundDeliveryLines(
        AxataMain.ENT007[]? lineList) =>
        lineList?
            .Where(line => !string.IsNullOrWhiteSpace(line.S07KALN) && !string.IsNullOrWhiteSpace(line.S07SKOD))
            .Select(line => new AxataOutboundDeliveryLine(
                ParseInt(line.S07KALN) ?? 0,
                line.S07SKOD.Trim(),
                line.S07MIKT.HasValue ? (double)line.S07MIKT.Value : 0d))
            .ToArray()
        ?? Array.Empty<AxataOutboundDeliveryLine>();

    private static (DateTime StartDate, DateTime EndDate) ResolveDateRange(DateTime? startDate, DateTime? endDate)
    {
        var normalizedEndDate = (endDate ?? DateTime.Today).Date;
        var normalizedStartDate = (startDate ?? normalizedEndDate).Date;

        if (normalizedEndDate < normalizedStartDate)
        {
            throw new ArgumentException("End date can not be earlier than start date.");
        }

        return (normalizedStartDate, normalizedEndDate);
    }

    private static int NormalizeTake(int? take) =>
        take is > 0 ? Math.Min(take.Value, MaxTake) : DefaultTake;

    private static string ResolveOutboundDeliveryMovementType(string? movementType)
    {
        var normalized = string.IsNullOrWhiteSpace(movementType)
            ? C01MovementType
            : movementType.Trim().ToUpperInvariant();

        if (normalized == "C04")
        {
            normalized = C04LegacyMovementType;
        }

        return AuditMovementTypes.Any(item => item.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            ? normalized
            : throw new ArgumentException(
                "Movement type must be one of C01, C02, C03, C4 or C04.",
                nameof(movementType));
    }

    private static IReadOnlyCollection<string> ResolveOutboundDeliveryStatuses(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return [PendingStatus, CompletedStatus];
        }

        var normalized = status.Trim();
        return normalized is PendingStatus or CompletedStatus
            ? [normalized]
            : throw new ArgumentException("Status must be 0 or 1.", nameof(status));
    }

    private static IReadOnlyCollection<string> ResolveAuditOutboundDeliveryStatuses(string? statuses)
    {
        if (string.IsNullOrWhiteSpace(statuses))
        {
            return [PendingStatus, CompletedStatus];
        }

        var normalizedStatuses = statuses
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedStatuses.Length == 0)
        {
            return [PendingStatus, CompletedStatus];
        }

        return normalizedStatuses.All(status => status is PendingStatus or CompletedStatus)
            ? normalizedStatuses
            : throw new ArgumentException("Statuses must contain only 0 and/or 1.", nameof(statuses));
    }

    private static string NormalizeRequiredDocumentSerie(string documentSerie)
    {
        if (string.IsNullOrWhiteSpace(documentSerie))
        {
            throw new ArgumentException("Document serie is required.", nameof(documentSerie));
        }

        return documentSerie.Trim();
    }

    private static string FormatAxataDocumentNo(string documentSerie, int documentOrderNo) =>
        $"{NormalizeRequiredDocumentSerie(documentSerie)}.{documentOrderNo.ToString(CultureInfo.InvariantCulture)}";

    private static string FormatStatuses(IReadOnlyCollection<string> statuses) =>
        string.Join(", ", statuses.Select(status => status.Trim()));

    private static bool MatchesAxataDocument(
        AxataOutboundDeliveryDocument document,
        string documentSerie,
        int documentOrderNo) =>
        (document.DocumentOrderNo == documentOrderNo &&
         string.Equals(document.DocumentSerie, documentSerie, StringComparison.OrdinalIgnoreCase)) ||
        string.Equals(
            document.AxataDeliveryNo,
            FormatAxataDocumentNo(documentSerie, documentOrderNo),
            StringComparison.OrdinalIgnoreCase);

    private static bool MatchesAuditDocumentFilter(
        AxataOutboundDeliveryDocument document,
        string normalizedDocumentSerie,
        int? documentOrderNo)
    {
        if (string.IsNullOrWhiteSpace(normalizedDocumentSerie) && !documentOrderNo.HasValue)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(normalizedDocumentSerie) &&
            !string.Equals(document.DocumentSerie, normalizedDocumentSerie, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !documentOrderNo.HasValue || document.DocumentOrderNo == documentOrderNo.Value;
    }

    private static string NormalizeQueryValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string? NormalizeNullableValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsInsideDateRange(DateTime? value, DateTime startDate, DateTime endDate) =>
        !value.HasValue || (value.Value.Date >= startDate.Date && value.Value.Date <= endDate.Date);

    private static bool IsPendingStatus(string status) =>
        string.Equals(status?.Trim(), PendingStatus, StringComparison.OrdinalIgnoreCase);

    private static bool IsCompletedStatus(string status) =>
        string.Equals(status?.Trim(), CompletedStatus, StringComparison.OrdinalIgnoreCase);

    private static bool HasPositiveAxataQuantity(AxataOutboundDeliveryDocument document) =>
        document.Lines.Sum(line => line.Quantity) > QuantityTolerance;

    private static bool IsCancelledOutboundDelivery(AxataOutboundDeliveryDocument document) =>
        !string.IsNullOrWhiteSpace(document.CancellationCode) ||
        (string.Equals(document.ShipmentState, "3", StringComparison.OrdinalIgnoreCase) &&
         !HasPositiveAxataQuantity(document));

    private static bool IsAxataCompletedC01ShipmentMissingMikro(C01DeliveryAnalysis analysis) =>
        analysis.Document.MovementType.Equals(C01MovementType, StringComparison.OrdinalIgnoreCase) &&
        IsCompletedStatus(analysis.Document.Status) &&
        !IsCancelledOutboundDelivery(analysis.Document) &&
        HasPositiveAxataQuantity(analysis.Document) &&
        analysis.ImportDto.ExistingLinkedMovementLineCount == 0;

    private static bool HasMatchingAxataOutboundDelivery(
        AxataSentWarehouseOrderMissingShipmentDto document,
        IReadOnlyCollection<AxataPendingOutboundDeliveryDto> outboundDeliveries) =>
        outboundDeliveries.Any(outboundDelivery =>
            outboundDelivery.DocumentOrderNo == document.DocumentOrderNo &&
            string.Equals(outboundDelivery.DocumentSerie, document.DocumentSerie, StringComparison.OrdinalIgnoreCase));

    private static AxataSentWarehouseOrderMissingShipmentDto ToAxataSourceMissingShipmentDto(
        C01DeliveryAnalysis analysis)
    {
        var document = analysis.Document;
        var positiveLineCount = document.Lines.Count(line => line.Quantity > QuantityTolerance);
        var quantity = document.Lines.Sum(line => line.Quantity);
        var warning = analysis.MikroCheckState switch
        {
            "OrderNotFound" => "AXATA C01 sevki var ancak Mikro depolar arasi siparis bulunamadi; Mikro'ya sevk fisi otomatik baglanamaz.",
            "OrderLineMismatch" => "AXATA C01 sevki var ancak satirlar Mikro siparis satirlariyla guvenli eslesmedi; manuel inceleme gerekir.",
            "ReadyForImport" => "AXATA C01 sevki Mikro'ya dusmemis; belge bazli C01 rescue ile Mikro sevk fisi olusturulabilir.",
            _ => "AXATA C01 sevki var ancak Mikro sevk linki STOK_HAREKETLERI_EK.sth_subesip_uid uzerinden bulunamadi."
        };

        return new AxataSentWarehouseOrderMissingShipmentDto(
            document.DocumentSerie,
            document.DocumentOrderNo ?? 0,
            document.AxataDate?.Date ?? DateTime.MinValue,
            document.TargetWarehouseNo,
            document.SourceWarehouseNo,
            document.Lines.Count,
            positiveLineCount,
            positiveLineCount,
            quantity,
            quantity,
            quantity,
            analysis.ImportDto.MikroDeliveredQuantity,
            analysis.ImportDto.ExistingLinkedMovementLineCount,
            positiveLineCount,
            quantity,
            "MissingMovementLink",
            "AxataShipmentMissingMikroShipment",
            null,
            warning,
            true,
            document.Status,
            document.AxataDate,
            document.Lines.Count,
            quantity);
    }

    private static bool IsAxataSentFlag(string? value) =>
        string.Equals(value?.Trim(), CompletedStatus, StringComparison.OrdinalIgnoreCase);

    private static bool HasQuantityDifference(double expectedQuantity, double actualQuantity) =>
        Math.Abs(expectedQuantity - actualQuantity) > QuantityTolerance;

    private static string BuildShipmentDifferenceReason(
        bool hasMissingMovementLink,
        bool hasQuantityDifference) =>
        (hasMissingMovementLink, hasQuantityDifference) switch
        {
            (true, true) => "MissingMovementLinkAndQuantityDifference",
            (true, false) => "MissingMovementLink",
            (false, true) => "QuantityDifference",
            _ => "None"
        };

    private static double GetRemainingOrderQuantity(DEPOLAR_ARASI_SIPARISLER orderLine) =>
        (orderLine.ssip_miktar ?? 0d) - (orderLine.ssip_teslim_miktar ?? 0d);

    private static (string Serie, int? OrderNo) ParseAxataDeliveryNo(string deliveryNo)
    {
        var normalized = deliveryNo.Trim();
        var separatorIndex = normalized.LastIndexOf('.');

        if (separatorIndex <= 0 || separatorIndex >= normalized.Length - 1)
        {
            return (normalized, null);
        }

        var serie = normalized[..separatorIndex].Trim();
        var orderNoText = normalized[(separatorIndex + 1)..].Trim();

        return int.TryParse(orderNoText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var orderNo)
            ? (serie, orderNo)
            : (serie, null);
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string NormalizeCode(string? value) =>
        value?.Trim() ?? string.Empty;

    private static int? ParseInt(string value) =>
        int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static string FormatDecimal(decimal? value) =>
        value?.ToString("0.#############################", CultureInfo.InvariantCulture) ?? string.Empty;

    private static decimal ToAxataDateKey(DateTime value) =>
        decimal.Parse(value.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    private static DateTime? ParseDate(string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        string[] formats =
        [
            "yyyyMMdd",
            "yyyyMMddHHmmss",
            "yyyy-MM-dd",
            "yyyy-MM-ddTHH:mm:ss",
            "dd.MM.yyyy",
            "dd.MM.yyyy HH:mm:ss",
            "dd/MM/yyyy",
            "dd/MM/yyyy HH:mm:ss"
        ];

        if (DateTime.TryParseExact(
                trimmed,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var exactValue))
        {
            return exactValue;
        }

        if (DateTime.TryParse(trimmed, CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.AssumeLocal, out var trValue))
        {
            return trValue;
        }

        return DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsedValue)
            ? parsedValue
            : null;
    }
}

internal sealed record AxataOutboundDeliveryConfiguration(
    string MainEndpointUrl,
    string ExtendedEndpointUrl,
    string Username,
    string Password);

internal sealed record AxataWcfServiceResponse(
    bool IsSuccess,
    int? State,
    string Message);

internal sealed record AxataAuditOutboundDeliveryHeaderRow(
    long AxataSequenceNo,
    string AxataDeliveryNo,
    string MovementType,
    decimal? Status,
    string? ShipmentState,
    string? CancellationCode,
    string? SourceWarehouseNo,
    string? TargetWarehouseNo,
    decimal? AxataDateKey);

internal sealed record AxataAuditOutboundDeliveryLineRow(
    string AxataDeliveryNo,
    string? LineNo,
    string? StockCode,
    decimal? Quantity);

internal sealed record AxataOutboundDeliveryDocument(
    long AxataSequenceNo,
    string AxataDeliveryNo,
    string DocumentSerie,
    int? DocumentOrderNo,
    string MovementType,
    string Status,
    string ShipmentState,
    string CancellationCode,
    int SourceWarehouseNo,
    int TargetWarehouseNo,
    DateTime? AxataDate,
    IReadOnlyCollection<AxataOutboundDeliveryLine> Lines);

internal sealed record AxataOutboundDeliveryLine(
    int LineNo,
    string StockCode,
    double Quantity);

internal sealed record MatchedC01Line(
    AxataOutboundDeliveryDocument Document,
    AxataOutboundDeliveryLine AxataLine,
    DEPOLAR_ARASI_SIPARISLER OrderLine);

internal sealed record OutboundDeliveryAuditFetchResult(
    bool IsSuccess,
    IReadOnlyCollection<AxataOutboundDeliveryDocument> Documents,
    string? ErrorMessage);

internal sealed record C01DeliveryAnalysis(
    AxataOutboundDeliveryDocument Document,
    AxataOutboundDeliveryImportDocumentDto ImportDto,
    IReadOnlyCollection<MatchedC01Line> MatchedLines,
    string MikroCheckState);

internal sealed record MikroDocumentKey(
    string DocumentSerie,
    int DocumentOrderNo);

internal sealed record WarehouseOrderAuditRow(
    string DocumentSerie,
    int DocumentOrderNo,
    DateTime DocumentDate,
    int InWarehouseNo,
    int OutWarehouseNo,
    int LineNo,
    Guid OrderLineGuid,
    double Quantity,
    double DeliveredQuantity,
    string Special1,
    DateTime? LastUpdateDate);

internal sealed record WarehouseOrderAuditResult(
    int DocumentCount,
    int SentDocumentCount,
    int PartiallySentDocumentCount,
    int UnsentDocumentCount,
    int SentMissingMikroShipmentDocumentCount,
    int SentMissingMikroShipmentLineCount,
    double SentMissingMikroShipmentQuantity,
    IReadOnlyCollection<AxataSentWarehouseOrderMissingShipmentDto> SentMissingMikroShipmentDocuments,
    int SentShipmentDifferenceDocumentCount,
    int SentShipmentDifferenceLineCount,
    double SentShipmentDifferenceQuantity,
    IReadOnlyCollection<AxataSentWarehouseOrderMissingShipmentDto> SentShipmentDifferenceDocuments,
    IReadOnlyCollection<AxataUnsyncedWarehouseOrderDto> UnsyncedDocuments);

internal sealed record OrderWorkflowMikroRow(
    string DocumentSerie,
    int DocumentOrderNo,
    DateTime DocumentDate,
    int SourceWarehouseNo,
    int TargetWarehouseNo,
    Guid OrderLineGuid,
    double Quantity,
    double DeliveredQuantity,
    string Special1);

internal sealed record OrderWorkflowMikroDocument(
    MikroDocumentKey Key,
    DateTime DocumentDate,
    int SourceWarehouseNo,
    int TargetWarehouseNo,
    int LineCount,
    double Quantity,
    int SentFlagLineCount,
    IReadOnlyCollection<OrderWorkflowMikroRow> Rows);

internal sealed record OrderWorkflowAxataOrder(
    int LineCount,
    double Quantity);

internal sealed record OrderWorkflowShipment(
    string DocumentNo,
    long AxataSequenceNo,
    string AxataDeliveryNo,
    string Status,
    DateTime? ShipmentDate,
    int LineCount,
    double Quantity,
    bool IsCancelled,
    string CancellationCode);

internal sealed record OrderWorkflowAuditResult(
    AxataOrderWorkflowSummaryDto Summary,
    IReadOnlyCollection<AxataOrderLifecycleDto> Documents)
{
    public static OrderWorkflowAuditResult Empty { get; } = new(
        new AxataOrderWorkflowSummaryDto(
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0),
        Array.Empty<AxataOrderLifecycleDto>());
}

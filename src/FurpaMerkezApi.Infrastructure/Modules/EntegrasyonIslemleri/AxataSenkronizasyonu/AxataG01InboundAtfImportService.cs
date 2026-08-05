using System.Globalization;
using System.ServiceModel;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using FurpaMerkezApi.Infrastructure.Services.MikroApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AxataExt = FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu.ServiceReferences.Ext;
using AxataMain = FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu.ServiceReferences.Main;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal sealed class AxataG01InboundAtfImportService(
    IOptionsMonitor<AxataSynchronizationOptions> options,
    MikroWriteDbContext mikroWriteDbContext,
    ICreateCompanyReceivingUseCase createCompanyReceivingUseCase,
    IOptionsMonitor<MikroWriteRoutingOptions> mikroWriteRoutingOptions)
    : IAxataG01InboundAtfImportService
{
    private const string MovementType = "G01";
    private const string PendingStatus = "0";
    private const string CompletedStatus = "1";
    private const string CompanyCode = "01";
    private const string WarehouseCode = "01";
    private const string FetchOperationName = "getInboundATFList";
    private const string AckOperationName = "updIntegrationTable";
    private const int DefaultTake = 20;
    private const int MaxTake = 200;
    private const short MikroUserNo = 39;
    private const byte MovementIn = 0;
    private const byte MovementGenre = 0;
    private const byte NormalReturn = 0;
    private const byte CompanyReceivingDocumentType = 13;
    private const double QuantityTolerance = 0.000001d;

    public async Task<AxataG01InboundAtfPreviewDto> PreviewAsync(
        AxataG01InboundAtfPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var take = NormalizeTake(request.Take);
        var lines = await FetchPendingLinesAsync(cancellationToken);
        var documents = lines
            .GroupBy(line => line.OrderDocumentNo, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .Select(group => new G01InboundAtfDocument(group.Key, group.ToArray()))
            .ToArray();
        var analyses = await AnalyzeDocumentsAsync(documents, cancellationToken);

        return new AxataG01InboundAtfPreviewDto(
            MovementType,
            PendingStatus,
            DateTime.UtcNow,
            lines.Count,
            analyses.Count,
            analyses.Count(analysis => analysis.Dto.CanImport),
            analyses.Sum(analysis => analysis.Lines.Count),
            analyses.Sum(analysis => analysis.Lines.Sum(line => line.Quantity)),
            analyses.Select(analysis => analysis.Dto).ToArray(),
            [
                "AXATA getInboundATFListAsync ile G01/Status=0 ENT016_IRS satirlari okunur.",
                "S16SIPN Mikro firma siparis belge no, S16KALN siparis satir no kabul edilir.",
                "CanImport=true olan belgeler Mikro STOK_HAREKETLERI DocumentType=13 firma mal kabul hareketine cevrilebilir."
            ]);
    }

    public async Task<AxataG01InboundAtfExecuteDto> ExecuteAsync(
        AxataG01InboundAtfExecuteRequest request,
        Guid requestedByUserId,
        CancellationToken cancellationToken)
    {
        var take = NormalizeTake(request.Take);
        var lines = await FetchPendingLinesAsync(cancellationToken);
        var documents = lines
            .GroupBy(line => line.OrderDocumentNo, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .Select(group => new G01InboundAtfDocument(group.Key, group.ToArray()))
            .ToArray();
        var analyses = await AnalyzeDocumentsAsync(documents, cancellationToken);
        var results = new List<AxataG01InboundAtfResultDto>(analyses.Count);
        var failures = new List<AxataG01InboundAtfFailureDto>();
        var skippedDocumentCount = 0;

        foreach (var analysis in analyses)
        {
            try
            {
                if (!analysis.Dto.CanImport)
                {
                    skippedDocumentCount++;
                    failures.Add(new AxataG01InboundAtfFailureDto(
                        analysis.Document.OrderDocumentNo,
                        analysis.Dto.Warning ?? "G01 inbound ATF can not be imported safely."));

                    if (!request.ContinueOnError)
                    {
                        break;
                    }

                    continue;
                }

                results.Add(await ExecuteAnalysisAsync(
                    analysis,
                    requestedByUserId,
                    request.Acknowledge,
                    cancellationToken));
            }
            catch (Exception exception)
            {
                failures.Add(new AxataG01InboundAtfFailureDto(
                    analysis.Document.OrderDocumentNo,
                    exception.Message));

                if (!request.ContinueOnError)
                {
                    break;
                }
            }
        }

        return new AxataG01InboundAtfExecuteDto(
            MovementType,
            PendingStatus,
            DateTime.UtcNow,
            analyses.Count,
            results.Count,
            failures.Count,
            skippedDocumentCount,
            results.Sum(result => result.CreatedMovementLineCount),
            results.Sum(result => result.CreatedMovementQuantity),
            results,
            failures,
            [
                "AXATA ack islemi Mikro firma mal kabul hareketleri ve siparis teslim miktari guncellemesi basarili olduktan sonra yapilir.",
                "S16SIRA duplicate kontrol anahtari olarak Mikro sth_HareketGrupKodu1 alaninda saklanir.",
                $"Talep eden kullanici: {requestedByUserId}"
            ]);
    }

    private async Task<AxataG01InboundAtfResultDto> ExecuteAnalysisAsync(
        G01InboundAtfAnalysis analysis,
        Guid requestedByUserId,
        bool acknowledge,
        CancellationToken cancellationToken)
    {
        if (mikroWriteRoutingOptions.CurrentValue.CompanyReceiving == MikroWriteMode.MikroApi)
        {
            return await ExecuteAnalysisWithMikroApiAsync(
                analysis,
                requestedByUserId,
                acknowledge,
                cancellationToken);
        }

        var orderGuids = analysis.MatchedLines.Select(line => line.OrderLine.sip_Guid).Distinct().ToArray();
        var trackedOrders = await mikroWriteDbContext.SIPARISLERs
            .Where(order => orderGuids.Contains(order.sip_Guid))
            .ToDictionaryAsync(order => order.sip_Guid, cancellationToken);
        var rowNo = await GetNextLineNoAsync(
            analysis.Dto.DocumentSerie,
            analysis.Dto.DocumentOrderNo,
            cancellationToken);
        var now = DateTime.Now;
        var movementDate = DateTime.Today;
        var createdLineCount = 0;
        var createdQuantity = 0d;

        await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(cancellationToken);
        foreach (var matchedLine in analysis.MatchedLines)
        {
            if (!trackedOrders.TryGetValue(matchedLine.OrderLine.sip_Guid, out var order))
            {
                throw new InvalidOperationException(
                    $"Mikro order line was not found for guid {matchedLine.OrderLine.sip_Guid}.");
            }

            order.sip_teslim_miktar = (order.sip_teslim_miktar ?? 0d) + matchedLine.Line.Quantity;
            mikroWriteDbContext.STOK_HAREKETLERIs.Add(CreateMovement(
                matchedLine,
                analysis.Dto.DocumentSerie,
                analysis.Dto.DocumentOrderNo,
                rowNo,
                now,
                movementDate));
            rowNo++;
            createdLineCount++;
            createdQuantity += matchedLine.Line.Quantity;
        }

        await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var acknowledged = false;
        if (acknowledge)
        {
            foreach (var line in analysis.Lines)
            {
                await AcknowledgeAsync(line.SequenceNo, cancellationToken);
            }

            acknowledged = true;
        }

        return new AxataG01InboundAtfResultDto(
            analysis.Document.OrderDocumentNo,
            analysis.Dto.DocumentSerie,
            analysis.Dto.DocumentOrderNo,
            createdLineCount,
            createdQuantity,
            acknowledged,
            acknowledged
                ? "Mikro G01 firma mal kabul hareketi olusturuldu, siparis teslim miktari guncellendi ve AXATA ENT016_IRS.S16STAT=1 yapildi."
                : "Mikro G01 firma mal kabul hareketi olusturuldu ve siparis teslim miktari guncellendi; AXATA status degistirilmedi.");
    }

    private async Task<AxataG01InboundAtfResultDto> ExecuteAnalysisWithMikroApiAsync(
        G01InboundAtfAnalysis analysis,
        Guid requestedByUserId,
        bool acknowledge,
        CancellationToken cancellationToken)
    {
        var orderGuids = GetG01OrderLineGuids(analysis);
        var existingMovementLineCount = await CountExistingG01MikroApiMovementLinesAsync(
            orderGuids,
            cancellationToken);

        if (existingMovementLineCount > 0)
        {
            return new AxataG01InboundAtfResultDto(
                analysis.Document.OrderDocumentNo,
                analysis.Dto.DocumentSerie,
                analysis.Dto.DocumentOrderNo,
                0,
                0d,
                false,
                "Mikro G01 firma mal kabul hareketi bu siparis satirlari icin zaten mevcut; duplicate fis olusturulmadi.");
        }

        var response = await createCompanyReceivingUseCase.ExecuteAsync(
            BuildCreateCompanyReceivingRequest(analysis, requestedByUserId),
            cancellationToken);

        await VerifyG01MikroApiOrderLinksAsync(orderGuids, cancellationToken);

        var acknowledged = false;
        if (acknowledge)
        {
            foreach (var line in analysis.Lines)
            {
                await AcknowledgeAsync(line.SequenceNo, cancellationToken);
            }

            acknowledged = true;
        }

        return new AxataG01InboundAtfResultDto(
            analysis.Document.OrderDocumentNo,
            response.DocumentSerie,
            response.DocumentOrderNo,
            response.LineCount,
            response.TotalReceivedQuantity,
            acknowledged,
            acknowledged
                ? "Mikro API ile G01 firma mal kabul hareketi olusturuldu, siparis linkleri dogrulandi ve AXATA ENT016_IRS.S16STAT=1 yapildi."
                : "Mikro API ile G01 firma mal kabul hareketi olusturuldu ve siparis linkleri dogrulandi; AXATA status degistirilmedi.");
    }

    private async Task<int> CountExistingG01MikroApiMovementLinesAsync(
        IReadOnlyCollection<Guid> orderGuids,
        CancellationToken cancellationToken)
    {
        if (orderGuids.Count == 0)
        {
            return 0;
        }

        return await mikroWriteDbContext.STOK_HAREKETLERIs
            .AsNoTracking()
            .Where(movement =>
                movement.sth_sip_uid.HasValue &&
                orderGuids.Contains(movement.sth_sip_uid.Value) &&
                movement.sth_iptal != true &&
                movement.sth_tip == MovementIn &&
                movement.sth_cins == MovementGenre &&
                movement.sth_normal_iade == NormalReturn &&
                movement.sth_evraktip == CompanyReceivingDocumentType)
            .CountAsync(cancellationToken);
    }

    private async Task VerifyG01MikroApiOrderLinksAsync(
        IReadOnlyCollection<Guid> orderGuids,
        CancellationToken cancellationToken)
    {
        if (orderGuids.Count == 0)
        {
            return;
        }

        var linkedOrderGuids = await mikroWriteDbContext.STOK_HAREKETLERIs
            .AsNoTracking()
            .Where(movement =>
                movement.sth_sip_uid.HasValue &&
                orderGuids.Contains(movement.sth_sip_uid.Value) &&
                movement.sth_iptal != true &&
                movement.sth_tip == MovementIn &&
                movement.sth_cins == MovementGenre &&
                movement.sth_normal_iade == NormalReturn &&
                movement.sth_evraktip == CompanyReceivingDocumentType)
            .Select(movement => movement.sth_sip_uid!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var missingOrderGuids = orderGuids
            .Where(orderGuid => !linkedOrderGuids.Contains(orderGuid))
            .ToArray();

        if (missingOrderGuids.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Mikro API G01 firma mal kabul olustu ancak {missingOrderGuids.Length} siparis satiri STOK_HAREKETLERI.sth_sip_uid ile dogrulanamadi; AXATA ack yapilmadi.");
    }

    private static CreateCompanyReceivingRequest BuildCreateCompanyReceivingRequest(
        G01InboundAtfAnalysis analysis,
        Guid requestedByUserId)
    {
        var movementDate = DateTime.Today;
        var documentNo = BuildCompanyReceivingDocumentNo(
            analysis.Dto.DocumentSerie,
            analysis.Dto.DocumentOrderNo);
        var requestLines = analysis.MatchedLines
            .GroupBy(line => line.OrderLine.sip_Guid)
            .OrderBy(group => group.Min(line => line.OrderLine.sip_satirno ?? line.Line.LineNo))
            .Select(group =>
            {
                var firstLine = group
                    .OrderBy(line => line.OrderLine.sip_satirno ?? line.Line.LineNo)
                    .First();
                var order = firstLine.OrderLine;

                return new CreateCompanyReceivingLineRequest(
                    firstLine.Line.StockCode,
                    group.Sum(line => line.Line.Quantity),
                    group.Sum(line => line.Line.Quantity),
                    group.Sum(line => line.Line.Quantity),
                    order.sip_b_fiyat ?? 0d,
                    order.sip_birim_pntr ?? 1,
                    null,
                    group.Key,
                    BuildG01LineDescription(group),
                    null,
                    0,
                    null,
                    null,
                    null);
            })
            .ToArray();

        return new CreateCompanyReceivingRequest(
            analysis.Dto.WarehouseNo,
            requestedByUserId,
            null,
            ResolveG01CustomerCode(analysis),
            movementDate,
            movementDate,
            documentNo,
            null,
            null,
            BuildG01Description(analysis),
            false,
            false,
            requestLines);
    }

    private static Guid[] GetG01OrderLineGuids(G01InboundAtfAnalysis analysis) =>
        analysis.MatchedLines
            .Select(line => line.OrderLine.sip_Guid)
            .Where(guid => guid != Guid.Empty)
            .Distinct()
            .ToArray();

    private static string ResolveG01CustomerCode(G01InboundAtfAnalysis analysis)
    {
        var orderCustomerCodes = analysis.MatchedLines
            .Select(line => NormalizeText(line.OrderLine.sip_musteri_kod))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (orderCustomerCodes.Length > 1)
        {
            throw new InvalidOperationException("G01 ATF satirlari birden fazla Mikro siparis carisiyle eslesti; tek firma mal kabul evraki guvenli olusturulamaz.");
        }

        return orderCustomerCodes.FirstOrDefault() ?? NormalizeText(analysis.Dto.CustomerCode);
    }

    private static string BuildCompanyReceivingDocumentNo(string documentSerie, int documentOrderNo) =>
        string.Concat(
            documentSerie,
            documentOrderNo.ToString(new string('0', 9), CultureInfo.InvariantCulture));

    private static string BuildG01Description(G01InboundAtfAnalysis analysis) =>
        Truncate(
            string.IsNullOrWhiteSpace(analysis.Dto.DespatchNo)
                ? $"AXATA G01 {analysis.Document.OrderDocumentNo}"
                : $"AXATA G01 {analysis.Document.OrderDocumentNo} / {analysis.Dto.DespatchNo}",
            50);

    private static string BuildG01LineDescription(IEnumerable<MatchedG01InboundAtfLine> lines)
    {
        var sequenceNos = string.Join(
            ",",
            lines
                .Select(line => line.Line.SequenceNo)
                .Distinct()
                .OrderBy(value => value));

        return Truncate($"AXATA-S16:{sequenceNos}", 50);
    }
    private async Task<IReadOnlyCollection<G01InboundAtfAnalysis>> AnalyzeDocumentsAsync(
        IReadOnlyCollection<G01InboundAtfDocument> documents,
        CancellationToken cancellationToken)
    {
        if (documents.Count == 0)
        {
            return Array.Empty<G01InboundAtfAnalysis>();
        }

        var parsedDocuments = documents
            .Select(document => new
            {
                Document = document,
                Parsed = ParseDocumentNo(document.OrderDocumentNo)
            })
            .Where(item => item.Parsed.OrderNo.HasValue)
            .ToArray();
        var series = parsedDocuments.Select(item => item.Parsed.Serie).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var orderNos = parsedDocuments.Select(item => item.Parsed.OrderNo!.Value).Distinct().ToArray();
        var orderLines = series.Length == 0
            ? Array.Empty<SIPARISLER>()
            : await mikroWriteDbContext.SIPARISLERs
                .AsNoTracking()
                .Where(order =>
                    order.sip_iptal != true &&
                    order.sip_tip == 0 &&
                    order.sip_cins == 0 &&
                    order.sip_evrakno_seri != null &&
                    series.Contains(order.sip_evrakno_seri) &&
                    order.sip_evrakno_sira.HasValue &&
                    orderNos.Contains(order.sip_evrakno_sira.Value))
                .ToArrayAsync(cancellationToken);
        var existingMovementCounts = await GetExistingMovementCountsAsync(orderLines, cancellationToken);
        var existingRowKeys = await GetExistingRowKeysAsync(documents.SelectMany(document => document.Lines).ToArray(), cancellationToken);
        var orderLinesByDocument = orderLines
            .GroupBy(order => new MikroDocumentKey(order.sip_evrakno_seri ?? string.Empty, order.sip_evrakno_sira ?? 0))
            .ToDictionary(group => group.Key, group => group.ToArray());

        return documents
            .Select(document =>
            {
                var (serie, orderNo) = ParseDocumentNo(document.OrderDocumentNo);
                var documentOrderLines = orderNo.HasValue
                    ? orderLinesByDocument.GetValueOrDefault(new MikroDocumentKey(serie, orderNo.Value)) ?? Array.Empty<SIPARISLER>()
                    : Array.Empty<SIPARISLER>();
                var matchedLines = MatchLines(document, documentOrderLines);
                var existingMovementLineCount = documentOrderLines.Sum(order => existingMovementCounts.GetValueOrDefault(order.sip_Guid)) +
                    document.Lines.Count(line => existingRowKeys.Contains(BuildMovementGroupCode(line.SequenceNo)));
                var warehouseNo = documentOrderLines
                    .Select(order => order.sip_depono ?? 0)
                    .Where(value => value > 0)
                    .Distinct()
                    .SingleOrDefault();
                var warning = BuildWarning(document, serie, orderNo, documentOrderLines, matchedLines, existingMovementLineCount, warehouseNo);

                return new G01InboundAtfAnalysis(
                    document,
                    document.Lines,
                    matchedLines,
                    new AxataG01InboundAtfDocumentDto(
                        document.OrderDocumentNo,
                        serie,
                        orderNo ?? 0,
                        document.Lines.Select(line => line.CustomerCode).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty,
                        document.Lines.Select(line => line.DespatchNo).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty,
                        warehouseNo,
                        document.Lines.Count,
                        document.Lines.Sum(line => line.Quantity),
                        documentOrderLines.Length,
                        documentOrderLines.Sum(order => order.sip_miktar ?? 0d),
                        documentOrderLines.Sum(order => order.sip_teslim_miktar ?? 0d),
                        existingMovementLineCount,
                        string.IsNullOrWhiteSpace(warning),
                        warning));
            })
            .ToArray();
    }

    private async Task<Dictionary<Guid, int>> GetExistingMovementCountsAsync(
        IReadOnlyCollection<SIPARISLER> orderLines,
        CancellationToken cancellationToken)
    {
        var orderGuids = orderLines.Select(order => order.sip_Guid).Where(guid => guid != Guid.Empty).Distinct().ToArray();
        if (orderGuids.Length == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var rows = await mikroWriteDbContext.STOK_HAREKETLERIs
            .AsNoTracking()
            .Where(movement =>
                movement.sth_sip_uid.HasValue &&
                orderGuids.Contains(movement.sth_sip_uid.Value) &&
                movement.sth_iptal != true &&
                movement.sth_tip == MovementIn &&
                movement.sth_cins == MovementGenre &&
                movement.sth_evraktip == CompanyReceivingDocumentType)
            .GroupBy(movement => movement.sth_sip_uid!.Value)
            .Select(group => new
            {
                OrderGuid = group.Key,
                Count = group.Count()
            })
            .ToArrayAsync(cancellationToken);

        return rows.ToDictionary(row => row.OrderGuid, row => row.Count);
    }

    private async Task<HashSet<string>> GetExistingRowKeysAsync(
        IReadOnlyCollection<G01InboundAtfLine> lines,
        CancellationToken cancellationToken)
    {
        var rowKeys = lines.Select(line => BuildMovementGroupCode(line.SequenceNo)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (rowKeys.Length == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return (await mikroWriteDbContext.STOK_HAREKETLERIs
                .AsNoTracking()
                .Where(movement =>
                    movement.sth_HareketGrupKodu1 != null &&
                    rowKeys.Contains(movement.sth_HareketGrupKodu1))
                .Select(movement => movement.sth_HareketGrupKodu1 ?? string.Empty)
                .Distinct()
                .ToArrayAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyCollection<MatchedG01InboundAtfLine> MatchLines(
        G01InboundAtfDocument document,
        IReadOnlyCollection<SIPARISLER> orderLines)
    {
        var result = new List<MatchedG01InboundAtfLine>();
        var matchedQuantitiesByOrderLine = new Dictionary<Guid, double>();

        foreach (var line in document.Lines.Where(line => line.Quantity > QuantityTolerance))
        {
            var orderLine = orderLines
                .Where(order =>
                    order.sip_satirno == line.LineNo &&
                    string.Equals(order.sip_stok_kod?.Trim(), line.StockCode, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(order =>
                {
                    var alreadyMatched = matchedQuantitiesByOrderLine.GetValueOrDefault(order.sip_Guid);
                    return GetRemainingQuantity(order) - alreadyMatched + QuantityTolerance >= line.Quantity;
                });

            if (orderLine is null)
            {
                continue;
            }

            matchedQuantitiesByOrderLine[orderLine.sip_Guid] =
                matchedQuantitiesByOrderLine.GetValueOrDefault(orderLine.sip_Guid) + line.Quantity;
            result.Add(new MatchedG01InboundAtfLine(line, orderLine));
        }

        return result;
    }

    private static string? BuildWarning(
        G01InboundAtfDocument document,
        string documentSerie,
        int? documentOrderNo,
        IReadOnlyCollection<SIPARISLER> orderLines,
        IReadOnlyCollection<MatchedG01InboundAtfLine> matchedLines,
        int existingMovementLineCount,
        int warehouseNo)
    {
        if (string.IsNullOrWhiteSpace(document.OrderDocumentNo) ||
            string.IsNullOrWhiteSpace(documentSerie) ||
            !documentOrderNo.HasValue)
        {
            return "AXATA S16SIPN seri.sira formatinda degil.";
        }

        if (document.Lines.Count == 0 || document.Lines.All(line => line.Quantity <= QuantityTolerance))
        {
            return "AXATA G01 ATF satiri yok veya miktarlar sifir.";
        }

        if (orderLines.Count == 0)
        {
            return "Mikro firma siparisi bulunamadi.";
        }

        if (matchedLines.Count != document.Lines.Count(line => line.Quantity > QuantityTolerance))
        {
            return "AXATA G01 ATF satirlari Mikro firma siparisi satirlariyla guvenli eslesmedi.";
        }

        if (warehouseNo <= 0)
        {
            return "Mikro firma siparisi depo bilgisi tekil veya gecerli degil.";
        }

        if (existingMovementLineCount > 0)
        {
            return "Bu G01 ATF icin Mikro firma mal kabul hareketi zaten var; duplicate fis olusturulmaz.";
        }

        return null;
    }

    private async Task<IReadOnlyCollection<G01InboundAtfLine>> FetchPendingLinesAsync(CancellationToken cancellationToken)
    {
        var configuration = GetRequiredConfiguration(requireExtendedEndpoint: false);
        var client = CreateMainClient(configuration.MainEndpointUrl);
        AxataMain.getInboundATF_Res response;

        try
        {
            response = await client
                .getInboundATFListAsync(
                    new AxataMain.getInboundDelivery_Req(
                        configuration.Username,
                        configuration.Password,
                        new AxataMain.InboundDeliveryQuery
                        {
                            CompanyCode = CompanyCode,
                            WarehouseCode = WarehouseCode,
                            MovementType = MovementType,
                            Status = PendingStatus
                        }))
                .WaitAsync(cancellationToken);

            CloseWcfClient(client);
        }
        catch
        {
            AbortWcfClient(client);
            throw;
        }

        if (response.state != 0)
        {
            throw new InvalidOperationException(
                $"AXATA {FetchOperationName} failed: {NormalizeText(response.message)}");
        }

        return (response.InboundATFList ?? Array.Empty<AxataMain.InboundATF>())
            .Select(item => item.ENT016_IRS)
            .Where(line => line is not null)
            .Select(line => new G01InboundAtfLine(
                line!.S16SIRA,
                NormalizeText(line.S16SIPN),
                ParseInt(line.S16KALN) ?? 0,
                NormalizeText(line.S16SKU),
                line.S16MIKT.HasValue ? (double)line.S16MIKT.Value : 0d,
                NormalizeText(line.S16FIRM),
                NormalizeText(line.S16IRSN),
                FormatDecimal(line.S16STAT),
                ParseDate(FormatDecimal(line.S16ITAR))))
            .Where(line =>
                string.IsNullOrWhiteSpace(line.Status) ||
                string.Equals(line.Status, PendingStatus, StringComparison.OrdinalIgnoreCase))
            .OrderBy(line => line.SequenceNo)
            .ToArray();
    }

    private async Task<int> GetNextLineNoAsync(
        string documentSerie,
        int documentOrderNo,
        CancellationToken cancellationToken)
    {
        var currentMax = await mikroWriteDbContext.STOK_HAREKETLERIs
            .Where(movement =>
                movement.sth_tip == MovementIn &&
                movement.sth_cins == MovementGenre &&
                movement.sth_normal_iade == NormalReturn &&
                movement.sth_evraktip == CompanyReceivingDocumentType &&
                movement.sth_evrakno_seri == documentSerie &&
                movement.sth_evrakno_sira == documentOrderNo)
            .MaxAsync(movement => movement.sth_satirno, cancellationToken);

        return currentMax.HasValue ? currentMax.Value + 1 : 0;
    }

    private async Task AcknowledgeAsync(long sequenceNo, CancellationToken cancellationToken)
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
                            TableName = "ENT016_IRS",
                            UpdateField = "S16STAT",
                            UpdateValue = CompletedStatus,
                            IDField = "S16SIRA",
                            IDValues = new AxataExt.IDList
                            {
                                sequenceNo.ToString(CultureInfo.InvariantCulture)
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

        if (response.state != 0)
        {
            throw new InvalidOperationException(
                $"AXATA {AckOperationName} failed: {NormalizeText(response.message)}");
        }
    }

    private static STOK_HAREKETLERI CreateMovement(
        MatchedG01InboundAtfLine matchedLine,
        string documentSerie,
        int documentOrderNo,
        int rowNo,
        DateTime now,
        DateTime movementDate)
    {
        var line = matchedLine.Line;
        var order = matchedLine.OrderLine;
        var warehouseNo = order.sip_depono ?? 0;

        return new STOK_HAREKETLERI
        {
            sth_Guid = Guid.NewGuid(),
            sth_DBCno = 0,
            sth_SpecRECno = 0,
            sth_iptal = false,
            sth_fileid = 16,
            sth_hidden = false,
            sth_kilitli = false,
            sth_degisti = false,
            sth_checksum = 0,
            sth_create_user = MikroUserNo,
            sth_create_date = now,
            sth_lastup_user = MikroUserNo,
            sth_lastup_date = now,
            sth_special1 = string.Empty,
            sth_special2 = string.Empty,
            sth_special3 = string.Empty,
            sth_firmano = 0,
            sth_subeno = 0,
            sth_tarih = movementDate,
            sth_tip = MovementIn,
            sth_cins = MovementGenre,
            sth_normal_iade = NormalReturn,
            sth_evraktip = CompanyReceivingDocumentType,
            sth_evrakno_seri = documentSerie,
            sth_evrakno_sira = documentOrderNo,
            sth_satirno = rowNo,
            sth_belge_no = Truncate(line.DespatchNo, 50),
            sth_belge_tarih = movementDate,
            sth_stok_kod = line.StockCode,
            sth_isk_mas1 = 0,
            sth_isk_mas2 = 1,
            sth_isk_mas3 = 1,
            sth_isk_mas4 = 1,
            sth_isk_mas5 = 1,
            sth_isk_mas6 = 1,
            sth_isk_mas7 = 1,
            sth_isk_mas8 = 1,
            sth_isk_mas9 = 1,
            sth_isk_mas10 = 1,
            sth_sat_iskmas1 = false,
            sth_sat_iskmas2 = false,
            sth_sat_iskmas3 = false,
            sth_sat_iskmas4 = false,
            sth_sat_iskmas5 = false,
            sth_sat_iskmas6 = false,
            sth_sat_iskmas7 = false,
            sth_sat_iskmas8 = false,
            sth_sat_iskmas9 = false,
            sth_sat_iskmas10 = false,
            sth_pos_satis = 0,
            sth_promosyon_fl = false,
            sth_cari_cinsi = 0,
            sth_cari_kodu = line.CustomerCode,
            sth_cari_grup_no = 0,
            sth_isemri_gider_kodu = string.Empty,
            sth_plasiyer_kodu = string.Empty,
            sth_har_doviz_cinsi = 0,
            sth_har_doviz_kuru = 1d,
            sth_alt_doviz_kuru = 0d,
            sth_stok_doviz_cinsi = 0,
            sth_stok_doviz_kuru = 1d,
            sth_miktar = line.Quantity,
            sth_miktar2 = 0d,
            sth_birim_pntr = 1,
            sth_tutar = 0d,
            sth_iskonto1 = 0d,
            sth_iskonto2 = 0d,
            sth_iskonto3 = 0d,
            sth_iskonto4 = 0d,
            sth_iskonto5 = 0d,
            sth_iskonto6 = 0d,
            sth_masraf1 = 0d,
            sth_masraf2 = 0d,
            sth_masraf3 = 0d,
            sth_masraf4 = 0d,
            sth_vergi_pntr = 0,
            sth_vergi = 0d,
            sth_masraf_vergi_pntr = 0,
            sth_masraf_vergi = 0d,
            sth_netagirlik = 0d,
            sth_odeme_op = 0,
            sth_aciklama = $"{line.OrderDocumentNo}.{line.DespatchNo}",
            sth_sip_uid = order.sip_Guid,
            sth_fat_uid = Guid.Empty,
            sth_giris_depo_no = warehouseNo,
            sth_cikis_depo_no = warehouseNo,
            sth_malkbl_sevk_tarihi = movementDate,
            sth_cari_srm_merkezi = string.Empty,
            sth_stok_srm_merkezi = string.Empty,
            sth_fis_tarihi = new DateTime(1899, 12, 30),
            sth_fis_sirano = 0,
            sth_vergisiz_fl = false,
            sth_maliyet_ana = 0d,
            sth_maliyet_alternatif = 0d,
            sth_maliyet_orjinal = 0d,
            sth_adres_no = 1,
            sth_parti_kodu = string.Empty,
            sth_lot_no = 0,
            sth_kons_uid = Guid.Empty,
            sth_proje_kodu = string.Empty,
            sth_exim_kodu = string.Empty,
            sth_otv_pntr = 0,
            sth_otv_vergi = 0d,
            sth_brutagirlik = 0d,
            sth_disticaret_turu = 0,
            sth_otvtutari = 0d,
            sth_otvvergisiz_fl = false,
            sth_oiv_pntr = 0,
            sth_oiv_vergi = 0d,
            sth_oivvergisiz_fl = false,
            sth_fiyat_liste_no = 0,
            sth_oivtutari = 0d,
            sth_Tevkifat_turu = 0,
            sth_nakliyedeposu = 0,
            sth_nakliyedurumu = 0,
            sth_yetkili_uid = Guid.Empty,
            sth_taxfree_fl = false,
            sth_ilave_edilecek_kdv = 0d,
            sth_ismerkezi_kodu = string.Empty,
            sth_HareketGrupKodu1 = BuildMovementGroupCode(line.SequenceNo),
            sth_HareketGrupKodu2 = string.Empty,
            sth_HareketGrupKodu3 = string.Empty,
            sth_Olcu1 = 0d,
            sth_Olcu2 = 0d,
            sth_Olcu3 = 0d,
            sth_Olcu4 = 0d,
            sth_Olcu5 = 0d,
            sth_FormulMiktarNo = 0,
            sth_FormulMiktar = 0d,
            sth_eirs_senaryo = 0,
            sth_eirs_tipi = 0,
            sth_teslim_tarihi = movementDate,
            sth_matbu_fl = false,
            sth_satis_fiyat_doviz_cinsi = 0,
            sth_satis_fiyat_doviz_kuru = 1d,
            sth_eticaret_kanal_kodu = string.Empty,
            sth_bagli_ithalat_kodu = string.Empty,
            sth_tevkifat_sifirlandi_fl = false
        };
    }

    private AxataG01Configuration GetRequiredConfiguration(bool requireExtendedEndpoint)
    {
        var currentOptions = options.CurrentValue;

        if (!currentOptions.Enabled)
        {
            throw new InvalidOperationException("AXATA synchronization is disabled.");
        }

        if (string.IsNullOrWhiteSpace(currentOptions.MainEndpointUrl))
        {
            throw new InvalidOperationException("AXATA main endpoint URL is not configured.");
        }

        if (requireExtendedEndpoint && string.IsNullOrWhiteSpace(currentOptions.ExtendedEndpointUrl))
        {
            throw new InvalidOperationException("AXATA extended endpoint URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(currentOptions.Username) ||
            string.IsNullOrWhiteSpace(currentOptions.Password))
        {
            throw new InvalidOperationException("AXATA credentials are not configured.");
        }

        return new AxataG01Configuration(
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

    private static int NormalizeTake(int? value)
    {
        if (!value.HasValue || value.Value <= 0)
        {
            return DefaultTake;
        }

        return Math.Min(value.Value, MaxTake);
    }

    private static (string Serie, int? OrderNo) ParseDocumentNo(string value)
    {
        var trimmed = value.Trim();
        var separatorIndex = trimmed.LastIndexOf('.');
        if (separatorIndex <= 0 || separatorIndex >= trimmed.Length - 1)
        {
            return (trimmed, null);
        }

        var serie = trimmed[..separatorIndex].Trim();
        var orderNoText = trimmed[(separatorIndex + 1)..].Trim();
        return int.TryParse(orderNoText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var orderNo)
            ? (serie, orderNo)
            : (serie, null);
    }

    private static double GetRemainingQuantity(SIPARISLER orderLine) =>
        (orderLine.sip_miktar ?? 0d) - (orderLine.sip_teslim_miktar ?? 0d);

    private static string BuildMovementGroupCode(long sequenceNo) =>
        $"AXATA-G01:{sequenceNo.ToString(CultureInfo.InvariantCulture)}";

    private static int? ParseInt(string? value) =>
        int.TryParse((value ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static string FormatDecimal(decimal? value) =>
        value?.ToString("0.#############################", CultureInfo.InvariantCulture) ?? string.Empty;

    private static DateTime? ParseDate(string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return DateTime.TryParseExact(
            trimmed,
            ["yyyyMMdd", "yyyyMMddHHmmss"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed
            : null;
    }

    private static string NormalizeText(string? value) =>
        value?.Trim() ?? string.Empty;

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}

internal sealed record G01InboundAtfLine(
    long SequenceNo,
    string OrderDocumentNo,
    int LineNo,
    string StockCode,
    double Quantity,
    string CustomerCode,
    string DespatchNo,
    string Status,
    DateTime? AxataDate);

internal sealed record G01InboundAtfDocument(
    string OrderDocumentNo,
    IReadOnlyCollection<G01InboundAtfLine> Lines);

internal sealed record MatchedG01InboundAtfLine(
    G01InboundAtfLine Line,
    SIPARISLER OrderLine);

internal sealed record G01InboundAtfAnalysis(
    G01InboundAtfDocument Document,
    IReadOnlyCollection<G01InboundAtfLine> Lines,
    IReadOnlyCollection<MatchedG01InboundAtfLine> MatchedLines,
    AxataG01InboundAtfDocumentDto Dto);

internal sealed record AxataG01Configuration(
    string MainEndpointUrl,
    string ExtendedEndpointUrl,
    string Username,
    string Password);

using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Application.Modules.FaturaIslemleri.Common;
using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGonderimi;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using FurpaMerkezApi.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UyumsoftInvoice = FurpaMerkezApi.Infrastructure.Services.ServiceReferences.Uyumsoft.Invoice;

namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGonderimi;

public sealed class InvoiceSendingService(
    MikroDbContext mikroDbContext,
    MikroWriteDbContext mikroWriteDbContext,
    IEInvoiceDocumentRenderer invoiceDocumentRenderer,
    UblTrInvoiceBusinessRuleValidator ublTrInvoiceBusinessRuleValidator,
    UblTrInvoiceXmlValidator ublTrInvoiceXmlValidator,
    IOptions<UyumsoftConnectedServicesOptions> uyumsoftOptions,
    IOptions<EDespatchOptions> eDespatchOptions,
    IHostEnvironment hostEnvironment,
    ILogger<InvoiceSendingService> logger)
{
    private const string InvoiceNamespace = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private const string ExtensionNamespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
    private const string AggregateNamespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private const string BasicNamespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private const string PlaceholderExtensionNamespace = "urn:furpa:ubl:extension-placeholder";
    private const short MikroUserNo = 39;
    private const string CurrencyCode = "TRY";
    private const string PreviewSource = "pending-send";
    private static readonly CultureInfo TurkishCulture = new("tr-TR");

    public async Task<InvoiceSendingListResponse> ListAsync(
        InvoiceSendingListRequest request,
        CancellationToken cancellationToken)
    {
        ValidateListRequest(request);

        var items = await LoadPendingInvoicesAsync(
            request.Scenario,
            request.StartDate.Date,
            request.EndDate.Date,
            null,
            null,
            cancellationToken);
        items = ApplySentState(items, request.SentState);

        var mappedItems = items
            .OrderByDescending(item => item.DocumentDate)
            .ThenByDescending(item => item.DocumentSerie, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(item => item.DocumentOrderNo)
            .Select(MapListItem)
            .ToArray();

        return new InvoiceSendingListResponse(mappedItems.Length, mappedItems);
    }

    public async Task<InvoiceSendingDetailDto> RenderAsync(
        InvoiceSendingRenderRequest request,
        CancellationToken cancellationToken)
    {
        var invoice = await LoadSingleInvoiceAsync(
            request.Scenario,
            request.DocumentSerie,
            request.DocumentOrderNo,
            cancellationToken);
        var builtInvoice = await BuildInvoiceDocumentAsync(invoice, cancellationToken);
        var profile = request.Profile == InvoiceDocumentProfile.Auto
            ? MapProfile(request.Scenario)
            : request.Profile;
        var preferEmbeddedXslt = request.PreferEmbeddedXslt ?? true;
        var renderedDocument = await invoiceDocumentRenderer.RenderXmlAsync(
            PreviewSource,
            builtInvoice.InvoiceId,
            builtInvoice.XmlContent,
            profile,
            preferEmbeddedXslt,
            cancellationToken,
            request.FallbackToDefaultXslt);

        return new InvoiceSendingDetailDto(
            MapListItem(invoice),
            renderedDocument with { InvoiceId = builtInvoice.InvoiceId });
    }

    public async Task<SendInvoiceDocumentsResponse> SendAsync(
        SendInvoiceDocumentsRequest request,
        CancellationToken cancellationToken)
    {
        ValidateSendRequest(request);
        ValidateConfiguration();

        var deduplicatedDocuments = request.Documents
            .Where(document => !string.IsNullOrWhiteSpace(document.DocumentSerie))
            .Select(document => new SendInvoiceDocumentSelection(document.DocumentSerie.Trim(), document.DocumentOrderNo))
            .Distinct()
            .ToArray();
        var results = new List<SendInvoiceDocumentResultDto>(deduplicatedDocuments.Length);

        foreach (var document in deduplicatedDocuments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PendingInvoiceRecord? invoice = null;

            try
            {
                invoice = await LoadSingleInvoiceAsync(
                    request.Scenario,
                    document.DocumentSerie,
                    document.DocumentOrderNo,
                    cancellationToken);

                if (invoice.IsSent)
                {
                    results.Add(new SendInvoiceDocumentResultDto(
                        invoice.DocumentSerie,
                        invoice.DocumentOrderNo,
                        invoice.InvoiceId,
                        invoice.CustomerCode,
                        invoice.CustomerTitle,
                        false,
                        null,
                        invoice.SentDocumentNo,
                        "Belge zaten gonderilmis."));
                    continue;
                }

                invoice = await EnsureReturnReferenceBeforeSendAsync(invoice, cancellationToken);
                var builtInvoice = await BuildInvoiceDocumentAsync(invoice, cancellationToken);
                ublTrInvoiceBusinessRuleValidator.Validate(
                    builtInvoice.XmlContent,
                    builtInvoice.InvoiceId,
                    request.Scenario,
                    builtInvoice.TargetAlias);
                ublTrInvoiceXmlValidator.Validate(builtInvoice.XmlContent, builtInvoice.InvoiceId);
                var serviceResponse = await SendToUyumsoftAsync(
                    builtInvoice,
                    request.Scenario,
                    cancellationToken);

                await MarkAsSentAsync(
                    invoice.DocumentSerie,
                    invoice.DocumentOrderNo,
                    serviceResponse.ServiceDocumentNumber,
                    cancellationToken);

                results.Add(new SendInvoiceDocumentResultDto(
                    invoice.DocumentSerie,
                    invoice.DocumentOrderNo,
                    invoice.InvoiceId,
                    invoice.CustomerCode,
                    invoice.CustomerTitle,
                    true,
                    serviceResponse.ServiceDocumentId,
                    serviceResponse.ServiceDocumentNumber,
                    "Gonderim basarili."));
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Invoice send failed for {Scenario} {DocumentSerie}/{DocumentOrderNo}.",
                    request.Scenario,
                    document.DocumentSerie,
                    document.DocumentOrderNo);

                results.Add(new SendInvoiceDocumentResultDto(
                    invoice?.DocumentSerie ?? document.DocumentSerie,
                    invoice?.DocumentOrderNo ?? document.DocumentOrderNo,
                    invoice?.InvoiceId ?? BuildInvoiceId(document.DocumentSerie, document.DocumentOrderNo, DateTime.Today.Year),
                    invoice?.CustomerCode ?? string.Empty,
                    invoice?.CustomerTitle ?? string.Empty,
                    false,
                    null,
                    null,
                    exception.Message));
            }
        }

        var succeededCount = results.Count(result => result.IsSucceeded);
        var failedCount = results.Count - succeededCount;

        return new SendInvoiceDocumentsResponse(
            request.Scenario,
            deduplicatedDocuments.Length,
            succeededCount,
            failedCount,
            results);
    }

    public async Task<ValidateInvoiceDocumentsResponse> ValidateAsync(
        ValidateInvoiceDocumentsRequest request,
        CancellationToken cancellationToken)
    {
        ValidateSendRequest(new SendInvoiceDocumentsRequest(request.Scenario, request.Documents));
        ValidatePreflightConfiguration();

        var deduplicatedDocuments = request.Documents
            .Where(document => !string.IsNullOrWhiteSpace(document.DocumentSerie))
            .Select(document => new SendInvoiceDocumentSelection(document.DocumentSerie.Trim(), document.DocumentOrderNo))
            .Distinct()
            .ToArray();
        var results = new List<ValidateInvoiceDocumentResultDto>(deduplicatedDocuments.Length);

        foreach (var document in deduplicatedDocuments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PendingInvoiceRecord? invoice = null;

            try
            {
                invoice = await LoadSingleInvoiceAsync(
                    request.Scenario,
                    document.DocumentSerie,
                    document.DocumentOrderNo,
                    cancellationToken);

                if (invoice.IsSent)
                {
                    results.Add(new ValidateInvoiceDocumentResultDto(
                        invoice.DocumentSerie,
                        invoice.DocumentOrderNo,
                        invoice.InvoiceId,
                        invoice.CustomerCode,
                        invoice.CustomerTitle,
                        false,
                        "Belge zaten gonderilmis."));
                    continue;
                }

                invoice = await ResolveReturnReferenceForValidationAsync(invoice, cancellationToken);
                var builtInvoice = await BuildInvoiceDocumentAsync(invoice, cancellationToken);
                ublTrInvoiceBusinessRuleValidator.Validate(
                    builtInvoice.XmlContent,
                    builtInvoice.InvoiceId,
                    request.Scenario,
                    builtInvoice.TargetAlias);
                ublTrInvoiceXmlValidator.Validate(builtInvoice.XmlContent, builtInvoice.InvoiceId);

                results.Add(new ValidateInvoiceDocumentResultDto(
                    invoice.DocumentSerie,
                    invoice.DocumentOrderNo,
                    invoice.InvoiceId,
                    invoice.CustomerCode,
                    invoice.CustomerTitle,
                    true,
                    "Gonderim oncesi kontrol basarili."));
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Invoice pre-send validation failed for {Scenario} {DocumentSerie}/{DocumentOrderNo}.",
                    request.Scenario,
                    document.DocumentSerie,
                    document.DocumentOrderNo);

                results.Add(new ValidateInvoiceDocumentResultDto(
                    invoice?.DocumentSerie ?? document.DocumentSerie,
                    invoice?.DocumentOrderNo ?? document.DocumentOrderNo,
                    invoice?.InvoiceId ?? BuildInvoiceId(document.DocumentSerie, document.DocumentOrderNo, DateTime.Today.Year),
                    invoice?.CustomerCode ?? string.Empty,
                    invoice?.CustomerTitle ?? string.Empty,
                    false,
                    exception.Message));
            }
        }

        var validCount = results.Count(result => result.IsValid);
        var invalidCount = results.Count - validCount;

        return new ValidateInvoiceDocumentsResponse(
            request.Scenario,
            deduplicatedDocuments.Length,
            validCount,
            invalidCount,
            results);
    }

    public async Task<InvoiceReturnReferenceCandidatesResponse> ListReturnReferenceCandidatesAsync(
        InvoiceReturnReferenceCandidatesRequest request,
        CancellationToken cancellationToken)
    {
        var invoice = await LoadSingleInvoiceAsync(
            request.Scenario,
            request.DocumentSerie,
            request.DocumentOrderNo,
            cancellationToken);
        EnsureReturnInvoice(invoice);

        var candidates = await LoadReturnReferenceCandidatesAsync(
            invoice,
            null,
            null,
            100,
            cancellationToken);
        var fallback = candidates.FirstOrDefault();

        return new InvoiceReturnReferenceCandidatesResponse(
            MapListItem(invoice),
            CreateCurrentReturnReference(invoice),
            fallback is null ? null : MapReturnReferenceCandidate(fallback, invoice, true),
            candidates
                .Select((candidate, index) => MapReturnReferenceCandidate(candidate, invoice, index == 0))
                .ToArray());
    }

    public async Task<UpdateInvoiceReturnReferenceResponse> UpdateReturnReferenceAsync(
        UpdateInvoiceReturnReferenceRequest request,
        CancellationToken cancellationToken)
    {
        var invoice = await LoadSingleInvoiceAsync(
            request.Scenario,
            request.DocumentSerie,
            request.DocumentOrderNo,
            cancellationToken);
        EnsureReturnInvoice(invoice);

        ReturnReferenceCandidateRecord? reference = null;
        var isFallbackReference = false;

        if (!string.IsNullOrWhiteSpace(request.SourceDocumentSerie) &&
            request.SourceDocumentOrderNo is > 0)
        {
            reference = (await LoadReturnReferenceCandidatesAsync(
                    invoice,
                    request.SourceDocumentSerie,
                    request.SourceDocumentOrderNo,
                    1,
                    cancellationToken))
                .FirstOrDefault();
        }

        if (reference is null && request.UseFallbackWhenNotSelected)
        {
            reference = (await LoadReturnReferenceCandidatesAsync(
                    invoice,
                    null,
                    null,
                    1,
                    cancellationToken))
                .FirstOrDefault();
            isFallbackReference = reference is not null;
        }

        if (reference is null)
        {
            throw new InvalidOperationException(
                "Iade faturasi icin iadeye konu fatura secilmeli veya gecici fallback kullanilmalidir.");
        }

        await SaveReturnReferenceAsync(invoice.InvoiceGuid, reference, cancellationToken);
        var updatedInvoice = invoice with
        {
            ReturnInvoiceNo = reference.InvoiceNo,
            ReturnInvoiceDate = reference.InvoiceDate
        };

        return new UpdateInvoiceReturnReferenceResponse(
            MapListItem(updatedInvoice),
            CreateReturnReference(
                reference,
                isFallbackReference
                    ? ResolveReferenceSource(reference, "fallback")
                    : ResolveReferenceSource(reference, "selected")));
    }

    private async Task<IReadOnlyCollection<PendingInvoiceRecord>> LoadPendingInvoicesAsync(
        InvoiceSendingScenario scenario,
        DateTime? startDate,
        DateTime? endDate,
        string? documentSerie,
        int? documentOrderNo,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH Faturalar AS (
                SELECT
                    ch.cha_Guid AS FatGuid,
                    ch.cha_evrakno_seri AS DocumentSerie,
                    ch.cha_evrakno_sira AS DocumentOrderNo,
                    ch.cha_evrak_tip AS EvrakTip,
                    ch.cha_normal_Iade AS Iade,
                    CAST(ch.cha_belge_tarih AS date) AS BelgeTarihi,
                    ch.cha_aciklama AS Aciklama,
                    ch.cha_belge_no AS BelgeNo,
                    ISNULL(ch.cha_aratoplam, 0) AS AraToplam,
                    ISNULL(ch.cha_vergi1, 0)
                        + ISNULL(ch.cha_vergi2, 0)
                        + ISNULL(ch.cha_vergi3, 0)
                        + ISNULL(ch.cha_vergi4, 0)
                        + ISNULL(ch.cha_vergi5, 0)
                        + ISNULL(ch.cha_vergi6, 0)
                        + ISNULL(ch.cha_vergi7, 0)
                        + ISNULL(ch.cha_vergi8, 0)
                        + ISNULL(ch.cha_vergi9, 0)
                        + ISNULL(ch.cha_vergi10, 0) AS TaxTotal,
                    ISNULL(ch.cha_ebelge_turu, 0) AS EBelgeTuru,
                    LTRIM(RTRIM(CONCAT(
                        ISNULL(c.cari_unvan1, N''),
                        CASE WHEN ISNULL(c.cari_unvan2, N'') = N'' THEN N'' ELSE N' ' + c.cari_unvan2 END))) AS MusteriAdi,
                    c.cari_kod AS MusteriKodu,
                    ISNULL(NULLIF(c.cari_vdaire_no, N''), ISNULL(c.cari_VergiKimlikNo, N'')) AS VDNo,
                    c.cari_efatura_fl AS EFaturaMukellefiMi,
                    ch.cha_cinsi AS CariHareketCins,
                    c.cari_vdaire_adi AS VergiDairesi,
                    adr.adr_cadde AS Cadde,
                    adr.adr_sokak AS Sokak,
                    adr.adr_ilce AS Ilce,
                    adr.adr_il AS Il,
                    adr.adr_efatura_alias AS FaturaMail,
                    ISNULL(ch.cha_miktari, 0) AS Miktar,
                    adr.adr_posta_kodu AS PostaKodu,
                    c.cari_CepTel AS CariTel,
                    c.cari_EMail AS Mail,
                    COALESCE(
                        NULLIF(LTRIM(RTRIM(ISNULL(ek.cha_Istisna1, N''))), N''),
                        stokIstisna.IstisnaKodu,
                        N'') AS IstisnaKodu,
                    ISNULL(ek.cha_HalRusum, 0) AS Rusum,
                    ISNULL(ek.cha_ozel_matrah_kodu, N'') AS OzelMatrahKodu,
                    ISNULL((SELECT TOP (1) sth_belge_no FROM STOK_HAREKETLERI WITH (NOLOCK) WHERE sth_fat_uid = ch.cha_Guid), N'') AS IrsaliyeNo,
                    (SELECT TOP (1) sth_belge_tarih FROM STOK_HAREKETLERI WITH (NOLOCK) WHERE sth_fat_uid = ch.cha_Guid) AS IrsaliyeTarihi,
                    ISNULL(iadeRef.IadeFaturaNo, N'') AS IadeFaturaNo,
                    iadeRef.IadeFaturaTarihi,
                    ISNULL((
                        SELECT TOP (1) dep.dep_adi
                        FROM STOK_HAREKETLERI sh WITH (NOLOCK)
                        INNER JOIN DEPOLAR dep WITH (NOLOCK) ON dep.dep_no = sh.sth_cikis_depo_no
                        WHERE sh.sth_fat_uid = ch.cha_Guid
                    ), N'') AS Depo
                FROM CARI_HESAP_HAREKETLERI ch WITH (NOLOCK)
                INNER JOIN CARI_HESAPLAR c WITH (NOLOCK) ON ch.cha_ciro_cari_kodu = c.cari_kod
                INNER JOIN CARI_HESAP_ADRESLERI adr WITH (NOLOCK) ON c.cari_kod = adr.adr_cari_kod
                LEFT JOIN CARI_HESAP_HAREKETLERI_EK ek WITH (NOLOCK) ON ch.cha_Guid = ek.chaek_related_uid
                CROSS APPLY (
                    SELECT TOP (1)
                        fatSer.efatura
                    FROM Furpa.dbo.FaturaSeries fatSer WITH (NOLOCK)
                    WHERE
                        NULLIF(LTRIM(RTRIM(ISNULL(fatSer.seri, N''))), N'') IS NOT NULL
                        AND ch.cha_evrakno_seri LIKE CONCAT(LTRIM(RTRIM(fatSer.seri)), N'%')
                    ORDER BY
                        LEN(LTRIM(RTRIM(fatSer.seri))) DESC,
                        LTRIM(RTRIM(fatSer.seri)) DESC
                ) fatSer
                OUTER APPLY (
                    SELECT TOP (1)
                        NULLIF(LTRIM(RTRIM(ISNULL(shek.sth_istisna, N''))), N'') AS IstisnaKodu
                    FROM dbo.STOK_HAREKETLERI sh WITH (NOLOCK)
                    INNER JOIN dbo.STOK_HAREKETLERI_EK shek WITH (NOLOCK)
                        ON shek.sthek_related_uid = sh.sth_Guid
                    WHERE
                        sh.sth_fat_uid = ch.cha_Guid
                        AND ISNULL(sh.sth_iptal, 0) = 0
                        AND ISNULL(shek.sthek_iptal, 0) = 0
                        AND NULLIF(LTRIM(RTRIM(ISNULL(shek.sth_istisna, N''))), N'') IS NOT NULL
                    ORDER BY
                        sh.sth_satirno,
                        shek.sthek_lastup_date DESC,
                        shek.sthek_create_date DESC
                ) stokIstisna
                OUTER APPLY (
                    SELECT TOP (1)
                        LTRIM(RTRIM(ISNULL(ebh.ebh_iade_fat_no1, N''))) AS IadeFaturaNo,
                        COALESCE(
                            TRY_CONVERT(
                                date,
                                NULLIF(CONVERT(nvarchar(30), ebh.ebh_iade_fat_tarihi1), N''),
                                112),
                            TRY_CONVERT(date, ebh.ebh_iade_fat_tarihi1)
                        ) AS IadeFaturaTarihi
                    FROM dbo.EBELGE_EVRAK_HAREKETLERI ebh WITH (NOLOCK)
                    WHERE
                        ebh.ebh_related_uid = ch.cha_Guid
                        AND ISNULL(ebh.ebh_iptal, 0) = 0
                        AND NULLIF(LTRIM(RTRIM(ISNULL(ebh.ebh_iade_fat_no1, N''))), N'') IS NOT NULL
                    ORDER BY
                        ebh.ebh_lastup_date DESC,
                        ebh.ebh_create_date DESC
                ) iadeRef
                WHERE
                    (@startDate IS NULL OR CAST(ch.cha_belge_tarih AS date) >= CAST(@startDate AS date))
                    AND (@endDate IS NULL OR CAST(ch.cha_belge_tarih AS date) <= CAST(@endDate AS date))
                    AND (@documentSerie IS NULL OR ch.cha_evrakno_seri = @documentSerie)
                    AND (@documentOrderNo IS NULL OR ch.cha_evrakno_sira = @documentOrderNo)
                    AND ch.cha_tip = 0
                    AND adr.adr_adres_no = 1
                    AND fatSer.efatura = @efatura
                    AND c.cari_efatura_fl = @efatura
                    AND ISNULL(ch.cha_iptal, 0) = 0
            )
            SELECT
                FatGuid,
                DocumentSerie,
                DocumentOrderNo,
                EvrakTip,
                Iade,
                BelgeTarihi,
                Aciklama,
                BelgeNo,
                SUM(AraToplam) AS AraToplam,
                SUM(TaxTotal) AS TaxTotal,
                SUM(Rusum) AS Rusum,
                EBelgeTuru,
                MusteriAdi,
                MusteriKodu,
                VDNo,
                EFaturaMukellefiMi,
                CariHareketCins,
                VergiDairesi,
                Cadde,
                Sokak,
                Ilce,
                Il,
                FaturaMail,
                SUM(Miktar) AS Miktar,
                PostaKodu,
                CariTel,
                Mail,
                IstisnaKodu,
                OzelMatrahKodu,
                IrsaliyeNo,
                IrsaliyeTarihi,
                IadeFaturaNo,
                IadeFaturaTarihi,
                Depo
            FROM Faturalar
            GROUP BY
                FatGuid,
                DocumentSerie,
                DocumentOrderNo,
                EvrakTip,
                Iade,
                BelgeTarihi,
                Aciklama,
                BelgeNo,
                EBelgeTuru,
                MusteriAdi,
                MusteriKodu,
                VDNo,
                EFaturaMukellefiMi,
                CariHareketCins,
                VergiDairesi,
                Cadde,
                Sokak,
                Ilce,
                Il,
                FaturaMail,
                PostaKodu,
                CariTel,
                Mail,
                IstisnaKodu,
                OzelMatrahKodu,
                IrsaliyeNo,
                IrsaliyeTarihi,
                IadeFaturaNo,
                IadeFaturaTarihi,
                Depo
            ORDER BY
                BelgeTarihi DESC,
                DocumentSerie DESC,
                DocumentOrderNo DESC;
            """;

        var efatura = scenario == InvoiceSendingScenario.EFatura;
        var items = await ExecuteReaderAsync(
            mikroDbContext,
            sql,
            command =>
            {
                AddParameter(command, "@startDate", startDate);
                AddParameter(command, "@endDate", endDate);
                AddParameter(command, "@documentSerie", string.IsNullOrWhiteSpace(documentSerie) ? null : documentSerie.Trim());
                AddParameter(command, "@documentOrderNo", documentOrderNo);
                AddParameter(command, "@efatura", efatura);
            },
            reader => MapPendingInvoice(reader, scenario),
            cancellationToken);

        return items;
    }

    private static IReadOnlyCollection<PendingInvoiceRecord> ApplySentState(
        IReadOnlyCollection<PendingInvoiceRecord> items,
        int sentState)
    {
        return sentState switch
        {
            0 => items.Where(item => !item.IsSent).ToArray(),
            1 => items.Where(item => item.IsSent).ToArray(),
            _ => items.ToArray()
        };
    }

    private async Task<PendingInvoiceRecord> LoadSingleInvoiceAsync(
        InvoiceSendingScenario scenario,
        string documentSerie,
        int documentOrderNo,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(documentSerie))
        {
            throw new ArgumentException("Document serie is required.", nameof(documentSerie));
        }

        var lookupSeries = ResolveDocumentSerieLookupCandidates(documentSerie);

        foreach (var lookupSerie in lookupSeries)
        {
            var items = await LoadPendingInvoicesAsync(
                scenario,
                null,
                null,
                lookupSerie,
                documentOrderNo,
                cancellationToken);

            var invoice = items.FirstOrDefault();
            if (invoice is not null)
            {
                return invoice;
            }
        }

        throw new KeyNotFoundException(
            $"Pending invoice was not found for {documentSerie}/{documentOrderNo}. Scenario={scenario}. Tried series: {string.Join(", ", lookupSeries)}.");
    }

    private static IReadOnlyCollection<string> ResolveDocumentSerieLookupCandidates(string documentSerie)
    {
        var trimmed = documentSerie.Trim();
        var candidates = new List<string> { trimmed };

        if (LooksLikeInvoiceIdDerivedSerie(trimmed))
        {
            candidates.Add(trimmed[..3]);
        }

        return candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool LooksLikeInvoiceIdDerivedSerie(string documentSerie)
    {
        if (documentSerie.Length != 5 ||
            !documentSerie[..3].All(char.IsLetter) ||
            !documentSerie[^2..].All(char.IsDigit))
        {
            return false;
        }

        var yearSuffix = documentSerie[^2..];
        var currentYear = DateTime.Today.Year % 100;
        var plausibleYearSuffixes = Enumerable
            .Range(currentYear - 1, 3)
            .Select(year => ((year + 100) % 100).ToString("D2", System.Globalization.CultureInfo.InvariantCulture));

        return plausibleYearSuffixes.Contains(yearSuffix, StringComparer.Ordinal);
    }

    private async Task<PendingInvoiceRecord> EnsureReturnReferenceBeforeSendAsync(
        PendingInvoiceRecord invoice,
        CancellationToken cancellationToken)
    {
        if (!invoice.IsReturn || !string.IsNullOrWhiteSpace(invoice.ReturnInvoiceNo))
        {
            return invoice;
        }

        var fallback = (await LoadReturnReferenceCandidatesAsync(
                invoice,
                null,
                null,
                1,
                cancellationToken))
            .FirstOrDefault();

        if (fallback is null)
        {
            throw new InvalidOperationException(
                "Iade faturasi icin iadeye konu fatura bulunamadi. Fatura gondermeden once referans fatura secilmelidir.");
        }

        await SaveReturnReferenceAsync(invoice.InvoiceGuid, fallback, cancellationToken);

        return invoice with
        {
            ReturnInvoiceNo = fallback.InvoiceNo,
            ReturnInvoiceDate = fallback.InvoiceDate
        };
    }

    private async Task<PendingInvoiceRecord> ResolveReturnReferenceForValidationAsync(
        PendingInvoiceRecord invoice,
        CancellationToken cancellationToken)
    {
        if (!invoice.IsReturn || !string.IsNullOrWhiteSpace(invoice.ReturnInvoiceNo))
        {
            return invoice;
        }

        var fallback = (await LoadReturnReferenceCandidatesAsync(
                invoice,
                null,
                null,
                1,
                cancellationToken))
            .FirstOrDefault();

        if (fallback is null)
        {
            throw new InvalidOperationException(
                "Iade faturasi icin iadeye konu fatura bulunamadi. Fatura gondermeden once referans fatura secilmelidir.");
        }

        return invoice with
        {
            ReturnInvoiceNo = fallback.InvoiceNo,
            ReturnInvoiceDate = fallback.InvoiceDate
        };
    }

    private async Task<IReadOnlyCollection<ReturnReferenceCandidateRecord>> LoadReturnReferenceCandidatesAsync(
        PendingInvoiceRecord invoice,
        string? sourceDocumentSerie,
        int? sourceDocumentOrderNo,
        int topCount,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH CandidateRows AS (
                SELECT
                    ISNULL(src.cha_evrakno_seri, N'') AS SourceDocumentSerie,
                    ISNULL(src.cha_evrakno_sira, 0) AS SourceDocumentOrderNo,
                    resolved.InvoiceNo,
                    CAST(COALESCE(src.cha_belge_tarih, src.cha_tarihi) AS date) AS InvoiceDate,
                    CAST(src.cha_belge_tarih AS date) AS DocumentDate,
                    src.cha_create_date AS CreatedAt,
                    ISNULL(NULLIF(src.cha_ciro_cari_kodu, N''), ISNULL(src.cha_kod, N'')) AS CustomerCode,
                    LTRIM(RTRIM(CONCAT(
                        ISNULL(c.cari_unvan1, N''),
                        CASE WHEN ISNULL(c.cari_unvan2, N'') = N'' THEN N'' ELSE N' ' + c.cari_unvan2 END))) AS CustomerTitle,
                    ISNULL(src.cha_aratoplam, 0) AS LineExtensionTotal,
                    ISNULL(src.cha_vergi1, 0) AS TaxTotal,
                    ISNULL(src.cha_aratoplam, 0) + ISNULL(src.cha_vergi1, 0) AS PayableTotal,
                    CASE WHEN stored.StoredInvoiceNo IS NULL THEN 1 ELSE 0 END AS IsGeneratedInvoiceNo
                FROM dbo.CARI_HESAP_HAREKETLERI ret WITH (NOLOCK)
                INNER JOIN dbo.CARI_HESAP_HAREKETLERI src WITH (NOLOCK) ON src.cha_kod = ret.cha_kod
                LEFT JOIN dbo.CARI_HESAPLAR c WITH (NOLOCK)
                    ON c.cari_kod = ISNULL(NULLIF(src.cha_ciro_cari_kodu, N''), src.cha_kod)
                CROSS APPLY (
                    SELECT NULLIF(LTRIM(RTRIM(ISNULL(src.cha_belge_no, N''))), N'') AS StoredInvoiceNo
                ) stored
                CROSS APPLY (
                    SELECT
                        CASE
                            WHEN stored.StoredInvoiceNo IS NOT NULL THEN stored.StoredInvoiceNo
                            WHEN
                                LEN(ISNULL(src.cha_evrakno_seri, N'')) BETWEEN 1 AND 13
                                AND src.cha_evrakno_sira IS NOT NULL
                            THEN
                                LEFT(src.cha_evrakno_seri, 3)
                                + N'20'
                                + RIGHT(CONVERT(nvarchar(4), YEAR(GETDATE())), 2)
                                + RIGHT(
                                    REPLICATE(N'0', 16) + CONVERT(nvarchar(20), src.cha_evrakno_sira),
                                    14 - LEN(src.cha_evrakno_seri))
                            ELSE N''
                        END AS InvoiceNo
                ) resolved
                WHERE
                    ret.cha_Guid = @returnInvoiceGuid
                    AND src.cha_Guid <> ret.cha_Guid
                    AND ISNULL(src.cha_iptal, 0) = 0
                    AND ISNULL(src.cha_normal_Iade, 0) = 0
                    AND src.cha_evrak_tip = 0
                    AND src.cha_tip = 1
                    AND (@sourceDocumentSerie IS NULL OR src.cha_evrakno_seri = @sourceDocumentSerie)
                    AND (@sourceDocumentOrderNo IS NULL OR src.cha_evrakno_sira = @sourceDocumentOrderNo)
                    AND src.cha_create_date <= GETDATE()
                    AND NULLIF(resolved.InvoiceNo, N'') IS NOT NULL
            )
            SELECT TOP (@topCount)
                SourceDocumentSerie,
                SourceDocumentOrderNo,
                InvoiceNo,
                InvoiceDate,
                DocumentDate,
                MIN(CreatedAt) AS CreatedAt,
                CustomerCode,
                CustomerTitle,
                SUM(LineExtensionTotal) AS LineExtensionTotal,
                SUM(TaxTotal) AS TaxTotal,
                SUM(PayableTotal) AS PayableTotal,
                MAX(IsGeneratedInvoiceNo) AS IsGeneratedInvoiceNo
            FROM CandidateRows
            GROUP BY
                SourceDocumentSerie,
                SourceDocumentOrderNo,
                InvoiceNo,
                InvoiceDate,
                DocumentDate,
                CustomerCode,
                CustomerTitle
            ORDER BY
                COALESCE(InvoiceDate, DocumentDate) DESC,
                MIN(CreatedAt) DESC,
                SourceDocumentSerie DESC,
                SourceDocumentOrderNo DESC;
            """;

        var rows = await ExecuteReaderAsync(
            mikroDbContext,
            sql,
            command =>
            {
                AddParameter(command, "@returnInvoiceGuid", invoice.InvoiceGuid);
                AddParameter(command, "@sourceDocumentSerie", string.IsNullOrWhiteSpace(sourceDocumentSerie) ? null : sourceDocumentSerie.Trim());
                AddParameter(command, "@sourceDocumentOrderNo", sourceDocumentOrderNo);
                AddParameter(command, "@topCount", Math.Max(1, topCount));
            },
            reader => new ReturnReferenceCandidateRecord(
                ReadString(reader, "SourceDocumentSerie"),
                ReadInt32(reader, "SourceDocumentOrderNo"),
                ReadString(reader, "InvoiceNo"),
                ReadNullableDateTime(reader, "InvoiceDate"),
                ReadNullableDateTime(reader, "DocumentDate"),
                ReadDateTime(reader, "CreatedAt"),
                ReadString(reader, "CustomerCode"),
                ReadString(reader, "CustomerTitle"),
                RoundMoney(ReadDecimal(reader, "LineExtensionTotal")),
                RoundMoney(ReadDecimal(reader, "TaxTotal")),
                RoundMoney(ReadDecimal(reader, "PayableTotal")),
                ReadInt32(reader, "IsGeneratedInvoiceNo") != 0),
            cancellationToken);

        return rows;
    }

    private async Task SaveReturnReferenceAsync(
        Guid returnInvoiceGuid,
        ReturnReferenceCandidateRecord reference,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.EBELGE_EVRAK_HAREKETLERI
            SET
                ebh_iade_fat_no1 = @returnInvoiceNo,
                ebh_iade_fat_tarihi1 = @returnInvoiceDateText,
                ebh_degisti = 1,
                ebh_lastup_user = @mikroUserNo,
                ebh_lastup_date = GETDATE()
            WHERE
                ebh_related_uid = @returnInvoiceGuid
                AND ISNULL(ebh_iptal, 0) = 0;

            IF @@ROWCOUNT = 0
            BEGIN
                INSERT INTO dbo.EBELGE_EVRAK_HAREKETLERI
                (
                    ebh_Guid,
                    ebh_DBCno,
                    ebh_SpecRecNo,
                    ebh_iptal,
                    ebh_fileid,
                    ebh_hidden,
                    ebh_kilitli,
                    ebh_degisti,
                    ebh_CheckSum,
                    Ebh_create_user,
                    ebh_create_date,
                    ebh_lastup_user,
                    ebh_lastup_date,
                    ebh_special1,
                    ebh_special2,
                    ebh_special3,
                    ebh_hareket_tipi,
                    ebh_related_uid,
                    ebh_odeme_sekli,
                    ebh_odeme_aciklama,
                    ebh_odeme_aracisi,
                    ebh_satisin_webadresi,
                    ebh_gonderi_tarihi,
                    ebh_gonderi_tasiyan,
                    ebh_gonderi_no,
                    ebh_iade_fat_no1,
                    ebh_iade_fat_tarihi1,
                    ebh_ekli_dosya,
                    ebh_mukellefiyetdosyano,
                    ebh_mukellefiyetdonembasi,
                    ebh_mukellefiyetdonemsonu,
                    ebh_konaklamafaturasi_fl,
                    ebh_ImeI_no,
                    ebh_mac_no,
                    ebh_enerjifaturatipi,
                    ebh_arac_plakano,
                    ebh_arac_kimlikno,
                    ebh_sarjunite_serino,
                    ebh_sarj_baslama,
                    ebh_sarj_bitis,
                    ebh_esurapor_id,
                    ebh_esuRapor_tarihi,
                    ebh_Internet_satis_fl
                )
                VALUES
                (
                    NEWID(),
                    0,
                    0,
                    0,
                    597,
                    0,
                    0,
                    0,
                    0,
                    @mikroUserNo,
                    GETDATE(),
                    @mikroUserNo,
                    GETDATE(),
                    N'',
                    N'',
                    N'',
                    1,
                    @returnInvoiceGuid,
                    0,
                    N'',
                    N'',
                    N'',
                    CONVERT(nvarchar(8), GETDATE(), 112),
                    N'',
                    N'',
                    @returnInvoiceNo,
                    @returnInvoiceDateText,
                    N'',
                    N'',
                    N'18991230',
                    N'18991230',
                    0,
                    N'',
                    N'',
                    0,
                    N'',
                    N'',
                    N'',
                    N'18991230',
                    N'18991230',
                    N'',
                    N'18991230',
                    0
                );
            END;
            """;

        await ExecuteNonQueryAsync(
            mikroWriteDbContext,
            sql,
            command =>
            {
                AddParameter(command, "@returnInvoiceGuid", returnInvoiceGuid);
                AddParameter(command, "@returnInvoiceNo", reference.InvoiceNo.Trim());
                AddParameter(
                    command,
                    "@returnInvoiceDateText",
                    reference.InvoiceDate.HasValue
                        ? reference.InvoiceDate.Value.ToString("yyyyMMdd")
                        : string.Empty);
                AddParameter(command, "@mikroUserNo", MikroUserNo);
            },
            cancellationToken);
    }

    private async Task<IReadOnlyCollection<InvoiceLineSeed>> LoadInvoiceLinesAsync(
        PendingInvoiceRecord invoice,
        CancellationToken cancellationToken)
    {
        return invoice.CariMovementType is 8 or 14
            ? await LoadServiceLinesAsync(invoice, cancellationToken)
            : await LoadStockLinesAsync(invoice, cancellationToken);
    }

    private async Task<IReadOnlyCollection<InvoiceLineSeed>> LoadStockLinesAsync(
        PendingInvoiceRecord invoice,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                sh.sth_stok_kod AS StockCode,
                st.sto_isim AS StockName,
                SUM(ISNULL(sh.sth_miktar, 0)) AS Quantity,
                SUM(ISNULL(sh.sth_tutar, 0)) AS GrossAmount,
                SUM(ISNULL(sh.sth_iskonto1, 0)) AS Discount1,
                SUM(ISNULL(sh.sth_iskonto2, 0)) AS Discount2,
                SUM(ISNULL(sh.sth_iskonto3, 0)) AS Discount3,
                SUM(ISNULL(sh.sth_iskonto4, 0)) AS Discount4,
                SUM(ISNULL(sh.sth_iskonto5, 0)) AS Discount5,
                SUM(ISNULL(sh.sth_iskonto6, 0)) AS Discount6,
                SUM(ISNULL(sh.sth_vergi, 0)) AS TaxAmount,
                ISNULL(sh.sth_vergi_pntr, 0) AS TaxPointer,
                ISNULL(taxRate.Rate, -1) AS ConfiguredTaxRate,
                st.sto_birim1_ad AS UnitName
            FROM STOK_HAREKETLERI sh WITH (NOLOCK)
            INNER JOIN STOKLAR st WITH (NOLOCK) ON sh.sth_stok_kod = st.sto_kod
            INNER JOIN CARI_HESAP_HAREKETLERI ch WITH (NOLOCK) ON ch.cha_Guid = sh.sth_fat_uid
            OUTER APPLY (
                SELECT TOP (1) rateList.Rate
                FROM dbo.fn_hs_vergi_oran_listesi() rateList
                WHERE rateList.DepartmentNo = ISNULL(sh.sth_vergi_pntr, 0)
            ) taxRate
            WHERE
                ch.cha_evrakno_seri = @documentSerie
                AND ch.cha_evrakno_sira = @documentOrderNo
                AND ISNULL(ch.cha_iptal, 0) = 0
            GROUP BY
                sh.sth_stok_kod,
                st.sto_isim,
                sh.sth_vergi_pntr,
                taxRate.Rate,
                st.sto_birim1_ad
            ORDER BY
                sh.sth_stok_kod;
            """;

        var rows = await ExecuteReaderAsync(
            mikroDbContext,
            sql,
            command =>
            {
                AddParameter(command, "@documentSerie", invoice.DocumentSerie);
                AddParameter(command, "@documentOrderNo", invoice.DocumentOrderNo);
            },
            reader =>
            {
                var discounts = new[]
                {
                    ReadDecimal(reader, "Discount1"),
                    ReadDecimal(reader, "Discount2"),
                    ReadDecimal(reader, "Discount3"),
                    ReadDecimal(reader, "Discount4"),
                    ReadDecimal(reader, "Discount5"),
                    ReadDecimal(reader, "Discount6")
                };
                var grossAmount = ReadDecimal(reader, "GrossAmount");
                var discountTotal = discounts.Sum();
                var netAmount = Math.Max(0m, grossAmount - discountTotal);
                var taxAmount = ReadDecimal(reader, "TaxAmount");
                var taxRate = ResolveTaxRate(
                    netAmount,
                    taxAmount,
                    ReadDecimal(reader, "ConfiguredTaxRate"));

                return new InvoiceLineSeed(
                    ReadString(reader, "StockCode"),
                    ReadString(reader, "StockName"),
                    NormalizeQuantity(ReadDecimal(reader, "Quantity")),
                    grossAmount,
                    discounts,
                    netAmount,
                    taxAmount,
                    taxRate,
                    ResolveUnitCode(ReadString(reader, "UnitName")));
            },
            cancellationToken);

        return rows;
    }

    private async Task<IReadOnlyCollection<InvoiceLineSeed>> LoadServiceLinesAsync(
        PendingInvoiceRecord invoice,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                ISNULL(hiz.hiz_kod, ISNULL(dm.dem_kod, N'')) AS ItemCode,
                ISNULL(hiz.hiz_isim, ISNULL(dm.dem_isim, N'')) AS ItemName,
                SUM(ISNULL(ch.cha_miktari, 0)) AS Quantity,
                SUM(ISNULL(ch.cha_aratoplam, 0)) AS GrossAmount,
                SUM(CASE ISNULL(ch.cha_vergipntr, 0)
                    WHEN 1 THEN ISNULL(ch.cha_vergi1, 0)
                    WHEN 2 THEN ISNULL(ch.cha_vergi2, 0)
                    WHEN 3 THEN ISNULL(ch.cha_vergi3, 0)
                    WHEN 4 THEN ISNULL(ch.cha_vergi4, 0)
                    WHEN 5 THEN ISNULL(ch.cha_vergi5, 0)
                    WHEN 6 THEN ISNULL(ch.cha_vergi6, 0)
                    WHEN 7 THEN ISNULL(ch.cha_vergi7, 0)
                    WHEN 8 THEN ISNULL(ch.cha_vergi8, 0)
                    WHEN 9 THEN ISNULL(ch.cha_vergi9, 0)
                    WHEN 10 THEN ISNULL(ch.cha_vergi10, 0)
                    ELSE ISNULL(ch.cha_vergi1, 0)
                END) AS TaxAmount,
                ISNULL(ch.cha_vergipntr, 0) AS TaxPointer,
                ISNULL(taxRate.Rate, -1) AS ConfiguredTaxRate
            FROM CARI_HESAP_HAREKETLERI ch WITH (NOLOCK)
            INNER JOIN CARI_HESAPLAR c WITH (NOLOCK) ON ch.cha_ciro_cari_kodu = c.cari_kod
            INNER JOIN CARI_HESAP_ADRESLERI adr WITH (NOLOCK) ON c.cari_kod = adr.adr_cari_kod
            LEFT JOIN HIZMET_HESAPLARI hiz WITH (NOLOCK) ON ch.cha_kasa_hizkod = hiz.hiz_kod
            LEFT JOIN DEMIRBASLAR dm WITH (NOLOCK) ON ch.cha_kasa_hizkod = dm.dem_kod
            OUTER APPLY (
                SELECT TOP (1) rateList.Rate
                FROM dbo.fn_hs_vergi_oran_listesi() rateList
                WHERE rateList.DepartmentNo = ISNULL(ch.cha_vergipntr, 0)
            ) taxRate
            WHERE
                adr.adr_adres_no = 1
                AND ch.cha_tip = 0
                AND ch.cha_evrakno_seri = @documentSerie
                AND ch.cha_evrakno_sira = @documentOrderNo
                AND ISNULL(ch.cha_iptal, 0) = 0
            GROUP BY
                ISNULL(hiz.hiz_kod, ISNULL(dm.dem_kod, N'')),
                ISNULL(hiz.hiz_isim, ISNULL(dm.dem_isim, N'')),
                ISNULL(ch.cha_vergipntr, 0),
                taxRate.Rate
            ORDER BY
                ItemCode,
                ItemName;
            """;

        var rows = await ExecuteReaderAsync(
            mikroDbContext,
            sql,
            command =>
            {
                AddParameter(command, "@documentSerie", invoice.DocumentSerie);
                AddParameter(command, "@documentOrderNo", invoice.DocumentOrderNo);
            },
            reader =>
            {
                var grossAmount = ReadDecimal(reader, "GrossAmount");
                var taxAmount = ReadDecimal(reader, "TaxAmount");
                var taxRate = ResolveTaxRate(
                    grossAmount,
                    taxAmount,
                    ReadDecimal(reader, "ConfiguredTaxRate"));

                return new InvoiceLineSeed(
                    ReadString(reader, "ItemCode"),
                    ReadString(reader, "ItemName"),
                    NormalizeQuantity(ReadDecimal(reader, "Quantity")),
                    grossAmount,
                    [],
                    grossAmount,
                    taxAmount,
                    taxRate,
                    "NIU");
            },
            cancellationToken);

        return rows;
    }

    private async Task<BuiltInvoiceDocument> BuildInvoiceDocumentAsync(
        PendingInvoiceRecord invoice,
        CancellationToken cancellationToken)
    {
        var invoiceLines = await LoadInvoiceLinesAsync(invoice, cancellationToken);

        if (invoiceLines.Count == 0)
        {
            throw new InvalidOperationException(
                $"Fatura satirlari bulunamadi: {invoice.DocumentSerie}/{invoice.DocumentOrderNo}.");
        }

        var supplier = await LoadSupplierAsync(cancellationToken);
        var invoiceDate = invoice.DocumentDate;
        var createdAt = DateTime.Now;
        var invoiceId = invoice.InvoiceId;
        var invoiceUuid = invoice.InvoiceGuid.ToString();
        var profileId = ResolveProfileId(invoice);
        var invoiceTypeCode = ResolveInvoiceTypeCode(invoice);
        var additionalDocumentReference = await BuildXsltDocumentReferenceAsync(
            invoice.Scenario,
            invoiceDate,
            cancellationToken);
        var totalDiscount = RoundMoney(invoiceLines.Sum(line => line.Discounts.Sum()));
        var lineExtensionTotal = RoundMoney(invoiceLines.Sum(line => line.NetAmount));
        var taxTotal = RoundMoney(invoiceLines.Sum(line => line.TaxAmount));
        var chargeTotal = RoundMoney(invoice.Rusum);
        var payableTotal = RoundMoney(lineExtensionTotal + taxTotal + chargeTotal);
        var taxSubtotals = BuildTaxSubtotals(invoice, invoiceLines);
        var lineElements = invoiceLines
            .Select((line, index) => BuildInvoiceLineElement(index + 1, line, invoice))
            .ToArray();

        var document = BuildInvoiceElement(
            invoice,
            supplier,
            invoiceDate,
            createdAt,
            invoiceId,
            invoiceUuid,
            profileId,
            invoiceTypeCode,
            lineExtensionTotal,
            totalDiscount,
            chargeTotal,
            taxTotal,
            payableTotal,
            taxSubtotals,
            lineElements,
            additionalDocumentReference);

        var xmlContent = new XDocument(document).ToString(SaveOptions.DisableFormatting);

        return new BuiltInvoiceDocument(
            invoice.InvoiceId,
            invoice.CustomerTaxNumber,
            invoice.TargetAlias,
            invoice.CustomerTitle,
            xmlContent,
            document,
            profileId,
            invoiceTypeCode);
    }

    private XElement BuildInvoiceElement(
        PendingInvoiceRecord invoice,
        PartyInfo supplier,
        DateTime invoiceDate,
        DateTime createdAt,
        string invoiceId,
        string invoiceUuid,
        string profileId,
        string invoiceTypeCode,
        decimal lineExtensionTotal,
        decimal allowanceTotal,
        decimal chargeTotal,
        decimal taxTotal,
        decimal payableTotal,
        IReadOnlyCollection<XElement> taxSubtotals,
        IReadOnlyCollection<XElement> lineElements,
        XElement? additionalDocumentReference)
    {
        var invoiceNamespace = XNamespace.Get(InvoiceNamespace);
        var extension = XNamespace.Get(ExtensionNamespace);
        var aggregate = XNamespace.Get(AggregateNamespace);
        var basic = XNamespace.Get(BasicNamespace);
        var placeholderExtension = XNamespace.Get(PlaceholderExtensionNamespace);
        var customer = BuildCustomerPartyInfo(invoice);
        var notes = BuildInvoiceNotes(invoice, basic);
        var elements = new List<object>
        {
            new XAttribute(XNamespace.Xmlns + "ext", extension.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "cac", aggregate.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "cbc", basic.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "furpa", placeholderExtension.NamespaceName),
            BuildUblExtensionsElement(extension, placeholderExtension),
            new XElement(basic + "UBLVersionID", "2.1"),
            new XElement(basic + "CustomizationID", "TR1.2"),
            new XElement(basic + "ProfileID", profileId),
            new XElement(basic + "ID", invoiceId),
            new XElement(basic + "CopyIndicator", "false"),
            new XElement(basic + "UUID", invoiceUuid),
            new XElement(basic + "IssueDate", invoiceDate.ToString("yyyy-MM-dd")),
            new XElement(basic + "IssueTime", createdAt.ToString("HH:mm:ss")),
            new XElement(basic + "InvoiceTypeCode", invoiceTypeCode)
        };

        elements.AddRange(notes);
        elements.Add(new XElement(basic + "DocumentCurrencyCode", CurrencyCode));
        elements.Add(new XElement(basic + "LineCountNumeric", lineElements.Count));

        var billingReference = BuildBillingReferenceElement(invoice);
        if (billingReference is not null)
        {
            elements.Add(billingReference);
        }

        if (!string.IsNullOrWhiteSpace(invoice.ShipmentDocumentNo))
        {
            var despatchDocumentReference = new XElement(
                aggregate + "DespatchDocumentReference",
                new XElement(basic + "ID", invoice.ShipmentDocumentNo));

            if (invoice.ShipmentDocumentDate.HasValue)
            {
                despatchDocumentReference.Add(
                    new XElement(
                        basic + "IssueDate",
                        invoice.ShipmentDocumentDate.Value.ToString("yyyy-MM-dd")));
            }

            elements.Add(despatchDocumentReference);
        }

        if (additionalDocumentReference is not null)
        {
            elements.Add(additionalDocumentReference);
        }

        elements.Add(BuildSignatureElement(supplier));
        elements.Add(BuildAccountingPartyElement("AccountingSupplierParty", supplier));
        elements.Add(BuildAccountingPartyElement("AccountingCustomerParty", customer));
        elements.Add(
            new XElement(
                aggregate + "TaxTotal",
                new XElement(
                    basic + "TaxAmount",
                    new XAttribute("currencyID", CurrencyCode),
                    FormatAmount(taxTotal)),
                taxSubtotals));
        elements.Add(
            new XElement(
                aggregate + "LegalMonetaryTotal",
                new XElement(
                    basic + "LineExtensionAmount",
                    new XAttribute("currencyID", CurrencyCode),
                    FormatAmount(lineExtensionTotal)),
                new XElement(
                    basic + "TaxExclusiveAmount",
                    new XAttribute("currencyID", CurrencyCode),
                    FormatAmount(lineExtensionTotal)),
                new XElement(
                    basic + "TaxInclusiveAmount",
                    new XAttribute("currencyID", CurrencyCode),
                    FormatAmount(payableTotal)),
                new XElement(
                    basic + "AllowanceTotalAmount",
                    new XAttribute("currencyID", CurrencyCode),
                    FormatAmount(allowanceTotal)),
                new XElement(
                    basic + "ChargeTotalAmount",
                    new XAttribute("currencyID", CurrencyCode),
                    FormatAmount(chargeTotal)),
                new XElement(
                    basic + "PayableAmount",
                    new XAttribute("currencyID", CurrencyCode),
                    FormatAmount(payableTotal))));
        elements.AddRange(lineElements);

        return new XElement(invoiceNamespace + "Invoice", elements);
    }

    private static XElement BuildUblExtensionsElement(
        XNamespace extension,
        XNamespace placeholderExtension) =>
        new(
            extension + "UBLExtensions",
            new XElement(
                extension + "UBLExtension",
                new XElement(
                    extension + "ExtensionContent",
                    new XElement(placeholderExtension + "ExtensionPlaceholder"))));

    private static XElement BuildSignatureElement(PartyInfo supplier)
    {
        var aggregate = XNamespace.Get(AggregateNamespace);
        var basic = XNamespace.Get(BasicNamespace);
        var taxSchemeId = ResolveTaxSchemeId(supplier.TaxNumber);

        return new XElement(
            aggregate + "Signature",
            new XElement(
                basic + "ID",
                new XAttribute("schemeID", "VKN_TCKN"),
                supplier.TaxNumber),
            new XElement(
                aggregate + "SignatoryParty",
                new XElement(
                    aggregate + "PartyIdentification",
                    new XElement(
                        basic + "ID",
                        new XAttribute("schemeID", taxSchemeId),
                        supplier.TaxNumber)),
                BuildAddressElement("PostalAddress", supplier)),
            new XElement(
                aggregate + "DigitalSignatureAttachment",
                new XElement(
                    aggregate + "ExternalReference",
                    new XElement(basic + "URI", "#Signature"))));
    }

    private static XElement? BuildBillingReferenceElement(PendingInvoiceRecord invoice)
    {
        if (!invoice.IsReturn || string.IsNullOrWhiteSpace(invoice.ReturnInvoiceNo))
        {
            return null;
        }

        var aggregate = XNamespace.Get(AggregateNamespace);
        var basic = XNamespace.Get(BasicNamespace);
        var documentReference = new XElement(
            aggregate + "InvoiceDocumentReference",
            new XElement(basic + "ID", invoice.ReturnInvoiceNo.Trim()));

        if (invoice.ReturnInvoiceDate.HasValue)
        {
            documentReference.Add(
                new XElement(
                    basic + "IssueDate",
                    invoice.ReturnInvoiceDate.Value.ToString("yyyy-MM-dd")));
        }

        documentReference.Add(new XElement(basic + "DocumentType", "FATURA"));

        return new XElement(
            aggregate + "BillingReference",
            documentReference);
    }

    private static IReadOnlyCollection<object> BuildInvoiceNotes(
        PendingInvoiceRecord invoice,
        XNamespace basic)
    {
        var notes = new List<object>();

        notes.Add(new XElement(basic + "Note", $"Yalnız: #{FormatTurkishAmountInWords(invoice.PayableTotal)}#"));

        if (!string.IsNullOrWhiteSpace(invoice.WarehouseName))
        {
            notes.Add(new XElement(basic + "Note", $"Şube: {invoice.WarehouseName.Trim()}"));
        }

        var shippingAddress = BuildShippingAddressNote(invoice);
        if (!string.IsNullOrWhiteSpace(shippingAddress))
        {
            notes.Add(new XElement(basic + "Note", $"Sevkiyat Adresi: {shippingAddress}"));
        }

        if (!string.IsNullOrWhiteSpace(invoice.Description))
        {
            notes.Add(new XElement(basic + "Note", invoice.Description.Trim()));
        }

        return notes;
    }

    private static string BuildShippingAddressNote(PendingInvoiceRecord invoice)
    {
        var parts = new[]
        {
            invoice.AddressCity,
            invoice.AddressDistrict,
            invoice.AddressStreet,
            invoice.AddressStreet2
        };

        return string.Join(
            " ",
            parts
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => part.Trim()))
            .ToUpper(TurkishCulture);
    }

    private async Task<PartyInfo> LoadSupplierAsync(CancellationToken cancellationToken)
    {
        var supplierCustomerCode = eDesPatchOptionsValue.SupplierCustomerCode;
        var supplier = await (
            from customer in mikroDbContext.CARI_HESAPLARs
            join address in mikroDbContext.CARI_HESAP_ADRESLERIs on customer.cari_kod equals address.adr_cari_kod
            where customer.cari_kod == supplierCustomerCode && address.adr_adres_no == 1
            select new PartyInfo(
                customer.cari_kod ?? string.Empty,
                BuildCustomerTitle(customer.cari_unvan1, customer.cari_unvan2),
                ResolveTaxNumber(customer.cari_vdaire_no, customer.cari_VergiKimlikNo),
                customer.cari_vdaire_adi ?? string.Empty,
                address.adr_cadde ?? string.Empty,
                address.adr_sokak ?? string.Empty,
                address.adr_ilce ?? string.Empty,
                address.adr_il ?? string.Empty,
                address.adr_posta_kodu ?? string.Empty,
                NormalizePhone(address.adr_tel_no1, address.adr_tel_no2),
                customer.cari_EMail ?? string.Empty,
                eDesPatchOptionsValue.CountryCode,
                eDesPatchOptionsValue.CountryName))
            .FirstOrDefaultAsync(cancellationToken);

        return supplier ?? throw new InvalidOperationException(
            $"Supplier customer was not found for code {supplierCustomerCode}.");
    }

    private EDespatchOptions eDesPatchOptionsValue => eDespatchOptions.Value;

    private static PartyInfo BuildCustomerPartyInfo(PendingInvoiceRecord invoice) =>
        new(
            invoice.CustomerCode,
            invoice.CustomerTitle,
            invoice.CustomerTaxNumber,
            invoice.TaxOffice,
            invoice.AddressStreet,
            invoice.AddressStreet2,
            invoice.AddressDistrict,
            invoice.AddressCity,
            invoice.PostalCode,
            invoice.Phone,
            invoice.Email,
            "TR",
            "TURKIYE");

    private static XElement BuildAccountingPartyElement(string elementName, PartyInfo partyInfo)
    {
        var aggregate = XNamespace.Get(AggregateNamespace);

        return new XElement(
            aggregate + elementName,
            new XElement(aggregate + "Party", BuildPartyChildren(partyInfo)));
    }

    private static IReadOnlyCollection<object> BuildPartyChildren(PartyInfo partyInfo)
    {
        var aggregate = XNamespace.Get(AggregateNamespace);
        var basic = XNamespace.Get(BasicNamespace);
        var children = new List<object>
        {
            new XElement(
                aggregate + "PartyIdentification",
                new XElement(
                    basic + "ID",
                    new XAttribute("schemeID", ResolveTaxSchemeId(partyInfo.TaxNumber)),
                    partyInfo.TaxNumber)),
            new XElement(
                aggregate + "PartyName",
                new XElement(basic + "Name", partyInfo.DisplayName)),
            BuildAddressElement("PostalAddress", partyInfo),
            new XElement(
                aggregate + "PartyTaxScheme",
                new XElement(
                    aggregate + "TaxScheme",
                    new XElement(basic + "Name", string.IsNullOrWhiteSpace(partyInfo.TaxOffice) ? "YOK" : partyInfo.TaxOffice)))
        };

        var contact = BuildContactElement(partyInfo.Phone, partyInfo.Email);
        if (contact is not null)
        {
            children.Add(contact);
        }

        return children;
    }

    private static XElement BuildAddressElement(string elementName, PartyInfo partyInfo)
    {
        var aggregate = XNamespace.Get(AggregateNamespace);
        var basic = XNamespace.Get(BasicNamespace);

        return new XElement(
            aggregate + elementName,
            new XElement(basic + "StreetName", BuildStreetName(partyInfo)),
            new XElement(basic + "CitySubdivisionName", string.IsNullOrWhiteSpace(partyInfo.District) ? "-" : partyInfo.District),
            new XElement(basic + "CityName", string.IsNullOrWhiteSpace(partyInfo.City) ? "-" : partyInfo.City),
            string.IsNullOrWhiteSpace(partyInfo.PostalCode)
                ? null
                : new XElement(basic + "PostalZone", partyInfo.PostalCode),
            new XElement(
                aggregate + "Country",
                new XElement(basic + "IdentificationCode", partyInfo.CountryCode),
                new XElement(basic + "Name", partyInfo.CountryName)));
    }

    private static string BuildStreetName(PartyInfo partyInfo)
    {
        var streetParts = new[] { partyInfo.Street, partyInfo.Street2 }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part.Trim())
            .ToArray();

        return streetParts.Length == 0
            ? "-"
            : string.Join(" ", streetParts);
    }

    private static XElement? BuildContactElement(string phone, string email)
    {
        var aggregate = XNamespace.Get(AggregateNamespace);
        var basic = XNamespace.Get(BasicNamespace);
        var children = new List<object>();

        if (!string.IsNullOrWhiteSpace(phone))
        {
            children.Add(new XElement(basic + "Telephone", phone));
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            children.Add(new XElement(basic + "ElectronicMail", email));
        }

        return children.Count == 0
            ? null
            : new XElement(aggregate + "Contact", children);
    }

    private IReadOnlyCollection<XElement> BuildTaxSubtotals(
        PendingInvoiceRecord invoice,
        IReadOnlyCollection<InvoiceLineSeed> lines)
    {
        var aggregate = XNamespace.Get(AggregateNamespace);
        var basic = XNamespace.Get(BasicNamespace);

        return lines
            .GroupBy(
                line => new
                {
                    line.TaxRate,
                    invoice.IstisnaKodu,
                    invoice.OzelMatrahKodu
                })
            .Select(group =>
            {
                var exemptionCode = group.Key.TaxRate == 0m
                    ? ResolveExemptionCode(invoice)
                    : string.Empty;
                var exemptionReason = group.Key.TaxRate == 0m
                    ? ResolveExemptionReason(invoice)
                    : string.Empty;

                return new XElement(
                    aggregate + "TaxSubtotal",
                    new XElement(
                        basic + "TaxableAmount",
                        new XAttribute("currencyID", CurrencyCode),
                        FormatAmount(RoundMoney(group.Sum(line => line.NetAmount)))),
                    new XElement(
                        basic + "TaxAmount",
                        new XAttribute("currencyID", CurrencyCode),
                        FormatAmount(RoundMoney(group.Sum(line => line.TaxAmount)))),
                    new XElement(basic + "Percent", FormatRate(group.Key.TaxRate)),
                    new XElement(
                        aggregate + "TaxCategory",
                        string.IsNullOrWhiteSpace(exemptionCode)
                            ? null
                            : new XElement(basic + "TaxExemptionReasonCode", exemptionCode),
                        string.IsNullOrWhiteSpace(exemptionReason)
                            ? null
                            : new XElement(basic + "TaxExemptionReason", exemptionReason),
                        new XElement(
                            aggregate + "TaxScheme",
                            new XElement(basic + "Name", "KDV"),
                            new XElement(basic + "TaxTypeCode", "0015"))));
            })
            .ToArray();
    }

    private XElement BuildInvoiceLineElement(
        int lineNo,
        InvoiceLineSeed line,
        PendingInvoiceRecord invoice)
    {
        var aggregate = XNamespace.Get(AggregateNamespace);
        var basic = XNamespace.Get(BasicNamespace);
        var unitPrice = line.Quantity <= 0m
            ? line.GrossAmount
            : RoundMoney(line.GrossAmount / line.Quantity);
        var allowanceElements = BuildAllowanceChargeElements(line);
        var exemptionCode = line.TaxRate == 0m
            ? ResolveExemptionCode(invoice)
            : string.Empty;
        var exemptionReason = line.TaxRate == 0m
            ? ResolveExemptionReason(invoice)
            : string.Empty;

        return new XElement(
            aggregate + "InvoiceLine",
            new XElement(basic + "ID", lineNo),
            new XElement(
                basic + "InvoicedQuantity",
                new XAttribute("unitCode", line.UnitCode),
                FormatQuantity(line.Quantity)),
            new XElement(
                basic + "LineExtensionAmount",
                new XAttribute("currencyID", CurrencyCode),
                FormatAmount(line.NetAmount)),
            allowanceElements,
            new XElement(
                aggregate + "TaxTotal",
                new XElement(
                    basic + "TaxAmount",
                    new XAttribute("currencyID", CurrencyCode),
                    FormatAmount(line.TaxAmount)),
                new XElement(
                    aggregate + "TaxSubtotal",
                    new XElement(
                        basic + "TaxableAmount",
                        new XAttribute("currencyID", CurrencyCode),
                        FormatAmount(line.NetAmount)),
                    new XElement(
                        basic + "TaxAmount",
                        new XAttribute("currencyID", CurrencyCode),
                        FormatAmount(line.TaxAmount)),
                    new XElement(basic + "Percent", FormatRate(line.TaxRate)),
                    new XElement(
                        aggregate + "TaxCategory",
                        string.IsNullOrWhiteSpace(exemptionCode)
                            ? null
                            : new XElement(basic + "TaxExemptionReasonCode", exemptionCode),
                        string.IsNullOrWhiteSpace(exemptionReason)
                            ? null
                            : new XElement(basic + "TaxExemptionReason", exemptionReason),
                        new XElement(
                            aggregate + "TaxScheme",
                            new XElement(basic + "Name", "KDV"),
                            new XElement(basic + "TaxTypeCode", "0015"))))),
            new XElement(
                aggregate + "Item",
                new XElement(basic + "Name", string.IsNullOrWhiteSpace(line.Name) ? "-" : line.Name),
                new XElement(
                    aggregate + "SellersItemIdentification",
                    new XElement(
                        basic + "ID",
                        string.IsNullOrWhiteSpace(line.Code) ? $"SATIR-{lineNo}" : line.Code))),
            new XElement(
                aggregate + "Price",
                new XElement(
                    basic + "PriceAmount",
                    new XAttribute("currencyID", CurrencyCode),
                    FormatAmount(unitPrice))));
    }

    private static IReadOnlyCollection<XElement> BuildAllowanceChargeElements(InvoiceLineSeed line)
    {
        var aggregate = XNamespace.Get(AggregateNamespace);
        var basic = XNamespace.Get(BasicNamespace);
        var result = new List<XElement>();
        var remainingBase = line.GrossAmount;

        foreach (var discount in line.Discounts.Where(discount => discount > 0m))
        {
            if (remainingBase <= 0m)
            {
                break;
            }

            var ratio = discount / remainingBase;
            result.Add(
                new XElement(
                    aggregate + "AllowanceCharge",
                    new XElement(basic + "ChargeIndicator", "false"),
                    new XElement(basic + "MultiplierFactorNumeric", FormatRate(Math.Round(ratio, 4, MidpointRounding.AwayFromZero))),
                    new XElement(
                        basic + "Amount",
                        new XAttribute("currencyID", CurrencyCode),
                        FormatAmount(discount)),
                    new XElement(
                        basic + "BaseAmount",
                        new XAttribute("currencyID", CurrencyCode),
                        FormatAmount(remainingBase))));
            remainingBase = Math.Max(0m, remainingBase - discount);
        }

        return result;
    }

    private async Task<XElement?> BuildXsltDocumentReferenceAsync(
        InvoiceSendingScenario scenario,
        DateTime issueDate,
        CancellationToken cancellationToken)
    {
        var fileName = scenario == InvoiceSendingScenario.EArsiv
            ? "earsiv.xslt"
            : "efatura.xslt";
        var path = Path.Combine(hostEnvironment.ContentRootPath, "Assets", "Xslt", fileName);

        if (!File.Exists(path))
        {
            logger.LogWarning("XSLT asset was not found for invoice sending preview: {Path}", path);
            return null;
        }

        var content = await File.ReadAllTextAsync(path, cancellationToken);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(content));
        var aggregate = XNamespace.Get(AggregateNamespace);
        var basic = XNamespace.Get(BasicNamespace);

        return new XElement(
            aggregate + "AdditionalDocumentReference",
            new XElement(basic + "ID", "XSLT"),
            new XElement(basic + "IssueDate", issueDate.ToString("yyyy-MM-dd")),
            new XElement(basic + "DocumentType", "XSLT"),
            new XElement(basic + "DocumentDescription", fileName),
            new XElement(
                aggregate + "Attachment",
                new XElement(
                    basic + "EmbeddedDocumentBinaryObject",
                    new XAttribute("characterSetCode", "UTF-8"),
                    new XAttribute("encodingCode", "Base64"),
                    new XAttribute("filename", fileName),
                    new XAttribute("mimeCode", "application/xml"),
                    encoded)));
    }

    private async Task<ServiceSendResponse> SendToUyumsoftAsync(
        BuiltInvoiceDocument invoice,
        InvoiceSendingScenario scenario,
        CancellationToken cancellationToken)
    {
        var config = uyumsoftOptions.Value.EInvoice;
        var client = UyumsoftWcfClientHelper.CreateInvoiceClient(config.EndpointUrl);

        try
        {
            var response = await client.SendInvoiceAsync(
                UyumsoftWcfClientHelper.CreateInvoiceUserInfo(config),
                [BuildInvoiceInfo(invoice, scenario)]);

            if (!response.IsSucceded)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(response.Message)
                        ? "Uyumsoft faturayi kabul etmedi."
                        : response.Message);
            }

            var identity = response.Value?.FirstOrDefault()
                           ?? throw new InvalidOperationException(
                               "Uyumsoft SendInvoice response does not contain a sent invoice identity.");

            if (string.IsNullOrWhiteSpace(identity.Number))
            {
                throw new InvalidOperationException(
                    "Uyumsoft SendInvoice response does not contain a document number.");
            }

            return new ServiceSendResponse(
                identity.Id?.Trim() ?? string.Empty,
                identity.Number.Trim());
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

    private static UyumsoftInvoice.InvoiceInfo BuildInvoiceInfo(
        BuiltInvoiceDocument invoice,
        InvoiceSendingScenario scenario) =>
        new()
        {
            Invoice = UyumsoftWcfClientHelper.DeserializeUbl<UyumsoftInvoice.InvoiceType>(
                invoice.XmlContent,
                "Invoice",
                InvoiceNamespace),
            LocalDocumentId = BuildLocalDocumentId(invoice.InvoiceId, scenario),
            ExtraInformation = string.Empty,
            TargetCustomer = new UyumsoftInvoice.CustomerInfo
            {
                VknTckn = invoice.CustomerTaxNumber,
                Alias = invoice.TargetAlias,
                Title = invoice.CustomerTitle
            },
            Scenario = scenario == InvoiceSendingScenario.EArsiv
                ? UyumsoftInvoice.InvoiceScenarioChoosen.eArchive
                : UyumsoftInvoice.InvoiceScenarioChoosen.eInvoice,
            EArchiveInvoiceInfo = scenario == InvoiceSendingScenario.EArsiv
                ? new UyumsoftInvoice.EArchiveInvoiceInformation
                {
                    DeliveryType = UyumsoftInvoice.InvoiceDeliveryType.Electronic
                }
                : null,
            CreateDateUtc = DateTime.UtcNow
        };

    private async Task MarkAsSentAsync(
        string documentSerie,
        int documentOrderNo,
        string serviceDocumentNumber,
        CancellationToken cancellationToken)
    {
        var trackedRows = await mikroWriteDbContext.CARI_HESAP_HAREKETLERIs
            .Where(row =>
                row.cha_evrakno_seri == documentSerie &&
                row.cha_evrakno_sira == documentOrderNo &&
                row.cha_tip == 0 &&
                row.cha_iptal != true)
            .ToListAsync(cancellationToken);

        if (trackedRows.Count == 0)
        {
            throw new KeyNotFoundException(
                $"Mikro hareketleri bulunamadi: {documentSerie}/{documentOrderNo}.");
        }

        var now = DateTime.Now;

        foreach (var row in trackedRows)
        {
            row.cha_belge_no = serviceDocumentNumber;
            row.cha_kilitli = true;
            row.cha_degisti = true;
            row.cha_lastup_user = MikroUserNo;
            row.cha_lastup_date = now;
        }

        await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
    }

    private static PendingInvoiceRecord MapPendingInvoice(
        DbDataReader reader,
        InvoiceSendingScenario scenario)
    {
        var documentSerie = ReadString(reader, "DocumentSerie");
        var documentOrderNo = ReadInt32(reader, "DocumentOrderNo");
        var documentDate = ReadDateTime(reader, "BelgeTarihi");
        var lineExtensionTotal = RoundMoney(ReadDecimal(reader, "AraToplam"));
        var taxTotal = RoundMoney(ReadDecimal(reader, "TaxTotal"));
        var chargeTotal = RoundMoney(ReadDecimal(reader, "Rusum"));
        var payableTotal = RoundMoney(lineExtensionTotal + taxTotal + chargeTotal);
        var customerTitle = ReadString(reader, "MusteriAdi");
        var targetAlias = ResolveTargetAlias(
            ReadString(reader, "FaturaMail"),
            ReadString(reader, "Mail"));

        return new PendingInvoiceRecord(
            ReadGuid(reader, "FatGuid"),
            documentSerie,
            documentOrderNo,
            BuildInvoiceId(documentSerie, documentOrderNo, documentDate.Year),
            documentDate,
            ReadString(reader, "BelgeNo"),
            ReadString(reader, "MusteriKodu"),
            customerTitle,
            ReadString(reader, "VDNo"),
            targetAlias,
            ReadString(reader, "VergiDairesi"),
            ReadString(reader, "Cadde"),
            ReadString(reader, "Sokak"),
            ReadString(reader, "Ilce"),
            ReadString(reader, "Il"),
            ReadString(reader, "PostaKodu"),
            ReadString(reader, "CariTel"),
            ReadString(reader, "Mail"),
            ReadInt32(reader, "CariHareketCins"),
            ReadInt32(reader, "EvrakTip"),
            ReadInt32(reader, "Iade"),
            ReadInt32(reader, "EBelgeTuru"),
            ReadString(reader, "IstisnaKodu"),
            ReadString(reader, "OzelMatrahKodu"),
            lineExtensionTotal,
            taxTotal,
            chargeTotal,
            payableTotal,
            ReadString(reader, "IrsaliyeNo"),
            ReadNullableDateTime(reader, "IrsaliyeTarihi"),
            ReadString(reader, "IadeFaturaNo"),
            ReadNullableDateTime(reader, "IadeFaturaTarihi"),
            ReadString(reader, "Depo"),
            ReadString(reader, "Aciklama"),
            scenario);
    }

    private static InvoiceSendingListItemDto MapListItem(PendingInvoiceRecord invoice)
    {
        return new InvoiceSendingListItemDto(
            invoice.DocumentSerie,
            invoice.DocumentOrderNo,
            invoice.InvoiceId,
            invoice.DocumentDate,
            invoice.SentDocumentNo,
            invoice.IsSent,
            invoice.CustomerCode,
            invoice.CustomerTitle,
            invoice.CustomerTaxNumber,
            invoice.TargetAlias,
            ResolveProfileId(invoice),
            ResolveInvoiceTypeCode(invoice),
            invoice.Scenario,
            invoice.LineExtensionTotal,
            invoice.TaxTotal,
            invoice.Rusum,
            invoice.PayableTotal,
            invoice.ShipmentDocumentNo,
            invoice.ShipmentDocumentDate,
            invoice.ReturnInvoiceNo,
            invoice.ReturnInvoiceDate,
            invoice.WarehouseName,
            invoice.Description);
    }

    private static void EnsureReturnInvoice(PendingInvoiceRecord invoice)
    {
        if (!invoice.IsReturn)
        {
            throw new InvalidOperationException("Bu belge iade faturasi degil.");
        }
    }

    private static InvoiceReturnReferenceDto? CreateCurrentReturnReference(PendingInvoiceRecord invoice) =>
        string.IsNullOrWhiteSpace(invoice.ReturnInvoiceNo)
            ? null
            : new InvoiceReturnReferenceDto(
                invoice.ReturnInvoiceNo,
                invoice.ReturnInvoiceDate,
                "saved");

    private static InvoiceReturnReferenceDto CreateReturnReference(
        ReturnReferenceCandidateRecord reference,
        string source) =>
        new(reference.InvoiceNo, reference.InvoiceDate, source);

    private static InvoiceReturnReferenceCandidateDto MapReturnReferenceCandidate(
        ReturnReferenceCandidateRecord candidate,
        PendingInvoiceRecord invoice,
        bool isFallbackCandidate) =>
        new(
            candidate.SourceDocumentSerie,
            candidate.SourceDocumentOrderNo,
            candidate.InvoiceNo,
            candidate.InvoiceDate,
            candidate.DocumentDate,
            candidate.CreatedAt,
            candidate.CustomerCode,
            candidate.CustomerTitle,
            candidate.LineExtensionTotal,
            candidate.TaxTotal,
            candidate.PayableTotal,
            isFallbackCandidate,
            IsCurrentReturnReference(invoice, candidate),
            candidate.IsGeneratedInvoiceNo);

    private static bool IsCurrentReturnReference(
        PendingInvoiceRecord invoice,
        ReturnReferenceCandidateRecord candidate)
    {
        if (string.IsNullOrWhiteSpace(invoice.ReturnInvoiceNo))
        {
            return false;
        }

        if (!string.Equals(
                invoice.ReturnInvoiceNo.Trim(),
                candidate.InvoiceNo.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !invoice.ReturnInvoiceDate.HasValue ||
               !candidate.InvoiceDate.HasValue ||
               invoice.ReturnInvoiceDate.Value.Date == candidate.InvoiceDate.Value.Date;
    }

    private static string ResolveReferenceSource(
        ReturnReferenceCandidateRecord reference,
        string source) =>
        reference.IsGeneratedInvoiceNo ? $"{source}-generated" : source;

    private static void ValidateListRequest(InvoiceSendingListRequest request)
    {
        if (request.EndDate.Date < request.StartDate.Date)
        {
            throw new ArgumentException("End date can not be earlier than start date.", nameof(request.EndDate));
        }

        if (request.SentState is < -1 or > 1)
        {
            throw new ArgumentException("SentState must be one of -1, 0 or 1.", nameof(request.SentState));
        }
    }

    private static void ValidateSendRequest(SendInvoiceDocumentsRequest request)
    {
        if (request.Documents.Count == 0)
        {
            throw new ArgumentException("En az bir fatura secilmelidir.", nameof(request.Documents));
        }

        if (request.Documents.Any(document =>
                string.IsNullOrWhiteSpace(document.DocumentSerie) ||
                document.DocumentOrderNo <= 0))
        {
            throw new ArgumentException(
                "Her secim icin gecerli documentSerie ve documentOrderNo verilmelidir.",
                nameof(request.Documents));
        }
    }

    private void ValidateConfiguration()
    {
        var config = uyumsoftOptions.Value.EInvoice;

        if (string.IsNullOrWhiteSpace(config.EndpointUrl))
        {
            throw new InvalidOperationException("EInvoice endpoint configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(config.Username))
        {
            throw new InvalidOperationException("EInvoice username configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(config.Password))
        {
            throw new InvalidOperationException("EInvoice password configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(eDesPatchOptionsValue.SupplierCustomerCode))
        {
            throw new InvalidOperationException("Supplier customer code configuration is required.");
        }
    }

    private void ValidatePreflightConfiguration()
    {
        if (string.IsNullOrWhiteSpace(eDesPatchOptionsValue.SupplierCustomerCode))
        {
            throw new InvalidOperationException("Supplier customer code configuration is required.");
        }
    }

    private static InvoiceDocumentProfile MapProfile(InvoiceSendingScenario scenario) =>
        scenario == InvoiceSendingScenario.EArsiv
            ? InvoiceDocumentProfile.EArsiv
            : InvoiceDocumentProfile.EFatura;

    private static string ResolveProfileId(PendingInvoiceRecord invoice) =>
        invoice.Scenario == InvoiceSendingScenario.EArsiv
            ? "EARSIVFATURA"
            : invoice.EBelgeTuru == 0
                ? "TICARIFATURA"
                : "TEMELFATURA";

    private static string ResolveInvoiceTypeCode(PendingInvoiceRecord invoice)
    {
        if (invoice.IsReturn)
        {
            return "IADE";
        }

        if (!string.IsNullOrWhiteSpace(invoice.IstisnaKodu))
        {
            return "ISTISNA";
        }

        if (!string.IsNullOrWhiteSpace(invoice.OzelMatrahKodu))
        {
            return "OZELMATRAH";
        }

        return "SATIS";
    }

    private static string ResolveExemptionCode(PendingInvoiceRecord invoice)
    {
        if (!string.IsNullOrWhiteSpace(invoice.IstisnaKodu))
        {
            return invoice.IstisnaKodu;
        }

        if (!string.IsNullOrWhiteSpace(invoice.OzelMatrahKodu))
        {
            return invoice.OzelMatrahKodu;
        }

        return string.Empty;
    }

    private static string ResolveExemptionReason(PendingInvoiceRecord invoice)
    {
        if (!string.IsNullOrWhiteSpace(invoice.IstisnaKodu))
        {
            return "Istisna";
        }

        if (!string.IsNullOrWhiteSpace(invoice.OzelMatrahKodu))
        {
            return "Ozel Matrah";
        }

        return string.Empty;
    }

    private static decimal ResolveTaxRate(
        decimal taxableAmount,
        decimal taxAmount,
        decimal configuredTaxRate)
    {
        if (configuredTaxRate >= 0m)
        {
            return configuredTaxRate;
        }

        if (taxableAmount > 0m && taxAmount > 0m)
        {
            return Math.Round(taxAmount * 100m / taxableAmount, 2, MidpointRounding.AwayFromZero);
        }

        return 0m;
    }

    private static string ResolveTargetAlias(string alias, string email)
    {
        if (!string.IsNullOrWhiteSpace(alias))
        {
            return alias.Trim();
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            return email.Trim();
        }

        return string.Empty;
    }

    private static string ResolveTaxNumber(string? taxNumber, string? identityNumber)
    {
        var resolved = string.IsNullOrWhiteSpace(taxNumber)
            ? identityNumber ?? string.Empty
            : taxNumber;

        return resolved.Trim();
    }

    private static string ResolveTaxSchemeId(string taxNumber) =>
        taxNumber.Length == 11 ? "TCKN" : "VKN";

    private static string BuildCustomerTitle(string? title1, string? title2)
    {
        var first = title1?.Trim() ?? string.Empty;
        var second = title2?.Trim() ?? string.Empty;

        return string.IsNullOrWhiteSpace(second)
            ? first
            : $"{first} {second}".Trim();
    }

    private static string NormalizePhone(string? phone1, string? phone2)
    {
        var values = new[] { phone1?.Trim(), phone2?.Trim() }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        return values.Length == 0 ? string.Empty : string.Join(" ", values);
    }

    private static string BuildInvoiceId(string documentSerie, int documentOrderNo, int year)
    {
        var prefix = (documentSerie ?? string.Empty).Trim().ToUpperInvariant();

        if (prefix.Length > 3)
        {
            prefix = prefix[..3];
        }

        return $"{prefix}{year}{documentOrderNo:D9}";
    }

    private static string BuildLocalDocumentId(string invoiceId, InvoiceSendingScenario scenario) =>
        $"{scenario}:{invoiceId}";

    private static string ResolveUnitCode(string unitName)
    {
        var normalized = (unitName ?? string.Empty).Trim().ToUpperInvariant();

        return normalized switch
        {
            "AD" or "ADET" => "C62",
            "KG" or "KILO" or "KILOGRAM" => "KGM",
            "GR" or "GRAM" => "GRM",
            "LT" or "LITRE" or "LITRE." => "LTR",
            "MT" or "METRE" => "MTR",
            "KOLI" => "AB",
            "KASA" => "BX",
            "PAKET" => "NIU",
            "SAAT" => "HUR",
            "GUN" => "DAY",
            _ => "NIU"
        };
    }

    private static decimal NormalizeQuantity(decimal quantity) => quantity <= 0m ? 1m : quantity;

    private static string FormatTurkishAmountInWords(decimal amount)
    {
        var rounded = RoundMoney(Math.Abs(amount));
        var lira = (long)Math.Truncate(rounded);
        var kurus = (int)((rounded - lira) * 100m);
        var prefix = amount < 0m ? "Eksi" : string.Empty;

        return $"{prefix}{NumberToTurkishWords(lira)} TL, {NumberToTurkishWords(kurus)} Krş.";
    }

    private static string NumberToTurkishWords(long value)
    {
        if (value == 0)
        {
            return "Sıfır";
        }

        var groups = new[] { string.Empty, "Bin", "Milyon", "Milyar", "Trilyon" };
        var parts = new List<string>();
        var groupIndex = 0;

        while (value > 0 && groupIndex < groups.Length)
        {
            var groupValue = (int)(value % 1000);
            if (groupValue > 0)
            {
                var groupText = groupValue == 1 && groupIndex == 1
                    ? groups[groupIndex]
                    : $"{ThreeDigitNumberToTurkishWords(groupValue)}{groups[groupIndex]}";

                parts.Insert(0, groupText);
            }

            value /= 1000;
            groupIndex++;
        }

        return string.Concat(parts);
    }

    private static string ThreeDigitNumberToTurkishWords(int value)
    {
        var ones = new[]
        {
            string.Empty,
            "Bir",
            "İki",
            "Üç",
            "Dört",
            "Beş",
            "Altı",
            "Yedi",
            "Sekiz",
            "Dokuz"
        };
        var tens = new[]
        {
            string.Empty,
            "On",
            "Yirmi",
            "Otuz",
            "Kırk",
            "Elli",
            "Altmış",
            "Yetmiş",
            "Seksen",
            "Doksan"
        };
        var hundreds = value / 100;
        var remainder = value % 100;
        var result = new StringBuilder();

        if (hundreds == 1)
        {
            result.Append("Yüz");
        }
        else if (hundreds > 1)
        {
            result.Append(ones[hundreds]);
            result.Append("Yüz");
        }

        result.Append(tens[remainder / 10]);
        result.Append(ones[remainder % 10]);

        return result.ToString();
    }

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string FormatAmount(decimal value) =>
        RoundMoney(value).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    private static string FormatQuantity(decimal value) =>
        value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

    private static string FormatRate(decimal value) =>
        value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<List<T>> ExecuteReaderAsync<T>(
        DbContext context,
        string sql,
        Action<DbCommand> configureCommand,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken)
    {
        var items = new List<T>();
        var connection = context.Database.GetDbConnection();
        var closeConnection = connection.State == ConnectionState.Closed;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 180;
            configureCommand(command);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(map(reader));
            }
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }

        return items;
    }

    private static async Task<int> ExecuteNonQueryAsync(
        DbContext context,
        string sql,
        Action<DbCommand> configureCommand,
        CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var closeConnection = connection.State == ConnectionState.Closed;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 180;
            configureCommand(command);

            return await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string ReadString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? string.Empty
            : reader.GetValue(ordinal)?.ToString()?.Trim() ?? string.Empty;
    }

    private static int ReadInt32(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        if (reader.IsDBNull(ordinal))
        {
            return 0;
        }

        return Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static Guid ReadGuid(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        if (reader.IsDBNull(ordinal))
        {
            return Guid.Empty;
        }

        var value = reader.GetValue(ordinal);

        return value is Guid guid
            ? guid
            : Guid.Parse(value.ToString() ?? Guid.Empty.ToString());
    }

    private static DateTime ReadDateTime(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? DateTime.MinValue
            : Convert.ToDateTime(reader.GetValue(ordinal));
    }

    private static DateTime? ReadNullableDateTime(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToDateTime(reader.GetValue(ordinal));
    }

    private static decimal ReadDecimal(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        if (reader.IsDBNull(ordinal))
        {
            return 0m;
        }

        return Convert.ToDecimal(reader.GetValue(ordinal));
    }

    private sealed record PendingInvoiceRecord(
        Guid InvoiceGuid,
        string DocumentSerie,
        int DocumentOrderNo,
        string InvoiceId,
        DateTime DocumentDate,
        string SentDocumentNo,
        string CustomerCode,
        string CustomerTitle,
        string CustomerTaxNumber,
        string TargetAlias,
        string TaxOffice,
        string AddressStreet,
        string AddressStreet2,
        string AddressDistrict,
        string AddressCity,
        string PostalCode,
        string Phone,
        string Email,
        int CariMovementType,
        int DocumentType,
        int ReturnFlag,
        int EBelgeTuru,
        string IstisnaKodu,
        string OzelMatrahKodu,
        decimal LineExtensionTotal,
        decimal TaxTotal,
        decimal Rusum,
        decimal PayableTotal,
        string ShipmentDocumentNo,
        DateTime? ShipmentDocumentDate,
        string ReturnInvoiceNo,
        DateTime? ReturnInvoiceDate,
        string WarehouseName,
        string Description,
        InvoiceSendingScenario Scenario)
    {
        public bool IsSent => !string.IsNullOrWhiteSpace(SentDocumentNo);

        public bool IsReturn => ReturnFlag != 0;
    }

    private sealed record ReturnReferenceCandidateRecord(
        string SourceDocumentSerie,
        int SourceDocumentOrderNo,
        string InvoiceNo,
        DateTime? InvoiceDate,
        DateTime? DocumentDate,
        DateTime CreatedAt,
        string CustomerCode,
        string CustomerTitle,
        decimal LineExtensionTotal,
        decimal TaxTotal,
        decimal PayableTotal,
        bool IsGeneratedInvoiceNo);

    private sealed record InvoiceLineSeed(
        string Code,
        string Name,
        decimal Quantity,
        decimal GrossAmount,
        IReadOnlyCollection<decimal> Discounts,
        decimal NetAmount,
        decimal TaxAmount,
        decimal TaxRate,
        string UnitCode);

    private sealed record PartyInfo(
        string Code,
        string DisplayName,
        string TaxNumber,
        string TaxOffice,
        string Street,
        string Street2,
        string District,
        string City,
        string PostalCode,
        string Phone,
        string Email,
        string CountryCode,
        string CountryName);

    private sealed record BuiltInvoiceDocument(
        string InvoiceId,
        string CustomerTaxNumber,
        string TargetAlias,
        string CustomerTitle,
        string XmlContent,
        XElement InvoiceElement,
        string ProfileId,
        string InvoiceTypeCode);

    private sealed record ServiceSendResponse(
        string ServiceDocumentId,
        string ServiceDocumentNumber);
}

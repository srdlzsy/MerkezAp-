using System.Data;
using System.Data.Common;
using System.Globalization;
using FurpaMerkezApi.Application.Modules.RaporIslemleri.TedarikciPerformansKarnesi;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.RaporIslemleri.TedarikciPerformansKarnesi;

public sealed class TedarikciPerformansKarnesiUseCase(
    MikroDbContext mikroDbContext,
    AuthDbContext authDbContext)
    : ITedarikciPerformansKarnesiUseCase
{
    private const double QuantityTolerance = 0.000001d;
    private const string InvoiceMetricsState = "summary-only";

    public async Task<SupplierPerformanceReportDto> GetReportAsync(
        SupplierPerformanceRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeRequest(request);
        var rows = await LoadSupplierRowsAsync(
            normalized.WarehouseNo,
            normalized.StartDate,
            normalized.EndDateExclusive,
            normalized.CustomerCode,
            cancellationToken);

        var incomingInvoicesByTaxNo = await LoadIncomingInvoiceMetricsAsync(
            rows.Select(row => row.TaxNoOrTckn),
            normalized.StartDate,
            normalized.EndDateExclusive,
            cancellationToken);

        var items = rows
            .Select(row => MapCard(row, incomingInvoicesByTaxNo))
            .OrderBy(item => item.Score)
            .ThenByDescending(item => item.Receiving.ReceivedQuantity)
            .ThenBy(item => item.CustomerTitle, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.CustomerCode, StringComparer.OrdinalIgnoreCase)
            .Take(normalized.Take)
            .ToArray();

        return new SupplierPerformanceReportDto(
            normalized.WarehouseNo,
            normalized.StartDate,
            normalized.EndDate,
            DateTime.UtcNow,
            CreateSummary(items),
            items);
    }

    public async Task<SupplierPerformanceDetailDto> GetDetailAsync(
        SupplierPerformanceDetailRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerCode))
        {
            throw new ArgumentException("Customer code is required.", nameof(request.CustomerCode));
        }

        var normalized = NormalizeRequest(new SupplierPerformanceRequest(
            request.WarehouseNo,
            request.StartDate,
            request.EndDate,
            request.CustomerCode,
            1));
        var report = await GetReportAsync(
            new SupplierPerformanceRequest(
                normalized.WarehouseNo,
                normalized.StartDate,
                normalized.EndDate,
                normalized.CustomerCode,
                1),
            cancellationToken);
        var card = report.Items.FirstOrDefault()
                   ?? throw new KeyNotFoundException($"Supplier performance card was not found for customer {request.CustomerCode}.");

        var eventTake = Math.Clamp(request.EventTake <= 0 ? 100 : request.EventTake, 1, 500);
        var events = await LoadDetailEventsAsync(
            normalized.WarehouseNo,
            normalized.StartDate,
            normalized.EndDateExclusive,
            normalized.CustomerCode!,
            eventTake,
            cancellationToken);
        var incomingEvents = await LoadIncomingInvoiceEventsAsync(
            card.TaxNoOrTckn,
            normalized.StartDate,
            normalized.EndDateExclusive,
            cancellationToken);

        var mergedEvents = events
            .Concat(incomingEvents)
            .OrderByDescending(item => item.EventDate ?? DateTime.MinValue)
            .ThenBy(item => item.Source, StringComparer.OrdinalIgnoreCase)
            .Take(eventTake)
            .ToArray();

        return new SupplierPerformanceDetailDto(card, mergedEvents);
    }

    private async Task<IReadOnlyCollection<SupplierPerformanceRow>> LoadSupplierRowsAsync(
        int? warehouseNo,
        DateTime startDate,
        DateTime endDateExclusive,
        string? customerCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH OrderRows AS (
                SELECT
                    LTRIM(RTRIM(ISNULL(sip.sip_musteri_kod, N''))) AS CustomerCode,
                    COUNT(DISTINCT CONCAT(ISNULL(sip.sip_evrakno_seri, N''), N'|', ISNULL(sip.sip_evrakno_sira, 0))) AS OrderDocumentCount,
                    COUNT_BIG(*) AS OrderLineCount,
                    SUM(ISNULL(sip.sip_miktar, 0)) AS OrderedQuantity,
                    SUM(ISNULL(sip.sip_teslim_miktar, 0)) AS DeliveredQuantity,
                    SUM(CASE
                            WHEN ISNULL(sip.sip_teslim_miktar, 0) + 0.000001 < ISNULL(sip.sip_miktar, 0)
                                 AND sip.sip_teslim_tarih < @endDateExclusive
                            THEN 1 ELSE 0
                        END) AS OpenLateLineCount
                FROM SIPARISLER sip WITH (NOLOCK)
                WHERE ISNULL(sip.sip_iptal, 0) = 0
                  AND sip.sip_tip = 1
                  AND sip.sip_tarih >= @startDate
                  AND sip.sip_tarih < @endDateExclusive
                  AND (@warehouseNo IS NULL OR sip.sip_depono = @warehouseNo)
                  AND (@customerCode IS NULL OR sip.sip_musteri_kod = @customerCode)
                  AND NULLIF(LTRIM(RTRIM(ISNULL(sip.sip_musteri_kod, N''))), N'') IS NOT NULL
                GROUP BY LTRIM(RTRIM(ISNULL(sip.sip_musteri_kod, N'')))
            ),
            OrderDelivery AS (
                SELECT
                    LTRIM(RTRIM(ISNULL(sip.sip_musteri_kod, N''))) AS CustomerCode,
                    sip.sip_Guid,
                    sip.sip_teslim_tarih AS ExpectedDeliveryDate,
                    MIN(sth.sth_tarih) AS ActualDeliveryDate
                FROM SIPARISLER sip WITH (NOLOCK)
                INNER JOIN STOK_HAREKETLERI sth WITH (NOLOCK)
                    ON sth.sth_sip_uid = sip.sip_Guid
                   AND ISNULL(sth.sth_iptal, 0) = 0
                   AND sth.sth_evraktip = 13
                   AND sth.sth_tip = 0
                   AND ISNULL(sth.sth_normal_iade, 0) = 0
                WHERE ISNULL(sip.sip_iptal, 0) = 0
                  AND sip.sip_tip = 1
                  AND sip.sip_tarih >= @startDate
                  AND sip.sip_tarih < @endDateExclusive
                  AND (@warehouseNo IS NULL OR sip.sip_depono = @warehouseNo)
                  AND (@customerCode IS NULL OR sip.sip_musteri_kod = @customerCode)
                GROUP BY
                    LTRIM(RTRIM(ISNULL(sip.sip_musteri_kod, N''))),
                    sip.sip_Guid,
                    sip.sip_teslim_tarih
            ),
            LateRows AS (
                SELECT
                    CustomerCode,
                    SUM(CASE
                            WHEN ExpectedDeliveryDate IS NOT NULL
                                 AND ActualDeliveryDate IS NOT NULL
                                 AND ActualDeliveryDate > ExpectedDeliveryDate
                            THEN 1 ELSE 0
                        END) AS LateDeliveredLineCount,
                    AVG(CASE
                            WHEN ExpectedDeliveryDate IS NOT NULL
                                 AND ActualDeliveryDate IS NOT NULL
                                 AND ActualDeliveryDate > ExpectedDeliveryDate
                            THEN CONVERT(float, DATEDIFF(DAY, ExpectedDeliveryDate, ActualDeliveryDate))
                            ELSE NULL
                        END) AS AverageLateDays
                FROM OrderDelivery
                GROUP BY CustomerCode
            ),
            ReceivingRows AS (
                SELECT
                    LTRIM(RTRIM(ISNULL(sth.sth_cari_kodu, N''))) AS CustomerCode,
                    COUNT(DISTINCT CONCAT(ISNULL(sth.sth_evrakno_seri, N''), N'|', ISNULL(sth.sth_evrakno_sira, 0))) AS ReceivingDocumentCount,
                    COUNT_BIG(*) AS ReceivingLineCount,
                    SUM(ISNULL(sth.sth_miktar, 0)) AS ReceivedQuantity,
                    SUM(ISNULL(sth.sth_tutar, 0)) AS ReceivedAmount
                FROM STOK_HAREKETLERI sth WITH (NOLOCK)
                WHERE ISNULL(sth.sth_iptal, 0) = 0
                  AND sth.sth_evraktip = 13
                  AND sth.sth_tip = 0
                  AND ISNULL(sth.sth_normal_iade, 0) = 0
                  AND sth.sth_tarih >= @startDate
                  AND sth.sth_tarih < @endDateExclusive
                  AND (@warehouseNo IS NULL OR sth.sth_giris_depo_no = @warehouseNo)
                  AND (@customerCode IS NULL OR sth.sth_cari_kodu = @customerCode)
                  AND NULLIF(LTRIM(RTRIM(ISNULL(sth.sth_cari_kodu, N''))), N'') IS NOT NULL
                GROUP BY LTRIM(RTRIM(ISNULL(sth.sth_cari_kodu, N'')))
            ),
            DifferenceRows AS (
                SELECT
                    LTRIM(RTRIM(ISNULL(sth.sth_cari_kodu, N''))) AS CustomerCode,
                    COUNT_BIG(*) AS DifferenceLineCount,
                    SUM(CASE
                            WHEN ISNULL(sth.sth_FormulMiktar, 0) < ISNULL(sth.sth_miktar, 0)
                            THEN ISNULL(sth.sth_miktar, 0) - ISNULL(sth.sth_FormulMiktar, 0)
                            ELSE 0
                        END) AS MissingQuantity,
                    SUM(CASE
                            WHEN ISNULL(sth.sth_FormulMiktar, 0) > ISNULL(sth.sth_miktar, 0)
                            THEN ISNULL(sth.sth_FormulMiktar, 0) - ISNULL(sth.sth_miktar, 0)
                            ELSE 0
                        END) AS ExcessQuantity
                FROM STOK_HAREKETLERI sth WITH (NOLOCK)
                WHERE ISNULL(sth.sth_iptal, 0) = 0
                  AND sth.sth_evraktip = 13
                  AND sth.sth_tip = 0
                  AND ISNULL(sth.sth_normal_iade, 0) = 0
                  AND sth.sth_tarih >= @startDate
                  AND sth.sth_tarih < @endDateExclusive
                  AND (@warehouseNo IS NULL OR sth.sth_giris_depo_no = @warehouseNo)
                  AND (@customerCode IS NULL OR sth.sth_cari_kodu = @customerCode)
                  AND sth.sth_FormulMiktar IS NOT NULL
                  AND ABS(ISNULL(sth.sth_FormulMiktar, 0) - ISNULL(sth.sth_miktar, 0)) > 0.000001
                  AND NULLIF(LTRIM(RTRIM(ISNULL(sth.sth_cari_kodu, N''))), N'') IS NOT NULL
                GROUP BY LTRIM(RTRIM(ISNULL(sth.sth_cari_kodu, N'')))
            ),
            ReturnRows AS (
                SELECT
                    LTRIM(RTRIM(ISNULL(sth.sth_cari_kodu, N''))) AS CustomerCode,
                    COUNT(DISTINCT CONCAT(ISNULL(sth.sth_evrakno_seri, N''), N'|', ISNULL(sth.sth_evrakno_sira, 0))) AS ReturnDocumentCount,
                    COUNT_BIG(*) AS ReturnLineCount,
                    SUM(ISNULL(sth.sth_miktar, 0)) AS ReturnedQuantity,
                    SUM(ISNULL(sth.sth_tutar, 0)) AS ReturnedAmount
                FROM STOK_HAREKETLERI sth WITH (NOLOCK)
                WHERE ISNULL(sth.sth_iptal, 0) = 0
                  AND sth.sth_evraktip = 1
                  AND sth.sth_tip = 1
                  AND sth.sth_normal_iade = 1
                  AND sth.sth_tarih >= @startDate
                  AND sth.sth_tarih < @endDateExclusive
                  AND (@warehouseNo IS NULL OR sth.sth_cikis_depo_no = @warehouseNo)
                  AND (@customerCode IS NULL OR sth.sth_cari_kodu = @customerCode)
                  AND NULLIF(LTRIM(RTRIM(ISNULL(sth.sth_cari_kodu, N''))), N'') IS NOT NULL
                GROUP BY LTRIM(RTRIM(ISNULL(sth.sth_cari_kodu, N'')))
            ),
            OutageRows AS (
                SELECT
                    LTRIM(RTRIM(ISNULL(sto.sto_sat_cari_kod, N''))) AS CustomerCode,
                    COUNT(DISTINCT CONCAT(ISNULL(sth.sth_evrakno_seri, N''), N'|', ISNULL(sth.sth_evrakno_sira, 0), N'|', ISNULL(sth.sth_cins, 0))) AS OutageDocumentCount,
                    COUNT_BIG(*) AS OutageLineCount,
                    SUM(ISNULL(sth.sth_miktar, 0)) AS OutageQuantity,
                    SUM(ISNULL(sth.sth_tutar, 0)) AS OutageAmount
                FROM STOK_HAREKETLERI sth WITH (NOLOCK)
                INNER JOIN STOKLAR sto WITH (NOLOCK)
                    ON sto.sto_kod = sth.sth_stok_kod
                WHERE ISNULL(sth.sth_iptal, 0) = 0
                  AND sth.sth_evraktip = 0
                  AND sth.sth_tip = 1
                  AND ISNULL(sth.sth_normal_iade, 0) = 0
                  AND sth.sth_cins IN (4, 5)
                  AND sth.sth_tarih >= @startDate
                  AND sth.sth_tarih < @endDateExclusive
                  AND (@warehouseNo IS NULL OR sth.sth_cikis_depo_no = @warehouseNo)
                  AND (@customerCode IS NULL OR sto.sto_sat_cari_kod = @customerCode)
                  AND NULLIF(LTRIM(RTRIM(ISNULL(sto.sto_sat_cari_kod, N''))), N'') IS NOT NULL
                GROUP BY LTRIM(RTRIM(ISNULL(sto.sto_sat_cari_kod, N'')))
            ),
            IssuedInvoiceRows AS (
                SELECT
                    LTRIM(RTRIM(ISNULL(ch.cha_ciro_cari_kodu, ISNULL(ch.cha_kod, N'')))) AS CustomerCode,
                    COUNT(DISTINCT CONCAT(ISNULL(ch.cha_evrakno_seri, N''), N'|', ISNULL(ch.cha_evrakno_sira, 0))) AS IssuedInvoiceCount,
                    SUM(ISNULL(ch.cha_meblag, 0)) AS IssuedInvoiceAmount
                FROM CARI_HESAP_HAREKETLERI ch WITH (NOLOCK)
                WHERE ISNULL(ch.cha_iptal, 0) = 0
                  AND ch.cha_tip = 0
                  AND ch.cha_belge_tarih >= @startDate
                  AND ch.cha_belge_tarih < @endDateExclusive
                  AND (@customerCode IS NULL OR ch.cha_ciro_cari_kodu = @customerCode OR ch.cha_kod = @customerCode)
                  AND NULLIF(LTRIM(RTRIM(ISNULL(ch.cha_ciro_cari_kodu, ISNULL(ch.cha_kod, N'')))), N'') IS NOT NULL
                GROUP BY LTRIM(RTRIM(ISNULL(ch.cha_ciro_cari_kodu, ISNULL(ch.cha_kod, N''))))
            ),
            SupplierCodes AS (
                SELECT CustomerCode FROM OrderRows
                UNION SELECT CustomerCode FROM ReceivingRows
                UNION SELECT CustomerCode FROM DifferenceRows
                UNION SELECT CustomerCode FROM ReturnRows
                UNION SELECT CustomerCode FROM OutageRows
                UNION SELECT CustomerCode FROM IssuedInvoiceRows
            )
            SELECT
                sc.CustomerCode,
                LTRIM(RTRIM(CONCAT(
                    ISNULL(cari.cari_unvan1, N''),
                    CASE WHEN ISNULL(cari.cari_unvan2, N'') = N'' THEN N'' ELSE N' ' + cari.cari_unvan2 END))) AS CustomerTitle,
                ISNULL(NULLIF(cari.cari_vdaire_no, N''), ISNULL(cari.cari_VergiKimlikNo, N'')) AS TaxNoOrTckn,
                ISNULL(ord.OrderDocumentCount, 0) AS OrderDocumentCount,
                ISNULL(ord.OrderLineCount, 0) AS OrderLineCount,
                ISNULL(ord.OrderedQuantity, 0) AS OrderedQuantity,
                ISNULL(ord.DeliveredQuantity, 0) AS OrderDeliveredQuantity,
                ISNULL(ord.OpenLateLineCount, 0) AS OpenLateLineCount,
                ISNULL(late.LateDeliveredLineCount, 0) AS LateDeliveredLineCount,
                ISNULL(late.AverageLateDays, 0) AS AverageLateDays,
                ISNULL(rec.ReceivingDocumentCount, 0) AS ReceivingDocumentCount,
                ISNULL(rec.ReceivingLineCount, 0) AS ReceivingLineCount,
                ISNULL(rec.ReceivedQuantity, 0) AS ReceivedQuantity,
                ISNULL(rec.ReceivedAmount, 0) AS ReceivedAmount,
                ISNULL(diff.DifferenceLineCount, 0) AS DifferenceLineCount,
                ISNULL(diff.MissingQuantity, 0) AS MissingQuantity,
                ISNULL(diff.ExcessQuantity, 0) AS ExcessQuantity,
                ISNULL(ret.ReturnDocumentCount, 0) AS ReturnDocumentCount,
                ISNULL(ret.ReturnLineCount, 0) AS ReturnLineCount,
                ISNULL(ret.ReturnedQuantity, 0) AS ReturnedQuantity,
                ISNULL(ret.ReturnedAmount, 0) AS ReturnedAmount,
                ISNULL(outg.OutageDocumentCount, 0) AS OutageDocumentCount,
                ISNULL(outg.OutageLineCount, 0) AS OutageLineCount,
                ISNULL(outg.OutageQuantity, 0) AS OutageQuantity,
                ISNULL(outg.OutageAmount, 0) AS OutageAmount,
                ISNULL(inv.IssuedInvoiceCount, 0) AS IssuedInvoiceCount,
                ISNULL(inv.IssuedInvoiceAmount, 0) AS IssuedInvoiceAmount
            FROM SupplierCodes sc
            INNER JOIN CARI_HESAPLAR cari WITH (NOLOCK)
                ON cari.cari_kod = sc.CustomerCode
            LEFT JOIN OrderRows ord ON ord.CustomerCode = sc.CustomerCode
            LEFT JOIN LateRows late ON late.CustomerCode = sc.CustomerCode
            LEFT JOIN ReceivingRows rec ON rec.CustomerCode = sc.CustomerCode
            LEFT JOIN DifferenceRows diff ON diff.CustomerCode = sc.CustomerCode
            LEFT JOIN ReturnRows ret ON ret.CustomerCode = sc.CustomerCode
            LEFT JOIN OutageRows outg ON outg.CustomerCode = sc.CustomerCode
            LEFT JOIN IssuedInvoiceRows inv ON inv.CustomerCode = sc.CustomerCode
            WHERE (@customerCode IS NULL OR sc.CustomerCode = @customerCode)
              AND ISNULL(cari.cari_iptal, 0) = 0
            ORDER BY sc.CustomerCode;
            """;

        return await ExecuteReaderAsync(
            mikroDbContext.Database.GetDbConnection(),
            sql,
            command =>
            {
                AddParameter(command, "@warehouseNo", warehouseNo);
                AddParameter(command, "@startDate", startDate);
                AddParameter(command, "@endDateExclusive", endDateExclusive);
                AddParameter(command, "@customerCode", string.IsNullOrWhiteSpace(customerCode) ? null : customerCode.Trim());
            },
            reader => new SupplierPerformanceRow(
                ReadString(reader, "CustomerCode"),
                ReadString(reader, "CustomerTitle"),
                ReadString(reader, "TaxNoOrTckn"),
                ReadInt(reader, "OrderDocumentCount"),
                ReadInt(reader, "OrderLineCount"),
                ReadDouble(reader, "OrderedQuantity"),
                ReadDouble(reader, "OrderDeliveredQuantity"),
                ReadInt(reader, "OpenLateLineCount"),
                ReadInt(reader, "LateDeliveredLineCount"),
                ReadDouble(reader, "AverageLateDays"),
                ReadInt(reader, "ReceivingDocumentCount"),
                ReadInt(reader, "ReceivingLineCount"),
                ReadDouble(reader, "ReceivedQuantity"),
                ReadDouble(reader, "ReceivedAmount"),
                ReadInt(reader, "DifferenceLineCount"),
                ReadDouble(reader, "MissingQuantity"),
                ReadDouble(reader, "ExcessQuantity"),
                ReadInt(reader, "ReturnDocumentCount"),
                ReadInt(reader, "ReturnLineCount"),
                ReadDouble(reader, "ReturnedQuantity"),
                ReadDouble(reader, "ReturnedAmount"),
                ReadInt(reader, "OutageDocumentCount"),
                ReadInt(reader, "OutageLineCount"),
                ReadDouble(reader, "OutageQuantity"),
                ReadDouble(reader, "OutageAmount"),
                ReadInt(reader, "IssuedInvoiceCount"),
                ReadDouble(reader, "IssuedInvoiceAmount")),
            cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, IncomingInvoiceMetric>> LoadIncomingInvoiceMetricsAsync(
        IEnumerable<string> taxNos,
        DateTime startDate,
        DateTime endDateExclusive,
        CancellationToken cancellationToken)
    {
        var normalizedTaxNos = taxNos
            .Select(NormalizeTaxNo)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedTaxNos.Length == 0)
        {
            return new Dictionary<string, IncomingInvoiceMetric>(StringComparer.OrdinalIgnoreCase);
        }

        var rows = await authDbContext.UyumsoftInboxInvoices
            .AsNoTracking()
            .Where(invoice =>
                normalizedTaxNos.Contains(invoice.CustomerTcknVkn) &&
                ((invoice.InvoiceDate.HasValue &&
                  invoice.InvoiceDate.Value >= startDate &&
                  invoice.InvoiceDate.Value < endDateExclusive) ||
                 (!invoice.InvoiceDate.HasValue &&
                  invoice.CreateDate.HasValue &&
                  invoice.CreateDate.Value >= startDate &&
                  invoice.CreateDate.Value < endDateExclusive)))
            .GroupBy(invoice => invoice.CustomerTcknVkn)
            .Select(grouped => new
            {
                TaxNo = grouped.Key,
                Count = grouped.Count(),
                Amount = grouped.Sum(invoice => invoice.InvoiceTotal)
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => NormalizeTaxNo(row.TaxNo),
            row => new IncomingInvoiceMetric(row.Count, (double)row.Amount),
            StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyCollection<SupplierPerformanceEventDto>> LoadIncomingInvoiceEventsAsync(
        string taxNo,
        DateTime startDate,
        DateTime endDateExclusive,
        CancellationToken cancellationToken)
    {
        var normalizedTaxNo = NormalizeTaxNo(taxNo);

        if (normalizedTaxNo.Length == 0)
        {
            return [];
        }

        var rows = await authDbContext.UyumsoftInboxInvoices
            .AsNoTracking()
            .Where(invoice =>
                invoice.CustomerTcknVkn == normalizedTaxNo &&
                ((invoice.InvoiceDate.HasValue &&
                  invoice.InvoiceDate.Value >= startDate &&
                  invoice.InvoiceDate.Value < endDateExclusive) ||
                 (!invoice.InvoiceDate.HasValue &&
                  invoice.CreateDate.HasValue &&
                  invoice.CreateDate.Value >= startDate &&
                  invoice.CreateDate.Value < endDateExclusive)))
            .OrderByDescending(invoice => invoice.InvoiceDate ?? invoice.CreateDate)
            .Take(250)
            .ToListAsync(cancellationToken);

        return rows
            .Select(invoice => new SupplierPerformanceEventDto(
                "FaturaGoruntuleme",
                "IncomingInvoice",
                invoice.InvoiceDate ?? invoice.CreateDate,
                string.Empty,
                0,
                invoice.InvoiceId,
                string.Empty,
                invoice.InvoiceType,
                0,
                string.Empty,
                0d,
                0d,
                (double)invoice.InvoiceTotal,
                invoice.DespatchId))
            .ToArray();
    }

    private async Task<IReadOnlyCollection<SupplierPerformanceEventDto>> LoadDetailEventsAsync(
        int? warehouseNo,
        DateTime startDate,
        DateTime endDateExclusive,
        string customerCode,
        int take,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (@take)
                Source,
                EventType,
                EventDate,
                DocumentSerie,
                DocumentOrderNo,
                DocumentNo,
                StockCode,
                StockName,
                WarehouseNo,
                WarehouseName,
                Quantity,
                RelatedQuantity,
                Amount,
                Description
            FROM (
                SELECT
                    N'SIPARISLER' AS Source,
                    CASE
                        WHEN ISNULL(sip.sip_teslim_miktar, 0) + 0.000001 < ISNULL(sip.sip_miktar, 0)
                             AND sip.sip_teslim_tarih < @endDateExclusive
                        THEN N'OpenLateOrder'
                        ELSE N'Order'
                    END AS EventType,
                    sip.sip_tarih AS EventDate,
                    ISNULL(sip.sip_evrakno_seri, N'') AS DocumentSerie,
                    ISNULL(sip.sip_evrakno_sira, 0) AS DocumentOrderNo,
                    ISNULL(sip.sip_belgeno, N'') AS DocumentNo,
                    ISNULL(sip.sip_stok_kod, N'') AS StockCode,
                    ISNULL(sto.sto_isim, N'') AS StockName,
                    ISNULL(sip.sip_depono, 0) AS WarehouseNo,
                    ISNULL(dep.dep_adi, N'') AS WarehouseName,
                    ISNULL(sip.sip_miktar, 0) AS Quantity,
                    ISNULL(sip.sip_teslim_miktar, 0) AS RelatedQuantity,
                    ISNULL(sip.sip_tutar, 0) AS Amount,
                    ISNULL(sip.sip_aciklama, N'') AS Description
                FROM SIPARISLER sip WITH (NOLOCK)
                LEFT JOIN STOKLAR sto WITH (NOLOCK) ON sto.sto_kod = sip.sip_stok_kod
                LEFT JOIN DEPOLAR dep WITH (NOLOCK) ON dep.dep_no = sip.sip_depono
                WHERE ISNULL(sip.sip_iptal, 0) = 0
                  AND sip.sip_tip = 1
                  AND sip.sip_tarih >= @startDate
                  AND sip.sip_tarih < @endDateExclusive
                  AND sip.sip_musteri_kod = @customerCode
                  AND (@warehouseNo IS NULL OR sip.sip_depono = @warehouseNo)

                UNION ALL

                SELECT
                    N'STOK_HAREKETLERI' AS Source,
                    N'ReceivingDifference' AS EventType,
                    sth.sth_tarih AS EventDate,
                    ISNULL(sth.sth_evrakno_seri, N'') AS DocumentSerie,
                    ISNULL(sth.sth_evrakno_sira, 0) AS DocumentOrderNo,
                    ISNULL(sth.sth_belge_no, N'') AS DocumentNo,
                    ISNULL(sth.sth_stok_kod, N'') AS StockCode,
                    ISNULL(sto.sto_isim, N'') AS StockName,
                    ISNULL(sth.sth_giris_depo_no, 0) AS WarehouseNo,
                    ISNULL(dep.dep_adi, N'') AS WarehouseName,
                    ISNULL(sth.sth_miktar, 0) AS Quantity,
                    ISNULL(sth.sth_FormulMiktar, 0) AS RelatedQuantity,
                    ISNULL(sth.sth_tutar, 0) AS Amount,
                    ISNULL(sth.sth_aciklama, N'') AS Description
                FROM STOK_HAREKETLERI sth WITH (NOLOCK)
                LEFT JOIN STOKLAR sto WITH (NOLOCK) ON sto.sto_kod = sth.sth_stok_kod
                LEFT JOIN DEPOLAR dep WITH (NOLOCK) ON dep.dep_no = sth.sth_giris_depo_no
                WHERE ISNULL(sth.sth_iptal, 0) = 0
                  AND sth.sth_evraktip = 13
                  AND sth.sth_tip = 0
                  AND ISNULL(sth.sth_normal_iade, 0) = 0
                  AND sth.sth_tarih >= @startDate
                  AND sth.sth_tarih < @endDateExclusive
                  AND sth.sth_cari_kodu = @customerCode
                  AND (@warehouseNo IS NULL OR sth.sth_giris_depo_no = @warehouseNo)
                  AND sth.sth_FormulMiktar IS NOT NULL
                  AND ABS(ISNULL(sth.sth_FormulMiktar, 0) - ISNULL(sth.sth_miktar, 0)) > 0.000001

                UNION ALL

                SELECT
                    N'STOK_HAREKETLERI' AS Source,
                    N'CompanyReturn' AS EventType,
                    sth.sth_tarih AS EventDate,
                    ISNULL(sth.sth_evrakno_seri, N'') AS DocumentSerie,
                    ISNULL(sth.sth_evrakno_sira, 0) AS DocumentOrderNo,
                    ISNULL(sth.sth_belge_no, N'') AS DocumentNo,
                    ISNULL(sth.sth_stok_kod, N'') AS StockCode,
                    ISNULL(sto.sto_isim, N'') AS StockName,
                    ISNULL(sth.sth_cikis_depo_no, 0) AS WarehouseNo,
                    ISNULL(dep.dep_adi, N'') AS WarehouseName,
                    ISNULL(sth.sth_miktar, 0) AS Quantity,
                    0 AS RelatedQuantity,
                    ISNULL(sth.sth_tutar, 0) AS Amount,
                    ISNULL(sth.sth_aciklama, N'') AS Description
                FROM STOK_HAREKETLERI sth WITH (NOLOCK)
                LEFT JOIN STOKLAR sto WITH (NOLOCK) ON sto.sto_kod = sth.sth_stok_kod
                LEFT JOIN DEPOLAR dep WITH (NOLOCK) ON dep.dep_no = sth.sth_cikis_depo_no
                WHERE ISNULL(sth.sth_iptal, 0) = 0
                  AND sth.sth_evraktip = 1
                  AND sth.sth_tip = 1
                  AND sth.sth_normal_iade = 1
                  AND sth.sth_tarih >= @startDate
                  AND sth.sth_tarih < @endDateExclusive
                  AND sth.sth_cari_kodu = @customerCode
                  AND (@warehouseNo IS NULL OR sth.sth_cikis_depo_no = @warehouseNo)

                UNION ALL

                SELECT
                    N'STOK_HAREKETLERI' AS Source,
                    CASE WHEN sth.sth_cins = 4 THEN N'OutageImpact' ELSE N'ExpenseImpact' END AS EventType,
                    sth.sth_tarih AS EventDate,
                    ISNULL(sth.sth_evrakno_seri, N'') AS DocumentSerie,
                    ISNULL(sth.sth_evrakno_sira, 0) AS DocumentOrderNo,
                    ISNULL(sth.sth_belge_no, N'') AS DocumentNo,
                    ISNULL(sth.sth_stok_kod, N'') AS StockCode,
                    ISNULL(sto.sto_isim, N'') AS StockName,
                    ISNULL(sth.sth_cikis_depo_no, 0) AS WarehouseNo,
                    ISNULL(dep.dep_adi, N'') AS WarehouseName,
                    ISNULL(sth.sth_miktar, 0) AS Quantity,
                    0 AS RelatedQuantity,
                    ISNULL(sth.sth_tutar, 0) AS Amount,
                    ISNULL(sth.sth_aciklama, N'') AS Description
                FROM STOK_HAREKETLERI sth WITH (NOLOCK)
                INNER JOIN STOKLAR sto WITH (NOLOCK) ON sto.sto_kod = sth.sth_stok_kod
                LEFT JOIN DEPOLAR dep WITH (NOLOCK) ON dep.dep_no = sth.sth_cikis_depo_no
                WHERE ISNULL(sth.sth_iptal, 0) = 0
                  AND sth.sth_evraktip = 0
                  AND sth.sth_tip = 1
                  AND ISNULL(sth.sth_normal_iade, 0) = 0
                  AND sth.sth_cins IN (4, 5)
                  AND sth.sth_tarih >= @startDate
                  AND sth.sth_tarih < @endDateExclusive
                  AND sto.sto_sat_cari_kod = @customerCode
                  AND (@warehouseNo IS NULL OR sth.sth_cikis_depo_no = @warehouseNo)

                UNION ALL

                SELECT
                    N'CARI_HESAP_HAREKETLERI' AS Source,
                    N'IssuedInvoice' AS EventType,
                    ch.cha_belge_tarih AS EventDate,
                    ISNULL(ch.cha_evrakno_seri, N'') AS DocumentSerie,
                    ISNULL(ch.cha_evrakno_sira, 0) AS DocumentOrderNo,
                    ISNULL(ch.cha_belge_no, N'') AS DocumentNo,
                    N'' AS StockCode,
                    N'' AS StockName,
                    0 AS WarehouseNo,
                    N'' AS WarehouseName,
                    ISNULL(ch.cha_miktari, 0) AS Quantity,
                    0 AS RelatedQuantity,
                    ISNULL(ch.cha_meblag, 0) AS Amount,
                    ISNULL(ch.cha_aciklama, N'') AS Description
                FROM CARI_HESAP_HAREKETLERI ch WITH (NOLOCK)
                WHERE ISNULL(ch.cha_iptal, 0) = 0
                  AND ch.cha_tip = 0
                  AND ch.cha_belge_tarih >= @startDate
                  AND ch.cha_belge_tarih < @endDateExclusive
                  AND (ch.cha_ciro_cari_kodu = @customerCode OR ch.cha_kod = @customerCode)
            ) events
            ORDER BY EventDate DESC, Source, DocumentSerie, DocumentOrderNo;
            """;

        return await ExecuteReaderAsync(
            mikroDbContext.Database.GetDbConnection(),
            sql,
            command =>
            {
                AddParameter(command, "@warehouseNo", warehouseNo);
                AddParameter(command, "@startDate", startDate);
                AddParameter(command, "@endDateExclusive", endDateExclusive);
                AddParameter(command, "@customerCode", customerCode.Trim());
                AddParameter(command, "@take", take);
            },
            reader => new SupplierPerformanceEventDto(
                ReadString(reader, "Source"),
                ReadString(reader, "EventType"),
                ReadNullableDateTime(reader, "EventDate"),
                ReadString(reader, "DocumentSerie"),
                ReadInt(reader, "DocumentOrderNo"),
                ReadString(reader, "DocumentNo"),
                ReadString(reader, "StockCode"),
                ReadString(reader, "StockName"),
                ReadInt(reader, "WarehouseNo"),
                ReadString(reader, "WarehouseName"),
                ReadDouble(reader, "Quantity"),
                ReadDouble(reader, "RelatedQuantity"),
                ReadDouble(reader, "Amount"),
                ReadString(reader, "Description")),
            cancellationToken);
    }

    private static SupplierPerformanceCardDto MapCard(
        SupplierPerformanceRow row,
        IReadOnlyDictionary<string, IncomingInvoiceMetric> incomingInvoicesByTaxNo)
    {
        incomingInvoicesByTaxNo.TryGetValue(NormalizeTaxNo(row.TaxNoOrTckn), out var incomingInvoices);

        var deliveredQuantity = row.OrderDeliveredQuantity > QuantityTolerance
            ? row.OrderDeliveredQuantity
            : row.ReceivedQuantity;
        var remainingQuantity = Math.Max(0d, row.OrderedQuantity - deliveredQuantity);
        var deliveryRate = Ratio(deliveredQuantity, row.OrderedQuantity);
        var differenceQuantity = Math.Abs(row.MissingQuantity) + Math.Abs(row.ExcessQuantity);
        var differenceRate = Ratio(differenceQuantity, row.ReceivedQuantity);
        var returnRate = Ratio(row.ReturnedQuantity, row.ReceivedQuantity);
        var outageRate = Ratio(row.OutageQuantity, row.ReceivedQuantity);
        var invoiceDifferenceAmount = incomingInvoices.Amount - row.IssuedInvoiceAmount;
        var invoiceDifferenceRate = Ratio(Math.Abs(invoiceDifferenceAmount), Math.Max(incomingInvoices.Amount, row.IssuedInvoiceAmount));

        var deliveryPenalty = Clamp(
            Ratio(row.LateDeliveredLineCount + row.OpenLateLineCount, row.OrderLineCount) * 25d +
            Math.Min(row.AverageLateDays, 30d) * 0.25d,
            0d,
            30d);
        var differencePenalty = Clamp(differenceRate * 30d, 0d, 20d);
        var returnPenalty = Clamp(returnRate * 30d, 0d, 20d);
        var outagePenalty = Clamp(outageRate * 20d, 0d, 10d);
        var invoicePenalty = 0d;
        var totalPenalty = deliveryPenalty + differencePenalty + returnPenalty + outagePenalty + invoicePenalty;
        var score = Clamp(100d - totalPenalty, 0d, 100d);

        var scoreBreakdown = new SupplierPerformanceScoreBreakdownDto(
            Round(deliveryPenalty),
            Round(differencePenalty),
            Round(returnPenalty),
            Round(outagePenalty),
            Round(invoicePenalty),
            Round(totalPenalty));

        return new SupplierPerformanceCardDto(
            row.CustomerCode,
            row.CustomerTitle,
            row.TaxNoOrTckn,
            Round(score),
            ResolveGrade(score),
            ResolveRiskLevel(score, differenceRate, returnRate, row.OpenLateLineCount),
            new SupplierOrderPerformanceDto(
                row.OrderDocumentCount,
                row.OrderLineCount,
                Round(row.OrderedQuantity),
                Round(deliveredQuantity),
                Round(remainingQuantity),
                Round(deliveryRate),
                row.LateDeliveredLineCount,
                row.OpenLateLineCount,
                Round(row.AverageLateDays)),
            new SupplierReceivingPerformanceDto(
                row.ReceivingDocumentCount,
                row.ReceivingLineCount,
                Round(row.ReceivedQuantity),
                Round(row.ReceivedAmount),
                row.DifferenceLineCount,
                Round(row.MissingQuantity),
                Round(row.ExcessQuantity),
                Round(differenceRate)),
            new SupplierReturnPerformanceDto(
                row.ReturnDocumentCount,
                row.ReturnLineCount,
                Round(row.ReturnedQuantity),
                Round(row.ReturnedAmount),
                Round(returnRate)),
            new SupplierOutageImpactDto(
                row.OutageDocumentCount,
                row.OutageLineCount,
                Round(row.OutageQuantity),
                Round(row.OutageAmount),
                Round(outageRate),
                "stok-karti-varsayilan-tedarikci"),
            new SupplierInvoicePerformanceDto(
                row.IssuedInvoiceCount,
                Round(row.IssuedInvoiceAmount),
                incomingInvoices.Count,
                Round(incomingInvoices.Amount),
                Round(invoiceDifferenceAmount),
                Round(invoiceDifferenceRate),
                InvoiceMetricsState,
                "Giden fatura Mikro cari hareketlerinden, gelen fatura Uyumsoft cache ozetinden okunur. Satir bazli fiyat/fatura farki ikinci fazdir."),
            scoreBreakdown);
    }

    private static SupplierPerformanceSummaryDto CreateSummary(IReadOnlyCollection<SupplierPerformanceCardDto> items)
    {
        var supplierCount = items.Count;

        return new SupplierPerformanceSummaryDto(
            supplierCount,
            Round(supplierCount == 0 ? 0d : items.Average(item => item.Score)),
            items.Count(item => string.Equals(item.RiskLevel, "Critical", StringComparison.OrdinalIgnoreCase)),
            items.Count(item => string.Equals(item.RiskLevel, "Warning", StringComparison.OrdinalIgnoreCase)),
            Round(items.Sum(item => item.Orders.OrderedQuantity)),
            Round(items.Sum(item => item.Receiving.ReceivedQuantity)),
            Round(items.Sum(item => item.Returns.ReturnedQuantity)),
            Round(items.Sum(item => item.Receiving.MissingQuantity)),
            Round(items.Sum(item => item.Receiving.ExcessQuantity)),
            Round(items.Sum(item => item.OutageImpact.Quantity)),
            Round(items.Sum(item => item.Invoices.IssuedInvoiceAmount)),
            Round(items.Sum(item => item.Invoices.IncomingInvoiceAmount)),
            Round(items.Sum(item => item.Invoices.InvoiceDifferenceAmount)),
            InvoiceMetricsState);
    }

    private static NormalizedSupplierPerformanceRequest NormalizeRequest(SupplierPerformanceRequest request)
    {
        if (request.StartDate == default)
        {
            throw new ArgumentException("Start date is required.", nameof(request.StartDate));
        }

        if (request.EndDate == default)
        {
            throw new ArgumentException("End date is required.", nameof(request.EndDate));
        }

        if (request.WarehouseNo is <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        var startDate = request.StartDate.Date;
        var endDate = request.EndDate.Date;

        if (endDate < startDate)
        {
            throw new ArgumentException("End date can not be earlier than start date.", nameof(request.EndDate));
        }

        return new NormalizedSupplierPerformanceRequest(
            request.WarehouseNo,
            startDate,
            endDate,
            endDate.AddDays(1),
            string.IsNullOrWhiteSpace(request.CustomerCode) ? null : request.CustomerCode.Trim(),
            Math.Clamp(request.Take <= 0 ? 100 : request.Take, 1, 500));
    }

    private static double Ratio(double numerator, double denominator) =>
        Math.Abs(denominator) <= QuantityTolerance ? 0d : numerator / denominator;

    private static double Clamp(double value, double min, double max) =>
        Math.Max(min, Math.Min(max, value));

    private static string ResolveGrade(double score) =>
        score switch
        {
            >= 90d => "A",
            >= 75d => "B",
            >= 60d => "C",
            >= 45d => "D",
            _ => "E"
        };

    private static string ResolveRiskLevel(
        double score,
        double differenceRate,
        double returnRate,
        int openLateLineCount)
    {
        if (score < 50d || differenceRate >= 0.15d || returnRate >= 0.2d || openLateLineCount >= 10)
        {
            return "Critical";
        }

        if (score < 70d || differenceRate >= 0.05d || returnRate >= 0.08d || openLateLineCount > 0)
        {
            return "Warning";
        }

        return "Healthy";
    }

    private static string NormalizeTaxNo(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();

    private static double Round(double value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static async Task<IReadOnlyCollection<T>> ExecuteReaderAsync<T>(
        DbConnection connection,
        string sql,
        Action<DbCommand> configureCommand,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken)
    {
        var items = new List<T>();
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

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static int ReadInt(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? 0
            : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static double ReadDouble(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? 0d
            : Convert.ToDouble(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static DateTime? ReadNullableDateTime(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToDateTime(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static string ReadString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? string.Empty
            : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
    }

    private sealed record SupplierPerformanceRow(
        string CustomerCode,
        string CustomerTitle,
        string TaxNoOrTckn,
        int OrderDocumentCount,
        int OrderLineCount,
        double OrderedQuantity,
        double OrderDeliveredQuantity,
        int OpenLateLineCount,
        int LateDeliveredLineCount,
        double AverageLateDays,
        int ReceivingDocumentCount,
        int ReceivingLineCount,
        double ReceivedQuantity,
        double ReceivedAmount,
        int DifferenceLineCount,
        double MissingQuantity,
        double ExcessQuantity,
        int ReturnDocumentCount,
        int ReturnLineCount,
        double ReturnedQuantity,
        double ReturnedAmount,
        int OutageDocumentCount,
        int OutageLineCount,
        double OutageQuantity,
        double OutageAmount,
        int IssuedInvoiceCount,
        double IssuedInvoiceAmount);

    private readonly record struct IncomingInvoiceMetric(
        int Count,
        double Amount);

    private sealed record NormalizedSupplierPerformanceRequest(
        int? WarehouseNo,
        DateTime StartDate,
        DateTime EndDate,
        DateTime EndDateExclusive,
        string? CustomerCode,
        int Take);
}

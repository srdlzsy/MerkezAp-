using System.Globalization;
using System.Text.Json;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.ManavMalKabulVeEtiket;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.ManavMalKabulVeEtiket;

public sealed partial class ManavMalKabulVeEtiketService
{
    private const string GreenGrocerGoodsReceiptPath = "/Api/apiMethods/AlimSatimEvragiKaydetV2";

    private async Task<ManavMalKabulVeEtiketCreateMicroGoodsReceiptResultDto> CreateMicroGoodsReceiptMikroApiAsync(
        ManavMalKabulVeEtiketCreateMicroGoodsReceiptRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeCreateMicroGoodsReceiptRequest(request);
        var stockCodes = normalized.Lines.Select(line => line.StockCode).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var stockInfos = await LoadManavStockInfosAsync(stockCodes, cancellationToken);
        var missingStocks = stockCodes.Where(stockCode => !stockInfos.ContainsKey(stockCode)).ToArray();
        if (missingStocks.Length > 0)
        {
            throw new ArgumentException("MNV stock was not found: " + string.Join(", ", missingStocks), nameof(request.Lines));
        }

        var supplier = await mikroWriteDbContext.CARI_HESAPLARs
            .AsNoTracking()
            .FirstOrDefaultAsync(customer => customer.cari_kod == normalized.SupplierCode, cancellationToken)
            ?? throw new ArgumentException("Supplier was not found.", nameof(request.SupplierCode));

        var documentSeries = normalized.DocumentSeries ?? "MNV";
        var documentOrderNo = normalized.DocumentOrderNo
                              ?? await GetNextMicroGoodsReceiptOrderNoAsync(documentSeries, cancellationToken);
        var documentNo = NormalizeOrNull(normalized.DocumentNo) ?? documentOrderNo.ToString(CultureInfo.InvariantCulture);
        var createUserNo = Convert.ToInt16(normalized.MikroUserNo ?? DefaultMikroUserNo);
        var offlineTraceKey = BuildOfflineTraceKey(normalized.Date, normalized.SupplierCode, documentSeries);
        var alternativeCurrencyRate = await GetAlternativeCurrencyRateAsync(normalized.Date, cancellationToken);
        var now = DateTime.Now;
        var temporaryInvoiceGuid = Guid.NewGuid();
        var expectedRows = normalized.Lines
            .Select((line, index) => CreateMicroGoodsReceiptMovement(
                normalized,
                line,
                stockInfos[line.StockCode],
                temporaryInvoiceGuid,
                documentSeries,
                documentOrderNo,
                documentNo,
                createUserNo,
                index,
                now,
                alternativeCurrencyRate,
                offlineTraceKey))
            .ToArray();
        var expectedCustomerMovement = CreateMicroGoodsReceiptCustomerMovement(
            normalized,
            supplier,
            temporaryInvoiceGuid,
            documentSeries,
            documentOrderNo,
            documentNo,
            createUserNo,
            now,
            alternativeCurrencyRate,
            expectedRows,
            offlineTraceKey);

        var existing = await TryReadBackMicroGoodsReceiptAsync(
            normalized,
            documentSeries,
            documentOrderNo,
            expectedRows,
            stockInfos,
            createUserNo,
            offlineTraceKey,
            updatedAcceptanceRecordCount: 0,
            cancellationToken);
        if (existing is not null)
        {
            var existingUpdatedCount = normalized.MarkAcceptanceRecordsTransferred
                ? await MarkAcceptanceRecordsTransferredAsync(normalized.Lines, cancellationToken)
                : 0;
            return existing with { UpdatedAcceptanceRecordCount = existingUpdatedCount };
        }

        var payload = CreateMicroGoodsReceiptMikroApiPayload(expectedCustomerMovement, expectedRows);
        var apiResult = await mikroApiClient.PostWithMikroPayloadAsync<JsonElement>(
            GreenGrocerGoodsReceiptPath,
            payload,
            cancellationToken);
        apiResult.EnsureSuccess();

        mikroWriteDbContext.ChangeTracker.Clear();
        var recovered = await TryReadBackMicroGoodsReceiptAsync(
            normalized,
            documentSeries,
            documentOrderNo,
            expectedRows,
            stockInfos,
            createUserNo,
            offlineTraceKey,
            updatedAcceptanceRecordCount: 0,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "Mikro API returned success, but the green-grocer goods receipt could not be verified by series/order and line totals.");

        var updatedAcceptanceRecordCount = normalized.MarkAcceptanceRecordsTransferred
            ? await MarkAcceptanceRecordsTransferredAsync(normalized.Lines, cancellationToken)
            : 0;
        recovered = recovered with { UpdatedAcceptanceRecordCount = updatedAcceptanceRecordCount };

        var recoveredGuid = recovered.Lines
            .Select(line => Guid.TryParse(line.MovementGuid, out var value) ? value : (Guid?)null)
            .FirstOrDefault(value => value.HasValue);
        await mikroApiClient.MarkRecoveredAsync(
            apiResult,
            recovered.SeriesAndNumber,
            recoveredGuid,
            cancellationToken: cancellationToken);
        return recovered;
    }

    private async Task<ManavMalKabulVeEtiketCreateMicroGoodsReceiptResultDto?> TryReadBackMicroGoodsReceiptAsync(
        ManavMalKabulVeEtiketCreateMicroGoodsReceiptRequest request,
        string documentSeries,
        int documentOrderNo,
        IReadOnlyCollection<STOK_HAREKETLERI> expectedRows,
        IReadOnlyDictionary<string, MicroStockInfo> stockInfos,
        short createUserNo,
        string offlineTraceKey,
        int updatedAcceptanceRecordCount,
        CancellationToken cancellationToken)
    {
        var actualRows = await mikroWriteDbContext.STOK_HAREKETLERIs
            .AsNoTracking()
            .Where(movement =>
                movement.sth_tarih == request.Date &&
                movement.sth_tip == IncomingMovementType &&
                movement.sth_cins == GreenGrocerGoodsReceiptGenre &&
                movement.sth_normal_iade == NormalMovement &&
                movement.sth_evraktip == GreenGrocerGoodsReceiptDocumentType &&
                movement.sth_evrakno_seri == documentSeries &&
                movement.sth_evrakno_sira == documentOrderNo &&
                movement.sth_cari_kodu == request.SupplierCode &&
                movement.sth_giris_depo_no == GreenGrocerWarehouseNo &&
                movement.sth_cikis_depo_no == MainWarehouseNo)
            .OrderBy(movement => movement.sth_satirno)
            .ToArrayAsync(cancellationToken);

        if (!MicroGoodsReceiptRowsMatch(expectedRows, actualRows))
        {
            return null;
        }

        var customerExists = await mikroWriteDbContext.CARI_HESAP_HAREKETLERIs
            .AsNoTracking()
            .AnyAsync(movement =>
                movement.cha_tarihi == request.Date &&
                movement.cha_tip == CustomerInvoiceMovementType &&
                movement.cha_cinsi == GreenGrocerCustomerInvoiceGenre &&
                movement.cha_evrak_tip == GreenGrocerCustomerInvoiceDocumentType &&
                movement.cha_evrakno_seri == documentSeries &&
                movement.cha_evrakno_sira == documentOrderNo &&
                movement.cha_kod == request.SupplierCode,
                cancellationToken);
        if (!customerExists)
        {
            return null;
        }

        var lines = actualRows.Select(row => new ManavMalKabulVeEtiketMicroGoodsReceiptLineDto(
            row.sth_satirno ?? 0,
            row.sth_stok_kod ?? string.Empty,
            stockInfos[row.sth_stok_kod ?? string.Empty].StockName,
            Convert.ToDecimal(row.sth_miktar ?? 0d),
            row.sth_miktar.GetValueOrDefault() == 0d
                ? 0m
                : Round(Convert.ToDecimal(row.sth_tutar.GetValueOrDefault() / row.sth_miktar.GetValueOrDefault())),
            Convert.ToDecimal(row.sth_tutar ?? 0d),
            Convert.ToDecimal(row.sth_vergi ?? 0d),
            row.sth_vergi_pntr ?? 0,
            row.sth_giris_depo_no ?? 0,
            row.sth_cikis_depo_no ?? 0,
            row.sth_Guid.ToString(),
            null,
            null,
            row.sth_aciklama)).ToArray();

        return new ManavMalKabulVeEtiketCreateMicroGoodsReceiptResultDto(
            request.Date,
            documentSeries,
            documentOrderNo,
            documentSeries + "/" + documentOrderNo,
            request.SupplierCode,
            createUserNo,
            lines.Length,
            Round(lines.Sum(line => line.Quantity)),
            Round(lines.Sum(line => line.Amount)),
            Round(lines.Sum(line => line.TaxAmount)),
            updatedAcceptanceRecordCount,
            offlineTraceKey,
            lines);
    }

    private static bool MicroGoodsReceiptRowsMatch(
        IReadOnlyCollection<STOK_HAREKETLERI> expectedRows,
        IReadOnlyCollection<STOK_HAREKETLERI> actualRows)
    {
        if (expectedRows.Count != actualRows.Count)
        {
            return false;
        }

        var expected = expectedRows.OrderBy(row => row.sth_satirno).ToArray();
        var actual = actualRows.OrderBy(row => row.sth_satirno).ToArray();
        return expected.Zip(actual).All(pair =>
            string.Equals(pair.First.sth_stok_kod, pair.Second.sth_stok_kod, StringComparison.OrdinalIgnoreCase) &&
            Math.Abs(pair.First.sth_miktar.GetValueOrDefault() - pair.Second.sth_miktar.GetValueOrDefault()) < 0.0001d &&
            Math.Abs(pair.First.sth_tutar.GetValueOrDefault() - pair.Second.sth_tutar.GetValueOrDefault()) < 0.01d &&
            Math.Abs(pair.First.sth_vergi.GetValueOrDefault() - pair.Second.sth_vergi.GetValueOrDefault()) < 0.01d);
    }

    private static object CreateMicroGoodsReceiptMikroApiPayload(
        CARI_HESAP_HAREKETLERI customer,
        IReadOnlyCollection<STOK_HAREKETLERI> rows) =>
        new
        {
            evraklar = new[]
            {
                new
                {
                    cha_tarihi = FormatMikroDate(customer.cha_tarihi),
                    cha_tip = customer.cha_tip,
                    cha_cinsi = customer.cha_cinsi,
                    cha_normal_Iade = customer.cha_normal_Iade,
                    cha_evrak_tip = customer.cha_evrak_tip,
                    cha_evrakno_seri = customer.cha_evrakno_seri,
                    cha_evrakno_sira = customer.cha_evrakno_sira,
                    cha_cari_cins = customer.cha_cari_cins,
                    cha_kod = customer.cha_kod,
                    cha_ciro_cari_kodu = customer.cha_ciro_cari_kodu,
                    cha_d_cins = customer.cha_d_cins,
                    cha_d_kur = customer.cha_d_kur,
                    cha_d_kurtar = customer.cha_altd_kur,
                    cha_tpoz = customer.cha_tpoz,
                    cha_kasa_hizkod = customer.cha_kasa_hizkod,
                    cha_kasa_hizmet = customer.cha_kasa_hizmet,
                    cha_miktari = customer.cha_miktari,
                    cha_aratoplam = customer.cha_aratoplam,
                    cha_vergipntr = customer.cha_vergipntr,
                    cha_ft_iskonto1 = customer.cha_ft_iskonto1,
                    cha_isk_mas1 = customer.cha_isk_mas1,
                    cha_satici_kodu = customer.cha_satici_kodu,
                    cha_srmrkkodu = customer.cha_srmrkkodu,
                    cha_projekodu = customer.cha_projekodu,
                    cha_aciklama = customer.cha_aciklama,
                    cha_ebelge_turu = customer.cha_ebelge_turu,
                    cha_fatura_belge_turu = customer.cha_fatura_belge_turu,
                    detay = rows.Select(row => new
                    {
                        sth_tarih = FormatMikroDate(row.sth_tarih),
                        sth_tip = row.sth_tip,
                        sth_cins = row.sth_cins,
                        sth_normal_iade = row.sth_normal_iade,
                        sth_evraktip = row.sth_evraktip,
                        sth_evrakno_seri = row.sth_evrakno_seri,
                        sth_evrakno_sira = row.sth_evrakno_sira,
                        sth_stok_kod = row.sth_stok_kod,
                        sth_cari_cinsi = row.sth_cari_cinsi,
                        sth_cari_kodu = row.sth_cari_kodu,
                        sth_miktar = row.sth_miktar,
                        sth_birim_pntr = row.sth_birim_pntr,
                        sth_tutar = row.sth_tutar,
                        sth_vergi_pntr = row.sth_vergi_pntr,
                        sth_vergi = row.sth_vergi,
                        sth_vergisiz_fl = row.sth_vergisiz_fl,
                        sth_iskonto1 = row.sth_iskonto1,
                        sth_iskonto2 = row.sth_iskonto2,
                        sth_giris_depo_no = row.sth_giris_depo_no,
                        sth_cikis_depo_no = row.sth_cikis_depo_no,
                        sth_stok_srm_merkezi = row.sth_stok_srm_merkezi,
                        sth_cari_srm_merkezi = row.sth_cari_srm_merkezi,
                        sth_proje_kodu = row.sth_proje_kodu,
                        sth_aciklama = row.sth_aciklama,
                        sth_eticaret_kanal_kodu = row.sth_eticaret_kanal_kodu
                    }).ToArray()
                }
            }
        };

    private static string FormatMikroDate(DateTime? value) =>
        value.GetValueOrDefault().ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
}

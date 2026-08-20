using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.UyumsoftServisleri;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.ManavMalKabulVeEtiket;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.ManavMalKabulVeEtiket;

public sealed class ManavMalKabulVeEtiketService(
    AuthDbContext authDbContext,
    FurpaDbContext furpaDbContext,
    MikroDbContext mikroDbContext,
    MikroWriteDbContext mikroWriteDbContext,
    IUyumsoftConnectedQueryService uyumsoftConnectedQueryService) : IManavMalKabulVeEtiketService
{
    private const int DefaultTake = 20;
    private const int MaxTake = 100;
    private const string DefaultStockPrefix = "MNV";
    private const short MovementFileId = 16;
    private const short CustomerMovementFileId = 51;
    private const short DefaultMikroUserNo = 39;
    private const byte IncomingMovementType = 0;
    private const byte CustomerInvoiceMovementType = 1;
    private const byte GreenGrocerGoodsReceiptGenre = 16;
    private const byte GreenGrocerCustomerInvoiceGenre = 35;
    private const byte NormalMovement = 0;
    private const byte GreenGrocerGoodsReceiptDocumentType = 3;
    private const byte GreenGrocerCustomerInvoiceDocumentType = 0;
    private const byte GreenGrocerCustomerInvoiceElectronicDocumentType = 7;
    private const byte GreenGrocerCustomerInvoiceDocumentKind = 0;
    private const int GreenGrocerWarehouseNo = 56;
    private const int MainWarehouseNo = 1;
    private const int FirstDocumentOrderNo = 0;
    private static readonly DateTime MikroEmptyDate = new(1899, 12, 30);

    public async Task<IReadOnlyCollection<ManavMalKabulVeEtiketSupplierSuggestionDto>> SearchSuppliersAsync(
        ManavMalKabulVeEtiketReferenceSearchRequest request,
        CancellationToken cancellationToken)
    {
        var query = NormalizeOrNull(request.Query)
            ?? throw new ArgumentException("Supplier search query is required.", nameof(request.Query));
        if (query.Length < 2)
        {
            throw new ArgumentException("Supplier search query must be at least 2 characters.", nameof(request.Query));
        }

        var take = NormalizeTake(request.Take);
        var suppliers = new List<ManavMalKabulVeEtiketSupplierSuggestionDto>(take);
        await using var lease = await OpenConnectionAsync(mikroDbContext.Database.GetDbConnection(), cancellationToken);
        using var command = lease.Connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (@take)
                LTRIM(RTRIM(cari_kod)) AS SupplierCode,
                LTRIM(RTRIM(cari_unvan1)) AS SupplierName,
                LTRIM(RTRIM(ISNULL(cari_unvan2, ''))) AS SupplierTitle2,
                LTRIM(RTRIM(ISNULL(NULLIF(cari_VergiKimlikNo, ''), cari_vdaire_no))) AS SupplierTaxNo
            FROM dbo.CARI_HESAPLAR WITH (NOLOCK)
            WHERE cari_unvan1 IS NOT NULL
              AND LTRIM(RTRIM(cari_unvan1)) <> ''
              AND cari_unvan1 LIKE @queryLike
              AND cari_kod NOT LIKE '8888%'
              AND cari_kod NOT LIKE '1999%'
              AND cari_kod NOT LIKE '2012%'
              AND cari_kod NOT LIKE '4690%'
              AND cari_kod NOT LIKE '1998%'
              AND cari_kod NOT LIKE '2022%'
              AND cari_kod NOT LIKE '120.MY%'
            ORDER BY cari_unvan1;
            """;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 300;
        AddParameter(command, "@take", take, DbType.Int32);
        AddParameter(command, "@queryLike", query + "%", DbType.String);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            suppliers.Add(new ManavMalKabulVeEtiketSupplierSuggestionDto(
                ReadString(reader, "SupplierCode"),
                ReadString(reader, "SupplierName"),
                ReadString(reader, "SupplierTitle2"),
                ReadString(reader, "SupplierTaxNo")));
        }

        return suppliers;
    }

    public async Task<ManavMalKabulVeEtiketSupplierSuggestionDto> GetSupplierByNameAsync(
        string supplierName,
        CancellationToken cancellationToken)
    {
        var normalizedName = NormalizeOrNull(supplierName)
            ?? throw new ArgumentException("Supplier name is required.", nameof(supplierName));

        var matches = await SearchSuppliersAsync(
            new ManavMalKabulVeEtiketReferenceSearchRequest(normalizedName, MaxTake),
            cancellationToken);

        return matches.FirstOrDefault(item =>
                   string.Equals(item.SupplierName, normalizedName, StringComparison.OrdinalIgnoreCase))
               ?? matches.FirstOrDefault()
               ?? throw new KeyNotFoundException("Supplier was not found.");
    }

    public async Task<IReadOnlyCollection<ManavMalKabulVeEtiketStockSuggestionDto>> SearchStocksAsync(
        ManavMalKabulVeEtiketStockSearchRequest request,
        CancellationToken cancellationToken)
    {
        var query = NormalizeOrNull(request.Query);
        var prefix = NormalizeOrNull(request.Prefix) ?? DefaultStockPrefix;
        var take = NormalizeTake(request.Take);
        var stocks = new List<ManavMalKabulVeEtiketStockSuggestionDto>(take);
        await using var lease = await OpenConnectionAsync(mikroDbContext.Database.GetDbConnection(), cancellationToken);
        using var command = lease.Connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (@take)
                LTRIM(RTRIM(stock.sto_kod)) AS StockCode,
                LTRIM(RTRIM(stock.sto_isim)) AS StockName,
                ISNULL(barcode.bar_kodu, '') AS Barcode,
                LTRIM(RTRIM(ISNULL(stock.sto_birim1_ad, ''))) AS UnitName,
                LTRIM(RTRIM(ISNULL(stock.sto_model_kodu, ''))) AS ModelCode,
                ISNULL(stock.sto_toptan_vergi, 0) AS WholesaleTaxPointer
            FROM dbo.STOKLAR AS stock WITH (NOLOCK)
            OUTER APPLY
            (
                SELECT TOP (1) LTRIM(RTRIM(item.bar_kodu)) AS bar_kodu
                FROM dbo.BARKOD_TANIMLARI AS item WITH (NOLOCK)
                WHERE item.bar_stokkodu = stock.sto_kod
                  AND ISNULL(item.bar_iptal, 0) <> 1
                  AND item.bar_kodu IS NOT NULL
                  AND LTRIM(RTRIM(item.bar_kodu)) <> ''
                ORDER BY ISNULL(item.bar_master, 0) DESC,
                         ISNULL(item.bar_birimpntr, 0),
                         item.bar_create_date DESC
            ) AS barcode
            WHERE stock.sto_isim IS NOT NULL
              AND LTRIM(RTRIM(stock.sto_isim)) <> ''
              AND stock.sto_isim LIKE @prefixLike
              AND
              (
                  @queryLike IS NULL
                  OR stock.sto_isim LIKE @queryLike
                  OR stock.sto_kod LIKE @queryLike
                  OR barcode.bar_kodu LIKE @queryLike
              )
            ORDER BY stock.sto_isim;
            """;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 300;
        AddParameter(command, "@take", take, DbType.Int32);
        AddParameter(command, "@prefixLike", prefix + "%", DbType.String);
        AddParameter(command, "@queryLike", BuildContainsLike(query), DbType.String);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            stocks.Add(new ManavMalKabulVeEtiketStockSuggestionDto(
                ReadString(reader, "StockCode"),
                ReadString(reader, "StockName"),
                ReadString(reader, "Barcode"),
                ReadString(reader, "UnitName"),
                ReadString(reader, "ModelCode"),
                ReadInt(reader, "WholesaleTaxPointer")));
        }

        return stocks;
    }

    public async Task<ManavMalKabulVeEtiketStockSuggestionDto> GetStockByCodeAsync(
        string stockCode,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeOrNull(stockCode)
            ?? throw new ArgumentException("Stock code is required.", nameof(stockCode));

        var stock = await FindStockAsync("stock.sto_kod = @value", normalizedCode, cancellationToken);
        return stock ?? throw new KeyNotFoundException("Stock was not found.");
    }

    public async Task<ManavMalKabulVeEtiketStockSuggestionDto> GetStockByNameAsync(
        string stockName,
        CancellationToken cancellationToken)
    {
        var normalizedName = NormalizeOrNull(stockName)
            ?? throw new ArgumentException("Stock name is required.", nameof(stockName));

        var stock = await FindStockAsync("stock.sto_isim = @value", normalizedName, cancellationToken);
        return stock ?? throw new KeyNotFoundException("Stock was not found.");
    }

    public async Task<IReadOnlyCollection<ManavMalKabulVeEtiketIncomingInvoiceDto>> ListIncomingInvoicesAsync(
        ManavMalKabulVeEtiketIncomingInvoiceQuery request,
        CancellationToken cancellationToken)
    {
        var startDate = request.StartDate.Date;
        var endDate = request.EndDate.Date;
        if (endDate < startDate)
        {
            throw new ArgumentException("End date can not be earlier than start date.", nameof(request.EndDate));
        }

        var supplierCode = NormalizeOrNull(request.SupplierCode);
        var supplierFilter = supplierCode is null
            ? null
            : await LoadSupplierFilterAsync(supplierCode, cancellationToken);
        var searchText = NormalizeOrNull(request.SearchText);
        var take = Math.Clamp(request.Take <= 0 ? 100 : request.Take, 1, 500);
        var endExclusive = endDate.AddDays(1);

        var query = authDbContext.UyumsoftInboxInvoices
            .AsNoTracking()
            .Where(invoice =>
                (invoice.InvoiceDate ?? invoice.CreateDate) >= startDate &&
                (invoice.InvoiceDate ?? invoice.CreateDate) < endExclusive);

        if (!request.IncludeArchived)
        {
            query = query.Where(invoice => !invoice.IsArchived);
        }

        if (supplierFilter is not null)
        {
            query = query.Where(invoice =>
                (!string.IsNullOrEmpty(supplierFilter.TaxNo) && invoice.CustomerTcknVkn == supplierFilter.TaxNo) ||
                (!string.IsNullOrEmpty(supplierFilter.SupplierName) && invoice.CustomerTitle.Contains(supplierFilter.SupplierName)));
        }

        if (searchText is not null)
        {
            query = query.Where(invoice =>
                invoice.InvoiceId.Contains(searchText) ||
                invoice.DocumentId.Contains(searchText) ||
                invoice.CustomerTitle.Contains(searchText) ||
                invoice.CustomerTcknVkn.Contains(searchText) ||
                invoice.DespatchId.Contains(searchText) ||
                invoice.OrderDocumentId.Contains(searchText));
        }

        var invoices = await query
            .OrderByDescending(invoice => invoice.InvoiceDate ?? invoice.CreateDate)
            .ThenByDescending(invoice => invoice.LastSynchronizedAtUtc)
            .Take(take)
            .ToArrayAsync(cancellationToken);

        var supplierMatches = await LoadSupplierMatchesByTaxNoAsync(
            invoices
                .Select(invoice => NormalizeOrNull(invoice.CustomerTcknVkn))
                .Where(taxNo => taxNo is not null)
                .Select(taxNo => taxNo!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            cancellationToken);

        return invoices
            .Select(invoice => MapIncomingInvoice(invoice, supplierMatches, supplierFilter))
            .ToArray();
    }

    public async Task<ManavMalKabulVeEtiketInvoiceDetailDto> GetIncomingInvoiceDetailAsync(
        ManavMalKabulVeEtiketInvoiceDetailQuery request,
        CancellationToken cancellationToken)
    {
        var invoiceLookupId = NormalizeOrNull(request.InvoiceLookupId)
            ?? throw new ArgumentException("Invoice lookup id is required.", nameof(request.InvoiceLookupId));
        var supplierCode = NormalizeOrNull(request.SupplierCode);
        var supplierFilter = supplierCode is null
            ? null
            : await LoadSupplierFilterAsync(supplierCode, cancellationToken);
        var cachedInvoice = await FindCachedIncomingInvoiceAsync(invoiceLookupId, cancellationToken);
        var lookupIds = BuildIncomingInvoiceLookupIds(invoiceLookupId, cachedInvoice);
        var (resolvedLookupId, invoiceXml) = await FetchIncomingInvoiceXmlAsync(lookupIds, cancellationToken);
        var invoiceDocument = XDocument.Parse(invoiceXml, LoadOptions.PreserveWhitespace);
        var invoice = FindXmlRoot(invoiceDocument, "Invoice")
            ?? throw new InvalidOperationException("Uyumsoft invoice XML does not contain an Invoice root.");

        var invoiceId = ReadPath(invoice, "UUID")
            ?? cachedInvoice?.InvoiceId
            ?? resolvedLookupId;
        var documentId = ReadPath(invoice, "ID")
            ?? cachedInvoice?.DocumentId
            ?? string.Empty;
        var supplierParty = FindPath(invoice, "AccountingSupplierParty", "Party");
        var supplierTitle = ReadPath(supplierParty, "PartyName", "Name")
            ?? ReadPath(supplierParty, "PartyLegalEntity", "RegistrationName")
            ?? cachedInvoice?.CustomerTitle
            ?? string.Empty;
        var supplierTaxNo = ReadFirstPath(supplierParty,
                ["PartyIdentification", "ID"],
                ["PartyTaxScheme", "CompanyID"])
            ?? cachedInvoice?.CustomerTcknVkn
            ?? string.Empty;
        var issueDate = ReadDate(ReadPath(invoice, "IssueDate")) ?? cachedInvoice?.InvoiceDate;
        var invoiceTypeCode = ReadPath(invoice, "InvoiceTypeCode") ?? cachedInvoice?.InvoiceType ?? string.Empty;
        var currencyCode = ReadPath(invoice, "DocumentCurrencyCode") ?? cachedInvoice?.DocumentCurrencyCode ?? string.Empty;
        var taxExclusiveAmount = ReadDecimal(FindPath(invoice, "LegalMonetaryTotal", "TaxExclusiveAmount"))
            ?? cachedInvoice?.TaxExclusiveAmount
            ?? 0m;
        var taxTotal = ReadDecimal(FindPath(invoice, "TaxTotal", "TaxAmount"))
            ?? cachedInvoice?.TaxTotal
            ?? 0m;
        var payableAmount = ReadDecimal(FindPath(invoice, "LegalMonetaryTotal", "PayableAmount"))
            ?? cachedInvoice?.InvoiceTotal
            ?? 0m;
        var despatchId = ReadPath(invoice, "DespatchDocumentReference", "ID")
            ?? NormalizeOrNull(cachedInvoice?.DespatchId);

        var supplierMatches = await LoadSupplierMatchesByTaxNoAsync(
            NormalizeOrNull(supplierTaxNo) is { } taxNo ? [taxNo] : [],
            cancellationToken);
        var matchedSupplier = NormalizeOrNull(supplierTaxNo) is { } normalizedTaxNo &&
                              supplierMatches.TryGetValue(normalizedTaxNo, out var match)
            ? match
            : supplierFilter;

        var packagingSummary = ReadInvoicePackagingSummary(invoice);
        var lines = await ReadInvoiceLinesAsync(invoice, cancellationToken);
        var warnings = new List<string>();
        if (cachedInvoice is null)
        {
            warnings.Add("Fatura cache kaydinda bulunamadi; detay Uyumsoft canli cevabindan cozuldu.");
        }

        if (matchedSupplier is null)
        {
            warnings.Add("Fatura tedarikcisi Mikro cari kartiyla otomatik eslesmedi; UI tedarikciyi secmelidir.");
        }

        if (lines.Count == 0)
        {
            warnings.Add("Fatura XML icinde okunabilir kalem bulunamadi.");
        }

        return new ManavMalKabulVeEtiketInvoiceDetailDto(
            resolvedLookupId,
            invoiceId,
            documentId,
            supplierTitle,
            supplierTaxNo,
            issueDate,
            invoiceTypeCode,
            currencyCode,
            Round(taxExclusiveAmount),
            Round(taxTotal),
            Round(payableAmount),
            packagingSummary.CaseCount,
            RoundOrNull(packagingSummary.GrossWithTareQuantity),
            RoundOrNull(packagingSummary.TareQuantity),
            RoundOrNull(packagingSummary.NetQuantity),
            despatchId,
            matchedSupplier?.SupplierCode,
            matchedSupplier?.SupplierName,
            matchedSupplier is not null && lines.Any(line => line.CanCreateAcceptance),
            lines,
            warnings);
    }

    public ManavMalKabulVeEtiketCalculationDto Calculate(ManavMalKabulVeEtiketCalculationRequest request) =>
        ManavMalKabulVeEtiketCalculator.Calculate(request);

    public async Task<IReadOnlyCollection<ManavMalKabulVeEtiketAcceptanceRecordDto>> ListAcceptanceRecordsAsync(
        DateTime date,
        CancellationToken cancellationToken)
    {
        var records = new List<ManavMalKabulVeEtiketAcceptanceRecordDto>();
        await using var lease = await OpenConnectionAsync(furpaDbContext.Database.GetDbConnection(), cancellationToken);
        using var command = lease.Connection.CreateCommand();
        command.CommandText = AcceptanceRecordSelectSql + """
            WHERE CAST(Olusturma_Tarihi AS date) = @date
            ORDER BY ID DESC;
            """;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 300;
        AddParameter(command, "@date", date.Date, DbType.Date);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(MapAcceptanceRecord(reader));
        }

        return records;
    }

    public async Task<ManavMalKabulVeEtiketAcceptanceRecordDto> GetAcceptanceRecordAsync(
        int id,
        CancellationToken cancellationToken)
    {
        ValidateRecordId(id);
        return await FindAcceptanceRecordAsync(id, cancellationToken)
               ?? throw new KeyNotFoundException("Acceptance record was not found.");
    }

    public async Task<ManavMalKabulVeEtiketAcceptanceRecordDto> CreateAcceptanceRecordAsync(
        SaveManavMalKabulVeEtiketAcceptanceRecordRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeSaveRequest(request);
        var calculation = Calculate(new ManavMalKabulVeEtiketCalculationRequest(
            normalized.GrossWeight,
            normalized.CaseTare,
            normalized.CaseCount,
            normalized.PalletTare,
            normalized.StockBarcode));

        await using var lease = await OpenConnectionAsync(furpaDbContext.Database.GetDbConnection(), cancellationToken);
        using var command = lease.Connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.Manav_Depo_Mal_Kabul_Etiket
            (
                Olusturma_Tarihi,
                Degistirme_Tarihi,
                Cari_Unvan,
                Evrak_Seri,
                Evrak_Sira,
                Stok_Kod,
                Stok_Ismi,
                Stok_Barkod,
                Toplam_Miktar,
                Kasa_Adet_Darasi,
                Kasa_Sayisi,
                Kasa_Toplam_Dara,
                Palet_Darasi,
                Kasa_Ortalama_Miktar,
                [Alınan_Net_Miktar],
                Teslim_Alan,
                Mikro_Aktarildi,
                Cari_Kod,
                KasaTipi
            )
            VALUES
            (
                GETDATE(),
                GETDATE(),
                @SupplierName,
                @DocumentSeries,
                @DocumentNo,
                @StockCode,
                @StockName,
                @StockBarcode,
                @GrossWeight,
                @CaseTare,
                @CaseCount,
                @CaseTotalTare,
                @PalletTare,
                @AverageCaseWeight,
                @NetReceivedWeight,
                @ReceivedBy,
                0,
                @SupplierCode,
                @CaseType
            );

            SELECT CAST(SCOPE_IDENTITY() AS int);
            """;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 300;
        AddSaveParameters(command, normalized, calculation);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        var id = result is null || result is DBNull ? 0 : Convert.ToInt32(result);
        return await GetAcceptanceRecordAsync(id, cancellationToken);
    }

    public async Task<ManavMalKabulVeEtiketAcceptanceRecordDto> UpdateAcceptanceRecordAsync(
        int id,
        SaveManavMalKabulVeEtiketAcceptanceRecordRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRecordId(id);
        var existing = await GetAcceptanceRecordAsync(id, cancellationToken);
        if (existing.MicroTransferred)
        {
            throw new InvalidOperationException("Micro transferred acceptance records cannot be updated.");
        }

        var normalized = NormalizeSaveRequest(request);
        var calculation = Calculate(new ManavMalKabulVeEtiketCalculationRequest(
            normalized.GrossWeight,
            normalized.CaseTare,
            normalized.CaseCount,
            normalized.PalletTare,
            normalized.StockBarcode));

        await using var lease = await OpenConnectionAsync(furpaDbContext.Database.GetDbConnection(), cancellationToken);
        using var command = lease.Connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.Manav_Depo_Mal_Kabul_Etiket
            SET
                Cari_Unvan = @SupplierName,
                Stok_Ismi = @StockName,
                Stok_Kod = @StockCode,
                Stok_Barkod = @StockBarcode,
                Toplam_Miktar = @GrossWeight,
                Kasa_Adet_Darasi = @CaseTare,
                Kasa_Sayisi = @CaseCount,
                Palet_Darasi = @PalletTare,
                Teslim_Alan = @ReceivedBy,
                Degistirme_Tarihi = GETDATE(),
                Evrak_Seri = @DocumentSeries,
                Evrak_Sira = @DocumentNo,
                Kasa_Toplam_Dara = @CaseTotalTare,
                Kasa_Ortalama_Miktar = @AverageCaseWeight,
                [Alınan_Net_Miktar] = @NetReceivedWeight,
                Cari_Kod = @SupplierCode,
                KasaTipi = @CaseType
            WHERE ID = @Id
              AND ISNULL(Mikro_Aktarildi, 0) = 0;
            """;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 300;
        AddParameter(command, "@Id", id, DbType.Int32);
        AddSaveParameters(command, normalized, calculation);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
        {
            throw new KeyNotFoundException("Acceptance record was not found.");
        }

        return await GetAcceptanceRecordAsync(id, cancellationToken);
    }

    public async Task DeleteAcceptanceRecordAsync(
        int id,
        CancellationToken cancellationToken)
    {
        ValidateRecordId(id);
        var existing = await GetAcceptanceRecordAsync(id, cancellationToken);
        if (existing.MicroTransferred)
        {
            throw new InvalidOperationException("Micro transferred acceptance records cannot be deleted.");
        }

        await using var lease = await OpenConnectionAsync(furpaDbContext.Database.GetDbConnection(), cancellationToken);
        using var command = lease.Connection.CreateCommand();
        command.CommandText = """
            DELETE dbo.Manav_Depo_Mal_Kabul_Etiket
            WHERE ID = @Id
              AND ISNULL(Mikro_Aktarildi, 0) = 0;
            """;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 300;
        AddParameter(command, "@Id", id, DbType.Int32);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
        {
            throw new KeyNotFoundException("Acceptance record was not found.");
        }
    }

    public async Task<ManavMalKabulVeEtiketLabelDto> GetLabelAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var record = await GetAcceptanceRecordAsync(id, cancellationToken);
        return ToLabel(record);
    }

    public ManavMalKabulVeEtiketLabelDto PreviewLabel(SaveManavMalKabulVeEtiketAcceptanceRecordRequest request)
    {
        var normalized = NormalizeSaveRequest(request);
        var calculation = Calculate(new ManavMalKabulVeEtiketCalculationRequest(
            normalized.GrossWeight,
            normalized.CaseTare,
            normalized.CaseCount,
            normalized.PalletTare,
            normalized.StockBarcode));

        var labelBarcode = calculation.LabelBarcode
            ?? throw new ArgumentException("Stock barcode is required for label preview.", nameof(request.StockBarcode));
        var labelBarcodeRaw = calculation.LabelBarcodeRaw ?? labelBarcode;

        return new ManavMalKabulVeEtiketLabelDto(
            null,
            normalized.StockCode,
            normalized.StockName,
            normalized.StockBarcode,
            normalized.SupplierName,
            calculation.AverageCaseWeight,
            DateTime.Today,
            normalized.CaseCount.GetValueOrDefault(1),
            labelBarcodeRaw,
            labelBarcode,
            calculation.BarcodeSymbology,
            normalized.CaseTare,
            normalized.CaseType);
    }

    public async Task<IReadOnlyCollection<ManavMalKabulVeEtiketReceivedProductReportItemDto>> GetReceivedProductsReportAsync(
        DateTime date,
        CancellationToken cancellationToken)
    {
        var receivedGroups = await ReadReceivedProductReportGroupsAsync(date.Date, cancellationToken);
        var microGroups = await ReadMicroReceiptReportGroupsAsync(date.Date, cancellationToken);

        return receivedGroups
            .Select(group =>
            {
                var microGroup = microGroups.GetValueOrDefault(BuildInvoiceKey(group.SupplierCode, group.StockCode)) ??
                                 microGroups.GetValueOrDefault(BuildInvoiceKey(group.SupplierName, group.StockCode));
                var invoiceQuantity = microGroup?.MicroQuantity ?? 0m;
                var difference = Round(invoiceQuantity - group.NetReceivedWeight);
                var status = ResolveComparisonStatus(group.LabelRowCount, microGroup?.MicroRowCount ?? 0, difference);
                return new ManavMalKabulVeEtiketReceivedProductReportItemDto(
                    group.SupplierName,
                    group.StockCode,
                    group.Barcode,
                    group.StockName,
                    group.GrossWeight,
                    group.CaseTotalTare,
                    group.PalletTare,
                    group.CaseCount,
                    group.NetReceivedWeight,
                    invoiceQuantity,
                    difference,
                    group.SupplierCode,
                    group.LabelRowCount,
                    group.DocumentSeries,
                    group.DocumentNo,
                    group.SeriesAndNumber,
                    microGroup?.MicroRowCount ?? 0,
                    microGroup?.MicroAmount ?? 0m,
                    microGroup?.MicroDocument,
                    status,
                    microGroup?.UnitName);
            })
            .OrderByDescending(item => Math.Abs(item.InvoiceDifference))
            .ThenBy(item => item.SupplierName)
            .ThenBy(item => item.StockName)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<ManavMalKabulVeEtiketDepotStockReportItemDto>> GetDepotStockReportAsync(
        int warehouseNo,
        DateTime date,
        CancellationToken cancellationToken)
    {
        if (warehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(warehouseNo));
        }

        var rows = new List<ManavMalKabulVeEtiketDepotStockReportItemDto>();
        await using var lease = await OpenConnectionAsync(mikroDbContext.Database.GetDbConnection(), cancellationToken);
        using var command = lease.Connection.CreateCommand();
        command.CommandText = """
            WITH stock_rows AS
            (
                SELECT
                    movement.sth_stok_kod AS StockCode,
                    stock.sto_isim AS StockName,
                    MAX(ISNULL(barcode.bar_kodu, '')) AS Barcode,
                    LTRIM(RTRIM(ISNULL(stock.sto_birim1_ad, ''))) AS UnitName,
                    LTRIM(RTRIM(ISNULL(stock.sto_model_kodu, ''))) AS ModelCode,
                    ROUND(SUM(
                        CASE
                            WHEN (movement.sth_tip = 0) OR ((movement.sth_tip = 2) AND (movement.sth_giris_depo_no = @warehouseNo))
                                THEN movement.sth_miktar
                            WHEN (movement.sth_tip = 1) OR ((movement.sth_tip = 2) AND (movement.sth_cikis_depo_no = @warehouseNo))
                                THEN (-1) * movement.sth_miktar
                            ELSE 0
                        END
                    ), 2) AS CurrentStock,
                    CONCAT(person.cari_per_adi, ' ', person.cari_per_soyadi) AS Responsible,
                    ROUND(ISNULL((
                        SELECT TOP 1 price.fid_yenifiy_tutar
                        FROM dbo.STOK_FIYAT_DEGISIKLIKLERI AS price WITH (NOLOCK)
                        WHERE price.fid_stok_kod = movement.sth_stok_kod
                          AND price.fid_depo_no = 0
                          AND price.fid_yapildi_fl = 1
                        ORDER BY price.fid_tarih DESC
                    ), 0), 2) AS SalesPrice,
                    CASE ROUND(ISNULL((
                        SELECT TOP 1 purchase.sas_net_alis_kdvli
                        FROM dbo.SATINALMA_SARTLARI AS purchase WITH (NOLOCK)
                        WHERE movement.sth_stok_kod = purchase.sas_stok_kod
                        ORDER BY purchase.sas_create_date DESC
                    ), 0), 2)
                        WHEN 0 THEN 0.1
                        ELSE ROUND(ISNULL((
                            SELECT TOP 1 purchase.sas_net_alis_kdvli
                            FROM dbo.SATINALMA_SARTLARI AS purchase WITH (NOLOCK)
                            WHERE movement.sth_stok_kod = purchase.sas_stok_kod
                            ORDER BY purchase.sas_create_date DESC
                        ), 0), 2)
                    END AS PurchasePriceWithVat
                FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
                INNER JOIN dbo.STOKLAR AS stock WITH (NOLOCK)
                    ON stock.sto_kod = movement.sth_stok_kod
                INNER JOIN dbo.CARI_PERSONEL_TANIMLARI AS person WITH (NOLOCK)
                    ON stock.sto_urun_sorkod = person.cari_per_kod
                OUTER APPLY
                (
                    SELECT TOP (1) LTRIM(RTRIM(item.bar_kodu)) AS bar_kodu
                    FROM dbo.BARKOD_TANIMLARI AS item WITH (NOLOCK)
                    WHERE item.bar_stokkodu = movement.sth_stok_kod
                      AND ISNULL(item.bar_iptal, 0) <> 1
                      AND item.bar_kodu IS NOT NULL
                      AND LTRIM(RTRIM(item.bar_kodu)) <> ''
                    ORDER BY ISNULL(item.bar_master, 0) DESC,
                             ISNULL(item.bar_birimpntr, 0),
                             item.bar_create_date DESC
                ) AS barcode
                WHERE movement.sth_tarih <= @date
                  AND
                  (
                    ((movement.sth_tip = 0) AND movement.sth_giris_depo_no = @warehouseNo)
                    OR ((movement.sth_tip = 1) AND movement.sth_cikis_depo_no = @warehouseNo)
                    OR
                    (
                        movement.sth_tip = 2
                        AND movement.sth_giris_depo_no <> movement.sth_cikis_depo_no
                        AND (movement.sth_giris_depo_no = @warehouseNo OR movement.sth_cikis_depo_no = @warehouseNo)
                    )
                  )
                  AND NOT (movement.sth_cins IN (9, 15))
                GROUP BY
                    movement.sth_stok_kod,
                    stock.sto_isim,
                    stock.sto_birim1_ad,
                    stock.sto_model_kodu,
                    person.cari_per_adi,
                    person.cari_per_soyadi
                HAVING ROUND(SUM(
                    CASE
                        WHEN (movement.sth_tip = 0) OR ((movement.sth_tip = 2) AND (movement.sth_giris_depo_no = @warehouseNo))
                            THEN movement.sth_miktar
                        WHEN (movement.sth_tip = 1) OR ((movement.sth_tip = 2) AND (movement.sth_cikis_depo_no = @warehouseNo))
                            THEN (-1) * movement.sth_miktar
                        ELSE 0
                    END
                ), 2) <> 0
            )
            SELECT
                StockCode,
                StockName,
                Barcode,
                UnitName,
                ModelCode,
                Responsible,
                CurrentStock,
                PurchasePriceWithVat,
                SalesPrice
            FROM stock_rows
            WHERE StockName NOT LIKE '%PALET%'
            ORDER BY StockName;
            """;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 300;
        AddParameter(command, "@warehouseNo", warehouseNo, DbType.Int32);
        AddParameter(command, "@date", date.Date, DbType.Date);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new ManavMalKabulVeEtiketDepotStockReportItemDto(
                ReadString(reader, "StockCode"),
                ReadString(reader, "StockName"),
                ReadString(reader, "Responsible"),
                ReadDecimal(reader, "CurrentStock"),
                ReadDecimal(reader, "PurchasePriceWithVat"),
                ReadDecimal(reader, "SalesPrice"),
                ReadString(reader, "Barcode"),
                ReadString(reader, "UnitName"),
                ReadString(reader, "ModelCode")));
        }

        return rows;
    }

    public async Task<IReadOnlyCollection<ManavMalKabulVeEtiketMicroGoodsReceiptDocumentDto>> GetMicroGoodsReceiptsAsync(
        ManavMalKabulVeEtiketMicroGoodsReceiptQuery request,
        CancellationToken cancellationToken)
    {
        var date = request.Date.Date;
        var supplierCode = NormalizeOrNull(request.SupplierCode);
        if (date == default)
        {
            throw new ArgumentException("Goods receipt date is required.", nameof(request.Date));
        }

        var rows = new List<MicroGoodsReceiptFlatRow>();
        await using var lease = await OpenConnectionAsync(mikroDbContext.Database.GetDbConnection(), cancellationToken);
        using var command = lease.Connection.CreateCommand();
        command.CommandText = """
            SELECT
                CAST(movement.sth_tarih AS date) AS ReceiptDate,
                LTRIM(RTRIM(movement.sth_evrakno_seri)) AS DocumentSeries,
                movement.sth_evrakno_sira AS DocumentOrderNo,
                movement.sth_satirno AS LineNo,
                CONVERT(varchar(36), movement.sth_Guid) AS MovementGuid,
                ISNULL(movement.sth_belge_no, '') AS DocumentNo,
                ISNULL(CONVERT(varchar(36), movement.sth_fat_uid), '') AS InvoiceGuid,
                ISNULL(movement.sth_eticaret_kanal_kodu, '') AS OfflineTraceKey,
                LTRIM(RTRIM(movement.sth_cari_kodu)) AS SupplierCode,
                LTRIM(RTRIM(ISNULL(customer.cari_unvan1, ''))) AS SupplierName,
                LTRIM(RTRIM(movement.sth_stok_kod)) AS StockCode,
                LTRIM(RTRIM(stock.sto_isim)) AS StockName,
                ISNULL(barcode.bar_kodu, '') AS Barcode,
                LTRIM(RTRIM(ISNULL(stock.sto_birim1_ad, ''))) AS UnitName,
                ROUND(ISNULL(movement.sth_miktar, 0), 4) AS Quantity,
                ROUND(ISNULL(movement.sth_tutar, 0), 4) AS Amount,
                ROUND(ISNULL(movement.sth_vergi, 0), 4) AS TaxAmount,
                movement.sth_vergi_pntr AS TaxPointer,
                movement.sth_giris_depo_no AS InWarehouseNo,
                movement.sth_cikis_depo_no AS OutWarehouseNo,
                movement.sth_create_user AS CreateUserNo,
                movement.sth_create_date AS CreatedAt,
                ISNULL(movement.sth_aciklama, '') AS Description
            FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
            INNER JOIN dbo.STOKLAR AS stock WITH (NOLOCK)
                ON stock.sto_kod = movement.sth_stok_kod
            LEFT JOIN dbo.CARI_HESAPLAR AS customer WITH (NOLOCK)
                ON customer.cari_kod = movement.sth_cari_kodu
            OUTER APPLY
            (
                SELECT TOP (1) LTRIM(RTRIM(item.bar_kodu)) AS bar_kodu
                FROM dbo.BARKOD_TANIMLARI AS item WITH (NOLOCK)
                WHERE item.bar_stokkodu = movement.sth_stok_kod
                  AND ISNULL(item.bar_iptal, 0) <> 1
                  AND item.bar_kodu IS NOT NULL
                  AND LTRIM(RTRIM(item.bar_kodu)) <> ''
                ORDER BY ISNULL(item.bar_master, 0) DESC,
                         ISNULL(item.bar_birimpntr, 0),
                         item.bar_create_date DESC
            ) AS barcode
            WHERE CAST(movement.sth_tarih AS date) = @date
              AND (@supplierCode IS NULL OR LTRIM(RTRIM(movement.sth_cari_kodu)) = @supplierCode)
              AND movement.sth_tip = 0
              AND movement.sth_cins = 16
              AND movement.sth_evraktip = 3
              AND movement.sth_normal_iade = 0
              AND movement.sth_giris_depo_no = 56
              AND movement.sth_cikis_depo_no = 1
              AND stock.sto_isim LIKE 'MNV%'
            ORDER BY
                movement.sth_create_date,
                movement.sth_evrakno_seri,
                movement.sth_evrakno_sira,
                movement.sth_satirno;
            """;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 300;
        AddParameter(command, "@date", date, DbType.Date);
        AddParameter(command, "@supplierCode", supplierCode, DbType.String);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new MicroGoodsReceiptFlatRow(
                ReadDateTime(reader, "ReceiptDate"),
                ReadString(reader, "DocumentSeries"),
                ReadInt(reader, "DocumentOrderNo"),
                ReadInt(reader, "LineNo"),
                ReadString(reader, "MovementGuid"),
                ReadString(reader, "DocumentNo"),
                ReadString(reader, "InvoiceGuid"),
                ReadString(reader, "OfflineTraceKey"),
                ReadString(reader, "SupplierCode"),
                ReadString(reader, "SupplierName"),
                ReadString(reader, "StockCode"),
                ReadString(reader, "StockName"),
                ReadString(reader, "Barcode"),
                ReadString(reader, "UnitName"),
                ReadDecimal(reader, "Quantity"),
                ReadDecimal(reader, "Amount"),
                ReadDecimal(reader, "TaxAmount"),
                ReadInt(reader, "TaxPointer"),
                ReadInt(reader, "InWarehouseNo"),
                ReadInt(reader, "OutWarehouseNo"),
                ReadInt(reader, "CreateUserNo"),
                ReadDateTime(reader, "CreatedAt"),
                ReadString(reader, "Description")));
        }

        return rows
            .GroupBy(row => new
            {
                row.Date,
                row.DocumentSeries,
                row.DocumentOrderNo,
                row.SupplierCode,
                row.SupplierName,
                row.CreateUserNo,
                row.DocumentNo,
                row.InvoiceGuid,
                row.OfflineTraceKey
            })
            .Select(group =>
            {
                var lines = group
                    .Select(row => new ManavMalKabulVeEtiketMicroGoodsReceiptLineDto(
                        row.LineNo,
                        row.StockCode,
                        row.StockName,
                        row.Quantity,
                        row.Quantity == 0 ? 0 : Round(row.Amount / row.Quantity),
                        row.Amount,
                        row.TaxAmount,
                        row.TaxPointer,
                        row.InWarehouseNo,
                        row.OutWarehouseNo,
                        row.MovementGuid,
                        row.Barcode,
                        row.UnitName,
                        row.Description))
                    .ToArray();

                return new ManavMalKabulVeEtiketMicroGoodsReceiptDocumentDto(
                    group.Key.Date,
                    group.Key.DocumentSeries,
                    group.Key.DocumentOrderNo,
                    group.Key.DocumentSeries + "/" + group.Key.DocumentOrderNo,
                    group.Key.SupplierCode,
                    group.Key.SupplierName,
                    group.Key.CreateUserNo,
                    lines.Length,
                    Round(lines.Sum(line => line.Quantity)),
                    Round(lines.Sum(line => line.Amount)),
                    Round(lines.Sum(line => line.TaxAmount)),
                    group.Min(row => row.CreatedAt),
                    group.Max(row => row.CreatedAt),
                    lines,
                    group.Key.DocumentNo,
                    group.Key.InvoiceGuid,
                    group.Key.OfflineTraceKey);
            })
            .OrderBy(item => item.FirstCreatedAt)
            .ThenBy(item => item.DocumentSeries)
            .ThenBy(item => item.DocumentOrderNo)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<ManavMalKabulVeEtiketGoodsReceiptComparisonItemDto>> CompareGoodsReceiptsAsync(
        ManavMalKabulVeEtiketMicroGoodsReceiptQuery request,
        CancellationToken cancellationToken)
    {
        var date = request.Date.Date;
        var supplierCode = NormalizeOrNull(request.SupplierCode);
        if (date == default)
        {
            throw new ArgumentException("Goods receipt date is required.", nameof(request.Date));
        }

        var rows = new List<ManavMalKabulVeEtiketGoodsReceiptComparisonItemDto>();
        await using var lease = await OpenConnectionAsync(mikroDbContext.Database.GetDbConnection(), cancellationToken);
        using var command = lease.Connection.CreateCommand();
        command.CommandText = """
            WITH label_groups AS
            (
                SELECT
                    CAST(label.Olusturma_Tarihi AS date) AS ReceiptDate,
                    LTRIM(RTRIM(ISNULL(label.Cari_Kod, ''))) AS SupplierCode,
                    MAX(LTRIM(RTRIM(ISNULL(label.Cari_Unvan, '')))) AS SupplierName,
                    LTRIM(RTRIM(label.Stok_Kod)) AS StockCode,
                    MAX(LTRIM(RTRIM(label.Stok_Ismi))) AS StockName,
                    COUNT(*) AS LabelRowCount,
                    ROUND(ISNULL(SUM(label.[Alınan_Net_Miktar]), 0), 4) AS LabelNetWeight
                FROM Furpa.dbo.Manav_Depo_Mal_Kabul_Etiket AS label WITH (NOLOCK)
                WHERE CAST(label.Olusturma_Tarihi AS date) = @date
                  AND (@supplierCode IS NULL OR LTRIM(RTRIM(ISNULL(label.Cari_Kod, ''))) = @supplierCode)
                  AND label.Stok_Ismi NOT LIKE '%PALET%'
                GROUP BY
                    CAST(label.Olusturma_Tarihi AS date),
                    LTRIM(RTRIM(ISNULL(label.Cari_Kod, ''))),
                    LTRIM(RTRIM(label.Stok_Kod))
            ),
            micro_groups AS
            (
                SELECT
                    CAST(movement.sth_tarih AS date) AS ReceiptDate,
                    LTRIM(RTRIM(movement.sth_cari_kodu)) AS SupplierCode,
                    MAX(LTRIM(RTRIM(ISNULL(customer.cari_unvan1, '')))) AS SupplierName,
                    LTRIM(RTRIM(movement.sth_stok_kod)) AS StockCode,
                    MAX(LTRIM(RTRIM(stock.sto_isim))) AS StockName,
                    COUNT(*) AS MicroRowCount,
                    ROUND(ISNULL(SUM(movement.sth_miktar), 0), 4) AS MicroQuantity,
                    ROUND(ISNULL(SUM(movement.sth_tutar), 0), 4) AS MicroAmount,
                    STRING_AGG(CONCAT(LTRIM(RTRIM(movement.sth_evrakno_seri)), '/', CONVERT(varchar(20), movement.sth_evrakno_sira)), ', ') AS MicroDocument
                FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
                INNER JOIN dbo.STOKLAR AS stock WITH (NOLOCK)
                    ON stock.sto_kod = movement.sth_stok_kod
                LEFT JOIN dbo.CARI_HESAPLAR AS customer WITH (NOLOCK)
                    ON customer.cari_kod = movement.sth_cari_kodu
                WHERE CAST(movement.sth_tarih AS date) = @date
                  AND (@supplierCode IS NULL OR LTRIM(RTRIM(movement.sth_cari_kodu)) = @supplierCode)
                  AND movement.sth_tip = 0
                  AND movement.sth_cins = 16
                  AND movement.sth_evraktip = 3
                  AND movement.sth_normal_iade = 0
                  AND movement.sth_giris_depo_no = 56
                  AND movement.sth_cikis_depo_no = 1
                  AND stock.sto_isim LIKE 'MNV%'
                GROUP BY
                    CAST(movement.sth_tarih AS date),
                    LTRIM(RTRIM(movement.sth_cari_kodu)),
                    LTRIM(RTRIM(movement.sth_stok_kod))
            )
            SELECT
                ISNULL(label_groups.ReceiptDate, micro_groups.ReceiptDate) AS ReceiptDate,
                ISNULL(label_groups.SupplierCode, micro_groups.SupplierCode) AS SupplierCode,
                ISNULL(NULLIF(label_groups.SupplierName, ''), micro_groups.SupplierName) AS SupplierName,
                ISNULL(label_groups.StockCode, micro_groups.StockCode) AS StockCode,
                ISNULL(NULLIF(label_groups.StockName, ''), micro_groups.StockName) AS StockName,
                ISNULL(label_groups.LabelRowCount, 0) AS LabelRowCount,
                ISNULL(label_groups.LabelNetWeight, 0) AS LabelNetWeight,
                ISNULL(micro_groups.MicroRowCount, 0) AS MicroRowCount,
                ISNULL(micro_groups.MicroQuantity, 0) AS MicroQuantity,
                ROUND(ISNULL(micro_groups.MicroQuantity, 0) - ISNULL(label_groups.LabelNetWeight, 0), 4) AS Difference,
                ISNULL(micro_groups.MicroAmount, 0) AS MicroAmount,
                ISNULL(micro_groups.MicroDocument, '') AS MicroDocument
            FROM label_groups
            FULL OUTER JOIN micro_groups
                ON micro_groups.ReceiptDate = label_groups.ReceiptDate
               AND micro_groups.SupplierCode = label_groups.SupplierCode
               AND micro_groups.StockCode = label_groups.StockCode
            ORDER BY
                ABS(ROUND(ISNULL(micro_groups.MicroQuantity, 0) - ISNULL(label_groups.LabelNetWeight, 0), 4)) DESC,
                SupplierName,
                StockName;
            """;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 300;
        AddParameter(command, "@date", date, DbType.Date);
        AddParameter(command, "@supplierCode", supplierCode, DbType.String);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var labelRowCount = ReadInt(reader, "LabelRowCount");
            var microRowCount = ReadInt(reader, "MicroRowCount");
            var difference = ReadDecimal(reader, "Difference");
            rows.Add(new ManavMalKabulVeEtiketGoodsReceiptComparisonItemDto(
                ReadDateTime(reader, "ReceiptDate"),
                ReadString(reader, "SupplierCode"),
                ReadString(reader, "SupplierName"),
                ReadString(reader, "StockCode"),
                ReadString(reader, "StockName"),
                labelRowCount,
                ReadDecimal(reader, "LabelNetWeight"),
                microRowCount,
                ReadDecimal(reader, "MicroQuantity"),
                difference,
                ReadDecimal(reader, "MicroAmount"),
                ReadString(reader, "MicroDocument"),
                ResolveComparisonStatus(labelRowCount, microRowCount, difference)));
        }

        return rows;
    }

    public async Task<ManavMalKabulVeEtiketCreateMicroGoodsReceiptResultDto> CreateMicroGoodsReceiptAsync(
        ManavMalKabulVeEtiketCreateMicroGoodsReceiptRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeCreateMicroGoodsReceiptRequest(request);
        var stockCodes = normalized.Lines.Select(line => line.StockCode).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var stockInfos = await LoadManavStockInfosAsync(stockCodes, cancellationToken);
        var missingStocks = stockCodes
            .Where(stockCode => !stockInfos.ContainsKey(stockCode))
            .ToArray();
        if (missingStocks.Length > 0)
        {
            throw new ArgumentException("MNV stock was not found: " + string.Join(", ", missingStocks), nameof(request.Lines));
        }

        var supplier = await mikroWriteDbContext.CARI_HESAPLARs
            .AsNoTracking()
            .FirstOrDefaultAsync(customer => customer.cari_kod == normalized.SupplierCode, cancellationToken);
        if (supplier is null)
        {
            throw new ArgumentException("Supplier was not found.", nameof(request.SupplierCode));
        }

        var now = DateTime.Now;
        var documentSeries = normalized.DocumentSeries ?? "MNV";
        var createUserNo = Convert.ToInt16(normalized.MikroUserNo ?? DefaultMikroUserNo);
        var offlineTraceKey = BuildOfflineTraceKey(normalized.Date, normalized.SupplierCode, documentSeries);
        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            var alternativeCurrencyRate = await GetAlternativeCurrencyRateAsync(normalized.Date, cancellationToken);

            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            try
            {
                var documentOrderNo = normalized.DocumentOrderNo
                                      ?? await GetNextMicroGoodsReceiptOrderNoAsync(documentSeries, cancellationToken);
                var documentNo = NormalizeOrNull(normalized.DocumentNo) ?? documentOrderNo.ToString();

                var duplicateExists = await mikroWriteDbContext.STOK_HAREKETLERIs
                    .AnyAsync(movement =>
                            movement.sth_tarih == normalized.Date &&
                            movement.sth_tip == IncomingMovementType &&
                            movement.sth_cins == GreenGrocerGoodsReceiptGenre &&
                            movement.sth_normal_iade == NormalMovement &&
                            movement.sth_evraktip == GreenGrocerGoodsReceiptDocumentType &&
                            movement.sth_evrakno_seri == documentSeries &&
                            movement.sth_evrakno_sira == documentOrderNo &&
                            movement.sth_giris_depo_no == GreenGrocerWarehouseNo &&
                            movement.sth_cikis_depo_no == MainWarehouseNo,
                        cancellationToken);
                if (duplicateExists)
                {
                    throw new InvalidOperationException(
                        $"Mikro manav mal kabul belgesi zaten var: {documentSeries}/{documentOrderNo}.");
                }

                var invoiceGuid = Guid.NewGuid();
                var movementRows = normalized.Lines
                    .Select((line, index) => CreateMicroGoodsReceiptMovement(
                        normalized,
                        line,
                        stockInfos[line.StockCode],
                        invoiceGuid,
                        documentSeries,
                        documentOrderNo,
                        documentNo,
                        createUserNo,
                        index,
                        now,
                        alternativeCurrencyRate,
                        offlineTraceKey))
                    .ToArray();
                var customerMovement = CreateMicroGoodsReceiptCustomerMovement(
                    normalized,
                    supplier,
                    invoiceGuid,
                    documentSeries,
                    documentOrderNo,
                    documentNo,
                    createUserNo,
                    now,
                    alternativeCurrencyRate,
                    movementRows,
                    offlineTraceKey);

                await mikroWriteDbContext.CARI_HESAP_HAREKETLERIs.AddAsync(customerMovement, cancellationToken);
                await mikroWriteDbContext.STOK_HAREKETLERIs.AddRangeAsync(movementRows, cancellationToken);
                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);

                var updatedAcceptanceRecordCount = normalized.MarkAcceptanceRecordsTransferred
                    ? await MarkAcceptanceRecordsTransferredAsync(normalized.Lines, cancellationToken)
                    : 0;

                await transaction.CommitAsync(cancellationToken);

                var resultLines = movementRows
                    .Select(row => new ManavMalKabulVeEtiketMicroGoodsReceiptLineDto(
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
                        row.sth_aciklama))
                    .ToArray();

                return new ManavMalKabulVeEtiketCreateMicroGoodsReceiptResultDto(
                    normalized.Date,
                    documentSeries,
                    documentOrderNo,
                    documentSeries + "/" + documentOrderNo,
                    normalized.SupplierCode,
                    createUserNo,
                    resultLines.Length,
                    Round(resultLines.Sum(line => line.Quantity)),
                    Round(resultLines.Sum(line => line.Amount)),
                    Round(resultLines.Sum(line => line.TaxAmount)),
                    updatedAcceptanceRecordCount,
                    offlineTraceKey,
                    resultLines);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<UyumsoftInboxInvoice?> FindCachedIncomingInvoiceAsync(
        string invoiceLookupId,
        CancellationToken cancellationToken) =>
        await authDbContext.UyumsoftInboxInvoices
            .AsNoTracking()
            .Where(invoice =>
                invoice.InvoiceId == invoiceLookupId ||
                invoice.DocumentId == invoiceLookupId ||
                invoice.ServiceDocumentId == invoiceLookupId ||
                invoice.LocalDocumentId == invoiceLookupId ||
                invoice.DespatchId == invoiceLookupId ||
                invoice.OrderDocumentId == invoiceLookupId)
            .OrderByDescending(invoice => invoice.LastSynchronizedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    private static IReadOnlyCollection<string> BuildIncomingInvoiceLookupIds(
        string invoiceLookupId,
        UyumsoftInboxInvoice? cachedInvoice)
    {
        var candidates = new[]
        {
            invoiceLookupId,
            cachedInvoice?.InvoiceId,
            cachedInvoice?.DocumentId,
            cachedInvoice?.ServiceDocumentId,
            cachedInvoice?.LocalDocumentId
        };

        return candidates
            .Select(NormalizeOrNull)
            .Where(value => value is not null)
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<(string LookupId, string InvoiceXml)> FetchIncomingInvoiceXmlAsync(
        IReadOnlyCollection<string> lookupIds,
        CancellationToken cancellationToken)
    {
        var failures = new List<string>();
        var operationNames = new[] { "GetInboxInvoice", "GetInboxInvoiceData" };

        foreach (var lookupId in lookupIds)
        {
            foreach (var operationName in operationNames)
            {
                try
                {
                    var response = await uyumsoftConnectedQueryService.InvokeGetOperationAsync(
                        UyumsoftConnectedServiceKind.EInvoice,
                        new UyumsoftOperationInvocationRequest(
                            operationName,
                            [new UyumsoftOperationParameterRequest("invoiceId", lookupId)]),
                        cancellationToken);

                    if (TryFindInvoiceXml(response, out var invoiceXml))
                    {
                        return (lookupId, invoiceXml);
                    }

                    failures.Add($"{operationName}/{lookupId}: response did not contain invoice XML.");
                }
                catch (InvalidOperationException exception)
                {
                    failures.Add($"{operationName}/{lookupId}: {exception.Message}");
                }
            }
        }

        throw new KeyNotFoundException(
            "Incoming invoice detail was not found in Uyumsoft. Attempts: " + string.Join(" | ", failures));
    }

    private async Task<IReadOnlyCollection<ManavMalKabulVeEtiketInvoiceLineDto>> ReadInvoiceLinesAsync(
        XElement invoice,
        CancellationToken cancellationToken)
    {
        var invoiceLines = invoice
            .Elements()
            .Where(element => IsElement(element, "InvoiceLine"))
            .ToArray();
        var result = new List<ManavMalKabulVeEtiketInvoiceLineDto>(invoiceLines.Length);

        for (var index = 0; index < invoiceLines.Length; index++)
        {
            var line = invoiceLines[index];
            var item = FindPath(line, "Item");
            var lineId = ReadPath(line, "ID") ?? (index + 1).ToString(CultureInfo.InvariantCulture);
            var itemName = ReadPath(item, "Name") ?? string.Empty;
            var note = ReadPath(line, "Note");
            var packagingSummary = ReadInvoiceLinePackagingSummary(note, ReadDecimal(FindPath(line, "InvoicedQuantity")));
            var candidates = CollectInvoiceLineStockCandidates(item).ToArray();
            var matchedStock = await FindStockByInvoiceLineAsync(candidates, itemName, cancellationToken);
            var lineWarnings = new List<string>();
            if (matchedStock is null)
            {
                lineWarnings.Add("Fatura kalemi Mikro stok/barkod kaydiyla otomatik eslesmedi; UI stok secimi istemelidir.");
            }

            var invoicedQuantity = FindPath(line, "InvoicedQuantity");
            var taxAmount = ReadDecimal(FindPath(line, "TaxTotal", "TaxSubtotal", "TaxAmount"))
                ?? ReadDecimal(FindPath(line, "TaxTotal", "TaxAmount"))
                ?? 0m;
            var taxRate = ReadDecimal(FindPath(line, "TaxTotal", "TaxSubtotal", "Percent"))
                ?? ReadDecimal(FindPath(line, "TaxTotal", "TaxSubtotal", "TaxCategory", "Percent"))
                ?? 0m;

            result.Add(new ManavMalKabulVeEtiketInvoiceLineDto(
                index + 1,
                lineId,
                candidates.FirstOrDefault() ?? string.Empty,
                itemName,
                ResolveInvoiceLineBarcode(candidates, matchedStock),
                ReadAttribute(invoicedQuantity, "unitCode") ?? string.Empty,
                note,
                Round(ReadDecimal(invoicedQuantity) ?? 0m),
                packagingSummary.CaseCount,
                RoundOrNull(packagingSummary.GrossWithTareQuantity),
                RoundOrNull(packagingSummary.TareQuantity),
                RoundOrNull(packagingSummary.NetQuantity),
                Round(ReadDecimal(FindPath(line, "Price", "PriceAmount")) ?? 0m),
                Round(ReadDecimal(FindPath(line, "LineExtensionAmount")) ?? 0m),
                Round(taxRate),
                Round(taxAmount),
                matchedStock?.WholesaleTaxPointer,
                matchedStock?.StockCode,
                matchedStock?.StockName,
                NormalizeOrNull(matchedStock?.Barcode),
                matchedStock is not null,
                lineWarnings));
        }

        return result;
    }

    private async Task<ManavMalKabulVeEtiketStockSuggestionDto?> FindStockByInvoiceLineAsync(
        IReadOnlyCollection<string> candidates,
        string stockName,
        CancellationToken cancellationToken)
    {
        foreach (var candidate in candidates)
        {
            if (await FindStockAsync("stock.sto_kod = @value", candidate, cancellationToken) is { } stock)
            {
                return stock;
            }
        }

        foreach (var candidate in candidates)
        {
            if (await FindStockByBarcodeAsync(candidate, cancellationToken) is { } stock)
            {
                return stock;
            }
        }

        var normalizedStockName = NormalizeOrNull(stockName);
        return normalizedStockName is null
            ? null
            : await FindStockAsync("stock.sto_isim = @value", normalizedStockName, cancellationToken);
    }

    private async Task<ManavMalKabulVeEtiketStockSuggestionDto?> FindStockByBarcodeAsync(
        string barcode,
        CancellationToken cancellationToken)
    {
        await using var lease = await OpenConnectionAsync(mikroDbContext.Database.GetDbConnection(), cancellationToken);
        using var command = lease.Connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (1)
                LTRIM(RTRIM(stock.sto_kod)) AS StockCode,
                LTRIM(RTRIM(stock.sto_isim)) AS StockName,
                ISNULL(preferredBarcode.bar_kodu, '') AS Barcode,
                LTRIM(RTRIM(ISNULL(stock.sto_birim1_ad, ''))) AS UnitName,
                LTRIM(RTRIM(ISNULL(stock.sto_model_kodu, ''))) AS ModelCode,
                ISNULL(stock.sto_toptan_vergi, 0) AS WholesaleTaxPointer
            FROM dbo.BARKOD_TANIMLARI AS matchedBarcode WITH (NOLOCK)
            INNER JOIN dbo.STOKLAR AS stock WITH (NOLOCK)
                ON stock.sto_kod = matchedBarcode.bar_stokkodu
            OUTER APPLY
            (
                SELECT TOP (1) LTRIM(RTRIM(item.bar_kodu)) AS bar_kodu
                FROM dbo.BARKOD_TANIMLARI AS item WITH (NOLOCK)
                WHERE item.bar_stokkodu = stock.sto_kod
                  AND ISNULL(item.bar_iptal, 0) <> 1
                  AND item.bar_kodu IS NOT NULL
                  AND LTRIM(RTRIM(item.bar_kodu)) <> ''
                ORDER BY ISNULL(item.bar_master, 0) DESC,
                         ISNULL(item.bar_birimpntr, 0),
                         item.bar_create_date DESC
            ) AS preferredBarcode
            WHERE ISNULL(matchedBarcode.bar_iptal, 0) <> 1
              AND LTRIM(RTRIM(matchedBarcode.bar_kodu)) = @value
            ORDER BY ISNULL(matchedBarcode.bar_master, 0) DESC,
                     ISNULL(matchedBarcode.bar_birimpntr, 0),
                     matchedBarcode.bar_create_date DESC;
            """;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 300;
        AddParameter(command, "@value", barcode, DbType.String);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ManavMalKabulVeEtiketStockSuggestionDto(
                ReadString(reader, "StockCode"),
                ReadString(reader, "StockName"),
                ReadString(reader, "Barcode"),
                ReadString(reader, "UnitName"),
                ReadString(reader, "ModelCode"),
                ReadInt(reader, "WholesaleTaxPointer"))
            : null;
    }

    private async Task<SupplierInvoiceMatch?> LoadSupplierFilterAsync(
        string supplierCode,
        CancellationToken cancellationToken)
    {
        var supplier = await mikroDbContext.CARI_HESAPLARs
            .AsNoTracking()
            .Where(customer => customer.cari_kod == supplierCode)
            .Select(customer => new
            {
                SupplierCode = customer.cari_kod ?? string.Empty,
                SupplierName = customer.cari_unvan1 ?? string.Empty,
                TaxNo = customer.cari_VergiKimlikNo ?? customer.cari_vdaire_no ?? string.Empty
            })
            .FirstOrDefaultAsync(cancellationToken);

        return supplier is null
            ? throw new KeyNotFoundException("Supplier was not found.")
            : new SupplierInvoiceMatch(
                supplier.SupplierCode.Trim(),
                supplier.SupplierName.Trim(),
                NormalizeOrNull(supplier.TaxNo));
    }

    private async Task<IReadOnlyDictionary<string, SupplierInvoiceMatch>> LoadSupplierMatchesByTaxNoAsync(
        IReadOnlyCollection<string> taxNos,
        CancellationToken cancellationToken)
    {
        if (taxNos.Count == 0)
        {
            return new Dictionary<string, SupplierInvoiceMatch>(StringComparer.OrdinalIgnoreCase);
        }

        var normalizedTaxNos = taxNos
            .Select(NormalizeOrNull)
            .Where(taxNo => taxNo is not null)
            .Select(taxNo => taxNo!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedTaxNos.Length == 0)
        {
            return new Dictionary<string, SupplierInvoiceMatch>(StringComparer.OrdinalIgnoreCase);
        }

        var matches = await mikroDbContext.CARI_HESAPLARs
            .AsNoTracking()
            .Where(customer =>
                normalizedTaxNos.Contains(customer.cari_VergiKimlikNo ?? string.Empty) ||
                normalizedTaxNos.Contains(customer.cari_vdaire_no ?? string.Empty))
            .Select(customer => new
            {
                SupplierCode = customer.cari_kod ?? string.Empty,
                SupplierName = customer.cari_unvan1 ?? string.Empty,
                TaxNo = customer.cari_VergiKimlikNo ?? customer.cari_vdaire_no ?? string.Empty
            })
            .ToArrayAsync(cancellationToken);

        return matches
            .Select(match => new SupplierInvoiceMatch(
                match.SupplierCode.Trim(),
                match.SupplierName.Trim(),
                NormalizeOrNull(match.TaxNo)))
            .Where(match => match.TaxNo is not null)
            .GroupBy(match => match.TaxNo!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    private static ManavMalKabulVeEtiketIncomingInvoiceDto MapIncomingInvoice(
        UyumsoftInboxInvoice invoice,
        IReadOnlyDictionary<string, SupplierInvoiceMatch> supplierMatches,
        SupplierInvoiceMatch? supplierFilter)
    {
        var taxNo = NormalizeOrNull(invoice.CustomerTcknVkn);
        var matchedSupplier = taxNo is not null && supplierMatches.TryGetValue(taxNo, out var match)
            ? match
            : supplierFilter;

        return new ManavMalKabulVeEtiketIncomingInvoiceDto(
            invoice.DocumentId,
            invoice.InvoiceId,
            invoice.CustomerTitle,
            invoice.CustomerTcknVkn,
            invoice.CreateDate,
            invoice.InvoiceDate,
            invoice.InvoiceType,
            invoice.InvoiceTotal,
            invoice.TaxExclusiveAmount,
            invoice.TaxTotal,
            invoice.DespatchId,
            invoice.IsProcessed,
            invoice.IsPrinted,
            invoice.IsStandard,
            invoice.StatusCode,
            invoice.Status,
            invoice.Message,
            invoice.DocumentCurrencyCode,
            invoice.ExchangeRate,
            invoice.OrderDocumentId,
            invoice.IsArchived,
            invoice.InvoiceTipType,
            invoice.InvoiceTipTypeCode,
            invoice.IsSeen,
            invoice.LastSynchronizedAtUtc,
            matchedSupplier?.SupplierCode,
            matchedSupplier?.SupplierName,
            matchedSupplier is not null);
    }

    public ManavMalKabulVeEtiketMicroTransferUnavailableDto ExplainMicroTransferAvailability(
        ManavMalKabulVeEtiketMicroTransferRequest request)
    {
        if (request.Date == default)
        {
            throw new ArgumentException("Transfer date is required.", nameof(request.Date));
        }

        if (NormalizeOrNull(request.SupplierCode) is null)
        {
            throw new ArgumentException("Supplier code is required.", nameof(request.SupplierCode));
        }

        return new ManavMalKabulVeEtiketMicroTransferUnavailableDto(
            true,
            "Mikro mal kabul yazma aktif. UI tarih + cari + fiyatli satir onayi aldiktan sonra aktarim yapabilir.",
            "Canli format korunur: CARI_HESAP_HAREKETLERI fatura basligi acilir, STOK_HAREKETLERI satirlari sth_fat_uid ile bu basliga baglanir; duplicate kontrolu ayni transaction icinde yapilir.");
    }

    private async Task<ManavMalKabulVeEtiketStockSuggestionDto?> FindStockAsync(
        string whereClause,
        string value,
        CancellationToken cancellationToken)
    {
        await using var lease = await OpenConnectionAsync(mikroDbContext.Database.GetDbConnection(), cancellationToken);
        using var command = lease.Connection.CreateCommand();
        command.CommandText = $"""
            SELECT TOP (1)
                LTRIM(RTRIM(stock.sto_kod)) AS StockCode,
                LTRIM(RTRIM(stock.sto_isim)) AS StockName,
                ISNULL(barcode.bar_kodu, '') AS Barcode,
                LTRIM(RTRIM(ISNULL(stock.sto_birim1_ad, ''))) AS UnitName,
                LTRIM(RTRIM(ISNULL(stock.sto_model_kodu, ''))) AS ModelCode,
                ISNULL(stock.sto_toptan_vergi, 0) AS WholesaleTaxPointer
            FROM dbo.STOKLAR AS stock WITH (NOLOCK)
            OUTER APPLY
            (
                SELECT TOP (1) LTRIM(RTRIM(item.bar_kodu)) AS bar_kodu
                FROM dbo.BARKOD_TANIMLARI AS item WITH (NOLOCK)
                WHERE item.bar_stokkodu = stock.sto_kod
                  AND ISNULL(item.bar_iptal, 0) <> 1
                  AND item.bar_kodu IS NOT NULL
                  AND LTRIM(RTRIM(item.bar_kodu)) <> ''
                ORDER BY ISNULL(item.bar_master, 0) DESC,
                         ISNULL(item.bar_birimpntr, 0),
                         item.bar_create_date DESC
            ) AS barcode
            WHERE {whereClause}
            ORDER BY stock.sto_isim;
            """;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 300;
        AddParameter(command, "@value", value, DbType.String);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ManavMalKabulVeEtiketStockSuggestionDto(
                ReadString(reader, "StockCode"),
                ReadString(reader, "StockName"),
                ReadString(reader, "Barcode"),
                ReadString(reader, "UnitName"),
                ReadString(reader, "ModelCode"),
                ReadInt(reader, "WholesaleTaxPointer"))
            : null;
    }

    private async Task<ManavMalKabulVeEtiketAcceptanceRecordDto?> FindAcceptanceRecordAsync(
        int id,
        CancellationToken cancellationToken)
    {
        await using var lease = await OpenConnectionAsync(furpaDbContext.Database.GetDbConnection(), cancellationToken);
        using var command = lease.Connection.CreateCommand();
        command.CommandText = AcceptanceRecordSelectSql + """
            WHERE ID = @Id;
            """;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 300;
        AddParameter(command, "@Id", id, DbType.Int32);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? MapAcceptanceRecord(reader)
            : null;
    }

    private async Task<IReadOnlyCollection<ReceivedProductGroup>> ReadReceivedProductGroupsAsync(
        DateTime date,
        CancellationToken cancellationToken)
    {
        var groups = new List<ReceivedProductGroup>();
        await using var lease = await OpenConnectionAsync(furpaDbContext.Database.GetDbConnection(), cancellationToken);
        using var command = lease.Connection.CreateCommand();
        command.CommandText = """
            SELECT
                Cari_Unvan AS SupplierName,
                Stok_Kod AS StockCode,
                Stok_Barkod AS Barcode,
                Stok_Ismi AS StockName,
                SUM(Toplam_Miktar) AS GrossWeight,
                SUM(Kasa_Toplam_Dara) AS CaseTotalTare,
                SUM(Palet_Darasi) AS PalletTare,
                SUM(Kasa_Sayisi) AS CaseCount,
                SUM([Alınan_Net_Miktar]) AS NetReceivedWeight
            FROM dbo.Manav_Depo_Mal_Kabul_Etiket WITH (NOLOCK)
            WHERE Stok_Ismi NOT LIKE '%PALET%'
              AND CAST(Olusturma_Tarihi AS date) = @date
            GROUP BY
                Cari_Unvan,
                Stok_Kod,
                Stok_Ismi,
                Stok_Barkod
            ORDER BY Cari_Unvan, Stok_Ismi;
            """;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 300;
        AddParameter(command, "@date", date, DbType.Date);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            groups.Add(new ReceivedProductGroup(
                string.Empty,
                ReadString(reader, "SupplierName"),
                ReadString(reader, "StockCode"),
                ReadString(reader, "Barcode"),
                ReadString(reader, "StockName"),
                0,
                string.Empty,
                string.Empty,
                string.Empty,
                ReadDecimal(reader, "GrossWeight"),
                ReadDecimal(reader, "CaseTotalTare"),
                ReadDecimal(reader, "PalletTare"),
                Convert.ToInt32(ReadDecimal(reader, "CaseCount")),
                ReadDecimal(reader, "NetReceivedWeight")));
        }

        return groups;
    }

    private async Task<IReadOnlyCollection<ReceivedProductGroup>> ReadReceivedProductReportGroupsAsync(
        DateTime date,
        CancellationToken cancellationToken)
    {
        var groups = new List<ReceivedProductGroup>();
        await using var lease = await OpenConnectionAsync(furpaDbContext.Database.GetDbConnection(), cancellationToken);
        using var command = lease.Connection.CreateCommand();
        command.CommandText = """
            SELECT
                LTRIM(RTRIM(ISNULL(Cari_Kod, ''))) AS SupplierCode,
                MAX(LTRIM(RTRIM(ISNULL(Cari_Unvan, '')))) AS SupplierName,
                LTRIM(RTRIM(Stok_Kod)) AS StockCode,
                MAX(LTRIM(RTRIM(ISNULL(Stok_Barkod, '')))) AS Barcode,
                MAX(LTRIM(RTRIM(Stok_Ismi))) AS StockName,
                COUNT(*) AS LabelRowCount,
                MAX(LTRIM(RTRIM(ISNULL(Evrak_Seri, '')))) AS DocumentSeries,
                STRING_AGG(NULLIF(LTRIM(RTRIM(ISNULL(Evrak_Sira, ''))), ''), ', ') AS DocumentNo,
                ROUND(ISNULL(SUM(Toplam_Miktar), 0), 4) AS GrossWeight,
                ROUND(ISNULL(SUM(Kasa_Toplam_Dara), 0), 4) AS CaseTotalTare,
                ROUND(ISNULL(SUM(Palet_Darasi), 0), 4) AS PalletTare,
                ROUND(ISNULL(SUM(Kasa_Sayisi), 0), 0) AS CaseCount,
                ROUND(ISNULL(SUM(ISNULL(Toplam_Miktar, 0) - ISNULL(Kasa_Toplam_Dara, 0) - ISNULL(Palet_Darasi, 0)), 0), 4) AS NetReceivedWeight
            FROM dbo.Manav_Depo_Mal_Kabul_Etiket WITH (NOLOCK)
            WHERE Stok_Ismi NOT LIKE '%PALET%'
              AND CAST(Olusturma_Tarihi AS date) = @date
            GROUP BY
                LTRIM(RTRIM(ISNULL(Cari_Kod, ''))),
                LTRIM(RTRIM(Stok_Kod))
            ORDER BY SupplierName, StockName;
            """;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 300;
        AddParameter(command, "@date", date, DbType.Date);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var documentSeries = ReadString(reader, "DocumentSeries");
            var documentNo = ReadString(reader, "DocumentNo");
            groups.Add(new ReceivedProductGroup(
                ReadString(reader, "SupplierCode"),
                ReadString(reader, "SupplierName"),
                ReadString(reader, "StockCode"),
                ReadString(reader, "Barcode"),
                ReadString(reader, "StockName"),
                ReadInt(reader, "LabelRowCount"),
                documentSeries,
                documentNo,
                BuildSeriesAndNumber(documentSeries, documentNo),
                ReadDecimal(reader, "GrossWeight"),
                ReadDecimal(reader, "CaseTotalTare"),
                ReadDecimal(reader, "PalletTare"),
                Convert.ToInt32(ReadDecimal(reader, "CaseCount")),
                ReadDecimal(reader, "NetReceivedWeight")));
        }

        return groups;
    }

    private async Task<IReadOnlyDictionary<string, MicroReceiptReportGroup>> ReadMicroReceiptReportGroupsAsync(
        DateTime date,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, MicroReceiptReportGroup>(StringComparer.OrdinalIgnoreCase);
        await using var lease = await OpenConnectionAsync(mikroDbContext.Database.GetDbConnection(), cancellationToken);
        using var command = lease.Connection.CreateCommand();
        command.CommandText = """
            SELECT
                LTRIM(RTRIM(movement.sth_cari_kodu)) AS SupplierCode,
                MAX(LTRIM(RTRIM(ISNULL(customer.cari_unvan1, '')))) AS SupplierName,
                LTRIM(RTRIM(movement.sth_stok_kod)) AS StockCode,
                MAX(LTRIM(RTRIM(stock.sto_isim))) AS StockName,
                MAX(LTRIM(RTRIM(ISNULL(stock.sto_birim1_ad, '')))) AS UnitName,
                COUNT(*) AS MicroRowCount,
                ROUND(ISNULL(SUM(movement.sth_miktar), 0), 4) AS MicroQuantity,
                ROUND(ISNULL(SUM(movement.sth_tutar), 0), 4) AS MicroAmount,
                STRING_AGG(CONCAT(LTRIM(RTRIM(movement.sth_evrakno_seri)), '/', CONVERT(varchar(20), movement.sth_evrakno_sira)), ', ') AS MicroDocument
            FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
            INNER JOIN dbo.STOKLAR AS stock WITH (NOLOCK)
                ON stock.sto_kod = movement.sth_stok_kod
            LEFT JOIN dbo.CARI_HESAPLAR AS customer WITH (NOLOCK)
                ON customer.cari_kod = movement.sth_cari_kodu
            WHERE CAST(movement.sth_tarih AS date) = @date
              AND movement.sth_tip = 0
              AND movement.sth_cins = 16
              AND movement.sth_evraktip = 3
              AND movement.sth_normal_iade = 0
              AND movement.sth_giris_depo_no = 56
              AND movement.sth_cikis_depo_no = 1
              AND stock.sto_isim LIKE 'MNV%'
            GROUP BY
                LTRIM(RTRIM(movement.sth_cari_kodu)),
                LTRIM(RTRIM(movement.sth_stok_kod));
            """;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 300;
        AddParameter(command, "@date", date, DbType.Date);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var group = new MicroReceiptReportGroup(
                ReadString(reader, "SupplierCode"),
                ReadString(reader, "SupplierName"),
                ReadString(reader, "StockCode"),
                ReadString(reader, "StockName"),
                ReadString(reader, "UnitName"),
                ReadInt(reader, "MicroRowCount"),
                ReadDecimal(reader, "MicroQuantity"),
                ReadDecimal(reader, "MicroAmount"),
                ReadString(reader, "MicroDocument"));

            result[BuildInvoiceKey(group.SupplierCode, group.StockCode)] = group;
            result[BuildInvoiceKey(group.SupplierName, group.StockCode)] = group;
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<string, decimal>> ReadInvoiceQuantitiesAsync(
        DateTime date,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        await using var lease = await OpenConnectionAsync(mikroDbContext.Database.GetDbConnection(), cancellationToken);
        using var command = lease.Connection.CreateCommand();
        command.CommandText = """
            SELECT
                LTRIM(RTRIM(customer.cari_unvan1)) AS SupplierName,
                LTRIM(RTRIM(movement.sth_stok_kod)) AS StockCode,
                ROUND(ISNULL(SUM(movement.sth_miktar), 0), 2) AS InvoiceQuantity
            FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
            INNER JOIN dbo.STOKLAR AS stock WITH (NOLOCK)
                ON stock.sto_kod = movement.sth_stok_kod
            INNER JOIN dbo.CARI_HESAPLAR AS customer WITH (NOLOCK)
                ON customer.cari_kod = movement.sth_cari_kodu
            WHERE CAST(movement.sth_tarih AS date) = @date
              AND movement.sth_tip = 0
              AND movement.sth_cins = 16
              AND movement.sth_evraktip = 3
              AND movement.sth_normal_iade = 0
              AND movement.sth_giris_depo_no = 56
              AND movement.sth_cikis_depo_no = 1
              AND stock.sto_isim LIKE 'MNV%'
            GROUP BY customer.cari_unvan1, movement.sth_stok_kod;
            """;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 300;
        AddParameter(command, "@date", date, DbType.Date);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result[BuildInvoiceKey(
                ReadString(reader, "SupplierName"),
                ReadString(reader, "StockCode"))] = ReadDecimal(reader, "InvoiceQuantity");
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<string, MicroStockInfo>> LoadManavStockInfosAsync(
        IReadOnlyCollection<string> stockCodes,
        CancellationToken cancellationToken)
    {
        var rows = await mikroWriteDbContext.STOKLARs
            .AsNoTracking()
            .Where(stock => stock.sto_kod != null &&
                            stockCodes.Contains(stock.sto_kod) &&
                            stock.sto_isim != null &&
                            stock.sto_isim.StartsWith(DefaultStockPrefix))
            .Select(stock => new MicroStockInfo(
                stock.sto_kod ?? string.Empty,
                stock.sto_isim ?? string.Empty,
                stock.sto_toptan_vergi ?? 0))
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.StockCode, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<int> GetNextMicroGoodsReceiptOrderNoAsync(
        string documentSeries,
        CancellationToken cancellationToken)
    {
        var currentMax = await mikroWriteDbContext.STOK_HAREKETLERIs
            .Where(movement =>
                movement.sth_tip == IncomingMovementType &&
                movement.sth_cins == GreenGrocerGoodsReceiptGenre &&
                movement.sth_normal_iade == NormalMovement &&
                movement.sth_evraktip == GreenGrocerGoodsReceiptDocumentType &&
                movement.sth_evrakno_seri == documentSeries)
            .MaxAsync(movement => movement.sth_evrakno_sira, cancellationToken);

        return currentMax.HasValue ? currentMax.Value + 1 : FirstDocumentOrderNo;
    }

    private async Task<double> GetAlternativeCurrencyRateAsync(
        DateTime date,
        CancellationToken cancellationToken)
    {
        await using var lease = await OpenConnectionAsync(mikroWriteDbContext.Database.GetDbConnection(), cancellationToken);
        using var command = lease.Connection.CreateCommand();
        command.CommandText = """
            SELECT ISNULL(NULLIF(dbo.fn_KurBul(@date, dbo.fn_FirmaAlternatifDovizCinsi(), 1), 0), 1);
            """;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 300;
        AddParameter(command, "@date", date.Date, DbType.Date);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null || result is DBNull ? 1d : Convert.ToDouble(result);
    }

    private async Task<int> MarkAcceptanceRecordsTransferredAsync(
        IReadOnlyCollection<ManavMalKabulVeEtiketCreateMicroGoodsReceiptLineRequest> lines,
        CancellationToken cancellationToken)
    {
        var recordIds = lines
            .Select(line => line.AcceptanceRecordId)
            .Where(id => id.HasValue && id.Value > 0)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
        if (recordIds.Length == 0)
        {
            return 0;
        }

        var updatedCount = 0;
        foreach (var recordId in recordIds)
        {
            updatedCount += await mikroWriteDbContext.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE Furpa.dbo.Manav_Depo_Mal_Kabul_Etiket
                SET Mikro_Aktarildi = 1,
                    Degistirme_Tarihi = GETDATE()
                WHERE ID = {recordId}
                  AND ISNULL(Mikro_Aktarildi, 0) = 0;
                """, cancellationToken);
        }

        return updatedCount;
    }

    private static STOK_HAREKETLERI CreateMicroGoodsReceiptMovement(
        ManavMalKabulVeEtiketCreateMicroGoodsReceiptRequest request,
        ManavMalKabulVeEtiketCreateMicroGoodsReceiptLineRequest line,
        MicroStockInfo stockInfo,
        Guid invoiceGuid,
        string documentSeries,
        int documentOrderNo,
        string documentNo,
        short createUserNo,
        int rowNo,
        DateTime now,
        double alternativeCurrencyRate,
        string offlineTraceKey)
    {
        var quantity = Convert.ToDouble(line.Quantity);
        var amount = Convert.ToDouble(Round(line.Quantity * line.UnitPrice));
        var taxPointer = Convert.ToByte(line.TaxPointer ?? stockInfo.WholesaleTaxPointer);
        var taxAmount = ResolveTaxAmount(line, Convert.ToDecimal(amount));

        return new STOK_HAREKETLERI
        {
            sth_Guid = Guid.NewGuid(),
            sth_DBCno = 0,
            sth_SpecRECno = 0,
            sth_iptal = false,
            sth_fileid = MovementFileId,
            sth_hidden = false,
            sth_kilitli = false,
            sth_degisti = false,
            sth_checksum = 0,
            sth_create_user = createUserNo,
            sth_create_date = now,
            sth_lastup_user = createUserNo,
            sth_lastup_date = now,
            sth_special1 = string.Empty,
            sth_special2 = string.Empty,
            sth_special3 = string.Empty,
            sth_firmano = 0,
            sth_subeno = 0,
            sth_tarih = request.Date,
            sth_tip = IncomingMovementType,
            sth_cins = GreenGrocerGoodsReceiptGenre,
            sth_normal_iade = NormalMovement,
            sth_evraktip = GreenGrocerGoodsReceiptDocumentType,
            sth_evrakno_seri = documentSeries,
            sth_evrakno_sira = documentOrderNo,
            sth_satirno = rowNo,
            sth_belge_no = documentNo,
            sth_belge_tarih = request.Date,
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
            sth_cari_kodu = request.SupplierCode,
            sth_cari_grup_no = 0,
            sth_isemri_gider_kodu = string.Empty,
            sth_plasiyer_kodu = string.Empty,
            sth_har_doviz_cinsi = 0,
            sth_har_doviz_kuru = 1d,
            sth_alt_doviz_kuru = alternativeCurrencyRate,
            sth_stok_doviz_cinsi = 0,
            sth_stok_doviz_kuru = 1d,
            sth_miktar = quantity,
            sth_miktar2 = 0d,
            sth_birim_pntr = Convert.ToByte(line.UnitPointer),
            sth_tutar = amount,
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
            sth_vergi_pntr = taxPointer,
            sth_vergi = Convert.ToDouble(taxAmount),
            sth_masraf_vergi_pntr = 0,
            sth_masraf_vergi = 0d,
            sth_netagirlik = 0d,
            sth_odeme_op = 0,
            sth_aciklama = NormalizeText(line.Description ?? request.Description, 50),
            sth_sip_uid = Guid.Empty,
            sth_fat_uid = invoiceGuid,
            sth_giris_depo_no = GreenGrocerWarehouseNo,
            sth_cikis_depo_no = MainWarehouseNo,
            sth_malkbl_sevk_tarihi = request.Date,
            sth_cari_srm_merkezi = string.Empty,
            sth_stok_srm_merkezi = string.Empty,
            sth_fis_tarihi = MikroEmptyDate,
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
            sth_fiyat_liste_no = -1,
            sth_oivtutari = 0d,
            sth_Tevkifat_turu = 0,
            sth_nakliyedeposu = 0,
            sth_nakliyedurumu = 0,
            sth_yetkili_uid = Guid.Empty,
            sth_taxfree_fl = false,
            sth_ilave_edilecek_kdv = 0d,
            sth_ismerkezi_kodu = string.Empty,
            sth_HareketGrupKodu1 = string.Empty,
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
            sth_teslim_tarihi = request.Date,
            sth_matbu_fl = false,
            sth_satis_fiyat_doviz_cinsi = 0,
            sth_satis_fiyat_doviz_kuru = 1d,
            sth_eticaret_kanal_kodu = offlineTraceKey,
            sth_bagli_ithalat_kodu = string.Empty,
            sth_tevkifat_sifirlandi_fl = false
        };
    }

    private static CARI_HESAP_HAREKETLERI CreateMicroGoodsReceiptCustomerMovement(
        ManavMalKabulVeEtiketCreateMicroGoodsReceiptRequest request,
        CARI_HESAPLAR supplier,
        Guid invoiceGuid,
        string documentSeries,
        int documentOrderNo,
        string documentNo,
        short createUserNo,
        DateTime now,
        double alternativeCurrencyRate,
        IReadOnlyCollection<STOK_HAREKETLERI> movementRows,
        string offlineTraceKey)
    {
        var subTotal = Math.Round(movementRows.Sum(row => row.sth_tutar ?? 0d), 2);
        var taxTotal = Math.Round(movementRows.Sum(row => row.sth_vergi ?? 0d), 2);
        var totalAmount = Math.Round(subTotal + taxTotal, 2);
        var taxTotals = ResolveTaxTotals(movementRows);

        return new CARI_HESAP_HAREKETLERI
        {
            cha_Guid = invoiceGuid,
            cha_DBCno = 0,
            cha_SpecRecNo = 0,
            cha_iptal = false,
            cha_fileid = CustomerMovementFileId,
            cha_hidden = false,
            cha_kilitli = false,
            cha_degisti = false,
            cha_CheckSum = 0,
            cha_create_user = createUserNo,
            cha_create_date = now,
            cha_lastup_user = createUserNo,
            cha_lastup_date = now,
            cha_special1 = string.Empty,
            cha_special2 = string.Empty,
            cha_special3 = string.Empty,
            cha_firmano = 0,
            cha_subeno = 0,
            cha_evrak_tip = GreenGrocerCustomerInvoiceDocumentType,
            cha_evrakno_seri = documentSeries,
            cha_evrakno_sira = documentOrderNo,
            cha_satir_no = 0,
            cha_tarihi = request.Date,
            cha_tip = CustomerInvoiceMovementType,
            cha_cinsi = GreenGrocerCustomerInvoiceGenre,
            cha_normal_Iade = NormalMovement,
            cha_tpoz = 0,
            cha_ticaret_turu = 0,
            cha_belge_no = NormalizeText(documentNo, 50),
            cha_belge_tarih = request.Date,
            cha_aciklama = NormalizeText(request.Description, 40),
            cha_satici_kodu = string.Empty,
            cha_EXIMkodu = string.Empty,
            cha_projekodu = string.Empty,
            cha_yat_tes_kodu = string.Empty,
            cha_cari_cins = 0,
            cha_kod = request.SupplierCode,
            cha_ciro_cari_kodu = request.SupplierCode,
            cha_d_cins = 0,
            cha_d_kur = 1d,
            cha_altd_kur = alternativeCurrencyRate,
            cha_grupno = 0,
            cha_srmrkkodu = GreenGrocerWarehouseNo.ToString(),
            cha_kasa_hizmet = 0,
            cha_kasa_hizkod = string.Empty,
            cha_karsidcinsi = 0,
            cha_karsid_kur = 1d,
            cha_karsidgrupno = 0,
            cha_karsisrmrkkodu = string.Empty,
            cha_miktari = 0d,
            cha_meblag = totalAmount,
            cha_aratoplam = subTotal,
            cha_vade = supplier.cari_odemeplan_no ?? 0,
            cha_Vade_Farki_Yuz = 0d,
            cha_ft_iskonto1 = 0d,
            cha_ft_iskonto2 = 0d,
            cha_ft_iskonto3 = 0d,
            cha_ft_iskonto4 = 0d,
            cha_ft_iskonto5 = 0d,
            cha_ft_iskonto6 = 0d,
            cha_ft_masraf1 = 0d,
            cha_ft_masraf2 = 0d,
            cha_ft_masraf3 = 0d,
            cha_ft_masraf4 = 0d,
            cha_isk_mas1 = 0,
            cha_isk_mas2 = 0,
            cha_isk_mas3 = 0,
            cha_isk_mas4 = 0,
            cha_isk_mas5 = 0,
            cha_isk_mas6 = 0,
            cha_isk_mas7 = 0,
            cha_isk_mas8 = 0,
            cha_isk_mas9 = 0,
            cha_isk_mas10 = 0,
            cha_sat_iskmas1 = false,
            cha_sat_iskmas2 = false,
            cha_sat_iskmas3 = false,
            cha_sat_iskmas4 = false,
            cha_sat_iskmas5 = false,
            cha_sat_iskmas6 = false,
            cha_sat_iskmas7 = false,
            cha_sat_iskmas8 = false,
            cha_sat_iskmas9 = false,
            cha_sat_iskmas10 = false,
            cha_yuvarlama = 0d,
            cha_StFonPntr = 0,
            cha_stopaj = 0d,
            cha_savsandesfonu = 0d,
            cha_avansmak_damgapul = 0d,
            cha_vergipntr = 0,
            cha_vergisiz_fl = false,
            cha_otvtutari = 0d,
            cha_otvvergisiz_fl = false,
            cha_oiv_pntr = 0,
            cha_oivtutari = 0d,
            cha_oiv_vergi = 0d,
            cha_oivergisiz_fl = false,
            cha_fis_tarih = request.Date,
            cha_fis_sirano = 0,
            cha_trefno = string.Empty,
            cha_sntck_poz = 0,
            cha_reftarihi = MikroEmptyDate,
            cha_istisnakodu = 0,
            cha_pos_hareketi = 0,
            cha_meblag_ana_doviz_icin_gecersiz_fl = 0,
            cha_meblag_alt_doviz_icin_gecersiz_fl = 0,
            cha_meblag_orj_doviz_icin_gecersiz_fl = 0,
            cha_sip_uid = Guid.Empty,
            cha_kirahar_uid = Guid.Empty,
            cha_vardiya_tarihi = MikroEmptyDate,
            cha_vardiya_no = 0,
            cha_vardiya_evrak_ti = 0,
            cha_ebelge_turu = GreenGrocerCustomerInvoiceElectronicDocumentType,
            cha_tevkifat_toplam = 0d,
            cha_e_islem_turu = 0,
            cha_fatura_belge_turu = GreenGrocerCustomerInvoiceDocumentKind,
            cha_diger_belge_adi = string.Empty,
            cha_uuid = Guid.NewGuid().ToString().ToUpperInvariant(),
            cha_adres_no = ResolveSupplierInvoiceAddressNo(supplier),
            cha_vergifon_toplam = 0d,
            cha_ilk_belge_tarihi = request.Date,
            cha_ilk_belge_doviz_kuru = 0d,
            cha_HareketGrupKodu1 = string.Empty,
            cha_HareketGrupKodu2 = string.Empty,
            cha_HareketGrupKodu3 = string.Empty,
            cha_ebelgeno_seri = string.Empty,
            cha_ebelgeno_sira = 0,
            cha_hubid = string.Empty,
            cha_hubglbid = string.Empty,
            cha_disyazilimid = string.Empty,
            cha_disyazilim_tip = 0,
            cha_bsba_e_belge_mi = 0,
            cha_eticaret_kanal_kodu = NormalizeText(offlineTraceKey, 25),
            cha_hizli_satis_kasa_no = 0,
            cha_ebelge_Islemturu = 0,
            cha_tevkifat_sifirlandi_fl = false,
            cha_vergi1 = taxTotals[0],
            cha_vergi2 = taxTotals[1],
            cha_vergi3 = taxTotals[2],
            cha_vergi4 = taxTotals[3],
            cha_vergi5 = taxTotals[4],
            cha_vergi6 = taxTotals[5],
            cha_vergi7 = taxTotals[6],
            cha_vergi8 = taxTotals[7],
            cha_vergi9 = taxTotals[8],
            cha_vergi10 = taxTotals[9],
            cha_vergi11 = taxTotals[10],
            cha_vergi12 = taxTotals[11],
            cha_vergi13 = taxTotals[12],
            cha_vergi14 = taxTotals[13],
            cha_vergi15 = taxTotals[14],
            cha_vergi16 = taxTotals[15],
            cha_vergi17 = taxTotals[16],
            cha_vergi18 = taxTotals[17],
            cha_vergi19 = taxTotals[18],
            cha_vergi20 = taxTotals[19],
            cha_ilave_edilecek_kdv1 = 0d,
            cha_ilave_edilecek_kdv2 = 0d,
            cha_ilave_edilecek_kdv3 = 0d,
            cha_ilave_edilecek_kdv4 = 0d,
            cha_ilave_edilecek_kdv5 = 0d,
            cha_ilave_edilecek_kdv6 = 0d,
            cha_ilave_edilecek_kdv7 = 0d,
            cha_ilave_edilecek_kdv8 = 0d,
            cha_ilave_edilecek_kdv9 = 0d,
            cha_ilave_edilecek_kdv10 = 0d,
            cha_ilave_edilecek_kdv11 = 0d,
            cha_ilave_edilecek_kdv12 = 0d,
            cha_ilave_edilecek_kdv13 = 0d,
            cha_ilave_edilecek_kdv14 = 0d,
            cha_ilave_edilecek_kdv15 = 0d,
            cha_ilave_edilecek_kdv16 = 0d,
            cha_ilave_edilecek_kdv17 = 0d,
            cha_ilave_edilecek_kdv18 = 0d,
            cha_ilave_edilecek_kdv19 = 0d,
            cha_ilave_edilecek_kdv20 = 0d,
            cha_efatura_belge_tipi = 0
        };
    }

    private static double[] ResolveTaxTotals(IReadOnlyCollection<STOK_HAREKETLERI> movementRows)
    {
        var taxTotals = new double[20];

        foreach (var movement in movementRows)
        {
            var taxPointer = movement.sth_vergi_pntr ?? 0;
            if (taxPointer is < 1 or > 20)
            {
                continue;
            }

            taxTotals[taxPointer - 1] = Math.Round(
                taxTotals[taxPointer - 1] + (movement.sth_vergi ?? 0d),
                2);
        }

        return taxTotals;
    }

    private static int ResolveSupplierInvoiceAddressNo(CARI_HESAPLAR supplier)
    {
        var addressNo = supplier.cari_fatura_adres_no ?? supplier.cari_sevk_adres_no ?? 1;
        return addressNo > 0 ? addressNo : 1;
    }

    private static ManavMalKabulVeEtiketCreateMicroGoodsReceiptRequest NormalizeCreateMicroGoodsReceiptRequest(
        ManavMalKabulVeEtiketCreateMicroGoodsReceiptRequest request)
    {
        if (request.Date == default)
        {
            throw new ArgumentException("Goods receipt date is required.", nameof(request.Date));
        }

        var supplierCode = NormalizeOrNull(request.SupplierCode)
            ?? throw new ArgumentException("Supplier code is required.", nameof(request.SupplierCode));
        var documentSeries = NormalizeText(NormalizeOrNull(request.DocumentSeries) ?? "MNV", 25);
        var documentNo = NormalizeText(request.DocumentNo, 25);
        var description = NormalizeText(request.Description, 50);
        if (request.MikroUserNo is < 0 or > short.MaxValue)
        {
            throw new ArgumentException("Mikro user no must be between 0 and 32767.", nameof(request.MikroUserNo));
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new ArgumentException("At least one goods receipt line is required.", nameof(request.Lines));
        }

        var normalizedLines = request.Lines.Select(NormalizeCreateMicroGoodsReceiptLine).ToArray();
        return request with
        {
            Date = request.Date.Date,
            SupplierCode = supplierCode,
            DocumentSeries = documentSeries,
            DocumentNo = documentNo,
            Description = description,
            Lines = normalizedLines
        };
    }

    private static ManavMalKabulVeEtiketCreateMicroGoodsReceiptLineRequest NormalizeCreateMicroGoodsReceiptLine(
        ManavMalKabulVeEtiketCreateMicroGoodsReceiptLineRequest line)
    {
        var stockCode = NormalizeOrNull(line.StockCode)
            ?? throw new ArgumentException("Stock code is required.", nameof(line.StockCode));
        if (line.Quantity <= 0)
        {
            throw new ArgumentException("Line quantity must be greater than zero.", nameof(line.Quantity));
        }

        if (line.UnitPrice < 0)
        {
            throw new ArgumentException("Line unit price can not be negative.", nameof(line.UnitPrice));
        }

        if (line.UnitPointer is < 1 or > byte.MaxValue)
        {
            throw new ArgumentException("Line unit pointer must be between 1 and 255.", nameof(line.UnitPointer));
        }

        if (line.TaxPointer is < 0 or > byte.MaxValue)
        {
            throw new ArgumentException("Line tax pointer must be between 0 and 255.", nameof(line.TaxPointer));
        }

        if (line.TaxRatePercent is < 0 or > 100)
        {
            throw new ArgumentException("Line tax rate must be between 0 and 100.", nameof(line.TaxRatePercent));
        }

        if (line.TaxAmount < 0)
        {
            throw new ArgumentException("Line tax amount can not be negative.", nameof(line.TaxAmount));
        }

        return line with
        {
            StockCode = NormalizeText(stockCode, 25),
            Description = NormalizeText(line.Description, 50)
        };
    }

    private static bool TryFindInvoiceXml(UyumsoftOperationResponseDto response, out string invoiceXml)
    {
        var candidates = new List<string?>(response.Nodes.Count + 2)
        {
            response.ResponsePayloadJson,
            response.ScalarValue
        };
        candidates.AddRange(response.Nodes.SelectMany(FlattenNodeValues));

        foreach (var candidate in candidates)
        {
            if (TryFindXmlDocument(candidate, "Invoice", out invoiceXml))
            {
                return true;
            }
        }

        invoiceXml = string.Empty;
        return false;
    }

    private static bool TryFindXmlDocument(
        string? value,
        string rootLocalName,
        out string documentXml)
    {
        foreach (var candidate in EnumerateXmlCandidates(value))
        {
            if (!candidate.Contains($"<{rootLocalName}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                var document = XDocument.Parse(candidate, LoadOptions.PreserveWhitespace);
                var root = FindXmlRoot(document, rootLocalName);
                if (root is not null)
                {
                    documentXml = root.ToString(SaveOptions.DisableFormatting);
                    return true;
                }
            }
            catch
            {
                if (TrySliceXmlDocument(candidate, rootLocalName, out documentXml))
                {
                    return true;
                }
            }
        }

        documentXml = string.Empty;
        return false;
    }

    private static bool TrySliceXmlDocument(
        string xmlCandidate,
        string rootLocalName,
        out string documentXml)
    {
        var startIndex = xmlCandidate.IndexOf($"<{rootLocalName}", StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0)
        {
            documentXml = string.Empty;
            return false;
        }

        var closeTag = $"</{rootLocalName}>";
        var endIndex = xmlCandidate.LastIndexOf(closeTag, StringComparison.OrdinalIgnoreCase);
        if (endIndex < startIndex)
        {
            documentXml = string.Empty;
            return false;
        }

        documentXml = xmlCandidate[startIndex..(endIndex + closeTag.Length)].Trim();
        return true;
    }

    private static IEnumerable<string> EnumerateXmlCandidates(string? value)
    {
        var normalized = NormalizeOrNull(value);
        if (normalized is null)
        {
            yield break;
        }

        yield return normalized;

        var decoded = WebUtility.HtmlDecode(normalized);
        if (!string.Equals(decoded, normalized, StringComparison.Ordinal))
        {
            yield return decoded;
        }
    }

    private static IEnumerable<string?> FlattenNodeValues(UyumsoftResponseNodeDto node)
    {
        yield return node.Value;

        foreach (var child in node.Children)
        {
            foreach (var value in FlattenNodeValues(child))
            {
                yield return value;
            }
        }
    }

    private static XElement? FindXmlRoot(XDocument document, string rootLocalName) =>
        document.Root?.Name.LocalName == rootLocalName
            ? document.Root
            : document.Root?
                .Descendants()
                .FirstOrDefault(element => IsElement(element, rootLocalName));

    private static XElement? FindPath(XElement? root, params string[] localPath)
    {
        var current = root;
        foreach (var localName in localPath)
        {
            current = current?
                .Elements()
                .FirstOrDefault(element => IsElement(element, localName));
            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    private static string? ReadPath(XElement? root, params string[] localPath) =>
        NormalizeOrNull(FindPath(root, localPath)?.Value);

    private static string? ReadFirstPath(XElement? root, params string[][] paths)
    {
        foreach (var path in paths)
        {
            if (ReadPath(root, path) is { } value)
            {
                return value;
            }
        }

        return null;
    }

    private static string? ReadAttribute(XElement? element, string localName) =>
        NormalizeOrNull(element?
            .Attributes()
            .FirstOrDefault(attribute => string.Equals(attribute.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))
            ?.Value);

    private static decimal? ReadDecimal(XElement? element)
    {
        var value = NormalizeOrNull(element?.Value);
        if (value is null)
        {
            return null;
        }

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed)
                ? parsed
                : null;
    }

    private static DateTime? ReadDate(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed
            : null;

    private static bool IsElement(XElement element, string localName) =>
        string.Equals(element.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> CollectInvoiceLineStockCandidates(XElement? item)
    {
        if (item is null)
        {
            yield break;
        }

        var candidatePaths = new[]
        {
            new[] { "SellersItemIdentification", "ID" },
            new[] { "BuyersItemIdentification", "ID" },
            new[] { "ManufacturersItemIdentification", "ID" },
            new[] { "StandardItemIdentification", "ID" }
        };

        foreach (var path in candidatePaths)
        {
            if (ReadPath(item, path) is { } value)
            {
                yield return value;
            }
        }

        foreach (var additionalId in item
                     .Elements()
                     .Where(element => IsElement(element, "AdditionalItemIdentification"))
                     .Select(element => ReadPath(element, "ID"))
                     .Where(value => value is not null)
                     .Select(value => value!))
        {
            yield return additionalId;
        }
    }

    private static string ResolveInvoiceLineBarcode(
        IReadOnlyCollection<string> candidates,
        ManavMalKabulVeEtiketStockSuggestionDto? matchedStock)
    {
        if (NormalizeOrNull(matchedStock?.Barcode) is { } matchedBarcode)
        {
            return matchedBarcode;
        }

        return candidates.FirstOrDefault(IsBarcodeLike) ?? string.Empty;
    }

    private static bool IsBarcodeLike(string value)
    {
        var normalized = NormalizeOrNull(value);
        return normalized is not null &&
               normalized.Length >= 7 &&
               normalized.All(char.IsDigit);
    }

    private static InvoicePackagingSummary ReadInvoicePackagingSummary(XElement invoice)
    {
        int? caseCount = null;
        decimal? grossWithTareQuantity = null;
        decimal? tareQuantity = null;
        decimal? netQuantity = null;

        foreach (var note in invoice.Elements().Where(element => IsElement(element, "Note")).Select(element => NormalizeOrNull(element.Value)))
        {
            if (note is null)
            {
                continue;
            }

            if (TryReadLabeledDecimal(note, ["Toplam Kap"], out var parsedCaseCount))
            {
                caseCount = ToNullableInt(parsedCaseCount);
                continue;
            }

            if (TryReadLabeledDecimal(note, ["Toplam Darali", "Toplam Daralı"], out var parsedGrossWithTareQuantity))
            {
                grossWithTareQuantity = parsedGrossWithTareQuantity;
                continue;
            }

            if (TryReadLabeledDecimal(note, ["Toplam Dara"], out var parsedTareQuantity))
            {
                tareQuantity = parsedTareQuantity;
                continue;
            }

            if (TryReadLabeledDecimal(note, ["Toplam Miktar"], out var parsedNetQuantity))
            {
                netQuantity = parsedNetQuantity;
            }
        }

        return new InvoicePackagingSummary(caseCount, grossWithTareQuantity, tareQuantity, netQuantity);
    }

    private static InvoicePackagingSummary ReadInvoiceLinePackagingSummary(string? note, decimal? invoicedQuantity)
    {
        var normalized = NormalizeOrNull(note);
        if (normalized is null)
        {
            return new InvoicePackagingSummary(null, null, null, null);
        }

        var parts = normalized
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3 ||
            !TryReadFlexibleDecimal(parts[0], out var parsedCaseCount) ||
            !TryReadFlexibleDecimal(parts[1], out var grossWithTareQuantity) ||
            !TryReadFlexibleDecimal(parts[2], out var tareQuantity))
        {
            return new InvoicePackagingSummary(null, null, null, null);
        }

        var netQuantity = invoicedQuantity ?? grossWithTareQuantity - tareQuantity;
        return new InvoicePackagingSummary(
            ToNullableInt(parsedCaseCount),
            grossWithTareQuantity,
            tareQuantity,
            netQuantity);
    }

    private static bool TryReadLabeledDecimal(string note, IReadOnlyCollection<string> labels, out decimal value)
    {
        foreach (var label in labels)
        {
            if (!note.StartsWith(label, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var separatorIndex = note.IndexOf(':');
            if (separatorIndex < 0)
            {
                break;
            }

            return TryReadFlexibleDecimal(note[(separatorIndex + 1)..], out value);
        }

        value = default;
        return false;
    }

    private static bool TryReadFlexibleDecimal(string value, out decimal parsed)
    {
        var normalized = NormalizeOrNull(value)?.Replace(" ", string.Empty);
        if (normalized is null)
        {
            parsed = default;
            return false;
        }

        if (normalized.Contains(','))
        {
            normalized = normalized.Replace(".", string.Empty).Replace(',', '.');
        }

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed);
    }

    private static int? ToNullableInt(decimal value) =>
        value == decimal.Truncate(value)
            ? decimal.ToInt32(value)
            : null;

    private static decimal ResolveTaxAmount(
        ManavMalKabulVeEtiketCreateMicroGoodsReceiptLineRequest line,
        decimal amount)
    {
        if (line.TaxAmount.HasValue)
        {
            return Round(line.TaxAmount.Value);
        }

        return line.TaxRatePercent.HasValue
            ? Round(amount * line.TaxRatePercent.Value / 100m)
            : 0m;
    }

    private static string BuildOfflineTraceKey(DateTime date, string supplierCode, string documentSeries)
    {
        var value = $"FURPA-MNV-{date:yyyyMMdd}-{supplierCode}-{documentSeries}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..8];
        return $"FRMNV{date:yyMMdd}{hash}";
    }

    private static SaveManavMalKabulVeEtiketAcceptanceRecordRequest NormalizeSaveRequest(
        SaveManavMalKabulVeEtiketAcceptanceRecordRequest request)
    {
        var supplierCode = NormalizeOrNull(request.SupplierCode)
            ?? throw new ArgumentException("Supplier code is required.", nameof(request.SupplierCode));
        var supplierName = NormalizeOrNull(request.SupplierName)
            ?? throw new ArgumentException("Supplier name is required.", nameof(request.SupplierName));
        var documentSeries = NormalizeOrNull(request.DocumentSeries) ?? "MNV";
        var documentNo = NormalizeOrNull(request.DocumentNo)
            ?? throw new ArgumentException("Document no is required.", nameof(request.DocumentNo));
        var stockCode = NormalizeOrNull(request.StockCode)
            ?? throw new ArgumentException("Stock code is required.", nameof(request.StockCode));
        var stockName = NormalizeOrNull(request.StockName)
            ?? throw new ArgumentException("Stock name is required.", nameof(request.StockName));
        var stockBarcode = NormalizeOrNull(request.StockBarcode)
            ?? throw new ArgumentException("Stock barcode is required.", nameof(request.StockBarcode));
        var receivedBy = NormalizeOrNull(request.ReceivedBy)
            ?? throw new ArgumentException("Received by is required.", nameof(request.ReceivedBy));
        var caseType = ManavMalKabulVeEtiketCalculator.NormalizeCaseType(request.CaseType);

        return request with
        {
            SupplierCode = supplierCode,
            SupplierName = supplierName,
            DocumentSeries = documentSeries,
            DocumentNo = documentNo,
            StockCode = stockCode,
            StockName = stockName,
            StockBarcode = stockBarcode,
            ReceivedBy = receivedBy,
            CaseType = caseType,
            CaseCount = request.CaseCount.GetValueOrDefault(1),
            PalletTare = request.PalletTare.GetValueOrDefault()
        };
    }

    private static ManavMalKabulVeEtiketAcceptanceRecordDto MapAcceptanceRecord(DbDataReader reader)
    {
        var id = ReadInt(reader, "Id");
        var stockBarcode = ReadString(reader, "StockBarcode");
        var averageCaseWeight = ReadDecimal(reader, "AverageCaseWeight");
        var labelBarcodeRaw = ManavMalKabulVeEtiketCalculator.BuildLabelBarcode(stockBarcode, averageCaseWeight);
        var labelBarcode = ManavMalKabulVeEtiketCalculator.BuildPrintableLabelBarcode(labelBarcodeRaw);
        var microTransferred = ReadBool(reader, "MicroTransferred");
        var documentSeries = ReadString(reader, "DocumentSeries");
        var documentNo = ReadString(reader, "DocumentNo");

        return new ManavMalKabulVeEtiketAcceptanceRecordDto(
            id,
            ReadDateTime(reader, "CreatedAt"),
            ReadDateTime(reader, "UpdatedAt"),
            ReadString(reader, "SupplierCode"),
            ReadString(reader, "SupplierName"),
            documentSeries,
            documentNo,
            string.IsNullOrWhiteSpace(documentSeries) && string.IsNullOrWhiteSpace(documentNo)
                ? string.Empty
                : documentSeries + " - " + documentNo,
            ReadString(reader, "StockCode"),
            ReadString(reader, "StockName"),
            stockBarcode,
            ReadDecimal(reader, "GrossWeight"),
            ReadDecimal(reader, "CaseTare"),
            ReadInt(reader, "CaseCount"),
            ReadDecimal(reader, "CaseTotalTare"),
            ReadDecimal(reader, "PalletTare"),
            averageCaseWeight,
            ReadDecimal(reader, "NetReceivedWeight"),
            ReadString(reader, "ReceivedBy"),
            microTransferred,
            microTransferred ? "AKTARILDI" : "BEKLIYOR",
            ReadString(reader, "CaseType"),
            labelBarcodeRaw,
            labelBarcode,
            ManavMalKabulVeEtiketCalculator.ResolveBarcodeSymbology(labelBarcode));
    }

    private static ManavMalKabulVeEtiketLabelDto ToLabel(ManavMalKabulVeEtiketAcceptanceRecordDto record)
    {
        var labelBarcode = record.LabelBarcode
            ?? throw new InvalidOperationException("Acceptance record does not contain a label barcode.");
        var labelBarcodeRaw = record.LabelBarcodeRaw ?? labelBarcode;

        return new ManavMalKabulVeEtiketLabelDto(
            record.Id,
            record.StockCode,
            record.StockName,
            record.StockBarcode,
            record.SupplierName,
            record.AverageCaseWeight,
            record.CreatedAt,
            record.CaseCount,
            labelBarcodeRaw,
            labelBarcode,
            record.BarcodeSymbology,
            record.CaseTare,
            record.CaseType);
    }

    private static void AddSaveParameters(
        DbCommand command,
        SaveManavMalKabulVeEtiketAcceptanceRecordRequest request,
        ManavMalKabulVeEtiketCalculationDto calculation)
    {
        AddParameter(command, "@SupplierCode", request.SupplierCode, DbType.String);
        AddParameter(command, "@SupplierName", request.SupplierName, DbType.String);
        AddParameter(command, "@DocumentSeries", request.DocumentSeries, DbType.String);
        AddParameter(command, "@DocumentNo", request.DocumentNo, DbType.String);
        AddParameter(command, "@StockCode", request.StockCode, DbType.String);
        AddParameter(command, "@StockName", request.StockName, DbType.String);
        AddParameter(command, "@StockBarcode", request.StockBarcode, DbType.String);
        AddParameter(command, "@GrossWeight", request.GrossWeight, DbType.Decimal);
        AddParameter(command, "@CaseTare", request.CaseTare, DbType.Decimal);
        AddParameter(command, "@CaseCount", request.CaseCount.GetValueOrDefault(1), DbType.Int32);
        AddParameter(command, "@CaseTotalTare", calculation.CaseTotalTare, DbType.Decimal);
        AddParameter(command, "@PalletTare", request.PalletTare.GetValueOrDefault(), DbType.Decimal);
        AddParameter(command, "@AverageCaseWeight", calculation.AverageCaseWeight, DbType.Decimal);
        AddParameter(command, "@NetReceivedWeight", calculation.NetReceivedWeight, DbType.Decimal);
        AddParameter(command, "@ReceivedBy", request.ReceivedBy, DbType.String);
        AddParameter(command, "@CaseType", request.CaseType, DbType.String);
    }

    private static string? BuildContainsLike(string? query)
    {
        var normalized = NormalizeOrNull(query);
        if (normalized is null)
        {
            return null;
        }

        var like = normalized.Replace('*', '%');
        return like.Contains('%', StringComparison.Ordinal)
            ? like
            : "%" + like + "%";
    }

    private static string BuildInvoiceKey(string supplierName, string stockCode) =>
        NormalizeOrNull(supplierName)?.ToUpperInvariant() + "|" + NormalizeOrNull(stockCode)?.ToUpperInvariant();

    private static string BuildSeriesAndNumber(string documentSeries, string documentNo) =>
        string.IsNullOrWhiteSpace(documentSeries) && string.IsNullOrWhiteSpace(documentNo)
            ? string.Empty
            : documentSeries + " - " + documentNo;

    private static int NormalizeTake(int take) =>
        take <= 0 ? DefaultTake : Math.Min(take, MaxTake);

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal? RoundOrNull(decimal? value) =>
        value.HasValue ? Round(value.Value) : null;

    private static string ResolveComparisonStatus(int labelRowCount, int microRowCount, decimal difference)
    {
        if (labelRowCount == 0 && microRowCount > 0)
        {
            return "SADECE_MIKRO";
        }

        if (labelRowCount > 0 && microRowCount == 0)
        {
            return "SADECE_ETIKET";
        }

        var absoluteDifference = Math.Abs(difference);
        if (absoluteDifference <= 0.01m)
        {
            return "ESLESTI";
        }

        return absoluteDifference <= 2m ? "YAKIN" : "FARKLI";
    }

    private static void ValidateRecordId(int id)
    {
        if (id <= 0)
        {
            throw new ArgumentException("Acceptance record id must be greater than zero.", nameof(id));
        }
    }

    private static string? NormalizeOrNull(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string NormalizeText(string? value, int maxLength)
    {
        var normalized = NormalizeOrNull(value) ?? string.Empty;
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }

    private static async Task<ConnectionLease> OpenConnectionAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var closeConnection = connection.State == ConnectionState.Closed;
        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        return new ConnectionLease(connection, closeConnection);
    }

    private static void AddParameter(DbCommand command, string name, object? value, DbType dbType)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static int ReadInt(DbDataReader reader, string name) =>
        Convert.ToInt32(reader[name]);

    private static string ReadString(DbDataReader reader, string name) =>
        reader[name] is DBNull ? string.Empty : Convert.ToString(reader[name])?.Trim() ?? string.Empty;

    private static decimal ReadDecimal(DbDataReader reader, string name) =>
        reader[name] is DBNull ? 0m : Convert.ToDecimal(reader[name]);

    private static DateTime ReadDateTime(DbDataReader reader, string name) =>
        reader[name] is DBNull ? DateTime.MinValue : Convert.ToDateTime(reader[name]);

    private static bool ReadBool(DbDataReader reader, string name)
    {
        if (reader[name] is DBNull)
        {
            return false;
        }

        var value = reader[name];
        return value is bool boolValue
            ? boolValue
            : Convert.ToInt32(value) != 0;
    }

    private const string AcceptanceRecordSelectSql = """
        SELECT
            ID AS Id,
            Olusturma_Tarihi AS CreatedAt,
            Degistirme_Tarihi AS UpdatedAt,
            ISNULL(Cari_Kod, '') AS SupplierCode,
            ISNULL(Cari_Unvan, '') AS SupplierName,
            ISNULL(Evrak_Seri, '') AS DocumentSeries,
            ISNULL(Evrak_Sira, '') AS DocumentNo,
            ISNULL(Stok_Kod, '') AS StockCode,
            ISNULL(Stok_Ismi, '') AS StockName,
            ISNULL(Stok_Barkod, '') AS StockBarcode,
            ISNULL(Toplam_Miktar, 0) AS GrossWeight,
            ISNULL(Kasa_Adet_Darasi, 0) AS CaseTare,
            ISNULL(Kasa_Sayisi, 0) AS CaseCount,
            ISNULL(Kasa_Toplam_Dara, 0) AS CaseTotalTare,
            ISNULL(Palet_Darasi, 0) AS PalletTare,
            ISNULL(Kasa_Ortalama_Miktar, 0) AS AverageCaseWeight,
            ISNULL([Alınan_Net_Miktar], 0) AS NetReceivedWeight,
            ISNULL(Teslim_Alan, '') AS ReceivedBy,
            ISNULL(Mikro_Aktarildi, 0) AS MicroTransferred,
            ISNULL(KasaTipi, '') AS CaseType
        FROM dbo.Manav_Depo_Mal_Kabul_Etiket WITH (NOLOCK)
        """;

    private sealed record ReceivedProductGroup(
        string SupplierCode,
        string SupplierName,
        string StockCode,
        string Barcode,
        string StockName,
        int LabelRowCount,
        string DocumentSeries,
        string DocumentNo,
        string SeriesAndNumber,
        decimal GrossWeight,
        decimal CaseTotalTare,
        decimal PalletTare,
        int CaseCount,
        decimal NetReceivedWeight);

    private sealed record MicroReceiptReportGroup(
        string SupplierCode,
        string SupplierName,
        string StockCode,
        string StockName,
        string UnitName,
        int MicroRowCount,
        decimal MicroQuantity,
        decimal MicroAmount,
        string MicroDocument);

    private sealed record MicroGoodsReceiptFlatRow(
        DateTime Date,
        string DocumentSeries,
        int DocumentOrderNo,
        int LineNo,
        string MovementGuid,
        string DocumentNo,
        string InvoiceGuid,
        string OfflineTraceKey,
        string SupplierCode,
        string SupplierName,
        string StockCode,
        string StockName,
        string Barcode,
        string UnitName,
        decimal Quantity,
        decimal Amount,
        decimal TaxAmount,
        int TaxPointer,
        int InWarehouseNo,
        int OutWarehouseNo,
        int CreateUserNo,
        DateTime CreatedAt,
        string Description);

    private sealed record MicroStockInfo(
        string StockCode,
        string StockName,
        int WholesaleTaxPointer);

    private sealed record SupplierInvoiceMatch(
        string SupplierCode,
        string SupplierName,
        string? TaxNo);

    private sealed record InvoicePackagingSummary(
        int? CaseCount,
        decimal? GrossWithTareQuantity,
        decimal? TareQuantity,
        decimal? NetQuantity);

    private sealed class ConnectionLease(DbConnection connection, bool closeConnection) : IAsyncDisposable
    {
        public DbConnection Connection { get; } = connection;

        public async ValueTask DisposeAsync()
        {
            if (closeConnection)
            {
                await Connection.CloseAsync();
            }
        }
    }
}

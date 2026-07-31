using System.Data;
using System.Data.Common;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBasim;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.EtiketBasim;

public sealed class EtiketBasimService(
    FurpaDbContext furpaDbContext,
    MikroDbContext mikroDbContext) : IEtiketBasimService
{
    private const int DefaultTake = 20;
    private const int MaxTake = 100;
    private const string DefaultStockPrefix = "MNV";

    public async Task<IReadOnlyCollection<EtiketBasimSupplierSuggestionDto>> SearchSuppliersAsync(
        EtiketBasimReferenceSearchRequest request,
        CancellationToken cancellationToken)
    {
        var query = NormalizeOrNull(request.Query)
            ?? throw new ArgumentException("Supplier search query is required.", nameof(request.Query));
        if (query.Length < 2)
        {
            throw new ArgumentException("Supplier search query must be at least 2 characters.", nameof(request.Query));
        }

        var take = NormalizeTake(request.Take);
        var suppliers = new List<EtiketBasimSupplierSuggestionDto>(take);
        await using var lease = await OpenConnectionAsync(mikroDbContext.Database.GetDbConnection(), cancellationToken);
        using var command = lease.Connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (@take)
                LTRIM(RTRIM(cari_kod)) AS SupplierCode,
                LTRIM(RTRIM(cari_unvan1)) AS SupplierName
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
            suppliers.Add(new EtiketBasimSupplierSuggestionDto(
                ReadString(reader, "SupplierCode"),
                ReadString(reader, "SupplierName")));
        }

        return suppliers;
    }

    public async Task<EtiketBasimSupplierSuggestionDto> GetSupplierByNameAsync(
        string supplierName,
        CancellationToken cancellationToken)
    {
        var normalizedName = NormalizeOrNull(supplierName)
            ?? throw new ArgumentException("Supplier name is required.", nameof(supplierName));

        var matches = await SearchSuppliersAsync(
            new EtiketBasimReferenceSearchRequest(normalizedName, MaxTake),
            cancellationToken);

        return matches.FirstOrDefault(item =>
                   string.Equals(item.SupplierName, normalizedName, StringComparison.OrdinalIgnoreCase))
               ?? matches.FirstOrDefault()
               ?? throw new KeyNotFoundException("Supplier was not found.");
    }

    public async Task<IReadOnlyCollection<EtiketBasimStockSuggestionDto>> SearchStocksAsync(
        EtiketBasimStockSearchRequest request,
        CancellationToken cancellationToken)
    {
        var query = NormalizeOrNull(request.Query);
        var prefix = NormalizeOrNull(request.Prefix) ?? DefaultStockPrefix;
        var take = NormalizeTake(request.Take);
        var stocks = new List<EtiketBasimStockSuggestionDto>(take);
        await using var lease = await OpenConnectionAsync(mikroDbContext.Database.GetDbConnection(), cancellationToken);
        using var command = lease.Connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (@take)
                LTRIM(RTRIM(stock.sto_kod)) AS StockCode,
                LTRIM(RTRIM(stock.sto_isim)) AS StockName,
                ISNULL(barcode.bar_kodu, '') AS Barcode
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
            stocks.Add(new EtiketBasimStockSuggestionDto(
                ReadString(reader, "StockCode"),
                ReadString(reader, "StockName"),
                ReadString(reader, "Barcode")));
        }

        return stocks;
    }

    public async Task<EtiketBasimStockSuggestionDto> GetStockByCodeAsync(
        string stockCode,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeOrNull(stockCode)
            ?? throw new ArgumentException("Stock code is required.", nameof(stockCode));

        var stock = await FindStockAsync("stock.sto_kod = @value", normalizedCode, cancellationToken);
        return stock ?? throw new KeyNotFoundException("Stock was not found.");
    }

    public async Task<EtiketBasimStockSuggestionDto> GetStockByNameAsync(
        string stockName,
        CancellationToken cancellationToken)
    {
        var normalizedName = NormalizeOrNull(stockName)
            ?? throw new ArgumentException("Stock name is required.", nameof(stockName));

        var stock = await FindStockAsync("stock.sto_isim = @value", normalizedName, cancellationToken);
        return stock ?? throw new KeyNotFoundException("Stock was not found.");
    }

    public EtiketBasimCalculationDto Calculate(EtiketBasimCalculationRequest request) =>
        EtiketBasimCalculator.Calculate(request);

    public async Task<IReadOnlyCollection<EtiketBasimAcceptanceRecordDto>> ListAcceptanceRecordsAsync(
        DateTime date,
        CancellationToken cancellationToken)
    {
        var records = new List<EtiketBasimAcceptanceRecordDto>();
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

    public async Task<EtiketBasimAcceptanceRecordDto> GetAcceptanceRecordAsync(
        int id,
        CancellationToken cancellationToken)
    {
        ValidateRecordId(id);
        return await FindAcceptanceRecordAsync(id, cancellationToken)
               ?? throw new KeyNotFoundException("Acceptance record was not found.");
    }

    public async Task<EtiketBasimAcceptanceRecordDto> CreateAcceptanceRecordAsync(
        SaveEtiketBasimAcceptanceRecordRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeSaveRequest(request);
        var calculation = Calculate(new EtiketBasimCalculationRequest(
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

    public async Task<EtiketBasimAcceptanceRecordDto> UpdateAcceptanceRecordAsync(
        int id,
        SaveEtiketBasimAcceptanceRecordRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRecordId(id);
        var existing = await GetAcceptanceRecordAsync(id, cancellationToken);
        if (existing.MicroTransferred)
        {
            throw new InvalidOperationException("Micro transferred acceptance records cannot be updated.");
        }

        var normalized = NormalizeSaveRequest(request);
        var calculation = Calculate(new EtiketBasimCalculationRequest(
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

    public async Task<EtiketBasimLabelDto> GetLabelAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var record = await GetAcceptanceRecordAsync(id, cancellationToken);
        return ToLabel(record);
    }

    public EtiketBasimLabelDto PreviewLabel(SaveEtiketBasimAcceptanceRecordRequest request)
    {
        var normalized = NormalizeSaveRequest(request);
        var calculation = Calculate(new EtiketBasimCalculationRequest(
            normalized.GrossWeight,
            normalized.CaseTare,
            normalized.CaseCount,
            normalized.PalletTare,
            normalized.StockBarcode));

        var labelBarcode = calculation.LabelBarcode
            ?? throw new ArgumentException("Stock barcode is required for label preview.", nameof(request.StockBarcode));
        var labelBarcodeRaw = calculation.LabelBarcodeRaw ?? labelBarcode;

        return new EtiketBasimLabelDto(
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

    public async Task<IReadOnlyCollection<EtiketBasimReceivedProductReportItemDto>> GetReceivedProductsReportAsync(
        DateTime date,
        CancellationToken cancellationToken)
    {
        var receivedGroups = await ReadReceivedProductGroupsAsync(date.Date, cancellationToken);
        var invoiceQuantities = await ReadInvoiceQuantitiesAsync(date.Date, cancellationToken);

        return receivedGroups
            .Select(group =>
            {
                var invoiceQuantity = invoiceQuantities.GetValueOrDefault(
                    BuildInvoiceKey(group.SupplierName, group.StockCode));
                return new EtiketBasimReceivedProductReportItemDto(
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
                    Round(invoiceQuantity - group.NetReceivedWeight));
            })
            .OrderBy(item => item.InvoiceDifference)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<EtiketBasimDepotStockReportItemDto>> GetDepotStockReportAsync(
        int warehouseNo,
        DateTime date,
        CancellationToken cancellationToken)
    {
        if (warehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(warehouseNo));
        }

        var rows = new List<EtiketBasimDepotStockReportItemDto>();
        await using var lease = await OpenConnectionAsync(mikroDbContext.Database.GetDbConnection(), cancellationToken);
        using var command = lease.Connection.CreateCommand();
        command.CommandText = """
            WITH stock_rows AS
            (
                SELECT
                    movement.sth_stok_kod AS StockCode,
                    stock.sto_isim AS StockName,
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
            rows.Add(new EtiketBasimDepotStockReportItemDto(
                ReadString(reader, "StockCode"),
                ReadString(reader, "StockName"),
                ReadString(reader, "Responsible"),
                ReadDecimal(reader, "CurrentStock"),
                ReadDecimal(reader, "PurchasePriceWithVat"),
                ReadDecimal(reader, "SalesPrice")));
        }

        return rows;
    }

    public EtiketBasimMicroTransferUnavailableDto ExplainMicroTransferAvailability(
        EtiketBasimMicroTransferRequest request)
    {
        if (request.Date == default)
        {
            throw new ArgumentException("Transfer date is required.", nameof(request.Date));
        }

        if (NormalizeOrNull(request.SupplierCode) is null)
        {
            throw new ArgumentException("Supplier code is required.", nameof(request.SupplierCode));
        }

        return new EtiketBasimMicroTransferUnavailableDto(
            false,
            "Mikro mal kabul aktarimi bu API surumunde bilerek kapali. Eski uygulama dogrudan STOK_HAREKETLERI insert yaptigi icin transaction, duplicate kontrol ve evrak no stratejisi netlestirilmeden acilmamali.",
            "Sadece Mikro_Aktarildi = 0 kayitlar, tarih + cari kod bazinda, tek transaction icinde ve idempotent olarak aktarilmalidir.");
    }

    private async Task<EtiketBasimStockSuggestionDto?> FindStockAsync(
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
                ISNULL(barcode.bar_kodu, '') AS Barcode
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
            ? new EtiketBasimStockSuggestionDto(
                ReadString(reader, "StockCode"),
                ReadString(reader, "StockName"),
                ReadString(reader, "Barcode"))
            : null;
    }

    private async Task<EtiketBasimAcceptanceRecordDto?> FindAcceptanceRecordAsync(
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
                ReadString(reader, "SupplierName"),
                ReadString(reader, "StockCode"),
                ReadString(reader, "Barcode"),
                ReadString(reader, "StockName"),
                ReadDecimal(reader, "GrossWeight"),
                ReadDecimal(reader, "CaseTotalTare"),
                ReadDecimal(reader, "PalletTare"),
                Convert.ToInt32(ReadDecimal(reader, "CaseCount")),
                ReadDecimal(reader, "NetReceivedWeight")));
        }

        return groups;
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
            INNER JOIN dbo.CARI_HESAPLAR AS customer WITH (NOLOCK)
                ON customer.cari_kod = movement.sth_cari_kodu
            WHERE CAST(movement.sth_tarih AS date) = @date
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

    private static SaveEtiketBasimAcceptanceRecordRequest NormalizeSaveRequest(
        SaveEtiketBasimAcceptanceRecordRequest request)
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
        var caseType = EtiketBasimCalculator.NormalizeCaseType(request.CaseType);

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

    private static EtiketBasimAcceptanceRecordDto MapAcceptanceRecord(DbDataReader reader)
    {
        var id = ReadInt(reader, "Id");
        var stockBarcode = ReadString(reader, "StockBarcode");
        var averageCaseWeight = ReadDecimal(reader, "AverageCaseWeight");
        var labelBarcodeRaw = EtiketBasimCalculator.BuildLabelBarcode(stockBarcode, averageCaseWeight);
        var labelBarcode = EtiketBasimCalculator.BuildPrintableLabelBarcode(labelBarcodeRaw);
        var microTransferred = ReadBool(reader, "MicroTransferred");
        var documentSeries = ReadString(reader, "DocumentSeries");
        var documentNo = ReadString(reader, "DocumentNo");

        return new EtiketBasimAcceptanceRecordDto(
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
            EtiketBasimCalculator.ResolveBarcodeSymbology(labelBarcode));
    }

    private static EtiketBasimLabelDto ToLabel(EtiketBasimAcceptanceRecordDto record)
    {
        var labelBarcode = record.LabelBarcode
            ?? throw new InvalidOperationException("Acceptance record does not contain a label barcode.");
        var labelBarcodeRaw = record.LabelBarcodeRaw ?? labelBarcode;

        return new EtiketBasimLabelDto(
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
        SaveEtiketBasimAcceptanceRecordRequest request,
        EtiketBasimCalculationDto calculation)
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

    private static int NormalizeTake(int take) =>
        take <= 0 ? DefaultTake : Math.Min(take, MaxTake);

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

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
        string SupplierName,
        string StockCode,
        string Barcode,
        string StockName,
        decimal GrossWeight,
        decimal CaseTotalTare,
        decimal PalletTare,
        int CaseCount,
        decimal NetReceivedWeight);

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

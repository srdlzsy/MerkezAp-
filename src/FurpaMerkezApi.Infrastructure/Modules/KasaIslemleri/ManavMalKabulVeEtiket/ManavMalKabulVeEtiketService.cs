using System.Data;
using System.Data.Common;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.ManavMalKabulVeEtiket;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.ManavMalKabulVeEtiket;

public sealed class ManavMalKabulVeEtiketService(
    FurpaDbContext furpaDbContext,
    MikroDbContext mikroDbContext,
    MikroWriteDbContext mikroWriteDbContext) : IManavMalKabulVeEtiketService
{
    private const int DefaultTake = 20;
    private const int MaxTake = 100;
    private const string DefaultStockPrefix = "MNV";
    private const short MovementFileId = 16;
    private const short DefaultMikroUserNo = 39;
    private const byte IncomingMovementType = 0;
    private const byte GreenGrocerGoodsReceiptGenre = 16;
    private const byte NormalMovement = 0;
    private const byte GreenGrocerGoodsReceiptDocumentType = 3;
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
            suppliers.Add(new ManavMalKabulVeEtiketSupplierSuggestionDto(
                ReadString(reader, "SupplierCode"),
                ReadString(reader, "SupplierName")));
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
            stocks.Add(new ManavMalKabulVeEtiketStockSuggestionDto(
                ReadString(reader, "StockCode"),
                ReadString(reader, "StockName"),
                ReadString(reader, "Barcode")));
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
        var receivedGroups = await ReadReceivedProductGroupsAsync(date.Date, cancellationToken);
        var invoiceQuantities = await ReadInvoiceQuantitiesAsync(date.Date, cancellationToken);

        return receivedGroups
            .Select(group =>
            {
                var invoiceQuantity = invoiceQuantities.GetValueOrDefault(
                    BuildInvoiceKey(group.SupplierName, group.StockCode));
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
                    Round(invoiceQuantity - group.NetReceivedWeight));
            })
            .OrderBy(item => item.InvoiceDifference)
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
            rows.Add(new ManavMalKabulVeEtiketDepotStockReportItemDto(
                ReadString(reader, "StockCode"),
                ReadString(reader, "StockName"),
                ReadString(reader, "Responsible"),
                ReadDecimal(reader, "CurrentStock"),
                ReadDecimal(reader, "PurchasePriceWithVat"),
                ReadDecimal(reader, "SalesPrice")));
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
                LTRIM(RTRIM(movement.sth_cari_kodu)) AS SupplierCode,
                LTRIM(RTRIM(ISNULL(customer.cari_unvan1, ''))) AS SupplierName,
                LTRIM(RTRIM(movement.sth_stok_kod)) AS StockCode,
                LTRIM(RTRIM(stock.sto_isim)) AS StockName,
                ROUND(ISNULL(movement.sth_miktar, 0), 4) AS Quantity,
                ROUND(ISNULL(movement.sth_tutar, 0), 4) AS Amount,
                ROUND(ISNULL(movement.sth_vergi, 0), 4) AS TaxAmount,
                movement.sth_vergi_pntr AS TaxPointer,
                movement.sth_giris_depo_no AS InWarehouseNo,
                movement.sth_cikis_depo_no AS OutWarehouseNo,
                movement.sth_create_user AS CreateUserNo,
                movement.sth_create_date AS CreatedAt
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
                ReadString(reader, "SupplierCode"),
                ReadString(reader, "SupplierName"),
                ReadString(reader, "StockCode"),
                ReadString(reader, "StockName"),
                ReadDecimal(reader, "Quantity"),
                ReadDecimal(reader, "Amount"),
                ReadDecimal(reader, "TaxAmount"),
                ReadInt(reader, "TaxPointer"),
                ReadInt(reader, "InWarehouseNo"),
                ReadInt(reader, "OutWarehouseNo"),
                ReadInt(reader, "CreateUserNo"),
                ReadDateTime(reader, "CreatedAt")));
        }

        return rows
            .GroupBy(row => new
            {
                row.Date,
                row.DocumentSeries,
                row.DocumentOrderNo,
                row.SupplierCode,
                row.SupplierName,
                row.CreateUserNo
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
                        row.OutWarehouseNo))
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
                    lines);
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

        var supplierExists = await mikroWriteDbContext.CARI_HESAPLARs
            .AsNoTracking()
            .AnyAsync(customer => customer.cari_kod == normalized.SupplierCode, cancellationToken);
        if (!supplierExists)
        {
            throw new ArgumentException("Supplier was not found.", nameof(request.SupplierCode));
        }

        var now = DateTime.Now;
        var documentSeries = normalized.DocumentSeries ?? "MNV";
        var createUserNo = Convert.ToInt16(normalized.MikroUserNo ?? DefaultMikroUserNo);
        var offlineTraceKey = BuildOfflineTraceKey(normalized.Date, normalized.SupplierCode, documentSeries);

        await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

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

        var movementRows = normalized.Lines
            .Select((line, index) => CreateMicroGoodsReceiptMovement(
                normalized,
                line,
                stockInfos[line.StockCode],
                documentSeries,
                documentOrderNo,
                documentNo,
                createUserNo,
                index,
                now,
                offlineTraceKey))
            .ToArray();

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
                row.sth_cikis_depo_no ?? 0))
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
            false,
            "Mikro mal kabul yazma bu API surumunde bilerek kapali. 2026 canli akisinda etiket tablosu tartim/etiket kaydi, Mikro mal kabul ise ayrica fiyatli STOK_HAREKETLERI belgesi olarak olusuyor.",
            "Yazma acilacaksa tarih + cari + fiyatli satir onayi alinmali, canli format sth_tip=0/sth_cins=16/sth_evraktip=3/giris_depo=56/cikis_depo=1 korunmali ve duplicate/idempotency kontrolu ayni transaction icinde yapilmalidir.");
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
            ? new ManavMalKabulVeEtiketStockSuggestionDto(
                ReadString(reader, "StockCode"),
                ReadString(reader, "StockName"),
                ReadString(reader, "Barcode"))
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
        string documentSeries,
        int documentOrderNo,
        string documentNo,
        short createUserNo,
        int rowNo,
        DateTime now,
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
            sth_alt_doviz_kuru = 0d,
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
            sth_fat_uid = Guid.Empty,
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
        return NormalizeText(value, 50);
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

    private static int NormalizeTake(int take) =>
        take <= 0 ? DefaultTake : Math.Min(take, MaxTake);

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

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
        string SupplierName,
        string StockCode,
        string Barcode,
        string StockName,
        decimal GrossWeight,
        decimal CaseTotalTare,
        decimal PalletTare,
        int CaseCount,
        decimal NetReceivedWeight);

    private sealed record MicroGoodsReceiptFlatRow(
        DateTime Date,
        string DocumentSeries,
        int DocumentOrderNo,
        int LineNo,
        string SupplierCode,
        string SupplierName,
        string StockCode,
        string StockName,
        decimal Quantity,
        decimal Amount,
        decimal TaxAmount,
        int TaxPointer,
        int InWarehouseNo,
        int OutWarehouseNo,
        int CreateUserNo,
        DateTime CreatedAt);

    private sealed record MicroStockInfo(
        string StockCode,
        string StockName,
        int WholesaleTaxPointer);

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

using System.Data;
using System.Data.Common;
using FurpaMerkezApi.Application.Modules.AramaIslemleri.ProductCustomerSuggestions;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.AramaIslemleri.ProductCustomerSuggestions;

public sealed class GetProductCustomerSuggestionsUseCase(MikroDbContext mikroDbContext)
    : IGetProductCustomerSuggestionsUseCase
{
    private const int DefaultTake = 10;
    private const int MaxTake = 25;
    private const int RecentMovementScanLimit = 500;

    public async Task<ProductCustomerSuggestionResponse> ExecuteAsync(
        ProductCustomerSuggestionRequest request,
        CancellationToken cancellationToken)
    {
        var stockCode = NormalizeOrNull(request.StockCode)
            ?? throw new ArgumentException("Stock code is required.", nameof(request.StockCode));
        var take = NormalizeTake(request.Take);

        var stock = await mikroDbContext.STOKLARs
            .AsNoTracking()
            .Where(item => item.sto_kod == stockCode)
            .Select(item => new StockInfo(
                item.sto_kod,
                item.sto_isim,
                item.sto_sat_cari_kod))
            .FirstOrDefaultAsync(cancellationToken);

        if (stock is null)
        {
            return new ProductCustomerSuggestionResponse(
                false,
                stockCode,
                null,
                null,
                null,
                Array.Empty<ProductCustomerSuggestionDto>());
        }

        var suggestions = new Dictionary<string, SuggestionAccumulator>(StringComparer.OrdinalIgnoreCase);
        var defaultSupplierCode = NormalizeOrNull(stock.DefaultSupplierCode);
        string? defaultSupplierName = null;

        if (defaultSupplierCode is not null)
        {
            var defaultSupplier = await mikroDbContext.CARI_HESAPLARs
                .AsNoTracking()
                .Where(customer => customer.cari_kod == defaultSupplierCode)
                .Select(customer => new CustomerInfo(
                    customer.cari_kod ?? string.Empty,
                    customer.cari_unvan1,
                    customer.cari_VergiKimlikNo))
                .FirstOrDefaultAsync(cancellationToken);

            defaultSupplierName = NormalizeOrNull(defaultSupplier?.CustomerName);

            suggestions[defaultSupplierCode] = new SuggestionAccumulator(
                defaultSupplierCode,
                defaultSupplierName ?? defaultSupplierCode,
                NormalizeOrNull(defaultSupplier?.TaxNoOrTckn),
                true);
            suggestions[defaultSupplierCode].AddSource("varsayilan-tedarikci");
        }

        var purchaseTermRows = await GetPurchaseTermRowsAsync(
            stock.StockCode,
            request.WarehouseNo,
            cancellationToken);

        foreach (var row in purchaseTermRows)
        {
            var customerCode = NormalizeOrNull(row.CustomerCode);
            if (customerCode is null)
            {
                continue;
            }

            if (!suggestions.TryGetValue(customerCode, out var accumulator))
            {
                accumulator = new SuggestionAccumulator(
                    customerCode,
                    NormalizeOrNull(row.CustomerName) ?? customerCode,
                    NormalizeOrNull(row.TaxNoOrTckn),
                    false);
                suggestions[customerCode] = accumulator;
            }

            accumulator.AddSource("satinalma-sarti");
            accumulator.RegisterPurchaseTerm(row.PurchaseTermDate ?? row.CreatedAt);
        }

        var movementRows = await (
                from movement in mikroDbContext.STOK_HAREKETLERIs.AsNoTracking()
                join customer in mikroDbContext.CARI_HESAPLARs.AsNoTracking()
                    on movement.sth_cari_kodu equals customer.cari_kod into customerJoin
                from customer in customerJoin.DefaultIfEmpty()
                where movement.sth_iptal != true &&
                      movement.sth_stok_kod == stockCode &&
                      movement.sth_cari_kodu != null &&
                      movement.sth_cari_kodu != string.Empty
                orderby movement.sth_tarih descending, movement.sth_belge_tarih descending, movement.sth_create_date descending
                select new MovementRow(
                    movement.sth_cari_kodu ?? string.Empty,
                    customer != null ? customer.cari_unvan1 : null,
                    customer != null ? customer.cari_VergiKimlikNo : null,
                    movement.sth_tarih ?? movement.sth_belge_tarih ?? movement.sth_create_date,
                    movement.sth_belge_no))
            .Take(RecentMovementScanLimit)
            .ToListAsync(cancellationToken);

        foreach (var row in movementRows)
        {
            var customerCode = NormalizeOrNull(row.CustomerCode);
            if (customerCode is null)
            {
                continue;
            }

            if (!suggestions.TryGetValue(customerCode, out var accumulator))
            {
                accumulator = new SuggestionAccumulator(
                    customerCode,
                    NormalizeOrNull(row.CustomerName) ?? customerCode,
                    NormalizeOrNull(row.TaxNoOrTckn),
                    false);
                suggestions[customerCode] = accumulator;
            }

            accumulator.AddSource("stok-hareketleri");
            accumulator.RegisterMovement(row.MovementDate, NormalizeOrNull(row.DocumentNo));
        }

        var orderedSuggestions = suggestions.Values
            .OrderByDescending(item => item.IsDefaultSupplier)
            .ThenByDescending(item => item.IsPurchaseTermSupplier)
            .ThenByDescending(item => item.LastPurchaseTermDate)
            .ThenByDescending(item => item.LastMovementDate)
            .ThenByDescending(item => item.MovementCount)
            .ThenBy(item => item.CustomerCode, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .Select(item => new ProductCustomerSuggestionDto(
                item.CustomerCode,
                item.CustomerName,
                item.TaxNoOrTckn,
                item.IsDefaultSupplier,
                item.MovementCount,
                item.LastMovementDate,
                item.LastDocumentNo,
                item.Sources
                    .OrderBy(source => source, StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .ToArray();

        return new ProductCustomerSuggestionResponse(
            true,
            stock.StockCode,
            NormalizeOrNull(stock.StockName),
            defaultSupplierCode,
            defaultSupplierName,
            orderedSuggestions);
    }

    private static int NormalizeTake(int take) =>
        take <= 0 ? DefaultTake : Math.Min(take, MaxTake);

    private async Task<IReadOnlyCollection<PurchaseTermRow>> GetPurchaseTermRowsAsync(
        string stockCode,
        int? warehouseNo,
        CancellationToken cancellationToken)
    {
        var connection = mikroDbContext.Database.GetDbConnection();
        var closeConnection = connection.State == ConnectionState.Closed;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT TOP (100)
                    LTRIM(RTRIM(term.sas_cari_kod)) AS CustomerCode,
                    customer.cari_unvan1 AS CustomerName,
                    COALESCE(customer.cari_VergiKimlikNo, customer.cari_vdaire_no) AS TaxNoOrTckn,
                    term.sas_belge_tarih AS PurchaseTermDate,
                    term.sas_create_date AS CreatedAt
                FROM dbo.SATINALMA_SARTLARI AS term WITH (NOLOCK)
                LEFT JOIN dbo.CARI_HESAPLAR AS customer WITH (NOLOCK)
                    ON customer.cari_kod = term.sas_cari_kod
                WHERE term.sas_stok_kod = @stockCode
                  AND ISNULL(term.sas_iptal, 0) = 0
                  AND NULLIF(LTRIM(RTRIM(term.sas_cari_kod)), N'') IS NOT NULL
                  AND (@warehouseNo IS NULL OR term.sas_depo_no IS NULL OR term.sas_depo_no IN (0, @warehouseNo))
                  AND (term.sas_basla_tarih IS NULL OR term.sas_basla_tarih <= GETDATE())
                  AND (
                        term.sas_bitis_tarih IS NULL
                        OR term.sas_bitis_tarih <= CONVERT(date, '19000101', 112)
                        OR term.sas_bitis_tarih >= CONVERT(date, GETDATE())
                  )
                ORDER BY
                    CASE WHEN @warehouseNo IS NOT NULL AND term.sas_depo_no = @warehouseNo THEN 0 ELSE 1 END,
                    term.sas_belge_tarih DESC,
                    term.sas_create_date DESC;
                """;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 300;

            AddParameter(command, "@stockCode", stockCode, DbType.String);
            AddParameter(command, "@warehouseNo", warehouseNo, DbType.Int32);

            var rows = new List<PurchaseTermRow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new PurchaseTermRow(
                    ReadString(reader, "CustomerCode"),
                    ReadString(reader, "CustomerName"),
                    ReadString(reader, "TaxNoOrTckn"),
                    ReadNullableDateTime(reader, "PurchaseTermDate"),
                    ReadNullableDateTime(reader, "CreatedAt")));
            }

            return rows;
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static string? NormalizeOrNull(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static void AddParameter(DbCommand command, string name, object? value, DbType dbType)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string ReadString(DbDataReader reader, string name) =>
        reader[name] is DBNull ? string.Empty : Convert.ToString(reader[name]) ?? string.Empty;

    private static DateTime? ReadNullableDateTime(DbDataReader reader, string name) =>
        reader[name] is DBNull ? null : Convert.ToDateTime(reader[name]);

    private sealed record StockInfo(
        string StockCode,
        string? StockName,
        string? DefaultSupplierCode);

    private sealed record CustomerInfo(
        string CustomerCode,
        string? CustomerName,
        string? TaxNoOrTckn);

    private sealed record MovementRow(
        string CustomerCode,
        string? CustomerName,
        string? TaxNoOrTckn,
        DateTime MovementDate,
        string? DocumentNo);

    private sealed record PurchaseTermRow(
        string CustomerCode,
        string? CustomerName,
        string? TaxNoOrTckn,
        DateTime? PurchaseTermDate,
        DateTime? CreatedAt);

    private sealed class SuggestionAccumulator(
        string customerCode,
        string customerName,
        string? taxNoOrTckn,
        bool isDefaultSupplier)
    {
        public string CustomerCode { get; } = customerCode;

        public string CustomerName { get; private set; } = customerName;

        public string? TaxNoOrTckn { get; private set; } = taxNoOrTckn;

        public bool IsDefaultSupplier { get; } = isDefaultSupplier;

        public bool IsPurchaseTermSupplier { get; private set; }

        public DateTime? LastPurchaseTermDate { get; private set; }

        public int MovementCount { get; private set; }

        public DateTime? LastMovementDate { get; private set; }

        public string? LastDocumentNo { get; private set; }

        public HashSet<string> Sources { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void AddSource(string source) =>
            Sources.Add(source);

        public void RegisterPurchaseTerm(DateTime? purchaseTermDate)
        {
            IsPurchaseTermSupplier = true;

            if (purchaseTermDate.HasValue &&
                (!LastPurchaseTermDate.HasValue || purchaseTermDate.Value > LastPurchaseTermDate.Value))
            {
                LastPurchaseTermDate = purchaseTermDate;
            }
        }

        public void RegisterMovement(DateTime movementDate, string? documentNo)
        {
            MovementCount++;

            if (!LastMovementDate.HasValue || movementDate > LastMovementDate.Value)
            {
                LastMovementDate = movementDate;
                LastDocumentNo = documentNo;
            }
        }
    }
}

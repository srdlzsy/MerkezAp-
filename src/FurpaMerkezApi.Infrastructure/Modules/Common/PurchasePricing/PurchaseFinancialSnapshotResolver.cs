using System.Data;
using System.Data.Common;
using FurpaMerkezApi.Application.Modules.Common.PurchasePricing;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Create;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.Common.PurchasePricing;

public sealed class PurchaseFinancialSnapshotResolver(MikroWriteDbContext mikroWriteDbContext)
{
    private const decimal ComparisonTolerance = 0.0001m;

    public async Task<CreateCompanyReceivingRequest> ResolveAsync(
        CreateCompanyReceivingRequest request,
        CancellationToken cancellationToken)
    {
        var effectiveDate = (request.MovementDate ?? DateTime.Today).Date;
        var lines = (request.Lines ?? []).ToArray();
        var conditions = await LoadPurchaseConditionsAsync(
            request.CustomerCode,
            request.WarehouseNo,
            effectiveDate,
            lines.Select(line => line.StockCode),
            cancellationToken);
        var orders = await LoadOrderSnapshotsAsync(
            lines.Where(line => line.OrderGuid.HasValue).Select(line => line.OrderGuid!.Value),
            cancellationToken);

        var resolvedLines = lines.Select(line =>
        {
            var stockCode = line.StockCode.Trim();
            conditions.TryGetValue(stockCode, out var condition);
            PurchaseFinancialSnapshot snapshot;

            if (line.OrderGuid.HasValue && orders.TryGetValue(line.OrderGuid.Value, out var order))
            {
                snapshot = ResolveOrderSnapshot(order, condition);
            }
            else if (condition is not null)
            {
                snapshot = condition;
            }
            else
            {
                snapshot = PurchaseFinancialCalculator.FromNetUnitPrice(
                    Convert.ToDecimal(line.UnitPrice),
                    0,
                    0m);
            }

            snapshot = PurchaseFinancialCalculator.Normalize(snapshot);
            return line with
            {
                UnitPrice = Convert.ToDouble(snapshot.NetUnitPrice),
                PurchaseFinancial = snapshot
            };
        }).ToArray();

        return request with { Lines = resolvedLines };
    }

    public async Task<CreateIssuedCompanyOrderRequest> ResolveAsync(
        CreateIssuedCompanyOrderRequest request,
        CancellationToken cancellationToken)
    {
        var effectiveDate = (request.OrderDate ?? DateTime.Today).Date;
        var lines = (request.Lines ?? []).ToArray();
        var conditions = await LoadPurchaseConditionsAsync(
            request.CustomerCode,
            request.WarehouseNo,
            effectiveDate,
            lines.Select(line => line.StockCode),
            cancellationToken);

        var resolvedLines = lines.Select(line =>
        {
            var stockCode = line.StockCode.Trim();
            var snapshot = conditions.TryGetValue(stockCode, out var condition)
                ? condition
                : PurchaseFinancialCalculator.FromNetUnitPrice(
                    Convert.ToDecimal(line.UnitPrice),
                    0,
                    0m);
            snapshot = PurchaseFinancialCalculator.Normalize(snapshot);

            return line with
            {
                UnitPrice = Convert.ToDouble(snapshot.NetUnitPrice),
                PurchaseFinancial = snapshot
            };
        }).ToArray();

        return request with { Lines = resolvedLines };
    }

    private async Task<IReadOnlyDictionary<string, PurchaseFinancialSnapshot>> LoadPurchaseConditionsAsync(
        string customerCode,
        int warehouseNo,
        DateTime effectiveDate,
        IEnumerable<string> requestedStockCodes,
        CancellationToken cancellationToken)
    {
        var stockCodes = requestedStockCodes
            .Select(code => code?.Trim() ?? string.Empty)
            .Where(code => code.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (stockCodes.Length == 0)
        {
            return new Dictionary<string, PurchaseFinancialSnapshot>(StringComparer.OrdinalIgnoreCase);
        }

        var connection = mikroWriteDbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            var stockParameters = stockCodes
                .Select((_, index) => $"@stock{index}")
                .ToArray();
            command.CommandText = BuildPurchaseConditionSql(stockParameters);
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 300;
            AddParameter(command, "@customerCode", customerCode.Trim(), DbType.String);
            AddParameter(command, "@warehouseNo", warehouseNo, DbType.Int32);
            AddParameter(command, "@effectiveDate", effectiveDate, DbType.Date);
            for (var index = 0; index < stockCodes.Length; index++)
            {
                AddParameter(command, stockParameters[index], stockCodes[index], DbType.String);
            }

            var result = new Dictionary<string, PurchaseFinancialSnapshot>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var stockCode = ReadString(reader, "StockCode");
                var snapshot = new PurchaseFinancialSnapshot(
                    ReadGuid(reader, "PurchaseConditionId"),
                    "purchase-requirement",
                    ReadDecimal(reader, "GrossUnitPrice"),
                    0m,
                    ReadValues(reader, "DiscountRate", 6),
                    ReadValues(reader, "DiscountUnitAmount", 6),
                    ReadValues(reader, "ExpenseRate", 4),
                    ReadValues(reader, "ExpenseUnitAmount", 4),
                    ReadInt32(reader, "TaxPointer"),
                    ReadDecimal(reader, "TaxRatePercent"));
                result[stockCode] = PurchaseFinancialCalculator.Normalize(snapshot);
            }

            return result;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<IReadOnlyDictionary<Guid, OrderFinancialRow>> LoadOrderSnapshotsAsync(
        IEnumerable<Guid> requestedOrderIds,
        CancellationToken cancellationToken)
    {
        var orderIds = requestedOrderIds.Distinct().ToArray();
        if (orderIds.Length == 0)
        {
            return new Dictionary<Guid, OrderFinancialRow>();
        }

        var orders = await mikroWriteDbContext.SIPARISLERs
            .AsNoTracking()
            .Where(order => orderIds.Contains(order.sip_Guid))
            .ToListAsync(cancellationToken);
        var taxRates = await LoadTaxRatesAsync(cancellationToken);

        return orders.ToDictionary(
            order => order.sip_Guid,
            order =>
            {
                var quantity = Convert.ToDecimal(order.sip_miktar ?? 0d);
                var divisor = quantity > 0m ? quantity : 1m;
                var taxPointer = order.sip_vergi_pntr ?? 0;
                return new OrderFinancialRow(
                    order.sip_Guid,
                    Convert.ToDecimal(order.sip_b_fiyat ?? 0d),
                    Convert.ToDecimal(order.sip_tutar ?? 0d),
                    ReadOrderValues(
                        divisor,
                        order.sip_iskonto_1,
                        order.sip_iskonto_2,
                        order.sip_iskonto_3,
                        order.sip_iskonto_4,
                        order.sip_iskonto_5,
                        order.sip_iskonto_6),
                    ReadOrderValues(
                        divisor,
                        order.sip_masraf_1,
                        order.sip_masraf_2,
                        order.sip_masraf_3,
                        order.sip_masraf_4),
                    taxPointer,
                    taxRates.TryGetValue(taxPointer, out var rate) ? rate : 0m);
            });
    }

    private static PurchaseFinancialSnapshot ResolveOrderSnapshot(
        OrderFinancialRow order,
        PurchaseFinancialSnapshot? condition)
    {
        var orderHasBreakdown = order.DiscountUnitAmounts.Any(value => value > 0m) ||
                                order.ExpenseUnitAmounts.Any(value => value > 0m);
        if (!orderHasBreakdown &&
            condition is not null &&
            (Math.Abs(order.UnitPrice - condition.NetUnitPrice) <= ComparisonTolerance ||
             Math.Abs(order.UnitPrice - condition.GrossUnitPrice) <= ComparisonTolerance))
        {
            return condition with { Source = "legacy-order-purchase-requirement" };
        }

        var grossUnitPrice = order.UnitPrice > 0m
            ? order.UnitPrice
            : order.TotalAmount;
        return PurchaseFinancialCalculator.Normalize(new PurchaseFinancialSnapshot(
            condition?.PurchaseConditionId,
            "issued-order",
            grossUnitPrice,
            0m,
            new decimal[6],
            order.DiscountUnitAmounts,
            new decimal[4],
            order.ExpenseUnitAmounts,
            order.TaxPointer,
            order.TaxRatePercent));
    }

    private async Task<IReadOnlyDictionary<int, decimal>> LoadTaxRatesAsync(CancellationToken cancellationToken)
    {
        var result = new Dictionary<int, decimal>();
        var connection = mikroWriteDbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT DepartmentNo, Rate FROM dbo.fn_hs_vergi_oran_listesi();";
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 300;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result[Convert.ToInt32(reader["DepartmentNo"])] = Convert.ToDecimal(reader["Rate"]);
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        return result;
    }

    private static string BuildPurchaseConditionSql(IReadOnlyCollection<string> stockParameters) => $"""
        WITH RankedConditions AS
        (
            SELECT
                LTRIM(RTRIM(term.sas_stok_kod)) AS StockCode,
                term.sas_Guid AS PurchaseConditionId,
                ISNULL(term.sas_brut_fiyat, 0) AS GrossUnitPrice,
                ISNULL(term.sas_isk_yuzde1, 0) AS DiscountRate1,
                ISNULL(term.sas_isk_yuzde2, 0) AS DiscountRate2,
                ISNULL(term.sas_isk_yuzde3, 0) AS DiscountRate3,
                ISNULL(term.sas_isk_yuzde4, 0) AS DiscountRate4,
                ISNULL(term.sas_isk_yuzde5, 0) AS DiscountRate5,
                ISNULL(term.sas_isk_yuzde6, 0) AS DiscountRate6,
                ISNULL(term.sas_isk_miktar1, 0) AS DiscountUnitAmount1,
                ISNULL(term.sas_isk_miktar2, 0) AS DiscountUnitAmount2,
                ISNULL(term.sas_isk_miktar3, 0) AS DiscountUnitAmount3,
                ISNULL(term.sas_isk_miktar4, 0) AS DiscountUnitAmount4,
                ISNULL(term.sas_isk_miktar5, 0) AS DiscountUnitAmount5,
                ISNULL(term.sas_isk_miktar6, 0) AS DiscountUnitAmount6,
                ISNULL(term.sas_mas_yuzde1, 0) AS ExpenseRate1,
                ISNULL(term.sas_mas_yuzde2, 0) AS ExpenseRate2,
                ISNULL(term.sas_mas_yuzde3, 0) AS ExpenseRate3,
                ISNULL(term.sas_mas_yuzde4, 0) AS ExpenseRate4,
                ISNULL(term.sas_mas_miktar1, 0) AS ExpenseUnitAmount1,
                ISNULL(term.sas_mas_miktar2, 0) AS ExpenseUnitAmount2,
                ISNULL(term.sas_mas_miktar3, 0) AS ExpenseUnitAmount3,
                ISNULL(term.sas_mas_miktar4, 0) AS ExpenseUnitAmount4,
                ISNULL(stock.sto_toptan_vergi, 0) AS TaxPointer,
                ISNULL(tax.Rate, 0) AS TaxRatePercent,
                ROW_NUMBER() OVER
                (
                    PARTITION BY LTRIM(RTRIM(term.sas_stok_kod))
                    ORDER BY
                        CASE WHEN term.sas_depo_no = @warehouseNo THEN 0 ELSE 1 END,
                        ISNULL(term.sas_basla_tarih, CONVERT(datetime, '19000101', 112)) DESC,
                        ISNULL(term.sas_belge_tarih, CONVERT(datetime, '19000101', 112)) DESC,
                        ISNULL(term.sas_create_date, CONVERT(datetime, '19000101', 112)) DESC,
                        term.sas_Guid DESC
                ) AS RowNo
            FROM dbo.SATINALMA_SARTLARI term
            INNER JOIN dbo.STOKLAR stock
                ON stock.sto_kod = term.sas_stok_kod
            OUTER APPLY
            (
                SELECT TOP (1) Rate
                FROM dbo.fn_hs_vergi_oran_listesi()
                WHERE DepartmentNo = ISNULL(stock.sto_toptan_vergi, 0)
            ) tax
            WHERE LTRIM(RTRIM(term.sas_stok_kod)) IN ({string.Join(", ", stockParameters)})
              AND term.sas_cari_kod = @customerCode
              AND ISNULL(term.sas_iptal, 0) = 0
              AND (term.sas_depo_no IS NULL OR term.sas_depo_no IN (0, @warehouseNo))
              AND (term.sas_basla_tarih IS NULL OR CONVERT(date, term.sas_basla_tarih) <= @effectiveDate)
              AND
              (
                  term.sas_bitis_tarih IS NULL
                  OR term.sas_bitis_tarih <= CONVERT(date, '19000101', 112)
                  OR CONVERT(date, term.sas_bitis_tarih) >= @effectiveDate
              )
        )
        SELECT * FROM RankedConditions WHERE RowNo = 1;
        """;

    private static decimal[] ReadValues(DbDataReader reader, string prefix, int count) =>
        Enumerable.Range(1, count)
            .Select(index => ReadDecimal(reader, $"{prefix}{index}"))
            .ToArray();

    private static decimal[] ReadOrderValues(decimal divisor, params double?[] values) =>
        values.Select(value => Convert.ToDecimal(value ?? 0d) / divisor).ToArray();

    private static string ReadString(DbDataReader reader, string name) =>
        Convert.ToString(reader[name])?.Trim() ?? string.Empty;

    private static Guid? ReadGuid(DbDataReader reader, string name) =>
        reader[name] is Guid value && value != Guid.Empty ? value : null;

    private static decimal ReadDecimal(DbDataReader reader, string name) =>
        reader[name] == DBNull.Value ? 0m : Convert.ToDecimal(reader[name]);

    private static int ReadInt32(DbDataReader reader, string name) =>
        reader[name] == DBNull.Value ? 0 : Convert.ToInt32(reader[name]);

    private static void AddParameter(DbCommand command, string name, object value, DbType dbType)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record OrderFinancialRow(
        Guid OrderId,
        decimal UnitPrice,
        decimal TotalAmount,
        IReadOnlyList<decimal> DiscountUnitAmounts,
        IReadOnlyList<decimal> ExpenseUnitAmounts,
        int TaxPointer,
        decimal TaxRatePercent);
}

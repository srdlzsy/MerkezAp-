using System.Data;
using System.Data.Common;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari.Queries;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaSayimlari.Queries;

public sealed class CashSummaryQueriesUseCase(MikroDbContext mikroDbContext)
    : ICashSummaryQueriesUseCase
{
    public async Task<IReadOnlyCollection<CashSummaryReportItemDto>> GetReportAsync(
        CashSummaryDateRequest request,
        CancellationToken cancellationToken)
    {
        var (date, nextDate) = NormalizeDateRange(request);

        const string sql = """
            SELECT
                s.BranchNo AS WarehouseNo,
                COALESCE(w.dep_adi, '') AS WarehouseName,
                SUM(CASE WHEN s.PaymentTypeID = 500 THEN s.Amount ELSE 0 END) AS CashAmount,
                SUM(CASE WHEN s.PaymentTypeID = 500 THEN s.SlipNumber ELSE 0 END) AS CashAmountQuantity,
                SUM(CASE WHEN s.PaymentTypeID = 1 THEN s.Amount ELSE 0 END) AS Akbank,
                SUM(CASE WHEN s.PaymentTypeID = 1 THEN s.SlipNumber ELSE 0 END) AS AkbankQuantity,
                SUM(CASE WHEN s.PaymentTypeID = 2 THEN s.Amount ELSE 0 END) AS Halkbank,
                SUM(CASE WHEN s.PaymentTypeID = 2 THEN s.SlipNumber ELSE 0 END) AS HalkbankQuantity,
                SUM(CASE WHEN s.PaymentTypeID = 3 THEN s.Amount ELSE 0 END) AS IsBankasi,
                SUM(CASE WHEN s.PaymentTypeID = 3 THEN s.SlipNumber ELSE 0 END) AS IsBankasiQuantity,
                SUM(CASE WHEN s.PaymentTypeID = 4 THEN s.Amount ELSE 0 END) AS Teb,
                SUM(CASE WHEN s.PaymentTypeID = 4 THEN s.SlipNumber ELSE 0 END) AS TebQuantity,
                SUM(CASE WHEN s.PaymentTypeID = 5 THEN s.Amount ELSE 0 END) AS YapiKredi,
                SUM(CASE WHEN s.PaymentTypeID = 5 THEN s.SlipNumber ELSE 0 END) AS YapiKrediQuantity,
                SUM(CASE WHEN s.PaymentTypeID = 6 THEN s.Amount ELSE 0 END) AS ZiraatBankasi,
                SUM(CASE WHEN s.PaymentTypeID = 6 THEN s.SlipNumber ELSE 0 END) AS ZiraatBankasiQuantity,
                SUM(CASE WHEN s.PaymentTypeID = 56 THEN s.Amount ELSE 0 END) AS Metropol,
                SUM(CASE WHEN s.PaymentTypeID = 56 THEN s.SlipNumber ELSE 0 END) AS MetropolQuantity,
                SUM(CASE WHEN s.PaymentTypeID = 54 THEN s.Amount ELSE 0 END) AS Multinet,
                SUM(CASE WHEN s.PaymentTypeID = 54 THEN s.SlipNumber ELSE 0 END) AS MultinetQuantity,
                SUM(CASE WHEN s.PaymentTypeID = 55 THEN s.Amount ELSE 0 END) AS Setcard,
                SUM(CASE WHEN s.PaymentTypeID = 55 THEN s.SlipNumber ELSE 0 END) AS SetcardQuantity,
                SUM(CASE WHEN s.PaymentTypeID = 51 THEN s.Amount ELSE 0 END) AS SodexoKupon,
                SUM(CASE WHEN s.PaymentTypeID = 51 THEN s.SlipNumber ELSE 0 END) AS SodexoKuponQuantity,
                SUM(CASE WHEN s.PaymentTypeID = 50 THEN s.Amount ELSE 0 END) AS SodexoPos,
                SUM(CASE WHEN s.PaymentTypeID = 50 THEN s.SlipNumber ELSE 0 END) AS SodexoPosQuantity,
                SUM(CASE WHEN s.PaymentTypeID = 53 THEN s.Amount ELSE 0 END) AS TicketKupon,
                SUM(CASE WHEN s.PaymentTypeID = 53 THEN s.SlipNumber ELSE 0 END) AS TicketKuponQuantity,
                SUM(CASE WHEN s.PaymentTypeID = 52 THEN s.Amount ELSE 0 END) AS TicketPos,
                SUM(CASE WHEN s.PaymentTypeID = 52 THEN s.SlipNumber ELSE 0 END) AS TicketPosQuantity,
                SUM(CASE WHEN s.PaymentTypeID = 100 THEN s.Amount ELSE 0 END) AS ExpenseCompass,
                SUM(CASE WHEN s.PaymentTypeID = 100 THEN s.SlipNumber ELSE 0 END) AS ExpenseCompassQuantity,
                SUM(CASE WHEN s.PaymentTypeID BETWEEN 110 AND 113 THEN s.Amount ELSE 0 END) AS StoreExpense,
                SUM(CASE WHEN s.PaymentTypeID BETWEEN 110 AND 113 THEN s.SlipNumber ELSE 0 END) AS StoreExpenseQuantity
            FROM Summaries s
            LEFT JOIN DEPOLAR w ON s.BranchNo = w.dep_no
            WHERE s.SummaryDate >= @date
              AND s.SummaryDate < @nextDate
              AND (@warehouseNo IS NULL OR s.BranchNo = @warehouseNo)
            GROUP BY
                s.BranchNo,
                COALESCE(w.dep_adi, '')
            ORDER BY
                COALESCE(w.dep_adi, ''),
                s.BranchNo;
            """;

        return await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@date", date);
                AddParameter(command, "@nextDate", nextDate);
                AddParameter(command, "@warehouseNo", request.WarehouseNo);
            },
            reader => new CashSummaryReportItemDto(
                ReadInt(reader, "WarehouseNo"),
                ReadString(reader, "WarehouseName"),
                Round(ReadDouble(reader, "CashAmount")),
                ReadInt(reader, "CashAmountQuantity"),
                Round(ReadDouble(reader, "Akbank")),
                ReadInt(reader, "AkbankQuantity"),
                Round(ReadDouble(reader, "Halkbank")),
                ReadInt(reader, "HalkbankQuantity"),
                Round(ReadDouble(reader, "IsBankasi")),
                ReadInt(reader, "IsBankasiQuantity"),
                Round(ReadDouble(reader, "Teb")),
                ReadInt(reader, "TebQuantity"),
                Round(ReadDouble(reader, "YapiKredi")),
                ReadInt(reader, "YapiKrediQuantity"),
                Round(ReadDouble(reader, "ZiraatBankasi")),
                ReadInt(reader, "ZiraatBankasiQuantity"),
                Round(ReadDouble(reader, "Metropol")),
                ReadInt(reader, "MetropolQuantity"),
                Round(ReadDouble(reader, "Multinet")),
                ReadInt(reader, "MultinetQuantity"),
                Round(ReadDouble(reader, "Setcard")),
                ReadInt(reader, "SetcardQuantity"),
                Round(ReadDouble(reader, "SodexoKupon")),
                ReadInt(reader, "SodexoKuponQuantity"),
                Round(ReadDouble(reader, "SodexoPos")),
                ReadInt(reader, "SodexoPosQuantity"),
                Round(ReadDouble(reader, "TicketKupon")),
                ReadInt(reader, "TicketKuponQuantity"),
                Round(ReadDouble(reader, "TicketPos")),
                ReadInt(reader, "TicketPosQuantity"),
                Round(ReadDouble(reader, "ExpenseCompass")),
                ReadInt(reader, "ExpenseCompassQuantity"),
                Round(ReadDouble(reader, "StoreExpense")),
                ReadInt(reader, "StoreExpenseQuantity")),
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<CashSummaryListItemDto>> ListAsync(
        CashSummaryDateRequest request,
        CancellationToken cancellationToken)
    {
        var (date, nextDate) = NormalizeDateRange(request);

        const string sql = """
            WITH DistinctSummaries AS (
                SELECT DISTINCT
                    s.BranchNo AS WarehouseNo,
                    COALESCE(w.dep_adi, '') AS WarehouseName,
                    s.DocumentSerie,
                    s.DocumentOrderNo,
                    s.CashNo,
                    s.ZReportNo,
                    s.CashierNo,
                    s.ManagerNo,
                    s.SummaryDate,
                    s.PaymentTypeID,
                    s.Amount
                FROM Summaries s
                LEFT JOIN DEPOLAR w ON s.BranchNo = w.dep_no
                WHERE s.SummaryDate >= @date
                  AND s.SummaryDate < @nextDate
                  AND (@warehouseNo IS NULL OR s.BranchNo = @warehouseNo)
            )
            SELECT
                ds.WarehouseNo,
                ds.WarehouseName,
                ds.DocumentSerie,
                ds.DocumentOrderNo,
                MAX(ds.CashNo) AS CashNo,
                MAX(ds.ZReportNo) AS ZReportNo,
                MAX(ds.CashierNo) AS CashierNo,
                MAX(ds.ManagerNo) AS ManagerNo,
                MAX(ds.SummaryDate) AS SummaryDate,
                SUM(CASE WHEN ds.PaymentTypeID < 100 OR ds.PaymentTypeID = 500 THEN ds.Amount ELSE 0 END) AS Total
            FROM DistinctSummaries ds
            GROUP BY
                ds.WarehouseNo,
                ds.WarehouseName,
                ds.DocumentSerie,
                ds.DocumentOrderNo
            ORDER BY
                SummaryDate,
                ds.WarehouseNo,
                ds.DocumentSerie,
                ds.DocumentOrderNo;
            """;

        return await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@date", date);
                AddParameter(command, "@nextDate", nextDate);
                AddParameter(command, "@warehouseNo", request.WarehouseNo);
            },
            reader => new CashSummaryListItemDto(
                ReadInt(reader, "WarehouseNo"),
                ReadString(reader, "WarehouseName"),
                ReadString(reader, "DocumentSerie"),
                ReadInt(reader, "DocumentOrderNo"),
                ReadInt(reader, "CashNo"),
                ReadInt(reader, "ZReportNo"),
                ReadInt(reader, "CashierNo"),
                ReadInt(reader, "ManagerNo"),
                ReadDateTime(reader, "SummaryDate"),
                Round(ReadDouble(reader, "Total"))),
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<CashSummaryDetailItemDto>> GetDetailsAsync(
        CashSummaryDocumentRequest request,
        CancellationToken cancellationToken)
    {
        ValidateDocumentRequest(request);

        const string sql = """
            SELECT
                CASE
                    WHEN s.PaymentTypeID = 500 THEN N'Nakit'
                    ELSE COALESCE(pt.PaymentName, '')
                END AS TypeName,
                s.PaymentTypeID AS PaymentTypeId,
                COALESCE(pt.AccountCode, '') AS AccountCode,
                s.SlipNumber,
                s.Amount,
                COALESCE(s.TerminalId, '') AS TerminalId,
                COALESCE(s.Description, '') AS Description
            FROM Summaries s
            LEFT JOIN PaymentTypes pt ON s.PaymentTypeID = pt.PaymentTypeNo
            WHERE s.BranchNo = @warehouseNo
              AND s.DocumentSerie = @documentSerie
              AND s.DocumentOrderNo = @documentOrderNo
            ORDER BY
                s.PaymentTypeID,
                s.SlipNumber,
                s.Id;
            """;

        return await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@warehouseNo", request.WarehouseNo);
                AddParameter(command, "@documentSerie", request.DocumentSerie);
                AddParameter(command, "@documentOrderNo", request.DocumentOrderNo);
            },
            reader => new CashSummaryDetailItemDto(
                ReadString(reader, "TypeName"),
                ReadInt(reader, "PaymentTypeId"),
                ReadString(reader, "AccountCode"),
                ReadInt(reader, "SlipNumber"),
                Round(ReadDouble(reader, "Amount")),
                ReadString(reader, "TerminalId"),
                ReadString(reader, "Description")),
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<BanknoteMovementItemDto>> GetBanknoteMovementsAsync(
        CashSummaryDocumentRequest request,
        CancellationToken cancellationToken)
    {
        ValidateDocumentRequest(request);

        const string sql = """
            SELECT
                COALESCE(bt.Value, 0) AS Value,
                bm.BanknoteTypeID AS BanknoteType,
                bm.Quantity,
                bm.Total
            FROM BanknoteMovements bm
            LEFT JOIN BanknoteTypes bt ON bm.BanknoteTypeID = bt.BanknoteType
            WHERE bm.BranchNo = @warehouseNo
              AND bm.DocumentSerie = @documentSerie
              AND bm.DocumentOrderNo = @documentOrderNo
            ORDER BY
                bm.BanknoteTypeID,
                COALESCE(bt.Value, 0);
            """;

        return await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@warehouseNo", request.WarehouseNo);
                AddParameter(command, "@documentSerie", request.DocumentSerie);
                AddParameter(command, "@documentOrderNo", request.DocumentOrderNo);
            },
            reader => new BanknoteMovementItemDto(
                Round(ReadDouble(reader, "Value")),
                ReadInt(reader, "BanknoteType"),
                ReadInt(reader, "Quantity"),
                Round(ReadDouble(reader, "Total"))),
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<GiftCheckMovementItemDto>> GetGiftCheckMovementsAsync(
        CashSummaryDocumentRequest request,
        CancellationToken cancellationToken)
    {
        ValidateDocumentRequest(request);

        const string sql = """
            SELECT
                COALESCE(gt.Value, 0) AS Value,
                gm.GiftCheckTypeID AS GiftCheckType,
                gm.Quantity,
                gm.Total
            FROM GiftCheckMovements gm
            LEFT JOIN GiftCheckTypes gt ON gm.GiftCheckTypeID = gt.GiftCheckType
            WHERE gm.BranchNo = @warehouseNo
              AND gm.DocumentSerie = @documentSerie
              AND gm.DocumentOrderNo = @documentOrderNo
            ORDER BY
                gm.GiftCheckTypeID,
                COALESCE(gt.Value, 0);
            """;

        return await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@warehouseNo", request.WarehouseNo);
                AddParameter(command, "@documentSerie", request.DocumentSerie);
                AddParameter(command, "@documentOrderNo", request.DocumentOrderNo);
            },
            reader => new GiftCheckMovementItemDto(
                Round(ReadDouble(reader, "Value")),
                ReadInt(reader, "GiftCheckType"),
                ReadInt(reader, "Quantity"),
                Round(ReadDouble(reader, "Total"))),
            cancellationToken);
    }

    private async Task<IReadOnlyCollection<T>> ExecuteReaderAsync<T>(
        string sql,
        Action<DbCommand> configureCommand,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken)
    {
        var items = new List<T>();
        var connection = mikroDbContext.Database.GetDbConnection();
        var closeConnection = connection.State == ConnectionState.Closed;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            using var command = connection.CreateCommand();
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

    private static (DateTime Date, DateTime NextDate) NormalizeDateRange(CashSummaryDateRequest request)
    {
        if (request.SummaryDate == default)
        {
            throw new ArgumentException("Summary date is required.", nameof(request.SummaryDate));
        }

        if (request.WarehouseNo is <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        var date = request.SummaryDate.Date;
        return (date, date.AddDays(1));
    }

    private static void ValidateDocumentRequest(CashSummaryDocumentRequest request)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        if (string.IsNullOrWhiteSpace(request.DocumentSerie))
        {
            throw new ArgumentException("Document serie is required.", nameof(request.DocumentSerie));
        }

        if (request.DocumentOrderNo < 0)
        {
            throw new ArgumentException("Document order no can not be negative.", nameof(request.DocumentOrderNo));
        }
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static int ReadInt(DbDataReader reader, string name) =>
        reader[name] is DBNull ? 0 : Convert.ToInt32(reader[name]);

    private static double ReadDouble(DbDataReader reader, string name) =>
        reader[name] is DBNull ? 0d : Convert.ToDouble(reader[name]);

    private static DateTime ReadDateTime(DbDataReader reader, string name) =>
        reader[name] is DBNull ? DateTime.MinValue : Convert.ToDateTime(reader[name]);

    private static string ReadString(DbDataReader reader, string name) =>
        reader[name] is DBNull ? string.Empty : Convert.ToString(reader[name]) ?? string.Empty;

    private static double Round(double value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

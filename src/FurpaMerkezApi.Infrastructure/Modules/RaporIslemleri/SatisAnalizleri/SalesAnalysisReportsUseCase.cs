using System.Data;
using System.Data.Common;
using System.Globalization;
using FurpaMerkezApi.Application.Modules.RaporIslemleri.SatisAnalizleri;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.RaporIslemleri.SatisAnalizleri;

public sealed class SalesAnalysisReportsUseCase(
    MikroDbContext mikroDbContext,
    FurpaDbContext furpaDbContext)
    : ISalesAnalysisReportsUseCase
{
    public async Task<IReadOnlyCollection<BankMovementAnalysisItemDto>> GetBankMovementsAsync(
        SalesAnalysisDateRangeRequest request,
        CancellationToken cancellationToken)
    {
        var (startDate, endDateExclusive) = NormalizeDateRange(request);

        const string sql = """
            SELECT
                s.BranchNo,
                COALESCE(w.dep_adi, '') AS BranchName,
                s.ZReportNo AS ZNo,
                s.SummaryDate AS [Date],
                COALESCE(crd.CashRegisterNo, CONVERT(nvarchar(40), s.CashNo)) AS CashRegisterNo,
                COALESCE(pt.PaymentName, '') AS Bank,
                s.Amount AS BankAmount,
                s.SlipNumber AS BankingNumber,
                COALESCE(NULLIF(s.TerminalId, ''), crd.TerminalId, '') AS TerminalId
            FROM Summaries s WITH (NOLOCK)
            LEFT JOIN PaymentTypes pt WITH (NOLOCK)
                ON s.PaymentTypeID = pt.PaymentTypeNo
            OUTER APPLY (
                SELECT TOP (1)
                    detail.CashRegisterNo,
                    detail.TerminalId
                FROM CashRegisterDetails detail WITH (NOLOCK)
                WHERE detail.Bank = pt.PaymentName
                  AND (NULLIF(LTRIM(RTRIM(s.TerminalId)), '') IS NULL OR detail.TerminalId = s.TerminalId)
                  AND (detail.CashNo IS NULL OR detail.CashNo = s.CashNo)
                ORDER BY
                    CASE WHEN detail.TerminalId = s.TerminalId THEN 0 ELSE 1 END,
                    CASE WHEN detail.CashNo = s.CashNo THEN 0 ELSE 1 END,
                    detail.Id
            ) crd
            LEFT JOIN DEPOLAR w WITH (NOLOCK)
                ON s.BranchNo = w.dep_no
            WHERE s.SummaryDate >= @startDate
              AND s.SummaryDate < @endDateExclusive
              AND s.PaymentTypeID BETWEEN 1 AND 10
              AND (@warehouseNo IS NULL OR s.BranchNo = @warehouseNo)
            ORDER BY
                s.SummaryDate,
                s.BranchNo,
                s.ZReportNo,
                COALESCE(pt.PaymentName, '');
            """;

        return await ExecuteReaderAsync(
            mikroDbContext.Database.GetDbConnection(),
            sql,
            command =>
            {
                AddParameter(command, "@startDate", startDate);
                AddParameter(command, "@endDateExclusive", endDateExclusive);
                AddParameter(command, "@warehouseNo", request.WarehouseNo);
            },
            reader => new BankMovementAnalysisItemDto(
                ReadInt(reader, "BranchNo"),
                ReadString(reader, "BranchName"),
                ReadInt(reader, "ZNo"),
                ReadDateTime(reader, "Date"),
                ReadString(reader, "CashRegisterNo"),
                ReadString(reader, "Bank"),
                Round(ReadDouble(reader, "BankAmount")),
                ReadInt(reader, "BankingNumber"),
                ReadString(reader, "TerminalId")),
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<BranchBankMovementSummaryItemDto>> GetBankMovementsByBranchAsync(
        SalesAnalysisDateRangeRequest request,
        CancellationToken cancellationToken)
    {
        var (startDate, endDateExclusive) = NormalizeDateRange(request);

        const string sql = """
            SELECT
                s.BranchNo,
                COALESCE(w.dep_adi, '') AS BranchName,
                COALESCE(pt.PaymentName, '') AS Bank,
                SUM(s.Amount) AS BankAmount,
                SUM(s.SlipNumber) AS BankingNumber
            FROM Summaries s WITH (NOLOCK)
            LEFT JOIN PaymentTypes pt WITH (NOLOCK)
                ON s.PaymentTypeID = pt.PaymentTypeNo
            LEFT JOIN DEPOLAR w WITH (NOLOCK)
                ON s.BranchNo = w.dep_no
            WHERE s.SummaryDate >= @startDate
              AND s.SummaryDate < @endDateExclusive
              AND s.PaymentTypeID BETWEEN 1 AND 10
              AND (@warehouseNo IS NULL OR s.BranchNo = @warehouseNo)
            GROUP BY
                s.BranchNo,
                COALESCE(w.dep_adi, ''),
                COALESCE(pt.PaymentName, '')
            ORDER BY
                COALESCE(w.dep_adi, ''),
                s.BranchNo,
                COALESCE(pt.PaymentName, '');
            """;

        return await ExecuteReaderAsync(
            mikroDbContext.Database.GetDbConnection(),
            sql,
            command =>
            {
                AddParameter(command, "@startDate", startDate);
                AddParameter(command, "@endDateExclusive", endDateExclusive);
                AddParameter(command, "@warehouseNo", request.WarehouseNo);
            },
            reader => new BranchBankMovementSummaryItemDto(
                ReadInt(reader, "BranchNo"),
                ReadString(reader, "BranchName"),
                ReadString(reader, "Bank"),
                Round(ReadDouble(reader, "BankAmount")),
                ReadInt(reader, "BankingNumber")),
            cancellationToken);
    }

    public async Task<BankPaymentSummaryReportDto> GetBankPaymentSummaryAsync(
        SalesAnalysisDateRangeRequest request,
        CancellationToken cancellationToken)
    {
        var (startDate, endDateExclusive) = NormalizeDateRange(request);

        const string sql = """
            SELECT
                COALESCE(pt.PaymentName, '') AS Bank,
                SUM(s.Amount) AS Amount,
                SUM(s.SlipNumber) AS SlipNumber
            FROM Summaries s WITH (NOLOCK)
            LEFT JOIN PaymentTypes pt WITH (NOLOCK)
                ON s.PaymentTypeID = pt.PaymentTypeNo
            WHERE s.SummaryDate >= @startDate
              AND s.SummaryDate < @endDateExclusive
              AND s.PaymentTypeID < 50
              AND (@warehouseNo IS NULL OR s.BranchNo = @warehouseNo)
            GROUP BY COALESCE(pt.PaymentName, '')
            ORDER BY COALESCE(pt.PaymentName, '');
            """;

        var items = await ExecuteReaderAsync(
            mikroDbContext.Database.GetDbConnection(),
            sql,
            command =>
            {
                AddParameter(command, "@startDate", startDate);
                AddParameter(command, "@endDateExclusive", endDateExclusive);
                AddParameter(command, "@warehouseNo", request.WarehouseNo);
            },
            reader => new BankPaymentSummaryItemDto(
                ReadString(reader, "Bank"),
                Round(ReadDouble(reader, "Amount")),
                ReadInt(reader, "SlipNumber")),
            cancellationToken);

        return new BankPaymentSummaryReportDto(
            items,
            Round(items.Sum(item => item.Amount)),
            items.Sum(item => item.SlipNumber));
    }

    public async Task<MerchantPaymentSummaryReportDto> GetMerchantPaymentSummaryAsync(
        SalesAnalysisDateRangeRequest request,
        CancellationToken cancellationToken)
    {
        var (startDate, endDateExclusive) = NormalizeDateRange(request);

        const string sql = """
            SELECT
                COALESCE(pt.PaymentName, '') AS Bank,
                COALESCE(crd.MerchantNo, '') AS MerchantNo,
                SUM(s.Amount) AS Amount,
                SUM(s.SlipNumber) AS SlipNumber
            FROM Summaries s WITH (NOLOCK)
            LEFT JOIN PaymentTypes pt WITH (NOLOCK)
                ON s.PaymentTypeID = pt.PaymentTypeNo
            OUTER APPLY (
                SELECT TOP (1)
                    detail.MerchantNo
                FROM CashRegisterDetails detail WITH (NOLOCK)
                WHERE detail.TerminalId = s.TerminalId
                  AND detail.Bank = pt.PaymentName
                ORDER BY detail.Id
            ) crd
            WHERE s.SummaryDate >= @startDate
              AND s.SummaryDate < @endDateExclusive
              AND s.PaymentTypeID < 50
              AND (@warehouseNo IS NULL OR s.BranchNo = @warehouseNo)
            GROUP BY
                COALESCE(pt.PaymentName, ''),
                COALESCE(crd.MerchantNo, '')
            ORDER BY
                COALESCE(pt.PaymentName, ''),
                COALESCE(crd.MerchantNo, '');
            """;

        var items = await ExecuteReaderAsync(
            mikroDbContext.Database.GetDbConnection(),
            sql,
            command =>
            {
                AddParameter(command, "@startDate", startDate);
                AddParameter(command, "@endDateExclusive", endDateExclusive);
                AddParameter(command, "@warehouseNo", request.WarehouseNo);
            },
            reader => new MerchantPaymentSummaryItemDto(
                ReadString(reader, "Bank"),
                ReadString(reader, "MerchantNo"),
                Round(ReadDouble(reader, "Amount")),
                ReadInt(reader, "SlipNumber")),
            cancellationToken);

        return new MerchantPaymentSummaryReportDto(
            items,
            Round(items.Sum(item => item.Amount)),
            items.Sum(item => item.SlipNumber));
    }

    public async Task<ValorPaymentSummaryReportDto> GetValorPaymentSummaryAsync(
        SalesAnalysisDateRangeRequest request,
        CancellationToken cancellationToken)
    {
        var (startDate, endDateExclusive) = NormalizeDateRange(request);

        const string sql = """
            SELECT
                COALESCE(pt.PaymentName, '') AS Bank,
                ISNULL(valor.ValorDay, 0) AS ValorDay,
                SUM(s.Amount) AS Amount,
                SUM(s.SlipNumber) AS SlipNumber
            FROM Summaries s WITH (NOLOCK)
            INNER JOIN PaymentTypes pt WITH (NOLOCK)
                ON s.PaymentTypeID = pt.PaymentTypeNo
            INNER JOIN ZReportValors valor WITH (NOLOCK)
                ON valor.Bank = pt.PaymentName
            WHERE s.PaymentTypeID < 50
              AND s.SummaryDate >= DATEADD(DAY, -ISNULL(valor.ValorDay, 0), @startDate)
              AND s.SummaryDate < DATEADD(DAY, -ISNULL(valor.ValorDay, 0), @endDateExclusive)
              AND (@warehouseNo IS NULL OR s.BranchNo = @warehouseNo)
            GROUP BY
                COALESCE(pt.PaymentName, ''),
                ISNULL(valor.ValorDay, 0)
            ORDER BY
                ISNULL(valor.ValorDay, 0),
                COALESCE(pt.PaymentName, '');
            """;

        var items = await ExecuteReaderAsync(
            mikroDbContext.Database.GetDbConnection(),
            sql,
            command =>
            {
                AddParameter(command, "@startDate", startDate);
                AddParameter(command, "@endDateExclusive", endDateExclusive);
                AddParameter(command, "@warehouseNo", request.WarehouseNo);
            },
            reader => new ValorPaymentSummaryItemDto(
                ReadString(reader, "Bank"),
                ReadInt(reader, "ValorDay"),
                Round(ReadDouble(reader, "Amount")),
                ReadInt(reader, "SlipNumber")),
            cancellationToken);

        return new ValorPaymentSummaryReportDto(
            items,
            Round(items.Sum(item => item.Amount)),
            items.Sum(item => item.SlipNumber));
    }

    public async Task<FoodCheckReportDto> GetFoodCheckReportAsync(
        SalesAnalysisDateRangeRequest request,
        CancellationToken cancellationToken)
    {
        var (startDate, endDateExclusive) = NormalizeDateRange(request);

        const string sql = """
            SELECT
                s.BranchNo,
                COALESCE(w.dep_adi, '') AS BranchName,
                SUM(CASE WHEN s.PaymentTypeID = 56 THEN s.Amount ELSE 0 END) AS Metropol,
                SUM(CASE WHEN s.PaymentTypeID = 54 THEN s.Amount ELSE 0 END) AS Multinet,
                SUM(CASE WHEN s.PaymentTypeID = 55 THEN s.Amount ELSE 0 END) AS Setcard,
                SUM(CASE WHEN s.PaymentTypeID = 51 THEN s.Amount ELSE 0 END) AS SodexoKupon,
                SUM(CASE WHEN s.PaymentTypeID = 50 THEN s.Amount ELSE 0 END) AS SodexoPos,
                SUM(CASE WHEN s.PaymentTypeID = 53 THEN s.Amount ELSE 0 END) AS TicketKupon,
                SUM(CASE WHEN s.PaymentTypeID = 52 THEN s.Amount ELSE 0 END) AS TicketPos,
                SUM(CASE WHEN s.PaymentTypeID BETWEEN 50 AND 60 THEN s.Amount ELSE 0 END) AS Total
            FROM Summaries s WITH (NOLOCK)
            LEFT JOIN DEPOLAR w WITH (NOLOCK)
                ON s.BranchNo = w.dep_no
            WHERE s.SummaryDate >= @startDate
              AND s.SummaryDate < @endDateExclusive
              AND s.PaymentTypeID BETWEEN 50 AND 60
              AND (@warehouseNo IS NULL OR s.BranchNo = @warehouseNo)
            GROUP BY
                s.BranchNo,
                COALESCE(w.dep_adi, '')
            ORDER BY
                COALESCE(w.dep_adi, ''),
                s.BranchNo;
            """;

        var items = await ExecuteReaderAsync(
            mikroDbContext.Database.GetDbConnection(),
            sql,
            command =>
            {
                AddParameter(command, "@startDate", startDate);
                AddParameter(command, "@endDateExclusive", endDateExclusive);
                AddParameter(command, "@warehouseNo", request.WarehouseNo);
            },
            reader => new FoodCheckReportItemDto(
                ReadInt(reader, "BranchNo"),
                ReadString(reader, "BranchName"),
                Round(ReadDouble(reader, "Metropol")),
                Round(ReadDouble(reader, "Multinet")),
                Round(ReadDouble(reader, "Setcard")),
                Round(ReadDouble(reader, "SodexoKupon")),
                Round(ReadDouble(reader, "SodexoPos")),
                Round(ReadDouble(reader, "TicketKupon")),
                Round(ReadDouble(reader, "TicketPos")),
                Round(ReadDouble(reader, "Total"))),
            cancellationToken);

        var totals = new FoodCheckTotalsDto(
            Round(items.Sum(item => item.Metropol)),
            Round(items.Sum(item => item.Multinet)),
            Round(items.Sum(item => item.Setcard)),
            Round(items.Sum(item => item.SodexoKupon)),
            Round(items.Sum(item => item.SodexoPos)),
            Round(items.Sum(item => item.TicketKupon)),
            Round(items.Sum(item => item.TicketPos)),
            Round(items.Sum(item => item.Total)));

        return new FoodCheckReportDto(items, totals);
    }

    public async Task<SalesAnalysisAmountDto> GetFoodCheckTotalAsync(
        SalesAnalysisDateRangeRequest request,
        FoodCheckTotalKind totalKind,
        CancellationToken cancellationToken)
    {
        var report = await GetFoodCheckReportAsync(request, cancellationToken);
        var amount = totalKind switch
        {
            FoodCheckTotalKind.Metropol => report.Totals.Metropol,
            FoodCheckTotalKind.Multinet => report.Totals.Multinet,
            FoodCheckTotalKind.Setcard => report.Totals.Setcard,
            FoodCheckTotalKind.SodexoKupon => report.Totals.SodexoKupon,
            FoodCheckTotalKind.SodexoPos => report.Totals.SodexoPos,
            FoodCheckTotalKind.TicketKupon => report.Totals.TicketKupon,
            FoodCheckTotalKind.TicketPos => report.Totals.TicketPos,
            _ => report.Totals.Total
        };

        return new SalesAnalysisAmountDto(
            totalKind.ToString(),
            GetFoodCheckTotalName(totalKind),
            amount);
    }

    public async Task<MyoSalesReportDto> GetMyoSalesReportAsync(
        SalesAnalysisDateRangeRequest request,
        CancellationToken cancellationToken)
    {
        var (startDate, endDateExclusive) = NormalizeDateRange(request);

        const string sql = """
            WITH MyoDocuments AS (
                SELECT
                    CAST(MIN(sth.sth_tarih) AS date) AS DocumentDate,
                    COALESCE(sth.sth_cikis_depo_no, 0) AS BranchNo,
                    COALESCE(sth.sth_evrakno_seri, '') AS DocumentSerie,
                    COALESCE(sth.sth_evrakno_sira, 0) AS DocumentOrderNo,
                    TRY_CONVERT(uniqueidentifier, MIN(CONVERT(nvarchar(36), sth.sth_fat_uid))) AS InvoiceGuid
                FROM STOK_HAREKETLERI sth WITH (NOLOCK)
                WHERE sth.sth_evrakno_seri = N'MYO'
                  AND sth.sth_tarih >= @startDate
                  AND sth.sth_tarih < @endDateExclusive
                  AND (@warehouseNo IS NULL OR sth.sth_cikis_depo_no = @warehouseNo)
                GROUP BY
                    COALESCE(sth.sth_cikis_depo_no, 0),
                    COALESCE(sth.sth_evrakno_seri, ''),
                    COALESCE(sth.sth_evrakno_sira, 0)
            )
            SELECT
                doc.DocumentDate,
                doc.BranchNo,
                COALESCE(w.dep_adi, '') AS BranchName,
                doc.DocumentSerie,
                doc.DocumentOrderNo,
                doc.InvoiceGuid,
                COALESCE(cha.cha_kod, '') AS CustomerCode,
                COALESCE(cha.cha_belge_no, '') AS DocumentNo,
                COALESCE(exp.egk_evracik1, '') AS Description1,
                COALESCE(exp.egk_evracik2, '') AS Description2,
                COALESCE(exp.egk_evracik3, '') AS PaymentDescription,
                COALESCE(cha.cha_aratoplam, 0) AS SubTotal,
                COALESCE(cha.cha_ft_iskonto1, 0)
                    + COALESCE(cha.cha_ft_iskonto2, 0)
                    + COALESCE(cha.cha_ft_iskonto3, 0)
                    + COALESCE(cha.cha_ft_iskonto4, 0)
                    + COALESCE(cha.cha_ft_iskonto5, 0)
                    + COALESCE(cha.cha_ft_iskonto6, 0) AS DiscountTotal,
                COALESCE(cha.cha_vergi1, 0)
                    + COALESCE(cha.cha_vergi2, 0)
                    + COALESCE(cha.cha_vergi3, 0)
                    + COALESCE(cha.cha_vergi4, 0)
                    + COALESCE(cha.cha_vergi5, 0)
                    + COALESCE(cha.cha_vergi6, 0)
                    + COALESCE(cha.cha_vergi7, 0)
                    + COALESCE(cha.cha_vergi8, 0)
                    + COALESCE(cha.cha_vergi9, 0)
                    + COALESCE(cha.cha_vergi10, 0) AS TotalTax,
                COALESCE(cha.cha_meblag, 0) AS Amount
            FROM MyoDocuments doc
            LEFT JOIN DEPOLAR w WITH (NOLOCK)
                ON doc.BranchNo = w.dep_no
            OUTER APPLY (
                SELECT TOP (1)
                    movement.*
                FROM CARI_HESAP_HAREKETLERI movement WITH (NOLOCK)
                WHERE (doc.InvoiceGuid IS NOT NULL AND movement.cha_Guid = doc.InvoiceGuid)
                   OR (movement.cha_evrakno_seri = doc.DocumentSerie AND movement.cha_evrakno_sira = doc.DocumentOrderNo)
                ORDER BY
                    CASE WHEN doc.InvoiceGuid IS NOT NULL AND movement.cha_Guid = doc.InvoiceGuid THEN 0 ELSE 1 END,
                    movement.cha_satir_no
            ) cha
            OUTER APPLY (
                SELECT TOP (1)
                    description.*
                FROM EVRAK_ACIKLAMALARI description WITH (NOLOCK)
                WHERE description.egk_evr_seri = doc.DocumentSerie
                  AND description.egk_evr_sira = doc.DocumentOrderNo
                ORDER BY description.egk_create_date DESC
            ) exp
            ORDER BY
                doc.DocumentDate,
                doc.BranchNo,
                doc.DocumentOrderNo;
            """;

        var items = await ExecuteReaderAsync(
            mikroDbContext.Database.GetDbConnection(),
            sql,
            command =>
            {
                AddParameter(command, "@startDate", startDate);
                AddParameter(command, "@endDateExclusive", endDateExclusive);
                AddParameter(command, "@warehouseNo", request.WarehouseNo);
            },
            reader =>
            {
                var subTotal = Round(ReadDouble(reader, "SubTotal"));
                var discountTotal = Round(ReadDouble(reader, "DiscountTotal"));
                var netAmount = Round(subTotal - discountTotal);

                return new MyoSalesReportItemDto(
                    ReadDateTime(reader, "DocumentDate"),
                    ReadInt(reader, "BranchNo"),
                    ReadString(reader, "BranchName"),
                    ReadString(reader, "DocumentSerie"),
                    ReadInt(reader, "DocumentOrderNo"),
                    ReadGuid(reader, "InvoiceGuid"),
                    ReadString(reader, "CustomerCode"),
                    ReadString(reader, "DocumentNo"),
                    ReadString(reader, "Description1"),
                    ReadString(reader, "Description2"),
                    ReadString(reader, "PaymentDescription"),
                    subTotal,
                    discountTotal,
                    netAmount,
                    Round(ReadDouble(reader, "TotalTax")),
                    Round(ReadDouble(reader, "Amount")));
            },
            cancellationToken);

        return new MyoSalesReportDto(
            items,
            Round(items.Sum(item => item.NetAmount)),
            Round(items.Sum(item => item.TotalTax)),
            Round(items.Sum(item => item.Amount)),
            Round(items
                .Where(item => string.Equals(item.PaymentDescription, "Kapida Nakit Odeme", StringComparison.OrdinalIgnoreCase))
                .Sum(item => item.Amount)),
            Round(items
                .Where(item => string.Equals(item.PaymentDescription, "Kapida Kredi Karti ile Odeme", StringComparison.OrdinalIgnoreCase))
                .Sum(item => item.Amount)));
    }

    public async Task<IReadOnlyCollection<MyoSalesByBranchItemDto>> GetMyoSalesByBranchAsync(
        SalesAnalysisDateRangeRequest request,
        CancellationToken cancellationToken)
    {
        var report = await GetMyoSalesReportAsync(request, cancellationToken);

        return report.Items
            .GroupBy(item => new
            {
                item.DocumentDate,
                item.BranchNo,
                item.BranchName
            })
            .Select(grouped => new MyoSalesByBranchItemDto(
                grouped.Key.DocumentDate,
                grouped.Key.BranchNo,
                grouped.Key.BranchName,
                Round(grouped.Sum(item => item.Amount))))
            .OrderBy(item => item.DocumentDate)
            .ThenBy(item => item.BranchName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.BranchNo)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<ZReportBankAnalysisItemDto>> GetZReportBankAnalysisAsync(
        SalesAnalysisDateRangeRequest request,
        CancellationToken cancellationToken)
    {
        var (startDate, endDateExclusive) = NormalizeDateRange(request);

        const string sql = """
            SELECT
                COALESCE(w.dep_adi, '') AS BranchName,
                crb.BranchNo,
                total.Date,
                total.ZNo,
                total.CashRegisterNo,
                bank.Bank,
                bank.BankAmount,
                bank.BankingNumber,
                COALESCE(crd.TerminalId, '') AS TerminalId,
                COALESCE(crd.MerchantNo, '') AS MerchantNo
            FROM ZReportTotals total WITH (NOLOCK)
            INNER JOIN CashRegisterBranches crb WITH (NOLOCK)
                ON total.CashRegisterNo = crb.CashRegisterNo
            INNER JOIN ZReportBankDetails bank WITH (NOLOCK)
                ON total.TotalId = bank.TotalId
            LEFT JOIN DEPOLAR w WITH (NOLOCK)
                ON crb.BranchNo = w.dep_no
            OUTER APPLY (
                SELECT TOP (1)
                    detail.TerminalId,
                    detail.MerchantNo
                FROM CashRegisterDetails detail WITH (NOLOCK)
                WHERE detail.Bank = bank.Bank
                  AND detail.CashRegisterNo = total.CashRegisterNo
                ORDER BY detail.Id
            ) crd
            WHERE total.Date >= @startDate
              AND total.Date < @endDateExclusive
              AND total.CashRegisterNo LIKE N'UB%'
              AND (@warehouseNo IS NULL OR crb.BranchNo = @warehouseNo)
            ORDER BY
                total.Date,
                crb.BranchNo,
                total.ZNo,
                bank.Bank;
            """;

        return await ExecuteReaderAsync(
            mikroDbContext.Database.GetDbConnection(),
            sql,
            command =>
            {
                AddParameter(command, "@startDate", startDate);
                AddParameter(command, "@endDateExclusive", endDateExclusive);
                AddParameter(command, "@warehouseNo", request.WarehouseNo);
            },
            reader => new ZReportBankAnalysisItemDto(
                ReadString(reader, "BranchName"),
                ReadInt(reader, "BranchNo"),
                ReadDateTime(reader, "Date"),
                ReadInt(reader, "ZNo"),
                ReadString(reader, "CashRegisterNo"),
                ReadString(reader, "Bank"),
                Round(ReadDouble(reader, "BankAmount")),
                ReadInt(reader, "BankingNumber"),
                ReadString(reader, "TerminalId"),
                ReadString(reader, "MerchantNo")),
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<DiscountCardDetailItemDto>> GetDiscountCardDetailsAsync(
        SalesAnalysisDateRangeRequest request,
        CancellationToken cancellationToken)
    {
        var (startDate, endDateExclusive) = NormalizeDateRange(request);

        const string usageSql = """
            SELECT
                COALESCE(card.CardNumber, '') AS CardNumber,
                total.BranchNo,
                COALESCE(w.dep_adi, '') AS BranchName,
                SUM(card.UsageCount) AS UsageCount
            FROM TurnoverDiscountCardDetails card WITH (NOLOCK)
            INNER JOIN TurnoverTotals total WITH (NOLOCK)
                ON card.TurnoverId = total.TurnoverId
            LEFT JOIN DEPOLAR w WITH (NOLOCK)
                ON total.BranchNo = w.dep_no
            WHERE total.TurnoverDate >= @startDate
              AND total.TurnoverDate < @endDateExclusive
              AND (@warehouseNo IS NULL OR total.BranchNo = @warehouseNo)
            GROUP BY
                COALESCE(card.CardNumber, ''),
                total.BranchNo,
                COALESCE(w.dep_adi, '')
            ORDER BY
                COALESCE(w.dep_adi, ''),
                total.BranchNo,
                COALESCE(card.CardNumber, '');
            """;

        const string invoiceSql = """
            SELECT
                TRY_CONVERT(int, invoice.Sube) AS BranchNo,
                LTRIM(RTRIM(COALESCE(invoice.KartNumarasi, ''))) AS CardNumber,
                SUM(COALESCE(invoice.Toplam, 0) + COALESCE(invoice.ToplamKdv, 0)) AS UsageTotal
            FROM dbo.PosFaturas invoice WITH (NOLOCK)
            WHERE invoice.Tarih >= @startDate
              AND invoice.Tarih < @endDateExclusive
              AND NULLIF(LTRIM(RTRIM(COALESCE(invoice.KartNumarasi, ''))), '') IS NOT NULL
              AND (@warehouseNo IS NULL OR TRY_CONVERT(int, invoice.Sube) = @warehouseNo)
            GROUP BY
                TRY_CONVERT(int, invoice.Sube),
                LTRIM(RTRIM(COALESCE(invoice.KartNumarasi, '')));
            """;

        var usages = await ExecuteReaderAsync(
            mikroDbContext.Database.GetDbConnection(),
            usageSql,
            command =>
            {
                AddParameter(command, "@startDate", startDate);
                AddParameter(command, "@endDateExclusive", endDateExclusive);
                AddParameter(command, "@warehouseNo", request.WarehouseNo);
            },
            reader => new DiscountCardUsageRow(
                ReadString(reader, "CardNumber"),
                ReadInt(reader, "BranchNo"),
                ReadString(reader, "BranchName"),
                ReadInt(reader, "UsageCount")),
            cancellationToken);

        var invoiceTotals = await ExecuteReaderAsync(
            furpaDbContext.Database.GetDbConnection(),
            invoiceSql,
            command =>
            {
                AddParameter(command, "@startDate", startDate);
                AddParameter(command, "@endDateExclusive", endDateExclusive);
                AddParameter(command, "@warehouseNo", request.WarehouseNo);
            },
            reader => new DiscountCardInvoiceTotalRow(
                ReadInt(reader, "BranchNo"),
                ReadString(reader, "CardNumber"),
                Round(ReadDouble(reader, "UsageTotal"))),
            cancellationToken);

        var invoiceTotalsByKey = invoiceTotals
            .GroupBy(item => new DiscountCardKey(item.BranchNo, NormalizeCardNumber(item.CardNumber)))
            .ToDictionary(
                grouped => grouped.Key,
                grouped => Round(grouped.Sum(item => item.UsageTotal)));

        return usages
            .Select(item =>
            {
                invoiceTotalsByKey.TryGetValue(
                    new DiscountCardKey(item.BranchNo, NormalizeCardNumber(item.CardNumber)),
                    out var usageTotal);

                return new DiscountCardDetailItemDto(
                    item.CardNumber,
                    item.BranchNo,
                    item.BranchName,
                    item.UsageCount,
                    usageTotal);
            })
            .ToArray();
    }

    public async Task<IReadOnlyCollection<MissingTurnoverBranchItemDto>> GetMissingTurnoverBranchesAsync(
        SalesAnalysisDateRangeRequest request,
        CancellationToken cancellationToken)
    {
        var (startDate, endDateExclusive) = NormalizeDateRange(request);

        const string sql = """
            SELECT
                dep.dep_no AS BranchNo,
                COALESCE(dep.dep_adi, '') AS BranchName,
                COALESCE(dep.dep_bolge_kodu, '') AS Region
            FROM DEPOLAR dep WITH (NOLOCK)
            WHERE dep.dep_tipi = 1
              AND dep.dep_no > 100
              AND (@warehouseNo IS NULL OR dep.dep_no = @warehouseNo)
              AND NOT EXISTS (
                  SELECT 1
                  FROM TurnoverTotals total WITH (NOLOCK)
                  WHERE total.BranchNo = dep.dep_no
                    AND total.TurnoverDate >= @startDate
                    AND total.TurnoverDate < @endDateExclusive
              )
            ORDER BY
                dep.dep_no,
                COALESCE(dep.dep_adi, '');
            """;

        return await ExecuteReaderAsync(
            mikroDbContext.Database.GetDbConnection(),
            sql,
            command =>
            {
                AddParameter(command, "@startDate", startDate);
                AddParameter(command, "@endDateExclusive", endDateExclusive);
                AddParameter(command, "@warehouseNo", request.WarehouseNo);
            },
            reader => new MissingTurnoverBranchItemDto(
                ReadInt(reader, "BranchNo"),
                ReadString(reader, "BranchName"),
                ReadString(reader, "Region")),
            cancellationToken);
    }

    private async Task<IReadOnlyCollection<T>> ExecuteReaderAsync<T>(
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
            command.CommandTimeout = 300;
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

    private static (DateTime StartDate, DateTime EndDateExclusive) NormalizeDateRange(
        SalesAnalysisDateRangeRequest request)
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

        return (startDate, endDate.AddDays(1));
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

    private static DateTime ReadDateTime(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? default
            : Convert.ToDateTime(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static Guid? ReadGuid(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetValue(ordinal);

        return value switch
        {
            Guid guid => guid,
            _ when Guid.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed) => parsed,
            _ => null
        };
    }

    private static string ReadString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? string.Empty
            : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
    }

    private static string GetFoodCheckTotalName(FoodCheckTotalKind totalKind) =>
        totalKind switch
        {
            FoodCheckTotalKind.Metropol => "Metropol",
            FoodCheckTotalKind.Multinet => "Multinet",
            FoodCheckTotalKind.Setcard => "Setcard",
            FoodCheckTotalKind.SodexoKupon => "Sodexo Kupon",
            FoodCheckTotalKind.SodexoPos => "Sodexo POS",
            FoodCheckTotalKind.TicketKupon => "Ticket Kupon",
            FoodCheckTotalKind.TicketPos => "Ticket POS",
            _ => "Yemek Ceki Toplam"
        };

    public async Task<IReadOnlyCollection<CategoryYearlyGrowthItemDto>> GetCategoryYearlyGrowthAsync(
        CategoryYearlyGrowthRequest request,
        CancellationToken cancellationToken)
    {
        ValidateSingleMonthRange(request.StartDate, request.EndDate);

        var currentStart = request.StartDate.ToDateTime(TimeOnly.MinValue);
        var currentEndExclusive = request.EndDate.ToDateTime(TimeOnly.MinValue).AddDays(1);

        var prevStart = request.StartDate.AddYears(-1).ToDateTime(TimeOnly.MinValue);
        var prevEndExclusive = request.EndDate.AddYears(-1).ToDateTime(TimeOnly.MinValue).AddDays(1);

        decimal goalPercent = 0m;
        const string goalSql = """
            SELECT TOP (1) CAST(COALESCE(HedeflenenCiroOrani_Yuzde, 0) AS decimal(18,2))
            FROM dbo.HEDEF_TANIMLARI WITH (NOLOCK)
            WHERE Yil = @year
              AND Ay_Sira = @month
              AND Sube_No IS NULL
            ORDER BY Id;
            """;

        var connection = mikroDbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var goalCmd = connection.CreateCommand();
            goalCmd.CommandText = goalSql;
            AddParameter(goalCmd, "@year", request.StartDate.Year);
            AddParameter(goalCmd, "@month", request.StartDate.Month);
            var goalObj = await goalCmd.ExecuteScalarAsync(cancellationToken);
            if (goalObj is not null && goalObj != DBNull.Value)
            {
                goalPercent = Convert.ToDecimal(goalObj, CultureInfo.InvariantCulture);
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        const string categoryDataSql = """
            SELECT
                COALESCE(dbo.fn_KategoriIsmi(SUBSTRING(t.ktg_kod, 1, 2)), SUBSTRING(t.ktg_kod, 1, 2)) AS CategoryName,
                COALESCE(dbo.fn_KategoriIsmi(SUBSTRING(t.ktg_kod, 1, 5)), SUBSTRING(t.ktg_kod, 1, 5)) AS SubCategoryName,
                COALESCE(dbo.fn_KategoriIsmi(SUBSTRING(t.ktg_kod, 1, 8)), SUBSTRING(t.ktg_kod, 1, 8)) AS SegmentName,
                COALESCE(dbo.fn_KategoriIsmi(SUBSTRING(t.ktg_kod, 1, 11)), SUBSTRING(t.ktg_kod, 1, 11)) AS SubSegmentName,
                SUM(COALESCE(h.sth_miktar, 0)) AS Quantity,
                SUM((COALESCE(h.sth_tutar, 0) + COALESCE(h.sth_vergi, 0))
                  - (COALESCE(h.sth_iskonto1, 0) + COALESCE(h.sth_iskonto2, 0) + COALESCE(h.sth_iskonto3, 0)
                   + COALESCE(h.sth_iskonto4, 0) + COALESCE(h.sth_iskonto5, 0) + COALESCE(h.sth_iskonto6, 0))) AS TotalAmount
            FROM dbo.STOK_HAREKETLERI AS h WITH (NOLOCK)
            INNER JOIN dbo.STOKLAR AS s WITH (NOLOCK) ON s.sto_kod = h.sth_stok_kod
            INNER JOIN dbo.STOK_KATEGORILERI AS t WITH (NOLOCK) ON s.sto_kategori_kodu = t.ktg_kod
            WHERE h.sth_tip = 1
              AND h.sth_cins = 1
              AND h.sth_normal_iade = 0
              AND (h.sth_evraktip = 1 OR h.sth_evraktip = 4)
              AND h.sth_tarih >= @startDate AND h.sth_tarih < @endDateExclusive
            GROUP BY
                SUBSTRING(t.ktg_kod, 1, 2),
                SUBSTRING(t.ktg_kod, 1, 5),
                SUBSTRING(t.ktg_kod, 1, 8),
                SUBSTRING(t.ktg_kod, 1, 11);
            """;

        var currentRows = await ExecuteReaderAsync(
            mikroDbContext.Database.GetDbConnection(),
            categoryDataSql,
            command =>
            {
                AddParameter(command, "@startDate", currentStart);
                AddParameter(command, "@endDateExclusive", currentEndExclusive);
            },
            reader => new CategoryRawRow(
                ReadString(reader, "CategoryName"),
                ReadString(reader, "SubCategoryName"),
                ReadString(reader, "SegmentName"),
                ReadString(reader, "SubSegmentName"),
                Round(ReadDouble(reader, "Quantity")),
                Round(ReadDouble(reader, "TotalAmount"))),
            cancellationToken);

        var prevRows = await ExecuteReaderAsync(
            mikroDbContext.Database.GetDbConnection(),
            categoryDataSql,
            command =>
            {
                AddParameter(command, "@startDate", prevStart);
                AddParameter(command, "@endDateExclusive", prevEndExclusive);
            },
            reader => new CategoryRawRow(
                ReadString(reader, "CategoryName"),
                ReadString(reader, "SubCategoryName"),
                ReadString(reader, "SegmentName"),
                ReadString(reader, "SubSegmentName"),
                Round(ReadDouble(reader, "Quantity")),
                Round(ReadDouble(reader, "TotalAmount"))),
            cancellationToken);

        var currentMap = currentRows.ToDictionary(r => $"{r.CategoryName}|{r.SubCategoryName}|{r.SegmentName}|{r.SubSegmentName}");
        var prevMap = prevRows.ToDictionary(r => $"{r.CategoryName}|{r.SubCategoryName}|{r.SegmentName}|{r.SubSegmentName}");

        var allKeys = currentMap.Keys.Union(prevMap.Keys).OrderBy(k => k).ToArray();
        var results = new List<CategoryYearlyGrowthItemDto>();

        foreach (var key in allKeys)
        {
            currentMap.TryGetValue(key, out var curr);
            prevMap.TryGetValue(key, out var prev);

            var cat = curr?.CategoryName ?? prev?.CategoryName ?? string.Empty;
            var subCat = curr?.SubCategoryName ?? prev?.SubCategoryName ?? string.Empty;
            var seg = curr?.SegmentName ?? prev?.SegmentName ?? string.Empty;
            var subSeg = curr?.SubSegmentName ?? prev?.SubSegmentName ?? string.Empty;

            var currQty = curr?.Quantity ?? 0d;
            var currAmt = curr?.TotalAmount ?? 0d;
            var prevQty = prev?.Quantity ?? 0d;
            var prevAmt = prev?.TotalAmount ?? 0d;

            var goalAmt = Math.Round(prevAmt * (1d + ((double)goalPercent / 100d)), 2);
            var realizedRatio = goalAmt > 0d
                ? Math.Round((currAmt / goalAmt) * 100d, 2)
                : 0d;

            results.Add(new CategoryYearlyGrowthItemDto(
                cat,
                subCat,
                seg,
                subSeg,
                currQty,
                currAmt,
                prevQty,
                prevAmt,
                goalPercent,
                (decimal)goalAmt,
                (decimal)realizedRatio));
        }

        return results;
    }

    public async Task<IReadOnlyCollection<CategorySalesDetailedItemDto>> GetCategorySalesDetailedAsync(
        CategorySalesDetailedRequest request,
        CancellationToken cancellationToken)
    {
        var startDate = request.StartDate.ToDateTime(TimeOnly.MinValue);
        var endDateExclusive = request.EndDate.ToDateTime(TimeOnly.MinValue).AddDays(1);
        var take = request.Take <= 0 ? 1000 : Math.Min(request.Take, 5000);

        const string sql = """
            SELECT TOP (@take)
                COALESCE(w.dep_bolge_kodu, '') AS Region,
                w.dep_no AS BranchNo,
                COALESCE(w.dep_adi, '') AS BranchName,
                COALESCE(sec.sek_isim, '') AS SectorName,
                COALESCE(ray.ryn_isim, '') AS RayonName,
                COALESCE(cat.ktg_isim, '') AS CategoryName,
                s.sto_kod AS ProductCode,
                COALESCE(s.sto_isim, '') AS ProductName,
                SUM(COALESCE(h.sth_miktar, 0)) AS Quantity,
                SUM(COALESCE(h.sth_tutar, 0) + COALESCE(h.sth_vergi, 0)) AS GrossAmount,
                SUM(COALESCE(h.sth_iskonto1, 0) + COALESCE(h.sth_iskonto2, 0) + COALESCE(h.sth_iskonto3, 0)
                  + COALESCE(h.sth_iskonto4, 0) + COALESCE(h.sth_iskonto5, 0) + COALESCE(h.sth_iskonto6, 0)) AS DiscountAmount,
                SUM((COALESCE(h.sth_tutar, 0) + COALESCE(h.sth_vergi, 0))
                  - (COALESCE(h.sth_iskonto1, 0) + COALESCE(h.sth_iskonto2, 0) + COALESCE(h.sth_iskonto3, 0)
                   + COALESCE(h.sth_iskonto4, 0) + COALESCE(h.sth_iskonto5, 0) + COALESCE(h.sth_iskonto6, 0))) AS NetAmount
            FROM dbo.STOK_HAREKETLERI AS h WITH (NOLOCK)
            INNER JOIN dbo.DEPOLAR AS w WITH (NOLOCK) ON h.sth_cikis_depo_no = w.dep_no
            INNER JOIN dbo.STOKLAR AS s WITH (NOLOCK) ON s.sto_kod = h.sth_stok_kod
            LEFT JOIN dbo.STOK_SEKTORLERI AS sec WITH (NOLOCK) ON s.sto_sektor_kodu = sec.sek_kod
            LEFT JOIN dbo.STOK_REYONLARI AS ray WITH (NOLOCK) ON s.sto_reyon_kodu = ray.ryn_kod
            LEFT JOIN dbo.STOK_KATEGORILERI AS cat WITH (NOLOCK) ON s.sto_kategori_kodu = cat.ktg_kod
            WHERE h.sth_tip = 1
              AND h.sth_cins = 1
              AND h.sth_normal_iade = 0
              AND (h.sth_evraktip = 1 OR h.sth_evraktip = 4)
              AND h.sth_tarih >= @startDate AND h.sth_tarih < @endDateExclusive
              AND (@branchNo IS NULL OR w.dep_no = @branchNo)
              AND (@region IS NULL OR w.dep_bolge_kodu = @region)
              AND (@sectorCode IS NULL OR s.sto_sektor_kodu = @sectorCode)
              AND (@rayonCode IS NULL OR s.sto_reyon_kodu = @rayonCode)
              AND (@categoryCode IS NULL OR s.sto_kategori_kodu LIKE @categoryCode + '%')
              AND (@productCode IS NULL OR s.sto_kod = @productCode)
            GROUP BY
                w.dep_bolge_kodu,
                w.dep_no,
                w.dep_adi,
                sec.sek_isim,
                ray.ryn_isim,
                cat.ktg_isim,
                s.sto_kod,
                s.sto_isim
            ORDER BY
                NetAmount DESC,
                ProductName;
            """;

        return await ExecuteReaderAsync(
            mikroDbContext.Database.GetDbConnection(),
            sql,
            command =>
            {
                AddParameter(command, "@startDate", startDate);
                AddParameter(command, "@endDateExclusive", endDateExclusive);
                AddParameter(command, "@branchNo", request.BranchNo);
                AddParameter(command, "@region", string.IsNullOrWhiteSpace(request.Region) ? null : request.Region);
                AddParameter(command, "@sectorCode", string.IsNullOrWhiteSpace(request.SectorCode) ? null : request.SectorCode);
                AddParameter(command, "@rayonCode", string.IsNullOrWhiteSpace(request.RayonCode) ? null : request.RayonCode);
                AddParameter(command, "@categoryCode", string.IsNullOrWhiteSpace(request.CategoryCode) ? null : request.CategoryCode);
                AddParameter(command, "@productCode", string.IsNullOrWhiteSpace(request.ProductCode) ? null : request.ProductCode);
                AddParameter(command, "@take", take);
            },
            reader => new CategorySalesDetailedItemDto(
                ReadString(reader, "Region"),
                ReadInt(reader, "BranchNo"),
                ReadString(reader, "BranchName"),
                ReadString(reader, "SectorName"),
                ReadString(reader, "RayonName"),
                ReadString(reader, "CategoryName"),
                ReadString(reader, "ProductCode"),
                ReadString(reader, "ProductName"),
                Round(ReadDouble(reader, "Quantity")),
                Round(ReadDouble(reader, "GrossAmount")),
                Round(ReadDouble(reader, "DiscountAmount")),
                Round(ReadDouble(reader, "NetAmount"))),
            cancellationToken);
    }

    private sealed record CategoryRawRow(
        string CategoryName,
        string SubCategoryName,
        string SegmentName,
        string SubSegmentName,
        double Quantity,
        double TotalAmount);

    private static void ValidateSingleMonthRange(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("Bitis tarihi baslangic tarihinden once olamaz.", nameof(endDate));
        }

        if (startDate.Year != endDate.Year || startDate.Month != endDate.Month)
        {
            throw new ArgumentException(
                "Yillik kategori karsilastirmasi ayni yil ve ay icindeki bir tarih araligi icin calistirilmalidir.",
                nameof(endDate));
        }
    }

    private static string NormalizeCardNumber(string value) =>
        value.Trim().ToUpperInvariant();

    private static double Round(double value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed record DiscountCardUsageRow(
        string CardNumber,
        int BranchNo,
        string BranchName,
        int UsageCount);

    private sealed record DiscountCardInvoiceTotalRow(
        int BranchNo,
        string CardNumber,
        double UsageTotal);

    private sealed record DiscountCardKey(
        int BranchNo,
        string CardNumber);
}

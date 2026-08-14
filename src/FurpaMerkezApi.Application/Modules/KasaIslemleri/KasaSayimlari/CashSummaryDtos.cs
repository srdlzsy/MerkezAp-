using System.Globalization;

namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari;

public sealed record CashSummaryReportItemDto(
    int WarehouseNo,
    string WarehouseName,
    double CashAmount,
    int CashAmountQuantity,
    double Akbank,
    int AkbankQuantity,
    double Halkbank,
    int HalkbankQuantity,
    double IsBankasi,
    int IsBankasiQuantity,
    double Teb,
    int TebQuantity,
    double YapiKredi,
    int YapiKrediQuantity,
    double ZiraatBankasi,
    int ZiraatBankasiQuantity,
    double Metropol,
    int MetropolQuantity,
    double Multinet,
    int MultinetQuantity,
    double Setcard,
    int SetcardQuantity,
    double SodexoKupon,
    int SodexoKuponQuantity,
    double SodexoPos,
    int SodexoPosQuantity,
    double TicketKupon,
    int TicketKuponQuantity,
    double TicketPos,
    int TicketPosQuantity,
    double ExpenseCompass,
    int ExpenseCompassQuantity,
    double StoreExpense,
    int StoreExpenseQuantity);

public sealed record CashSummaryListItemDto(
    int WarehouseNo,
    string WarehouseName,
    string DocumentSerie,
    int DocumentOrderNo,
    int CashNo,
    int ZReportNo,
    int CashierNo,
    int ManagerNo,
    DateTime SummaryDate,
    double Total);

public sealed record CashSummaryDetailItemDto(
    string TypeName,
    string PaymentName,
    int PaymentTypeId,
    int PaymentTypeNo,
    string AccountCode,
    string TerminalId,
    string Source,
    string Category,
    int SlipNumber,
    double Amount,
    string Description)
{
    public string PaymentTypeKey => CashSummaryDisplayNameFormatter.BuildPaymentTypeKey(
        PaymentTypeNo,
        AccountCode,
        TerminalId);
}

public sealed record BanknoteMovementItemDto(
    double Value,
    int BanknoteType,
    int Quantity,
    double Total)
{
    public string BanknoteTypeName => CashSummaryDisplayNameFormatter.FormatMoney(Value);
}

public sealed record BanknoteTypeItemDto(
    double Value,
    double Quantity,
    double Total,
    int BanknoteType)
{
    public string BanknoteTypeName => CashSummaryDisplayNameFormatter.FormatMoney(Value);
}

public sealed record GiftCheckMovementItemDto(
    double Value,
    int GiftCheckType,
    int Quantity,
    double Total)
{
    public string GiftCheckTypeName => $"Hediye Çeki {CashSummaryDisplayNameFormatter.FormatMoney(Value)}";
}

public sealed record GiftCheckTypeItemDto(
    double Value,
    double Quantity,
    double Total,
    int GiftCheckType)
{
    public string GiftCheckTypeName => $"Hediye Çeki {CashSummaryDisplayNameFormatter.FormatMoney(Value)}";
}

public sealed record PaymentTypeItemDto(
    string PaymentName,
    int PaymentTypeNo,
    string TerminalId,
    string AccountCode,
    int SlipNumber,
    double AmountValue)
{
    public int PaymentTypeId => PaymentTypeNo;

    public string PaymentTypeKey => CashSummaryDisplayNameFormatter.BuildPaymentTypeKey(
        PaymentTypeNo,
        AccountCode,
        TerminalId);
}

public sealed record CashierItemDto(
    int CashierId,
    int CreateUser,
    DateTime CreateDate,
    int UpdateUser,
    DateTime UpdateDate,
    int CashierCode,
    string CashierName,
    string CashierPassword,
    string CashierAuthorization,
    bool CashierState);

public sealed record CashierSearchItemDto(
    int CashierCode,
    string CashierName,
    string CashierPassword,
    string CashierAuthorization,
    bool CashierState);

public sealed record CashRegistryItemDto(
    int DetailId,
    int BranchNo,
    int CashRegisterNo,
    byte CashRegisterType,
    string CashRegisterTypeName,
    string CashRegisterTypeDescription);

public sealed record CashRegisterDetailDto(
    int Id,
    string CashRegisterNo,
    string Bank,
    string TerminalId,
    string MerchantNo,
    int? CashNo);

internal static class CashSummaryDisplayNameFormatter
{
    public static string FormatMoney(double value)
    {
        var format = Math.Abs(value % 1) < 0.000001 ? "0" : "0.##";
        return $"{value.ToString(format, CultureInfo.InvariantCulture)} TL";
    }

    public static string BuildPaymentTypeKey(int paymentTypeNo, string accountCode, string terminalId) =>
        string.Join(
            "|",
            paymentTypeNo.ToString(CultureInfo.InvariantCulture),
            NormalizeKeyPart(accountCode),
            NormalizeKeyPart(terminalId));

    private static string NormalizeKeyPart(string? value) =>
        (value ?? string.Empty).Trim().ToUpperInvariant();
}

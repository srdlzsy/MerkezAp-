using System.Globalization;

namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGonderimi;

internal static class InvoiceSendingAmountFormatter
{
    public static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static string FormatMoneyAmount(decimal value) =>
        RoundMoney(value).ToString("0.00", CultureInfo.InvariantCulture);

    public static decimal RoundAllowanceAmount(decimal value) =>
        Math.Round(value, 4, MidpointRounding.AwayFromZero);

    public static string FormatAllowanceAmount(decimal value) =>
        RoundAllowanceAmount(value).ToString("0.00##", CultureInfo.InvariantCulture);
}

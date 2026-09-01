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

    public static InvoiceSendingTotals CalculateTotals(
        decimal grossTotal,
        decimal discountTotal,
        decimal taxTotal,
        decimal chargeTotal)
    {
        var roundedGrossTotal = RoundMoney(grossTotal);
        var roundedDiscountTotal = RoundMoney(Math.Max(0m, discountTotal));
        var lineExtensionTotal = RoundMoney(Math.Max(0m, roundedGrossTotal - roundedDiscountTotal));
        var roundedTaxTotal = RoundMoney(taxTotal);
        var roundedChargeTotal = RoundMoney(chargeTotal);

        return new InvoiceSendingTotals(
            roundedGrossTotal,
            roundedDiscountTotal,
            lineExtensionTotal,
            roundedTaxTotal,
            roundedChargeTotal,
            RoundMoney(lineExtensionTotal + roundedTaxTotal + roundedChargeTotal));
    }
}

internal sealed record InvoiceSendingTotals(
    decimal GrossTotal,
    decimal DiscountTotal,
    decimal LineExtensionTotal,
    decimal TaxTotal,
    decimal ChargeTotal,
    decimal PayableTotal);

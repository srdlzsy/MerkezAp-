namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaSayimlari;

internal enum CashSummaryReportCategory
{
    Unknown = 0,
    CashAmount,
    Akbank,
    Halkbank,
    IsBankasi,
    Teb,
    YapiKredi,
    ZiraatBankasi,
    Metropol,
    Multinet,
    Setcard,
    SodexoKupon,
    SodexoPos,
    TicketKupon,
    TicketPos,
    ExpenseCompass,
    StoreExpense
}

internal static class CashSummaryCategoryMatcher
{
    public static CashSummaryReportCategory ResolveReportCategory(
        string? typeName,
        string? paymentName,
        bool isStoreExpense)
    {
        if (isStoreExpense)
        {
            return CashSummaryReportCategory.StoreExpense;
        }

        var normalized = Normalize(!string.IsNullOrWhiteSpace(typeName) ? typeName : paymentName);

        if (normalized.Length == 0)
        {
            return CashSummaryReportCategory.Unknown;
        }

        if (IsCash(normalized))
        {
            return CashSummaryReportCategory.CashAmount;
        }

        if (Contains(normalized, "akbank"))
        {
            return CashSummaryReportCategory.Akbank;
        }

        if (Contains(normalized, "halk"))
        {
            return CashSummaryReportCategory.Halkbank;
        }

        if (Contains(normalized, "isbank") || Contains(normalized, "is bank"))
        {
            return CashSummaryReportCategory.IsBankasi;
        }

        if (Contains(normalized, "teb"))
        {
            return CashSummaryReportCategory.Teb;
        }

        if (Contains(normalized, "yapi") && Contains(normalized, "kredi"))
        {
            return CashSummaryReportCategory.YapiKredi;
        }

        if (Contains(normalized, "ziraat"))
        {
            return CashSummaryReportCategory.ZiraatBankasi;
        }

        if (Contains(normalized, "metropol"))
        {
            return CashSummaryReportCategory.Metropol;
        }

        if (Contains(normalized, "multinet"))
        {
            return CashSummaryReportCategory.Multinet;
        }

        if (Contains(normalized, "setcard"))
        {
            return CashSummaryReportCategory.Setcard;
        }

        if (Contains(normalized, "sodexo") && Contains(normalized, "kupon"))
        {
            return CashSummaryReportCategory.SodexoKupon;
        }

        if (Contains(normalized, "sodexo"))
        {
            return CashSummaryReportCategory.SodexoPos;
        }

        if (Contains(normalized, "ticket") && Contains(normalized, "kupon"))
        {
            return CashSummaryReportCategory.TicketKupon;
        }

        if (Contains(normalized, "ticket"))
        {
            return CashSummaryReportCategory.TicketPos;
        }

        if (Contains(normalized, "expense") || Contains(normalized, "masraf pusulasi"))
        {
            return CashSummaryReportCategory.ExpenseCompass;
        }

        if (Contains(normalized, "magaza gider") || Contains(normalized, "store expense"))
        {
            return CashSummaryReportCategory.StoreExpense;
        }

        return CashSummaryReportCategory.Unknown;
    }

    public static bool IsBankPaymentType(string? value)
    {
        var normalized = Normalize(value);

        return Contains(normalized, "akbank") ||
               Contains(normalized, "halk") ||
               Contains(normalized, "isbank") ||
               Contains(normalized, "is bank") ||
               Contains(normalized, "teb") ||
               (Contains(normalized, "yapi") && Contains(normalized, "kredi")) ||
               Contains(normalized, "ziraat");
    }

    public static bool IsBankPaymentMatch(string? paymentName, string? bankName)
    {
        var normalizedPaymentName = Normalize(paymentName);
        var normalizedBankName = Normalize(bankName);

        if (normalizedBankName.Length == 0)
        {
            return IsBankPaymentType(normalizedPaymentName);
        }

        if (Contains(normalizedPaymentName, normalizedBankName))
        {
            return true;
        }

        return normalizedBankName switch
        {
            var value when value.Contains("yapi") && value.Contains("kredi") =>
                Contains(normalizedPaymentName, "yapi") && Contains(normalizedPaymentName, "kredi"),
            var value when value.Contains("is") && value.Contains("bank") =>
                Contains(normalizedPaymentName, "isbank") || Contains(normalizedPaymentName, "is bank"),
            _ => false
        };
    }

    public static bool IsFoodCheckPaymentType(string? value)
    {
        var normalized = Normalize(value);

        return Contains(normalized, "metropol") ||
               Contains(normalized, "multinet") ||
               Contains(normalized, "setcard") ||
               Contains(normalized, "sodexo") ||
               Contains(normalized, "ticket");
    }

    public static bool IsOnlineSalesPaymentType(string? value) =>
        Contains(Normalize(value), "online");

    public static bool IsExpenseCompassPaymentType(string? value)
    {
        var normalized = Normalize(value);
        return Contains(normalized, "expense") || Contains(normalized, "masraf pusulasi");
    }

    public static bool IsStoreExpensePaymentType(string? value)
    {
        var normalized = Normalize(value);
        return Contains(normalized, "store expense") || Contains(normalized, "magaza gider") || Contains(normalized, "gider");
    }

    private static bool IsCash(string normalized) =>
        Contains(normalized, "cash") || Contains(normalized, "nakit");

    private static bool Contains(string value, string token) =>
        value.Contains(token, StringComparison.Ordinal);

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim()
            .Replace('İ', 'I')
            .ToLowerInvariant()
            .Replace('ı', 'i')
            .Replace('ş', 's')
            .Replace('ğ', 'g')
            .Replace('ü', 'u')
            .Replace('ö', 'o')
            .Replace('ç', 'c');
    }
}

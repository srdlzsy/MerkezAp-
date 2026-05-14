namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaCirolari;

public enum CashTurnoverSource
{
    New = 1,
    Old = 2,
    All = 3
}

public static class CashTurnoverSourceExtensions
{
    public static string ToApiValue(this CashTurnoverSource source) =>
        source switch
        {
            CashTurnoverSource.New => "new",
            CashTurnoverSource.Old => "old",
            CashTurnoverSource.All => "all",
            _ => "unknown"
        };
}

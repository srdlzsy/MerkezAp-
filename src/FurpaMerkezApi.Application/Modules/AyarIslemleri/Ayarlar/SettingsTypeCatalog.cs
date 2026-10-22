using System.Globalization;

namespace FurpaMerkezApi.Application.Modules.AyarIslemleri.Ayarlar;

public static class SettingsTypeCatalog
{
    private static readonly IReadOnlyCollection<SettingsTypeOptionDto> ScalesTypeOptions =
    [
        new(
            0,
            "cas-16",
            "CAS 16",
            "Terazi.plu formatinda CAS 16 terazi dosyasi uretir.",
            true),
        new(
            1,
            "cas-500",
            "CAS 500",
            "ART_STM.txt formatinda CAS 500 terazi dosyasi uretir.",
            true)
    ];

    private static readonly IReadOnlyCollection<SettingsTypeOptionDto> CashTypeOptions =
    [
        new(
            0,
            "standard-pos-cash-register",
            "Standart Kasa",
            "Subenin POSKON/MESAJ dosya islemlerine dahil edilen ana fiziksel kasasidir. Banka veya POS terminali degildir; terminal bilgileri ayri tanimlanir.",
            true),
        new(
            1,
            "additional-pos-cash-register",
            "Ek Kasa",
            "Subede ana kasaya ek olarak tanimli fiziksel kasadir. POSKON/MESAJ ve kasa hareketlerinde kendi kasa numarasiyla izlenir; banka/POS terminali ayri tanimlanir.",
            true)
    ];

    public static IReadOnlyCollection<SettingsTypeOptionDto> GetScalesTypeOptions() =>
        ScalesTypeOptions;

    public static IReadOnlyCollection<SettingsTypeOptionDto> GetCashTypeOptions() =>
        CashTypeOptions;

    public static SettingsTypeOptionDto ResolveScalesTypeOption(byte value) =>
        ScalesTypeOptions.FirstOrDefault(item => item.Value == value)
        ?? new SettingsTypeOptionDto(
            value,
            $"scales-type-{value.ToString(CultureInfo.InvariantCulture)}",
            $"Tanimlanmamis Terazi Tipi ({value.ToString(CultureInfo.InvariantCulture)})",
            "Bu terazi tipi katalogda yok; UI islem yaptirmadan once merkez tarafindan anlaminin netlestirilmesi gerekir.",
            false);

    public static SettingsTypeOptionDto ResolveCashTypeOption(byte value) =>
        CashTypeOptions.FirstOrDefault(item => item.Value == value)
        ?? new SettingsTypeOptionDto(
            value,
            $"cash-type-{value.ToString(CultureInfo.InvariantCulture)}",
            $"Tanimlanmamis Kasa Tipi ({value.ToString(CultureInfo.InvariantCulture)})",
            "Bu kasa tipi katalogda yok; UI islem yaptirmadan once merkez tarafindan anlaminin netlestirilmesi gerekir.",
            false);
}

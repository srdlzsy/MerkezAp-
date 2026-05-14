namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal static class AxataSynchronizationCatalog
{
    public static IReadOnlyCollection<AxataSynchronizationTaskDefinition> Definitions { get; } =
    [
        new(
            "firm-master-sync",
            "Firma Master Senkronizasyonu",
            "Cari hesap kayitlarini AXATA'ya gidecek master payload formatina donusturur.",
            "Mikro -> AXATA",
            false,
            "Mikro",
            "AXATA"),
        new(
            "product-master-sync",
            "Urun Master Senkronizasyonu",
            "Stok, barkod ve temel urun alanlarini AXATA payload formatina donusturur.",
            "Mikro -> AXATA",
            false,
            "Mikro",
            "AXATA"),
        new(
            "issued-warehouse-order-sync",
            "Verilen Depo Siparisi Senkronizasyonu",
            "Verilen depo siparislerini belge bazli toplayip senkronizasyon payload'ina cevirir.",
            "Mikro -> AXATA",
            true,
            "Mikro",
            "AXATA"),
        new(
            "company-receiving-sync",
            "Firma Mal Kabul Senkronizasyonu",
            "Firma mal kabul belgelerini manuel veya zamanli entegrasyon akisi icin hazirlar.",
            "Mikro -> AXATA",
            true,
            "Mikro",
            "AXATA"),
        new(
            "inventory-count-sync",
            "Sayim Sonucu Senkronizasyonu",
            "Sayim sonuc belgelerini senkronizasyon payload'ina donusturur.",
            "Mikro -> AXATA",
            true,
            "Mikro",
            "AXATA")
    ];

    public static AxataSynchronizationTaskDefinition GetRequired(string taskCode) =>
        Definitions.FirstOrDefault(
            definition => string.Equals(definition.Code, taskCode, StringComparison.OrdinalIgnoreCase))
        ?? throw new KeyNotFoundException($"AXATA synchronization task '{taskCode}' was not found.");
}

internal sealed record AxataSynchronizationTaskDefinition(
    string Code,
    string Name,
    string Description,
    string Flow,
    bool RequiresWarehouseNo,
    string SourceSystem,
    string TargetSystem);

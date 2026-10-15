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
            "received-company-order-sync",
            "Alinan Firma Siparisi C02 Senkronizasyonu",
            "Alinan firma/musteri siparislerini AXATA C02 outbound order formatina cevirir.",
            "Mikro -> AXATA",
            false,
            "Mikro",
            "AXATA"),
        new(
            "warehouse-inbound-order-sync",
            "Merkez Depo Giris Siparisi Senkronizasyonu",
            "Merkez depoya gelen depolar arasi siparisleri AXATA G02 inbound order formatina cevirir.",
            "Mikro -> AXATA",
            true,
            "Mikro",
            "AXATA"),
        new(
            "company-receiving-sync",
            "Verilen Firma Siparisi G01 Senkronizasyonu",
            "Verilen firma/satinalma siparislerini AXATA G01 inbound order formatina cevirir.",
            "Mikro -> AXATA",
            true,
            "Mikro",
            "AXATA"),
        new(
            "inventory-count-sync",
            "Sayim Sonucu Senkronizasyonu",
            "AXATA dinamik sayim/duzeltme satirlarini Mikro stok hareketlerine aktarir.",
            "AXATA -> Mikro",
            true,
            "Mikro",
            "AXATA"),
        new(
            "c01-outbound-delivery-import",
            "C01 Depo Sevki Aktarimi",
            "AXATA'da tamamlanan C01 depo sevklerini Mikro stok hareketlerine aktarir.",
            "AXATA -> Mikro",
            false,
            "AXATA",
            "Mikro"),
        new(
            "c02-outbound-delivery-import",
            "C02 Firma Sevki Aktarimi",
            "AXATA'da tamamlanan C02 firma sevklerini Mikro stok hareketlerine aktarir.",
            "AXATA -> Mikro",
            false,
            "AXATA",
            "Mikro"),
        new(
            "c03-outbound-delivery-import",
            "C03 Depo Iadesi Aktarimi",
            "AXATA'da tamamlanan C03 depo iadelerini Mikro stok hareketlerine aktarir.",
            "AXATA -> Mikro",
            false,
            "AXATA",
            "Mikro"),
        new(
            "c04-outbound-delivery-import",
            "C4 Firma Iadesi Aktarimi",
            "AXATA'da tamamlanan C4 firma iadelerini Mikro stok hareketlerine aktarir.",
            "AXATA -> Mikro",
            false,
            "AXATA",
            "Mikro"),
        new(
            "g01-inbound-atf-import",
            "G01 Firma Mal Kabul Aktarimi",
            "AXATA G01 ATF satirlarini Mikro firma mal kabul hareketlerine aktarir.",
            "AXATA -> Mikro",
            false,
            "AXATA",
            "Mikro"),
        new(
            "g02-inbound-delivery-import",
            "G02 Depo Mal Kabul Aktarimi",
            "AXATA'da tamamlanan G02 depo mal kabullerini Mikro stok hareketlerine aktarir.",
            "AXATA -> Mikro",
            false,
            "AXATA",
            "Mikro")
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

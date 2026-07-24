namespace FurpaMerkezApi.Application.Security;

public static class PermissionCatalog
{
    private static readonly PermissionActionDefinition[] CrudActions =
    [
        new("list", "Listele"),
        new("detail", "Detay"),
        new("create", "Ekle"),
        new("update", "Guncelle")
    ];

    private static readonly PermissionActionDefinition[] ReadActions =
    [
        new("list", "Listele"),
        new("detail", "Detay")
    ];

    private static readonly PermissionActionDefinition[] ListActions =
    [
        new("list", "Listele")
    ];

    private static readonly PermissionActionDefinition[] ListCreateActions =
    [
        new("list", "Listele"),
        new("create", "Ekle")
    ];

    private static readonly PermissionActionDefinition[] ReadCreateActions =
    [
        new("list", "Listele"),
        new("detail", "Detay"),
        new("create", "Ekle")
    ];

    private static readonly PermissionActionDefinition[] ReadUpdateActions =
    [
        new("list", "Listele"),
        new("detail", "Detay"),
        new("update", "Guncelle")
    ];

    private static readonly PermissionActionDefinition[] ReadUpdateDeleteActions =
    [
        new("list", "Listele"),
        new("detail", "Detay"),
        new("update", "Guncelle"),
        new("delete", "Sil")
    ];

    private static readonly PermissionActionDefinition[] StockAnomalyActions =
    [
        new("list", "Listele"),
        new("detail", "Detay"),
        new("update", "Guncelle"),
        new("scan", "Tara")
    ];

    private static readonly PermissionActionDefinition[] FeedbackActions =
    [
        new("list", "Listele"),
        new("detail", "Detay"),
        new("update", "Guncelle"),
        new("list-all", "Tumunu Listele")
    ];

    private static readonly PermissionActionDefinition[] CashSummaryEntryActions =
    [
        new("list", "Listele"),
        new("create", "Ekle"),
        new("update", "Guncelle"),
        new("delete", "Sil")
    ];

    private static readonly PermissionActionDefinition[] ProductDistributionActions =
    [
        new("list", "Listele"),
        new("detail", "Detay"),
        new("create", "Ekle"),
        new("update", "Guncelle"),
        new("delete", "Sil")
    ];

    public static IReadOnlyCollection<PermissionDefinition> Definitions { get; } =
    [
        new(
            PermissionCodes.RolesManage,
            "Roller Yonet",
            "KullaniciIslemleri > Roller > Yonet yetkisi.",
            "kullanici-islemleri",
            "KullaniciIslemleri",
            "roller",
            "Roller",
            "manage",
            "Yonet"),
        new(
            PermissionCodes.PermissionsManage,
            "Yetkiler Yonet",
            "KullaniciIslemleri > Yetkiler > Yonet yetkisi.",
            "kullanici-islemleri",
            "KullaniciIslemleri",
            "yetkiler",
            "Yetkiler",
            "manage",
            "Yonet"),
        new(
            PermissionCodes.UsersManage,
            "Kullanicilar Yonet",
            "KullaniciIslemleri > Kullanicilar > Yonet yetkisi.",
            "kullanici-islemleri",
            "KullaniciIslemleri",
            "kullanicilar",
            "Kullanicilar",
            "manage",
            "Yonet"),

        ..CreateMenuPermissions("arama-islemleri", "AramaIslemleri", "fiyat-gor", "FiyatGor", ListActions),
        ..CreateMenuPermissions("arama-islemleri", "AramaIslemleri", "cari-bul", "CariBul", ListActions),

        ..CreateMenuPermissions("green-grocer", "GreenGrocer", "reports", "Reports", ReadUpdateActions),
        ..CreateMenuPermissions("ortak-islemler", "OrtakIslemler", "sikayet-oneri", "SikayetOneri", FeedbackActions),

        ..CreateMenuPermissions("ayar-islemleri", "AyarIslemleri", "cihazlar", "Cihazlar"),
        ..CreateMenuPermissions("ayar-islemleri", "AyarIslemleri", "sube-ayarlari", "SubeAyarlari"),
        ..CreateMenuPermissions("ayar-islemleri", "AyarIslemleri", "kasa-pos-terminalleri", "KasaPosTerminalleri"),
        ..CreateMenuPermissions("ayar-islemleri", "AyarIslemleri", "kasiyerler", "Kasiyerler"),

        ..CreateMenuPermissions("siparis-islemleri", "SiparisIslemleri", "alinan-depo-siparisleri", "AlinanDepoSiparisleri"),
        ..CreateMenuPermissions("siparis-islemleri", "SiparisIslemleri", "verilen-depo-siparisleri", "VerilenDepoSiparisleri"),
        ..CreateMenuPermissions("siparis-islemleri", "SiparisIslemleri", "alinan-firma-siparisleri", "AlinanFirmaSiparisleri"),
        ..CreateMenuPermissions("siparis-islemleri", "SiparisIslemleri", "verilen-firma-siparisleri", "VerilenFirmaSiparisleri"),
        ..CreateMenuPermissions("siparis-islemleri", "SiparisIslemleri", "onerilen-depo-siparisleri", "OnerilenDepoSiparisleri", ListCreateActions),
        ..CreateMenuPermissions("siparis-islemleri", "SiparisIslemleri", "onerilen-firma-siparisleri", "OnerilenFirmaSiparisleri", ListCreateActions),

        ..CreateMenuPermissions("sevk-islemleri", "SevkIslemleri", "giden-depolar-arasi-sevkler", "GidenDepolarArasiSevkler"),
        ..CreateMenuPermissions("sevk-islemleri", "SevkIslemleri", "gelen-depolar-arasi-sevkler", "GelenDepolarArasiSevkler"),
        ..CreateMenuPermissions("sevk-islemleri", "SevkIslemleri", "giden-firma-sevkleri", "GidenFirmaSevkleri"),
        ..CreateMenuPermissions("sevk-islemleri", "SevkIslemleri", "gelen-firma-sevkleri", "GelenFirmaSevkleri"),

        ..CreateMenuPermissions("iade-islemleri", "IadeIslemleri", "giden-depo-iadeleri", "GidenDepoIadeleri"),
        ..CreateMenuPermissions("iade-islemleri", "IadeIslemleri", "gelen-depo-iadeleri", "GelenDepoIadeleri", ReadActions),
        ..CreateMenuPermissions("iade-islemleri", "IadeIslemleri", "firma-iadeleri", "FirmaIadeleri"),
        ..CreateMenuPermissions("mal-kabul-islemleri", "MalKabulIslemleri", "depo-mal-kabulleri", "DepoMalKabulleri"),
        ..CreateMenuPermissions("mal-kabul-islemleri", "MalKabulIslemleri", "mal-kabul-farklari", "MalKabulFarklari", ListActions),
        ..CreateMenuPermissions("mal-kabul-islemleri", "MalKabulIslemleri", "firma-mal-kabulleri", "FirmaMalKabulleri"),
        ..CreateMenuPermissions("stok-islemleri", "StokIslemleri", "zayiat-fisleri", "ZayiatFisleri"),
        ..CreateMenuPermissions("stok-islemleri", "StokIslemleri", "masraf-fisleri", "MasrafFisleri"),
        ..CreateMenuPermissions("stok-islemleri", "StokIslemleri", "sayim-sonuclari", "SayimSonuclari"),
        ..CreateMenuPermissions("stok-islemleri", "StokIslemleri", "virmanlar", "Virmanlar"),
        ..CreateMenuPermissions("stok-islemleri", "StokIslemleri", "stok-anomali-merkezi", "StokAnomaliMerkezi", StockAnomalyActions),
        ..CreateMenuPermissions("rapor-islemleri", "RaporIslemleri", "satis-analizleri", "SatisAnalizleri", ListActions),
        ..CreateMenuPermissions("rapor-islemleri", "RaporIslemleri", "stok-raporlari", "StokRaporlari", ListActions),
        ..CreateMenuPermissions("rapor-islemleri", "RaporIslemleri", "promosyon-raporlari", "PromosyonRaporlari", ListActions),
        ..CreateMenuPermissions("rapor-islemleri", "RaporIslemleri", "tedarikci-performans-karnesi", "TedarikciPerformansKarnesi", ReadActions),
        ..CreateMenuPermissions("operasyon-islemleri", "OperasyonIslemleri", "operations", "Operations"),
        ..CreateMenuPermissions("operasyon-islemleri", "OperasyonIslemleri", "belge-akis-takibi", "BelgeAkisTakibi", ReadActions),
        ..CreateMenuPermissions("operasyon-islemleri", "OperasyonIslemleri", "depo-operasyon-paneli", "DepoOperasyonPaneli", ListActions),
        ..CreateMenuPermissions("operasyon-islemleri", "OperasyonIslemleri", "urun-dagilimlari", "UrunDagilimlari", ProductDistributionActions),
        ..CreateMenuPermissions("duzeltme-islemleri", "DuzeltmeIslemleri", "mikro-evrak-duzenleme", "MikroEvrakDuzenleme", ReadUpdateDeleteActions),
        ..CreateMenuPermissions("entegrasyon-islemleri", "EntegrasyonIslemleri", "axata-senkronizasyonu", "AxataSenkronizasyonu"),
        ..CreateMenuPermissions("entegrasyon-islemleri", "EntegrasyonIslemleri", "pos-muhasebe-aktarimi", "PosMuhasebeAktarimi"),
        ..CreateMenuPermissions("entegrasyon-islemleri", "EntegrasyonIslemleri", "uyumsoft-e-fatura", "UyumsoftEFatura"),
        ..CreateMenuPermissions("entegrasyon-islemleri", "EntegrasyonIslemleri", "uyumsoft-e-irsaliye", "UyumsoftEIrsaliye"),
        ..CreateMenuPermissions("fatura-islemleri", "FaturaIslemleri", "fatura-goruntuleme", "FaturaGoruntuleme", ReadUpdateActions),
        ..CreateMenuPermissions("fatura-islemleri", "FaturaIslemleri", "fatura-gonderimi", "FaturaGonderimi", ReadCreateActions),

        ..CreateMenuPermissions("kasa-islemleri", "KasaIslemleri", "kasa-sayimlari", "KasaSayimlari", ReadActions),
        ..CreateMenuPermissions("kasa-islemleri", "KasaIslemleri", "icmal-kaydi-girisi", "IcmalKaydiGirisi", CashSummaryEntryActions),
        ..CreateMenuPermissions("kasa-islemleri", "KasaIslemleri", "kasa-cirolari", "KasaCirolari", ReadActions),
        ..CreateMenuPermissions("kasa-islemleri", "KasaIslemleri", "yeni-kasa-analizleri", "YeniKasaAnalizleri", ListActions),
        ..CreateMenuPermissions("kasa-islemleri", "KasaIslemleri", "kasa-ciro-aktarimi", "KasaCiroAktarimi", ReadCreateActions),
        ..CreateMenuPermissions("kasa-islemleri", "KasaIslemleri", "kasa-hareket-aktarimi", "KasaHareketAktarimi"),
        ..CreateMenuPermissions("kasa-islemleri", "KasaIslemleri", "etiket-belgeleri", "EtiketBelgeleri"),
        ..CreateMenuPermissions("kasa-islemleri", "KasaIslemleri", "kunye-etiket-yazdirma", "KunyeEtiketYazdirma"),
        ..CreateMenuPermissions("kasa-islemleri", "KasaIslemleri", "manav-kunye-etiket-yazdirma", "ManavKunyeEtiketYazdirma", ListActions),
        ..CreateMenuPermissions("kasa-islemleri", "KasaIslemleri", "banknot-takipleri", "BanknotTakipleri", ReadCreateActions)
    ];

    public static IReadOnlyCollection<string> Codes { get; } =
        Definitions.Select(definition => definition.Code).ToArray();

    public static PermissionDefinition? Find(string code) =>
        Definitions.FirstOrDefault(definition => string.Equals(definition.Code, code, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<PermissionDefinition> CreateMenuPermissions(
        string moduleCode,
        string moduleName,
        string menuCode,
        string menuName,
        params PermissionActionDefinition[] actions) =>
        (actions.Length == 0 ? CrudActions : actions)
            .Select(action => new PermissionDefinition(
                $"{moduleCode}.{menuCode}.{action.Code}",
                $"{menuName} {action.Name}",
                $"{moduleName} > {menuName} > {action.Name} yetkisi.",
                moduleCode,
                moduleName,
                menuCode,
                menuName,
                action.Code,
                action.Name));

    private sealed record PermissionActionDefinition(string Code, string Name);
}

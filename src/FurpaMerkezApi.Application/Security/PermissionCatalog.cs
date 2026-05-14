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

        ..CreateMenuPermissions("siparis-islemleri", "SiparisIslemleri", "alinan-depo-siparisleri", "AlinanDepoSiparisleri"),
        ..CreateMenuPermissions("siparis-islemleri", "SiparisIslemleri", "verilen-depo-siparisleri", "VerilenDepoSiparisleri"),
        ..CreateMenuPermissions("siparis-islemleri", "SiparisIslemleri", "alinan-firma-siparisleri", "AlinanFirmaSiparisleri"),
        ..CreateMenuPermissions("siparis-islemleri", "SiparisIslemleri", "verilen-firma-siparisleri", "VerilenFirmaSiparisleri"),

        ..CreateMenuPermissions("sevk-islemleri", "SevkIslemleri", "giden-depolar-arasi-sevkler", "GidenDepolarArasiSevkler"),
        ..CreateMenuPermissions("sevk-islemleri", "SevkIslemleri", "gelen-depolar-arasi-sevkler", "GelenDepolarArasiSevkler"),
        ..CreateMenuPermissions("sevk-islemleri", "SevkIslemleri", "giden-firma-sevkleri", "GidenFirmaSevkleri"),
        ..CreateMenuPermissions("sevk-islemleri", "SevkIslemleri", "gelen-firma-sevkleri", "GelenFirmaSevkleri"),
        ..CreateMenuPermissions("sevk-islemleri", "SevkIslemleri", "sevk-planlari", "SevkPlanlari"),

        ..CreateMenuPermissions("iade-islemleri", "IadeIslemleri", "giden-depo-iadeleri", "GidenDepoIadeleri"),
        ..CreateMenuPermissions("iade-islemleri", "IadeIslemleri", "gelen-depo-iadeleri", "GelenDepoIadeleri", ReadActions),
        ..CreateMenuPermissions("iade-islemleri", "IadeIslemleri", "firma-iadeleri", "FirmaIadeleri"),
        ..CreateMenuPermissions("mal-kabul-islemleri", "MalKabulIslemleri", "depo-mal-kabulleri", "DepoMalKabulleri"),
        ..CreateMenuPermissions("mal-kabul-islemleri", "MalKabulIslemleri", "firma-mal-kabulleri", "FirmaMalKabulleri"),
        ..CreateMenuPermissions("mal-kabul-islemleri", "MalKabulIslemleri", "irsaliye-kabulleri", "IrsaliyeKabulleri"),
        ..CreateMenuPermissions("stok-islemleri", "StokIslemleri", "zayiat-fisleri", "ZayiatFisleri"),
        ..CreateMenuPermissions("stok-islemleri", "StokIslemleri", "masraf-fisleri", "MasrafFisleri"),
        ..CreateMenuPermissions("stok-islemleri", "StokIslemleri", "sayim-sonuclari", "SayimSonuclari"),
        ..CreateMenuPermissions("stok-islemleri", "StokIslemleri", "etiket-belgeleri", "EtiketBelgeleri"),
        ..CreateMenuPermissions("stok-islemleri", "StokIslemleri", "virmanlar", "Virmanlar"),
         ..CreateMenuPermissions("stok-islemleri", "StokIslemleri", "kunye-etiket-yazdirma", "KunyeEtiketYazdirma"),
        ..CreateMenuPermissions("operasyon-islemleri", "OperasyonIslemleri", "operations", "Operations"),
        ..CreateMenuPermissions("entegrasyon-islemleri", "EntegrasyonIslemleri", "axata-senkronizasyonu", "AxataSenkronizasyonu"),
        ..CreateMenuPermissions("entegrasyon-islemleri", "EntegrasyonIslemleri", "pos-muhasebe-aktarimi", "PosMuhasebeAktarimi"),
        ..CreateMenuPermissions("entegrasyon-islemleri", "EntegrasyonIslemleri", "uyumsoft-e-fatura", "UyumsoftEFatura"),
        ..CreateMenuPermissions("entegrasyon-islemleri", "EntegrasyonIslemleri", "uyumsoft-e-irsaliye", "UyumsoftEIrsaliye"),
        ..CreateMenuPermissions("fatura-islemleri", "FaturaIslemleri", "fatura-goruntuleme", "FaturaGoruntuleme", ReadUpdateActions),
        ..CreateMenuPermissions("fatura-islemleri", "FaturaIslemleri", "fatura-gonderimi", "FaturaGonderimi", ReadCreateActions),

        ..CreateMenuPermissions("iade-islemleri", "IadeIslemleri", "musteri-iadeleri", "MusteriIadeleri"),
        ..CreateMenuPermissions("iade-islemleri", "IadeIslemleri", "tedarikci-iadeleri", "TedarikciIadeleri"),

        ..CreateMenuPermissions("kasa-islemleri", "KasaIslemleri", "kasa-hareketleri", "KasaHareketleri"),
        ..CreateMenuPermissions("kasa-islemleri", "KasaIslemleri", "kasa-sayimlari", "KasaSayimlari"),
        ..CreateMenuPermissions("kasa-islemleri", "KasaIslemleri", "kasa-cirolari", "KasaCirolari", ReadActions)
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

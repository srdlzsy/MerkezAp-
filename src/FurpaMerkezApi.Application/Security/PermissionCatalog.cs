namespace FurpaMerkezApi.Application.Security;

public static class PermissionCatalog
{
    private static readonly PermissionActionDefinition PageAction = new("page", "Sayfa");
    private static readonly PermissionActionDefinition ManageAction = new("manage", "Yonet");

    private static readonly PermissionActionDefinition ListAction = new("list", "Listele");
    private static readonly PermissionActionDefinition DetailAction = new("detail", "Detay");
    private static readonly PermissionActionDefinition CreateAction = new("create", "Ekle");
    private static readonly PermissionActionDefinition UpdateAction = new("update", "Guncelle");
    private static readonly PermissionActionDefinition DeleteAction = new("delete", "Sil");
    private static readonly PermissionActionDefinition AllWarehousesAction = new("all-warehouses", "Tum Depolar");

    private static readonly PermissionActionDefinition[] CrudActions =
    [
        PageAction,
        ListAction,
        DetailAction,
        CreateAction,
        UpdateAction,
        AllWarehousesAction
    ];

    private static readonly PermissionActionDefinition[] ManageCrudActions =
    [
        ManageAction,
        ListAction,
        DetailAction,
        CreateAction,
        UpdateAction,
        AllWarehousesAction
    ];

    private static readonly PermissionActionDefinition[] ManageCrudDeleteActions =
    [
        ManageAction,
        ListAction,
        DetailAction,
        CreateAction,
        UpdateAction,
        DeleteAction,
        AllWarehousesAction
    ];

    private static readonly PermissionActionDefinition[] ReadActions =
    [
        PageAction,
        ListAction,
        DetailAction,
        AllWarehousesAction
    ];

    private static readonly PermissionActionDefinition[] ListActions =
    [
        PageAction,
        ListAction,
        AllWarehousesAction
    ];

    private static readonly PermissionActionDefinition[] ReportListActions =
    [
        PageAction,
        ListAction,
        AllWarehousesAction
    ];

    private static readonly PermissionActionDefinition[] ReportReadActions =
    [
        PageAction,
        ListAction,
        DetailAction,
        AllWarehousesAction
    ];

    private static readonly PermissionActionDefinition[] HomeWarehousePriorityActions =
    [
        PageAction,
        AllWarehousesAction
    ];

    private static readonly PermissionActionDefinition[] ListCreateActions =
    [
        PageAction,
        ListAction,
        CreateAction,
        AllWarehousesAction
    ];

    private static readonly PermissionActionDefinition[] ReadCreateActions =
    [
        PageAction,
        ListAction,
        DetailAction,
        CreateAction,
        AllWarehousesAction
    ];

    private static readonly PermissionActionDefinition[] ReadUpdateActions =
    [
        PageAction,
        ListAction,
        DetailAction,
        UpdateAction,
        AllWarehousesAction
    ];

    private static readonly PermissionActionDefinition[] ReadUpdateDeleteActions =
    [
        PageAction,
        ListAction,
        DetailAction,
        UpdateAction,
        DeleteAction,
        AllWarehousesAction
    ];

    private static readonly PermissionActionDefinition[] StockAnomalyActions =
    [
        PageAction,
        ListAction,
        DetailAction,
        UpdateAction,
        new("scan", "Tara"),
        AllWarehousesAction
    ];

    private static readonly PermissionActionDefinition[] FeedbackActions =
    [
        PageAction,
        ListAction,
        DetailAction,
        UpdateAction,
        new("list-all", "Tumunu Listele")
    ];

    private static readonly PermissionActionDefinition[] AnnouncementActions =
    [
        PageAction,
        ListAction,
        DetailAction,
        CreateAction,
        UpdateAction,
        new("archive", "Arsivle"),
        AllWarehousesAction
    ];

    private static readonly PermissionActionDefinition[] CashSummaryEntryActions =
    [
        PageAction,
        ListAction,
        CreateAction,
        AllWarehousesAction
    ];

    private static readonly PermissionActionDefinition[] ProductDistributionActions =
    [
        PageAction,
        ListAction,
        DetailAction,
        CreateAction,
        UpdateAction,
        DeleteAction,
        AllWarehousesAction
    ];

    private static readonly PermissionActionDefinition[] ProductCaseProfileActions =
    [
        ManageAction,
        ListAction,
        DetailAction,
        CreateAction,
        UpdateAction,
        DeleteAction,
        AllWarehousesAction
    ];

    private static readonly PermissionActionDefinition[] GreenGrocerOperationsActions =
    [
        PageAction,
        ListAction,
        CreateAction,
        AllWarehousesAction
    ];
    private static readonly PermissionActionDefinition[] ManavMalKabulVeEtiketActions =
    [
        PageAction,
        ListAction,
        DetailAction,
        CreateAction,
        UpdateAction,
        DeleteAction,
        new("transfer", "Mal Kabul"),
        AllWarehousesAction
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

        ..CreateMenuPermissions("home", "AnaSayfa", "depo-oncelikleri", "DepoOncelikleri", HomeWarehousePriorityActions),

        ..CreateMenuPermissions("arama-islemleri", "AramaIslemleri", "fiyat-gor", "FiyatGor", ListActions),
        ..CreateMenuPermissions("arama-islemleri", "AramaIslemleri", "cari-bul", "CariBul", ListActions),

        ..CreateMenuPermissions("green-grocer", "Manav", "reports", "ManavRaporlari", ReadUpdateActions),
        ..CreateMenuPermissions("green-grocer", "Manav", "product-case-profiles", "ManavKasaProfilleri", ProductCaseProfileActions),
        ..CreateMenuPermissions("green-grocer", "Manav", "operations", "ManavOperasyonPaneli", GreenGrocerOperationsActions),
        ..CreateMenuPermissions("ortak-islemler", "OrtakIslemler", "sikayet-oneri", "SikayetOneri", FeedbackActions),
        ..CreateMenuPermissions("ortak-islemler", "OrtakIslemler", "duyurular", "Duyurular", AnnouncementActions),

        ..CreateMenuPermissions("ayar-islemleri", "AyarIslemleri", "cihazlar", "Cihazlar", ManageCrudActions),
        ..CreateMenuPermissions("ayar-islemleri", "AyarIslemleri", "sube-ayarlari", "SubeAyarlari", ManageCrudActions),
        ..CreateMenuPermissions("ayar-islemleri", "AyarIslemleri", "kasa-pos-terminalleri", "KasaPosTerminalleri", ManageCrudActions),
        ..CreateMenuPermissions("ayar-islemleri", "AyarIslemleri", "kasiyerler", "Kasiyerler", ManageCrudActions),
        ..CreateMenuPermissions("ayar-islemleri", "AyarIslemleri", "soforler", "Soforler", ManageCrudDeleteActions),
        ..CreateMenuPermissions("ayar-islemleri", "AyarIslemleri", "b2b-ayarlari", "B2BAyarlari", ManageCrudDeleteActions),

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
        ..CreateMenuPermissions("rapor-islemleri", "RaporIslemleri", "satis-analizleri", "SatisAnalizleri", ReportListActions),
        ..CreateMenuPermissions("rapor-islemleri", "RaporIslemleri", "stok-raporlari", "StokRaporlari", ReportListActions),
        ..CreateMenuPermissions("rapor-islemleri", "RaporIslemleri", "promosyon-raporlari", "PromosyonRaporlari", ReportListActions),
        ..CreateMenuPermissions("rapor-islemleri", "RaporIslemleri", "tedarikci-performans-karnesi", "TedarikciPerformansKarnesi", ReportReadActions),
        ..CreateMenuPermissions("operasyon-islemleri", "OperasyonIslemleri", "operations", "Operasyonlar"),
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

        ..CreateMenuPermissions("kasa-islemleri", "KasaIslemleri", "kasa-sayimlari", "KasaSayimlari", ReadUpdateDeleteActions),
        ..CreateMenuPermissions("kasa-islemleri", "KasaIslemleri", "icmal-kaydi-girisi", "IcmalKaydiGirisi", CashSummaryEntryActions),
        ..CreateMenuPermissions("kasa-islemleri", "KasaIslemleri", "kasa-cirolari", "KasaCirolari", ReadActions),
        ..CreateMenuPermissions("kasa-islemleri", "KasaIslemleri", "yeni-kasa-analizleri", "YeniKasaAnalizleri", ListActions),
        ..CreateMenuPermissions("kasa-islemleri", "KasaIslemleri", "kasa-ciro-aktarimi", "KasaCiroAktarimi", ReadCreateActions),
        ..CreateMenuPermissions("kasa-islemleri", "KasaIslemleri", "kasa-hareket-aktarimi", "KasaHareketAktarimi"),
        ..CreateMenuPermissions("kasa-islemleri", "KasaIslemleri", "etiket-belgeleri", "EtiketBelgeleri"),
        ..CreateMenuPermissions("kasa-islemleri", "KasaIslemleri", "manav-mal-kabul-etiket", "ManavMalKabulVeEtiket", ManavMalKabulVeEtiketActions),
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



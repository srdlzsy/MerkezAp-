using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAllWarehouseScopePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @AdminRoleId uniqueidentifier = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a';
                DECLARE @CreatedAt datetime2 = '2026-04-14T00:00:00';

                DECLARE @Permissions TABLE (
                    [id] uniqueidentifier NOT NULL,
                    [code] nvarchar(160) NOT NULL,
                    [description] nvarchar(250) NOT NULL,
                    [name] nvarchar(100) NOT NULL
                );

                INSERT INTO @Permissions ([id], [code], [description], [name])
                VALUES
                    ('0414f3a0-1817-b407-6248-95cd6e01e7ee', N'stok-islemleri.stok-anomali-merkezi.all-warehouses', N'StokIslemleri > StokAnomaliMerkezi > Tum Depolar yetkisi.', N'StokAnomaliMerkezi Tum Depolar'),
                    ('08b6a220-8731-1214-24be-7e8945b334d7', N'sevk-islemleri.gelen-firma-sevkleri.all-warehouses', N'SevkIslemleri > GelenFirmaSevkleri > Tum Depolar yetkisi.', N'GelenFirmaSevkleri Tum Depolar'),
                    ('0e010b75-e04b-a163-c5f7-357754c0b947', N'siparis-islemleri.onerilen-firma-siparisleri.all-warehouses', N'SiparisIslemleri > OnerilenFirmaSiparisleri > Tum Depolar yetkisi.', N'OnerilenFirmaSiparisleri Tum Depolar'),
                    ('1589007b-fca6-49eb-6d11-08dd66b58221', N'kasa-islemleri.etiket-belgeleri.all-warehouses', N'KasaIslemleri > EtiketBelgeleri > Tum Depolar yetkisi.', N'EtiketBelgeleri Tum Depolar'),
                    ('1b4fc8ca-0890-c6c2-1496-e918e442bf5a', N'rapor-islemleri.stok-raporlari.all-warehouses', N'RaporIslemleri > StokRaporlari > Tum Depolar yetkisi.', N'StokRaporlari Tum Depolar'),
                    ('1cd4f241-0802-c7d1-5895-fb705dc688df', N'stok-islemleri.sayim-sonuclari.all-warehouses', N'StokIslemleri > SayimSonuclari > Tum Depolar yetkisi.', N'SayimSonuclari Tum Depolar'),
                    ('22821046-0d88-88d7-53e8-10bdee3af8c8', N'kasa-islemleri.kasa-cirolari.all-warehouses', N'KasaIslemleri > KasaCirolari > Tum Depolar yetkisi.', N'KasaCirolari Tum Depolar'),
                    ('24ab284c-86ef-fb4d-eca5-ad15018ff158', N'duzeltme-islemleri.mikro-evrak-duzenleme.all-warehouses', N'DuzeltmeIslemleri > MikroEvrakDuzenleme > Tum Depolar yetkisi.', N'MikroEvrakDuzenleme Tum Depolar'),
                    ('2c9f1bef-abb7-b862-4308-7abf9f29cb6d', N'siparis-islemleri.onerilen-depo-siparisleri.all-warehouses', N'SiparisIslemleri > OnerilenDepoSiparisleri > Tum Depolar yetkisi.', N'OnerilenDepoSiparisleri Tum Depolar'),
                    ('324e356e-dff0-ac59-6fb7-e78b8c25bec4', N'kasa-islemleri.kasa-hareket-aktarimi.all-warehouses', N'KasaIslemleri > KasaHareketAktarimi > Tum Depolar yetkisi.', N'KasaHareketAktarimi Tum Depolar'),
                    ('338ce2af-0728-ff0d-9db2-8849335dc4aa', N'operasyon-islemleri.belge-akis-takibi.all-warehouses', N'OperasyonIslemleri > BelgeAkisTakibi > Tum Depolar yetkisi.', N'BelgeAkisTakibi Tum Depolar'),
                    ('35edfa18-1e87-37f3-68b1-e8efa1cd320a', N'operasyon-islemleri.depo-operasyon-paneli.all-warehouses', N'OperasyonIslemleri > DepoOperasyonPaneli > Tum Depolar yetkisi.', N'DepoOperasyonPaneli Tum Depolar'),
                    ('370eb534-49da-ba8c-7e72-58d72785477a', N'sevk-islemleri.giden-depolar-arasi-sevkler.all-warehouses', N'SevkIslemleri > GidenDepolarArasiSevkler > Tum Depolar yetkisi.', N'GidenDepolarArasiSevkler Tum Depolar'),
                    ('49290d47-c824-906e-24e3-01dffbd6e33a', N'ayar-islemleri.sube-ayarlari.all-warehouses', N'AyarIslemleri > SubeAyarlari > Tum Depolar yetkisi.', N'SubeAyarlari Tum Depolar'),
                    ('4b24c47c-ad8b-bc62-d87f-4454b3a76bc3', N'iade-islemleri.firma-iadeleri.all-warehouses', N'IadeIslemleri > FirmaIadeleri > Tum Depolar yetkisi.', N'FirmaIadeleri Tum Depolar'),
                    ('4eb86f75-5c85-f48c-e9a7-f40e9f63fac6', N'kasa-islemleri.kasa-ciro-aktarimi.all-warehouses', N'KasaIslemleri > KasaCiroAktarimi > Tum Depolar yetkisi.', N'KasaCiroAktarimi Tum Depolar'),
                    ('52467ca4-0662-a8ff-52a7-251892c42b92', N'iade-islemleri.giden-depo-iadeleri.all-warehouses', N'IadeIslemleri > GidenDepoIadeleri > Tum Depolar yetkisi.', N'GidenDepoIadeleri Tum Depolar'),
                    ('5669a1ad-3f1a-de5b-e9f5-ff3d7b20ac78', N'operasyon-islemleri.urun-dagilimlari.all-warehouses', N'OperasyonIslemleri > UrunDagilimlari > Tum Depolar yetkisi.', N'UrunDagilimlari Tum Depolar'),
                    ('599a768c-e6aa-92bf-d190-a19967ec91c2', N'green-grocer.reports.all-warehouses', N'GreenGrocer > Reports > Tum Depolar yetkisi.', N'Reports Tum Depolar'),
                    ('5d65b0f9-8509-988c-a474-95747ed8edde', N'arama-islemleri.fiyat-gor.all-warehouses', N'AramaIslemleri > FiyatGor > Tum Depolar yetkisi.', N'FiyatGor Tum Depolar'),
                    ('636e2155-66ce-fec9-7146-da92c81daece', N'rapor-islemleri.tedarikci-performans-karnesi.all-warehouses', N'RaporIslemleri > TedarikciPerformansKarnesi > Tum Depolar yetkisi.', N'TedarikciPerformansKarnesi Tum Depolar'),
                    ('69f6761f-234d-4202-0197-e3286abcbebf', N'mal-kabul-islemleri.depo-mal-kabulleri.all-warehouses', N'MalKabulIslemleri > DepoMalKabulleri > Tum Depolar yetkisi.', N'DepoMalKabulleri Tum Depolar'),
                    ('6aaea0f7-58ec-d82b-96e6-87d22c01ba75', N'siparis-islemleri.alinan-firma-siparisleri.all-warehouses', N'SiparisIslemleri > AlinanFirmaSiparisleri > Tum Depolar yetkisi.', N'AlinanFirmaSiparisleri Tum Depolar'),
                    ('6ab9df8a-4e1e-8c3c-71ea-16928a051390', N'stok-islemleri.masraf-fisleri.all-warehouses', N'StokIslemleri > MasrafFisleri > Tum Depolar yetkisi.', N'MasrafFisleri Tum Depolar'),
                    ('6e0b021b-0426-54f8-ce31-63c31db4b119', N'mal-kabul-islemleri.firma-mal-kabulleri.all-warehouses', N'MalKabulIslemleri > FirmaMalKabulleri > Tum Depolar yetkisi.', N'FirmaMalKabulleri Tum Depolar'),
                    ('70fe63b4-4486-209b-f7a9-e7785191a384', N'iade-islemleri.gelen-depo-iadeleri.all-warehouses', N'IadeIslemleri > GelenDepoIadeleri > Tum Depolar yetkisi.', N'GelenDepoIadeleri Tum Depolar'),
                    ('81941826-fa91-4a19-0c78-c2193b220eb2', N'ayar-islemleri.kasa-pos-terminalleri.all-warehouses', N'AyarIslemleri > KasaPosTerminalleri > Tum Depolar yetkisi.', N'KasaPosTerminalleri Tum Depolar'),
                    ('87b26e62-b1f5-262e-431a-300e26031722', N'ayar-islemleri.kasiyerler.all-warehouses', N'AyarIslemleri > Kasiyerler > Tum Depolar yetkisi.', N'Kasiyerler Tum Depolar'),
                    ('884ddedc-6aa4-d021-f8ef-989f32f68fe3', N'kasa-islemleri.kunye-etiket-yazdirma.all-warehouses', N'KasaIslemleri > KunyeEtiketYazdirma > Tum Depolar yetkisi.', N'KunyeEtiketYazdirma Tum Depolar'),
                    ('91a4472b-0a2f-9120-ebca-f2b5b782f8c6', N'ayar-islemleri.cihazlar.all-warehouses', N'AyarIslemleri > Cihazlar > Tum Depolar yetkisi.', N'Cihazlar Tum Depolar'),
                    ('96bbc3f9-c3d3-6be2-b9b6-c222a828f909', N'kasa-islemleri.icmal-kaydi-girisi.all-warehouses', N'KasaIslemleri > IcmalKaydiGirisi > Tum Depolar yetkisi.', N'IcmalKaydiGirisi Tum Depolar'),
                    ('98ff4234-cdcf-e191-14b9-2731dea9a1c2', N'siparis-islemleri.verilen-firma-siparisleri.all-warehouses', N'SiparisIslemleri > VerilenFirmaSiparisleri > Tum Depolar yetkisi.', N'VerilenFirmaSiparisleri Tum Depolar'),
                    ('9c257210-df11-c76d-11a0-f039e0f9568c', N'arama-islemleri.cari-bul.all-warehouses', N'AramaIslemleri > CariBul > Tum Depolar yetkisi.', N'CariBul Tum Depolar'),
                    ('9e531925-eb3e-bf49-2463-4761e8b4276f', N'operasyon-islemleri.operations.all-warehouses', N'OperasyonIslemleri > Operations > Tum Depolar yetkisi.', N'Operations Tum Depolar'),
                    ('a6e0802b-a9ab-c921-5447-6a42ed352a4c', N'sevk-islemleri.giden-firma-sevkleri.all-warehouses', N'SevkIslemleri > GidenFirmaSevkleri > Tum Depolar yetkisi.', N'GidenFirmaSevkleri Tum Depolar'),
                    ('ac61bf1b-9ccf-c771-332b-3ab210633343', N'home.depo-oncelikleri.all-warehouses', N'Home > DepoOncelikleri > Tum Depolar yetkisi.', N'DepoOncelikleri Tum Depolar'),
                    ('b048ae58-5b9d-5d2f-11f7-45aa0602ba5d', N'stok-islemleri.virmanlar.all-warehouses', N'StokIslemleri > Virmanlar > Tum Depolar yetkisi.', N'Virmanlar Tum Depolar'),
                    ('b37c7045-8660-17e9-6d74-6d6f8c5d397c', N'entegrasyon-islemleri.axata-senkronizasyonu.all-warehouses', N'EntegrasyonIslemleri > AxataSenkronizasyonu > Tum Depolar yetkisi.', N'AxataSenkronizasyonu Tum Depolar'),
                    ('bb940f83-81e1-db8f-3ed4-502a97948842', N'entegrasyon-islemleri.uyumsoft-e-irsaliye.all-warehouses', N'EntegrasyonIslemleri > UyumsoftEIrsaliye > Tum Depolar yetkisi.', N'UyumsoftEIrsaliye Tum Depolar'),
                    ('bc61f6dc-1b85-48bb-11e4-24bd1bc7df3e', N'fatura-islemleri.fatura-gonderimi.all-warehouses', N'FaturaIslemleri > FaturaGonderimi > Tum Depolar yetkisi.', N'FaturaGonderimi Tum Depolar'),
                    ('bfa8f7e9-629d-8d7a-3e8c-58363fcc9a4b', N'kasa-islemleri.manav-kunye-etiket-yazdirma.all-warehouses', N'KasaIslemleri > ManavKunyeEtiketYazdirma > Tum Depolar yetkisi.', N'ManavKunyeEtiketYazdirma Tum Depolar'),
                    ('c712f1e3-1731-f186-a782-6c1c0cc1ac09', N'rapor-islemleri.satis-analizleri.all-warehouses', N'RaporIslemleri > SatisAnalizleri > Tum Depolar yetkisi.', N'SatisAnalizleri Tum Depolar'),
                    ('ca7eefa2-01cb-2bb0-d0b2-9d348e352dbb', N'kasa-islemleri.yeni-kasa-analizleri.all-warehouses', N'KasaIslemleri > YeniKasaAnalizleri > Tum Depolar yetkisi.', N'YeniKasaAnalizleri Tum Depolar'),
                    ('cafaba59-dc03-9b90-0c72-754ae620dd09', N'siparis-islemleri.alinan-depo-siparisleri.all-warehouses', N'SiparisIslemleri > AlinanDepoSiparisleri > Tum Depolar yetkisi.', N'AlinanDepoSiparisleri Tum Depolar'),
                    ('cea8d238-0fc4-aafa-320a-704d57c1e19a', N'rapor-islemleri.promosyon-raporlari.all-warehouses', N'RaporIslemleri > PromosyonRaporlari > Tum Depolar yetkisi.', N'PromosyonRaporlari Tum Depolar'),
                    ('d1362574-6098-386c-7ff2-631852add6ce', N'stok-islemleri.zayiat-fisleri.all-warehouses', N'StokIslemleri > ZayiatFisleri > Tum Depolar yetkisi.', N'ZayiatFisleri Tum Depolar'),
                    ('d31267c6-786d-9ac7-7b2a-2da81d95753e', N'entegrasyon-islemleri.uyumsoft-e-fatura.all-warehouses', N'EntegrasyonIslemleri > UyumsoftEFatura > Tum Depolar yetkisi.', N'UyumsoftEFatura Tum Depolar'),
                    ('d34c588a-90d8-072a-94cd-063d9c615471', N'kasa-islemleri.kasa-sayimlari.all-warehouses', N'KasaIslemleri > KasaSayimlari > Tum Depolar yetkisi.', N'KasaSayimlari Tum Depolar'),
                    ('d80e44d2-d648-85fb-d85b-8cd9ab8c8238', N'siparis-islemleri.verilen-depo-siparisleri.all-warehouses', N'SiparisIslemleri > VerilenDepoSiparisleri > Tum Depolar yetkisi.', N'VerilenDepoSiparisleri Tum Depolar'),
                    ('e6b98809-b9b4-496a-d378-93a60b68433f', N'fatura-islemleri.fatura-goruntuleme.all-warehouses', N'FaturaIslemleri > FaturaGoruntuleme > Tum Depolar yetkisi.', N'FaturaGoruntuleme Tum Depolar'),
                    ('e9c854f3-4aff-9f14-4e89-5eb3d22cfe16', N'sevk-islemleri.gelen-depolar-arasi-sevkler.all-warehouses', N'SevkIslemleri > GelenDepolarArasiSevkler > Tum Depolar yetkisi.', N'GelenDepolarArasiSevkler Tum Depolar'),
                    ('f3da9831-b40e-65ee-816e-32e09d21475b', N'kasa-islemleri.banknot-takipleri.all-warehouses', N'KasaIslemleri > BanknotTakipleri > Tum Depolar yetkisi.', N'BanknotTakipleri Tum Depolar'),
                    ('f873e8ec-8d5f-a9ef-10ca-2fda9f57cdf9', N'mal-kabul-islemleri.mal-kabul-farklari.all-warehouses', N'MalKabulIslemleri > MalKabulFarklari > Tum Depolar yetkisi.', N'MalKabulFarklari Tum Depolar'),
                    ('fb3a52e7-e221-a7eb-f69d-25144eb2992c', N'entegrasyon-islemleri.pos-muhasebe-aktarimi.all-warehouses', N'EntegrasyonIslemleri > PosMuhasebeAktarimi > Tum Depolar yetkisi.', N'PosMuhasebeAktarimi Tum Depolar');

                INSERT INTO [app_permissions] ([id], [code], [created_at_utc], [description], [name], [updated_at_utc])
                SELECT pending.[id], pending.[code], @CreatedAt, pending.[description], pending.[name], NULL
                FROM @Permissions AS pending
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [app_permissions] AS existing
                    WHERE existing.[code] = pending.[code]
                       OR existing.[id] = pending.[id]
                );

                INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                SELECT permission.[id], @AdminRoleId, @CreatedAt
                FROM [app_permissions] AS permission
                INNER JOIN @Permissions AS pending ON pending.[code] = permission.[code]
                WHERE EXISTS (
                    SELECT 1
                    FROM [app_roles]
                    WHERE [id] = @AdminRoleId
                )
                AND NOT EXISTS (
                    SELECT 1
                    FROM [app_role_permissions] AS rolePermission
                    WHERE rolePermission.[permission_id] = permission.[id]
                      AND rolePermission.[role_id] = @AdminRoleId
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE rolePermission
                FROM [app_role_permissions] AS rolePermission
                INNER JOIN [app_permissions] AS permission ON permission.[id] = rolePermission.[permission_id]
                WHERE permission.[code] IN (
                    N'stok-islemleri.stok-anomali-merkezi.all-warehouses',
                    N'sevk-islemleri.gelen-firma-sevkleri.all-warehouses',
                    N'siparis-islemleri.onerilen-firma-siparisleri.all-warehouses',
                    N'kasa-islemleri.etiket-belgeleri.all-warehouses',
                    N'rapor-islemleri.stok-raporlari.all-warehouses',
                    N'stok-islemleri.sayim-sonuclari.all-warehouses',
                    N'kasa-islemleri.kasa-cirolari.all-warehouses',
                    N'duzeltme-islemleri.mikro-evrak-duzenleme.all-warehouses',
                    N'siparis-islemleri.onerilen-depo-siparisleri.all-warehouses',
                    N'kasa-islemleri.kasa-hareket-aktarimi.all-warehouses',
                    N'operasyon-islemleri.belge-akis-takibi.all-warehouses',
                    N'operasyon-islemleri.depo-operasyon-paneli.all-warehouses',
                    N'sevk-islemleri.giden-depolar-arasi-sevkler.all-warehouses',
                    N'ayar-islemleri.sube-ayarlari.all-warehouses',
                    N'iade-islemleri.firma-iadeleri.all-warehouses',
                    N'kasa-islemleri.kasa-ciro-aktarimi.all-warehouses',
                    N'iade-islemleri.giden-depo-iadeleri.all-warehouses',
                    N'operasyon-islemleri.urun-dagilimlari.all-warehouses',
                    N'green-grocer.reports.all-warehouses',
                    N'arama-islemleri.fiyat-gor.all-warehouses',
                    N'rapor-islemleri.tedarikci-performans-karnesi.all-warehouses',
                    N'mal-kabul-islemleri.depo-mal-kabulleri.all-warehouses',
                    N'siparis-islemleri.alinan-firma-siparisleri.all-warehouses',
                    N'stok-islemleri.masraf-fisleri.all-warehouses',
                    N'mal-kabul-islemleri.firma-mal-kabulleri.all-warehouses',
                    N'iade-islemleri.gelen-depo-iadeleri.all-warehouses',
                    N'ayar-islemleri.kasa-pos-terminalleri.all-warehouses',
                    N'ayar-islemleri.kasiyerler.all-warehouses',
                    N'kasa-islemleri.kunye-etiket-yazdirma.all-warehouses',
                    N'ayar-islemleri.cihazlar.all-warehouses',
                    N'kasa-islemleri.icmal-kaydi-girisi.all-warehouses',
                    N'siparis-islemleri.verilen-firma-siparisleri.all-warehouses',
                    N'arama-islemleri.cari-bul.all-warehouses',
                    N'operasyon-islemleri.operations.all-warehouses',
                    N'sevk-islemleri.giden-firma-sevkleri.all-warehouses',
                    N'home.depo-oncelikleri.all-warehouses',
                    N'stok-islemleri.virmanlar.all-warehouses',
                    N'entegrasyon-islemleri.axata-senkronizasyonu.all-warehouses',
                    N'entegrasyon-islemleri.uyumsoft-e-irsaliye.all-warehouses',
                    N'fatura-islemleri.fatura-gonderimi.all-warehouses',
                    N'kasa-islemleri.manav-kunye-etiket-yazdirma.all-warehouses',
                    N'rapor-islemleri.satis-analizleri.all-warehouses',
                    N'kasa-islemleri.yeni-kasa-analizleri.all-warehouses',
                    N'siparis-islemleri.alinan-depo-siparisleri.all-warehouses',
                    N'rapor-islemleri.promosyon-raporlari.all-warehouses',
                    N'stok-islemleri.zayiat-fisleri.all-warehouses',
                    N'entegrasyon-islemleri.uyumsoft-e-fatura.all-warehouses',
                    N'kasa-islemleri.kasa-sayimlari.all-warehouses',
                    N'siparis-islemleri.verilen-depo-siparisleri.all-warehouses',
                    N'fatura-islemleri.fatura-goruntuleme.all-warehouses',
                    N'sevk-islemleri.gelen-depolar-arasi-sevkler.all-warehouses',
                    N'kasa-islemleri.banknot-takipleri.all-warehouses',
                    N'mal-kabul-islemleri.mal-kabul-farklari.all-warehouses',
                    N'entegrasyon-islemleri.pos-muhasebe-aktarimi.all-warehouses'
                );

                DELETE FROM [app_permissions]
                WHERE [code] IN (
                    N'stok-islemleri.stok-anomali-merkezi.all-warehouses',
                    N'sevk-islemleri.gelen-firma-sevkleri.all-warehouses',
                    N'siparis-islemleri.onerilen-firma-siparisleri.all-warehouses',
                    N'kasa-islemleri.etiket-belgeleri.all-warehouses',
                    N'rapor-islemleri.stok-raporlari.all-warehouses',
                    N'stok-islemleri.sayim-sonuclari.all-warehouses',
                    N'kasa-islemleri.kasa-cirolari.all-warehouses',
                    N'duzeltme-islemleri.mikro-evrak-duzenleme.all-warehouses',
                    N'siparis-islemleri.onerilen-depo-siparisleri.all-warehouses',
                    N'kasa-islemleri.kasa-hareket-aktarimi.all-warehouses',
                    N'operasyon-islemleri.belge-akis-takibi.all-warehouses',
                    N'operasyon-islemleri.depo-operasyon-paneli.all-warehouses',
                    N'sevk-islemleri.giden-depolar-arasi-sevkler.all-warehouses',
                    N'ayar-islemleri.sube-ayarlari.all-warehouses',
                    N'iade-islemleri.firma-iadeleri.all-warehouses',
                    N'kasa-islemleri.kasa-ciro-aktarimi.all-warehouses',
                    N'iade-islemleri.giden-depo-iadeleri.all-warehouses',
                    N'operasyon-islemleri.urun-dagilimlari.all-warehouses',
                    N'green-grocer.reports.all-warehouses',
                    N'arama-islemleri.fiyat-gor.all-warehouses',
                    N'rapor-islemleri.tedarikci-performans-karnesi.all-warehouses',
                    N'mal-kabul-islemleri.depo-mal-kabulleri.all-warehouses',
                    N'siparis-islemleri.alinan-firma-siparisleri.all-warehouses',
                    N'stok-islemleri.masraf-fisleri.all-warehouses',
                    N'mal-kabul-islemleri.firma-mal-kabulleri.all-warehouses',
                    N'iade-islemleri.gelen-depo-iadeleri.all-warehouses',
                    N'ayar-islemleri.kasa-pos-terminalleri.all-warehouses',
                    N'ayar-islemleri.kasiyerler.all-warehouses',
                    N'kasa-islemleri.kunye-etiket-yazdirma.all-warehouses',
                    N'ayar-islemleri.cihazlar.all-warehouses',
                    N'kasa-islemleri.icmal-kaydi-girisi.all-warehouses',
                    N'siparis-islemleri.verilen-firma-siparisleri.all-warehouses',
                    N'arama-islemleri.cari-bul.all-warehouses',
                    N'operasyon-islemleri.operations.all-warehouses',
                    N'sevk-islemleri.giden-firma-sevkleri.all-warehouses',
                    N'home.depo-oncelikleri.all-warehouses',
                    N'stok-islemleri.virmanlar.all-warehouses',
                    N'entegrasyon-islemleri.axata-senkronizasyonu.all-warehouses',
                    N'entegrasyon-islemleri.uyumsoft-e-irsaliye.all-warehouses',
                    N'fatura-islemleri.fatura-gonderimi.all-warehouses',
                    N'kasa-islemleri.manav-kunye-etiket-yazdirma.all-warehouses',
                    N'rapor-islemleri.satis-analizleri.all-warehouses',
                    N'kasa-islemleri.yeni-kasa-analizleri.all-warehouses',
                    N'siparis-islemleri.alinan-depo-siparisleri.all-warehouses',
                    N'rapor-islemleri.promosyon-raporlari.all-warehouses',
                    N'stok-islemleri.zayiat-fisleri.all-warehouses',
                    N'entegrasyon-islemleri.uyumsoft-e-fatura.all-warehouses',
                    N'kasa-islemleri.kasa-sayimlari.all-warehouses',
                    N'siparis-islemleri.verilen-depo-siparisleri.all-warehouses',
                    N'fatura-islemleri.fatura-goruntuleme.all-warehouses',
                    N'sevk-islemleri.gelen-depolar-arasi-sevkler.all-warehouses',
                    N'kasa-islemleri.banknot-takipleri.all-warehouses',
                    N'mal-kabul-islemleri.mal-kabul-farklari.all-warehouses',
                    N'entegrasyon-islemleri.pos-muhasebe-aktarimi.all-warehouses'
                );
                """);
        }
    }
}

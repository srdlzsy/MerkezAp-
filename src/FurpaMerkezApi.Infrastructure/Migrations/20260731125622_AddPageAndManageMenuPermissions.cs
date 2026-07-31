using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPageAndManageMenuPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("01ccdd13-642f-093a-e7d6-644197371cf7"), "siparis-islemleri.alinan-firma-siparisleri.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SiparisIslemleri > AlinanFirmaSiparisleri > Sayfa yetkisi.", "AlinanFirmaSiparisleri Sayfa", null },
                    { new Guid("03c12c1a-4a7e-a2d3-003e-2fcced777d1f"), "rapor-islemleri.promosyon-raporlari.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "RaporIslemleri > PromosyonRaporlari > Sayfa yetkisi.", "PromosyonRaporlari Sayfa", null },
                    { new Guid("05f72094-0eb6-824a-148f-8d40af4653f9"), "operasyon-islemleri.belge-akis-takibi.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OperasyonIslemleri > BelgeAkisTakibi > Sayfa yetkisi.", "BelgeAkisTakibi Sayfa", null },
                    { new Guid("0773b4fc-85a3-f397-2cd9-45884524e7cd"), "entegrasyon-islemleri.uyumsoft-e-fatura.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "EntegrasyonIslemleri > UyumsoftEFatura > Sayfa yetkisi.", "UyumsoftEFatura Sayfa", null },
                    { new Guid("0a15de74-241e-0bc9-c37e-bffd65c7f731"), "stok-islemleri.sayim-sonuclari.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > SayimSonuclari > Sayfa yetkisi.", "SayimSonuclari Sayfa", null },
                    { new Guid("0cd11f32-2124-80af-a5dc-b994ae8f4a49"), "kasa-islemleri.manav-kunye-etiket-yazdirma.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > ManavKunyeEtiketYazdirma > Sayfa yetkisi.", "ManavKunyeEtiketYazdirma Sayfa", null },
                    { new Guid("1c60e631-936d-21e7-00da-c78c3a78f92a"), "kasa-islemleri.etiket-basim.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > EtiketBasim > Sayfa yetkisi.", "EtiketBasim Sayfa", null },
                    { new Guid("2191161a-ab7c-6eb4-987c-2aef582544f0"), "stok-islemleri.stok-anomali-merkezi.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > StokAnomaliMerkezi > Sayfa yetkisi.", "StokAnomaliMerkezi Sayfa", null },
                    { new Guid("32ae4589-1029-cc34-383e-661cc0818145"), "sevk-islemleri.giden-firma-sevkleri.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SevkIslemleri > GidenFirmaSevkleri > Sayfa yetkisi.", "GidenFirmaSevkleri Sayfa", null },
                    { new Guid("3b4a2843-71ae-df09-933c-49f5ac4ea835"), "ayar-islemleri.kasa-pos-terminalleri.manage", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > KasaPosTerminalleri > Yonet yetkisi.", "KasaPosTerminalleri Yonet", null },
                    { new Guid("3f39e294-bbe6-b256-b73c-a506d42ec0c8"), "iade-islemleri.giden-depo-iadeleri.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > GidenDepoIadeleri > Sayfa yetkisi.", "GidenDepoIadeleri Sayfa", null },
                    { new Guid("421772b7-2615-d8e9-41f6-929c9a40e598"), "operasyon-islemleri.operations.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OperasyonIslemleri > Operations > Sayfa yetkisi.", "Operations Sayfa", null },
                    { new Guid("4623d3df-e5c4-b363-4860-ec1e098d9c53"), "kasa-islemleri.banknot-takipleri.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > BanknotTakipleri > Sayfa yetkisi.", "BanknotTakipleri Sayfa", null },
                    { new Guid("4be114ac-4951-89c5-6ccb-9c5f183797b8"), "ayar-islemleri.sube-ayarlari.manage", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > SubeAyarlari > Yonet yetkisi.", "SubeAyarlari Yonet", null },
                    { new Guid("4e698b19-0149-6804-a7a2-78aefb8ca4c3"), "siparis-islemleri.alinan-depo-siparisleri.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SiparisIslemleri > AlinanDepoSiparisleri > Sayfa yetkisi.", "AlinanDepoSiparisleri Sayfa", null },
                    { new Guid("50aaf4c5-8bc1-1bd7-0509-a847714afa4a"), "stok-islemleri.zayiat-fisleri.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > ZayiatFisleri > Sayfa yetkisi.", "ZayiatFisleri Sayfa", null },
                    { new Guid("5533112c-409d-a83f-97e0-c38faacd14d6"), "operasyon-islemleri.depo-operasyon-paneli.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OperasyonIslemleri > DepoOperasyonPaneli > Sayfa yetkisi.", "DepoOperasyonPaneli Sayfa", null },
                    { new Guid("570442a8-9f68-b03b-5974-0012a8b3c717"), "kasa-islemleri.kasa-hareket-aktarimi.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > KasaHareketAktarimi > Sayfa yetkisi.", "KasaHareketAktarimi Sayfa", null },
                    { new Guid("59d687ba-460a-c168-14f6-8bb53a4e751e"), "fatura-islemleri.fatura-goruntuleme.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "FaturaIslemleri > FaturaGoruntuleme > Sayfa yetkisi.", "FaturaGoruntuleme Sayfa", null },
                    { new Guid("5ba83241-0500-9699-6f8d-49d40f369dd0"), "arama-islemleri.fiyat-gor.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AramaIslemleri > FiyatGor > Sayfa yetkisi.", "FiyatGor Sayfa", null },
                    { new Guid("62365491-f5eb-997d-1718-f7e73b95b2a6"), "mal-kabul-islemleri.depo-mal-kabulleri.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "MalKabulIslemleri > DepoMalKabulleri > Sayfa yetkisi.", "DepoMalKabulleri Sayfa", null },
                    { new Guid("678c4ec7-9d0c-5126-a4c8-e9693a064712"), "iade-islemleri.gelen-depo-iadeleri.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > GelenDepoIadeleri > Sayfa yetkisi.", "GelenDepoIadeleri Sayfa", null },
                    { new Guid("6d904388-7d7c-19f1-b408-605bcb83890f"), "kasa-islemleri.kunye-etiket-yazdirma.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > KunyeEtiketYazdirma > Sayfa yetkisi.", "KunyeEtiketYazdirma Sayfa", null },
                    { new Guid("700f4861-1efd-1882-2904-bf9c876565a6"), "rapor-islemleri.satis-analizleri.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "RaporIslemleri > SatisAnalizleri > Sayfa yetkisi.", "SatisAnalizleri Sayfa", null },
                    { new Guid("70805ff4-2179-ba22-e5e4-820aa8859302"), "home.depo-oncelikleri.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Home > DepoOncelikleri > Sayfa yetkisi.", "DepoOncelikleri Sayfa", null },
                    { new Guid("7153c74a-616d-3ec7-7288-139e9c68b9c2"), "entegrasyon-islemleri.axata-senkronizasyonu.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "EntegrasyonIslemleri > AxataSenkronizasyonu > Sayfa yetkisi.", "AxataSenkronizasyonu Sayfa", null },
                    { new Guid("719de8b9-05de-e6c0-bc48-97e07a6a7b32"), "green-grocer.reports.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "GreenGrocer > Reports > Sayfa yetkisi.", "Reports Sayfa", null },
                    { new Guid("770ac54a-d04b-022f-f374-e6e4ae852994"), "kasa-islemleri.kasa-ciro-aktarimi.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > KasaCiroAktarimi > Sayfa yetkisi.", "KasaCiroAktarimi Sayfa", null },
                    { new Guid("81cacb51-a3a9-3f87-0231-863027047ea2"), "ortak-islemler.sikayet-oneri.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OrtakIslemler > SikayetOneri > Sayfa yetkisi.", "SikayetOneri Sayfa", null },
                    { new Guid("884f6e0f-cd9d-098a-1eab-5a21282e6822"), "ayar-islemleri.cihazlar.manage", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > Cihazlar > Yonet yetkisi.", "Cihazlar Yonet", null },
                    { new Guid("917d1298-01bc-4681-c26c-b8621bb8cdf3"), "stok-islemleri.masraf-fisleri.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > MasrafFisleri > Sayfa yetkisi.", "MasrafFisleri Sayfa", null },
                    { new Guid("93f6c941-fd4b-7b0c-7796-ebfe961d19f4"), "kasa-islemleri.kasa-sayimlari.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > KasaSayimlari > Sayfa yetkisi.", "KasaSayimlari Sayfa", null },
                    { new Guid("96551c29-f958-5c4a-015a-304fe4fcb227"), "ayar-islemleri.kasiyerler.manage", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > Kasiyerler > Yonet yetkisi.", "Kasiyerler Yonet", null },
                    { new Guid("a47f8de0-c5e0-a8e1-22cf-3b299c65f4fc"), "sevk-islemleri.gelen-firma-sevkleri.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SevkIslemleri > GelenFirmaSevkleri > Sayfa yetkisi.", "GelenFirmaSevkleri Sayfa", null },
                    { new Guid("a6419da9-86be-45e1-6384-616080de59f5"), "green-grocer.product-case-profiles.manage", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "GreenGrocer > ProductCaseProfiles > Yonet yetkisi.", "ProductCaseProfiles Yonet", null },
                    { new Guid("acfa8f71-c395-5cf4-1ce5-b70660895d5a"), "kasa-islemleri.icmal-kaydi-girisi.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > IcmalKaydiGirisi > Sayfa yetkisi.", "IcmalKaydiGirisi Sayfa", null },
                    { new Guid("b0e29b8f-09e0-d2e1-d7dc-61b5eea8aadb"), "siparis-islemleri.onerilen-depo-siparisleri.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SiparisIslemleri > OnerilenDepoSiparisleri > Sayfa yetkisi.", "OnerilenDepoSiparisleri Sayfa", null },
                    { new Guid("b60daace-b236-78c4-c9c2-98f0352ef0cf"), "kasa-islemleri.etiket-belgeleri.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > EtiketBelgeleri > Sayfa yetkisi.", "EtiketBelgeleri Sayfa", null },
                    { new Guid("b9a54bbf-1945-0d54-e0ae-9f0be6947d37"), "siparis-islemleri.verilen-firma-siparisleri.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SiparisIslemleri > VerilenFirmaSiparisleri > Sayfa yetkisi.", "VerilenFirmaSiparisleri Sayfa", null },
                    { new Guid("bb84b117-1010-cc33-2103-9c929682c779"), "mal-kabul-islemleri.firma-mal-kabulleri.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "MalKabulIslemleri > FirmaMalKabulleri > Sayfa yetkisi.", "FirmaMalKabulleri Sayfa", null },
                    { new Guid("bc9aea79-db23-96ad-d11c-e83d663c78ae"), "sevk-islemleri.gelen-depolar-arasi-sevkler.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SevkIslemleri > GelenDepolarArasiSevkler > Sayfa yetkisi.", "GelenDepolarArasiSevkler Sayfa", null },
                    { new Guid("c361dd06-daaa-51ae-74ff-824040132546"), "siparis-islemleri.verilen-depo-siparisleri.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SiparisIslemleri > VerilenDepoSiparisleri > Sayfa yetkisi.", "VerilenDepoSiparisleri Sayfa", null },
                    { new Guid("c3fb33f7-9293-58a3-791f-163fe2083b1a"), "kasa-islemleri.yeni-kasa-analizleri.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > YeniKasaAnalizleri > Sayfa yetkisi.", "YeniKasaAnalizleri Sayfa", null },
                    { new Guid("c4d3f8e8-2eec-fa95-e5c6-b4df3a1a39c4"), "operasyon-islemleri.urun-dagilimlari.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OperasyonIslemleri > UrunDagilimlari > Sayfa yetkisi.", "UrunDagilimlari Sayfa", null },
                    { new Guid("c5a5bee9-ddfc-a1ae-f186-aadbc19a95cc"), "entegrasyon-islemleri.uyumsoft-e-irsaliye.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "EntegrasyonIslemleri > UyumsoftEIrsaliye > Sayfa yetkisi.", "UyumsoftEIrsaliye Sayfa", null },
                    { new Guid("ca4ee4f9-ead1-213d-0ee4-4113ddaa4a22"), "sevk-islemleri.giden-depolar-arasi-sevkler.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SevkIslemleri > GidenDepolarArasiSevkler > Sayfa yetkisi.", "GidenDepolarArasiSevkler Sayfa", null },
                    { new Guid("ca7ab614-9c8d-bd71-9cb6-f60eafcb9083"), "iade-islemleri.firma-iadeleri.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > FirmaIadeleri > Sayfa yetkisi.", "FirmaIadeleri Sayfa", null },
                    { new Guid("d3b6fe33-da2d-c373-c1a9-1f4e06e845b6"), "siparis-islemleri.onerilen-firma-siparisleri.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SiparisIslemleri > OnerilenFirmaSiparisleri > Sayfa yetkisi.", "OnerilenFirmaSiparisleri Sayfa", null },
                    { new Guid("dd18c75f-8465-e9ef-0b41-18f813ef045b"), "duzeltme-islemleri.mikro-evrak-duzenleme.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "DuzeltmeIslemleri > MikroEvrakDuzenleme > Sayfa yetkisi.", "MikroEvrakDuzenleme Sayfa", null },
                    { new Guid("e0bdfae4-2d25-66ad-371b-0b06d8136d71"), "kasa-islemleri.kasa-cirolari.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > KasaCirolari > Sayfa yetkisi.", "KasaCirolari Sayfa", null },
                    { new Guid("e47c3063-6eff-69b3-20bd-360c6883d26f"), "mal-kabul-islemleri.mal-kabul-farklari.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "MalKabulIslemleri > MalKabulFarklari > Sayfa yetkisi.", "MalKabulFarklari Sayfa", null },
                    { new Guid("e77410c2-f282-f1e8-2042-99bf2601b157"), "stok-islemleri.virmanlar.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > Virmanlar > Sayfa yetkisi.", "Virmanlar Sayfa", null },
                    { new Guid("e896468c-32e5-8acb-83ee-665f1d8760ff"), "ortak-islemler.duyurular.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OrtakIslemler > Duyurular > Sayfa yetkisi.", "Duyurular Sayfa", null },
                    { new Guid("f1e0195b-9a62-771e-f078-43fee8edae47"), "fatura-islemleri.fatura-gonderimi.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "FaturaIslemleri > FaturaGonderimi > Sayfa yetkisi.", "FaturaGonderimi Sayfa", null },
                    { new Guid("f3599c4c-879c-28d7-aba4-2169958a9e29"), "entegrasyon-islemleri.pos-muhasebe-aktarimi.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "EntegrasyonIslemleri > PosMuhasebeAktarimi > Sayfa yetkisi.", "PosMuhasebeAktarimi Sayfa", null },
                    { new Guid("f4bf89d6-18d4-3c2e-e823-c669f8e2d1a5"), "rapor-islemleri.stok-raporlari.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "RaporIslemleri > StokRaporlari > Sayfa yetkisi.", "StokRaporlari Sayfa", null },
                    { new Guid("fb412304-8589-7855-82a4-f99e22683823"), "rapor-islemleri.tedarikci-performans-karnesi.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "RaporIslemleri > TedarikciPerformansKarnesi > Sayfa yetkisi.", "TedarikciPerformansKarnesi Sayfa", null },
                    { new Guid("fc076dbb-3485-feba-01d3-a637770b45dd"), "arama-islemleri.cari-bul.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AramaIslemleri > CariBul > Sayfa yetkisi.", "CariBul Sayfa", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("01ccdd13-642f-093a-e7d6-644197371cf7"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("03c12c1a-4a7e-a2d3-003e-2fcced777d1f"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("05f72094-0eb6-824a-148f-8d40af4653f9"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("0773b4fc-85a3-f397-2cd9-45884524e7cd"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("0a15de74-241e-0bc9-c37e-bffd65c7f731"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("0cd11f32-2124-80af-a5dc-b994ae8f4a49"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("1c60e631-936d-21e7-00da-c78c3a78f92a"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("2191161a-ab7c-6eb4-987c-2aef582544f0"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("32ae4589-1029-cc34-383e-661cc0818145"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("3b4a2843-71ae-df09-933c-49f5ac4ea835"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("3f39e294-bbe6-b256-b73c-a506d42ec0c8"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("421772b7-2615-d8e9-41f6-929c9a40e598"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("4623d3df-e5c4-b363-4860-ec1e098d9c53"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("4be114ac-4951-89c5-6ccb-9c5f183797b8"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("4e698b19-0149-6804-a7a2-78aefb8ca4c3"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("50aaf4c5-8bc1-1bd7-0509-a847714afa4a"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("5533112c-409d-a83f-97e0-c38faacd14d6"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("570442a8-9f68-b03b-5974-0012a8b3c717"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("59d687ba-460a-c168-14f6-8bb53a4e751e"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("5ba83241-0500-9699-6f8d-49d40f369dd0"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("62365491-f5eb-997d-1718-f7e73b95b2a6"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("678c4ec7-9d0c-5126-a4c8-e9693a064712"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("6d904388-7d7c-19f1-b408-605bcb83890f"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("700f4861-1efd-1882-2904-bf9c876565a6"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("70805ff4-2179-ba22-e5e4-820aa8859302"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("7153c74a-616d-3ec7-7288-139e9c68b9c2"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("719de8b9-05de-e6c0-bc48-97e07a6a7b32"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("770ac54a-d04b-022f-f374-e6e4ae852994"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("81cacb51-a3a9-3f87-0231-863027047ea2"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("884f6e0f-cd9d-098a-1eab-5a21282e6822"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("917d1298-01bc-4681-c26c-b8621bb8cdf3"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("93f6c941-fd4b-7b0c-7796-ebfe961d19f4"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("96551c29-f958-5c4a-015a-304fe4fcb227"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a47f8de0-c5e0-a8e1-22cf-3b299c65f4fc"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a6419da9-86be-45e1-6384-616080de59f5"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("acfa8f71-c395-5cf4-1ce5-b70660895d5a"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("b0e29b8f-09e0-d2e1-d7dc-61b5eea8aadb"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("b60daace-b236-78c4-c9c2-98f0352ef0cf"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("b9a54bbf-1945-0d54-e0ae-9f0be6947d37"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("bb84b117-1010-cc33-2103-9c929682c779"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("bc9aea79-db23-96ad-d11c-e83d663c78ae"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("c361dd06-daaa-51ae-74ff-824040132546"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("c3fb33f7-9293-58a3-791f-163fe2083b1a"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("c4d3f8e8-2eec-fa95-e5c6-b4df3a1a39c4"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("c5a5bee9-ddfc-a1ae-f186-aadbc19a95cc"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca4ee4f9-ead1-213d-0ee4-4113ddaa4a22"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca7ab614-9c8d-bd71-9cb6-f60eafcb9083"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d3b6fe33-da2d-c373-c1a9-1f4e06e845b6"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("dd18c75f-8465-e9ef-0b41-18f813ef045b"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("e0bdfae4-2d25-66ad-371b-0b06d8136d71"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("e47c3063-6eff-69b3-20bd-360c6883d26f"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("e77410c2-f282-f1e8-2042-99bf2601b157"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("e896468c-32e5-8acb-83ee-665f1d8760ff"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("f1e0195b-9a62-771e-f078-43fee8edae47"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("f3599c4c-879c-28d7-aba4-2169958a9e29"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("f4bf89d6-18d4-3c2e-e823-c669f8e2d1a5"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("fb412304-8589-7855-82a4-f99e22683823"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("fc076dbb-3485-feba-01d3-a637770b45dd"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.Sql("""
                INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                SELECT pagePermission.[id], sourceRole.[role_id], MIN(sourceRole.[assigned_at_utc])
                FROM [app_permissions] AS pagePermission
                INNER JOIN [app_permissions] AS actionPermission
                    ON actionPermission.[code] LIKE LEFT(pagePermission.[code], LEN(pagePermission.[code]) - LEN(N'.page')) + N'.%'
                    AND actionPermission.[code] <> pagePermission.[code]
                    AND actionPermission.[code] NOT LIKE N'%.page'
                    AND actionPermission.[code] NOT LIKE N'%.manage'
                INNER JOIN [app_role_permissions] AS sourceRole
                    ON sourceRole.[permission_id] = actionPermission.[id]
                WHERE pagePermission.[code] LIKE N'%.page'
                    AND NOT EXISTS
                    (
                        SELECT 1
                        FROM [app_role_permissions] AS existing
                        WHERE existing.[permission_id] = pagePermission.[id]
                            AND existing.[role_id] = sourceRole.[role_id]
                    )
                GROUP BY pagePermission.[id], sourceRole.[role_id];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE rolePermission
                FROM [app_role_permissions] AS rolePermission
                INNER JOIN [app_permissions] AS permission
                    ON permission.[id] = rolePermission.[permission_id]
                WHERE permission.[code] LIKE N'%.page'
                    OR permission.[code] IN
                    (
                        N'green-grocer.product-case-profiles.manage',
                        N'ayar-islemleri.cihazlar.manage',
                        N'ayar-islemleri.sube-ayarlari.manage',
                        N'ayar-islemleri.kasa-pos-terminalleri.manage',
                        N'ayar-islemleri.kasiyerler.manage'
                    );
                """);

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01ccdd13-642f-093a-e7d6-644197371cf7"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("03c12c1a-4a7e-a2d3-003e-2fcced777d1f"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("05f72094-0eb6-824a-148f-8d40af4653f9"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("0773b4fc-85a3-f397-2cd9-45884524e7cd"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("0a15de74-241e-0bc9-c37e-bffd65c7f731"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("0cd11f32-2124-80af-a5dc-b994ae8f4a49"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("1c60e631-936d-21e7-00da-c78c3a78f92a"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("2191161a-ab7c-6eb4-987c-2aef582544f0"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("32ae4589-1029-cc34-383e-661cc0818145"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("3b4a2843-71ae-df09-933c-49f5ac4ea835"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("3f39e294-bbe6-b256-b73c-a506d42ec0c8"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("421772b7-2615-d8e9-41f6-929c9a40e598"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("4623d3df-e5c4-b363-4860-ec1e098d9c53"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("4be114ac-4951-89c5-6ccb-9c5f183797b8"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("4e698b19-0149-6804-a7a2-78aefb8ca4c3"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("50aaf4c5-8bc1-1bd7-0509-a847714afa4a"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("5533112c-409d-a83f-97e0-c38faacd14d6"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("570442a8-9f68-b03b-5974-0012a8b3c717"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("59d687ba-460a-c168-14f6-8bb53a4e751e"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("5ba83241-0500-9699-6f8d-49d40f369dd0"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("62365491-f5eb-997d-1718-f7e73b95b2a6"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("678c4ec7-9d0c-5126-a4c8-e9693a064712"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("6d904388-7d7c-19f1-b408-605bcb83890f"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("700f4861-1efd-1882-2904-bf9c876565a6"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("70805ff4-2179-ba22-e5e4-820aa8859302"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("7153c74a-616d-3ec7-7288-139e9c68b9c2"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("719de8b9-05de-e6c0-bc48-97e07a6a7b32"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("770ac54a-d04b-022f-f374-e6e4ae852994"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("81cacb51-a3a9-3f87-0231-863027047ea2"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("884f6e0f-cd9d-098a-1eab-5a21282e6822"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("917d1298-01bc-4681-c26c-b8621bb8cdf3"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("93f6c941-fd4b-7b0c-7796-ebfe961d19f4"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("96551c29-f958-5c4a-015a-304fe4fcb227"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("a47f8de0-c5e0-a8e1-22cf-3b299c65f4fc"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("a6419da9-86be-45e1-6384-616080de59f5"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("acfa8f71-c395-5cf4-1ce5-b70660895d5a"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("b0e29b8f-09e0-d2e1-d7dc-61b5eea8aadb"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("b60daace-b236-78c4-c9c2-98f0352ef0cf"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("b9a54bbf-1945-0d54-e0ae-9f0be6947d37"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("bb84b117-1010-cc33-2103-9c929682c779"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("bc9aea79-db23-96ad-d11c-e83d663c78ae"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("c361dd06-daaa-51ae-74ff-824040132546"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("c3fb33f7-9293-58a3-791f-163fe2083b1a"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("c4d3f8e8-2eec-fa95-e5c6-b4df3a1a39c4"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("c5a5bee9-ddfc-a1ae-f186-aadbc19a95cc"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("ca4ee4f9-ead1-213d-0ee4-4113ddaa4a22"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("ca7ab614-9c8d-bd71-9cb6-f60eafcb9083"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("d3b6fe33-da2d-c373-c1a9-1f4e06e845b6"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("dd18c75f-8465-e9ef-0b41-18f813ef045b"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("e0bdfae4-2d25-66ad-371b-0b06d8136d71"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("e47c3063-6eff-69b3-20bd-360c6883d26f"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("e77410c2-f282-f1e8-2042-99bf2601b157"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("e896468c-32e5-8acb-83ee-665f1d8760ff"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("f1e0195b-9a62-771e-f078-43fee8edae47"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("f3599c4c-879c-28d7-aba4-2169958a9e29"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("f4bf89d6-18d4-3c2e-e823-c669f8e2d1a5"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("fb412304-8589-7855-82a4-f99e22683823"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("fc076dbb-3485-feba-01d3-a637770b45dd"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("01ccdd13-642f-093a-e7d6-644197371cf7"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("03c12c1a-4a7e-a2d3-003e-2fcced777d1f"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("05f72094-0eb6-824a-148f-8d40af4653f9"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("0773b4fc-85a3-f397-2cd9-45884524e7cd"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("0a15de74-241e-0bc9-c37e-bffd65c7f731"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("0cd11f32-2124-80af-a5dc-b994ae8f4a49"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("1c60e631-936d-21e7-00da-c78c3a78f92a"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("2191161a-ab7c-6eb4-987c-2aef582544f0"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("32ae4589-1029-cc34-383e-661cc0818145"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("3b4a2843-71ae-df09-933c-49f5ac4ea835"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("3f39e294-bbe6-b256-b73c-a506d42ec0c8"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("421772b7-2615-d8e9-41f6-929c9a40e598"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("4623d3df-e5c4-b363-4860-ec1e098d9c53"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("4be114ac-4951-89c5-6ccb-9c5f183797b8"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("4e698b19-0149-6804-a7a2-78aefb8ca4c3"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("50aaf4c5-8bc1-1bd7-0509-a847714afa4a"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("5533112c-409d-a83f-97e0-c38faacd14d6"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("570442a8-9f68-b03b-5974-0012a8b3c717"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("59d687ba-460a-c168-14f6-8bb53a4e751e"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("5ba83241-0500-9699-6f8d-49d40f369dd0"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("62365491-f5eb-997d-1718-f7e73b95b2a6"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("678c4ec7-9d0c-5126-a4c8-e9693a064712"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("6d904388-7d7c-19f1-b408-605bcb83890f"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("700f4861-1efd-1882-2904-bf9c876565a6"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("70805ff4-2179-ba22-e5e4-820aa8859302"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("7153c74a-616d-3ec7-7288-139e9c68b9c2"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("719de8b9-05de-e6c0-bc48-97e07a6a7b32"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("770ac54a-d04b-022f-f374-e6e4ae852994"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("81cacb51-a3a9-3f87-0231-863027047ea2"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("884f6e0f-cd9d-098a-1eab-5a21282e6822"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("917d1298-01bc-4681-c26c-b8621bb8cdf3"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("93f6c941-fd4b-7b0c-7796-ebfe961d19f4"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("96551c29-f958-5c4a-015a-304fe4fcb227"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("a47f8de0-c5e0-a8e1-22cf-3b299c65f4fc"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("a6419da9-86be-45e1-6384-616080de59f5"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("acfa8f71-c395-5cf4-1ce5-b70660895d5a"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("b0e29b8f-09e0-d2e1-d7dc-61b5eea8aadb"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("b60daace-b236-78c4-c9c2-98f0352ef0cf"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("b9a54bbf-1945-0d54-e0ae-9f0be6947d37"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("bb84b117-1010-cc33-2103-9c929682c779"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("bc9aea79-db23-96ad-d11c-e83d663c78ae"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("c361dd06-daaa-51ae-74ff-824040132546"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("c3fb33f7-9293-58a3-791f-163fe2083b1a"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("c4d3f8e8-2eec-fa95-e5c6-b4df3a1a39c4"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("c5a5bee9-ddfc-a1ae-f186-aadbc19a95cc"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("ca4ee4f9-ead1-213d-0ee4-4113ddaa4a22"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("ca7ab614-9c8d-bd71-9cb6-f60eafcb9083"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("d3b6fe33-da2d-c373-c1a9-1f4e06e845b6"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("dd18c75f-8465-e9ef-0b41-18f813ef045b"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("e0bdfae4-2d25-66ad-371b-0b06d8136d71"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("e47c3063-6eff-69b3-20bd-360c6883d26f"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("e77410c2-f282-f1e8-2042-99bf2601b157"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("e896468c-32e5-8acb-83ee-665f1d8760ff"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("f1e0195b-9a62-771e-f078-43fee8edae47"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("f3599c4c-879c-28d7-aba4-2169958a9e29"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("f4bf89d6-18d4-3c2e-e823-c669f8e2d1a5"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("fb412304-8589-7855-82a4-f99e22683823"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("fc076dbb-3485-feba-01d3-a637770b45dd"));
        }
    }
}
